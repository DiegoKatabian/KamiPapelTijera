using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public struct DialogueEvent
{
    [TextAreaAttribute] public string text;
    public Sprite sprite;
    public string speakerName;
    [SoundId, Tooltip("Optional AudioId played once when this line starts being written (e.g. Handcuffs, RadioStatic). Empty = no sound.")]
    public string soundOnLine;
    [Tooltip("Optional animation beats played when this line starts being written, e.g. Ariel: PlayPose Point (spec 014). Addressed by actor id, so they reach scene characters from this asset.")]
    public AnimationCue[] animationCues;
}

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Dialogue", order = 1)]
public class DialogueSO : ScriptableObject
{
    public bool wasRead = false;
    public DialogueEvent[] events;
    public int currentText = 0;
    [Tooltip("Optional animation beats played when this dialogue closes, e.g. Natalia: EndPose (back to idle after thinking).")]
    public AnimationCue[] cuesOnEnd;
}
