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

        protected float damage;
        protected Health target;
        protected Vector3 lastKnownPos;
        private float life;

        public virtual void Launch(Health targetHealth, float damageAmount)
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
                Detonate();
                Destroy(gameObject);
                return;
            }

            transform.position += toTarget / dist * step;
        }

        /// <summary>Effect on reaching the target point. Base: single-target hit.</summary>
        protected virtual void Detonate()
        {
            if (target != null && !target.IsDead)
                target.TakeDamage(damage);
        }
    }
}
