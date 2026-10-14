using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Level 2 page 5, "Clearing their name" (spec 006 Phase 5): owns the page's story.
///
/// 1. Ariel and two cops are already with Grace (the museum director), taking her statement, their
///    patrol car parked nearby (#182). Walking up to Grace (_approachRadius) or talking to her, with
///    the evidence (the Pelusa, the café ticket and the three page 2 clues), plays the ARRIVAL
///    CUTSCENE (Timeline_MuseumArrival: Grace reacts to the Pelusa, the camera pulls back and returns)
///    and then she hears the girls out (_graceMeeting). Without the evidence the approach does nothing
///    and talking gets her asking for proof, which a normal playthrough never reaches.
/// 2. When her meeting dialogue ends the café ticket's pedestal appears and auto-prompts the unfold.
/// 3. Once the ticket's text is read and closed, _evidenceDialogue plays: Natalia's receipt is her
///    alibi, the hat and glove are Ariel's, the watch dates the robbery, the police apologize, and
///    Grace offers the catapult to send Kami and the Abuela home (one talk, Diego 2026-09-28).
/// 4. When it ends the cops march Ariel to the patrol car and drive off (MuseumArrest, #183).
/// 5. Then everything is handed over (it leaves the inventory), OnPaintingReturned completes Quest08
///    (and ends the story music), Natalia walks to her spot away from Grace and becomes talkable
///    (farewell, #184), the Abuela stops following and the catapult (Catapult), which ends the level,
///    unlocks.
///
/// Starting the scene on this page (PageScrollerManager.startingPage) sets it up by itself: the
/// items, Quest08 and both followers, as if Kami had just arrived from page 4.
/// </summary>
public class MuseumPage : MonoBehaviour
{
    enum Phase
    {
        Arrived,
        Meeting,
        WaitingForTicket,
        Evidence,
        Arrest,
        CatapultReady
    }

    [Header("Grace")]
    [SerializeField, Tooltip("Grace's dialogue trigger. What she says is decided here.")]
    GraceDialogueTrigger _grace;

    [SerializeField, Tooltip("The lines of the meeting: the last part of the arrival cutscene (a dialogue marker on the timeline points at this same asset). When it ends the ticket pedestal appears.")]
    DialogueSO _graceMeeting;

    [SerializeField, Tooltip("First talk WITHOUT the evidence (should not happen in a normal playthrough).")]
    DialogueSO _graceNoProof;

    [SerializeField, Tooltip("Talking to her again before the ticket is unfolded.")]
    DialogueSO _graceTicketReminder;

    [SerializeField, Tooltip("Plays by itself once the ticket's text is closed: the alibi, Ariel's hat and glove, the watch, the apology, the catapult offer. When it ends the quest closes and the catapult unlocks.")]
    DialogueSO _evidenceDialogue;

    [SerializeField, Tooltip("Talking to her once the catapult is unlocked.")]
    DialogueSO _graceCatapultReminder;

    [SerializeField, Tooltip("Walking within this distance of Grace (horizontal, world units) with the evidence starts the meeting by itself, once. 0 = only by talking to her.")]
    float _approachRadius = 12f;

    [Header("Arrival cutscene")]
    [SerializeField, Tooltip("Timeline_MuseumArrival. Played when the meeting starts. Empty = no cutscene: Grace plays the meeting directly.")]
    CutsceneDirector _arrivalCutscene;

    [SerializeField, Tooltip("Shots that frame Kami: their Follow is set to her when the cutscene starts (a scene object cannot be assigned her prefab instance in the timeline).")]
    CinemachineVirtualCamera[] _camerasFollowingKami;

    [Header("Ariel's arrest")]
    [SerializeField, Tooltip("Timeline_MuseumArrest: after the evidence dialogue the cops take Ariel to the patrol car. Empty = the case closes straight away and Ariel stays where he is.")]
    MuseumArrest _arrest;

