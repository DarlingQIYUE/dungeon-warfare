using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Sniper round (A31Aim). Flies to a single target and on arrival deals a heavy
    /// direct hit, then marks the target "vulnerable" via <see cref="EnemyStatus"/> so
    /// it takes amplified damage from every source for a while. Damage is dealt BEFORE
    /// the mark is (re)applied, so a clean target's first hit is normal and the team
    /// reaps the amplification afterwards. Reuses the base flight; overrides the hit.
    /// The hit damage uses the tower's <c>damage</c>; the mark's amp/duration live in
    /// <see cref="DebugTuning"/> for live tweaking.
    /// </summary>
    public class AimProjectile : Projectile
    {
        protected override void Detonate()
        {
            if (target == null || target.IsDead) return;

            target.TakeDamage(damage); // dealt first, then the mark is applied below

            if (target.TryGetComponent(out EnemyStatus status))
                status.ApplyVulnerability(DebugTuning.VulnerabilityAmp, DebugTuning.VulnerabilityDuration);
        }
    }
}
