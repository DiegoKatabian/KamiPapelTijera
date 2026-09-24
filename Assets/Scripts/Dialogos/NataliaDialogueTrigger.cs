using UnityEngine;

/// <summary>
/// Natalia's dialogue trigger for Level 2 page 1 (spec 006, Story 1 / FR-001, FR-003).
///
/// Inherits TriggerDialogue, NOT QuestDialogueTrigger: that subclass hardcodes the
/// request -> reminder -> thanks -> chat mold (4 dialogues, fixed indices, a reward
/// fanfare and an AddResource of the quest's resource on dialogue 2). Natalia's arc runs
/// across 5 pages with 3 chained quests and no item handover, so only the plain
/// TriggerDialogue behaviour is reusable.
///
/// Dialogue 0 is her opening. When it ENDS (not when it starts) the "Find clues" quest is
/// registered and she starts following Kami. Every dialogue from index 1 on is
/// while-you-travel chatter; later phases append to the array without touching this script.
/// </summary>
public class NataliaDialogueTrigger : TriggerDialogue
{
    [SerializeField, Tooltip("Natalia's FSM. She starts following Kami when the opening dialogue ends.")]
    NPC_Natalia _natalia;

    [SerializeField, Tooltip("Quest registered when the opening dialogue ends (Quest05_FindClues).")]
    QuestSO _quest;

    bool _openingDialogueDone;

    protected override void Start()
    {
        base.Start();
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);

        //Tiburcio's quest shipped broken precisely because nothing ever called AddQuest, so the
        //event that was supposed to complete it had nothing to complete. Shout at setup time
        //instead of failing silently three pages later.
        if (_quest == null)
        {
            Debug.LogError($"[NataliaDialogueTrigger] {gameObject.name}: _quest is not assigned in the Inspector. Her opening dialogue will play but the quest will never register.");
        }

        if (_natalia == null)
        {
            Debug.LogWarning($"[NataliaDialogueTrigger] {gameObject.name}: no NPC_Natalia assigned, she will not start following Kami after the opening dialogue.");
        }
    }

    void OnDialogueEnded(params object[] parameters)
    {
        //OnDialogueEnd is global: it fires for EVERY dialogue in the game, so filter to ours first.
        if (_openingDialogueDone)
        {
            return;
        }

        if (parameters == null || parameters.Length < 2)
        {
            return;
        }

        if (_dialogues == null || _dialogues.Length == 0 || _dialogues[0] == null)
        {
            return;
        }

        if ((DialogueSO)parameters[1] != _dialogues[0])
        {
            return;
        }

        _openingDialogueDone = true;
        Debug.Log($"[NataliaDialogueTrigger] {gameObject.name}: opening dialogue finished");

        StartFindCluesQuest();
        StartFollowingKami();
        PasarAlSiguienteDialogo();
    }

    void StartFindCluesQuest()
    {
        if (_quest == null)
        {
            Debug.LogError($"[NataliaDialogueTrigger] {gameObject.name}: _quest is not assigned, cannot register the quest.");
            return;
        }

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning($"[NataliaDialogueTrigger] {gameObject.name}: there is no QuestManager in the scene, cannot register {_quest.name}.");
            return;
        }

        QuestManager.Instance.AddQuest(_quest);
        Debug.Log($"[NataliaDialogueTrigger] {gameObject.name}: added quest {_quest.name}");
    }

    void StartFollowingKami()
    {
        if (_natalia == null)
        {
            Debug.LogWarning($"[NataliaDialogueTrigger] {gameObject.name}: no NPC_Natalia assigned, she will not follow Kami.");
            return;
        }

        _natalia.StartFollowingPlayer();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        }
    }
}
