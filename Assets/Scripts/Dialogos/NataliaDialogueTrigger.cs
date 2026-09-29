using System.Collections;
using UnityEngine;

/// <summary>
/// Natalia's dialogue trigger (spec 006, Story 1 / FR-001, FR-003).
///
/// Inherits TriggerDialogue, NOT QuestDialogueTrigger: that subclass hardcodes the
/// request -> reminder -> thanks -> chat mold (4 dialogues, fixed indices, a reward
/// fanfare and an AddResource of the quest's resource on dialogue 2). Natalia's arc runs
/// across 5 pages with 3 chained quests and no item handover, so only the plain
/// TriggerDialogue behaviour is reusable.
///
/// Page 1 (Diego, 2026-09-28): dialogue 0 is her opening; when it ENDS the café pedestal appears
/// (_activateAfterOpening) and dialogue 1 becomes her "fold the ticket first" reminder. Folding the
/// café ticket is what makes her join: _afterTicketDialogue plays (she promises to help Kami get
/// back to her own book), and when it ends the "Find clues" quest registers and she starts
/// following. Tying the follow to the fold guarantees Kami reaches page 5 with the ticket.
///
/// Page 5: StayBehind() makes her talkable again, with _farewellDialogue from then on.
/// </summary>
public class NataliaDialogueTrigger : TriggerDialogue
{
    [SerializeField, Tooltip("Natalia's FSM. She starts following Kami once the café ticket is folded.")]
    NPC_Natalia _natalia;

    [SerializeField, Tooltip("Quest registered when she joins Kami (Quest05_FindClues).")]
    QuestSO _quest;

    [SerializeField, Tooltip("Turned on when the opening dialogue ends: the cafe wrapper's pedestal, which then auto prompts its fold.")]
    GameObject[] _activateAfterOpening;

    [SerializeField, Tooltip("What she says once the café ticket is folded. When it ends she joins Kami (quest + follow).")]
    DialogueSO _afterTicketDialogue;

    [SerializeField, Tooltip("What she says when talked to on page 5, after she stayed behind.")]
    DialogueSO _farewellDialogue;

    bool _openingDialogueDone;
    bool _joined;
    bool _stayedBehind;

    protected override void Start()
    {
        base.Start();
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Subscribe(Evento.OnCafeWrapperFolded, OnTicketFolded);

        //Tiburcio's quest shipped broken precisely because nothing ever called AddQuest, so the
        //event that was supposed to complete it had nothing to complete. Shout at setup time
        //instead of failing silently three pages later.
        if (_quest == null)
        {
            Debug.LogError($"[NataliaDialogueTrigger] {gameObject.name}: _quest is not assigned in the Inspector. Her opening dialogue will play but the quest will never register.");
        }

        if (_natalia == null)
        {
            Debug.LogWarning($"[NataliaDialogueTrigger] {gameObject.name}: no NPC_Natalia assigned, she will not start following Kami after the café ticket.");
        }
    }

    public override void Interact(params object[] parameter)
    {
        if (_stayedBehind && _farewellDialogue != null)
        {
            if (triggerBool)
            {
                DialogueManager.Instance.ShowDialogue(_farewellDialogue);
            }
            return;
        }

        base.Interact(parameter);
    }

    void OnDialogueEnded(params object[] parameters)
    {
        //OnDialogueEnd is global: it fires for EVERY dialogue in the game, so filter to ours first.
        if (parameters == null || parameters.Length < 2)
        {
            return;
        }

        DialogueSO ended = (DialogueSO)parameters[1];

        if (!_openingDialogueDone && _dialogues != null && _dialogues.Length > 0 && ended == _dialogues[0])
        {
            _openingDialogueDone = true;
            Debug.Log($"[NataliaDialogueTrigger] {gameObject.name}: opening dialogue finished, waiting for the café ticket");
            PasarAlSiguienteDialogo();
            ActivateAfterOpening();
            return;
        }

        if (!_joined && _afterTicketDialogue != null && ended == _afterTicketDialogue)
        {
            JoinKami();
        }
    }

    void OnTicketFolded(params object[] parameters)
    {
        if (_joined)
        {
            return;
        }

        Debug.Log($"[NataliaDialogueTrigger] {gameObject.name}: the café ticket is folded");

        if (_afterTicketDialogue == null)
        {
            JoinKami();
            return;
        }

        StartCoroutine(ShowWhenFree(_afterTicketDialogue));
    }

    //the fold's own end and its reward sticker can still hold the screen: ShowDialogue silently
    //drops requests while it is busy
    IEnumerator ShowWhenFree(DialogueSO dialogue)
    {
        while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
        {
            yield return null;
        }

        DialogueManager.Instance.ShowDialogue(dialogue);
    }

    void JoinKami()
    {
        _joined = true;
        StartFindCluesQuest();
        StartFollowingKami();
        PasarAlSiguienteDialogo();
    }

    /// <summary>
    /// Page 5: she stops travelling with Kami. Talkable again, with her farewell from now on. The
    /// caller stops her follow (NPC.StopFollowingPlayer); this only handles the talking side.
    /// </summary>
    public void StayBehind()
    {
        _stayedBehind = true;
        SetTalkable(true);
    }

    void ActivateAfterOpening()
    {
        if (_activateAfterOpening == null)
        {
            return;
        }

        foreach (GameObject target in _activateAfterOpening)
        {
            if (target == null)
            {
                Debug.LogWarning($"[NataliaDialogueTrigger] {gameObject.name}: an empty slot in _activateAfterOpening, skipping it.");
                continue;
            }

            target.SetActive(true);
        }
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
        SetTalkable(false);
    }

    /// <summary>
    /// While she follows Kami, this trigger travels with her and Kami is always inside it, so
    /// EVERY interact press opened her chatter and beat whatever Kami was actually aiming at (the
    /// gift box, a trash can). She is not talkable while following; whoever makes her stop
    /// following later (page 5) calls SetTalkable(true).
    /// </summary>
    public void SetTalkable(bool talkable)
    {
        Collider trigger = GetComponent<Collider>();

        if (!talkable)
        {
            //disabling a collider sends no OnTriggerExit, so leave the trigger by hand: that clears
            //triggerBool (and with it the InteractionContext registration) and hides our post-it
            OnExitBehaviour();
        }

        if (trigger != null)
        {
            trigger.enabled = talkable;
        }

        Debug.Log($"[NataliaDialogueTrigger] {gameObject.name}: talkable = {talkable}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
            EventManager.Unsubscribe(Evento.OnCafeWrapperFolded, OnTicketFolded);
        }
    }
}
