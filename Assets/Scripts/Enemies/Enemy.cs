using System;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Ties together health + path movement for a single invader.
    /// Rewards gold on death, costs the player a life if it escapes.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(PathFollower))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private int rewardGold = 5;
        [SerializeField] private int damageToBase = 1;

        /// <summary>Fired when this enemy leaves play (killed or escaped).</summary>
        public event Action<Enemy> Despawned;

        private Health health;
        private PathFollower follower;
        private GameManager game;
        private bool despawned;

        /// <summary>Grid cells the enemy occupies (current + the one it's entering).</summary>
        public Vector2Int CurrentCell => follower.CurrentCell;
        public Vector2Int TargetCell => follower.TargetCell;

        /// <summary>Remaining route distance to the exit (smaller = closer to leaking).</summary>
        public float DistanceToExit => follower != null ? follower.DistanceToExit() : float.MaxValue;

        private void Awake()
        {
            health = GetComponent<Health>();
            follower = GetComponent<PathFollower>();
        }

        /// <summary>Called by the spawner right after instantiation, with this wave's HP.</summary>
        public void Spawn(GridSystem grid, GameManager gameManager, float hp)
        {
            game = gameManager;
            despawned = false;
            health.Configure(hp);
            follower.Initialize(grid);

            health.Died -= OnDied;
            health.Died += OnDied;
        }

        private void Update()
        {
            if (despawned) return;

            if (follower.ReachedEnd)
            {
                game?.OnEnemyReachedBase(damageToBase);
                Despawn();
            }
        }

        private void OnDied(Health _)
        {
            if (despawned) return;
            game?.OnEnemyKilled(rewardGold);
            Despawn();
        }

        private void Despawn()
        {
            despawned = true;
            health.Died -= OnDied;
            Despawned?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
