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

        // --- A32Lighting: chain lightning --- (first-hit damage lives on the tower)
        public static int ChainJumps = 3;          // extra hops after the first target
        public static float ChainFalloff = 0.7f;   // damage multiplier per hop
        public static float ChainJumpRange = 2.5f; // max distance between consecutive targets

        // --- A32Lazer: ramping laser beam --- (base dps = tower damage)
        public static float LaserRampTime = 3f;       // seconds of continuous fire to reach max
        public static float LaserRampMultiplier = 4f; // dps multiplier at full ramp

        // --- A32Veteran: kill-stacking growth --- (base stats live on the tower; both uncapped)
        public static float VeteranDamagePerKill = 1.5f;      // +damage per last-hit kill (uncapped)
        public static float VeteranFireReducePerKill = 0.01f; // fireInterval cut per kill, s (uncapped)

        // --- A33Poison: toxic fan-ring wave --- (per-wave damage = tower damage)
        public static float PoisonConeAngle = 120f;    // fan arc (degrees)
        public static float PoisonWaveWidth = 1f;      // radial thickness of the ring band (world units)
        public static float PoisonSlowFactor = 0.5f;   // movement reduction the wave applies (0..1)
        public static float PoisonSlowDuration = 1f;   // slow lingers this long after being swept
    }
}
