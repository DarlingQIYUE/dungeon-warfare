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
            Vector3 aim = ComputeAim();
            if (aim == Vector3.zero) return;
            float aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            PoisonWave.Spawn(transform.position, aimDeg, DebugTuning.PoisonConeAngle * 0.5f,
                             Range, Damage, DebugTuning.PoisonWaveWidth);
        }

        /// <summary>
        /// Aim so the fan's leading edge sits on the frontmost (closest-to-exit) enemy — the
        /// one most likely to slip away — and the fan opens toward the side with more enemies
        /// (i.e. back over the trailing pack). Bearings are predicted to where each enemy will
        /// be when the wave front reaches it (ease-out timing × base speed), so movers aren't
        /// missed. Returns <see cref="Vector3.zero"/> if no enemy is in range.
        /// </summary>
        private Vector3 ComputeAim()
        {
            float cone = DebugTuning.PoisonConeAngle;
            float total = Range + DebugTuning.PoisonWaveWidth; // front travels to maxRange+width

            var bearings = new List<float>();
            float leaderBearing = 0f;
            float leaderDist = float.MaxValue;

            foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, Range))
            {
                if (!col.TryGetComponent(out Enemy enemy)) continue;
                if (!col.TryGetComponent(out Health h) || h.IsDead) continue;

                Vector3 to = col.transform.position - transform.position;
                to.z = 0f;
                float d = to.magnitude;
                if (d < 1e-4f) continue;

                // Invert the wave's ease-out (front = total·(1-(1-t')²)) to get arrival time,
                // then lead at the enemy's BASE speed (a slow may wear off before it arrives).
                float e = Mathf.Clamp01(d / total);
                float t = PoisonWave.ExpandTime * (1f - Mathf.Sqrt(1f - e));
                Vector3 vel = col.TryGetComponent(out PathFollower pf) ? pf.BaseVelocity : Vector3.zero;
                Vector3 predicted = col.transform.position + vel * t - transform.position;
                predicted.z = 0f;
                if (predicted.sqrMagnitude < 1e-6f) continue;

                float bearing = Mathf.Atan2(predicted.y, predicted.x) * Mathf.Rad2Deg;
                bearings.Add(bearing);

                if (enemy.DistanceToExit < leaderDist) { leaderDist = enemy.DistanceToExit; leaderBearing = bearing; }
            }

            // Same leader-anchored opening as the flame, but over predicted bearings.
            return AnchorAim(bearings, leaderBearing, cone);
        }
    }
}
