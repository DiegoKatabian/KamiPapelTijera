using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Level 2 page 5, "Clearing their name" (spec 006 Phase 5): owns the page's story.
///
/// 1. Talking to Grace (the museum director) needs the evidence: the Pelusa, the café ticket and
///    the three page 2 clues. With it the ARRIVAL CUTSCENE plays (Timeline_MuseumArrival: Grace
///    reacts to the Pelusa, sirens, a patrol car drives in, Ariel and a cop walk up, the camera pulls
///    back and returns) and then she hears the girls out (_graceMeeting); without the evidence she
///    asks for proof, which a normal playthrough never reaches.
///    Ariel and a cop are NOT on the page until then: they chased the escapees from the police station.
/// 2. When her meeting dialogue ends the café ticket's pedestal appears and auto-prompts the unfold.
/// 3. Once the ticket's text is read and closed, _evidenceDialogue plays: Natalia's receipt is her
///    alibi, the hat and glove are Ariel's, the watch dates the robbery, the police apologize, and
///    Grace offers the catapult to send Kami and the Abuela home (one talk, Diego 2026-09-28).
/// 4. When it ends everything is handed over (it leaves the inventory), OnPaintingReturned completes
///    Quest08, Natalia and the Abuela stop following Kami (Natalia is talkable again, farewell) and
///    the catapult (Catapult), which ends the level, unlocks.
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

    [Header("Arrival cutscene")]
    [SerializeField, Tooltip("Timeline_MuseumArrival. Played on the first talk with the evidence. Empty = no cutscene: Ariel and the cop are just there and Grace plays the meeting directly.")]
    CutsceneDirector _arrivalCutscene;

    [SerializeField, Tooltip("Shots that frame Kami: their Follow is set to her when the cutscene starts (a scene object cannot be assigned her prefab instance in the timeline).")]
    CinemachineVirtualCamera[] _camerasFollowingKami;

    [SerializeField, Tooltip("Ariel. Hidden until the cutscene; where he is placed in the scene is where he ends up standing.")]
    Transform _ariel;

    [SerializeField, Tooltip("The cop. Same as Ariel.")]
    Transform _copWitness;

    [SerializeField, Tooltip("The patrol car. Hidden until it drives in.")]
    Transform _patrolCar;

    [SerializeField, Tooltip("Where the car comes from (out of frame).")]
    Transform _carStart;

    [SerializeField, Tooltip("Where the car parks. Ariel and the cop step out here and walk to their spots.")]
    Transform _carStop;

    [SerializeField, Tooltip("Seconds the car takes to drive from its start to its stop (it slows down as it arrives).")]
    float _carArriveSeconds = 2.8f;

    [SerializeField, Tooltip("Seconds Ariel and the cop take to walk from the car to their spots.")]
    float _walkUpSeconds = 2.5f;

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
    Vector3 _arielSpot;
    Vector3 _copSpot;

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

        //they arrive in the cutscene: remember where they were placed, and keep them off the page until then
        if (_ariel != null)
        {
            _arielSpot = _ariel.position;
            _ariel.gameObject.SetActive(false);
        }

        if (_copWitness != null)
        {
            _copSpot = _copWitness.position;
            _copWitness.gameObject.SetActive(false);
        }

        if (_patrolCar != null)
        {
            _patrolCar.gameObject.SetActive(false);
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
            Debug.LogWarning("[MuseumPage] no arrival cutscene assigned: Ariel and the cop are just there and Grace plays the meeting directly");
            ShowPeople();
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

    void ShowPeople()
    {
        if (_ariel != null)
        {
            _ariel.position = _arielSpot;
            _ariel.gameObject.SetActive(true);
        }

        if (_copWitness != null)
        {
            _copWitness.position = _copSpot;
            _copWitness.gameObject.SetActive(true);
        }
    }

    /// <summary>Signal: the sirens start, from off the page.</summary>
    public void CUE_Sirens()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(AudioId.PoliceSirenLoop);
        }
    }

    /// <summary>Signal: the patrol car drives in from off frame and parks.</summary>
    public void CUE_CarArrives()
    {
        if (_patrolCar == null || _carStart == null || _carStop == null)
        {
            Debug.LogWarning("[MuseumPage] no patrol car or its marks: the car never arrives");
            return;
        }

        StartCoroutine(DriveCarIn());
    }

    IEnumerator DriveCarIn()
    {
        Vector3 direction = _carStop.position - _carStart.position;
        direction.y = 0f;

        _patrolCar.position = _carStart.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            _patrolCar.rotation = Quaternion.LookRotation(direction);
        }
        _patrolCar.gameObject.SetActive(true);

        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(AudioId.PoliceHorn);
            AudioManager.instance.Play(AudioId.CarEngine);
        }

        bool braked = false;
        float elapsed = 0f;
        while (elapsed < _carArriveSeconds)
        {
            float t = elapsed / Mathf.Max(0.01f, _carArriveSeconds);
            //ease out: a car pulling up to the kerb slows down as it arrives
            _patrolCar.position = Vector3.Lerp(_carStart.position, _carStop.position, 1f - ((1f - t) * (1f - t)));

            if (!braked && t > 0.7f)
            {
                braked = true;
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.Play(AudioId.CarBrake);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        _patrolCar.position = _carStop.position;
    }

    /// <summary>Signal: the doors slam, Ariel and the cop get out and walk up to the group. The siren stops.</summary>
    public void CUE_WalkUp()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopById(AudioId.PoliceSirenLoop);
            AudioManager.instance.Play(AudioId.CarDoor);
            AudioManager.instance.Play(AudioId.RunningFootsteps);
        }

        Vector3 door = _carStop != null ? _carStop.position : _arielSpot;
        StartCoroutine(WalkTo(_ariel, door, _arielSpot));
        StartCoroutine(WalkTo(_copWitness, door, _copSpot));
    }

    //they keep their own height (the sprite's pivot is at its center), only X and Z travel
    IEnumerator WalkTo(Transform person, Vector3 from, Vector3 to)
    {
        if (person == null)
        {
            yield break;
        }

        Vector3 start = new Vector3(from.x, to.y, from.z);
        person.position = start;
        person.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < _walkUpSeconds)
        {
            person.position = Vector3.Lerp(start, to, elapsed / Mathf.Max(0.01f, _walkUpSeconds));
            elapsed += Time.deltaTime;
            yield return null;
        }

        person.position = to;
    }

    void OnDisable()
    {
        //the page is switched off mid-cutscene only by a test or a bug: never leave the siren looping
        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopById(AudioId.PoliceSirenLoop);
        }
    }

    // ---------------------------------------------------------------- Grace

    bool HasEvidence()
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
                Debug.Log($"[MuseumPage] Kami has no {item}: Grace asks for proof");
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
            CloseTheCase();
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
            CloseTheCase();
            yield break;
        }

        DialogueManager.Instance.ShowDialogue(_evidenceDialogue);
    }

    void CloseTheCase()
    {
        _phase = Phase.CatapultReady;
        Debug.Log("[MuseumPage] the case is closed: the Pelusa is back, the girls stay behind, the catapult unlocks");

        HandOverEvidence();

        if (_natalia != null)
        {
            _natalia.StopFollowingPlayer();
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

        if (Abuela != null)
        {
            Abuela.StopFollowingPlayer();
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

        Vector3 kami = player.transform.position;
        Vector3 side = player.transform.right * _followerSpacing;

        if (_natalia != null)
        {
            NataliaDialogueTrigger talk = _natalia.GetComponentInChildren<NataliaDialogueTrigger>(true);
            if (talk != null)
            {
                talk.SetTalkable(false);
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
