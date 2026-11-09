using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// EditMode checks of spec 025 Part B (D39): the imported hand, intro books and clips, and (once Kami > Book Intro >
/// Build has run) the prefabs and the Level 3 controller. Every test logs "[GenCheck] PASS/FAIL/SKIP ..." for Editor.log.
/// </summary>
public class BookIntroTests
{
    const string HandKgen = "Assets/Generated/NarratorHand/NarratorHand.kgen";

    static void Check(bool ok, string what)
    {
        Debug.Log($"[GenCheck] {(ok ? "PASS" : "FAIL")} {what}");
        Assert.IsTrue(ok, what);
    }

    static void Skip(string what)
    {
        Debug.Log($"[GenCheck] SKIP {what}");
        Assert.Inconclusive(what);
    }

    [Test]
    public void HandImports()
    {
        GameObject hand = AssetDatabase.LoadAssetAtPath<GameObject>(HandKgen);
        Check(hand != null, "NarratorHand.kgen imports as a GameObject");
        Check(hand.GetComponentsInChildren<MeshRenderer>(true).Length == 18, $"the hand has 18 pieces ({hand.GetComponentsInChildren<MeshRenderer>(true).Length})");
        Check(hand.transform.Find("NarratorHand/Root/Forearm/Wrist/Palm/Grip") != null, "the hand has its Grip socket");
        Check(hand.transform.Find("NarratorHand/Root/Forearm/Wrist/Palm/Carry") != null, "the hand has its Carry socket");
        foreach (MeshRenderer r in hand.GetComponentsInChildren<MeshRenderer>(true))
        {
            Check(r.sharedMaterials.All(m => m != null), $"hand piece {r.name} has its materials");
        }
    }

    [TestCase("Level1")]
    [TestCase("Level2")]
    public void IntroBookImports(string level)
    {
        GameObject book = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Generated/IntroBooks/IntroBook_{level}.kgen");
        Check(book != null, $"IntroBook_{level}.kgen imports");
        Transform moving = book.transform.Find($"IntroBook_{level}/Hinge/Moving");
        Check(moving != null, $"IntroBook_{level} has Hinge/Moving");
        MeshRenderer mr = moving.GetComponent<MeshRenderer>();
        Check(mr.sharedMaterials.Length == 4 && mr.sharedMaterials.All(m => m != null), $"IntroBook_{level} cover half has its 4 materials");
    }

    [TestCase("Assets/Generated/BookIntro/BookIntroClips_Level1.kgen", "Assets/Generated/IntroBooks/IntroBook_Level1.kgen")]
    [TestCase("Assets/Generated/BookIntro/BookIntroClips_Level2.kgen", "Assets/Generated/IntroBooks/IntroBook_Level2.kgen")]
    [TestCase("Assets/Generated/NarratorHand/NarratorHand_Level3.kgen", null)]
    public void ClipPathsResolve(string clipsPath, string bookPath)
    {
        GameObject hand = AssetDatabase.LoadAssetAtPath<GameObject>(HandKgen);
        GameObject book = bookPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(bookPath) : null;
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(clipsPath).OfType<AnimationClip>().ToArray();
        Check(clips.Length > 0, $"{clipsPath} has clips ({clips.Length})");
        foreach (AnimationClip clip in clips)
        {
            GameObject target = clip.name.StartsWith("BookIntro_Book") ? book : hand;
            int missing = 0;
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
            {
                if (target == null || target.transform.Find(b.path) == null)
                {
                    missing++;
                }
            }
            Check(missing == 0, $"{clip.name}: every curve's path exists on its object ({missing} missing)");
        }
    }

    [TestCase("Level1")]
    [TestCase("Level2")]
    public void PrefabIsWired(string level)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/BookIntro/BookIntro_{level}.prefab");
        if (prefab == null)
        {
            Skip($"BookIntro_{level}.prefab not built yet (Kami > Book Intro > Build)");
            return;
        }
        BookIntro intro = prefab.GetComponent<BookIntro>();
        Check(intro != null, $"BookIntro_{level} has BookIntro");
        SerializedObject so = new SerializedObject(intro);
        foreach (string f in new[] { "_playable", "_cutscene", "_hand", "_carrySocket", "_introBook", "_hinge", "_introFixed", "_introMoving", "_skipPrompt" })
        {
            Check(so.FindProperty(f).objectReferenceValue != null, $"BookIntro_{level}.{f} is wired");
        }
        PlayableDirector director = prefab.GetComponent<PlayableDirector>();
        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        Check(timeline != null, $"BookIntro_{level} has its timeline");
        foreach (TrackAsset t in timeline.GetOutputTracks())
        {
            if (t is AnimationTrack)
            {
                Check(director.GetGenericBinding(t) != null, $"BookIntro_{level} track {t.name} is bound");
            }
            if (t is CinemachineTrack)
            {
                foreach (TimelineClip c in t.GetClips())
                {
                    CinemachineShot shot = (CinemachineShot)c.asset;
                    Check(director.GetReferenceValue(shot.VirtualCamera.exposedName, out bool valid) != null && valid,
                          $"BookIntro_{level} shot {c.displayName} has its camera");
                }
            }
        }
    }

    [Test]
    public void Level3ControllerBuilt()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Generated/NarratorHand/NarratorHand.controller");
        if (controller == null)
        {
            Skip("NarratorHand.controller not built yet (Kami > Book Intro > Build)");
            return;
        }
        Check(controller.layers[0].stateMachine.states.Length >= 10, $"NarratorHand.controller has the Level 3 states ({controller.layers[0].stateMachine.states.Length})");
    }
}
