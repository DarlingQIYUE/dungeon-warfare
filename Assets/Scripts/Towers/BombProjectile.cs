using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Cannon shell (A31Bomb). Flies to the target point like a normal projectile,
    /// but on arrival explodes: every enemy inside the blast radius takes the full
    /// damage and is knocked back, with the impulse fading toward the edge. The aimed
    /// enemy is pushed straight back; splash enemies fly out radially. Values live in
    /// <see cref="DebugTuning"/> so they can be tweaked at runtime.
    /// </summary>
    public class BombProjectile : Projectile
    {
        [SerializeField] private Sprite explosionSprite; // defaults to the shell's own sprite

        // Knockback/AoE values are read from DebugTuning so they can be tweaked live.

        protected override void Detonate()
        {
            Vector3 center = transform.position;
            float radius = DebugTuning.ExplosionRadius;
            SpawnFx(center, radius);

            foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;

                col.TryGetComponent(out Health health);
                if (health != null && !health.IsDead) health.TakeDamage(damage);

                if (col.TryGetComponent(out PathFollower follower))
                {
                    float dist = Vector2.Distance(center, col.transform.position);
                    float falloff = Mathf.Clamp01(1f - dist / radius);
                    float strength = DebugTuning.KnockStrength * falloff;

                    // The aimed enemy sits at the blast centre (radial is undefined), so push
                    // it straight back along its path; everyone else is knocked radially out.
                    if (health != null && health == target)
                        follower.AddKnockbackBackward(strength);
                    else
                        follower.AddKnockback(center, strength, DebugTuning.KnockSpread);
                }
            }
        }

        private void SpawnFx(Vector3 center, float radius)
        {
            Sprite s = explosionSprite;
            if (s == null && TryGetComponent(out SpriteRenderer sr)) s = sr.sprite;
            if (s != null) ExplosionFx.Spawn(center, radius, s);
        }
    }
}
