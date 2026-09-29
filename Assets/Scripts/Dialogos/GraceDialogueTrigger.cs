using System;
using UnityEngine;

/// <summary>
/// Grace, the museum director (Level 2 page 5, spec 006 Story 5). Talked to like any NPC, but what
/// she says depends on where page 5's story is, and that lives in MuseumPage: the page sets
/// ChooseDialogue and this trigger just asks it on every talk.
///
/// With nothing set (the prefab dropped into another scene) she falls back to the plain
/// TriggerDialogue behaviour over _dialogues.
/// </summary>
public class GraceDialogueTrigger : TriggerDialogue
{
    /// <summary>Returns what Grace says on this talk. Null = fall back to _dialogues.</summary>
    public Func<DialogueSO> ChooseDialogue;

    public override void Interact(params object[] parameter)
    {
        if (!triggerBool)
        {
            return;
        }

        DialogueSO chosen = ChooseDialogue?.Invoke();
        if (chosen == null)
        {
            base.Interact(parameter);
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning($"[GraceDialogueTrigger] {gameObject.name}: there is no DialogueManager in the scene");
            return;
        }

        DialogueManager.Instance.ShowDialogue(chosen);
    }
}
