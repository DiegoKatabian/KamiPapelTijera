using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
    const string AUDIO_BANK_PATH = "Assets/Resources/AudioBank.asset";

    //Not for the demo: Level 2's music is commercially licensed (Bohren & Der Club Of Gore, and the
    //"DeadEndAngels" track next to it) and the QuestCompleted/NewsJingle clip is named after The Sims' build
    //mode. The demo has no Level 2, but the AudioBank sits in Resources, so every clip it references ships
    //whether or not anything plays it: those entries lose their clips for the length of the build.
    static readonly string[] DEMO_EXCLUDED_AUDIO_IDS = { "BohrenDestroyingAngels", "DeadEndAngels", "QuestCompleted", "NewsJingle" };

    [MenuItem("Kami/Build/Steam Demo (Level 1)")]
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
            options = BuildOptions.None, //never a development build: it would ship the profiler hooks
            extraScriptingDefines = new[] { DEMO_DEFINE }
        };

        Debug.Log($"[SteamDemoBuild] building version {PlayerSettings.bundleVersion} as Steam app {SteamAppIds.Demo} (KAMI_STEAM_DEMO): " +
                  string.Join(", ", scenes));

        List<KeyValuePair<int, Object[]>> strippedAudio = StripExcludedAudio();
        UnityEditor.Build.Reporting.BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            RestoreExcludedAudio(strippedAudio); //the AudioBank must never stay stripped, whatever the build did
        }
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

    static List<KeyValuePair<int, Object[]>> StripExcludedAudio()
    {
        List<KeyValuePair<int, Object[]>> stripped = new List<KeyValuePair<int, Object[]>>();
        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(AUDIO_BANK_PATH);
        if (bank == null)
        {
            Debug.LogWarning($"[SteamDemoBuild] no AudioBank at {AUDIO_BANK_PATH}: nothing to strip from it");
            return stripped;
        }

        SerializedObject bankObject = new SerializedObject(bank);
        SerializedProperty entries = bankObject.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("id").stringValue;
            if (System.Array.IndexOf(DEMO_EXCLUDED_AUDIO_IDS, id) < 0)
            {
                continue;
            }

            SerializedProperty clips = entry.FindPropertyRelative("clips");
            Object[] saved = new Object[clips.arraySize];
            for (int c = 0; c < saved.Length; c++)
            {
                saved[c] = clips.GetArrayElementAtIndex(c).objectReferenceValue;
            }
            stripped.Add(new KeyValuePair<int, Object[]>(i, saved));
            clips.ClearArray();
            Debug.Log($"[SteamDemoBuild] audio '{id}' has no clips in this build ({saved.Length} taken out until it ends)");
        }

        bankObject.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return stripped;
    }

    static void RestoreExcludedAudio(List<KeyValuePair<int, Object[]>> stripped)
    {
        if (stripped == null || stripped.Count == 0)
        {
            return;
        }

        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(AUDIO_BANK_PATH);
        SerializedObject bankObject = new SerializedObject(bank);
        SerializedProperty entries = bankObject.FindProperty("entries");
        foreach (KeyValuePair<int, Object[]> pair in stripped)
        {
            SerializedProperty clips = entries.GetArrayElementAtIndex(pair.Key).FindPropertyRelative("clips");
            clips.arraySize = pair.Value.Length;
            for (int c = 0; c < pair.Value.Length; c++)
            {
                clips.GetArrayElementAtIndex(c).objectReferenceValue = pair.Value[c];
            }
        }
        bankObject.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SteamDemoBuild] AudioBank restored ({stripped.Count} entries got their clips back)");
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
