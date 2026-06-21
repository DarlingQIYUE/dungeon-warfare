using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Throwaway visual: a polyline drawn through the chain-lightning hit points that
    /// fades out and destroys itself. Spawned by <see cref="LightningProjectile"/>.
    /// </summary>
    public class LightningFx : MonoBehaviour
    {
        private LineRenderer lr;
        private float life;
        private const float MaxLife = 0.15f;
        private static readonly Color BoltColor = new(0.6f, 0.85f, 1f, 1f); // electric blue

        public static void Spawn(List<Vector3> points)
        {
            var go = new GameObject("LightningFx");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.06f;
            lr.numCapVertices = 2;
            lr.sortingOrder = 5;
            lr.startColor = lr.endColor = BoltColor;

            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                p.z = 0f;
                lr.SetPosition(i, p);
            }

            var fx = go.AddComponent<LightningFx>();
            fx.lr = lr;
            fx.life = MaxLife;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            Color c = BoltColor;
            c.a = life / MaxLife; // fade out
            lr.startColor = lr.endColor = c;
        }

        private void OnDestroy()
        {
            if (lr != null) Destroy(lr.material); // the per-fx material instance
        }
    }
}
