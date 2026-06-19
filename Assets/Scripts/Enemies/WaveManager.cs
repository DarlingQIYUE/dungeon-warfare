using System;
using System.Collections;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Runs a fixed number of waves. Each wave spawns a batch of enemies whose
    /// HP scales by <see cref="healthGrowth"/> per wave (e.g. 1.5x). A wave must
    /// be fully cleared (killed or leaked) before the next begins; clearing the
    /// last wave raises <see cref="AllWavesCleared"/> (the win condition).
    /// Driven by <see cref="GameFlow"/> — it does not start on its own.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private GridSystem grid;
        [SerializeField] private GameManager game;

        [Header("Waves")]
        [SerializeField] private int totalWaves = 8;
        [SerializeField] private int enemiesPerWave = 10;
        [SerializeField] private float baseHealth = 30f;
        [SerializeField] private float healthGrowth = 1.5f; // each wave = prev x this
        [SerializeField] private float spawnInterval = 0.8f;
        [SerializeField] private float betweenWaveDelay = 8f; // countdown length between waves
        [SerializeField] private int earlyStartGoldPerSecond = 5; // bonus per second skipped

        public int TotalWaves => totalWaves;
        public int CurrentWave { get; private set; }
        public int AliveCount { get; private set; }

        /// <summary>Before wave 1: waiting for the player to press start (no timer).</summary>
        public bool AwaitingStart { get; private set; }
        /// <summary>Between waves: counting down to the next one (can be started early).</summary>
        public bool CountingDown { get; private set; }
        /// <summary>Seconds left on the inter-wave countdown.</summary>
        public float Countdown { get; private set; }
        /// <summary>Gold the player would earn by starting the next wave right now.</summary>
        public int EarlyStartBonus => CountingDown ? Mathf.RoundToInt(Countdown * earlyStartGoldPerSecond) : 0;

        public event Action AllWavesCleared;
        public event Action WaveChanged;
        /// <summary>Fired when an early start is rewarded, with the gold granted.</summary>
        public event Action<int> EarlyStartRewarded;

        private bool startRequested; // player pressed "start" / "start next wave early"

        /// <summary>Open the current wave gate: begin wave 1, or skip the countdown.</summary>
        public void RequestNextWave() => startRequested = true;

        private void Awake()
        {
            if (enemyPrefab == null) enemyPrefab = Resources.Load<Enemy>("Enemy");
            if (grid == null) grid = FindFirstObjectByType<GridSystem>();
            if (game == null) game = GameManager.Instance;
        }

        public void Begin()
        {
            StopAllCoroutines();
            CurrentWave = 0;
            AliveCount = 0;
            AwaitingStart = false;
            CountingDown = false;
            Countdown = 0f;
            startRequested = false;
            WaveChanged?.Invoke();
            StartCoroutine(RunWaves());
        }

        public void Stop()
        {
            StopAllCoroutines();
            AwaitingStart = false;
            CountingDown = false;
            Countdown = 0f;
        }

        private IEnumerator RunWaves()
        {
            for (int wave = 1; wave <= totalWaves; wave++)
            {
                // The gate (countdown) opens as soon as the previous wave finished
                // spawning — NOT after it's cleared — so the next wave can be started
                // (or auto-start) while the previous wave's enemies are still alive.
                yield return WaveGate(wave); // wave 1: wait for start; later: countdown (skippable)

                CurrentWave = wave;
                WaveChanged?.Invoke();

                float hp = baseHealth * Mathf.Pow(healthGrowth, wave - 1);
                for (int i = 0; i < enemiesPerWave; i++)
                {
                    SpawnEnemy(hp);
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            // All waves spawned; the level is won once the board is finally clear.
            while (AliveCount > 0) yield return null;
            AllWavesCleared?.Invoke();
        }

        /// <summary>
        /// Hold before a wave. Wave 1 waits for the player to press start (no timer);
        /// later waves count down and auto-start. Starting a countdown early grants a
        /// gold bonus proportional to the time skipped.
        /// </summary>
        private IEnumerator WaveGate(int wave)
        {
            startRequested = false;

            if (wave == 1)
            {
                AwaitingStart = true;
                while (!startRequested) yield return null;
                AwaitingStart = false;
                yield break;
            }

            CountingDown = true;
            Countdown = betweenWaveDelay;
            while (Countdown > 0f && !startRequested)
            {
                Countdown -= Time.deltaTime;
                yield return null;
            }

            // Started early (timer not yet expired) -> reward the skipped seconds.
            if (startRequested && Countdown > 0f)
            {
                int bonus = Mathf.RoundToInt(Countdown * earlyStartGoldPerSecond);
                if (bonus > 0)
                {
                    if (game != null) game.AddGold(bonus);
                    EarlyStartRewarded?.Invoke(bonus);
                }
            }

            CountingDown = false;
            Countdown = 0f;
        }

        private void SpawnEnemy(float hp)
        {
            if (enemyPrefab == null || grid == null) return;

            Enemy enemy = Instantiate(enemyPrefab, grid.EntryWorld, Quaternion.identity);
            AliveCount++;
            enemy.Despawned += OnEnemyDespawned;
            enemy.Spawn(grid, game, hp);
        }

        private void OnEnemyDespawned(Enemy enemy)
        {
            enemy.Despawned -= OnEnemyDespawned;
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }
    }
}
