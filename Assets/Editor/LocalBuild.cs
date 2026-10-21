using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One click: Kami > Build > Local (Both Levels). A 64-bit Windows player with the whole game (MainMenu,
/// Level 1 and its end cutscene, Level 2 and its end cutscene) into Builds/Local/, to play and test on this PC.
/// Never uploaded anywhere: the Steam demo is SteamDemoBuild.
///
/// Differences from the demo build, on purpose:
/// - No KAMI_STEAM_DEMO and no AudioBank stripping: Level 2 and its music are in.
/// - DISABLESTEAMWORKS (Steamworks.NET's own switch): the build never talks to Steam. Without it,
///   SteamManager calls RestartAppIfNecessary with the demo's id (SteamAppIds.FullGame is still 0), and a
///   double-clicked exe gets closed and replaced by the demo installed in the Steam library. A steam_appid.txt
///   next to the exe would also stop that, but then Steam must be running and the account must own the demo;
///   with the define the folder runs anywhere, Steam or not.
/// Addressables (Localization's tables live on them) build with the player like in the demo build:
/// AddressableAssetSettings has m_BuildAddressablesWithPlayerBuild on.
/// </summary>
public static class LocalBuild
{
    const string OUTPUT_FOLDER = "Builds/Local";
    const string NO_STEAM_DEFINE = "DISABLESTEAMWORKS";

    //MainMenu first: build index 0 is the scene the game starts in
    static readonly GameScene[] SCENES =
    {
        GameScene.MainMenu,
        GameScene.Level1,
        GameScene.Level1EndCutscene,
        GameScene.Level2,
        GameScene.Level2EndCutscene
    };

    [MenuItem("Kami/Build/Local (Both Levels)")]
    public static void Build()
    {
        string[] scenes = ResolveScenes();
        if (scenes == null)
        {
            return;
        }

        string executable = Path.Combine(OUTPUT_FOLDER, PlayerSettings.productName + ".exe");
        if (Directory.Exists(OUTPUT_FOLDER))
        {
            Directory.Delete(OUTPUT_FOLDER, true); //a stale file from an older build must not ride along
        }
        Directory.CreateDirectory(OUTPUT_FOLDER);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = executable,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { NO_STEAM_DEFINE }
        };

        Debug.Log($"[LocalBuild] building version {PlayerSettings.bundleVersion} without Steam ({NO_STEAM_DEFINE}): " +
                  string.Join(", ", scenes));

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[LocalBuild] build {report.summary.result}: {report.summary.totalErrors} error(s), see the console above");
            return;
        }

        Debug.Log($"[LocalBuild] done: {Path.GetFullPath(executable)} ({report.summary.totalSize / (1024 * 1024)} MB). " +
                  "Double-click the exe to play.");
        EditorUtility.RevealInFinder(Path.GetFullPath(executable));
    }

    static string[] ResolveScenes()
    {
        SceneCatalog catalog = SceneCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError("[LocalBuild] no SceneCatalog (Resources/SceneCatalog.asset), not building");
            return null;
        }

        List<string> paths = new List<string>();
        foreach (GameScene scene in SCENES)
        {
            string path = null;
            foreach (SceneCatalogEntry entry in catalog.Entries)
            {
                if (entry.scene == scene)
                {
                    path = entry.ScenePath;
                    break;
                }
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"[LocalBuild] '{scene}' has no scene file in the SceneCatalog (path '{path}'), not building");
                return null;
            }
            paths.Add(path);
        }
        return paths.ToArray();
    }
}
