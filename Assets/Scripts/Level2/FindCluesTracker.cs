using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2 page 2 (spec 006, Story 2): watches the inventory for the three clues and drives
/// everything that depends on HOW MANY have been found. Nothing here cares about the ORDER.
///
/// - Every new clue queues Natalia's comment about it.
/// - At _cluesToCallOffCops clues, fires Evento.OnCrimeSceneUnguarded (the cops at the
///   museum's broken fence drive off, CrimeSceneGate listens) and queues her "they left" line.
/// - At all clues, fires Evento.OnAllCluesFound EXACTLY ONCE (latched), which completes
///   Quest05_FindClues through QuestManager.
/// - When that quest completes, queues her "let's head home" line, closes Quest05 and starts
///   Quest06_GoBackToNataliasHouse.
///
/// Why the dialogue queue: DialogueManager.ShowDialogue silently drops a request while another
/// dialogue is open, and the third clue alone produces two lines back to back (the clue comment
/// and the "head home" line). Natalia's own chatter trigger can also be open when a clue lands.
/// </summary>
public class FindCluesTracker : MonoBehaviour
{
    [System.Serializable]
    struct Clue
    {
        [Tooltip("The inventory resource that counts as this clue.")]
        public ResourceType resource;

        [Tooltip("What Natalia says when Kami gets it. Optional.")]
        public DialogueSO comment;
    }

    [SerializeField, Tooltip("The clues, in any order. Finding all of them completes the quest.")]
    Clue[] _clues;

    [SerializeField, Tooltip("How many clues it takes for the cops guarding the museum to drive off.")]
    int _cluesToCallOffCops = 2;

    [SerializeField, Tooltip("Natalia's line when the cops leave. Optional.")]
    DialogueSO _copsLeftComment;

    [SerializeField, Tooltip("Natalia's line once every clue is in. Optional.")]
    DialogueSO _allCluesComment;

    [SerializeField, Tooltip("Quest05_FindClues: closed once every clue is found.")]
    QuestSO _findCluesQuest;

    [SerializeField, Tooltip("Quest06_GoBackToNataliasHouse: started once every clue is found.")]
    QuestSO _nextQuest;

    readonly HashSet<ResourceType> _found = new HashSet<ResourceType>();
    readonly Queue<DialogueSO> _pendingComments = new Queue<DialogueSO>();
    bool _copsCalledOff;
    bool _allCluesFired;
    bool _findCluesClosed;

    void Start()
    {
        if (_clues == null || _clues.Length == 0)
        {
            Debug.LogWarning($"[FindCluesTracker] {gameObject.name}: no clues configured, nothing will ever complete.");
        }

        if (_findCluesQuest == null || _nextQuest == null)
        {
            Debug.LogWarning($"[FindCluesTracker] {gameObject.name}: _findCluesQuest or _nextQuest is not assigned, the quest handoff will not happen.");
        }

        EventManager.Subscribe(Evento.OnResourceUpdated, OnResourceUpdated);
        EventManager.Subscribe(Evento.OnQuestCompleted, OnQuestCompleted);
    }

    void OnResourceUpdated(params object[] parameters)
    {
        if (_clues == null || LevelManager.Instance == null)
        {
            return;
        }

        foreach (Clue clue in _clues)
        {
            if (_found.Contains(clue.resource))
            {
                continue;
            }

            if (LevelManager.Instance.recursosRecolectados[clue.resource] <= 0)
            {
                continue;
            }

            _found.Add(clue.resource);
            Debug.Log($"[FindCluesTracker] clue found: {clue.resource} ({_found.Count}/{_clues.Length})");
            QueueComment(clue.comment);
        }

        if (!_copsCalledOff && _found.Count >= _cluesToCallOffCops)
        {
            _copsCalledOff = true;
            Debug.Log("[FindCluesTracker] enough clues found, the cops drive off");
            EventManager.Trigger(Evento.OnCrimeSceneUnguarded);
            QueueComment(_copsLeftComment);
        }

        if (!_allCluesFired && _found.Count >= _clues.Length)
        {
            _allCluesFired = true;
            Debug.Log("[FindCluesTracker] all clues found, firing OnAllCluesFound (once)");
            EventManager.Trigger(Evento.OnAllCluesFound);
        }
    }

    void OnQuestCompleted(params object[] parameters)
    {
        if (_findCluesClosed || parameters == null || parameters.Length == 0)
        {
            return;
        }

        //QuestManager re-announces a completed quest on every later CheckQuests until it is
        //removed, hence the latch
        if ((QuestSO)parameters[0] != _findCluesQuest)
        {
            return;
        }

        _findCluesClosed = true;
        QueueComment(_allCluesComment);
        StartCoroutine(HandOffQuestNextFrame());
    }

    //OnQuestCompleted is raised from INSIDE QuestManager.CheckQuests' foreach over its quest
    //list, so removing or adding a quest right here would throw "collection was modified".
    //Deliberately NOT firing OnQuestDelivered: ResourceParticleManager answers it by showing the
    //quest's reward sticker, and these quests have no reward (it would show a mushroom).
    IEnumerator HandOffQuestNextFrame()
    {
        yield return null;

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[FindCluesTracker] there is no QuestManager in the scene, cannot hand off the quest.");
            yield break;
        }

        if (_findCluesQuest != null)
        {
            QuestManager.Instance.RemoveQuest(_findCluesQuest);
        }

        AudioManager.instance.Play(AudioId.QuestCompleted02);

        if (_nextQuest != null)
        {
            QuestManager.Instance.AddQuest(_nextQuest);
            Debug.Log($"[FindCluesTracker] started {_nextQuest.name}");
        }
    }

    void QueueComment(DialogueSO comment)
    {
        if (comment != null)
        {
            _pendingComments.Enqueue(comment);
        }
    }

    void Update()
    {
        if (_pendingComments.Count == 0 || DialogueManager.Instance == null || LevelManager.Instance == null)
        {
            return;
        }

        if (DialogueManager.Instance.isShowing || LevelManager.Instance.inDialogue)
        {
            return;
        }

        if (OverlayManager.Instance != null && OverlayManager.Instance.isLocked)
        {
            return;
        }

        DialogueManager.Instance.ShowDialogue(_pendingComments.Dequeue());
    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnResourceUpdated, OnResourceUpdated);
            EventManager.Unsubscribe(Evento.OnQuestCompleted, OnQuestCompleted);
        }
    }
}
