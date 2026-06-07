using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DungeonWarfare.EditorTools
{
    /// <summary>
    /// One-click Windows build. Outputs to &lt;project&gt;/Build/Windows/DungeonWarfare.exe.
    /// Menu: Tools/Dungeon Warfare/Build Windows EXE
    /// </summary>
    public static class DungeonWarfareBuild
    {
        [MenuItem("Tools/Dungeon Warfare/Build Windows EXE")]
        public static void BuildWindows()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(root, "Build", "Windows");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "DungeonWarfare.exe");

            string[] scenes = GetScenesToBuild();
            if (scenes.Length == 0)
            {
                Debug.LogError("[DungeonWarfare] No scene to build. Run 'Build Demo Scene' first.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[DungeonWarfare] Build OK -> {exe}  ({summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(exe); // open the output folder
            }
            else
            {
                Debug.LogError($"[DungeonWarfare] Build {summary.result}: {summary.totalErrors} error(s). " +
                               "If the platform is missing, install the 'Windows Build Support' module in Unity Hub.");
            }
        }

        private const string MainScene = "Assets/Scenes/DungeonWarfare.unity";

        // Build ONLY the game scene so the exe always boots into it (never the
        // leftover SampleScene). Falls back to whatever is enabled if it's missing.
        private static string[] GetScenesToBuild()
        {
            if (File.Exists(MainScene)) return new[] { MainScene };

            var list = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }
    }
}
