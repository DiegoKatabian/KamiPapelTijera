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
}

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Dialogue", order = 1)]
public class DialogueSO : ScriptableObject
{
    public bool wasRead = false;
    public DialogueEvent[] events;
    public int currentText = 0;
}
