using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Growth tower (A32Veteran). Every enemy it last-hits permanently raises its damage
    /// AND fire rate, both uncapped (the fire interval only bottoms out at a tiny engine
    /// safety value — i.e. effectively a shot every frame). The bonus is layered on top of
    /// the tunable base stats, so live-tuning still works. Kills are counted by
    /// <see cref="VeteranProjectile"/> (last-hit only). Lasts the level; lost when sold.
    /// Tints goldener the more kills it has racked up.
    /// </summary>
    public class VeteranTower : Tower
    {
        private const float MinFireInterval = 0.02f; // safety floor only (avoid 0/negative)

        private int kills;
        private SpriteRenderer body;
        private Color baseColor;

        public override float Damage => base.Damage + kills * DebugTuning.VeteranDamagePerKill;

        public override float FireInterval =>
            Mathf.Max(base.FireInterval - kills * DebugTuning.VeteranFireReducePerKill, MinFireInterval);

        // Start (not Awake) so we don't collide with the base's private Awake.
        private void Start()
        {
            body = GetComponent<SpriteRenderer>();
            if (body != null) baseColor = body.color;
        }

        /// <summary>Called by the veteran's projectile when its hit lands a kill.</summary>
        public void RegisterKill()
        {
            kills++;
            UpdateTint();
        }

        protected override void Fire(Health target)
        {
            Projectile shot = Instantiate(ProjectilePrefab, Muzzle.position, Quaternion.identity);
            if (shot is VeteranProjectile veteran) veteran.SetOwner(this);
            shot.Launch(target, Damage);
        }

        private void UpdateTint()
        {
            if (body == null) return;
            float t = Mathf.Clamp01(kills / 20f); // ~20 kills => full gold
            body.color = Color.Lerp(baseColor, new Color(1f, 0.85f, 0.2f), t);
        }
    }
}
