namespace DungeonWarfare
{
    /// <summary>
    /// Live-tunable knobs for prototyping the cannon/knockback feel. Edited in-game via
    /// <see cref="DebugTuningPanel"/> and read by the relevant components. Values reset
    /// each play session — once a feel is dialed in, bake it back into the prefab/builder.
    /// </summary>
    public static class DebugTuning
    {
        public static float KnockStrength = 0.9f;   // impulse per hit (before falloff)
        public static float KnockSpread = 40f;     // ± degrees of scatter for splash hits
        public static float ExplosionRadius = 1f;  // AoE radius (world units)
        public static float KnockDecay = 6f;       // how fast knockback settles
        public static float WallSlamPerSpeed = 2f; // wall-slam damage per unit impact speed

        // --- A31Injection: poison / DOT --- (direct hit damage lives on the tower, like the cannon)
        public static float PoisonPerStackDps = 10f;    // damage-per-second contributed by each stack
        public static int PoisonMaxStacks = 10;         // stack cap (full stacks => max dps)
        public static float PoisonDropInterval = 1.2f;  // seconds between losing a stack once unhit

        // --- A31Aim: sniper vulnerability mark --- (hit damage lives on the tower)
        public static float VulnerabilityAmp = 0.4f;      // +40% damage taken while marked
        public static float VulnerabilityDuration = 4f;   // mark duration (s), refreshed on hit
    }
}
