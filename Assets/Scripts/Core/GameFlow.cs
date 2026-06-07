using System;
using UnityEngine;

namespace DungeonWarfare
{
    public enum GameState { MainMenu, LevelSelect, Playing, Won, Lost }

    /// <summary>
    /// Top-level game state machine: main menu -> level select -> playing ->
    /// won/lost -> (retry / menu). Starts the waves on level start, and listens
    /// for the win (all waves cleared) and lose (lives depleted) conditions.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [SerializeField] private GameManager game;
        [SerializeField] private WaveManager waves;
        [SerializeField] private GridSystem grid;

        public GameState State { get; private set; } = GameState.MainMenu;
        public bool IsPlaying => State == GameState.Playing;
        public event Action<GameState> StateChanged;

        private void Awake()
        {
            Instance = this;
            if (game == null) game = GameManager.Instance;
            if (waves == null) waves = FindFirstObjectByType<WaveManager>();
            if (grid == null) grid = FindFirstObjectByType<GridSystem>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (game != null) game.LivesDepleted += OnLivesDepleted;
            if (waves != null) waves.AllWavesCleared += OnAllWavesCleared;
        }

        private void OnDisable()
        {
            if (game != null) game.LivesDepleted -= OnLivesDepleted;
            if (waves != null) waves.AllWavesCleared -= OnAllWavesCleared;
        }

        private void Start() => SetState(GameState.MainMenu);

        // ---- transitions (called from UI) ----

        public void GoToLevelSelect() => SetState(GameState.LevelSelect);

        public void BackToMenu()
        {
            if (waves != null) waves.Stop();
            ClearBoard();
            SetState(GameState.MainMenu);
        }

        public void StartLevel(int index) // only level 0 exists for now
        {
            ClearBoard();
            if (game != null) game.ResetForNewGame();
            SetState(GameState.Playing);
            if (waves != null) waves.Begin();
        }

        public void Retry() => StartLevel(0);

        // ---- win / lose ----

        private void OnLivesDepleted()
        {
            if (State != GameState.Playing) return;
            if (waves != null) waves.Stop();
            SetState(GameState.Lost);
        }

        private void OnAllWavesCleared()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Won);
        }

        // ---- helpers ----

        private void ClearBoard()
        {
            foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            foreach (Tower t in FindObjectsByType<Tower>(FindObjectsSortMode.None)) Destroy(t.gameObject);
            foreach (Terrain ter in FindObjectsByType<Terrain>(FindObjectsSortMode.None)) Destroy(ter.gameObject);
            foreach (Projectile p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            if (grid != null) grid.ClearBuildings();
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
