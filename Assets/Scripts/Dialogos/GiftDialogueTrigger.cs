using UnityEngine;

/// <summary>
/// The giant gift box at Natalia's door, Level 2 page 3 (spec 006). Talked to like an NPC.
///
/// Its first dialogue starting fires Evento.OnGiftAtNataliasDoorReached, which completes
/// Quest06_GoBackToNataliasHouse through QuestManager. When that first dialogue ENDS the quest
/// is delivered (removed from the list) and the trigger moves on to its next dialogue, so later
/// talks don't re-complete anything.
///
/// Inherits TriggerDialogue, not QuestDialogueTrigger, for the same reason as
/// NataliaDialogueTrigger: there is no item handover and no reward fanfare. And like
/// FindCluesTracker it does NOT fire OnQuestDelivered, which would pop a reward sticker for a
/// quest that has no reward.
/// </summary>
public class GiftDialogueTrigger : TriggerDialogue
{
    [SerializeField, Tooltip("Quest06_GoBackToNataliasHouse: completed and delivered by the first talk.")]
    QuestSO _quest;

    bool _reached;
    bool _delivered;

    protected override void Start()
    {
        base.Start();
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Subscribe(Evento.OnDialogueWriteText, OnDialogueWriteText);

        if (_quest == null)
        {
            Debug.LogWarning($"[GiftDialogueTrigger] {gameObject.name}: _quest is not assigned, talking to the gift will not deliver anything.");
        }
    }

    //keyed off OUR dialogue starting to write, not off Interact: the same press can open
    //Natalia's chatter instead (DialogueManager then refuses ours), and that must not count
    void OnDialogueWriteText(params object[] parameters)
    {
        if (_reached || parameters == null || parameters.Length == 0 || _dialogues == null || _dialogues.Length == 0)
        {
            return;
        }

        if ((DialogueSO)parameters[0] != _dialogues[0])
        {
            return;
        }

        _reached = true;
        Debug.Log($"[GiftDialogueTrigger] {gameObject.name}: Kami reached the gift at Natalia's door");
        EventManager.Trigger(Evento.OnGiftAtNataliasDoorReached);
    }

    void OnDialogueEnded(params object[] parameters)
    {
        if (_delivered || !_reached)
        {
            return;
        }

        if (parameters == null || parameters.Length < 2 || _dialogues == null || _dialogues.Length == 0)
        {
            return;
        }

        //OnDialogueEnd is global: only react to our own first dialogue
        if ((DialogueSO)parameters[1] != _dialogues[0])
        {
            return;
        }

        _delivered = true;
        PasarAlSiguienteDialogo();

        if (_quest == null || QuestManager.Instance == null)
        {
            Debug.LogWarning($"[GiftDialogueTrigger] {gameObject.name}: no quest or no QuestManager, nothing to deliver.");
            return;
        }

        QuestManager.Instance.RemoveQuest(_quest);
        AudioManager.instance.Play(AudioId.QuestCompleted02);
        Debug.Log($"[GiftDialogueTrigger] {gameObject.name}: delivered {_quest.name}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
            EventManager.Unsubscribe(Evento.OnDialogueWriteText, OnDialogueWriteText);
        }
    }
}
