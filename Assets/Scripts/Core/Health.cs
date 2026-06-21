using System;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>Anything that can be damaged.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
        bool IsDead { get; }
    }

    /// <summary>Generic health pool. Raises events on damage and death.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;

        /// <summary>
        /// Multiplier applied to all incoming damage (1 = normal). Driven by debuffs
        /// such as the sniper's vulnerability mark, so every damage source (towers,
        /// poison, wall-slam) is amplified through one central hook.
        /// </summary>
        public float DamageTakenMultiplier { get; set; } = 1f;

        /// <summary>Fired once when health first reaches zero.</summary>
        public event Action<Health> Died;
        /// <summary>Fired every time damage is applied (amount > 0).</summary>
        public event Action<Health, float> DamageTaken;

        private void Awake() => Current = maxHealth;

        public void Configure(float newMax)
        {
            maxHealth = newMax;
            Current = newMax;
        }

        public void ResetHealth() => Current = maxHealth;

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            amount *= DamageTakenMultiplier; // amplified by vulnerability / armor-break debuffs

            Current = Mathf.Max(0f, Current - amount);
            DamageTaken?.Invoke(this, amount);

            if (IsDead) Died?.Invoke(this);
        }
    }
}
