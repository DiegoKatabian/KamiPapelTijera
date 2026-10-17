using Cinemachine;
using UnityEngine;

/// <summary>
/// A code-driven cutscene shot: a Cinemachine vcam that sits in the scene at priority 0 and is
/// lifted above the player's cameras (CameraManager uses 10) while a beat plays, then dropped back.
/// The brain blends both ways with its default blend, so the camera returns to whatever mode the
/// player had. For short beats that are not on a Timeline (#231, #232, #233); a Timeline's
/// Cinemachine track overrides priorities anyway, so the two never fight.
///
/// Framing is the vcam itself (drag it, or its Transposer offset), not code.
/// </summary>
public static class CutsceneCamera
{
    //above every CameraManager mode (10), below nothing that matters
    const int LivePriority = 50;

    public static void Show(CinemachineVirtualCamera shot)
    {
        if (shot == null)
        {
            Debug.LogWarning("[CutsceneCamera] Show: no vcam assigned, the camera stays where it is");
            return;
        }

        shot.Priority = LivePriority;
        Debug.Log($"[CutsceneCamera] live: {shot.name}");
    }

    public static void Hide(CinemachineVirtualCamera shot)
    {
        if (shot == null)
        {
            return;
        }

        shot.Priority = 0;
        Debug.Log($"[CutsceneCamera] back to the player's camera from {shot.name}");
    }

    /// <summary>Seconds the brain's default blend takes, so a beat can wait for the camera to arrive.</summary>
    public static float BlendSeconds
    {
        get
        {
            CinemachineBrain brain = Object.FindObjectOfType<CinemachineBrain>();
            return brain != null ? brain.m_DefaultBlend.BlendTime : 0f;
        }
    }
}
