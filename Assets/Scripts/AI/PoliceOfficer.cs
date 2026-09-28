using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 4's patrolling cop: the implementation of specs/002-enemigo-sigilo (spec 006 FR-007).
/// Walks PatrollingAgent's waypoints, sees Kami with a vision cone + line of sight, and catches her
/// (Player.Die with DeathCause.Caught) if she stays in view long enough.
///
/// Being seen fills an awareness meter instead of catching on sight: _secondsToCatch of plain view
/// catches her, and the meter drains while she is hidden, so a quick dash across the cone is
/// survivable. The first glimpse puts the officer on alert: he stops and stares while he can see
/// her, walks to where he last saw her when he can't, and gives up after _secondsToGiveUp.
///
/// The alert rides on PatrollingAgent's evade hooks on purpose: they already hand the agent over
/// to the subclass every frame (ShouldEvade/UpdateEvadeDestination), and when they let go the base
/// resumes the waypoint he was walking to -- exactly spec 002's "resume the route" (FR-004). So in
/// the base class's debug logs, "Evading" means "alerted" for this agent.
///
/// Only Kami can be seen. Natalia and the Abuela are never spotted (Diego, 2026-09-27).
/// </summary>
public class PoliceOfficer : PatrollingAgent
{
    [Header("Vision")]
    [SerializeField, Tooltip("Full width of the vision cone, in degrees.")]
    float _visionAngle = 45f;

    [SerializeField, Tooltip("How far the officer sees, in world units.")]
    float _visionRange = 10f;

    [SerializeField, Tooltip("Height of the officer's eyes above his feet, in world units. Line of sight is cast from here to Kami's body, so anything taller than her between them (a desk, a wall, a fence) hides her.")]
    float _eyeHeight = 6f;

    [SerializeField, Tooltip("Kami is only seen when her height differs from the officer's feet by less than this, in world units. Keeps the 2nd floor out of his sight.")]
    float _maxHeightDifference = 6f;

    [SerializeField, Tooltip("Layers that block line of sight: walls, desks, cell fences.")]
    LayerMask _sightBlockers = 1;

    [SerializeField, Tooltip("Direction the officer faces before he first moves, in degrees around the vertical axis (0 = +Z, 90 = +X).")]
    float _startFacingDegrees = 90f;

    [Header("Catching (seconds)")]
    [SerializeField, Tooltip("Seconds Kami has to stay in plain view to get caught. Higher = more forgiving.")]
    float _secondsToCatch = 1.5f;

    [SerializeField, Tooltip("Seconds a full awareness meter takes to drain back to zero once Kami is out of view.")]
    float _secondsToCalmDown = 2f;

    [SerializeField, Tooltip("Seconds without seeing Kami before the officer gives up and goes back to his route.")]
    float _secondsToGiveUp = 3f;

    [SerializeField, Tooltip("Seconds the officer stands still at each waypoint before walking to the next one.")]
    float _pauseAtWaypoint = 1f;

    [Header("Movement")]
    [SerializeField, Tooltip("Walking speed while alerted, as a multiple of the NavMeshAgent's speed (his patrol speed).")]
    float _alertSpeedMultiplier = 1.25f;

    [SerializeField, Tooltip("How fast the officer turns (and his cone with him), in degrees per second.")]
    float _turnDegreesPerSecond = 270f;

    [Header("Feedback")]
    [SerializeField, Tooltip("Sound played when he first notices Kami. Leave empty for none.")]
    string _alertSound = AudioId.MagicFail;

    [SerializeField, Tooltip("The officer's sprite, flipped to face where he walks.")]
    SpriteRenderer _sprite;

    [SerializeField, Tooltip("Turn on if the art is drawn facing the opposite way (same knob as NPC._invertFlip).")]
    bool _invertFlip;

    Player _kami;
    CharacterController _kamiBody;
    Vector3 _facing;
    Vector3 _startPosition;
    Vector3 _startFacing;
    Transform[] _startWaypoints;
    float _patrolSpeed;
    bool _detectionEnabled = true;
    bool _alerted;
    bool _canSeeKami;
    Vector3 _lastSeenPosition;
    float _lastSeenTime;
    bool _pausing;
    float _pauseUntil;
    float _nextRepathTime;

