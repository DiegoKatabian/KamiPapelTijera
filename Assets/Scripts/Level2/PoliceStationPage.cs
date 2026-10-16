using System.Collections;
using UnityEngine;

/// <summary>
/// Level 2 page 4, "Escape from the police station" (spec 006 Phase 4): owns the page's sequence
/// and is the only thing that restarts it.
///
/// The sequence: Kami and Natalia wake up in separate cells with their things confiscated. The
/// intro cutscene plays the first time (a camera tour of the page, the cop's taunt, Natalia's reply
/// and the narrator, all on Timeline_PoliceIntro), and _abuelaDelaySeconds after it ends the Abuela
/// crashes down and opens Kami's cell fence (AbuelaEntrance). Kami
/// gets her things back at the evidence pickup (ConfiscatedGear), cuts the padlock on Natalia's
/// cell -- which also holds the paper-plane pedestal -- folds the plane, jumps to the 2nd floor,
/// cuts the drapes and goes through the window. Crossing it completes Quest07 (escape) and starts
/// Quest08 (take the Pelusa back to the museum); the page itself is left the normal way, by the
/// edge of the book.
///
/// Getting caught by a cop restarts the WHOLE page (Diego, 2026-09-27): both girls back in their
/// cells, everything confiscated again, padlock and drapes whole again, the Abuela gone and falling
/// again after the shorter _abuelaDelayAfterCaptureSeconds, no intro cutscene, and anyone riding the
/// paper plane (PaperPlaneRide) gets off first. The restart runs when Kami respawns in
/// her cell (the arrest made the cell her respawn point), so the overlay hides every teleport.
/// </summary>
public class PoliceStationPage : MonoBehaviour
{
    enum Phase
    {
        NotStarted,
        Imprisoned,
        Escaped
    }

    [Header("Cells")]
    [SerializeField, Tooltip("Where Kami is locked up. Also her respawn point while she is on this page.")]
    Transform _kamiCell;

    [SerializeField, Tooltip("Where Natalia is locked up.")]
    Transform _nataliaCell;

    [SerializeField, Tooltip("Natalia (the one from page 1: there is only one in the level).")]
    NPC _natalia;

    [SerializeField, Tooltip("The fence segment that opens when the padlock is cut.")]
    GameObject _nataliaCellDoor;

    [SerializeField, Tooltip("The padlock on Natalia's cell door.")]
    CandadoCortable _nataliaPadlock;

    [SerializeField, Tooltip("What Natalia says when Kami frees her, the first time only: thanks, and the window + paper plane idea that points the player at the pedestal in her cell. Empty = no line.")]
    DialogueSO _nataliaFreedLine;

    [SerializeField, Tooltip("Natalia's rest pose (mood) behind bars, a pose id from her SpineCharacter. Cleared when Kami frees her. Empty = plain idle.")]
    string _nataliaInCellRestPose = "Angry";

    [Header("Abuela")]
    [SerializeField, Tooltip("The Abuela's fall onto Kami's cell.")]
    AbuelaEntrance _abuelaEntrance;

    [SerializeField, Tooltip("The intro cutscene (Timeline_PoliceIntro), played the first time only and not skippable: camera tour, the cop's taunt, Natalia's reply, the narrator. Empty = no intro.")]
    CutsceneDirector _introCutscene;

    [SerializeField, Tooltip("The camera the player starts with once the intro is over and the Abuela has landed (Kami is free), so the whole station is in view. The player changes it as usual afterwards. Only after the intro (first time), not after a capture.")]
    CameraMode _cameraAfterIntro = CameraMode.BookCenter;

    [SerializeField, Tooltip("Seconds between the girls landing in their cells and the intro cutscene starting.")]
    float _introDelaySeconds = 0.5f;

    [SerializeField, Tooltip("Seconds between the intro ending and the Abuela crashing down. Kami waits, locked, from the start of it until the Abuela's line ends.")]
    float _abuelaDelaySeconds = 3f;

    [SerializeField, Tooltip("The same wait after a capture restarts the page: shorter, the player already saw it.")]
    float _abuelaDelayAfterCaptureSeconds = 3f;

    [Header("Police")]
    [SerializeField, Tooltip("What the police take from Kami and give back at the evidence pickup.")]
    ConfiscatedGear _gear;

    [SerializeField, Tooltip("Every cop on the page: reset to their posts on a restart, blind after the escape.")]
    PoliceOfficer[] _officers;

