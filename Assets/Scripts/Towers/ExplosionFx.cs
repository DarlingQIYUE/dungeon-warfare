using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Throwaway visual: a circle that expands to the blast radius and fades out,
    /// then destroys itself. Spawned by <see cref="BombProjectile"/> on detonation.
    /// </summary>
    public class ExplosionFx : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float radius;
        private float life;
        private const float MaxLife = 0.25f;

        public static void Spawn(Vector3 pos, float radius, Sprite sprite)
        {
            var go = new GameObject("ExplosionFx");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.55f, 0.2f, 0.6f);
            sr.sortingOrder = 5;

            var fx = go.AddComponent<ExplosionFx>();
            fx.sr = sr;
            fx.radius = radius;
            fx.life = MaxLife;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            float t = 1f - life / MaxLife;                 // 0 -> 1 over the lifetime
            float diameter = Mathf.Lerp(0.3f, radius * 2f, t); // 1-unit sprite -> world diameter
            transform.localScale = new Vector3(diameter, diameter, 1f);

            Color c = sr.color;
            c.a = 0.6f * (1f - t);
            sr.color = c;
        }
    }
}