    /// <summary>0 = hasn't noticed anything, 1 = caught.</summary>
    public float Awareness { get; private set; }
    public bool IsAlerted => _alerted;
    public Vector3 Facing => _facing;
    public float VisionAngle => _visionAngle;
    public float VisionRange => _visionRange;
    public LayerMask SightBlockers => _sightBlockers;

    /// <summary>Where his feet touch the NavMesh (the transform sits higher, at the sprite's pivot).</summary>
    public Vector3 FeetPosition => transform.position - Vector3.up * (navAgent != null ? navAgent.baseOffset * transform.lossyScale.y : 0f);

    protected override void Awake()
    {
        base.Awake();

        _startPosition = transform.position;
        _startFacing = Quaternion.Euler(0f, _startFacingDegrees, 0f) * Vector3.forward;
        _facing = _startFacing;
        _startWaypoints = waypoints.ToArray();
        _patrolSpeed = navAgent != null ? navAgent.speed : 0f;

        if (navAgent != null)
        {
            navAgent.updateRotation = false; //the cone turns in code; the sprite only flips
        }
    }

    protected override void Start()
    {
        base.Start();

        _kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (_kami == null)
        {
            Debug.LogWarning($"[PoliceOfficer] {gameObject.name}: no Player in LevelManager, he will never see anyone");
        }
        else
        {
            _kamiBody = _kami.GetComponent<CharacterController>();
        }

        if (_sprite == null)
        {
            _sprite = GetComponentInChildren<SpriteRenderer>();
        }

        //PatrollingAgent.Start never sets a first destination: without this he would skip waypoint 0
        if (_startWaypoints.Length > 0)
        {
            SetWaypoints(_startWaypoints);
        }
        else
        {
            Debug.LogWarning($"[PoliceOfficer] {gameObject.name}: no waypoints, he will stand still");
        }
    }

    protected override void Update()
    {
        UpdateAwareness();
        base.Update();

        if (_pausing && !_alerted && Time.time >= _pauseUntil)
        {
            _pausing = false;
            MoveToNextWaypoint();
        }

        UpdateFacing();
        UpdateSpriteFlip();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Off after the escape: he keeps patrolling but can't catch anyone any more.</summary>
    public void SetDetectionEnabled(bool enabled)
    {
        _detectionEnabled = enabled;
        if (!enabled)
        {
            _alerted = false;
            Awareness = 0f;
        }
    }

    /// <summary>Back to where the page started him, calm, walking his route from waypoint 0.</summary>
    public void ResetToStart()
    {
        _alerted = false;
        _canSeeKami = false;
        _pausing = false;
        Awareness = 0f;
        _facing = _startFacing;
        state = AgentState.Patrolling;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(_startPosition);
            navAgent.isStopped = false;
            navAgent.speed = _patrolSpeed;
        }
        else
        {
            transform.position = _startPosition;
        }

        if (_startWaypoints.Length > 0)
        {
            SetWaypoints(_startWaypoints);
        }

        Debug.Log($"[PoliceOfficer] {gameObject.name}: reset to his starting post");
    }

    // ---------------------------------------------------------------- seeing Kami

    void UpdateAwareness()
    {
        _canSeeKami = CanDetectKamiNow() && KamiInCone(out Vector3 kamiPoint) && HasLineOfSight(kamiPoint);

        if (_canSeeKami)
        {
            _lastSeenPosition = _kami.transform.position;
            _lastSeenTime = Time.time;

            if (!_alerted)
            {
                _alerted = true;
                _pausing = false;
                Debug.Log($"[PoliceOfficer] {gameObject.name}: spotted Kami, on alert");
                if (!string.IsNullOrEmpty(_alertSound))
                {
                    AudioManager.instance.Play(_alertSound);
                }
            }

            Awareness = Mathf.Min(1f, Awareness + Time.deltaTime / Mathf.Max(0.01f, _secondsToCatch));
            if (Awareness >= 1f)
            {
                Catch();
            }
            return;
        }

        Awareness = Mathf.Max(0f, Awareness - Time.deltaTime / Mathf.Max(0.01f, _secondsToCalmDown));

        if (_alerted && Time.time - _lastSeenTime >= _secondsToGiveUp)
        {
            _alerted = false;
            Debug.Log($"[PoliceOfficer] {gameObject.name}: lost Kami, back to the route");
        }
    }

