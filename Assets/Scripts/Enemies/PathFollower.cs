using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Moves the enemy through continuous space along the grid's current best
    /// route to the exit. The route (a list of cell centers from BFS) is only a
    /// *guide*: each frame the enemy aims at a "carrot" point <see cref="lookahead"/>
    /// units further along the route and walks straight toward it, so its motion
    /// is free-direction (it cuts corners) rather than locked to the grid axes.
    /// When the walkable map changes (a barricade is placed/removed), it re-routes
    /// in real time from the cell it's currently heading into.
    /// </summary>
    public class PathFollower : MonoBehaviour
    {
        [SerializeField] private float speed = 2f;

        [Tooltip("How far ahead on the route the enemy aims, in world units. " +
                 "Larger = rounds corners more / moves straighter; smaller = hugs the route.")]
        [SerializeField] private float lookahead = 0.35f;

        [Tooltip("Draw the straightened route, the projection point, and the carrot " +
                 "as gizmos (enable Gizmos in the Game view to see them at runtime).")]
        [SerializeField] private bool debugDraw = true;

        private GridSystem grid;
        private readonly List<Vector3> path = new(); // route as world-space cell centers
        private int seg;                             // segment we're on: path[seg] -> path[seg+1]

        private Vector3 debugProj;   // where we're projected onto the route
        private Vector3 debugCarrot; // the carrot we're steering toward

        public bool ReachedEnd { get; private set; }

        /// <summary>The cell the enemy is physically over right now.</summary>
        public Vector2Int CurrentCell => grid != null ? grid.WorldToCell(transform.position) : default;

        /// <summary>The cell the enemy is about to walk into (roughly one cell ahead on
        /// the route). Waypoints are sparse after straightening, so this is sampled by
        /// distance rather than read off the next waypoint.</summary>
        public Vector2Int TargetCell
        {
            get
            {
                if (grid == null || path.Count < 2) return CurrentCell;
                return grid.WorldToCell(PointAhead(transform.position, grid.CellSize));
            }
        }

        public void Initialize(GridSystem gridSystem)
        {
            grid = gridSystem;
            ReachedEnd = false;

            SetPath(grid.FindAnyAnglePath(grid.EntryCell));
            if (path.Count > 0) transform.position = path[0];
            seg = 0;

            grid.PathsChanged -= Recompute;
            grid.PathsChanged += Recompute;
        }

        private void OnDisable()
        {
            if (grid != null) grid.PathsChanged -= Recompute;
        }

        /// <summary>
        /// Re-route from the cell the enemy is standing in (never the one being blocked —
        /// placement validation forbids blocking <see cref="CurrentCell"/>). Anchoring at
        /// the current cell keeps the new route's start under the enemy, so it can't
        /// beeline across a wall / the freshly placed terrain to catch a route that
        /// started a cell ahead. The enemy's real position is prepended so the very first
        /// segment runs from where it actually is, inside its (walkable) cell.
        /// </summary>
        public void Recompute()
        {
            if (grid == null || ReachedEnd) return;

            var pts = grid.FindAnyAnglePath(CurrentCell);
            if (pts.Count == 0) pts = grid.FindAnyAnglePath(TargetCell); // fallback
            if (pts.Count == 0) return; // unreachable (shouldn't happen; placement is validated)

            SetPath(pts);
            path.Insert(0, transform.position);
            seg = 0;
        }

        private void SetPath(List<Vector3> worldPoints)
        {
            path.Clear();
            path.AddRange(worldPoints);
        }

        private void Update()
        {
            if (grid == null || ReachedEnd) return;
            if (path.Count == 0) { ReachedEnd = true; return; }

            Vector3 pos = transform.position;

            if (path.Count == 1)
            {
                transform.position = Vector3.MoveTowards(pos, path[0], speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, path[0]) < 0.04f) ReachedEnd = true;
                return;
            }

            // 1) Slide our segment marker forward to the closest part of the route
            //    (forward-only, so cutting a corner never snaps us backward).
            AdvanceSegment(pos);

            // 2) Aim at a point `lookahead` further along the route and walk to it.
            Vector3 carrot = PointAhead(pos, lookahead);
            transform.position = Vector3.MoveTowards(pos, carrot, speed * Time.deltaTime);

            if (debugDraw)
            {
                DistanceToSegment(pos, path[seg], path[seg + 1], out float t);
                debugProj = Vector3.Lerp(path[seg], path[seg + 1], t);
                debugCarrot = carrot;
            }

            // 3) Finished once we're on the last segment and close to the exit point.
            if (seg >= path.Count - 2 && Vector3.Distance(transform.position, path[^1]) < 0.04f)
                ReachedEnd = true;
        }

        /// <summary>
        /// Advance <see cref="seg"/> to the nearest segment within a short forward
        /// window. Forward-only (cutting a corner never snaps us backward) and
        /// windowed (the route can fold back on itself in the maze region without
        /// us teleporting onto the return leg).
        /// </summary>
        private void AdvanceSegment(Vector3 pos)
        {
            const int window = 4;
            float best = float.MaxValue;
            int bestSeg = seg;
            int end = Mathf.Min(seg + window, path.Count - 2);
            for (int i = seg; i <= end; i++)
            {
                float d = DistanceToSegment(pos, path[i], path[i + 1], out _);
                if (d < best) { best = d; bestSeg = i; }
            }
            seg = bestSeg;
        }

        /// <summary>The point <paramref name="dist"/> units along the route, measured from
        /// the projection of pos onto the current segment.</summary>
        private Vector3 PointAhead(Vector3 pos, float dist)
        {
            DistanceToSegment(pos, path[seg], path[seg + 1], out float t);
            Vector3 a = Vector3.Lerp(path[seg], path[seg + 1], t);

            float remaining = dist;
            for (int i = seg; i < path.Count - 1; i++)
            {
                Vector3 b = path[i + 1];
                float len = Vector3.Distance(a, b);
                if (len >= remaining)
                    return Vector3.Lerp(a, b, remaining / Mathf.Max(len, 1e-5f));
                remaining -= len;
                a = b;
            }
            return path[^1];
        }

        /// <summary>Distance from p to segment a-b; outputs the clamped projection param t.</summary>
        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private void OnDrawGizmos()
        {
            if (!debugDraw || path.Count == 0) return;

            // straightened route (cyan): right angles here would mean string-pulling
            // did nothing; a diagonal means it cut the corner.
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.Count - 1; i++) Gizmos.DrawLine(path[i], path[i + 1]);
            foreach (Vector3 p in path) Gizmos.DrawWireSphere(p, 0.03f);

            if (!Application.isPlaying) return;

            // projection onto the route (yellow), carrot (magenta), aim line (white)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(debugProj, 0.05f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(debugCarrot, 0.06f);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, debugCarrot);
        }
    }
}
