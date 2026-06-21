using System.Collections.Generic;
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
            // Aim where the most enemies will BE when the wave front reaches them (the wave
            // takes time to travel, so aiming at current positions misses movers).
            Vector3 aim = BestAimOverAngles(PredictedBearings(), DebugTuning.PoisonConeAngle);
            if (aim == Vector3.zero) return;
            float aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            PoisonWave.Spawn(transform.position, aimDeg, DebugTuning.PoisonConeAngle * 0.5f,
                             Range, Damage, DebugTuning.PoisonWaveWidth);
        }

        /// <summary>
        /// Each in-range enemy's predicted bearing at the moment the wave front would reach
        /// its current distance (time from the wave's ease-out model × the enemy's velocity).
        /// </summary>
        private List<float> PredictedBearings()
        {
            var bearings = new List<float>();
            float total = Range + DebugTuning.PoisonWaveWidth; // front travels to maxRange+width

            foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, Range))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;
                if (!col.TryGetComponent(out Health h) || h.IsDead) continue;

                Vector3 to = col.transform.position - transform.position;
                to.z = 0f;
                float d = to.magnitude;
                if (d < 1e-4f) continue;

                // Invert the wave's ease-out (front = total·(1-(1-t')²)) to get arrival time.
                float e = Mathf.Clamp01(d / total);
                float t = PoisonWave.ExpandTime * (1f - Mathf.Sqrt(1f - e));

                Vector3 vel = col.TryGetComponent(out PathFollower pf) ? pf.BaseVelocity : Vector3.zero;
                Vector3 predicted = col.transform.position + vel * t - transform.position;
                predicted.z = 0f;
                if (predicted.sqrMagnitude < 1e-6f) continue;

                bearings.Add(Mathf.Atan2(predicted.y, predicted.x) * Mathf.Rad2Deg);
            }
            return bearings;
        }
    }
}
