using System.Collections.Generic;
using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// An expanding 120° fan-ring fired by <see cref="PoisonTower"/>. It's a thick arc
    /// band (radial width = <see cref="DebugTuning.PoisonWaveWidth"/>) that sprays outward
    /// from the tower — fast at first, slowing as it goes (ease-out, like an aerosol puff).
    /// Each enemy is hit once as the band sweeps over it (slow + light damage). Drawn as a
    /// translucent green annulus-sector mesh.
    /// </summary>
    public class PoisonWave : MonoBehaviour
    {
        public const float ExpandTime = 0.5f; // time for the front to finish its sweep
        private const int Segments = 20;
        private static readonly Color WaveColor = new(0.5f, 0.95f, 0.35f, 0.45f);

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private readonly HashSet<Health> hit = new();

        private Vector3 origin;
        private float aimDeg, halfAngle, maxRange, damage, width;
        private float t; // 0..1 progress

        public static void Spawn(Vector3 origin, float aimDeg, float halfAngle,
                                 float maxRange, float damage, float width)
        {
            var go = new GameObject("PoisonWave");
            go.transform.position = origin;

            var w = go.AddComponent<PoisonWave>();
            w.origin = origin;
            w.aimDeg = aimDeg;
            w.halfAngle = halfAngle;
            w.maxRange = maxRange;
            w.damage = damage;
            w.width = Mathf.Max(0.05f, width);

            w.meshFilter = go.AddComponent<MeshFilter>();
            w.meshRenderer = go.AddComponent<MeshRenderer>();
            w.meshRenderer.material = new Material(Shader.Find("Sprites/Default"))
            { mainTexture = Texture2D.whiteTexture };
            w.meshRenderer.sortingOrder = 4;
            w.mesh = new Mesh();
            w.meshFilter.mesh = w.mesh;
        }

        private void Update()
        {
            t += Time.deltaTime / ExpandTime;

            // Ease-out: fast at the start, slowing toward the end (aerosol spray feel).
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            float front = (maxRange + width) * e;
            float inner = Mathf.Max(0f, front - width);

            float hitOuter = Mathf.Min(front, maxRange);
            SweepBand(inner, hitOuter);
            DrawBand(inner, hitOuter);

            if (t >= 1f || inner >= maxRange) Destroy(gameObject);
        }

        /// <summary>Hit each enemy in the arc band once (inner..outer], within range.</summary>
        private void SweepBand(float inner, float outer)
        {
            if (outer <= inner) return;
            Vector3 aimDir = new(Mathf.Cos(aimDeg * Mathf.Deg2Rad), Mathf.Sin(aimDeg * Mathf.Deg2Rad), 0f);

            foreach (Collider2D col in Physics2D.OverlapCircleAll(origin, outer))
            {
                if (!col.TryGetComponent(out Enemy _)) continue;

                Vector3 to = col.transform.position - origin;
                to.z = 0f;
                float d = to.magnitude;
                if (d < inner || d > outer) continue;
                if (d > 1e-4f && Vector3.Angle(aimDir, to) > halfAngle) continue;

                if (!col.TryGetComponent(out Health h) || h.IsDead || hit.Contains(h)) continue;
                hit.Add(h);

                h.TakeDamage(damage);
                if (col.TryGetComponent(out EnemyStatus st))
                    st.ApplySlow(DebugTuning.PoisonSlowFactor, DebugTuning.PoisonSlowDuration);
            }
        }

        /// <summary>Rebuild the annulus-sector band mesh (vertices are local offsets from the tower).</summary>
        private void DrawBand(float inner, float outer)
        {
            if (outer <= inner) { meshRenderer.enabled = false; return; }
            meshRenderer.enabled = true;

            var verts = new Vector3[(Segments + 1) * 2];
            var cols = new Color[verts.Length];
            float start = (aimDeg - halfAngle) * Mathf.Deg2Rad;
            float span = halfAngle * 2f * Mathf.Deg2Rad;

            for (int i = 0; i <= Segments; i++)
            {
                float a = start + span * (i / (float)Segments);
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                cols[i * 2] = cols[i * 2 + 1] = WaveColor;
            }

            var tris = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int b = i * 2;
                tris[i * 6] = b;     tris[i * 6 + 1] = b + 1; tris[i * 6 + 2] = b + 2;
                tris[i * 6 + 3] = b + 1; tris[i * 6 + 4] = b + 3; tris[i * 6 + 5] = b + 2;
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (meshRenderer != null && meshRenderer.material != null) Destroy(meshRenderer.material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
