using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Veteran's round (A32Veteran). A normal flying shot that, when its hit lands the
    /// killing blow, credits the firing tower so it can grow. Last-hit attribution: it
    /// only counts kills its own impact causes (poison/other sources don't count).
    /// </summary>
    public class VeteranProjectile : Projectile
    {
        private VeteranTower owner;

        public void SetOwner(VeteranTower tower) => owner = tower;

        protected override void Detonate()
        {
            bool aliveBefore = target != null && !target.IsDead;
            base.Detonate(); // deals the hit
            if (aliveBefore && target != null && target.IsDead && owner != null)
                owner.RegisterKill();
        }
    }
}
