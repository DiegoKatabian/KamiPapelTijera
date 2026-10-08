using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 3's arrest (spec 006, Story 3): after the trap letter is read, the police show up,
/// Ariel accuses the girls, the cops escort them into the patrol car, the car drives off, and the
/// page turns to the police station with Kami and Natalia each in a cell.
///
/// The WHEN lives in the timeline (Timeline_Arrest): camera shots, the street group appearing,
/// dialogue markers, and signals for every beat below. This script only holds the WHAT of each
/// beat — the SignalReceiver on this GameObject calls the CUE_ methods. To retime anything, move
/// clips and markers in the Timeline window; nothing here has a duration except the car's drive.
///
/// Lives at the scene root on purpose, not under Page 3: the page folder is switched off mid-turn,
/// and the wrap-up (cells, quests) runs after that.
/// </summary>
[RequireComponent(typeof(CutsceneDirector))]
public class ArrestCutscene : MonoBehaviour
{
    [Header("Start")]
    [SerializeField, Tooltip("The panel that shows the trap letter. The arrest starts once the player closes it. Optional: without it the arrest starts right after the unfold.")]
    OrigamiTextRevealDisplay _letterDisplay;

    [SerializeField, Tooltip("Seconds of calm between closing the letter and the police shout.")]
    float _startDelay = 0.6f;

    [Header("Characters")]
    [SerializeField, Tooltip("Natalia (the one from page 1, following Kami).")]
    NPC _natalia;

    [SerializeField, Tooltip("The cops that pop up behind the girls and escort them. Start inactive.")]
    NPC[] _escortCops;

    [SerializeField, Tooltip("How far behind the girls the escort cops appear, away from the car.")]
    float _copsBehindDistance = 5f;

    [SerializeField, Tooltip("Sideways spacing between the escort cops.")]
    float _copsSpread = 3f;

    [Header("Escort walk")]
    [SerializeField, Tooltip("Where Kami walks to during the escort: the patrol car's door.")]
    Transform _carDoorMark;

    [SerializeField, Tooltip("Stick amount for Kami's escort walk (1 = full stick); her speed scales with it (about 20 units/s at 1). Kept below Player.walkThreshold at runtime: at or above it she skips instead of walking (the old 0.6 did exactly that).")]
    [Range(0.1f, 1f)]
    float _escortWalkInput = 0.3f;

    [SerializeField, Tooltip("Kami stops walking this close to the car door.")]
    float _arriveDistance = 1.5f;

    [Header("Escort formation (spec 013)")]
    [SerializeField, Tooltip("The cops walk the NavMesh to the car door and the girls are marched along THEIR route, in front of them. Off, or when there is no NavMesh route for the lead cop, the old behaviour: Kami walks a straight line and everybody follows her.")]
    bool _copsLeadTheEscort = true;

    [SerializeField, Tooltip("Kami is aimed this many units further along the cops' route than where she is now, so she keeps marching ahead of them.")]
    float _girlsLookAhead = 3f;

    [SerializeField, Tooltip("The girls stop and wait when they get further than this ahead of the lead cop (world units).")]
    float _maxGirlsAhead = 9f;

    [SerializeField, Tooltip("A cop stops this close behind Kami so the escort never overtakes the girls; it walks again once she is a unit further.")]
    float _copsKeepBack = 3f;

    [SerializeField, Tooltip("Cop walking speed as a multiple of Kami's escort speed. A bit above 1 so they keep up and close behind her.")]
    float _copsSpeedFactor = 1.2f;

    [SerializeField, Tooltip("Seconds between recalculations of the cops' route.")]
    float _routeRefreshSeconds = 0.25f;

    [Header("Patrol car")]
    [SerializeField, Tooltip("The patrol car that drives off with the girls.")]
    TrafficObstacle _policeCar;

    [SerializeField, Tooltip("Where the car drives to.")]
    Transform _driveOffTarget;

    [SerializeField, Tooltip("Seconds the car takes to reach _driveOffTarget. Duration, not speed, like all traffic.")]
    float _driveOffSeconds = 4f;

    [Header("Cameras")]
    [SerializeField, Tooltip("Shots that frame Kami: their Follow is set to her when the cutscene starts (a prefab cannot reference her).")]
    CinemachineVirtualCamera[] _camerasFollowingKami;

