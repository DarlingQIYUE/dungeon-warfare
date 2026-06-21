using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonWarfare
{
    /// <summary>
    /// IMGUI front-end for the whole game: menus, the right-hand HUD/build sidebar
    /// (gold/lives/waves), a "wave incoming" banner, spacebar pause, a contextual
    /// remove panel for selected buildings, and win/lose screens. Prototype shell.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private GameFlow flow;
        [SerializeField] private GameManager game;
        [SerializeField] private WaveManager waves;
        [SerializeField] private GridPlacer placer; // build menu hooks into this
        [SerializeField] private Camera sidebarCamera; // to position the sidebar text

        private GUIStyle title, button, banner, sideHeader, sideValue, buildButton, hint, tooltip;
        private bool stylesReady;

        private bool paused;     // space quick-pause
        private bool menuOpen;   // esc pause menu

        private static readonly float[] Speeds = { 1f, 2f, 3f };
        private int speedIndex;  // index into Speeds; the active game speed multiplier
        private float GameSpeed => Speeds[speedIndex];
        private float waveBannerTimer;
        private int bannerWave;
        private float bonusToastTimer;
        private int bonusToastAmount;

        private void Awake()
        {
            if (flow == null) flow = GameFlow.Instance;
            if (game == null) game = GameManager.Instance;
            if (waves == null) waves = FindFirstObjectByType<WaveManager>();
            if (placer == null) placer = FindFirstObjectByType<GridPlacer>();
        }

        private void OnEnable()
        {
            if (waves == null) waves = FindFirstObjectByType<WaveManager>();
            if (waves != null)
            {
                waves.WaveChanged += OnWaveChanged;
                waves.EarlyStartRewarded += OnEarlyStartRewarded;
            }
        }

        private void OnDisable()
        {
            if (waves != null)
            {
                waves.WaveChanged -= OnWaveChanged;
                waves.EarlyStartRewarded -= OnEarlyStartRewarded;
            }
            Time.timeScale = 1f; // never leave the game frozen
        }

        private void OnEarlyStartRewarded(int gold)
        {
            bonusToastAmount = gold;
            bonusToastTimer = 2f;
        }

        private void OnWaveChanged()
        {
            if (waves != null && waves.CurrentWave >= 1)
            {
                bannerWave = waves.CurrentWave;
                waveBannerTimer = 2.5f;
            }
        }

        private void Update()
        {
            if (flow == null) flow = GameFlow.Instance;
            bool playing = flow != null && flow.State == GameState.Playing;

            if (playing && Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) menuOpen = !menuOpen;
                if (!menuOpen && Keyboard.current.spaceKey.wasPressedThisFrame) paused = !paused;
            }
            if (!playing) { paused = false; menuOpen = false; }

            // Game speed multiplier while playing; frozen by pause/menu; normal (1x)
            // on menus/result screens so they aren't sped up.
            Time.timeScale = !playing ? 1f : (paused || menuOpen) ? 0f : GameSpeed;

            // The ESC menu hard-blocks board interaction; the space time-stop does
            // not. Publish that via GameFlow so the placer can tell them apart.
            if (flow != null) flow.ModalMenuOpen = menuOpen;

            if (waveBannerTimer > 0f) waveBannerTimer -= Time.unscaledDeltaTime;
            if (bonusToastTimer > 0f) bonusToastTimer -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (flow == null) flow = GameFlow.Instance;
            if (flow == null) return;
            EnsureStyles();

            switch (flow.State)
            {
                case GameState.MainMenu:
                    DrawDim();
                    DrawMenu("TOWER DEFENSE", "开始游戏", flow.GoToLevelSelect);
                    break;
                case GameState.LevelSelect:
                    DrawDim();
                    DrawLevelSelect();
                    break;
                case GameState.Playing:
                    DrawSidebar();
                    DrawWavePrompt();
                    DrawBonusToast();
                    DrawWaveBanner();
                    if (menuOpen) DrawPauseMenu();
                    else if (paused) DrawPauseOverlay();
                    break;
                case GameState.Won:
                    DrawSidebar();
                    DrawResult("VICTORY 胜利", new Color(0.45f, 1f, 0.55f));
                    break;
                case GameState.Lost:
                    DrawSidebar();
                    DrawResult("DEFEATED 失败", new Color(1f, 0.45f, 0.45f));
                    break;
            }
        }

        // ---- screens ----

        private void DrawMenu(string heading, string action, System.Action onAction)
        {
            var area = Centered(400, 240);
            GUILayout.BeginArea(area);
            GUILayout.Label(heading, title);
            GUILayout.Space(24);
            if (GUILayout.Button(action, button, GUILayout.Height(54))) onAction();
            GUILayout.EndArea();
        }

        private void DrawLevelSelect()
        {
            var area = Centered(400, 300);
            GUILayout.BeginArea(area);
            GUILayout.Label("选择关卡", title);
            GUILayout.Space(20);
            if (GUILayout.Button("第 1 关", button, GUILayout.Height(50))) flow.StartLevel(0);
            GUILayout.Space(10);
            if (GUILayout.Button("返回", button, GUILayout.Height(40))) flow.BackToMenu();
            GUILayout.EndArea();
        }

        private void DrawResult(string text, Color color)
        {
            DrawDim();
            var area = Centered(460, 420);
            GUILayout.BeginArea(area);
            banner.normal.textColor = color;
            GUILayout.Label(text, banner);
            GUILayout.Space(12);

            bool won = flow.State == GameState.Won;
            int wave = waves != null ? waves.CurrentWave : 0;
            int total = waves != null ? waves.TotalWaves : 0;

            hint.normal.textColor = Color.white;
            if (won) GUILayout.Label(StarRating(), hint);
            GUILayout.Label($"波数 {wave} / {total}", hint);
            if (game != null)
            {
                GUILayout.Label($"击杀 {game.EnemiesKilled}", hint);
                GUILayout.Label($"剩余生命 {game.Lives} / {game.StartingLives}", hint);
            }
            hint.normal.textColor = new Color(1f, 0.9f, 0.5f); // restore

            GUILayout.Space(18);
            if (GUILayout.Button("重玩", button, GUILayout.Height(50))) flow.Retry();
            GUILayout.Space(10);
            if (GUILayout.Button("返回菜单", button, GUILayout.Height(40))) flow.BackToMenu();
            GUILayout.EndArea();
        }

        // 3 stars if no lives lost, 2 if at least half remain, else 1 (win only).
        private string StarRating()
        {
            if (game == null) return "";
            float frac = (float)game.Lives / Mathf.Max(1, game.StartingLives);
            int stars = game.Lives >= game.StartingLives ? 3 : frac >= 0.5f ? 2 : 1;
            return new string('★', stars) + new string('☆', 3 - stars);
        }

        private void DrawSidebar()
        {
            Rect sb = SidebarRect();
            float x = sb.x + 6f;
            float w = sb.width - 12f;
            float y = sb.y + 6f;

            if (game != null)
                DrawStatPair(x, ref y, w, "金币", $"{game.Gold}", "生命", $"{game.Lives}");

            if (waves != null)
            {
                DrawStatPair(x, ref y, w, "波次", $"{waves.CurrentWave}/{waves.TotalWaves}",
                             "剩余敌人", $"{waves.AliveCount}");

                if (flow.State == GameState.Playing)
                    DrawWaveAndSpeed(x, ref y, w);
            }

            // Build menu (only while actively playing)
            if (flow.State == GameState.Playing && placer != null &&
                placer.AvailableTowers != null && placer.AvailableTowers.Count > 0)
                DrawBuildMenu(x, ref y, w);

            // Remove panel for a selected building (in the sidebar, so its button
            // is outside the grid and won't fight the placer's click handling).
            if (flow.State == GameState.Playing && placer != null && placer.HasSelection && !placer.IsPlacing)
                DrawRemoveSection(x, ref y, w);
        }

        // Two labeled stats side by side (header on top, value below).
        private void DrawStatPair(float x, ref float y, float w,
                                  string h1, string v1, string h2, string v2)
        {
            float half = (w - 6f) / 2f;
            float x2 = x + half + 6f;
            GUI.Label(new Rect(x, y, half, 16), h1, sideHeader);
            GUI.Label(new Rect(x2, y, half, 16), h2, sideHeader);
            y += 17f;
            GUI.Label(new Rect(x, y, half, 24), v1, sideValue);
            GUI.Label(new Rect(x2, y, half, 24), v2, sideValue);
            y += 28f;
        }

        // Next-wave info on one line, then start/early-start (left) + speed (right) on one row.
        private void DrawWaveAndSpeed(float x, ref float y, float w)
        {
            if (waves == null) return;

            if (waves.CountingDown)
            {
                GUI.Label(new Rect(x, y, w, 16), $"下一波 {waves.Countdown:0.0}s", hint); y += 18f;
            }
            else if (waves.HasNextWavePreview)
            {
                GUI.Label(new Rect(x, y, w, 16),
                    $"下一波 #{waves.NextWaveNumber}  {waves.EnemiesPerWave}只 HP{waves.NextWaveEnemyHealth:0}", hint);
                y += 18f;
            }

            float half = (w - 6f) / 2f;
            float x2 = x + half + 6f;
            Color prev = GUI.backgroundColor;

            if (waves.AwaitingStart)
            {
                GUI.backgroundColor = new Color(0.35f, 0.75f, 0.4f);
                if (GUI.Button(new Rect(x, y, half, 32), "▶ 开始", buildButton)) waves.RequestNextWave();
            }
            else if (waves.CountingDown)
            {
                GUI.backgroundColor = new Color(0.45f, 0.62f, 0.78f);
                if (GUI.Button(new Rect(x, y, half, 32), $"提前 +{waves.EarlyStartBonus}", buildButton))
                    waves.RequestNextWave();
            }
            GUI.backgroundColor = prev;

            if (GUI.Button(new Rect(x2, y, half, 32), $"▶ x{GameSpeed:0}", buildButton))
                speedIndex = (speedIndex + 1) % Speeds.Length;
            y += 36f;
        }

        private void DrawRemoveSection(float x, ref float y, float w)
        {
            y += 8f;
            GUI.Label(new Rect(x, y, w, 16), $"已选：{placer.SelectedLabel}", sideHeader); y += 18f;

            // Stats for a selected tower (terrain has none).
            Tower sel = placer.SelectedTower;
            if (sel != null)
            {
                GUI.Label(new Rect(x, y, w, 16), $"伤害 {sel.Damage:0}　射程 {sel.Range:0.0}", hint); y += 17f;
                GUI.Label(new Rect(x, y, w, 16), $"攻速 每 {sel.FireInterval:0.0}s", hint); y += 19f;
            }

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.82f, 0.42f, 0.36f);
            if (GUI.Button(new Rect(x, y, w, 32), $"拆除 返还 ${placer.SelectedRefund}", buildButton))
                placer.RemoveSelected();
            GUI.backgroundColor = prev;
            y += 36f;
        }

        // ESC pause menu.
        private void DrawPauseMenu()
        {
            DrawDim();
            var area = Centered(360, 360);
            GUILayout.BeginArea(area);
            GUILayout.Label("暂停", title);
            GUILayout.Space(20);
            if (GUILayout.Button("继续", button, GUILayout.Height(48))) { menuOpen = false; paused = false; }
            GUILayout.Space(8);
            if (GUILayout.Button("重新开始", button, GUILayout.Height(48))) { menuOpen = false; flow.Retry(); }
            GUILayout.Space(8);
            if (GUILayout.Button("回到主菜单", button, GUILayout.Height(48))) { menuOpen = false; flow.BackToMenu(); }
            GUILayout.Space(8);
            if (GUILayout.Button("退出游戏", button, GUILayout.Height(48))) QuitGame();
            GUILayout.EndArea();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Top-of-board prompt: invite the first start, or show the next-wave countdown.
        private void DrawWavePrompt()
        {
            if (waves == null) return;

            string text;
            if (waves.AwaitingStart) text = "布置好防御后，点击右侧 ▶ 开始";
            else if (waves.CountingDown) text = $"下一波 {waves.Countdown:0.0}s　提前开始 +{waves.EarlyStartBonus} 金";
            else return;

            var area = new Rect(PlayAreaCenterX() - 260f, Screen.height * 0.12f, 520f, 40f);
            GUI.Label(area, text, hint);
        }

        // Brief "+N gold" feedback when an early start is rewarded.
        private void DrawBonusToast()
        {
            if (bonusToastTimer <= 0f) return;
            var area = new Rect(PlayAreaCenterX() - 200f, Screen.height * 0.18f, 400f, 36f);
            GUI.Label(area, $"提前开始  +{bonusToastAmount} 金币", hint);
        }

        private void DrawWaveBanner()
        {
            if (waveBannerTimer <= 0f) return;
            var area = new Rect(PlayAreaCenterX() - 230f, Screen.height * 0.2f, 460f, 70f);
            banner.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(area, $"第 {bannerWave} 波  来袭", banner);
        }

        private void DrawPauseOverlay()
        {
            // Time-stop: the world is frozen but building/selling stays available,
            // so we don't dim the board — just show an indicator near the top.
            var area = new Rect(PlayAreaCenterX() - 240f, Screen.height * 0.05f, 480f, 80f);
            banner.normal.textColor = new Color(0.6f, 0.85f, 1f);
            GUI.Label(new Rect(area.x, area.y, area.width, 52f), "时停 TIME-STOP", banner);
            GUI.Label(new Rect(area.x, area.y + 54f, area.width, 24f), "可建造 / 拆除 · 空格继续", hint);
        }

        private float PlayAreaCenterX()
        {
            float right = sidebarCamera != null ? sidebarCamera.pixelRect.x : Screen.width * 0.875f;
            return right * 0.5f;
        }

        private void DrawBuildMenu(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 16), "建造 BUILD", sideHeader); y += 18f;

            const float bh = 40f;            // button height
            float half = (w - 6f) / 2f;      // two columns
            var towers = placer.AvailableTowers;
            Terrain ter = placer.AvailableTerrain;
            int count = towers.Count + (ter != null ? 1 : 0);

            for (int i = 0; i < count; i++)
            {
                int col = i % 2;
                var cell = new Rect(x + col * (half + 6f), y, half, bh);

                if (i < towers.Count)
                {
                    Tower t = towers[i];
                    if (t != null)
                    {
                        int idx = i; // capture for the click closure
                        DrawBuildButton(cell, $"{t.DisplayName}\n${t.Cost}", t.Cost,
                            placer.IsSelectedTower(idx), () => placer.SelectTower(idx),
                            $"伤害 {t.Damage:0}\n攻速 每 {t.FireInterval:0.0}s\n射程 {t.Range:0.0}");
                    }
                }
                else
                {
                    DrawBuildButton(cell, $"地形\n${ter.Cost}", ter.Cost,
                        placer.IsPlacingTerrain, placer.SelectTerrain,
                        "铺在路上\n敌人绕行\n可在上面建炮塔");
                }

                if (col == 1) y += bh + 4f; // advance after filling the right column
            }
            if (count % 2 == 1) y += bh + 4f; // last row had only the left column

            if (placer.IsPlacing)
                GUI.Label(new Rect(x, y, w, 22), "左键放置  右键取消", hint);
        }

        private void DrawBuildButton(Rect r, string label, int cost, bool selected,
                                     System.Action onClick, string tip)
        {
            bool affordable = game == null || game.Gold >= cost;
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = selected ? new Color(1f, 0.85f, 0.35f)   // selected = yellow
                                : affordable ? new Color(0.45f, 0.62f, 0.78f)
                                : new Color(0.5f, 0.5f, 0.5f);             // unaffordable (still selectable)
            if (GUI.Button(r, label, buildButton)) onClick();
            GUI.backgroundColor = prev;

            if (r.Contains(Event.current.mousePosition))
                DrawTooltip(Event.current.mousePosition, tip);
        }

        private void DrawTooltip(Vector2 mouse, string text)
        {
            var size = new Vector2(168f, 78f);
            float tx = mouse.x - size.x - 12f;      // prefer left of cursor
            if (tx < 4f) tx = mouse.x + 16f;        // flip to right if no room
            float ty = Mathf.Clamp(mouse.y, 4f, Screen.height - size.y - 4f);
            GUI.Box(new Rect(tx, ty, size.x, size.y), text, tooltip);
        }

        // ---- layout helpers ----

        private Rect SidebarRect()
        {
            if (sidebarCamera != null)
            {
                Rect pr = sidebarCamera.pixelRect; // bottom-left origin
                return new Rect(pr.x + 12f, Screen.height - pr.yMax + 12f, pr.width - 20f, pr.height - 24f);
            }
            // fallback: right 1/8 of the screen
            float w = Screen.width * 0.125f;
            return new Rect(Screen.width - w + 12f, 12f, w - 20f, Screen.height - 24f);
        }

        private Rect Centered(float w, float h) =>
            new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        private void DrawDim()
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            title = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = Color.white;

            button = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };

            banner = new GUIStyle(GUI.skin.label)
            { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            sideHeader = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            sideHeader.normal.textColor = new Color(0.7f, 0.75f, 0.85f);

            sideValue = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sideValue.normal.textColor = Color.white;

            buildButton = new GUIStyle(GUI.skin.button)
            { fontSize = 13, fontStyle = FontStyle.Bold };

            hint = new GUIStyle(GUI.skin.label)
            { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            hint.normal.textColor = new Color(1f, 0.9f, 0.5f);

            tooltip = new GUIStyle(GUI.skin.box)
            { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 8, 8) };
            tooltip.normal.textColor = Color.white;
        }
    }
}
