using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SceneCatalogEntry : ISerializationCallbackReceiver
{
    public GameScene scene;

#if UNITY_EDITOR
    [Tooltip("Drag the .unity file here. The name and path below are baked from it on save, so " +
             "renaming the scene cannot break the reference.")]
    [SerializeField] UnityEditor.SceneAsset sceneAsset;
#endif

    //baked from sceneAsset in the editor; these are what ship in a player build
    [SerializeField, HideInInspector] string scenePath;
    [SerializeField, HideInInspector] string sceneName;

    [Tooltip("Cutscene/storyboard scene: keyword highlighting stays off and text renders plain.")]
    public bool isCinematic;

    public string ScenePath => scenePath;
    public string SceneName => sceneName;

#if UNITY_EDITOR
    public bool HasSceneAsset => sceneAsset != null;
#endif

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        if (sceneAsset != null)
        {
            scenePath = UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
            sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
        else
        {
            scenePath = "";
            sceneName = "";
        }
#endif
    }

    public void OnAfterDeserialize() { }
}

/// <summary>
/// The single source of truth for which .unity file each GameScene value points at.
///
/// Self-loading from Resources, same pattern as TextHighlightSettings: nothing has to be wired in
/// any inspector, and a missing asset degrades to loud errors rather than a NullReference.
/// </summary>
[CreateAssetMenu(fileName = "SceneCatalog", menuName = "Kami/Scene Catalog")]
public class SceneCatalog : ScriptableObject
{
    const string RESOURCE_NAME = "SceneCatalog";

    [SerializeField] List<SceneCatalogEntry> entries = new List<SceneCatalogEntry>();

    static SceneCatalog _instance;
    static bool _missingReported;

    public IReadOnlyList<SceneCatalogEntry> Entries => entries;

    public static SceneCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<SceneCatalog>(RESOURCE_NAME);
                if (_instance == null && !_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError($"[SceneCatalog] could not find Resources/{RESOURCE_NAME}.asset: " +
                                   "cannot resolve any scene name. Create it with " +
                                   "Create > Kami > Scene Catalog and put it in Assets/Resources.");
                }
            }
            return _instance;
        }
    }

    public static string NameOf(GameScene scene)
    {
        SceneCatalogEntry entry = Find(scene);
        if (entry == null || string.IsNullOrEmpty(entry.SceneName))
        {
            Debug.LogError($"[SceneCatalog] scene '{scene}' has no valid row in the catalog. " +
                           "Run Kami/Validate Scene Catalog.");
            return "";
        }
        return entry.SceneName;
    }

    public static string PathOf(GameScene scene)
    {
        SceneCatalogEntry entry = Find(scene);
        if (entry == null || string.IsNullOrEmpty(entry.ScenePath))
        {
            Debug.LogError($"[SceneCatalog] scene '{scene}' has no valid path in the catalog.");
            return "";
        }
        return entry.ScenePath;
    }

    public static bool IsCinematic(GameScene scene)
    {
        SceneCatalogEntry entry = Find(scene);
        return entry != null && entry.isCinematic;
    }

    /// <summary>
    /// Reverse lookup by loaded scene name. Returns false for scenes that are not in the catalog
    /// (test scenes, Spine sample scenes) so callers can fall back instead of erroring.
    /// </summary>
    public static bool TryResolve(string sceneName, out GameScene scene)
    {
        scene = default;
        if (string.IsNullOrEmpty(sceneName) || Instance == null)
        {
            return false;
        }

        for (int i = 0; i < Instance.entries.Count; i++)
        {
            if (Instance.entries[i].SceneName == sceneName)
            {
                scene = Instance.entries[i].scene;
                return true;
            }
        }
        return false;
    }

    static SceneCatalogEntry Find(GameScene scene)
    {
        if (Instance == null)
        {
            return null;
        }

        for (int i = 0; i < Instance.entries.Count; i++)
        {
            if (Instance.entries[i].scene == scene)
            {
                return Instance.entries[i];
            }
        }
        return null;
    }
}
