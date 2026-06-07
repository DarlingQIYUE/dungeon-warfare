using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// A grid-placed tower. Periodically finds the nearest enemy within range
    /// and fires a projectile at it. Carries a gold cost used by the placer.
    /// </summary>
    public class Tower : MonoBehaviour
    {
        [SerializeField] private int cost = 30;
        [SerializeField] private float range = 4f;
        [SerializeField] private float fireInterval = 0.6f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle; // optional spawn point; defaults to self

        public int Cost => cost;
        public float Range => range;
        public float Damage => damage;
        public float FireInterval => fireInterval;

        private float cooldown;

        private void Awake()
        {
            if (projectilePrefab == null)
                projectilePrefab = Resources.Load<Projectile>("Projectile");
        }

        private void Update()
        {
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            Health target = FindNearestEnemy();
            if (target == null) return;

            cooldown = fireInterval;
            Fire(target);
        }

        private Health FindNearestEnemy()
        {
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, range);
            Health best = null;
            float bestSqr = float.MaxValue;

            foreach (Collider2D col in cols)
            {
                if (!col.TryGetComponent(out Enemy _)) continue;
                if (!col.TryGetComponent(out Health health) || health.IsDead) continue;

                float sqr = (health.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
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
