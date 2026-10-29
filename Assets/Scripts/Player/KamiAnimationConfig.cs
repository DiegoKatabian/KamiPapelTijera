using UnityEngine;

/// <summary>
/// A story beat on Kami's body, played by an AnimationCue addressed to actor "Kami" (a DialogueSO line or
/// its end) or by code (Player.PlayAnimationPose). Only while she stands still: walking ends it.
/// </summary>
[System.Serializable]
public class KamiPose
{
    [Tooltip("Name cues and code use (Surprised, Celebrate...). Not case-sensitive.")]
    public string id;

    [Tooltip("Spine animation played once before Main (Sorprendida). Empty = straight to Main.")]
    public string intro;

    [Tooltip("Spine animation of the pose itself (Sorprendida Loop).")]
    public string main;

    [Tooltip("On: Main loops until the pose ends (a cue, code, her moving or Hold Seconds). Off: Main plays once, then back to idle.")]
    public bool loop = true;

    [Tooltip("Seconds until the pose ends by itself, counted from its start. 0 = it holds until ended (or, with Loop off, until Main has played).")]
    public float holdSeconds;
}

/// <summary>Tuning of Kami's idle variety, bench sitting, reflections and story poses (Atlas 15 animations).</summary>
[System.Serializable]
public class KamiAnimationConfig
{
    [Header("Idle break")]
    [Tooltip("Shortest wait standing still before the Idle Break plays, seconds.")]
    public float idleBreakMinSeconds = 5f;
    [Tooltip("Longest wait standing still before the Idle Break plays, seconds. A new random wait is rolled after each break.")]
    public float idleBreakMaxSeconds = 10f;

    [Header("Reflections")]
    [Tooltip("Seconds standing still inside a reflecting pool's zone (TriggerReflection) before she looks into it.")]
    public float lookIntoReflectionDelay = 1f;

    [Header("Story poses (AnimationCue actor 'Kami')")]
    public KamiPose[] poses =
    {
        new KamiPose { id = "Surprised", intro = "Sorprendida", main = "Sorprendida Loop", loop = true },
        new KamiPose { id = "Celebrate", intro = "Reward", main = "RewardLoop", loop = true, holdSeconds = 3f },
    };
}