    [Header("Page 4")]
    [SerializeField, Tooltip("Kami's cell on page 4. The page turn drops her here, and it becomes her respawn point.")]
    Transform _kamiCell;

    [SerializeField, Tooltip("Natalia's cell on page 4.")]
    Transform _nataliaCell;

    [SerializeField, Tooltip("Page 4's sequence: told to start once the girls are in their cells.")]
    PoliceStationPage _policeStation;

    [Header("Quests")]
    [SerializeField, Tooltip("Quest06_GoBackToNataliasHouse: completed and removed when the girls reach the station.")]
    QuestSO _goHomeQuest;

    [SerializeField, Tooltip("Quest07_EscapeThePoliceStation: started when the girls reach the station.")]
    QuestSO _escapeQuest;

    CutsceneDirector _cutscene;
    Player _player;
    bool _started;
    bool _escorting;
    bool _waitingForPage;
    bool _questsHandedOff;
    bool _warnedWalkClamp;
    bool _copsLead; //this escort runs with the cops leading (decided when it starts: needs a NavMesh route)
    NavMeshPath _copRoute; //created in Awake: NavMeshPath cannot be built in a field initializer
    float _nextRouteRefresh;
    bool[] _copHolding;
    HiddenRenderers _hiddenKami;
    HiddenRenderers _hiddenNatalia;

    void Awake()
    {
        _cutscene = GetComponent<CutsceneDirector>();
        _copRoute = new NavMeshPath();
    }

    void Start()
    {
        EventManager.Subscribe(Evento.OnTrapLetterUnfolded, OnLetterUnfolded);
        EventManager.Subscribe(Evento.OnPageFinishTurning, OnPageFinishTurning);
        EventManager.Subscribe(Evento.OnQuestCompleted, OnQuestCompleted);

        foreach (NPC cop in _escortCops)
        {
            if (cop != null)
            {
                cop.gameObject.SetActive(false);
            }
        }

        if (_natalia == null || _carDoorMark == null || _policeCar == null || _driveOffTarget == null || _kamiCell == null || _nataliaCell == null)
        {
            Debug.LogWarning($"[ArrestCutscene] {gameObject.name}: some references are not assigned (Natalia, car door, car, drive-off target or cells). The cutscene will skip whatever is missing.");
        }
    }

