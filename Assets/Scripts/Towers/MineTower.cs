using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Minelayer (A33Mine). Instead of shooting, every <c>fireInterval</c> it drops a
    /// <see cref="Mine"/> just BESIDE the enemies' route (a point on the trajectory within
    /// range, nudged sideways by a random offset up to the trigger radius) so the mine sits
    /// off to the side yet still catches enemies passing on the path. It prefers to keep
    /// <see cref="DebugTuning.MineMinSpacing"/> from existing mines but will let them pile
    /// up when the area is crowded. Holds up to <see cref="DebugTuning.MineMaxLive"/> live
    /// mines, refilling as they detonate. Blast damage = the tower's <c>damage</c>.
    /// </summary>
    public class MineTower : Tower
    {
        [SerializeField] private Mine minePrefab;

        private GridSystem grid;
        private readonly List<Vector3> route = new(); // the enemy trajectory (entry -> exit)
        private float placeTimer;
        private int liveMines;

        // Start (not Awake) so we don't collide with the base's private Awake.
        private void Start()
        {
            grid = FindFirstObjectByType<GridSystem>();
            if (minePrefab == null) minePrefab = Resources.Load<Mine>("Mine");

            RefreshRoute();
            if (grid != null) grid.PathsChanged += RefreshRoute; // re-cache when barricades reroute
        }

        private void OnDisable()
        {
            if (grid != null) grid.PathsChanged -= RefreshRoute;
        }

        private void RefreshRoute()
        {
            route.Clear();
            if (grid != null) route.AddRange(grid.FindAnyAnglePath(grid.EntryCell));
        }

        protected override void Update()
        {
            placeTimer -= Time.deltaTime;
            if (placeTimer > 0f) return;

            placeTimer = FireInterval;             // throttle attempts to one per interval
            if (liveMines >= DebugTuning.MineMaxLive) return;

            TryPlaceMine();
        }

        private void TryPlaceMine()
        {
            if (grid == null || minePrefab == null || route.Count < 2) return;

            // Sample points along the in-range route, each with its sideways (perpendicular)
            // direction, so we can nudge the mine off to the side of the path.
            var candidates = new List<(Vector3 point, Vector3 perp)>();
            const float step = 0.25f;
            float rangeSqr = Range * Range;

            for (int i = 0; i < route.Count - 1; i++)
            {
                Vector3 a = route[i], b = route[i + 1];
                Vector3 dir = b - a;
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();
                var perp = new Vector3(-dir.y, dir.x, 0f);

                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
                for (int k = 0; k <= n; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                    if ((p - transform.position).sqrMagnitude <= rangeSqr) candidates.Add((p, perp));
                }
            }

            if (candidates.Count == 0) return;

            Mine[] existing = FindObjectsByType<Mine>(FindObjectsSortMode.None);
            float maxOffset = DebugTuning.MineTriggerRadius; // stay within trigger reach of the path
            float spacingSqr = DebugTuning.MineMinSpacing * DebugTuning.MineMinSpacing;

            // Prefer a spot that respects spacing; if the area is crowded, fall back to the
            // last try so mines are allowed to pile up rather than refusing to place.
            Vector3 chosen = transform.position;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var c = candidates[Random.Range(0, candidates.Count)];
                Vector3 pos = c.point + c.perp * Random.Range(-maxOffset, maxOffset);
                pos.z = 0f;
                chosen = pos;
                if (!TooCloseToMine(pos, existing, spacingSqr)) break;
            }

            Mine mine = Instantiate(minePrefab, chosen, Quaternion.identity);
            mine.Init(this, Damage);
            liveMines++;
        }

        private static bool TooCloseToMine(Vector3 p, Mine[] mines, float spacingSqr)
        {
            foreach (Mine m in mines)
                if (m != null && (m.transform.position - p).sqrMagnitude < spacingSqr) return true;
            return false;
        }

        /// <summary>Called by a mine when it detonates, so the layer can top the field back up.</summary>
        public void MineCleared() => liveMines = Mathf.Max(0, liveMines - 1);
    }
}
