using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// The whole playfield as a grid. Converts world &lt;-&gt; cell, enforces
    /// bounds, tracks tower occupancy, and knows which cells are the enemy path
    /// (the road) so towers can't be built on it. Spawns colored tiles so the
    /// road and buildable cells are visible at runtime.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        // 28x18 cells of 0.5 units => a 14x9 play area (matches the 14:9 region
        // left of the sidebar). Origin centers the grid on world (0,0).
        [SerializeField] private Vector2 origin = new(-7f, -4.5f); // bottom-left corner of cell (0,0)
        [SerializeField] private float cellSize = 0.5f;
        [SerializeField] private int columns = 28;
        [SerializeField] private int rows = 18;

        [Tooltip("The set of road cells (any shape). Order doesn't matter — the route is found by BFS.")]
        [SerializeField] private Vector2Int[] pathCells;
        [SerializeField] private Vector2Int entryCell; // where enemies spawn
        [SerializeField] private Vector2Int exitCell;  // where they leave (cost a life)

        [Header("Visual (optional)")]
        [SerializeField] private Sprite tileSprite;
        [SerializeField] private Color buildableColor = new(0.40f, 0.60f, 0.90f, 0.14f);
        [SerializeField] private Color pathColor = new(0.45f, 0.36f, 0.26f, 0.85f);

        private readonly HashSet<Vector2Int> towers = new();  // cells with a tower
        private readonly HashSet<Vector2Int> blocked = new(); // terrain (blocks pathfinding, buildable on top)
        private HashSet<Vector2Int> pathSet;

        /// <summary>Raised when the walkable map changes (terrain added/cleared).</summary>
        public event System.Action PathsChanged;

        public float CellSize => cellSize;
        public int Columns => columns;
        public int Rows => rows;
        public int PathCellCount => pathCells != null ? pathCells.Length : 0;
        public Vector2Int EntryCell => entryCell;
        public Vector2Int ExitCell => exitCell;
        public Vector3 EntryWorld => CellToWorldCenter(entryCell);

        private HashSet<Vector2Int> PathSet
        {
            get
            {
                if (pathSet == null)
                {
                    pathSet = new HashSet<Vector2Int>();
                    if (pathCells != null)
                        foreach (Vector2Int c in pathCells) pathSet.Add(c);
                }
                return pathSet;
            }
        }

        private void Start()
        {
            if (tileSprite != null) BuildVisualTiles();
        }

        private void OnValidate() => pathSet = null; // refresh cache when edited

        public Vector2Int WorldToCell(Vector3 world)
        {
            int cx = Mathf.FloorToInt((world.x - origin.x) / cellSize);
            int cy = Mathf.FloorToInt((world.y - origin.y) / cellSize);
            return new Vector2Int(cx, cy);
        }

        public Vector3 CellToWorldCenter(Vector2Int cell)
        {
            float x = origin.x + (cell.x + 0.5f) * cellSize;
            float y = origin.y + (cell.y + 0.5f) * cellSize;
            return new Vector3(x, y, 0f);
        }

        public bool IsInside(Vector2Int cell) =>
            cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;

        public bool IsPathCell(Vector2Int cell) => PathSet.Contains(cell);
        public bool HasTower(Vector2Int cell) => towers.Contains(cell);

        /// <summary>
        /// A tower can go on buildable ground (off the road) OR on placed terrain
        /// (road that's been covered), as long as no tower is there yet.
        /// </summary>
        public bool CanPlace(Vector2Int cell) =>
            IsInside(cell) && (!IsPathCell(cell) || IsBlocked(cell)) && !HasTower(cell);

        public void AddTower(Vector2Int cell) => towers.Add(cell);
        public void RemoveTower(Vector2Int cell) => towers.Remove(cell);

        // ---- terrain / walkability ----

        public bool IsBlocked(Vector2Int cell) => blocked.Contains(cell);

        /// <summary>A cell enemies can walk on: it's road and not covered by terrain.</summary>
        public bool IsWalkable(Vector2Int cell) => IsPathCell(cell) && !blocked.Contains(cell);

        /// <summary>Lay terrain on a road cell: block it and re-route enemies.</summary>
        public void AddTerrain(Vector2Int cell)
        {
            blocked.Add(cell);
            PathsChanged?.Invoke();
        }

        /// <summary>Remove terrain: un-block the cell and re-route enemies.</summary>
        public void RemoveTerrain(Vector2Int cell)
        {
            blocked.Remove(cell);
            PathsChanged?.Invoke();
        }

        /// <summary>Remove all towers + terrain (used on (re)start).</summary>
        public void ClearBuildings()
        {
            towers.Clear();
            blocked.Clear();
            PathsChanged?.Invoke();
        }

        // ---- pathfinding ----

        private static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        /// <summary>
        /// Shortest orthogonal path (BFS) from <paramref name="from"/> to the exit
        /// over walkable cells (road minus barricades). Empty list if unreachable.
        /// </summary>
        public List<Vector2Int> FindPathCells(Vector2Int from)
        {
            var result = new List<Vector2Int>();
            if (!IsWalkable(from) || !IsWalkable(exitCell)) return result;

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            bool found = from == exitCell;
            while (queue.Count > 0 && !found)
            {
                Vector2Int cur = queue.Dequeue();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cur + step;
                    if (visited.Contains(next) || !IsWalkable(next)) continue;
                    visited.Add(next);
                    cameFrom[next] = cur;
                    if (next == exitCell) { found = true; break; }
                    queue.Enqueue(next);
                }
            }

            if (!found) return result;

            result.Add(exitCell);
            Vector2Int c = exitCell;
            while (c != from) { c = cameFrom[c]; result.Add(c); }
            result.Reverse();
            return result;
        }

        /// <summary>Route from the entry as world points (for preview / initial spawn).</summary>
        public List<Vector3> ComputeWorldPath()
        {
            var cells = FindPathCells(entryCell);
            var pts = new List<Vector3>(cells.Count);
            foreach (Vector2Int cell in cells) pts.Add(CellToWorldCenter(cell));
            return pts;
        }

        /// <summary>
        /// All cells from which the exit is still reachable, optionally pretending
        /// <paramref name="extraBlock"/> is also barricaded. Used to validate that a
        /// new barricade won't seal off the exit. (One flood-fill from the exit.)
        /// </summary>
        public HashSet<Vector2Int> ReachableToExit(Vector2Int? extraBlock = null)
        {
            var reachable = new HashSet<Vector2Int>();
            if (!IsWalkable(exitCell) || (extraBlock.HasValue && extraBlock.Value == exitCell))
                return reachable;

            var queue = new Queue<Vector2Int>();
            reachable.Add(exitCell);
            queue.Enqueue(exitCell);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cur + step;
                    if (reachable.Contains(next) || !IsWalkable(next)) continue;
                    if (extraBlock.HasValue && next == extraBlock.Value) continue;
                    reachable.Add(next);
                    queue.Enqueue(next);
                }
            }
            return reachable;
        }

        private void BuildVisualTiles()
        {
            var parent = new GameObject("GridTiles").transform;
            parent.SetParent(transform, false);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    var cell = new Vector2Int(x, y);
                    bool isPath = IsPathCell(cell);

                    var tile = new GameObject($"Cell_{x}_{y}");
                    tile.transform.SetParent(parent, false);
                    tile.transform.position = CellToWorldCenter(cell);
                    // Fill the whole cell so tiles sit flush with no gaps.
                    tile.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                    Color color = isPath ? pathColor : buildableColor;
                    if (cell == entryCell) color = new Color(0.30f, 0.80f, 0.40f, 0.9f); // entry = green
                    else if (cell == exitCell) color = new Color(0.85f, 0.30f, 0.30f, 0.9f); // exit = red

                    var sr = tile.AddComponent<SpriteRenderer>();
                    sr.sprite = tileSprite;
                    sr.color = color;
                    sr.sortingOrder = isPath ? 0 : -1;
                }
            }
        }

        private void OnDrawGizmos()
        {
            float width = columns * cellSize;
            float height = rows * cellSize;

            // road cells
            Gizmos.color = pathColor;
            if (pathCells != null)
                foreach (Vector2Int c in pathCells)
                    Gizmos.DrawCube(CellToWorldCenter(c), new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.1f));

            // entry (green) / exit (red)
            Gizmos.color = new Color(0.3f, 0.85f, 0.4f);
            Gizmos.DrawCube(CellToWorldCenter(entryCell), new Vector3(cellSize, cellSize, 0.1f));
            Gizmos.color = new Color(0.9f, 0.3f, 0.3f);
            Gizmos.DrawCube(CellToWorldCenter(exitCell), new Vector3(cellSize, cellSize, 0.1f));

            // current best route (yellow)
            List<Vector3> route = ComputeWorldPath();
            Gizmos.color = Color.yellow;
            for (int i = 0; i < route.Count - 1; i++)
                Gizmos.DrawLine(route[i], route[i + 1]);

            // grid lines
            Gizmos.color = new Color(0.40f, 0.60f, 0.90f, 0.5f);
            for (int x = 0; x <= columns; x++)
            {
                float px = origin.x + x * cellSize;
                Gizmos.DrawLine(new Vector3(px, origin.y, 0f), new Vector3(px, origin.y + height, 0f));
            }
            for (int y = 0; y <= rows; y++)
            {
                float py = origin.y + y * cellSize;
                Gizmos.DrawLine(new Vector3(origin.x, py, 0f), new Vector3(origin.x + width, py, 0f));
            }
        }
    }
}
