using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Flies toward a target's current position and deals damage on contact.
    /// If the target dies/disappears mid-flight, it continues to the last known
    /// spot and despawns harmlessly. A lifetime cap prevents stray projectiles.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 11f;
        [SerializeField] private float maxLifetime = 3f;

        private float damage;
        private Health target;
        private Vector3 lastKnownPos;
        private float life;

        public void Launch(Health targetHealth, float damageAmount)
        {
            target = targetHealth;
            damage = damageAmount;
            life = maxLifetime;
            if (target != null) lastKnownPos = target.transform.position;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            if (target != null && !target.IsDead)
                lastKnownPos = target.transform.position;

            Vector3 toTarget = lastKnownPos - transform.position;
            float dist = toTarget.magnitude;
            float step = speed * Time.deltaTime;

            if (dist <= step || dist < 0.05f)
            {
                if (target != null && !target.IsDead)
                    target.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            transform.position += toTarget / dist * step;
        }
    }
}
