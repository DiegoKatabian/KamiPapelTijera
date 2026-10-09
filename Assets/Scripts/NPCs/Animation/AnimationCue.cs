using UnityEngine;

/// <summary>What an AnimationCue asks a SpineCharacter to do (spec 014, D2).</summary>
public enum AnimationCueAction
{
    PlayPose,       //play the pose named in Value (intro -> main), on top of whatever she was doing
    EndPose,        //stop the pose that is playing, back to idle/walk/rest pose
    SetRestPose,    //the pose named in Value becomes what she does while standing still (a mood)
    ClearRestPose,  //standing still is plain idle again
    SetIdleOverlay, //Value = an animation layered on idle (track 1), e.g. the cops' Lantern. Empty = none
    SetWalkOverlay  //same, layered on walk/run
}

/// <summary>
/// One animation beat, addressed by actor id instead of a scene reference, so a DialogueSO (an asset,
/// which cannot point at scene objects) can say "Ariel points" on the exact line he says it.
/// Every active SpineCharacter whose actor id matches gets it.
/// </summary>
[System.Serializable]
public struct AnimationCue
{
    [Tooltip("Who: the Actor Id on the character's SpineCharacter (Natalia, Ariel, Police, Guard). Every active character with that id gets the cue.")]
    public string actor;

    [Tooltip("What to do.")]
    public AnimationCueAction action;

    [Tooltip("Pose id (PlayPose / SetRestPose, as named in the character's Poses list) or animation name (Set*Overlay). Ignored by EndPose / ClearRestPose.")]
    public string value;

    public override string ToString()
    {
        return $"{actor}: {action}{(string.IsNullOrEmpty(value) ? "" : $" '{value}'")}";
    }

    /// <summary>Runs a list of cues; null or empty does nothing. <paramref name="source"/> only labels the logs.</summary>
    public static void RunAll(AnimationCue[] cues, string source)
    {
        if (cues == null)
        {
            return;
        }

        foreach (AnimationCue cue in cues)
        {
            if (string.IsNullOrEmpty(cue.actor))
            {
                Debug.LogWarning($"[AnimationCue] {source}: a cue with no actor, skipped");
                continue;
            }

            if (SpineCharacter.ApplyCue(cue) == 0)
            {
                //not an error by itself (a character on another page is inactive), but worth seeing when a beat doesn't show
                Debug.LogWarning($"[AnimationCue] {source}: '{cue}' reached nobody (no active SpineCharacter with actor id '{cue.actor}')");
            }
        }
    }
}
