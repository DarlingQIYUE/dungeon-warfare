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
        public float Damage => damage;
        public float FireInterval => fireInterval;

        // Live tuning (DebugTuningPanel): adjust combat stats at runtime, then bake
        // the dialed-in values back into the prefab via the scene builder.
        public void SetRange(float value) => range = value;
        public void SetDamage(float value) => damage = value;
        public void SetFireInterval(float value) => fireInterval = value;

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

            cooldown = fireInterval;
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

        private void Fire(Health target)
        {
            Vector3 spawn = muzzle != null ? muzzle.position : transform.position;

            if (projectilePrefab != null)
            {
                Projectile shot = Instantiate(projectilePrefab, spawn, Quaternion.identity);
                shot.Launch(target, damage);
            }
            else
            {
                target.TakeDamage(damage); // hitscan fallback if no projectile prefab
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