    [Header("Escape")]
    [SerializeField, Tooltip("The drapes over the escape window. Whole again on a restart.")]
    CuttableDrapes _windowDrapes;

    [SerializeField, Tooltip("The page's trash cans: closed and full again after a capture, so Kami can always gather the paper the page asks for (#219).")]
    TriggerSolapa[] _trashCans;

    [SerializeField, Tooltip("Outside, below the window: Natalia and the Abuela are brought here when Kami escapes, and it becomes her respawn point.")]
    Transform _outsideLanding;

    [SerializeField, Tooltip("Sideways spacing between Natalia and the Abuela at _outsideLanding, in world units.")]
    float _followerSpacing = 3f;

    [SerializeField, Tooltip("Gets the followers on and off the paper plane. Told to get everyone off before a restart, and to hand them back as followers when Kami crosses the window. Optional.")]
    PaperPlaneRide _planeRide;

    [SerializeField, Tooltip("Quest07_EscapeThePoliceStation: completed by crossing the window.")]
    QuestSO _escapeQuest;

    [SerializeField, Tooltip("Quest08_ReturnThePelusa: started once the escape is complete.")]
    QuestSO _museumQuest;

    [Header("Testing")]
    [SerializeField, Tooltip("Starting the scene on this page (PageScrollerManager.startingPage) sets the prison up by itself, without playing pages 1-3.")]
    int _pageNumber = 4;

    Phase _phase = Phase.NotStarted;
    bool _restartPending;
    bool _questsHandedOff;
    bool _freedLineShown;
    Coroutine _introRoutine;