    bool CanDetectKamiNow()
    {
        if (!_detectionEnabled || _kami == null)
        {
            return false;
        }

        if (_kami.CurrentState == PlayerState.Dead || _kami.CurrentState == PlayerState.RidingPage)
        {
            return false;
        }

        //dialogues, cutscenes, overlays and page turns all freeze the game for the player: fair play
        LevelManager levelManager = LevelManager.Instance;
        return levelManager != null && !levelManager.inDialogue && !levelManager.inCutscene;
    }

    bool KamiInCone(out Vector3 kamiPoint)
    {
        kamiPoint = _kamiBody != null ? _kamiBody.bounds.center : _kami.transform.position;

        if (Mathf.Abs(kamiPoint.y - FeetPosition.y) > _maxHeightDifference)
        {
            return false;
        }

        Vector3 toKami = kamiPoint - transform.position;
        toKami.y = 0f;

        if (toKami.sqrMagnitude > _visionRange * _visionRange)
        {
            return false;
        }

        return Vector3.Angle(_facing, toKami) <= _visionAngle * 0.5f;
    }

    bool HasLineOfSight(Vector3 kamiPoint)
    {
        Vector3 eye = FeetPosition + Vector3.up * _eyeHeight;
        Vector3 toKami = kamiPoint - eye;
        return !Physics.Raycast(eye, toKami.normalized, toKami.magnitude, _sightBlockers, QueryTriggerInteraction.Ignore);
    }

    void Catch()
    {
        Debug.Log($"[PoliceOfficer] {gameObject.name}: caught Kami");
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
        }
        _kami.Die(DeathCause.Caught);
    }

    // ---------------------------------------------------------------- patrol + alert (PatrollingAgent hooks)

    protected override bool OnWaypointReached()
    {
        if (_pauseAtWaypoint <= 0f)
        {
            return false;
        }

        //we advance ourselves once the pause is over (Update)
        _pausing = true;
        _pauseUntil = Time.time + _pauseAtWaypoint;
        return true;
    }

    protected override bool ShouldEvade()
    {
        return _alerted;
    }

    protected override void OnEvadeStart()
    {
        _pausing = false;
        navAgent.speed = _patrolSpeed * _alertSpeedMultiplier;
    }

    protected override void UpdateEvadeDestination()
    {
        if (!navAgent.enabled || !navAgent.isOnNavMesh)
        {
            return;
        }

        //while he can see her he stops and stares; once she hides he goes to where she was
        navAgent.isStopped = _canSeeKami;
        if (_canSeeKami || Time.time < _nextRepathTime)
        {
            return;
        }

        _nextRepathTime = Time.time + 0.25f;
        if (NavMesh.SamplePosition(_lastSeenPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
    }

    protected override void ResumeCurrentWaypoint()
    {
        if (navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.speed = _patrolSpeed;
        }
        base.ResumeCurrentWaypoint();
    }

    // ---------------------------------------------------------------- facing

    void UpdateFacing()
    {
        Vector3 desired = _facing;

        if (_alerted)
        {
            Vector3 toLastSeen = _lastSeenPosition - transform.position;
            toLastSeen.y = 0f;
            if (toLastSeen.sqrMagnitude > 0.01f)
            {
                desired = toLastSeen.normalized;
            }
        }
        else if (navAgent != null && navAgent.enabled)
        {
            Vector3 velocity = navAgent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.04f)
            {
                desired = velocity.normalized;
            }
        }

        _facing = Vector3.RotateTowards(_facing, desired, _turnDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime, 0f);
    }

    void UpdateSpriteFlip()
    {
        if (_sprite == null || Mathf.Abs(_facing.x) < 0.1f)
        {
            return;
        }

        _sprite.flipX = (_facing.x < 0f) != _invertFlip;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 facing = Application.isPlaying ? _facing : Quaternion.Euler(0f, _startFacingDegrees, 0f) * Vector3.forward;
        NavMeshAgent agent = navAgent != null ? navAgent : GetComponent<NavMeshAgent>();
        Vector3 feet = transform.position - Vector3.up * (agent != null ? agent.baseOffset * transform.lossyScale.y : 0f);

        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -_visionAngle * 0.5f, 0f) * facing * _visionRange;
        Vector3 right = Quaternion.Euler(0f, _visionAngle * 0.5f, 0f) * facing * _visionRange;
        Gizmos.DrawLine(feet, feet + left);
        Gizmos.DrawLine(feet, feet + right);
        Gizmos.DrawLine(feet + left, feet + right);
        Gizmos.DrawLine(feet, feet + Vector3.up * _eyeHeight);
    }
}
