using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonWarfare
{
    /// <summary>
    /// Grid-snapped placement for two building kinds:
    ///  - Tower: on buildable ground OR on placed terrain; shows range while placing.
    ///  - Terrain: on the road, blocks pathfinding (and becomes buildable). Rejected
    ///    if it would crush an enemy or seal the exit off from the entry/any enemy.
    /// A green/red ghost follows the cursor. Left-click builds (if affordable),
    /// right-click cancels. Uses the new Input System.
    /// </summary>
    public class GridPlacer : MonoBehaviour
    {
        private enum BuildKind { None, Tower, Terrain }

        [SerializeField] private Tower towerPrefab;
        [SerializeField] private Terrain terrainPrefab;
        [SerializeField] private GridSystem grid;
        [SerializeField] private Camera cam;
        [SerializeField] private GameManager game;

        [Header("Ghost preview")]
        [SerializeField] private Sprite ghostSprite;
        [SerializeField] private Color okColor = new(0.30f, 1f, 0.30f, 0.45f);
        [SerializeField] private Color badColor = new(1f, 0.30f, 0.30f, 0.45f);

        [Header("Range preview")]
        [SerializeField] private Sprite rangeSprite; // a filled circle
        [SerializeField] private Color rangeColor = new(1f, 1f, 0.4f, 0.13f);

        [SerializeField, Range(0f, 1f)] private float refundRate = 0.6f;

        private SpriteRenderer ghost;
        private SpriteRenderer rangeDisc;
        private BuildKind kind = BuildKind.None;

        private bool hasSelection;
        private Vector2Int selectedCell;

        // ---- build API ----
        public Tower AvailableTower => towerPrefab;
        public Terrain AvailableTerrain => terrainPrefab;
        public bool IsPlacing => kind != BuildKind.None;
        public bool IsPlacingTower => kind == BuildKind.Tower;
        public bool IsPlacingTerrain => kind == BuildKind.Terrain;
        public void SelectTower() { ClearSelection(); kind = BuildKind.Tower; }
        public void SelectTerrain() { ClearSelection(); kind = BuildKind.Terrain; }

        public void CancelPlacement()
        {
            kind = BuildKind.None;
            if (ghost != null) ghost.enabled = false;
            HideRange();
        }

        // ---- selection / removal API ----
        public bool HasSelection => hasSelection;
        public string SelectedLabel =>
            !hasSelection ? "" : grid.HasTower(selectedCell) ? "炮塔" : "地形";
        public int SelectedRefund
        {
            get
            {
                if (!hasSelection) return 0;
                int cost = grid.HasTower(selectedCell) ? towerPrefab.Cost : terrainPrefab.Cost;
                return Mathf.FloorToInt(cost * refundRate);
            }
        }

        public void ClearSelection()
        {
            hasSelection = false;
            HideRange();
        }

        /// <summary>Remove the top building on the selected cell (tower before terrain).</summary>
        public void RemoveSelected()
        {
            if (!hasSelection) return;
            Vector2Int cell = selectedCell;

            if (grid.HasTower(cell))
            {
                Tower t = FindAtCell<Tower>(cell);
                if (t != null) { Refund(t.Cost); Destroy(t.gameObject); }
                grid.RemoveTower(cell);
            }
            else if (grid.IsBlocked(cell))
            {
                Terrain ter = FindAtCell<Terrain>(cell);
                if (ter != null) { Refund(ter.Cost); Destroy(ter.gameObject); }
                grid.RemoveTerrain(cell); // re-opens the road -> enemies re-path
            }
            ClearSelection();
        }

        private void Refund(int cost)
        {
            if (game != null) game.AddGold(Mathf.FloorToInt(cost * refundRate));
        }

        private T FindAtCell<T>(Vector2Int cell) where T : Component
        {
            foreach (T c in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (grid.WorldToCell(c.transform.position) == cell) return c;
            return null;
        }

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (game == null) game = GameManager.Instance;
            if (grid == null) grid = FindFirstObjectByType<GridSystem>();
            if (towerPrefab == null) towerPrefab = Resources.Load<Tower>("Tower");
            if (terrainPrefab == null) terrainPrefab = Resources.Load<Terrain>("Terrain");
        }

        private void Start()
        {
            CreateGhost();
            CreateRangeDisc();
        }

        private void Update()
        {
            if (grid == null || cam == null || Mouse.current == null) return;
            if (Time.timeScale == 0f) return; // paused

            bool playing = GameFlow.Instance == null || GameFlow.Instance.IsPlaying;
            if (!playing)
            {
                kind = BuildKind.None;
                hasSelection = false;
                if (ghost != null) ghost.enabled = false;
                HideRange();
                return;
            }

            if (kind != BuildKind.None && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            Vector3 world = ScreenToWorld();
            Vector2Int cell = grid.WorldToCell(world);

            switch (kind)
            {
                case BuildKind.Tower: UpdateTowerPlacement(cell); break;
                case BuildKind.Terrain: UpdateTerrainPlacement(cell); break;
                default: UpdateIdle(world); break;
            }
        }

        // ---- tower placement ----

        private void UpdateTowerPlacement(Vector2Int cell)
        {
            bool affordable = game == null || game.Gold >= towerPrefab.Cost;
            bool valid = grid.CanPlace(cell) && affordable;

            UpdateGhost(cell, valid);

            if (grid.IsInside(cell))
                ShowRangeAt(grid.CellToWorldCenter(cell), towerPrefab.Range);
            else
                HideRange();

            if (valid && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (game == null || game.TrySpend(towerPrefab.Cost))
                {
                    Instantiate(towerPrefab, grid.CellToWorldCenter(cell), Quaternion.identity);
                    grid.AddTower(cell);
                }
            }
        }

        // ---- terrain placement ----

        private void UpdateTerrainPlacement(Vector2Int cell)
        {
            HideRange(); // terrain has no range

            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            bool affordable = game == null || game.Gold >= terrainPrefab.Cost;
            bool valid = affordable && CanPlaceTerrain(cell, enemies);

            UpdateGhost(cell, valid);

            if (valid && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (game == null || game.TrySpend(terrainPrefab.Cost))
                {
                    Instantiate(terrainPrefab, grid.CellToWorldCenter(cell), Quaternion.identity);
                    grid.AddTerrain(cell); // blocks the cell + re-routes every enemy
                }
            }
        }

        private bool CanPlaceTerrain(Vector2Int cell, Enemy[] enemies)
        {
            if (!grid.IsInside(cell) || !grid.IsPathCell(cell)) return false; // must be road
            if (grid.IsBlocked(cell)) return false;                           // terrain already here
            if (cell == grid.EntryCell || cell == grid.ExitCell) return false;

            // don't crush an enemy standing on / walking into the cell
            foreach (Enemy e in enemies)
                if (e.CurrentCell == cell || e.TargetCell == cell) return false;

            // placing it must not seal off the exit for the entry or any enemy
            var reachable = grid.ReachableToExit(cell);
            if (!reachable.Contains(grid.EntryCell)) return false;
            foreach (Enemy e in enemies)
                if (!reachable.Contains(e.CurrentCell) || !reachable.Contains(e.TargetCell))
                    return false;

            return true;
        }

        // ---- idle: click a building to select it (for removal); hover shows range ----

        private void UpdateIdle(Vector3 world)
        {
            if (ghost != null) ghost.enabled = false;

            Vector2Int cell = grid.WorldToCell(world);

            // Only react to clicks INSIDE the grid, so clicking UI (sidebar, the
            // remove button) never deselects the building you just picked.
            if (Mouse.current.leftButton.wasPressedThisFrame && grid.IsInside(cell))
            {
                if (grid.HasTower(cell) || grid.IsBlocked(cell))
                {
                    hasSelection = true;
                    selectedCell = cell;
                }
                else ClearSelection();
            }
            if (Mouse.current.rightButton.wasPressedThisFrame)
                ClearSelection();

            // Show range for the selected tower (persistent), else the hovered one.
            Vector2Int rangeCell = hasSelection ? selectedCell : cell;
            if (grid.IsInside(rangeCell) && grid.HasTower(rangeCell))
                ShowRangeAt(grid.CellToWorldCenter(rangeCell), towerPrefab.Range);
            else
                HideRange();
        }

        // ---- shared helpers ----

        private Vector3 ScreenToWorld()
        {
            Vector3 screen = Mouse.current.position.ReadValue();
            screen.z = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        private void CreateGhost()
        {
            if (ghostSprite == null) return;

            var go = new GameObject("PlacementGhost");
            ghost = go.AddComponent<SpriteRenderer>();
            ghost.sprite = ghostSprite;
            ghost.sortingOrder = 5;
            float s = (grid != null ? grid.CellSize : 1f) * 0.9f;
            go.transform.localScale = new Vector3(s, s, 1f);
        }

        private void UpdateGhost(Vector2Int cell, bool valid)
        {
            if (ghost == null) return;

            bool show = grid.IsInside(cell);
            ghost.enabled = show;
            if (!show) return;

            ghost.transform.position = grid.CellToWorldCenter(cell);
            ghost.color = valid ? okColor : badColor;
        }

        private void CreateRangeDisc()
        {
            if (rangeSprite == null) return;

            var go = new GameObject("RangeDisc");
            rangeDisc = go.AddComponent<SpriteRenderer>();
            rangeDisc.sprite = rangeSprite;
            rangeDisc.color = rangeColor;
            rangeDisc.sortingOrder = 1; // above tiles, below units
            rangeDisc.enabled = false;
        }

        private void ShowRangeAt(Vector3 position, float range)
        {
            if (rangeDisc == null) return;

            rangeDisc.enabled = true;
            rangeDisc.transform.position = position;
            float diameter = range * 2f; // circle sprite is 1 unit across
            rangeDisc.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        private void HideRange()
        {
            if (rangeDisc != null) rangeDisc.enabled = false;
        }
    }
}
