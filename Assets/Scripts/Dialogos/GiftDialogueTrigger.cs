using System.Collections;
using UnityEngine;

/// <summary>
/// The giant gift box at Natalia's door, Level 2 page 3 (spec 006). Talked to like an NPC, and
/// opened by cutting its ribbon.
///
/// Talking: dialogue 0 is Natalia's "what's this? let's cut it open!", every later talk is the
/// reminder that it is still wrapped. It does NOT touch any quest: Quest06 closes when the arrest
/// that follows is over (ArrestCutscene), per Diego's 2026-09-25 design pass.
///
/// Opening: the ribbon can be cut at any time, talked to or not. Cutting it swaps the closed art
/// for the open one, reveals what's inside (the Pelusa pickup, the letter pedestal), stops the box
/// from being talkable, and queues Natalia's reaction. The letter pedestal auto-prompts its own
/// origami once that reaction is over (TriggerOrigami._promptAutomatically).
///
/// Inherits TriggerDialogue, not QuestDialogueTrigger: there is no item handover and no reward.
/// </summary>
public class GiftDialogueTrigger : TriggerDialogue
{
    [Header("Opening")]
    [SerializeField, Tooltip("The ribbon tied around the box. Cutting it opens the box.")]
    CuttableTwoPieces _ribbon;

    [SerializeField, Tooltip("Turned on when the box opens: the open-box art, the Pelusa pickup, the letter pedestal.")]
    GameObject[] _showOnOpen;

    [SerializeField, Tooltip("Turned off when the box opens: the closed-box art.")]
    GameObject[] _hideOnOpen;

    [SerializeField, Tooltip("What Natalia says when the box opens. Optional.")]
    DialogueSO _openedComment;

    bool _opened;

    protected override void Start()
    {
        base.Start();
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);

        if (_ribbon == null)
        {
            Debug.LogWarning($"[GiftDialogueTrigger] {gameObject.name}: _ribbon is not assigned, the box can never be opened.");
            return;
        }

        //from code and not a serialized UnityEvent: the ribbon is a nested prefab instance, and
        //this keeps the whole box working the moment it is dragged into a scene
        _ribbon.OnCut.AddListener(Open);
    }

    //after the first talk, the box only repeats the "still wrapped" reminder
    void OnDialogueEnded(params object[] parameters)
    {
        if (currentDialogue != 0 || parameters == null || parameters.Length < 2 || _dialogues == null || _dialogues.Length == 0)
        {
            return;
        }

        //OnDialogueEnd is global: only react to our own first dialogue
        if ((DialogueSO)parameters[1] != _dialogues[0])
        {
            return;
        }

        PasarAlSiguienteDialogo();
    }

    public void Open()
    {
        if (_opened)
        {
            return;
        }

        _opened = true;
        Debug.Log($"[GiftDialogueTrigger] {gameObject.name}: the ribbon was cut, the box opens");

        SetTalkable(false);
        SetAllActive(_hideOnOpen, false);

        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(AudioId.GiftBoxOpen);
        }

        //the reaction goes first so it is already on screen when the letter pedestal appears and
        //starts waiting for the screen to be free
        if (_openedComment != null)
        {
            StartCoroutine(ShowWhenFree(_openedComment));
        }

        SetAllActive(_showOnOpen, true);
    }

    //a talk that was already open when the ribbon got cut must not swallow Natalia's reaction:
    //ShowDialogue silently drops requests while another dialogue is up
    IEnumerator ShowWhenFree(DialogueSO dialogue)
    {
        while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
        {
            yield return null;
        }

        DialogueManager.Instance.ShowDialogue(dialogue);
    }

    //disabling a collider sends no OnTriggerExit, so leave the trigger by hand (same as
    //NataliaDialogueTrigger.SetTalkable): that clears triggerBool and the InteractionContext entry
    void SetTalkable(bool talkable)
    {
        if (!talkable)
        {
            OnExitBehaviour();
        }

        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.enabled = talkable;
        }
    }

    void SetAllActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject target in objects)
        {
            if (target == null)
            {
                Debug.LogWarning($"[GiftDialogueTrigger] {gameObject.name}: an empty slot in the open/close lists, skipping it.");
                continue;
            }

            target.SetActive(active);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_ribbon != null)
        {
            _ribbon.OnCut.RemoveListener(Open);
        }

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        }
    }
}
