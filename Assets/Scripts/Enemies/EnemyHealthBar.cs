using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// A floating health bar that appears only after the enemy first takes
    /// damage (full-HP enemies show nothing). The bar objects are world-space
    /// and NOT parented to the enemy, so they ignore the enemy's scale/flip;
    /// they simply track its position each frame.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private Sprite barSprite; // white square
        [SerializeField] private float width = 0.5f;
        [SerializeField] private float height = 0.08f;
        [SerializeField] private float yOffset = 0.45f;
        [SerializeField] private int sortingOrder = 6;

        private Health health;
        private Transform background;
        private Transform fill;
        private SpriteRenderer fillRenderer;
        private bool shown;

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable() => health.DamageTaken += OnDamage;

        private void OnDisable()
        {
            health.DamageTaken -= OnDamage;
            DestroyBars();
        }

        private void OnDamage(Health h, float amount)
        {
            if (!shown) CreateBars();
        }

        private void CreateBars()
        {
            if (barSprite == null) return; // nothing to draw with
            shown = true;

            background = MakeBar("HealthBar_BG", new Color(0.07f, 0.07f, 0.07f, 0.85f), sortingOrder).transform;
            fillRenderer = MakeBar("HealthBar_Fill", Color.green, sortingOrder + 1);
            fill = fillRenderer.transform;
        }

        private SpriteRenderer MakeBar(string name, Color color, int order)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = barSprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(width, height, 1f);
            return sr;
        }

        private void LateUpdate()
        {
            if (!shown) return;

            Vector3 anchor = transform.position + Vector3.up * yOffset;
            if (background != null) background.position = anchor;

            if (fill != null)
            {
                float n = Mathf.Clamp01(health.Normalized);
                fill.localScale = new Vector3(width * n, height, 1f);
                // keep the fill left-aligned with the background as it shrinks
                fill.position = anchor + Vector3.left * (width * (1f - n) * 0.5f);
                fillRenderer.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.35f, 0.9f, 0.35f), n);
            }
        }

        private void DestroyBars()
        {
            if (background != null) Destroy(background.gameObject);
            if (fill != null) Destroy(fill.gameObject);
            shown = false;
        }
    }
}
