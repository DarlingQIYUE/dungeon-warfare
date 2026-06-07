using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Moves the enemy cell-by-cell along the grid's current best route to the
    /// exit. When the walkable map changes (a barricade is placed/removed), it
    /// re-routes in real time from the cell it's currently heading to.
    /// </summary>
    public class PathFollower : MonoBehaviour
    {
        [SerializeField] private float speed = 2f;

        private GridSystem grid;
        private List<Vector2Int> cellPath = new();
        private int index;

        public bool ReachedEnd { get; private set; }

        /// <summary>The cell the enemy is physically over right now.</summary>
        public Vector2Int CurrentCell => grid != null ? grid.WorldToCell(transform.position) : default;

        /// <summary>The cell the enemy is currently walking toward.</summary>
        public Vector2Int TargetCell =>
            (cellPath != null && index >= 0 && index < cellPath.Count) ? cellPath[index] : CurrentCell;

        public void Initialize(GridSystem gridSystem)
        {
            grid = gridSystem;
            ReachedEnd = false;

            cellPath = grid.FindPathCells(grid.EntryCell);
            if (cellPath.Count > 0) transform.position = grid.CellToWorldCenter(cellPath[0]);
            index = cellPath.Count > 1 ? 1 : 0;

            grid.PathsChanged -= Recompute;
            grid.PathsChanged += Recompute;
        }

        private void OnDisable()
        {
            if (grid != null) grid.PathsChanged -= Recompute;
        }

        /// <summary>Re-route from the cell we're heading to (keeps motion smooth).</summary>
        public void Recompute()
        {
            if (grid == null || ReachedEnd) return;

            Vector2Int anchor = TargetCell;
            var newPath = grid.FindPathCells(anchor);
            if (newPath.Count == 0) return; // unreachable (shouldn't happen; placement is validated)

            cellPath = newPath;
            index = 0; // keep heading to `anchor` (newPath[0]), then follow the rest
        }

        private void Update()
        {
            if (grid == null || ReachedEnd) return;
            if (cellPath == null || index >= cellPath.Count) { ReachedEnd = true; return; }

            Vector3 target = grid.CellToWorldCenter(cellPath[index]);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target) < 0.02f)
            {
                index++;
                if (index >= cellPath.Count) ReachedEnd = true;
            }
        }
    }
}
