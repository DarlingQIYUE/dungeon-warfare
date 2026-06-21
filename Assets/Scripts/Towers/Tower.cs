using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// A grid-placed tower. Periodically targets the enemy within range that is
    /// closest to the exit (so the one about to leak is shot first) and fires a
    /// projectile at it. Carries a gold cost used by the placer.
    /// </summary>
    public class Tower : MonoBehaviour
    {
        [SerializeField] private string displayName = "炮塔";
        [SerializeField] private int cost = 30;
        [SerializeField] private float range = 4f;
        [SerializeField] private float fireInterval = 0.6f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle; // optional spawn point; defaults to self

        public string DisplayName => displayName;
        public int Cost => cost;
        public float Range => range;
        // Virtual so growth towers (e.g. the veteran) can layer a kill bonus on top
        // of the tunable base value.
        public virtual float Damage => damage;
        public virtual float FireInterval => fireInterval;

        // Live tuning (DebugTuningPanel): adjust combat stats at runtime, then bake
        // the dialed-in values back into the prefab via the scene builder.
        public void SetRange(float value) => range = value;
        public void SetDamage(float value) => damage = value;
        public void SetFireInterval(float value) => fireInterval = value;

        // Spawn point + projectile, exposed so subclasses can fire custom shots.
        protected Transform Muzzle => muzzle != null ? muzzle : transform;
        protected Projectile ProjectilePrefab => projectilePrefab;

        private float cooldown;

        private void Awake()
        {
            if (projectilePrefab == null)
                projectilePrefab = Resources.Load<Projectile>("Projectile");
        }

        protected virtual void Update()
        {
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            Health target = FindTarget();
            if (target == null) return;

            cooldown = FireInterval;
            Fire(target);
        }

        /// <summary>The in-range enemy closest to the exit (least route distance left).</summary>
        protected Health FindTarget()
        {
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, range);
            Health best = null;
            float bestDist = float.MaxValue;

            foreach (Collider2D col in cols)
            {
                if (!col.TryGetComponent(out Enemy enemy)) continue;
                if (!col.TryGetComponent(out Health health) || health.IsDead) continue;

                float dist = enemy.DistanceToExit;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = health;
                }
            }
            return best;
        }

        /// <summary>
        /// For cone weapons: the aim direction whose fan (width <paramref name="coneAngleDeg"/>)
        /// covers the MOST in-range enemies — so the cone catches as many as possible rather
        /// than centering on the frontmost one. Returns <see cref="Vector3.zero"/> if no enemy
        /// is in range. O(n²) over in-range enemies (n is small).
        /// </summary>
        protected Vector3 BestConeAim(float coneAngleDeg)
        {
            var angles = new List<float>();
            foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, range))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;
                if (!col.TryGetComponent(out Health h) || h.IsDead) continue;
                Vector3 to = col.transform.position - transform.position;
                if (to.sqrMagnitude < 1e-6f) continue;
                angles.Add(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            }
            return BestAimOverAngles(angles, coneAngleDeg);
        }

        /// <summary>
        /// Of the given enemy bearings (degrees), the aim direction whose fan of width
        /// <paramref name="coneAngleDeg"/> covers the most. <see cref="Vector3.zero"/> if empty.
        /// Subclasses can predict future bearings and pass them here for lead-aiming.
        /// </summary>
        protected static Vector3 BestAimOverAngles(List<float> angles, float coneAngleDeg)
        {
            if (angles.Count == 0) return Vector3.zero;

            // Slide a window of width coneAngleDeg starting at each bearing; the one
            // covering the most (with 360° wraparound) wins.
            int best = 0;
            float bestStart = angles[0];
            foreach (float start in angles)
            {
                int count = 0;
                foreach (float a in angles)
                    if (Mathf.Repeat(a - start, 360f) <= coneAngleDeg) count++;
                if (count > best) { best = count; bestStart = start; }
            }

            float aimDeg = (bestStart + coneAngleDeg * 0.5f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(aimDeg), Mathf.Sin(aimDeg), 0f);
        }

        protected virtual void Fire(Health target)
        {
            Vector3 spawn = Muzzle.position;

            if (ProjectilePrefab != null)
            {
                Projectile shot = Instantiate(ProjectilePrefab, spawn, Quaternion.identity);
                shot.Launch(target, Damage);
            }
            else
            {
                target.TakeDamage(Damage); // hitscan fallback if no projectile prefab
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
