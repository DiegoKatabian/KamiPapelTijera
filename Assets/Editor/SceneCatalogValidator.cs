using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the three things that can silently break a scene load, and that nothing else checks:
/// a GameScene value with no row, a row with no scene assigned, and a scene that exists but is
/// NOT enabled in Build Settings (which fails only at build time or at runtime load).
/// </summary>
public static class SceneCatalogValidator
{
    [MenuItem("Kami/Validate Scene Catalog")]
    public static void Validate()
    {
        SceneCatalog catalog = Resources.Load<SceneCatalog>("SceneCatalog");
        if (catalog == null)
        {
            Debug.LogError("[SceneCatalogValidator] there is no Resources/SceneCatalog.asset.");
            return;
        }

        int problems = 0;
        var seen = new Dictionary<GameScene, int>();

        foreach (SceneCatalogEntry entry in catalog.Entries)
        {
            if (seen.ContainsKey(entry.scene))
            {
                Debug.LogError($"[SceneCatalogValidator] '{entry.scene}' is duplicated in the catalog.");
                problems++;
                continue;
            }
            seen[entry.scene] = 1;

            if (!entry.HasSceneAsset || string.IsNullOrEmpty(entry.ScenePath))
            {
                Debug.LogError($"[SceneCatalogValidator] '{entry.scene}' has no scene assigned.");
                problems++;
                continue;
            }

            if (!IsEnabledInBuildSettings(entry.ScenePath))
            {
                Debug.LogError($"[SceneCatalogValidator] '{entry.scene}' ({entry.ScenePath}) is not " +
                               "enabled in Build Settings: it will fail to load in a build.");
                problems++;
            }
        }

        foreach (GameScene scene in System.Enum.GetValues(typeof(GameScene)))
        {
            if (!seen.ContainsKey(scene))
            {
                Debug.LogError($"[SceneCatalogValidator] missing the row for '{scene}' in the catalog.");
                problems++;
            }
        }

        if (problems == 0)
        {
            Debug.Log($"[SceneCatalogValidator] OK: {seen.Count} scenes, all assigned and in Build Settings.");
        }
    }

    static bool IsEnabledInBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == scenePath)
            {
                return scene.enabled;
            }
        }
        return false;
    }
}
