using System;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Run economy: gold (to build towers) and lives (lost when enemies escape).
    /// Game-state/flow lives in <see cref="GameFlow"/>; this is purely numbers.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private int startingGold = 100;
        [SerializeField] private int startingLives = 20;

        public int Gold { get; private set; }
        public int Lives { get; private set; }
        public int StartingLives => startingLives;
        public int EnemiesKilled { get; private set; }

        public event Action<int> GoldChanged;
        public event Action<int> LivesChanged;
        public event Action LivesDepleted;

        private void Awake()
        {
            Instance = this;
            ResetForNewGame();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Restore starting gold/lives (used when a level (re)starts).</summary>
        public void ResetForNewGame()
        {
            Gold = startingGold;
            Lives = startingLives;
            EnemiesKilled = 0;
            GoldChanged?.Invoke(Gold);
            LivesChanged?.Invoke(Lives);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Gold < amount) return false;
            Gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            GoldChanged?.Invoke(Gold);
        }

        public void OnEnemyKilled(int reward)
        {
            EnemiesKilled++;
            AddGold(reward);
        }

        public void OnEnemyReachedBase(int damage)
        {
            if (Lives <= 0) return;

            Lives = Mathf.Max(0, Lives - Mathf.Max(0, damage));
            LivesChanged?.Invoke(Lives);

            if (Lives <= 0) LivesDepleted?.Invoke();
        }
    }
}
