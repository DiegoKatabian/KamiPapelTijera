using UnityEngine;

/// <summary>
/// What a storyboard cutscene loads when it ends, when that depends on how the player got there.
///
/// Nivel1_EndCutscene is reached two ways: by finishing Level 1 (it then ends in the main menu, its
/// own Inspector setting) and by picking Level 2 in the main menu's level select (it then leads into
/// Level 2). There are no saves, so one static request is enough: the level select sets it, the
/// cutscene consumes it in its Start, and clearing it on read means a later Level 1 playthrough in the
/// same session gets the normal ending again.
/// </summary>
public static class SceneFlow
{
    static bool _hasRequest;
    static GameScene _sceneAfterCutscene;

    //Domain reload can be off in the Editor: a request left over from a stopped Play session must not
    //redirect the next one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        _hasRequest = false;
    }

    /// <summary>The next storyboard cutscene that starts loads this scene instead of its own setting.</summary>
    public static void RequestSceneAfterCutscene(GameScene scene)
    {
        _hasRequest = true;
        _sceneAfterCutscene = scene;
        Debug.Log($"[SceneFlow] the next storyboard cutscene will lead into '{scene}'");
    }

    /// <summary>Takes the pending request, if any, and clears it.</summary>
    public static bool TryConsumeSceneAfterCutscene(out GameScene scene)
    {
        scene = _sceneAfterCutscene;
        if (!_hasRequest)
        {
            return false;
        }

        _hasRequest = false;
        return true;
    }
}
