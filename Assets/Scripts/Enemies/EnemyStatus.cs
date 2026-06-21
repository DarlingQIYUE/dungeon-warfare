using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Holds the temporary combat effects riding on an enemy. Three channels:
    ///  - Poison: stacking damage-over-time (A31Injection).
    ///  - Vulnerability: a timed "marked" debuff that amplifies ALL incoming damage
    ///    via <see cref="Health.DamageTakenMultiplier"/> (A31Aim sniper).
    ///  - Slow: a timed movement-speed reduction read by <see cref="PathFollower"/>
    ///    (A33Poison toxic cone). Strongest-wins, refreshed on hit, hard on/off, capped.
    ///
    /// Poison: each hit adds a stack (capped). Current DPS = stacks × per-stack DPS,
    /// applied continuously via <see cref="Health.TakeDamage"/>. Once the enemy stops
    /// being hit, it sheds one stack every <see cref="DebugTuning.PoisonDropInterval"/>
    /// seconds. Vulnerability: a fixed amp, refreshed on hit, that expires after a set
    /// duration. The sprite tints to show its state (vulnerable takes display priority).
    /// Tuning values live in <see cref="DebugTuning"/> so they can be tweaked live.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyStatus : MonoBehaviour
    {
        private const float MaxSlow = 0.8f; // never slow past 80% (100% = stun, reserved for later)

        private static readonly Color PoisonColor = new(0.3f, 1f, 0.3f);      // green
        private static readonly Color VulnerableColor = new(0.85f, 0.3f, 1f); // purple "exposed"
        private static readonly Color SlowColor = new(0.4f, 0.8f, 1f);        // cyan "bogged down"

        private Health health;
        private SpriteRenderer body;
        private Color baseColor;

        private int poisonStacks;
        private float dropTimer; // counts down to shedding the next stack

        private float vulnAmp;   // current damage-amp fraction (0 = none)
        private float vulnTimer; // counts down; clears the mark at zero

        private float slowFactor; // current movement reduction (0 = none)
        private float slowTimer;  // counts down; clears the slow at zero

        /// <summary>Movement-speed multiplier for <see cref="PathFollower"/> (1 = normal).</summary>
        public float SpeedMultiplier => slowTimer > 0f ? 1f - Mathf.Min(slowFactor, MaxSlow) : 1f;

        private void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<SpriteRenderer>();
            if (body != null) baseColor = body.color;
        }

        // Reused enemy instances (if ever pooled) start clean.
        private void OnEnable()
        {
            poisonStacks = 0;
            dropTimer = 0f;
            vulnAmp = 0f;
            vulnTimer = 0f;
            slowFactor = 0f;
            slowTimer = 0f;
            if (health != null) health.DamageTakenMultiplier = 1f;
            if (body != null) body.color = baseColor;
        }

        /// <summary>Add one poison stack (capped) and reset the shed timer.</summary>
        public void AddPoisonStack()
        {
            poisonStacks = Mathf.Min(poisonStacks + 1, Mathf.Max(1, DebugTuning.PoisonMaxStacks));
            dropTimer = DebugTuning.PoisonDropInterval;
        }

        /// <summary>Mark the enemy vulnerable: incoming damage ×(1+amp) for the duration (refreshes).</summary>
        public void ApplyVulnerability(float amp, float duration)
        {
            vulnAmp = Mathf.Max(vulnAmp, amp); // fixed model: a single source, take the strongest
            vulnTimer = duration;
        }

        /// <summary>Slow the enemy by <paramref name="factor"/> (0..1) for the duration (strongest wins, refreshes).</summary>
        public void ApplySlow(float factor, float duration)
        {
            slowFactor = Mathf.Max(slowFactor, factor);
            slowTimer = duration;
        }

        private void Update()
        {
            if (health == null || health.IsDead) return;

            UpdateVulnerability(); // set the damage multiplier BEFORE poison ticks, so poison is amplified too
            UpdatePoison();
            UpdateSlow();
            UpdateTint();
        }

        private void UpdateSlow()
        {
            if (slowTimer > 0f)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0f) slowFactor = 0f;
            }
        }

        private void UpdateVulnerability()
        {
            if (vulnTimer > 0f)
            {
                vulnTimer -= Time.deltaTime;
                if (vulnTimer <= 0f) vulnAmp = 0f;
            }
            health.DamageTakenMultiplier = 1f + vulnAmp;
        }

        private void UpdatePoison()
        {
            if (poisonStacks <= 0) return;

            // Continuous DOT: stacks × per-stack DPS, accumulated each frame.
            health.TakeDamage(poisonStacks * DebugTuning.PoisonPerStackDps * Time.deltaTime);

            // Shed a stack once the drop interval elapses with no fresh hit.
            dropTimer -= Time.deltaTime;
            if (dropTimer <= 0f)
            {
                poisonStacks--;
                dropTimer = DebugTuning.PoisonDropInterval;
            }
        }

        /// <summary>Display priority: vulnerable (purple) > poison (green) > slow (cyan) > base.</summary>
        private void UpdateTint()
        {
            if (body == null) return;

            if (vulnTimer > 0f) { body.color = Color.Lerp(baseColor, VulnerableColor, 0.7f); return; }

            if (poisonStacks > 0)
            {
                float t = Mathf.Clamp01((float)poisonStacks / Mathf.Max(1, DebugTuning.PoisonMaxStacks));
                body.color = Color.Lerp(baseColor, PoisonColor, t);
                return;
            }

            if (slowTimer > 0f) { body.color = Color.Lerp(baseColor, SlowColor, 0.6f); return; }

            body.color = baseColor;
        }
    }
}
