using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 2 (spec 006, Story 2): the museum's broken fence, taped off and guarded.
///
/// While guarded, the police tape cannot be cut (its trigger collider is off, so the scissors
/// never register it) and a solid blocker fills the gap. On Evento.OnCrimeSceneUnguarded
/// (fired by FindCluesTracker, once the clue comment before it has been read) the cops leave in a
/// short cutscene (#231): Kami is locked, the siren sounds, the camera goes to the patrol car, the
/// cops walk to its door and get in, the car drives off, the camera comes back to Kami. Only then is
/// she free, and FindCluesTracker plays Natalia's "they left" line. The tape becomes cuttable once
/// the car pulls away; cutting it removes the blocker.
///
/// The tape is found by GetComponentInChildren instead of a serialized reference, so this
/// prefab can nest a plain CuttablePoliceTape instance without stripped-reference plumbing.
/// </summary>
public class CrimeSceneGate : MonoBehaviour
{
    [SerializeField, Tooltip("Solid collider filling the fence gap. Turned off when the tape is cut.")]
    GameObject _blocker;

    [SerializeField, Tooltip("The police car. It drives to _driveOffTarget and is destroyed there.")]
    TrafficObstacle _policeCar;

    [SerializeField, Tooltip("Where the car drives to before disappearing.")]
    Transform _driveOffTarget;

    [SerializeField, Tooltip("Seconds the car takes to reach _driveOffTarget (a duration, not a speed, so it reads the same on any street).")]
    float _driveOffSeconds = 3f;

    [SerializeField, Tooltip("Seconds between the last cop getting in and the car pulling away.")]
    float _boardingSeconds = 0.5f;

    [SerializeField, Tooltip("The cops standing guard (they walk to the car and get in) and the guard talk trigger (switched off when they start to leave).")]
    GameObject[] _cops;

    [Header("Leaving cutscene (#231)")]
    [SerializeField, Tooltip("The shot that watches the cops leave: follows the patrol car (set in code). Framing = its Transposer Follow Offset + its X tilt. Empty = the camera stays on Kami.")]
    CinemachineVirtualCamera _leaveShot;

    [SerializeField, Tooltip("Where the cops get in: next to the car's door, snapped to the NavMesh.")]
    Transform _carDoorMark;

    [SerializeField, Tooltip("The cops' walking speed to the car, units per second.")]
    float _copWalkSpeed = 7f;

    [SerializeField, Tooltip("A cop counts as at the door within this distance (horizontal), and gets in.")]
    float _arriveDistance = 2f;

    [SerializeField, Tooltip("Safety net: whoever has not reached the door after this many seconds gets in anyway.")]
    float _maxWalkSeconds = 10f;

    [SerializeField, Tooltip("Of the camera's blend to the car, how much plays before the cops start walking (0 = they walk at once, 1 = once the camera has arrived).")]
    [Range(0f, 1f)]
    float _walkAfterBlendFraction = 0.6f;

    CuttableTwoPieces _tape;
    Collider _tapeCollider;
    bool _unguarded;
    bool _carLaunched;
    bool _lockedKami;
    bool _sequenceRunning;

    void Awake()
    {
        _tape = GetComponentInChildren<CuttableTwoPieces>(true);

        if (_tape == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: no CuttableTwoPieces (police tape) found in children, the gate can never open.");
            return;
        }

        _tapeCollider = _tape.GetComponent<Collider>();
        if (_tapeCollider == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: the police tape has no Collider, it cannot be gated.");
        }
        else
        {
            _tapeCollider.enabled = false;
        }

        _tape.OnCut.AddListener(OpenGap);
    }

    void Start()
    {
        if (_blocker == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: no _blocker assigned, nothing physically closes the fence gap.");
        }

        EventManager.Subscribe(Evento.OnCrimeSceneUnguarded, Unguard);
    }

    void Unguard(params object[] parameters)
    {
        if (_unguarded)
        {
            return;
        }

        _unguarded = true;
        Debug.Log($"[CrimeSceneGate] {gameObject.name}: the cops leave, the tape can be cut once they drive off");

        //page 2 switched off (only a test does that here): OnEnable finishes it without the cutscene
        if (!isActiveAndEnabled)
        {
            return;
        }

        //the lock is taken here, synchronously, so FindCluesTracker holds Natalia's "they left" line
        //from this very frame (it waits while inCutscene is on)
        SetKamiLocked(true);
        _sequenceRunning = true;
        StartCoroutine(LeaveSequence());
    }

    //turning the page deactivates it and kills a running coroutine: finish leaving on return
    void OnEnable()
    {
        if (_unguarded && !_carLaunched)
        {
            StartCoroutine(LeaveQuietly());
        }
    }

    void OnDisable()
    {
        if (!_sequenceRunning)
        {
            return;
        }

        //Kami is locked during the cutscene, so only a test or a bug switches the page off mid-way:
        //never leave the siren looping, the shot live or Kami frozen
        _sequenceRunning = false;
        StopSiren();
        CutsceneCamera.Hide(_leaveShot);
        SetKamiLocked(false);
    }

