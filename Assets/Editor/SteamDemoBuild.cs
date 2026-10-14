using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One click: Kami > Build > Steam Demo (Level 1). Builds a 64-bit Windows player with only the Level 1
/// scenes (MainMenu, Level 1, Level 1's end cutscene) into Builds/SteamDemo/, as the Steam DEMO app
/// (KAMI_STEAM_DEMO selects SteamAppIds.Demo), then removes what must never be uploaded.
///
/// The scene list is explicit on purpose: File > Build Settings stays as Diego's Level 2 work needs it, and
/// the scenes are looked up through the SceneCatalog, so a renamed scene keeps working. Builds/ is git-ignored.
/// </summary>
public static class SteamDemoBuild
{
    const string OUTPUT_FOLDER = "Builds/SteamDemo";
    const string DEMO_DEFINE = "KAMI_STEAM_DEMO";

    [MenuItem("Kami/Build/Steam Demo (Level 1)")]
    public static void Build()
    {
        if (SteamAppIds.Demo == 0)
        {
            EditorUtility.DisplayDialog("Steam demo build",
                "SteamAppIds.Demo is 0. Put the demo app's App ID in Assets/Scripts/Steamworks.NET/SteamAppIds.cs " +
                "(Steamworks > All Associated Software, the demo is a separate app from the full game).", "OK");
            Debug.LogError("[SteamDemoBuild] SteamAppIds.Demo is 0: the demo's App ID is missing, not building");
            return;
        }

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
            options = BuildOptions.None, //never a development build: it would ship the profiler hooks
            extraScriptingDefines = new[] { DEMO_DEFINE }
        };

        Debug.Log($"[SteamDemoBuild] building version {PlayerSettings.bundleVersion} as Steam app {SteamAppIds.Demo}: " +
                  string.Join(", ", scenes));

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[SteamDemoBuild] build {report.summary.result}: {report.summary.totalErrors} error(s), see the console above");
            return;
        }

        RemoveNonShippingFiles();

        Debug.Log($"[SteamDemoBuild] done: {Path.GetFullPath(OUTPUT_FOLDER)} " +
                  $"({report.summary.totalSize / (1024 * 1024)} MB). This folder is the SteamPipe ContentRoot.");
        EditorUtility.RevealInFinder(Path.GetFullPath(executable));
    }

    static string[] ResolveScenes()
    {
        SceneCatalog catalog = SceneCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogError("[SteamDemoBuild] no SceneCatalog (Resources/SceneCatalog.asset), not building");
            return null;
        }

        //MainMenu first: it is build index 0, the scene the game starts in
        GameScene[] wanted = { GameScene.MainMenu, GameScene.Level1, GameScene.Level1EndCutscene };
        List<string> paths = new List<string>();
        foreach (GameScene scene in wanted)
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
                Debug.LogError($"[SteamDemoBuild] '{scene}' has no scene file in the SceneCatalog (path '{path}'), not building");
                return null;
            }
            paths.Add(path);
        }
        return paths.ToArray();
    }

    //Unity writes debugging leftovers next to the exe, and a steam_appid.txt would switch off
    //RestartAppIfNecessary (the "only launch through Steam" check) for whoever downloads the build
    static void RemoveNonShippingFiles()
    {
        foreach (string directory in Directory.GetDirectories(OUTPUT_FOLDER))
        {
            string name = Path.GetFileName(directory);
            if (name.EndsWith("_BurstDebugInformation_DoNotShip") || name.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame"))
            {
                Directory.Delete(directory, true);
                Debug.Log($"[SteamDemoBuild] removed {name}");
            }
        }

        foreach (string file in Directory.GetFiles(OUTPUT_FOLDER, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (name == "steam_appid.txt" || name.EndsWith(".pdb"))
            {
                File.Delete(file);
                Debug.Log($"[SteamDemoBuild] removed {name}");
            }
        }
    }
}
