using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Kami > Book Intro > Build (missing only) (spec 025 D1, D13): builds what the Python generators can't safely write as
/// YAML, from the imported .kgen assets, then never touches it again (Diego owns the timing afterwards):
///   - Prefabs/BookIntro/BookIntro_Level1|2.prefab: BookIntro + PlayableDirector + CutsceneDirector, the hand and the
///     intro book (model instances of their .kgen, each with an Animator), three vcams, the skip prompt;
///   - Timelines/BookIntro/BookIntro_Level1|2.playable: the hand and book clips on Animation tracks, the three shots;
///   - an instance of each prefab at the root of its level scene;
///   - Generated/NarratorHand/NarratorHand.controller with the Level 3 clips (task 7.A).
/// Anything that already exists is left alone: delete it to have it rebuilt.
/// </summary>
public static class BookIntroBuilder
{
    const string HandKgen = "Assets/Generated/NarratorHand/NarratorHand.kgen";
    const string Level3Kgen = "Assets/Generated/NarratorHand/NarratorHand_Level3.kgen";
    const string ControllerPath = "Assets/Generated/NarratorHand/NarratorHand.controller";
    static readonly (string level, string scene)[] Levels =
    {
        ("Level1", "Assets/Scenes/Nivel1_KamiPapelTijera.unity"),
        ("Level2", "Assets/Scenes/Level2_Newspaper.unity"),
    };