    IEnumerator LeaveSequence()
    {
        PlaySound(AudioId.PoliceSirenLoop);
        SwitchOffGuardTalk();

        if (_leaveShot != null && _policeCar != null)
        {
            _leaveShot.Follow = _policeCar.transform;
        }
        CutsceneCamera.Show(_leaveShot);

        //the player should see them start walking, not arrive at a scene already in motion
        if (_leaveShot != null)
        {
            yield return new WaitForSeconds(CutsceneCamera.BlendSeconds * _walkAfterBlendFraction);
        }

        yield return WalkCopsToTheCar();

        yield return new WaitForSeconds(_boardingSeconds);

        LaunchCar();

        //TrafficObstacle destroys itself on arrival, which is how we know it left the page
        float waited = 0f;
        float maxWait = (_driveOffSeconds * 3f) + 1f; //the same safety net the car itself despawns with
        while (_policeCar != null && waited < maxWait)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        StopSiren();
        CutsceneCamera.Hide(_leaveShot);
        if (_leaveShot != null)
        {
            yield return new WaitForSeconds(CutsceneCamera.BlendSeconds);
        }

        _sequenceRunning = false;
        SetKamiLocked(false);
        Debug.Log($"[CrimeSceneGate] {gameObject.name}: the cops are gone, Kami is free");
    }

    //the old instant version, for a page that was switched off before the car left: nobody watches
    IEnumerator LeaveQuietly()
    {
        SwitchOffGuardTalk();
        foreach (GameObject cop in _cops)
        {
            if (cop != null)
            {
                cop.SetActive(false);
            }
        }

        yield return new WaitForSeconds(_boardingSeconds);

        LaunchCar();
    }

    // ---------------------------------------------------------------- the walk

    IEnumerator WalkCopsToTheCar()
    {
        List<NPC> walkers = new List<NPC>();
        foreach (GameObject cop in _cops)
        {
            NPC npc = cop != null ? cop.GetComponent<NPC>() : null;
            if (npc != null && cop.activeInHierarchy)
            {
                walkers.Add(npc);
            }
        }

        Vector3 door = Vector3.zero;
        bool canWalk = _carDoorMark != null && SampleGround(_carDoorMark.position, out door);
        if (!canWalk)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: no _carDoorMark, or it is not near the NavMesh (page 2 baked?): the cops get in where they stand");
        }

        if (canWalk)
        {
            foreach (NPC cop in walkers)
            {
                //a page's NPCs wake up before PageNavMeshManager adds the page's NavMesh, and an agent
                //that missed it stays detached until it is warped (same trap as MuseumArrest)
                if (SampleGround(cop.transform.position, out Vector3 ground))
                {
                    cop.WarpTo(ground);
                }

                if (cop.navAgent != null)
                {
                    cop.navAgent.speed = _copWalkSpeed;
                }
            }
        }

        float waited = 0f;
        while (canWalk && walkers.Count > 0 && waited < _maxWalkSeconds)
        {
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                NPC cop = walkers[i];
                if (HorizontalDistance(cop.transform.position, door) <= _arriveDistance)
                {
                    GetIn(cop);
                    walkers.RemoveAt(i);
                    continue;
                }

                cop.MoveTowards(door);
                cop.UpdateSpriteFlip();
            }

            waited += Time.deltaTime;
            yield return null;
        }

        if (canWalk && walkers.Count > 0)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: {walkers.Count} cop(s) did not reach the car door in {_maxWalkSeconds}s, they get in anyway");
        }

        foreach (NPC cop in walkers)
        {
            GetIn(cop);
        }
    }

    void GetIn(NPC cop)
    {
        cop.StopAgent();
        cop.gameObject.SetActive(false);
        PlaySound(AudioId.CarDoor);
        Debug.Log($"[CrimeSceneGate] {cop.name} gets into the patrol car");
    }

    //the guard talk trigger lives in _cops: the guards are leaving, nobody to talk to
    void SwitchOffGuardTalk()
    {
        foreach (GameObject cop in _cops)
        {
            if (cop != null && cop.GetComponent<NPC>() == null)
            {
                cop.SetActive(false);
            }
        }
    }

    void LaunchCar()
    {
        //the tape opens once the guards are gone, not when the car is out of sight
        if (_tapeCollider != null)
        {
            _tapeCollider.enabled = true;
        }

        _carLaunched = true;

        if (_policeCar != null && _driveOffTarget != null)
        {
            //the lifetime cap is only a safety net for the despawn, give it plenty of room
            _policeCar.Launch(_driveOffTarget.position, _driveOffSeconds, _driveOffSeconds * 3f + 1f);
            PlaySound(AudioId.CarEngine);
            PlaySound(AudioId.PoliceHorn);
        }
        else
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: _policeCar or _driveOffTarget missing, the car stays parked.");
        }
    }

    static bool SampleGround(Vector3 position, out Vector3 ground)
    {
        //generous radius: the cops' pivots sit several units above their feet
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            ground = hit.position;
            return true;
        }

        ground = position;
        return false;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------- lock and sound

    //only ever releases a lock this component took, so it can't cut another cutscene short
    void SetKamiLocked(bool locked)
    {
        if (LevelManager.Instance == null || (!locked && !_lockedKami))
        {
            return;
        }

        _lockedKami = locked;
        LevelManager.Instance.inCutscene = locked;
    }

    static void PlaySound(string id)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] no AudioManager, '{id}' skipped");
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

    void OpenGap()
    {
        Debug.Log($"[CrimeSceneGate] {gameObject.name}: tape cut, the fence gap is open");

        if (_blocker != null)
        {
            _blocker.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (_tape != null)
        {
            _tape.OnCut.RemoveListener(OpenGap);
        }

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnCrimeSceneUnguarded, Unguard);
        }
    }
}
