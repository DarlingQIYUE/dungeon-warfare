using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonWarfare
{
    /// <summary>
    /// On-screen live-tuning panel, organized one section per tower type. Each section
    /// shows that type's core stats (damage / range / fire rate) plus its special knobs
    /// (cannon: explosion + knockback; injection: poison/DOT). Editing a value applies it
    /// to every tower of that type immediately — including ones built afterwards — so it
    /// behaves as a global per-type override. Auto-spawns on play; toggle with F1.
    /// Dial values in, then bake the numbers back into the scene builder.
    /// </summary>
    public class DebugTuningPanel : MonoBehaviour
    {
        // Tower display names (set by the scene builder) used to attach the right
        // special knobs to each section.
        private const string CannonName = "加农炮";
        private const string InjectionName = "病毒注射";
        private const string SniperName = "狙击炮";
        private const string LightningName = "闪电链";
        private const string LaserName = "激光";
        private const string VeteranName = "老兵";
        private const string PoisonName = "毒素炮";
        private const string FireName = "火焰炮";
        private const string MineName = "布雷塔";

        private class TowerStat { public string name; public float damage, range, fire; }

        private bool show = true;
        private GUIStyle title, label, foldout;
        private bool stylesReady;
        private GridPlacer placer;
        private List<TowerStat> towerStats;       // editable per-type base stats, seeded from prefabs
        private readonly HashSet<string> expanded = new(); // section names currently unfolded (default: all collapsed)
        private Vector2 scroll;                    // scroll position when content overflows the fixed panel

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
            EnsureTowerStats();

            // Fixed panel: cap height to the screen and scroll when content overflows.
            const float titleH = 24f;
            float height = Mathf.Min(PanelHeight(), Screen.height - 24f);
            var area = new Rect(12f, 12f, 300f, height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));

            GUILayout.Label("调试 DEBUG  (F1 隐藏)", title);

            if (towerStats == null || towerStats.Count == 0)
            {
                GUILayout.Label("（进入关卡后显示炮塔数值）", label);
                GUILayout.EndArea();
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(area.height - 16f - titleH));
            foreach (TowerStat s in towerStats)
            {
                GUILayout.Space(4f);
                bool open = expanded.Contains(s.name);
                if (GUILayout.Button((open ? "▾ " : "▸ ") + s.name, foldout))
                {
                    if (open) expanded.Remove(s.name); else expanded.Add(s.name);
                }

                if (open)
                {
                    s.damage = Row("伤害", s.damage, 0f, 100f);
                    s.range  = Row("射程", s.range, 1f, 10f);
                    s.fire   = Row("射速(秒)", s.fire, 0.1f, 3f);
                    DrawSpecialKnobs(s.name);
                }

                ApplyToType(s); // values keep applying even while collapsed
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        /// <summary>Type-specific extra knobs (live in <see cref="DebugTuning"/>, global per type).</summary>
        private void DrawSpecialKnobs(string towerName)
        {
            if (towerName == CannonName)
            {
                DebugTuning.ExplosionRadius  = Row("爆炸半径", DebugTuning.ExplosionRadius, 0.3f, 2.5f);
                DebugTuning.KnockStrength    = Row("击退力度", DebugTuning.KnockStrength, 0f, 5f);
                DebugTuning.KnockSpread      = Row("散布角°", DebugTuning.KnockSpread, 0f, 90f);
                DebugTuning.KnockDecay       = Row("击退衰减", DebugTuning.KnockDecay, 1f, 15f);
                DebugTuning.WallSlamPerSpeed = Row("撞墙伤害", DebugTuning.WallSlamPerSpeed, 0f, 10f);
            }
            else if (towerName == InjectionName)
            {
                DebugTuning.PoisonPerStackDps  = Row("每层每秒", DebugTuning.PoisonPerStackDps, 0f, 20f);
                DebugTuning.PoisonMaxStacks    = Mathf.RoundToInt(Row("最大层数", DebugTuning.PoisonMaxStacks, 1f, 20f));
                DebugTuning.PoisonDropInterval = Row("掉层间隔", DebugTuning.PoisonDropInterval, 0.3f, 4f);
            }
            else if (towerName == SniperName)
            {
                DebugTuning.VulnerabilityAmp      = Row("伤害加深", DebugTuning.VulnerabilityAmp, 0f, 2f);
                DebugTuning.VulnerabilityDuration = Row("标记时长", DebugTuning.VulnerabilityDuration, 0.5f, 10f);
            }
            else if (towerName == LightningName)
            {
                DebugTuning.ChainJumps     = Mathf.RoundToInt(Row("跳数", DebugTuning.ChainJumps, 0f, 8f));
                DebugTuning.ChainFalloff   = Row("每跳衰减", DebugTuning.ChainFalloff, 0.1f, 1f);
                DebugTuning.ChainJumpRange = Row("跳跃距离", DebugTuning.ChainJumpRange, 1f, 5f);
            }
            else if (towerName == LaserName)
            {
                DebugTuning.LaserRampTime       = Row("斜坡时间", DebugTuning.LaserRampTime, 0.5f, 8f);
                DebugTuning.LaserRampMultiplier = Row("最大倍率", DebugTuning.LaserRampMultiplier, 1f, 8f);
            }
            else if (towerName == VeteranName)
            {
                DebugTuning.VeteranDamagePerKill    = Row("每杀+伤害", DebugTuning.VeteranDamagePerKill, 0f, 5f);
                DebugTuning.VeteranFireReducePerKill = Row("每杀加速", DebugTuning.VeteranFireReducePerKill, 0f, 0.05f);
            }
            else if (towerName == PoisonName)
            {
                DebugTuning.PoisonConeAngle    = Row("扇形角度", DebugTuning.PoisonConeAngle, 30f, 180f);
                DebugTuning.PoisonWaveWidth    = Row("环宽", DebugTuning.PoisonWaveWidth, 0.2f, 4f);
                DebugTuning.PoisonSlowFactor   = Row("减速比例", DebugTuning.PoisonSlowFactor, 0f, 0.8f);
                DebugTuning.PoisonSlowDuration = Row("减速余留", DebugTuning.PoisonSlowDuration, 0.2f, 4f);
            }
            else if (towerName == FireName)
            {
                DebugTuning.FireConeAngle = Row("扇形角度", DebugTuning.FireConeAngle, 20f, 150f);
            }
            else if (towerName == MineName)
            {
                DebugTuning.MineMaxLive       = Mathf.RoundToInt(Row("最大存量", DebugTuning.MineMaxLive, 1f, 20f));
                DebugTuning.MineTriggerRadius = Row("触发半径", DebugTuning.MineTriggerRadius, 0.1f, 1f);
                DebugTuning.MineBlastRadius   = Row("爆炸半径", DebugTuning.MineBlastRadius, 0.3f, 2.5f);
                DebugTuning.MineMinSpacing    = Row("最小间距", DebugTuning.MineMinSpacing, 0f, 2f);
            }
        }

        /// <summary>How many extra rows a type adds, for sizing the panel.</summary>
        private static int SpecialKnobCount(string towerName) =>
            towerName == CannonName ? 5 :
            towerName == InjectionName ? 3 :
            towerName == SniperName ? 2 :
            towerName == LightningName ? 3 :
            towerName == LaserName ? 2 :
            towerName == VeteranName ? 2 :
            towerName == PoisonName ? 4 :
            towerName == FireName ? 1 :
            towerName == MineName ? 4 : 0;

        private float PanelHeight()
        {
            float h = 34f; // title
            if (towerStats == null || towerStats.Count == 0) return h + 28f;
            foreach (TowerStat s in towerStats)
            {
                h += 4f + 26f; // space + foldout header (always shown)
                if (expanded.Contains(s.name))
                    h += (3 + SpecialKnobCount(s.name)) * 22f + 4f; // rows only when unfolded
            }
            return h + 12f;
        }

        /// <summary>Apply a type's edited stats to every live tower of that type.</summary>
        private void ApplyToType(TowerStat s)
        {
            foreach (Tower t in FindObjectsByType<Tower>(FindObjectsSortMode.None))
            {
                if (t.DisplayName != s.name) continue;
                t.SetDamage(s.damage);
                t.SetRange(s.range);
                t.SetFireInterval(s.fire);
            }
        }

        /// <summary>Seed the editable stats from the buildable tower prefabs, once.</summary>
        private void EnsureTowerStats()
        {
            if (towerStats != null) return;
            if (placer == null) placer = FindFirstObjectByType<GridPlacer>();
            if (placer == null || placer.AvailableTowers == null || placer.AvailableTowers.Count == 0) return;

            towerStats = new List<TowerStat>();
            foreach (Tower t in placer.AvailableTowers)
            {
                if (t == null) continue;
                towerStats.Add(new TowerStat
                {
                    name = t.DisplayName,
                    damage = t.Damage,
                    range = t.Range,
                    fire = t.FireInterval,
                });
            }
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

            foldout = new GUIStyle(GUI.skin.button)
            { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            foldout.normal.textColor = Color.white;
        }
    }
}