    void Start()
    {
        EventManager.Subscribe(Evento.OnPlayerDie, OnPlayerDie);
        EventManager.Subscribe(Evento.OnPlayerPlaced, OnPlayerPlaced);
        EventManager.Subscribe(Evento.OnQuestCompleted, OnQuestCompleted);

        if (_nataliaPadlock != null)
        {
            _nataliaPadlock.AddCutListener(OnNataliaPadlockCut);
        }

        if (_kamiCell == null || _nataliaCell == null || _natalia == null || _nataliaCellDoor == null || _abuelaEntrance == null || _gear == null || _outsideLanding == null)
        {
            Debug.LogWarning($"[PoliceStationPage] {gameObject.name}: some references are not assigned, the page sequence will be incomplete");
        }

        if (PageScrollerManager.Instance != null && PageScrollerManager.Instance.startingPage == _pageNumber)
        {
            StartCoroutine(BeginForTest());
        }
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnPlayerDie, OnPlayerDie);
        EventManager.Unsubscribe(Evento.OnPlayerPlaced, OnPlayerPlaced);
        EventManager.Unsubscribe(Evento.OnQuestCompleted, OnQuestCompleted);
    }

    // ---------------------------------------------------------------- starting the page

    /// <summary>Called by ArrestCutscene once the girls are in their cells.</summary>
    public void BeginFromArrest()
    {
        if (_phase != Phase.NotStarted)
        {
            return;
        }

        StartSequence(firstTime: true);
    }

    //a test started on this page skips the arrest: put everyone where the arrest would have
    IEnumerator BeginForTest()
    {
        //right away, before the waits: she lives under page 1's folder (off on this test), and her own
        //Start (which runs once she is out of it) must not undo the cell's mood set below (#170)
        if (_natalia is NPC_Natalia natalia)
        {
            natalia.SetUpForTestStart(_pageNumber, followKami: false);
        }
        else
        {
            Debug.LogWarning("[PoliceStationPage] _natalia is not an NPC_Natalia: her page 1 lines stay on for this test start");
        }

        //two frames: Player gives Kami her starting scissors one frame late, and they must be
        //confiscated like in the real flow
        yield return null;
        yield return null;

        if (_phase != Phase.NotStarted)
        {
            yield break;
        }

        Debug.Log("[PoliceStationPage] test started on page 4: setting up the prison without the arrest");

        if (_kamiCell != null && PlayerPageSpawnManager.Instance != null)
        {
            PlayerPageSpawnManager.Instance.PositionPlayerAtPoint(_kamiCell.position);
            PlayerPageSpawnManager.Instance.SavePosition(_kamiCell.position);
        }

        if (_escapeQuest != null && QuestManager.Instance != null && !QuestManager.Instance.HasQuest(_escapeQuest))
        {
            QuestManager.Instance.AddQuest(_escapeQuest);
        }

        //before StartSequence: the confiscation takes the Pelusa and the pickup gives it back, like
        //after the real arrest (#187)
        Level2TestStartItems.GrantItemsBefore(_pageNumber);

        //the arrest's story music, as if it had played (PageMusicManager)
        EventManager.Trigger(Evento.OnArrestStarted);

        StartSequence(firstTime: true);
    }

    void StartSequence(bool firstTime)
    {
        _phase = Phase.Imprisoned;
        Debug.Log($"[PoliceStationPage] {(firstTime ? "the girls are locked up" : "caught: the page starts over")}");

        //the cell doors slam behind the girls (also after a capture, when they are put back in)
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(AudioId.CellBarsSlam);
        }

        //before anything moves: riders must be off the plane, or the warps below would fight their seats
        if (_planeRide != null)
        {
            _planeRide.ForceDismountAll();
        }

        if (_gear != null)
        {
            _gear.ConfiscateAll();
        }

        if (_natalia != null && _nataliaCell != null)
        {
            _natalia.StopFollowingPlayer();
            _natalia.WarpTo(_nataliaCell.position);
            if (_natalia.Character != null && !string.IsNullOrEmpty(_nataliaInCellRestPose))
            {
                _natalia.Character.SetRestPose(_nataliaInCellRestPose);
            }
        }

        if (_nataliaCellDoor != null)
        {
            _nataliaCellDoor.SetActive(true);
        }

        if (_nataliaPadlock != null)
        {
            _nataliaPadlock.RestoreUncut();
        }

        if (_windowDrapes != null)
        {
            _windowDrapes.RestoreUncut();
        }

        //the paper she spent on the fold and the padlock is gone, and both have to be redone (#219)
        if (!firstTime && _trashCans != null)
        {
            foreach (TriggerSolapa trashCan in _trashCans)
            {
                if (trashCan != null)
                {
                    trashCan.ResetToFresh();
                }
            }
        }

        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }

        if (_abuelaEntrance != null)
        {
            _abuelaEntrance.ResetEntrance();

            //the intro only opens the page the first time; after a capture the Abuela just comes back
            if (firstTime && _introCutscene != null)
            {
                _introRoutine = StartCoroutine(IntroThenAbuela());
            }
            else
            {
                _abuelaEntrance.Schedule(firstTime ? _abuelaDelaySeconds : _abuelaDelayAfterCaptureSeconds);
            }
        }

        foreach (PoliceOfficer officer in _officers)
        {
            if (officer != null)
            {
                officer.ResetToStart();
                officer.SetDetectionEnabled(true);
            }
        }
    }

    //the cutscene locks Kami by itself (CutsceneDirector -> LevelManager.inCutscene) and the cops go
    //blind while it plays, so the tour is safe. The Abuela is only scheduled once it is over.
    IEnumerator IntroThenAbuela()
    {
        yield return new WaitForSeconds(_introDelaySeconds);

        //the page turn that brought Kami here (or a stray dialogue) may still be freezing the game
        while (LevelManager.Instance == null || LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene)
        {
            yield return null;
        }

        _introCutscene.Play();
        yield return null;

        while (_introCutscene.IsPlaying)
        {
            yield return null;
        }

        _abuelaEntrance.Schedule(_abuelaDelaySeconds);

        //Kami is locked until the Abuela has landed and spoken (HasLanded is set the moment she gets control
        //back): that is when the player gets the wide shot of the whole station
        while (!_abuelaEntrance.HasLanded)
        {
            yield return null;
        }

        _introRoutine = null;

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCamera(_cameraAfterIntro);
        }
        else
        {
            Debug.LogWarning("[PoliceStationPage] no CameraManager: the camera stays as it was after the intro");
        }
    }

    // ---------------------------------------------------------------- during the page

    void OnNataliaPadlockCut()
    {
        if (_phase != Phase.Imprisoned)
        {
            return;
        }

        Debug.Log("[PoliceStationPage] Natalia's padlock is cut: she is free");

        if (_nataliaCellDoor != null)
        {
            _nataliaCellDoor.SetActive(false);
        }

        //the padlock's own clank comes from CandadoCortable; this is the door swinging open
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(AudioId.FenceRattle);
        }

        if (_natalia != null)
        {
            if (_natalia.Character != null)
            {
                _natalia.Character.ClearRestPose();
            }
            _natalia.StartFollowingPlayer();
        }

        if (!_freedLineShown && _nataliaFreedLine != null)
        {
            _freedLineShown = true;
            StartCoroutine(SayFreedLine());
        }
    }

    //the padlock cut can land in the middle of another line: wait for a free dialogue box
    IEnumerator SayFreedLine()
    {
        while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
        {
            yield return null;
        }

        DialogueManager.Instance.ShowDialogue(_nataliaFreedLine);
    }

    void OnPlayerDie(params object[] parameters)
    {
        if (_phase != Phase.Imprisoned || parameters == null || parameters.Length == 0 || !(parameters[0] is DeathCause cause))
        {
            return;
        }

        if (cause == DeathCause.Caught)
        {
            _restartPending = true;
        }
    }

    //Kami has just respawned in her cell: restart behind the overlay
    void OnPlayerPlaced(params object[] parameters)
    {
        if (!_restartPending)
        {
            return;
        }

        _restartPending = false;
        StartSequence(firstTime: false);
    }

    /// <summary>Called by EscapeWindow when Kami goes through the 2nd floor window.</summary>
    public void OnKamiCrossedWindow()
    {
        if (_phase != Phase.Imprisoned)
        {
            return;
        }

        _phase = Phase.Escaped;
        Debug.Log("[PoliceStationPage] Kami escaped through the window");

        foreach (PoliceOfficer officer in _officers)
        {
            if (officer != null)
            {
                officer.SetDetectionEnabled(false);
            }
        }

        if (_planeRide != null)
        {
            //anyone still on the plane (or parked up here) becomes a follower again, so the warp below takes them outside
            _planeRide.RejoinAllForWindow();
        }

        if (_outsideLanding != null)
        {
            //the girls following her can't climb to the 2nd floor: they "get out" and wait for her below
            Vector3 side = Vector3.Cross(Vector3.up, _outsideLanding.forward).normalized * (_followerSpacing * 0.5f);
            BringOutside(_natalia, _outsideLanding.position + side);
            BringOutside(_abuelaEntrance != null ? _abuelaEntrance.Abuela : null, _outsideLanding.position - side);

            //the cell stops being her respawn point once she is out
            if (PlayerPageSpawnManager.Instance != null)
            {
                PlayerPageSpawnManager.Instance.SavePosition(_outsideLanding.position);
            }
        }

        //always: PageExitLock opens page 4's next sphere on this event, and the way out must not depend
        //on quest bookkeeping (#169: a lost Quest07 kept the sphere locked for good)
        EventManager.Trigger(Evento.OnPoliceStationEscaped);

        //with Quest07 held, QuestManager completes it and raises OnQuestCompleted, which hands off below
        if (_escapeQuest == null || QuestManager.Instance == null || !QuestManager.Instance.HasQuest(_escapeQuest))
        {
            StartCoroutine(HandOffQuestNextFrame());
        }
    }

    static void BringOutside(NPC follower, Vector3 position)
    {
        if (follower != null && follower.gameObject.activeInHierarchy && follower.isFollowing)
        {
            follower.WarpTo(position);
        }
    }

    // ---------------------------------------------------------------- quests

    void OnQuestCompleted(params object[] parameters)
    {
        if (_questsHandedOff || _escapeQuest == null || parameters == null || parameters.Length == 0)
        {
            return;
        }

        if ((QuestSO)parameters[0] != _escapeQuest)
        {
            return;
        }

        StartCoroutine(HandOffQuestNextFrame());
    }

    //OnQuestCompleted is raised from inside QuestManager.CheckQuests' foreach over its quests, so
    //removing/adding a quest right there throws: wait a frame (same as ArrestCutscene). No
    //OnQuestDelivered: these quests have no reward (it would pop a mushroom sticker).
    IEnumerator HandOffQuestNextFrame()
    {
        if (_questsHandedOff)
        {
            yield break;
        }

        _questsHandedOff = true;
        yield return null;

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[PoliceStationPage] there is no QuestManager in the scene, cannot hand off the quests.");
            yield break;
        }

        if (_escapeQuest != null && QuestManager.Instance.HasQuest(_escapeQuest))
        {
            QuestManager.Instance.RemoveQuest(_escapeQuest);
            AudioManager.instance.Play(AudioId.QuestCompleted02);
        }

        if (_museumQuest != null)
        {
            QuestManager.Instance.AddQuest(_museumQuest);
            Debug.Log($"[PoliceStationPage] started {_museumQuest.name}");
        }
    }
}