    [Header("Evidence")]
    [SerializeField, Tooltip("Kami needs at least one of each to be heard out.")]
    ResourceType[] _requiredEvidence =
    {
        ResourceType.pelusaPainting,
        ResourceType.cafeTicket,
        ResourceType.caughtBelonging,
        ResourceType.lostGlove,
        ResourceType.brokenWatch
    };

    [SerializeField, Tooltip("Leaves the inventory when the case is closed (handed to Grace and the police).")]
    ResourceType[] _handedOver =
    {
        ResourceType.pelusaPainting,
        ResourceType.cafeTicket,
        ResourceType.caughtBelonging,
        ResourceType.lostGlove,
        ResourceType.brokenWatch
    };

    [Header("Café ticket")]
    [SerializeField, Tooltip("The pedestal of the ticket unfold. Starts inactive, appears when Grace's meeting ends.")]
    GameObject _ticketPedestal;

    [SerializeField, Tooltip("The panel that shows the ticket's text. The evidence dialogue waits for the player to close it. Optional.")]
    OrigamiTextRevealDisplay _ticketDisplay;

    [Header("Followers")]
    [SerializeField, Tooltip("Natalia (the one from page 1: there is only one in the level).")]
    NPC _natalia;

    [SerializeField, Tooltip("Where Natalia waits once the case is closed: a few units from Grace, so talking to each of them is a separate spot. Empty = she stays where she is.")]
    Transform _nataliaStayMark;

    [SerializeField, Tooltip("Where the Abuela waits once the case is closed, until Kami boards the catapult. Empty = she stays where she is.")]
    Transform _abuelaStayMark;

    [SerializeField, Tooltip("Safety net: Natalia becomes talkable (and both stop trying to walk) after this many seconds even if they haven't reached their marks (unreachable mark?).")]
    float _nataliaWalkMaxSeconds = 15f;

    [SerializeField, Tooltip("Page 4's Abuela entrance: the Abuela who follows Kami comes from there.")]
    AbuelaEntrance _abuelaEntrance;

    [SerializeField, Tooltip("Sideways spacing of the followers around Kami when the scene starts on this page.")]
    float _followerSpacing = 3f;

    [Header("Ending")]
    [SerializeField, Tooltip("The catapult that ends the level. Unlocked when the evidence dialogue ends.")]
    Catapult _catapult;

    [SerializeField, Tooltip("Quest08_ReturnThePelusa: completed when the case is closed.")]
    QuestSO _museumQuest;

    [Header("Testing")]
    [SerializeField, Tooltip("Starting the scene on this page (PageScrollerManager.startingPage) sets it up by itself.")]
    int _pageNumber = 5;

    Phase _phase = Phase.Arrived;
    bool _questHandedOff;

    NPC Abuela => _abuelaEntrance != null ? _abuelaEntrance.Abuela : null;

    void Start()
    {
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Subscribe(Evento.OnCafeTicketUnfolded, OnTicketUnfolded);
        EventManager.Subscribe(Evento.OnQuestCompleted, OnQuestCompleted);

        if (_grace == null)
        {
            Debug.LogWarning($"[MuseumPage] {gameObject.name}: _grace is not assigned, page 5 can't be played");
        }
        else
        {
            _grace.ChooseDialogue = ChooseGraceDialogue;
        }

        if (_ticketPedestal == null || _evidenceDialogue == null || _catapult == null || _natalia == null)
        {
            Debug.LogWarning($"[MuseumPage] {gameObject.name}: some references are not assigned (ticket pedestal, evidence dialogue, catapult or Natalia), the page sequence will be incomplete");
        }

        if (_ticketPedestal != null)
        {
            _ticketPedestal.SetActive(false);
        }

        if (PageScrollerManager.Instance != null && PageScrollerManager.Instance.startingPage == _pageNumber)
        {
            StartCoroutine(BeginForTest());
        }
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Unsubscribe(Evento.OnCafeTicketUnfolded, OnTicketUnfolded);
        EventManager.Unsubscribe(Evento.OnQuestCompleted, OnQuestCompleted);
    }

