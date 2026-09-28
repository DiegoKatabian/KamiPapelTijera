using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// A dialogue line dropped on a cutscene's timeline. When the playhead reaches it, the timeline
/// holds still (cameras and all) until the player closes that dialogue, then carries on — so
/// timing a cutscene is only about the gaps between beats, never about how fast someone reads.
///
/// Drop it in the Markers area of the timeline (above the tracks) and pick the DialogueSO in the
/// Inspector. The PlayableDirector's GameObject needs a CutsceneDirector to act on it.
/// </summary>
[DisplayName("Kami/Dialogue")]
public class CutsceneDialogueMarker : Marker, INotification
{
    [SerializeField, Tooltip("The dialogue shown when the playhead reaches this marker. The timeline waits until it is closed.")]
    DialogueSO _dialogue;

    public DialogueSO Dialogue => _dialogue;

    public PropertyName id => new PropertyName("CutsceneDialogue");
}
