using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2 page 2 (spec 006, Story 2): watches the inventory for the three clues and drives
/// everything that depends on HOW MANY have been found. Nothing here cares about the ORDER.
///
/// - Every new clue queues Natalia's comment about it.
/// - At _cluesToCallOffCops clues, queues the cops' exit right after that clue's comment: once the
///   comment is read it fires Evento.OnCrimeSceneUnguarded (CrimeSceneGate plays the cops leaving as
///   a cutscene, #231), and her "they left" line waits until that cutscene is over.
/// - At all clues, fires Evento.OnAllCluesFound EXACTLY ONCE (latched), which completes
///   Quest05_FindClues through QuestManager.
/// - When that quest completes, queues her "let's head home" line, closes Quest05 and starts
///   Quest06_GoBackToNataliasHouse.
/// - Right after that line, queues the page-end beat (NataliaPageEndBeat, V5): Natalia appears at the
///   far end of the page waving at Kami, in a short camera shot.
///
/// Why the dialogue queue: DialogueManager.ShowDialogue silently drops a request while another
/// dialogue is open, and the third clue alone produces two lines back to back (the clue comment
/// and the "head home" line). Natalia's own chatter trigger can also be open when a clue lands.
/// The queue also holds the cops' exit as a step, so the order is always comment -> cutscene ->
/// "they left", and it waits while any cutscene holds Kami.
/// </summary>
public class FindCluesTracker : MonoBehaviour
{
    //a dialogue to show, or (callOffCops) the moment the cops leave, or (playPageEndBeat) Natalia's wave
    struct Step
    {
        public DialogueSO dialogue;
        public bool callOffCops;
        public bool playPageEndBeat;
    }

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

    [SerializeField, Tooltip("Plays right after Natalia's 'every clue is in' line: she appears at the end of the page and waves, with a camera shot. Optional.")]
    NataliaPageEndBeat _allCluesBeat;

    [SerializeField, Tooltip("Quest05_FindClues: closed once every clue is found.")]
    QuestSO _findCluesQuest;

    [SerializeField, Tooltip("Quest06_GoBackToNataliasHouse: started once every clue is found.")]
    QuestSO _nextQuest;

    [Header("Testing")]
    [SerializeField, Tooltip("Natalia (the one from page 1). Only used by a test started on this page, to bring her along.")]
    NPC_Natalia _natalia;

    [SerializeField, Tooltip("Starting the scene on this page (PageScrollerManager.startingPage) brings Natalia along and gives Kami the find clues quest, as if page 1 was played.")]
    int _pageNumber = 2;

    readonly HashSet<ResourceType> _found = new HashSet<ResourceType>();
    readonly Queue<Step> _pending = new Queue<Step>();
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

        if (PageScrollerManager.Instance != null && PageScrollerManager.Instance.startingPage == _pageNumber)
        {
            StartCoroutine(BeginForTest());
        }
    }

    //a test started on this page skips page 1: Natalia is already with Kami, on the find clues quest (#170)
    IEnumerator BeginForTest()
    {
        //two frames, like the other page owners' test starts: the Player and the managers set up first
        yield return null;
        yield return null;

        Level2TestStartItems.GrantItemsBefore(_pageNumber); //the café ticket (#187)

        if (_natalia == null)
        {
            Debug.LogWarning("[FindCluesTracker] _natalia is not assigned: she is not brought along for this test start");
            yield break;
        }

        _natalia.SetUpForTestStart(_pageNumber, followKami: true);
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
            PlayClueSounds();
            QueueComment(clue.comment);
        }

        if (!_copsCalledOff && _found.Count >= _cluesToCallOffCops)
        {
            _copsCalledOff = true;
            Debug.Log("[FindCluesTracker] enough clues found, the cops leave after this clue's comment");
            _pending.Enqueue(new Step { callOffCops = true });
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
        if (_allCluesBeat != null)
        {
            //after the line in the queue, which only moves on once that dialogue is closed
            _pending.Enqueue(new Step { playPageEndBeat = true });
        }
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

    //every clue gets the short "found it" sting (the hat is stuck on a car's hood now, so
    //the manhole clank it used to get is gone)
    void PlayClueSounds()
    {
        if (AudioManager.instance == null)
        {
            Debug.LogWarning("[FindCluesTracker] no AudioManager, clue sounds skipped.");
            return;
        }

        //the same sound as opening a chest in Level 1 (CofreCortable)
        AudioManager.instance.Play(AudioId.MagicSuccess, 1.4f);
    }

    void QueueComment(DialogueSO comment)
    {
        if (comment != null)
        {
            _pending.Enqueue(new Step { dialogue = comment });
        }
    }

    void Update()
    {
        if (_pending.Count == 0 || DialogueManager.Instance == null || LevelManager.Instance == null)
        {
            return;
        }

        //inCutscene too: the cops' exit holds Kami, and "they left" must wait until she is free
        if (DialogueManager.Instance.isShowing || LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene)
        {
            return;
        }

        if (OverlayManager.Instance != null && OverlayManager.Instance.isLocked)
        {
            return;
        }

        Step step = _pending.Dequeue();
        if (step.callOffCops)
        {
            //CrimeSceneGate takes the cutscene lock inside this call, so the next step waits for it
            Debug.Log("[FindCluesTracker] the clue comment is read: the cops leave");
            EventManager.Trigger(Evento.OnCrimeSceneUnguarded);
            return;
        }

        if (step.playPageEndBeat)
        {
            //the beat takes the cutscene lock inside this call, so nothing queued behind it runs until it is over
            Debug.Log("[FindCluesTracker] the last clue's line is read: Natalia waves from the end of the page");
            _allCluesBeat.Play();
            return;
        }

        DialogueManager.Instance.ShowDialogue(step.dialogue);
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