    // ---------------------------------------------------------------- Grace

    DialogueSO ChooseGraceDialogue()
    {
        switch (_phase)
        {
            case Phase.Arrived:
                if (!HasEvidence())
                {
                    return _graceNoProof;
                }
                return StartArrival();
            case Phase.Meeting:
                return null; //the cutscene is on: Kami is frozen anyway, nothing to say
            case Phase.WaitingForTicket:
            case Phase.Evidence:
                return _graceTicketReminder;
            case Phase.CatapultReady:
                return _graceCatapultReminder;
            default:
                return null;
        }
    }

    // ---------------------------------------------------------------- the arrival cutscene

    //starts the cutscene and answers "nothing to say here" (null): the timeline plays the reaction and
    //then the _graceMeeting lines itself. Without a cutscene: everyone just shows up and the meeting plays
    DialogueSO StartArrival()
    {
        _phase = Phase.Meeting;

        if (_arrivalCutscene == null)
        {
            Debug.LogWarning("[MuseumPage] no arrival cutscene assigned: Grace plays the meeting directly");
            return _graceMeeting;
        }

        Player player = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        foreach (CinemachineVirtualCamera cam in _camerasFollowingKami)
        {
            if (cam != null && player != null)
            {
                cam.Follow = player.transform;
            }
        }

        Debug.Log("[MuseumPage] Kami brought the evidence: the arrival cutscene starts");
        _arrivalCutscene.Play();
        return null;
    }

    //#182: the meeting starts by itself when the girls walk up to Grace, the same as talking to her
    void Update()
    {
        if (_phase != Phase.Arrived || _approachRadius <= 0f || _grace == null || !ScreenIsFree())
        {
            return;
        }

        Player player = LevelManager.Instance.player;
        Vector3 toGrace = _grace.transform.position - player.transform.position;
        toGrace.y = 0f;
        if (toGrace.sqrMagnitude > _approachRadius * _approachRadius || !HasEvidence(false))
        {
            return;
        }

        Debug.Log("[MuseumPage] Kami walked up to Grace with the evidence: the meeting starts by itself");
        DialogueSO meeting = StartArrival();
        if (meeting != null)
        {
            DialogueManager.Instance.ShowDialogue(meeting);
        }
    }

