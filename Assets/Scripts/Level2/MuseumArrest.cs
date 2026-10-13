using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 5 (#183): once the evidence is laid out, the cops march Ariel to their parked patrol
/// car and drive him away. Page 3's arrest, the other way round. MuseumPage starts it with Play() and
/// gets a callback when the car is gone, then closes the case.
///
/// Timeline_MuseumArrest drives the beats with signals (CUE_ methods), the gaps between them are drag
/// work in the Timeline window:
///   StartEscort -> Ariel walks the NavMesh to the car door, the cops trail behind him.
///   BoardCar    -> holds the timeline until Ariel is at the door, then all three get in (they vanish).
///   DriveOff    -> the car leaves with the siren on (TrafficObstacle.Launch, like page 3).
///   Finish      -> holds the timeline until the car has arrived, then hands back to MuseumPage.
///
/// Everyone walks on a NavMeshAgent, so they stay on the ground over the yard's slope (sliding them
/// along the route's corners floated them, Diego 2026-10-13). Ariel's prefab has no agent and page 3
/// uses the same prefab, so his is added here, sized by the Inspector. The cops use their own
/// (CopEscort NPCs); each holds back once it gets within _copsKeepBack of the one in front, which
/// keeps them in a line behind him. All three are warped onto the NavMesh first: their agents woke up
/// before PageNavMeshManager added the page's NavMesh, and an agent that misses it stays detached
/// until it is warped. Each is lifted by the gap it stands with, so nobody pops up or down.
/// </summary>
[RequireComponent(typeof(CutsceneDirector))]
public class MuseumArrest : MonoBehaviour
{
    [Header("People")]
    [SerializeField, Tooltip("Ariel: he walks in front, from wherever he stands.")]
    Transform _ariel;

    [SerializeField, Tooltip("The cops who escort him, in the order they follow (first one right behind him).")]
    Transform[] _cops;

    [SerializeField, Tooltip("Walking speed of the escort, units per second. The cops get a little more so they keep up.")]
    float _walkSpeed = 7f;

    [SerializeField, Tooltip("Each cop stops walking while the one in front of him (Ariel for the first) is closer than this, in world units.")]
    float _copsKeepBack = 4.5f;

    [SerializeField, Tooltip("Ariel's mood while he is marched off: the pose he goes back to whenever he stops. Empty = plain idle.")]
    string _arielMood = "Angry";

    [SerializeField, Tooltip("Radius of the NavMeshAgent added to Ariel for the walk, in world units (the cops keep their own).")]
    float _arielAgentRadius = 1f;

    [SerializeField, Tooltip("Height of the NavMeshAgent added to Ariel for the walk, in world units.")]
    float _arielAgentHeight = 7f;

    [Header("Car")]
    [SerializeField, Tooltip("The parked patrol car (the ambient-traffic prefab). Its own Animators are switched off so it stays parked.")]
    TrafficObstacle _patrolCar;

    [SerializeField, Tooltip("Where Ariel gets in. Snapped to the NavMesh; drag it next to the car's door.")]
    Transform _carDoorMark;

    [SerializeField, Tooltip("Where the car drives to (off the page). The car never turns, so keep it ahead of its nose.")]
    Transform _driveOffTarget;

    [SerializeField, Tooltip("Seconds the drive takes to reach the drive-off mark.")]
    float _driveOffSeconds = 3f;

    [SerializeField, Tooltip("Ariel counts as at the door within this distance (horizontal).")]
    float _arriveDistance = 1.5f;

    [SerializeField, Tooltip("Safety net: board anyway after this many seconds of holding at BoardCar (is the door reachable?).")]
    float _maxWaitForArrivalSeconds = 40f;

    const float RepathSeconds = 0.25f;

    CutsceneDirector _cutscene;
    Action _onFinished;
    bool _started;
    bool _escorting;
    bool _carDrivingOff;

    NavMeshAgent _arielAgent;
    NavMeshAgent[] _copAgents = new NavMeshAgent[0];
    bool[] _copHolding = new bool[0];
    Vector3 _door;
    float _nextRepath;

    void Awake()
    {
        _cutscene = GetComponent<CutsceneDirector>();
    }

    void Start()
    {
        ParkCar();
    }

    /// <summary>MuseumPage: run the arrest; <paramref name="onFinished"/> is called once the car has left.</summary>
    public void Play(Action onFinished)
    {
        if (_started)
        {
            Debug.LogWarning("[MuseumArrest] already played, ignoring");
            return;
        }

        _started = true;
        _onFinished = onFinished;
        Debug.Log("[MuseumArrest] the cops take Ariel to the patrol car");
        _cutscene.Play();
    }

    //the ambient-traffic car's "Andando" Animator slides its body down the street by itself: off for
    //good, so it rests parked and only Launch moves it (same as page 3's ArrestCutscene.ParkCar)
    void ParkCar()
    {
        if (_patrolCar == null)
        {
            Debug.LogWarning("[MuseumArrest] no _patrolCar assigned: Ariel will vanish with no car to leave in");
            return;
        }

        foreach (Animator animator in _patrolCar.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }
    }

    // ---------------------------------------------------------------- timeline signals

    /// <summary>Signal: Ariel starts walking to the car, the cops behind him.</summary>
    public void CUE_StartEscort()
    {
        if (_ariel == null || _carDoorMark == null)
        {
            Debug.LogWarning("[MuseumArrest] no _ariel or _carDoorMark: nobody walks, they just board at BoardCar");
            return;
        }

        if (!SampleGround(_carDoorMark.position, out _door))
        {
            Debug.LogWarning("[MuseumArrest] the car door mark is not near the NavMesh (page 5 baked?): they board where they stand");
            return;
        }

        //the evidence dialogue left him in his Angry pose, which doesn't end when he moves: it would
        //cover the walk the whole way. Ended here, and kept as his mood for when he stops at the car
        SpineCharacter arielCharacter = _ariel.GetComponent<SpineCharacter>();
        if (arielCharacter != null)
        {
            arielCharacter.EndPose();
            if (!string.IsNullOrEmpty(_arielMood))
            {
                arielCharacter.SetRestPose(_arielMood);
            }
        }

        _arielAgent = PutOnAgent(_ariel, _walkSpeed, 30); //he leads: the cops (50) give way, never the other way round
        if (_arielAgent == null)
        {
            return;
        }

        _copAgents = new NavMeshAgent[_cops.Length];
        _copHolding = new bool[_cops.Length];
        for (int i = 0; i < _cops.Length; i++)
        {
            Transform cop = _cops[i];
            if (cop == null || !cop.gameObject.activeInHierarchy)
            {
                continue;
            }

            NavMeshAgent agent = PutOnAgent(cop, _walkSpeed * 1.25f, 50);
            if (agent == null)
            {
                continue; //he stays put and boards at the end
            }

            _copAgents[i] = agent;
        }

        _arielAgent.SetDestination(_door);
        _nextRepath = 0f;
        _escorting = true;
        Debug.Log($"[MuseumArrest] escort starts: Ariel walks to the car door, {_cops.Length} cops behind him");
    }

    /// <summary>Signal: everyone gets in. Holds the timeline until Ariel is at the door.</summary>
    public void CUE_BoardCar()
    {
        StartCoroutine(BoardWhenArielArrives());
    }

    /// <summary>Signal: the car drives off with the siren on.</summary>
    public void CUE_DriveOff()
    {
        if (_patrolCar == null || _driveOffTarget == null)
        {
            Debug.LogWarning("[MuseumArrest] no patrol car or drive-off target, the car stays put");
            return;
        }

        //the lifetime is only a safety net; the car despawns itself on arrival
        _patrolCar.Launch(_driveOffTarget.position, _driveOffSeconds, (_driveOffSeconds * 3f) + 1f);
        _carDrivingOff = true;
        PlaySound(AudioId.PoliceSirenLoop);
        PlaySound(AudioId.CarEngine);
        PlaySound(AudioId.PoliceHorn);
        Debug.Log("[MuseumArrest] the patrol car drives off");
    }

    /// <summary>Signal: the end. Holds the timeline until the car has left, then MuseumPage closes the case.</summary>
    public void CUE_Finish()
    {
        StartCoroutine(FinishWhenCarArrives());
    }

    // ---------------------------------------------------------------- the walk

    //warps the person onto the NavMesh under them, lifted by the gap between their pivot and that ground
    //so they walk exactly as high as they stood. Ariel's prefab has no agent (page 3 shares it): he gets
    //one here, sized by the Inspector; the cops keep their prefab's size. Null = they stay put.
    NavMeshAgent PutOnAgent(Transform person, float speed, int avoidancePriority)
    {
        if (!SampleGround(person.position, out Vector3 ground))
        {
            Debug.LogWarning($"[MuseumArrest] {person.name} is not near the NavMesh (page 5 baked?): stays put and boards at the end");
            return null;
        }

        //agent sizes are multiplied by the transform's scale: author them in world units
        float scale = Mathf.Max(0.0001f, person.lossyScale.y);
        NavMeshAgent agent = person.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = person.gameObject.AddComponent<NavMeshAgent>();
            agent.radius = _arielAgentRadius / scale;
            agent.height = _arielAgentHeight / scale;
        }

        agent.enabled = false;
        agent.baseOffset = (person.position.y - ground.y) / scale;
        agent.speed = speed;
        agent.acceleration = 40f;
        agent.stoppingDistance = 0.3f;
        agent.updateRotation = false; //SpineCharacter flips the art, the transform never turns
        agent.avoidancePriority = avoidancePriority;
        agent.enabled = true;

        if (!agent.Warp(ground))
        {
            Debug.LogWarning($"[MuseumArrest] {person.name}'s agent could not be placed on the NavMesh: stays put and boards at the end");
            agent.enabled = false;
            return null;
        }

        agent.isStopped = false;
        return agent;
    }

    void Update()
    {
        if (!_escorting)
        {
            return;
        }

        bool repath = Time.time >= _nextRepath;
        if (repath)
        {
            _nextRepath = Time.time + RepathSeconds;
        }

        Transform ahead = _ariel;
        for (int i = 0; i < _copAgents.Length; i++)
        {
            NavMeshAgent cop = _copAgents[i];
            if (cop == null)
            {
                continue;
            }

            float gap = HorizontalDistance(cop.transform.position, ahead.position);
            //hysteresis, otherwise a cop right at the limit flickers between walking and stopped every frame
            _copHolding[i] = _copHolding[i] ? gap < _copsKeepBack + 1f : gap < _copsKeepBack;
            cop.isStopped = _copHolding[i];

            if (!_copHolding[i] && repath)
            {
                cop.SetDestination(_door);
            }

            ahead = cop.transform;
        }
    }

    bool ArielHasArrived()
    {
        if (!_escorting || _arielAgent == null)
        {
            return true;
        }

        return HorizontalDistance(_ariel.position, _door) <= _arriveDistance
               || (!_arielAgent.pathPending && _arielAgent.remainingDistance <= _arielAgent.stoppingDistance + 0.05f);
    }

    IEnumerator BoardWhenArielArrives()
    {
        if (!ArielHasArrived())
        {
            Debug.Log("[MuseumArrest] Ariel hasn't reached the car yet, holding the timeline");
            _cutscene.HoldTimeline();

            float waited = 0f;
            while (!ArielHasArrived() && waited < _maxWaitForArrivalSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!ArielHasArrived())
            {
                Debug.LogWarning($"[MuseumArrest] Ariel did not reach the car in {_maxWaitForArrivalSeconds}s, boarding anyway");
            }

            _cutscene.ResumeTimeline();
        }

        _escorting = false;

        if (_ariel != null)
        {
            _ariel.gameObject.SetActive(false);
        }

        foreach (Transform cop in _cops)
        {
            if (cop != null)
            {
                cop.gameObject.SetActive(false);
            }
        }

        PlaySound(AudioId.CarDoor);
        Debug.Log("[MuseumArrest] Ariel and the cops get into the patrol car");
    }

    IEnumerator FinishWhenCarArrives()
    {
        //TrafficObstacle destroys itself on arrival, which is how we know it left the page
        if (_carDrivingOff && _patrolCar != null)
        {
            Debug.Log("[MuseumArrest] the car is still driving, holding the timeline until it reaches the mark");
            _cutscene.HoldTimeline();

            float waited = 0f;
            float maxWait = (_driveOffSeconds * 3f) + 1f; //the same safety net the car itself despawns with
            while (_patrolCar != null && waited < maxWait)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            _cutscene.ResumeTimeline();
        }

        StopSiren();
        Debug.Log("[MuseumArrest] Ariel is gone, back to MuseumPage");

        Action finished = _onFinished;
        _onFinished = null;
        finished?.Invoke();
    }

    static bool SampleGround(Vector3 position, out Vector3 ground)
    {
        //generous radius: the people's pivots sit several units above their feet
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

    // ---------------------------------------------------------------- sound

    static void PlaySound(string id)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[MuseumArrest] no AudioManager, '{id}' skipped");
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

    void OnDisable()
    {
        //only a test or a bug switches page 5 off mid-arrest: never leave the siren looping
        StopSiren();
    }
}
