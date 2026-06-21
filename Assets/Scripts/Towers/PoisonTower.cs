using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Toxic fan-wave tower (A33Poison). Short range; on each shot it emits an expanding
    /// 120° fan-ring (<see cref="PoisonWave"/>) aimed at the in-range enemy nearest the
    /// exit. The wave sweeps outward and slows + lightly damages every enemy its front
    /// crosses. Reuses the base cooldown + targeting; only the "shot" is replaced by a
    /// wave instead of a projectile. Cone width / slow / dps live in DebugTuning.
    /// </summary>
    public class PoisonTower : Tower
    {
        protected override void Fire(Health target)
        {
            Vector3 aim = target.transform.position - transform.position;
            aim.z = 0f;
            float aimDeg = aim.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            PoisonWave.Spawn(transform.position, aimDeg, DebugTuning.PoisonConeAngle * 0.5f,
                             Range, Damage, DebugTuning.PoisonWaveWidth);
        }
    }
}
