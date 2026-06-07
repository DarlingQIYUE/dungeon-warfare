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
        [SerializeField] private float startDelay = 1f;
        [SerializeField] private float betweenWaveDelay = 2.5f;

        public int TotalWaves => totalWaves;
        public int CurrentWave { get; private set; }
        public int AliveCount { get; private set; }

        public event Action AllWavesCleared;
        public event Action WaveChanged;

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
            WaveChanged?.Invoke();
            StartCoroutine(RunWaves());
        }

        public void Stop() => StopAllCoroutines();

        private IEnumerator RunWaves()
        {
            yield return new WaitForSeconds(startDelay);

            for (int wave = 1; wave <= totalWaves; wave++)
            {
                CurrentWave = wave;
                WaveChanged?.Invoke();

                float hp = baseHealth * Mathf.Pow(healthGrowth, wave - 1);
                for (int i = 0; i < enemiesPerWave; i++)
                {
                    SpawnEnemy(hp);
                    yield return new WaitForSeconds(spawnInterval);
                }

                // Wait until this wave is fully gone before starting the next.
                while (AliveCount > 0) yield return null;

                if (wave < totalWaves)
                    yield return new WaitForSeconds(betweenWaveDelay);
            }

            AllWavesCleared?.Invoke();
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
