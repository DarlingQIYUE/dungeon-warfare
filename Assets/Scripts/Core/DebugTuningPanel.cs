using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonWarfare
{
    /// <summary>
    /// On-screen slider panel for the <see cref="DebugTuning"/> knobs, so the cannon /
    /// knockback feel can be tweaked live while playing. Auto-spawns on play; toggle with F1.
    /// </summary>
    public class DebugTuningPanel : MonoBehaviour
    {
        private bool show = true;
        private GUIStyle title, label;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            var go = new GameObject("DebugTuningPanel");
            go.AddComponent<DebugTuningPanel>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                show = !show;
        }

        private void OnGUI()
        {
            if (!show) return;
            EnsureStyles();

            var area = new Rect(12f, 12f, 290f, 200f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));

            GUILayout.Label("调试 DEBUG  (F1 隐藏)", title);
            DebugTuning.KnockStrength   = Row("击退力度", DebugTuning.KnockStrength, 0f, 5f);
            DebugTuning.KnockSpread     = Row("散布角°", DebugTuning.KnockSpread, 0f, 90f);
            DebugTuning.ExplosionRadius = Row("爆炸半径", DebugTuning.ExplosionRadius, 0.3f, 2.5f);
            DebugTuning.KnockDecay      = Row("击退衰减", DebugTuning.KnockDecay, 1f, 15f);
            DebugTuning.WallSlamPerSpeed = Row("撞墙伤害", DebugTuning.WallSlamPerSpeed, 0f, 10f);

            GUILayout.EndArea();
        }

        private float Row(string name, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{name} {value:0.0}", label, GUILayout.Width(140f));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            return value;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;

            label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            label.normal.textColor = new Color(0.9f, 0.95f, 1f);
        }
    }
}
