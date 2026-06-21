using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DungeonWarfare.EditorTools
{
    /// <summary>
    /// One-click generator for a playable 2D tower-defense demo:
    /// creates placeholder sprites, the Enemy + Tower + Projectile prefabs, and a scene
    /// wired up with a camera, path, build grid, spawner, tower placer and HUD.
    ///
    /// Menu: Tools/Dungeon Warfare/Build Demo Scene
    /// </summary>
    public static class DungeonWarfareSceneBuilder
    {
        private const string ArtDir = "Assets/Art";
        // Prefabs live under Resources so the runtime can auto-load them as a
        // fallback even if the inspector references aren't wired.
        private const string PrefabDir = "Assets/Resources";
        private const string SceneDir = "Assets/Scenes";
        private const string SquarePath = ArtDir + "/WhiteSquare.png";
        private const string CirclePath = ArtDir + "/WhiteCircle.png";
        private const string EnemyPrefabPath = PrefabDir + "/Enemy.prefab";
        private const string TowerPrefabPath = PrefabDir + "/Tower.prefab";
        private const string BombTowerPrefabPath = PrefabDir + "/BombTower.prefab";
        private const string ProjectilePrefabPath = PrefabDir + "/Projectile.prefab";
        private const string BombProjectilePrefabPath = PrefabDir + "/BombProjectile.prefab";
        private const string InjectionTowerPrefabPath = PrefabDir + "/InjectionTower.prefab";
        private const string InjectionProjectilePrefabPath = PrefabDir + "/InjectionProjectile.prefab";
        private const string AimTowerPrefabPath = PrefabDir + "/AimTower.prefab";
        private const string AimProjectilePrefabPath = PrefabDir + "/AimProjectile.prefab";
        private const string LightningTowerPrefabPath = PrefabDir + "/LightningTower.prefab";
        private const string LightningProjectilePrefabPath = PrefabDir + "/LightningProjectile.prefab";
        private const string LaserTowerPrefabPath = PrefabDir + "/LaserTower.prefab";
        private const string VeteranTowerPrefabPath = PrefabDir + "/VeteranTower.prefab";
        private const string VeteranProjectilePrefabPath = PrefabDir + "/VeteranProjectile.prefab";
        private const string PoisonTowerPrefabPath = PrefabDir + "/PoisonTower.prefab";
        private const string TerrainPrefabPath = PrefabDir + "/Terrain.prefab";
        private const string ScenePath = SceneDir + "/DungeonWarfare.unity";

        // Shared unit size: square side length == circle diameter.
        // Slightly under one 0.5-unit cell so units sit inside their cell.
        private const float UnitSize = 0.45f;

        // Enemies are half the tower's size: smaller body leaves lateral room in
        // the corridors so the continuous (corner-cutting) movement doesn't clip walls.
        private const float EnemySize = UnitSize * 0.5f;

        [MenuItem("Tools/Dungeon Warfare/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Sprite square = EnsureSquareSprite();
            Sprite circle = EnsureCircleSprite();
            Projectile projectilePrefab = EnsureProjectilePrefab(circle);
            Tower towerPrefab = EnsureTowerPrefab(square, projectilePrefab);
            BombProjectile bombProjectilePrefab = EnsureBombProjectilePrefab(circle);
            Tower bombTowerPrefab = EnsureBombTowerPrefab(square, bombProjectilePrefab);
            InjectionProjectile injectionProjectilePrefab = EnsureInjectionProjectilePrefab(circle);
            Tower injectionTowerPrefab = EnsureInjectionTowerPrefab(square, injectionProjectilePrefab);
            AimProjectile aimProjectilePrefab = EnsureAimProjectilePrefab(circle);
            Tower aimTowerPrefab = EnsureAimTowerPrefab(square, aimProjectilePrefab);
            LightningProjectile lightningProjectilePrefab = EnsureLightningProjectilePrefab();
            Tower lightningTowerPrefab = EnsureLightningTowerPrefab(square, lightningProjectilePrefab);
            Tower laserTowerPrefab = EnsureLaserTowerPrefab(square);
            VeteranProjectile veteranProjectilePrefab = EnsureVeteranProjectilePrefab(circle);
            Tower veteranTowerPrefab = EnsureVeteranTowerPrefab(square, veteranProjectilePrefab);
            Tower poisonTowerPrefab = EnsurePoisonTowerPrefab(square);
            Terrain terrainPrefab = EnsureTerrainPrefab(square);
            Enemy enemyPrefab = EnsureEnemyPrefab(circle, square);

            BuildScene(square, circle, enemyPrefab,
                       new[] { towerPrefab, bombTowerPrefab, injectionTowerPrefab, aimTowerPrefab,
                               lightningTowerPrefab, laserTowerPrefab, veteranTowerPrefab, poisonTowerPrefab },
                       terrainPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("[DungeonWarfare] Demo scene built. Press Play -> 开始游戏 -> 第 1 关, then left-click cells to build towers.");
        }

        // ---------- Sprites ----------
        // Both base sprites are authored at 1 world unit, so scaling enemy and
        // tower by the same UnitSize gives "square side == circle diameter".

        private static Sprite EnsureSquareSprite()
        {
            EnsureFolder(ArtDir);

            if (!File.Exists(SquarePath))
            {
                var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = Enumerable.Repeat((Color32)Color.white, 32 * 32).ToArray();
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SquarePath, ImportAssetOptions.ForceSynchronousImport);
            }

            // Solid square: point filter keeps edges crisp at any zoom.
            return ConfigureSpriteImporter(SquarePath, 32f, FilterMode.Point);
        }

        private static Sprite EnsureCircleSprite()
        {
            EnsureFolder(ArtDir);

            if (!File.Exists(CirclePath))
            {
                const int size = 256; // high-res so the range disc stays smooth when scaled up
                const float radius = size / 2f;
                var center = new Vector2(radius, radius);
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        float alpha = Mathf.Clamp01(radius - dist); // 1px soft edge
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(CirclePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(CirclePath, ImportAssetOptions.ForceSynchronousImport);
            }

            // Soft-edged circle: bilinear + high res => smooth even when enlarged.
            return ConfigureSpriteImporter(CirclePath, 256f, FilterMode.Bilinear);
        }

        private static Sprite ConfigureSpriteImporter(string path, float pixelsPerUnit, FilterMode filter)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit; // full texture => 1 world unit
            importer.filterMode = filter;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // no compression blur
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- Prefabs ----------

        private static Enemy EnsureEnemyPrefab(Sprite circle, Sprite square)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("Enemy");
            go.transform.localScale = new Vector3(EnemySize, EnemySize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(0.85f, 0.25f, 0.25f);
            sr.sortingOrder = 2;

            // Kinematic body + trigger collider so towers can target it via
            // overlap queries, without physically shoving anything.
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            // Circle enemy => circle collider; radius 0.5 (local) matches the
            // 1-unit sprite, so its diameter equals the tower's side length.
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.isTrigger = true;

            go.AddComponent<Health>().Configure(30f);
            go.AddComponent<PathFollower>();
            go.AddComponent<EnemyStatus>(); // holds poison/DOT (and future slow) effects
            Enemy enemy = go.AddComponent<Enemy>();

            // Health bar (appears only after first damage); uses the square sprite.
            EnemyHealthBar bar = go.AddComponent<EnemyHealthBar>();
            SetRef(bar, "barSprite", square);

            Enemy asset = PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefabPath).GetComponent<Enemy>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Projectile EnsureProjectilePrefab(Sprite circle)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("Projectile");
            go.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(1f, 0.95f, 0.4f);
            sr.sortingOrder = 4;

            go.AddComponent<Projectile>();

            Projectile asset = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath).GetComponent<Projectile>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureTowerPrefab(Sprite square, Projectile projectilePrefab)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("Tower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.30f, 0.70f, 0.85f);
            sr.sortingOrder = 3;

            // Trigger collider (~1 cell) only so the mouse can hover-pick the
            // tower for its range display; it doesn't affect combat or movement.
            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            Tower tower = go.AddComponent<Tower>();
            SetRef(tower, "projectilePrefab", projectilePrefab);

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, TowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static BombProjectile EnsureBombProjectilePrefab(Sprite circle)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("BombProjectile");
            go.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(1f, 0.55f, 0.2f); // orange shell
            sr.sortingOrder = 4;

            go.AddComponent<BombProjectile>(); // knockback/AoE values come from DebugTuning

            BombProjectile asset = PrefabUtility.SaveAsPrefabAsset(go, BombProjectilePrefabPath)
                .GetComponent<BombProjectile>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureBombTowerPrefab(Sprite square, Projectile projectilePrefab)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("BombTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.85f, 0.5f, 0.25f); // orange = cannon
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            Tower tower = go.AddComponent<Tower>();
            SetRef(tower, "projectilePrefab", projectilePrefab);
            SetString(tower, "displayName", "加农炮");
            SetInt(tower, "cost", 50);
            SetFloat(tower, "fireInterval", 1.8f);
            SetFloat(tower, "damage", 25f);

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, BombTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static InjectionProjectile EnsureInjectionProjectilePrefab(Sprite circle)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("InjectionProjectile");
            go.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(0.45f, 0.95f, 0.35f); // toxic green dart
            sr.sortingOrder = 4;

            go.AddComponent<InjectionProjectile>(); // direct hit + poison via DebugTuning

            InjectionProjectile asset = PrefabUtility.SaveAsPrefabAsset(go, InjectionProjectilePrefabPath)
                .GetComponent<InjectionProjectile>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureInjectionTowerPrefab(Sprite square, Projectile projectilePrefab)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("InjectionTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.4f, 0.75f, 0.35f); // green = poison
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            Tower tower = go.AddComponent<Tower>();
            SetRef(tower, "projectilePrefab", projectilePrefab);
            SetString(tower, "displayName", "病毒注射");
            SetInt(tower, "cost", 45);
            SetFloat(tower, "fireInterval", 0.8f); // faster than poison drop interval so stacks build
            SetFloat(tower, "damage", 2f);         // small direct hit; most damage comes from poison

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, InjectionTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static AimProjectile EnsureAimProjectilePrefab(Sprite circle)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("AimProjectile");
            go.transform.localScale = new Vector3(0.14f, 0.14f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(0.85f, 0.45f, 1f); // purple sniper round (vulnerability theme)
            sr.sortingOrder = 4;

            go.AddComponent<AimProjectile>(); // heavy hit + vulnerability mark via DebugTuning

            AimProjectile asset = PrefabUtility.SaveAsPrefabAsset(go, AimProjectilePrefabPath)
                .GetComponent<AimProjectile>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureAimTowerPrefab(Sprite square, Projectile projectilePrefab)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("AimTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.55f, 0.4f, 0.8f); // purple = sniper / vulnerability
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            Tower tower = go.AddComponent<Tower>();
            SetRef(tower, "projectilePrefab", projectilePrefab);
            SetString(tower, "displayName", "狙击炮");
            SetInt(tower, "cost", 60);
            SetFloat(tower, "range", 8f);          // long reach: tag enemies far out
            SetFloat(tower, "fireInterval", 2.0f); // slow
            SetFloat(tower, "damage", 35f);        // heavy single hit

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, AimTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static LightningProjectile EnsureLightningProjectilePrefab()
        {
            EnsureFolder(PrefabDir);

            // Hitscan: no flying body/sprite — the strike resolves instantly and the
            // only visual is the LightningFx arc. Just a holder for the component.
            var go = new GameObject("LightningProjectile");
            go.AddComponent<LightningProjectile>(); // chain hops via DebugTuning

            LightningProjectile asset = PrefabUtility.SaveAsPrefabAsset(go, LightningProjectilePrefabPath)
                .GetComponent<LightningProjectile>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureLightningTowerPrefab(Sprite square, Projectile projectilePrefab)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("LightningTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.35f, 0.7f, 1f); // electric blue = chain lightning
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            Tower tower = go.AddComponent<Tower>();
            SetRef(tower, "projectilePrefab", projectilePrefab);
            SetString(tower, "displayName", "闪电链");
            SetInt(tower, "cost", 50);
            SetFloat(tower, "range", 4.5f);
            SetFloat(tower, "fireInterval", 0.5f); // fast (A22 连发加速 branch)
            SetFloat(tower, "damage", 8f);         // low per-shot; value comes from chaining

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, LightningTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureLaserTowerPrefab(Sprite square)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("LaserTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.9f, 0.35f, 0.3f); // red = laser
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            // No projectile: the laser is a continuous beam (LaserTower drives its own
            // damage + LineRenderer). Base dps lives in the tower's damage field.
            Tower tower = go.AddComponent<LaserTower>();
            SetString(tower, "displayName", "激光");
            SetInt(tower, "cost", 55);
            SetFloat(tower, "range", 4f);
            SetFloat(tower, "damage", 6f); // base dps; ramps up via DebugTuning

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, LaserTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static VeteranProjectile EnsureVeteranProjectilePrefab(Sprite circle)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("VeteranProjectile");
            go.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(1f, 0.85f, 0.35f); // gold round
            sr.sortingOrder = 4;

            go.AddComponent<VeteranProjectile>(); // credits the firing veteran on a last-hit

            VeteranProjectile asset = PrefabUtility.SaveAsPrefabAsset(go, VeteranProjectilePrefabPath)
                .GetComponent<VeteranProjectile>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsureVeteranTowerPrefab(Sprite square, Projectile projectilePrefab)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("VeteranTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.6f, 0.55f, 0.35f); // drab → tints gold as it grows
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            Tower tower = go.AddComponent<VeteranTower>();
            SetRef(tower, "projectilePrefab", projectilePrefab);
            SetString(tower, "displayName", "老兵");
            SetInt(tower, "cost", 50);
            SetFloat(tower, "range", 4f);
            SetFloat(tower, "fireInterval", 0.4f); // fast (A22 连发加速 branch)
            SetFloat(tower, "damage", 5f);         // low base; grows per kill

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, VeteranTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Tower EnsurePoisonTowerPrefab(Sprite square)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("PoisonTower");
            go.transform.localScale = new Vector3(UnitSize, UnitSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.45f, 0.7f, 0.3f); // toxic green
            sr.sortingOrder = 3;

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.1f, 1.1f);
            hitbox.isTrigger = true;

            // No projectile: fires an expanding fan-ring wave (PoisonWave) on each shot.
            // Per-wave damage lives in the tower's damage field.
            Tower tower = go.AddComponent<PoisonTower>();
            SetString(tower, "displayName", "毒素炮");
            SetInt(tower, "cost", 45);
            SetFloat(tower, "range", 2.5f);        // small
            SetFloat(tower, "fireInterval", 0.6f); // wave emission rate
            SetFloat(tower, "damage", 8f);         // damage per wave hit

            Tower asset = PrefabUtility.SaveAsPrefabAsset(go, PoisonTowerPrefabPath).GetComponent<Tower>();
            Object.DestroyImmediate(go);
            return asset;
        }

        private static Terrain EnsureTerrainPrefab(Sprite square)
        {
            EnsureFolder(PrefabDir);

            var go = new GameObject("Terrain");
            // Fills its road cell (cell = 0.5 units); a tower can sit on top.
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.5f, 0.46f, 0.38f); // earth/stone
            sr.sortingOrder = 1; // above tiles, below towers/enemies

            go.AddComponent<Terrain>();

            Terrain asset = PrefabUtility.SaveAsPrefabAsset(go, TerrainPrefabPath).GetComponent<Terrain>();
            Object.DestroyImmediate(go);
            return asset;
        }

        // ---------- Scene ----------

        private static void BuildScene(Sprite square, Sprite circle, Enemy enemyPrefab,
                                       Tower[] towerPrefabs, Terrain terrainPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Whole-map grid, sized and shaped from the ASCII MapRows below.
            var gridGo = new GameObject("Grid");
            GridSystem grid = gridGo.AddComponent<GridSystem>();
            SetRef(grid, "tileSprite", square);

            int columns = MapRows[0].Length;
            int rows = MapRows.Length;
            float cellSize = grid.CellSize; // 0.5
            SetInt(grid, "columns", columns);
            SetInt(grid, "rows", rows);
            SetVector2(grid, "origin", new Vector2(-columns * cellSize / 2f, -rows * cellSize / 2f));

            Vector2Int[] pathCells = ParseMap(out Vector2Int entryCell, out Vector2Int exitCell);
            SetVector2IntArray(grid, "pathCells", pathCells);
            SetVector2Int(grid, "entryCell", entryCell);
            SetVector2Int(grid, "exitCell", exitCell);

            // World camera (orthographic 2D): exactly frames the grid height.
            // Its viewport is the left 14:9 region.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = rows * cellSize / 2f;
            cam.backgroundColor = new Color(0.13f, 0.14f, 0.18f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // Sidebar camera: renders nothing, just fills the right 2/16 with a
            // solid panel color (Red Alert style command bar, empty for now).
            var sidebarGo = new GameObject("Sidebar Camera");
            var sidebarCam = sidebarGo.AddComponent<Camera>();
            sidebarCam.orthographic = true;
            sidebarCam.cullingMask = 0; // draw no world objects
            sidebarCam.clearFlags = CameraClearFlags.SolidColor;
            sidebarCam.backgroundColor = new Color(0.10f, 0.11f, 0.14f);
            sidebarCam.depth = 1;
            sidebarGo.transform.position = new Vector3(100f, 100f, -10f); // out of the way

            // Layout: lock 16:9 and split into play (left 14) + sidebar (right 2)
            var layout = camGo.AddComponent<GameViewportLayout>();
            SetRef(layout, "worldCamera", cam);
            SetRef(layout, "sidebarCamera", sidebarCam);

            // Systems root
            var systems = new GameObject("Systems");
            GameManager game = systems.AddComponent<GameManager>();
            WaveManager waves = systems.AddComponent<WaveManager>();
            GridPlacer placer = systems.AddComponent<GridPlacer>();
            GameFlow flow = systems.AddComponent<GameFlow>();
            GameUI ui = systems.AddComponent<GameUI>();

            // Wire serialized references (enemies route via the grid directly)
            SetRef(waves, "enemyPrefab", enemyPrefab);
            SetRef(waves, "grid", grid);
            SetRef(waves, "game", game);

            SetObjectArray(placer, "towerPrefabs", towerPrefabs);
            SetRef(placer, "terrainPrefab", terrainPrefab);
            SetRef(placer, "grid", grid);
            SetRef(placer, "cam", cam);
            SetRef(placer, "game", game);
            SetRef(placer, "ghostSprite", square);
            SetRef(placer, "rangeSprite", circle);

            SetRef(flow, "game", game);
            SetRef(flow, "waves", waves);
            SetRef(flow, "grid", grid);

            SetRef(ui, "flow", flow);
            SetRef(ui, "game", game);
            SetRef(ui, "waves", waves);
            SetRef(ui, "placer", placer);
            SetRef(ui, "sidebarCamera", sidebarCam);

            EnsureFolder(SceneDir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        // ---------- Map ----------
        // Edit this ASCII map to reshape the level. Top line = top row.
        //   '#' = road (enemies walk)   '.' = buildable ground
        //   'E' = entry (road)          'X' = exit (road)
        // The enemy route is found automatically (BFS) over the road cells.
        private static readonly string[] MapRows =
        {
            "############################",
            "E###########################",
            "############################",
            ".........................###",
            ".........................###",
            ".........................###",
            "############################",
            "############################",
            "############################",
            "###.........................",
            "###.........................",
            "###.........................",
            "############################",
            "############################",
            "############################",
            ".........................###",
            ".........................###",
            ".........................##X",
        };

        /// <summary>Parse <see cref="MapRows"/> into road cells + entry/exit.</summary>
        private static Vector2Int[] ParseMap(out Vector2Int entry, out Vector2Int exit)
        {
            int rows = MapRows.Length;
            int cols = MapRows[0].Length;
            var road = new List<Vector2Int>();
            entry = Vector2Int.zero;
            exit = Vector2Int.zero;

            for (int r = 0; r < rows; r++)
            {
                string line = MapRows[r];
                int gridRow = (rows - 1) - r; // first line is the top row
                for (int c = 0; c < cols && c < line.Length; c++)
                {
                    char ch = line[c];
                    if (ch == '.') continue; // buildable, not road

                    var cell = new Vector2Int(c, gridRow);
                    road.Add(cell);
                    if (ch == 'E') entry = cell;
                    else if (ch == 'X') exit = cell;
                }
            }
            return road.ToArray();
        }

        // ---------- Helpers ----------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing field '{field}' on {target}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing field '{field}' on {target}"); return; }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing field '{field}' on {target}"); return; }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing field '{field}' on {target}"); return; }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing array '{field}' on {target}"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing field '{field}' on {target}"); return; }
            prop.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2Int(Object target, string field, Vector2Int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing field '{field}' on {target}"); return; }
            prop.vector2IntValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2IntArray(Object target, string field, Vector2Int[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[DungeonWarfare] Missing array '{field}' on {target}"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).vector2IntValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            // Put the game scene FIRST (build index 0) so the exe boots into it,
            // not into a leftover SampleScene. Keep any other scenes after it.
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
