using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Chain-lightning strike (A32Lighting). Hitscan, not a flying shell: it resolves
    /// instantly on launch. The arc runs from the tower to the first target, then hops
    /// to the nearest not-yet-hit enemy within <see cref="DebugTuning.ChainJumpRange"/>,
    /// up to <see cref="DebugTuning.ChainJumps"/> extra times, each hop dealing
    /// ×<see cref="DebugTuning.ChainFalloff"/> of the previous damage. Every hit goes
    /// through <see cref="Health.TakeDamage"/>, so marked (vulnerable) enemies on the
    /// chain take amplified damage automatically. The visual is the same bolt drawn for
    /// every link, including the tower→first-target segment.
    /// </summary>
    public class LightningProjectile : Projectile
    {
        // Hitscan: strike the moment we're launched, then remove ourselves.
        public override void Launch(Health targetHealth, float damageAmount)
        {
            base.Launch(targetHealth, damageAmount);
            Detonate();
            Destroy(gameObject);
        }

        protected override void Detonate()
        {
            var hit = new HashSet<Health>();
            var points = new List<Vector3> { transform.position }; // arc starts at the tower

            float dmg = damage;
            Vector3 pos = transform.position;

            // First link: the aimed target.
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(dmg);
                hit.Add(target);
                pos = target.transform.position;
                points.Add(pos);
            }

            // Subsequent hops to the nearest fresh enemy within jump range.
            int jumps = Mathf.Max(0, DebugTuning.ChainJumps);
            for (int i = 0; i < jumps; i++)
            {
                Health next = NearestUnhit(pos, DebugTuning.ChainJumpRange, hit);
                if (next == null) break;

                dmg *= DebugTuning.ChainFalloff;
                next.TakeDamage(dmg);
                hit.Add(next);
                pos = next.transform.position;
                points.Add(pos);
            }

            if (points.Count >= 2) LightningFx.Spawn(points);
        }

        private static Health NearestUnhit(Vector3 from, float range, HashSet<Health> hit)
        {
            Health best = null;
            float bestSqr = float.MaxValue;

            foreach (Collider2D col in Physics2D.OverlapCircleAll(from, range))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;
                if (!col.TryGetComponent(out Health h) || h.IsDead || hit.Contains(h)) continue;

                float sqr = ((Vector3)col.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = h; }
            }
            return best;
        }
    }
}
