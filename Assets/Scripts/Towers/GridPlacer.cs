using System.Collections.Generic;
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

        [SerializeField] private Tower[] towerPrefabs; // buildable tower types (auto-listed in the UI)
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
        private int selectedTowerIndex; // which entry of towerPrefabs is being placed

        // ---- build API ----
        public IReadOnlyList<Tower> AvailableTowers => towerPrefabs;
        public Terrain AvailableTerrain => terrainPrefab;
        public bool IsPlacing => kind != BuildKind.None;
        public bool IsPlacingTower => kind == BuildKind.Tower;
        public bool IsPlacingTerrain => kind == BuildKind.Terrain;

        /// <summary>The tower prefab currently chosen for placement (null if none).</summary>
        public Tower ActiveTower =>
            towerPrefabs != null && selectedTowerIndex >= 0 && selectedTowerIndex < towerPrefabs.Length
                ? towerPrefabs[selectedTowerIndex] : null;
        /// <summary>True when placing the tower at index <paramref name="i"/> (for button highlight).</summary>
        public bool IsSelectedTower(int i) => kind == BuildKind.Tower && selectedTowerIndex == i;

        public void SelectTower(int i) { ClearSelection(); kind = BuildKind.Tower; selectedTowerIndex = i; }
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
        /// <summary>The selected cell's tower (null if a terrain block is selected).</summary>
        public Tower SelectedTower =>
            hasSelection && grid.HasTower(selectedCell) ? FindAtCell<Tower>(selectedCell) : null;

        public int SelectedRefund
        {
            get
            {
                if (!hasSelection) return 0;
                Tower t = SelectedTower;
                int cost = grid.HasTower(selectedCell) ? (t != null ? t.Cost : 0) : terrainPrefab.Cost;
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
            towerPrefabs = ResolveTowerPrefabs(towerPrefabs);
            if (terrainPrefab == null) terrainPrefab = Resources.Load<Terrain>("Terrain");
        }

        /// <summary>
        /// Keep only the valid wired tower entries; if none survive, load the known
        /// towers from Resources. The build wires prefab references unreliably (hence
        /// the Resources-as-source-of-truth note in the scene builder), and an array
        /// of null entries would otherwise leave the build menu empty.
        /// </summary>
        private static Tower[] ResolveTowerPrefabs(Tower[] wired)
        {
            var list = new List<Tower>();
            if (wired != null)
                foreach (Tower t in wired)
                    if (t != null) list.Add(t);

            if (list.Count == 0)
            {
                Tower basic = Resources.Load<Tower>("Tower");
                Tower bomb = Resources.Load<Tower>("BombTower");
                Tower injection = Resources.Load<Tower>("InjectionTower");
                Tower aim = Resources.Load<Tower>("AimTower");
                Tower lightning = Resources.Load<Tower>("LightningTower");
                Tower laser = Resources.Load<Tower>("LaserTower");
                if (basic != null) list.Add(basic);
                if (bomb != null) list.Add(bomb);
                if (injection != null) list.Add(injection);
                if (aim != null) list.Add(aim);
                if (lightning != null) list.Add(lightning);
                if (laser != null) list.Add(laser);
            }
            return list.ToArray();
        }

        private void Start()
        {
            CreateGhost();
            CreateRangeDisc();
        }

        private void Update()
        {
            if (grid == null || cam == null || Mouse.current == null) return;

            // The ESC pause menu hard-blocks interaction. The space "time-stop"
            // freezes the world (timeScale 0) but still lets the player build/sell,
            // so we deliberately do NOT bail just because time is stopped.
            if (GameFlow.Instance != null && GameFlow.Instance.ModalMenuOpen)
            {
                if (ghost != null) ghost.enabled = false;
                HideRange();
                return;
            }

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
            Tower prefab = ActiveTower;
            if (prefab == null) { HideRange(); return; }

            bool affordable = game == null || game.Gold >= prefab.Cost;
            bool valid = grid.CanPlace(cell) && affordable;

            UpdateGhost(cell, valid);

            if (grid.IsInside(cell))
                ShowRangeAt(grid.CellToWorldCenter(cell), prefab.Range);
            else
                HideRange();

            if (valid && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (game == null || game.TrySpend(prefab.Cost))
                {
                    Instantiate(prefab, grid.CellToWorldCenter(cell), Quaternion.identity);
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
            Tower at = grid.IsInside(rangeCell) && grid.HasTower(rangeCell) ? FindAtCell<Tower>(rangeCell) : null;
            if (at != null)
                ShowRangeAt(grid.CellToWorldCenter(rangeCell), at.Range);
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
