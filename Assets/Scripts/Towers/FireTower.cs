using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// Flamethrower (A33Fire). Short range; instead of discrete shots it continuously
    /// sprays a small forward fan aimed at the in-range enemy nearest the exit, dealing
    /// high continuous damage (dps = the tower's <c>damage</c>) to every enemy inside the
    /// cone each frame. Damage runs through <see cref="Health.TakeDamage"/>, so it benefits
    /// from vulnerability too. Strong at melting groups up close. Fan width in DebugTuning.
    /// </summary>
    public class FireTower : Tower
    {
        private static readonly Color FlameColor = new(1f, 0.5f, 0.15f, 0.4f); // translucent orange

        private MeshFilter coneFilter;
        private MeshRenderer coneRenderer;
        private float builtAngle = -1f;

        // Start (not Awake) so we don't collide with the base's private Awake.
        private void Start()
        {
            var go = new GameObject("FlameCone");
            go.transform.SetParent(transform, false);
            coneFilter = go.AddComponent<MeshFilter>();
            coneRenderer = go.AddComponent<MeshRenderer>();
            coneRenderer.material = new Material(Shader.Find("Sprites/Default"))
            { mainTexture = Texture2D.whiteTexture };
            coneRenderer.sortingOrder = 4;
            coneRenderer.enabled = false;
        }

        protected override void Update()
        {
            // Aim the flame where it covers the most enemies, not at the frontmost one.
            Vector3 aim = BestConeAim(DebugTuning.FireConeAngle);
            if (aim == Vector3.zero) { if (coneRenderer != null) coneRenderer.enabled = false; return; }
            float half = DebugTuning.FireConeAngle * 0.5f;

            foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, Range))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;

                Vector3 to = col.transform.position - transform.position;
                to.z = 0f;
                if (to.sqrMagnitude > 1e-6f && Vector3.Angle(aim, to) > half) continue; // outside the fan

                if (col.TryGetComponent(out Health h) && !h.IsDead)
                    h.TakeDamage(Damage * Time.deltaTime); // high continuous dps
            }

            DrawCone(aim);
        }

        private void DrawCone(Vector3 aim)
        {
            if (coneRenderer == null) return;

            if (!Mathf.Approximately(builtAngle, DebugTuning.FireConeAngle))
            {
                coneFilter.mesh = BuildConeMesh(DebugTuning.FireConeAngle);
                builtAngle = DebugTuning.FireConeAngle;
            }

            coneRenderer.enabled = true;
            float ang = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            coneRenderer.transform.rotation = Quaternion.Euler(0f, 0f, ang);

            // Unit-radius mesh -> world radius == Range, regardless of the tower's own scale.
            float s = Range / Mathf.Max(transform.lossyScale.x, 1e-4f);
            coneRenderer.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>A unit-radius sector mesh centered on +X spanning ±angle/2.</summary>
        private static Mesh BuildConeMesh(float angleDeg)
        {
            const int seg = 20;
            float fan = angleDeg * Mathf.Deg2Rad;
            float half = fan * 0.5f;

            var verts = new Vector3[seg + 2];
            var cols = new Color[seg + 2];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float a = -half + fan * (i / (float)seg);
                verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
            for (int i = 0; i < cols.Length; i++) cols[i] = FlameColor;

            var tris = new int[seg * 3];
            for (int i = 0; i < seg; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            var m = new Mesh();
            m.vertices = verts;
            m.colors = cols;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        private void OnDestroy()
        {
            if (coneRenderer != null && coneRenderer.material != null) Destroy(coneRenderer.material);
            if (coneFilter != null && coneFilter.sharedMesh != null) Destroy(coneFilter.sharedMesh);
        }
    }
}
