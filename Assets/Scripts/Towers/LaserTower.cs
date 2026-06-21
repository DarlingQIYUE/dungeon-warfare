using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Ramping laser (A32Lazer). Unlike the discrete-shot towers, it locks one target
    /// and burns it continuously: damage = base dps (the tower's <c>damage</c>) × a ramp
    /// multiplier that climbs from 1 to <see cref="DebugTuning.LaserRampMultiplier"/> over
    /// <see cref="DebugTuning.LaserRampTime"/> seconds of unbroken fire. Losing the target
    /// (it dies or leaves range) or switching to a new one resets the ramp — so it melts a
    /// single tanky target but is weak against streams it keeps re-acquiring. Damage runs
    /// through <see cref="Health.TakeDamage"/>, so a marked target takes amplified burn.
    /// </summary>
    public class LaserTower : Tower
    {
        private static readonly Color BeamCool = new(1f, 0.6f, 0.3f, 0.7f);  // dim orange
        private static readonly Color BeamHot = new(1f, 0.2f, 0.2f, 0.95f);  // bright red

        private LineRenderer beam;
        private Health locked;
        private float rampTimer; // seconds of continuous fire on the locked target

        // Start (not Awake) so we don't collide with the base's private Awake.
        private void Start()
        {
            var go = new GameObject("LaserBeam");
            go.transform.SetParent(transform, false);
            beam = go.AddComponent<LineRenderer>();
            beam.material = new Material(Shader.Find("Sprites/Default"));
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.numCapVertices = 2;
            beam.sortingOrder = 5;
            beam.enabled = false;
        }

        protected override void Update()
        {
            // Keep firing the locked target while it lives and stays in range; otherwise
            // acquire a new one (and reset the ramp, since the target changed).
            if (locked == null || locked.IsDead || !InRange(locked))
            {
                Health next = FindTarget();
                if (next != locked) rampTimer = 0f;
                locked = next;
            }

            if (locked == null)
            {
                rampTimer = 0f;
                if (beam != null) beam.enabled = false;
                return;
            }

            rampTimer = Mathf.Min(rampTimer + Time.deltaTime, DebugTuning.LaserRampTime);
            float t = DebugTuning.LaserRampTime > 0f ? rampTimer / DebugTuning.LaserRampTime : 1f;
            float mult = Mathf.Lerp(1f, DebugTuning.LaserRampMultiplier, t);

            locked.TakeDamage(Damage * mult * Time.deltaTime); // base dps = tower damage

            DrawBeam(locked.transform.position, t);
        }

        private bool InRange(Health h)
            => h != null && (h.transform.position - transform.position).sqrMagnitude <= Range * Range;

        private void DrawBeam(Vector3 targetPos, float t)
        {
            if (beam == null) return;

            beam.enabled = true;
            Vector3 a = transform.position; a.z = 0f;
            Vector3 b = targetPos; b.z = 0f;
            beam.SetPosition(0, a);
            beam.SetPosition(1, b);
            beam.widthMultiplier = Mathf.Lerp(0.04f, 0.12f, t); // thickens as it heats up
            Color c = Color.Lerp(BeamCool, BeamHot, t);
            beam.startColor = beam.endColor = c;
        }

        private void OnDestroy()
        {
            if (beam != null && beam.material != null) Destroy(beam.material);
        }
    }
}