    [MenuItem("Kami/Book Intro/Build (missing only)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        string before = SceneManager.GetActiveScene().path;
        List<string> report = new List<string>();
        foreach ((string level, string scene) in Levels)
        {
            string prefab = $"Assets/Prefabs/BookIntro/BookIntro_{level}.prefab";
            if (File.Exists(prefab))
            {
                report.Add($"{prefab}: exists, left alone");
            }
            else
            {
                report.Add(BuildPrefab(level, prefab));
            }
            report.Add(PlaceInScene(prefab, scene));
        }
        report.Add(BuildController());
        if (!string.IsNullOrEmpty(before))
        {
            EditorSceneManager.OpenScene(before, OpenSceneMode.Single);
        }
        Debug.Log("[BookIntroBuilder] " + string.Join("\n  ", report));
    }

    static T LoadSub<T>(string path, string name) where T : Object
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(o => o.name == name);
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }
        foreach (Transform c in root)
        {
            Transform f = Find(c, name);
            if (f != null)
            {
                return f;
            }
        }
        return null;
    }

    static string BuildPrefab(string level, string prefabPath)
    {
        string bookKgen = $"Assets/Generated/IntroBooks/IntroBook_{level}.kgen";
        string clipsKgen = $"Assets/Generated/BookIntro/BookIntroClips_{level}.kgen";
        GameObject handAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HandKgen);
        GameObject bookAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bookKgen);
        AnimationClip handClip = LoadSub<AnimationClip>(clipsKgen, $"BookIntro_Hand_{level}");
        AnimationClip bookClip = LoadSub<AnimationClip>(clipsKgen, $"BookIntro_Book_{level}");
        if (handAsset == null || bookAsset == null || handClip == null || bookClip == null)
        {
            return $"{prefabPath}: NOT built, an imported .kgen is missing (hand {handAsset != null}, book {bookAsset != null}, " +
                   $"hand clip {handClip != null}, book clip {bookClip != null}): let Unity import Assets/Generated, check the Console";
        }

        GameObject root = new GameObject($"BookIntro_{level}");
        try
        {
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            CutsceneDirector cutscene = root.AddComponent<CutsceneDirector>();
            BookIntro intro = root.AddComponent<BookIntro>();

            GameObject hand = (GameObject)PrefabUtility.InstantiatePrefab(handAsset);
            hand.transform.SetParent(root.transform, false);
            Animator handAnim = hand.AddComponent<Animator>();
            handAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            handAnim.applyRootMotion = false;

            GameObject book = (GameObject)PrefabUtility.InstantiatePrefab(bookAsset);
            book.transform.SetParent(root.transform, false);
            Animator bookAnim = book.AddComponent<Animator>();
            bookAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bookAnim.applyRootMotion = false;

            Transform carry = Find(hand.transform, "Carry");
            Transform hinge = Find(book.transform, "Hinge");
            Renderer fixedR = Find(book.transform, "Fixed")?.GetComponent<Renderer>();
            Renderer movingR = Find(book.transform, "Moving")?.GetComponent<Renderer>();

            // cameras, framed on the intro book's bounds
            Bounds b = new Bounds(book.transform.position, Vector3.zero);
            foreach (Renderer r in book.GetComponentsInChildren<Renderer>())
            {
                b.Encapsulate(r.bounds);
            }
            float size = Mathf.Max(b.size.x, b.size.z);
            CinemachineVirtualCamera establishing = Vcam(root, "CM Intro Establishing", b.center + new Vector3(0, 0.75f * size, -0.85f * size), b.center);
            CinemachineVirtualCamera opening = Vcam(root, "CM Intro Opening", b.center + new Vector3(0, 1.05f * size, -1.15f * size), b.center + new Vector3(0, 0.15f * size, 0));
            CinemachineVirtualCamera placing = Vcam(root, "CM Intro Placing", b.center + new Vector3(0, 0.5f * size, -0.8f * size), b.center);
            if (carry != null)
            {
                placing.Follow = carry;
                placing.LookAt = carry;
                CinemachineTransposer tr = placing.AddCinemachineComponent<CinemachineTransposer>();
                tr.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                tr.m_FollowOffset = new Vector3(0, 10, -40);
                placing.AddCinemachineComponent<CinemachineComposer>();
            }

            // skip prompt
            GameObject canvasGo = new GameObject("Skip Prompt Canvas");
            canvasGo.transform.SetParent(root.transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            GameObject textGo = new GameObject("Skip Prompt");
            textGo.transform.SetParent(canvasGo.transform, false);
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 30;
            text.alignment = TextAlignmentOptions.BottomRight;
            RectTransform rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-40, 30);
            rt.sizeDelta = new Vector2(700, 60);
            text.text = "";
            textGo.SetActive(false);

            // timeline
            Directory.CreateDirectory("Assets/Timelines/BookIntro");
            string timelinePath = $"Assets/Timelines/BookIntro/BookIntro_{level}.playable";
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, timelinePath);
                AnimationTrack handTrack = timeline.CreateTrack<AnimationTrack>(null, "Hand");
                TimelineClip hc = handTrack.CreateClip(handClip);
                hc.start = 0;
                hc.duration = handClip.length;
                AnimationTrack bookTrack = timeline.CreateTrack<AnimationTrack>(null, "Book");
                TimelineClip bc = bookTrack.CreateClip(bookClip);
                bc.start = 0;
                bc.duration = bookClip.length;
                CinemachineTrack camTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cameras");
                Shot(camTrack, director, establishing, 0, 3.2);
                Shot(camTrack, director, opening, 2.6, 4.4);
                Shot(camTrack, director, placing, 6.4, handClip.length - 6.4);
                EditorUtility.SetDirty(timeline);
            }
            director.playableAsset = timeline;
            foreach (TrackAsset t in timeline.GetOutputTracks())
            {
                if (t.name == "Hand")
                {
                    director.SetGenericBinding(t, handAnim);
                }
                else if (t.name == "Book")
                {
                    director.SetGenericBinding(t, bookAnim);
                }
                else if (t is CinemachineTrack ct)
                {
                    foreach (TimelineClip c in ct.GetClips())
                    {
                        CinemachineShot shot = (CinemachineShot)c.asset;
                        CinemachineVirtualCamera cam = c.displayName.Contains("Establishing") ? establishing
                            : c.displayName.Contains("Opening") ? opening : placing;
                        if (PropertyName.IsNullOrEmpty(shot.VirtualCamera.exposedName))
                        {
                            shot.VirtualCamera.exposedName = GUID.Generate().ToString();
                        }
                        director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
                    }
                }
            }

            SerializedObject so = new SerializedObject(intro);
            so.FindProperty("_playable").objectReferenceValue = director;
            so.FindProperty("_cutscene").objectReferenceValue = cutscene;
            so.FindProperty("_hand").objectReferenceValue = hand;
            so.FindProperty("_carrySocket").objectReferenceValue = carry;
            so.FindProperty("_introBook").objectReferenceValue = book;
            so.FindProperty("_hinge").objectReferenceValue = hinge;
            so.FindProperty("_introFixed").objectReferenceValue = fixedR;
            so.FindProperty("_introMoving").objectReferenceValue = movingR;
            so.FindProperty("_skipPrompt").objectReferenceValue = text;
            so.FindProperty("_end").floatValue = handClip.length;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory("Assets/Prefabs/BookIntro");
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
            return ok ? $"{prefabPath}: built (timeline {timelinePath})" : $"{prefabPath}: SaveAsPrefabAsset failed";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static CinemachineVirtualCamera Vcam(GameObject root, string name, Vector3 position, Vector3 lookAt)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
        CinemachineVirtualCamera vcam = go.AddComponent<CinemachineVirtualCamera>();
        vcam.Priority = 0;
        vcam.m_Lens.FieldOfView = 40;
        return vcam;
    }

    static void Shot(CinemachineTrack track, PlayableDirector director, CinemachineVirtualCamera cam, double start, double duration)
    {
        TimelineClip clip = track.CreateClip<CinemachineShot>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = cam.name;
        CinemachineShot shot = (CinemachineShot)clip.asset;
        shot.DisplayName = cam.name;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
    }

    static string PlaceInScene(string prefabPath, string scenePath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return $"{scenePath}: no prefab to place";
        }
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (Object.FindObjectOfType<BookIntro>(true) != null)
        {
            return $"{scenePath}: already has a BookIntro, left alone";
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.transform.position = Vector3.zero;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{scenePath}: BookIntro placed at the scene root";
    }

    static string BuildController()
    {
        if (File.Exists(ControllerPath))
        {
            return $"{ControllerPath}: exists, left alone";
        }
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(Level3Kgen).OfType<AnimationClip>().ToArray();
        if (clips.Length == 0)
        {
            return $"{ControllerPath}: NOT built, {Level3Kgen} has no imported clips yet";
        }
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (AnimationClip clip in clips.OrderBy(c => c.name))
        {
            AnimatorState state = sm.AddState(clip.name.Replace("Hand_", ""));
            state.motion = clip;
            if (clip.name == "Hand_Idle_Hover")
            {
                sm.defaultState = state;
            }
        }
        AssetDatabase.SaveAssets();
        return $"{ControllerPath}: built with {clips.Length} states";
    }
}