    //nothing else on screen and Kami free to walk, so the meeting never cuts into a dialogue, a fold or the menu
    static bool ScreenIsFree()
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null || levelManager.player == null || levelManager.inDialogue || levelManager.inCutscene)
        {
            return false;
        }

        if (FlapManager.Instance != null && FlapManager.Instance.IsMenuOpen)
        {
            return false;
        }

        return DialogueManager.Instance != null && DialogueManager.Instance.CanShowDialogueNow;
    }

    // ---------------------------------------------------------------- Grace

    //log = false for the approach check, which asks every frame Kami stands near Grace
    bool HasEvidence(bool log = true)
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null)
        {
            return false;
        }

        foreach (ResourceType item in _requiredEvidence)
        {
            if (!levelManager.recursosRecolectados.TryGetValue(item, out int held) || held <= 0)
            {
                if (log)
                {
                    Debug.Log($"[MuseumPage] Kami has no {item}: Grace asks for proof");
                }
                return false;
            }
        }
        return true;
    }

    void OnDialogueEnded(params object[] parameters)
    {
        if (parameters == null || parameters.Length < 2)
        {
            return;
        }

        DialogueSO ended = (DialogueSO)parameters[1];

        if ((_phase == Phase.Arrived || _phase == Phase.Meeting) && ended == _graceMeeting && _graceMeeting != null)
        {
            _phase = Phase.WaitingForTicket;
            Debug.Log("[MuseumPage] Grace heard the girls out: the ticket pedestal appears");
            if (_ticketPedestal != null)
            {
                _ticketPedestal.SetActive(true);
            }
        }
        else if (_phase == Phase.Evidence && ended == _evidenceDialogue && _evidenceDialogue != null)
        {
            StartArrest();
        }
    }

    // ---------------------------------------------------------------- the ticket and the evidence

    void OnTicketUnfolded(params object[] parameters)
    {
        if (_phase != Phase.WaitingForTicket)
        {
            return;
        }

        _phase = Phase.Evidence;
        StartCoroutine(EvidenceWhenTicketClosed());
    }

    IEnumerator EvidenceWhenTicketClosed()
    {
        //Apply fires the event and opens the panel in the same frame: wait for it to show first
        yield return null;

        while (_ticketDisplay != null && _ticketDisplay.IsReading)
        {
            yield return null;
        }

        while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
        {
            yield return null;
        }

        if (_evidenceDialogue == null)
        {
            StartArrest();
            yield break;
        }

        DialogueManager.Instance.ShowDialogue(_evidenceDialogue);
    }

    //#183: the cops take Ariel away first; the case closes once the patrol car has left
    void StartArrest()
    {
        _phase = Phase.Arrest;

        if (_arrest == null)
        {
            Debug.LogWarning("[MuseumPage] no _arrest assigned: the case closes with Ariel still standing there");
            CloseTheCase();
            return;
        }

        _arrest.Play(CloseTheCase);
    }

    void CloseTheCase()
    {
        _phase = Phase.CatapultReady;
        Debug.Log("[MuseumPage] the case is closed: the Pelusa is back, the girls stay behind, the catapult unlocks");

        HandOverEvidence();

        if (_natalia != null)
        {
            _natalia.StopFollowingPlayer();
            StartCoroutine(NataliaStaysBehind());
        }

        if (Abuela != null)
        {
            Abuela.StopFollowingPlayer();
            StartCoroutine(WalkToMark(Abuela, _abuelaStayMark));
        }

        if (_museumQuest != null && QuestManager.Instance != null && QuestManager.Instance.HasQuest(_museumQuest))
        {
            //QuestManager completes it and raises OnQuestCompleted, which removes it below
            EventManager.Trigger(Evento.OnPaintingReturned);
        }

        if (_catapult != null)
        {
            _catapult.Unlock(Abuela);
        }
    }

    //#184: stopping her where she stood often left her next to Grace, and their two talk triggers
    //overlapped. She walks to her own spot first and only then becomes talkable.
    IEnumerator NataliaStaysBehind()
    {
        yield return WalkToMark(_natalia, _nataliaStayMark);

        NataliaDialogueTrigger talk = _natalia.GetComponentInChildren<NataliaDialogueTrigger>(true);
        if (talk != null)
        {
            talk.StayBehind();
        }
        else
        {
            Debug.LogWarning("[MuseumPage] Natalia has no NataliaDialogueTrigger child, she stays mute");
        }
    }

    //a follower who stopped following walks to her waiting spot on the NavMesh and faces Kami there
    IEnumerator WalkToMark(NPC npc, Transform mark)
    {
        if (mark == null)
        {
            Debug.LogWarning($"[MuseumPage] {npc.name} has no stay mark: she stays where she stopped following");
            yield break;
        }

        Debug.Log($"[MuseumPage] {npc.name} walks to {mark.name}");
        npc.MoveTowards(mark.position, true);
        yield return null; //the path request needs a frame before HasArrived means anything

        float waited = 0f;
        while (!npc.HasArrived() && waited < _nataliaWalkMaxSeconds)
        {
            //Kami boarded the catapult: it switched the Abuela's agent off and seated her by hand
            if (npc.navAgent == null || !npc.navAgent.enabled)
            {
                yield break;
            }

            npc.MoveTowards(mark.position);
            waited += Time.deltaTime;
            yield return null;
        }

        if (!npc.HasArrived())
        {
            Debug.LogWarning($"[MuseumPage] {npc.name} did not reach {mark.name} in {_nataliaWalkMaxSeconds}s (off the NavMesh?), she stays where she is");
        }

        npc.StopAgent();
        if (npc.Character != null && LevelManager.Instance.player != null)
        {
            npc.Character.FaceTowards(LevelManager.Instance.player.transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (_grace != null && _approachRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(_grace.transform.position, _approachRadius);
        }
    }

    void HandOverEvidence()
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null)
        {
            return;
        }

        foreach (ResourceType item in _handedOver)
        {
            if (levelManager.recursosRecolectados.TryGetValue(item, out int held) && held > 0)
            {
                levelManager.AddResource(item, -held);
            }
        }
    }

    // ---------------------------------------------------------------- quest

    void OnQuestCompleted(params object[] parameters)
    {
        if (_questHandedOff || _museumQuest == null || parameters == null || parameters.Length == 0)
        {
            return;
        }

        if ((QuestSO)parameters[0] != _museumQuest)
        {
            return;
        }

        StartCoroutine(RemoveQuestNextFrame());
    }

    //OnQuestCompleted is raised from inside QuestManager.CheckQuests' foreach over its quests, so
    //removing a quest right there throws: wait a frame (same as PoliceStationPage). No
    //OnQuestDelivered: this quest has no reward (it would pop a mushroom sticker).
    IEnumerator RemoveQuestNextFrame()
    {
        _questHandedOff = true;
        yield return null;

        if (QuestManager.Instance != null && QuestManager.Instance.HasQuest(_museumQuest))
        {
            QuestManager.Instance.RemoveQuest(_museumQuest);
            AudioManager.instance.Play(AudioId.QuestCompleted02);
            Debug.Log($"[MuseumPage] {_museumQuest.name} is done");
        }
    }

    // ---------------------------------------------------------------- testing

    //a test started on this page skips pages 1-4: give Kami what she would be carrying and bring
    //both followers along
    IEnumerator BeginForTest()
    {
        yield return null;
        yield return null;

        Debug.Log("[MuseumPage] test started on page 5: giving Kami the evidence and bringing the followers");

        LevelManager levelManager = LevelManager.Instance;
        Player player = levelManager != null ? levelManager.player : null;
        if (player == null)
        {
            Debug.LogWarning("[MuseumPage] no Player, cannot set page 5 up");
            yield break;
        }

        foreach (ResourceType item in _requiredEvidence)
        {
            if (!levelManager.recursosRecolectados.TryGetValue(item, out int held) || held <= 0)
            {
                levelManager.AddResource(item, 1);
            }
        }

        if (_museumQuest != null && QuestManager.Instance != null && !QuestManager.Instance.HasQuest(_museumQuest))
        {
            QuestManager.Instance.AddQuest(_museumQuest);
        }

        //the case is still open: the arrest's story music plays until the painting is returned (PageMusicManager)
        EventManager.Trigger(Evento.OnArrestStarted);

        Vector3 kami = player.transform.position;
        Vector3 side = player.transform.right * _followerSpacing;

        if (_natalia != null)
        {
            NataliaDialogueTrigger talk = _natalia.GetComponentInChildren<NataliaDialogueTrigger>(true);
            if (talk != null)
            {
                talk.SetTalkable(false);
            }
            if (_natalia.Character != null)
            {
                _natalia.Character.ClearRestPose(); //not angry: page 1's opening was skipped
            }
            //follow first: it reparents her next to Kami, out of page 1's folder, which is
            //switched off when the scene starts on another page (an inactive agent can't warp)
            _natalia.player = player;
            _natalia.StartFollowingPlayer();
            _natalia.WarpTo(kami - side);
        }

        NPC abuela = Abuela;
        if (abuela != null)
        {
            abuela.gameObject.SetActive(true);
            abuela.player = player;
            if (abuela.navAgent != null)
            {
                abuela.navAgent.enabled = true;
            }
            abuela.StartFollowingPlayer();
            abuela.WarpTo(kami - side * 2f);
        }
    }
}
