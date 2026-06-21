using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Holds the temporary combat effects riding on an enemy. First version only
    /// implements poison (a stacking damage-over-time), used by A31Injection; a
    /// slow/stun channel can be added here later without touching movement.
    ///
    /// Poison: each hit adds a stack (capped). Current DPS = stacks × per-stack DPS,
    /// applied continuously via <see cref="Health.TakeDamage"/>. Once the enemy stops
    /// being hit, it sheds one stack every <see cref="DebugTuning.PoisonDropInterval"/>
    /// seconds until clear. The sprite tints greener the more stacks it carries.
    /// Tuning values live in <see cref="DebugTuning"/> so they can be tweaked live.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyStatus : MonoBehaviour
    {
        private Health health;
        private SpriteRenderer body;
        private Color baseColor;

        private int poisonStacks;
        private float dropTimer; // counts down to shedding the next stack

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
            if (body != null) body.color = baseColor;
        }

        /// <summary>Add one poison stack (capped) and reset the shed timer.</summary>
        public void AddPoisonStack()
        {
            poisonStacks = Mathf.Min(poisonStacks + 1, Mathf.Max(1, DebugTuning.PoisonMaxStacks));
            dropTimer = DebugTuning.PoisonDropInterval;
        }

        private void Update()
        {
            if (poisonStacks <= 0 || health == null || health.IsDead) return;

            // Continuous DOT: stacks × per-stack DPS, accumulated each frame.
            health.TakeDamage(poisonStacks * DebugTuning.PoisonPerStackDps * Time.deltaTime);

            // Shed a stack once the drop interval elapses with no fresh hit.
            dropTimer -= Time.deltaTime;
            if (dropTimer <= 0f)
            {
                poisonStacks--;
                dropTimer = DebugTuning.PoisonDropInterval;
            }

            UpdateTint();
        }

        /// <summary>Greener the more stacks are carried; back to base when clear.</summary>
        private void UpdateTint()
        {
            if (body == null) return;

            if (poisonStacks <= 0) { body.color = baseColor; return; }

            float t = Mathf.Clamp01((float)poisonStacks / Mathf.Max(1, DebugTuning.PoisonMaxStacks));
            body.color = Color.Lerp(baseColor, new Color(0.3f, 1f, 0.3f), t);
        }
    }
}