    void OnLetterUnfolded(params object[] parameters)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        StartCoroutine(StartWhenLetterClosed());
    }

    IEnumerator StartWhenLetterClosed()
    {
        //Apply fires the event and opens the panel in the same frame: wait for it to show first
        yield return null;

        while (_letterDisplay != null && _letterDisplay.IsReading)
        {
            yield return null;
        }

        yield return new WaitForSeconds(_startDelay);

        _player = LevelManager.Instance.player;
        foreach (CinemachineVirtualCamera cam in _camerasFollowingKami)
        {
            if (cam != null && _player != null)
            {
                cam.Follow = _player.transform;
            }
        }

        Debug.Log("[ArrestCutscene] the letter was read, the police arrive");
        _cutscene.Play();
    }

    void Update()
    {
        if (!_escorting || _player == null || _carDoorMark == null)
        {
            return;
        }

        Vector3 toDoor = _carDoorMark.position - _player.transform.position;
        toDoor.y = 0f;

        bool arrived = toDoor.magnitude <= _arriveDistance;

        //PlayerModel picks Skipping for any stick amount at or above walkThreshold, so clamp just under it:
        //tuning the Inspector number later can't bring the skip back
        float maxWalkInput = _player.walkThreshold - 0.05f;
        float walkInput = _escortWalkInput;
        if (walkInput > maxWalkInput)
        {
            if (!_warnedWalkClamp)
            {
                _warnedWalkClamp = true;
                Debug.LogWarning($"[ArrestCutscene] _escortWalkInput {_escortWalkInput} is not below Player.walkThreshold {_player.walkThreshold}: Kami would skip, so the walk uses {maxWalkInput} instead");
            }
            walkInput = maxWalkInput;
        }

        Vector3 aim = toDoor;
        if (_copsLead)
        {
            NPC lead = FirstActiveCop();
            if (lead != null)
            {
                RefreshRoute(lead);
                Vector3 onRoute = AimAlongRoute(_player.transform.position);
                if (onRoute != Vector3.zero)
                {
                    aim = onRoute - _player.transform.position;
                    aim.y = 0f;
                }

                //never get far ahead of the cops: the girls are marched, they don't lead
                if (HorizontalDistance(lead.transform.position, _player.transform.position) > _maxGirlsAhead)
                {
                    walkInput = 0f;
                }
            }

            DriveCops();
        }

        if (arrived || walkInput <= 0f || aim.sqrMagnitude < 0.0001f)
        {
            _player.StopCutsceneWalk();
            return;
        }

        _player.SetCutsceneWalk(aim.normalized, walkInput);
    }

    // ---------------------------------------------------------------- the cops lead the escort

    NPC FirstActiveCop()
    {
        foreach (NPC cop in _escortCops)
        {
            if (cop != null && cop.gameObject.activeInHierarchy)
            {
                return cop;
            }
        }
        return null;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>The lead cop's NavMesh route to the car door. False when there is none (no mesh near him or the door).</summary>
    bool CalculateRoute(NPC cop)
    {
        if (_carDoorMark == null
            || !NavMesh.SamplePosition(cop.transform.position, out NavMeshHit from, 8f, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(_carDoorMark.position, out NavMeshHit to, 8f, NavMesh.AllAreas))
        {
            return false;
        }

        return NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _copRoute)
               && _copRoute.status == NavMeshPathStatus.PathComplete
               && _copRoute.corners.Length >= 2;
    }

    void RefreshRoute(NPC lead)
    {
        if (Time.time < _nextRouteRefresh)
        {
            return;
        }

        _nextRouteRefresh = Time.time + _routeRefreshSeconds;
        CalculateRoute(lead); //on failure the last good route stays in _copRoute
    }

    /// <summary>
    /// A point on the cops' route a little further along than the closest point to <paramref name="from"/>,
    /// so Kami walks the route and never turns back toward it. Vector3.zero when there is no route.
    /// </summary>
    Vector3 AimAlongRoute(Vector3 from)
    {
        Vector3[] corners = _copRoute.corners;
        if (corners == null || corners.Length < 2)
        {
            return Vector3.zero;
        }

        float bestDistance = float.MaxValue;
        float bestAlong = 0f;
        float walked = 0f;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 segment = corners[i + 1] - corners[i];
            float length = segment.magnitude;
            if (length < 0.0001f)
            {
                continue;
            }

            float t = Mathf.Clamp01(Vector3.Dot(from - corners[i], segment) / (length * length));
            float distance = Vector3.Distance(from, corners[i] + (segment * t));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestAlong = walked + (length * t);
            }
            walked += length;
        }

        float remaining = Mathf.Min(bestAlong + _girlsLookAhead, walked);
        for (int i = 0; i < corners.Length - 1; i++)
        {
            float length = Vector3.Distance(corners[i], corners[i + 1]);
            if (remaining <= length)
            {
                return length < 0.0001f ? corners[i] : Vector3.Lerp(corners[i], corners[i + 1], remaining / length);
            }
            remaining -= length;
        }

        return corners[corners.Length - 1];
    }

    /// <summary>Cops walk to the car door on the NavMesh, and hold back when they get right behind Kami.</summary>
    void DriveCops()
    {
        if (_copHolding == null || _copHolding.Length != _escortCops.Length)
        {
            _copHolding = new bool[_escortCops.Length];
        }

        for (int i = 0; i < _escortCops.Length; i++)
        {
            NPC cop = _escortCops[i];
            if (cop == null || !cop.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceToKami = HorizontalDistance(cop.transform.position, _player.transform.position);
            //hysteresis, otherwise a cop right at the limit flickers between walking and stopped every frame
            _copHolding[i] = _copHolding[i] ? distanceToKami < _copsKeepBack + 1f : distanceToKami < _copsKeepBack;

            if (_copHolding[i])
            {
                cop.StopAgent();
                cop.SetWalkAnimation(false);
                continue;
            }

            cop.MoveTowards(_carDoorMark.position);
            cop.SetWalkAnimation(true);
            cop.UpdateSpriteFlip();
        }
    }

    // ---------------------------------------------------------------- timeline signals

    /// <summary>Signal: the escort cops appear right behind the girls, wherever Kami is standing.</summary>
    public void CUE_CopsBehindGirls()
    {
        if (_player == null)
        {
            return;
        }

        Vector3 kami = _player.transform.position;
        Vector3 away = _carDoorMark != null ? kami - _carDoorMark.position : -_player.transform.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, away);

        for (int i = 0; i < _escortCops.Length; i++)
        {
            NPC cop = _escortCops[i];
            if (cop == null)
            {
                continue;
            }

            //spread the cops evenly across the line behind the girls
            float slot = _escortCops.Length == 1 ? 0f : (i / (float)(_escortCops.Length - 1)) - 0.5f;
            Vector3 position = kami + (away * _copsBehindDistance) + (side * slot * _copsSpread * 2f);

            cop.gameObject.SetActive(true);
            cop.player = _player;
            cop.WarpTo(position);
        }

        //the squad rolls in: brakes, a horn beep, running boots, and the siren stays on until the
        //car leaves (stopped in CUE_TurnPage/WrapUp/OnDestroy)
        PlaySound(AudioId.CarBrake);
        PlaySound(AudioId.PoliceHorn);
        PlaySound(AudioId.RunningFootsteps);
        PlaySound(AudioId.PoliceSirenLoop);

        Debug.Log("[ArrestCutscene] the cops appear behind the girls");
    }

    /// <summary>Signal: Kami walks to the car door, Natalia and the cops follow her.</summary>
    public void CUE_StartEscort()
    {
        _escorting = true;

        if (_natalia != null)
        {
            _natalia.StartFollowingPlayer();
        }

        NPC lead = FirstActiveCop();
        _copsLead = _copsLeadTheEscort && lead != null && _carDoorMark != null && CalculateRoute(lead);
        _nextRouteRefresh = Time.time + _routeRefreshSeconds;

        if (_copsLeadTheEscort && !_copsLead)
        {
            Debug.LogWarning("[ArrestCutscene] no NavMesh route from the lead cop to the car door (is page 3's NavMesh baked there?): falling back to the cops following Kami");
        }

        //walking pace tied to Kami's, not to the cops' chase speed
        float kamiWalkInput = _player != null ? Mathf.Min(_escortWalkInput, _player.walkThreshold - 0.05f) : _escortWalkInput;
        float copSpeed = kamiWalkInput * (_player != null ? _player.CurrentSpeed : 20f) * _copsSpeedFactor;

        foreach (NPC cop in _escortCops)
        {
            if (cop == null || !cop.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (_copsLead)
            {
                if (cop.navAgent != null)
                {
                    cop.navAgent.speed = copSpeed;
                }
            }
            else
            {
                cop.StartFollowingPlayer();
            }
        }

        Debug.Log($"[ArrestCutscene] the escort to the car starts ({(_copsLead ? $"cops lead on the NavMesh at {copSpeed:F1} units/s" : "cops follow Kami")})");
    }

    /// <summary>Signal: everyone gets in the car (they vanish, the door slams).</summary>
    public void CUE_BoardCar()
    {
        _escorting = false;

        if (_player != null)
        {
            _player.StopCutsceneWalk();
            _hiddenKami = HiddenRenderers.Hide(_player.gameObject);
        }

        if (_natalia != null)
        {
            _natalia.StopFollowingPlayer();
            _hiddenNatalia = HiddenRenderers.Hide(_natalia.gameObject);
        }

        foreach (NPC cop in _escortCops)
        {
            if (cop != null)
            {
                cop.StopFollowingPlayer();
                cop.gameObject.SetActive(false);
            }
        }

        AudioManager.instance.Play(AudioId.CarDoor);
        Debug.Log("[ArrestCutscene] the girls get into the patrol car");
    }

    /// <summary>Signal: the patrol car drives off (the car-follow shot should be live by now).</summary>
    public void CUE_DriveOff()
    {
        if (_policeCar == null || _driveOffTarget == null)
        {
            Debug.LogWarning("[ArrestCutscene] no police car or drive-off target, the car stays put");
            return;
        }

        //the lifetime is only a safety net; the car despawns itself on arrival
        _policeCar.Launch(_driveOffTarget.position, _driveOffSeconds, (_driveOffSeconds * 3f) + 1f);
        PlaySound(AudioId.CarEngine);
        PlaySound(AudioId.PoliceHorn);
        Debug.Log("[ArrestCutscene] the patrol car drives off");
    }

    /// <summary>Signal: turn to page 4, dropping Kami in her cell.</summary>
    public void CUE_TurnPage()
    {
        StopSiren();

        if (_kamiCell != null && PlayerPageSpawnManager.Instance != null)
        {
            PlayerPageSpawnManager.Instance.OverrideNextPlacement(_kamiCell.position);
        }

        _waitingForPage = PageScrollerManager.Instance != null && PageScrollerManager.Instance.TurnToNextPage(false);

        if (!_waitingForPage)
        {
            Debug.LogWarning("[ArrestCutscene] the page did not turn, wrapping up the cutscene here");
            WrapUp();
        }
    }

    // ---------------------------------------------------------------- sound

    static void PlaySound(string id)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[ArrestCutscene] no AudioManager, '{id}' skipped");
            return;
        }
        AudioManager.instance.Play(id);
    }

    static void StopSiren()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopById(AudioId.PoliceSirenLoop);
        }
    }

    // ---------------------------------------------------------------- wrap-up on page 4

    void OnPageFinishTurning(params object[] parameters)
    {
        if (!_waitingForPage)
        {
            return;
        }

        _waitingForPage = false;
        WrapUp();
    }

    void WrapUp()
    {
        StopSiren(); //safety net when the page never turned and CUE_TurnPage's stop was skipped

        _hiddenKami?.Restore();
        _hiddenNatalia?.Restore();

        //she stays in her own cell until Kami frees her (Phase 4 calls StartFollowingPlayer again)
        if (_natalia != null && _nataliaCell != null)
        {
            _natalia.WarpTo(_nataliaCell.position);
        }

        _cutscene.Release();
        Debug.Log("[ArrestCutscene] the girls are in their cells, closing Quest06");

        if (_policeStation != null)
        {
            _policeStation.BeginFromArrest();
        }
        else
        {
            Debug.LogWarning("[ArrestCutscene] no _policeStation assigned, page 4's escape will not start");
        }

        if (_goHomeQuest != null && QuestManager.Instance != null && QuestManager.Instance.HasQuest(_goHomeQuest))
        {
            //QuestManager completes it and raises OnQuestCompleted, which hands off below
            EventManager.Trigger(Evento.OnArrestSequenceEnded);
        }
        else
        {
            //a test started past page 2 never had Quest06: still start the next quest
            StartCoroutine(HandOffQuestNextFrame());
        }
    }

    void OnQuestCompleted(params object[] parameters)
    {
        if (_questsHandedOff || parameters == null || parameters.Length == 0 || _goHomeQuest == null)
        {
            return;
        }

        if ((QuestSO)parameters[0] != _goHomeQuest)
        {
            return;
        }

        StartCoroutine(HandOffQuestNextFrame());
    }

    //OnQuestCompleted is raised from inside QuestManager.CheckQuests' foreach over its quests, so
    //removing/adding a quest right there throws; same wait-a-frame as FindCluesTracker. No
    //OnQuestDelivered either: these quests have no reward (it would pop a mushroom sticker).
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
            Debug.LogWarning("[ArrestCutscene] there is no QuestManager in the scene, cannot hand off the quests.");
            yield break;
        }

        if (_goHomeQuest != null && QuestManager.Instance.HasQuest(_goHomeQuest))
        {
            QuestManager.Instance.RemoveQuest(_goHomeQuest);
            AudioManager.instance.Play(AudioId.QuestCompleted02);
        }

        if (_escapeQuest != null)
        {
            QuestManager.Instance.AddQuest(_escapeQuest);
            Debug.Log($"[ArrestCutscene] started {_escapeQuest.name}");
        }
    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnTrapLetterUnfolded, OnLetterUnfolded);
            EventManager.Unsubscribe(Evento.OnPageFinishTurning, OnPageFinishTurning);
            EventManager.Unsubscribe(Evento.OnQuestCompleted, OnQuestCompleted);
            StopSiren();
        }
    }
}
