using UnityEngine;

/// <summary>
/// Marks a string field as holding an AudioBank id, so the inspector shows a dropdown of real
/// ids instead of a free-text box (see SoundIdDrawer). Used by DialogueEvent.sighSound.
/// </summary>
public class SoundIdAttribute : PropertyAttribute { }
