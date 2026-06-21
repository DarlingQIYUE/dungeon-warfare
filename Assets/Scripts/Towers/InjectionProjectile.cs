using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Virus-injection dart (A31Injection). Flies to a single target like a normal
    /// projectile, then on arrival deals a small instant hit and adds one poison
    /// stack via <see cref="EnemyStatus"/>. The poison (stacking DOT) is where most
    /// of this tower's damage comes from — reuse the base flight, override the hit.
    /// The small direct hit uses the tower's <c>damage</c> (like the cannon); the
    /// poison's parameters live in <see cref="DebugTuning"/> for live tweaking.
    /// </summary>
    public class InjectionProjectile : Projectile
    {
        protected override void Detonate()
        {
            if (target == null || target.IsDead) return;

            target.TakeDamage(damage); // small direct hit (tower's damage field)

            if (target.TryGetComponent(out EnemyStatus status))
                status.AddPoisonStack();
        }
    }
}
