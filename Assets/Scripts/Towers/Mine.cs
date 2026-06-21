using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// A persistent, independent trap laid by <see cref="MineTower"/>. Once placed it
    /// lives on the map on its own (not tied to the tower); when any enemy comes within
    /// <see cref="DebugTuning.MineTriggerRadius"/> it detonates — dealing AoE damage in
    /// <see cref="DebugTuning.MineBlastRadius"/> — then despawns. Damage runs through
    /// <see cref="Health.TakeDamage"/>, so marked enemies take amplified blasts.
    /// </summary>
    public class Mine : MonoBehaviour
    {
        private MineTower owner;
        private float damage;

        /// <summary>Wire up the mine with the tower that laid it and the blast damage.</summary>
        public void Init(MineTower layer, float blastDamage)
        {
            owner = layer;
            damage = blastDamage;
        }

        private void Update()
        {
            foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, DebugTuning.MineTriggerRadius))
            {
                if (col.TryGetComponent(out Enemy _)) { Detonate(); return; }
            }
        }

        private void Detonate()
        {
            float blast = DebugTuning.MineBlastRadius;

            if (TryGetComponent(out SpriteRenderer sr) && sr.sprite != null)
                ExplosionFx.Spawn(transform.position, blast, sr.sprite);

            foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, blast))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;
                if (col.TryGetComponent(out Health h) && !h.IsDead) h.TakeDamage(damage);
            }

            if (owner != null) owner.MineCleared();
            Destroy(gameObject);
        }
    }
}
