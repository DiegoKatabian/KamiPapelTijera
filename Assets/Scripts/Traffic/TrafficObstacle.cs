using UnityEngine;

// Moves a spawned obstacle (car, and eventually a pedestrian once that art exists)
// in a straight line from its spawn point to a target point, then despawns itself.
// Deliberately simpler than BarquitoBehaviour/Pathfinding's node-graph steering:
// this is ambient decoration on a fixed street segment, not a navigated NPC.
//
// Also a moving platform (IMovingGround): whoever stands on its solid collider is carried along.
[DisallowMultipleComponent]
public class TrafficObstacle : MonoBehaviour, IMovingGround
{
    [Tooltip("Rotates to face the movement direction when launched. Turn off for art that's already oriented correctly (e.g. a sprite baked facing one way).")]
    [SerializeField] private bool faceMovementDirection = true;

    [Header("Roof ride")]
    [Tooltip("Seconds before the despawn point at which the obstacle stops carrying anyone, so it slides out from under a rider instead of vanishing with them on top. Seconds, not units: it reads the same on any street length.")]
    [SerializeField] private float releaseSecondsBeforeDespawn = 0.75f;

    [Tooltip("The transform of the solid collider that carries riders. Empty = the first non-trigger collider found under this object. It is NOT this root: the car art has its own looping Animator that slides the collider relative to the root, so the real velocity is measured on the collider itself.")]
    [SerializeField] private Transform motionSource;

    // Below this the measured motion is just jitter and the root's own heading is used instead.
    private const float MIN_MEASURED_SPEED = 0.5f;

    private Vector3 _targetPosition;
    private float _speed;
    private float _maxLifetime;
    private float _aliveTime;
    private bool _isMoving;

    // Cached so IMovingGround/TravelDirection never touch `transform`: a rider may ask for the
    // velocity one more time after this object was Destroy()ed (see IMovingGround).
    private float _remainingDistance;
    private Vector3 _rootHeading;
    private Vector3 _measuredVelocity;
    private Vector3 _lastSourcePosition;
    private bool _hasLastSourcePosition;

    // World-space XZ direction the car is actually travelling. Measured from the solid collider
    // (the art's own animation adds to the root's motion), falling back to the root's heading.
    // Never zeroed by the roof-ride release: the hitbox's frontal test needs it until despawn.
    public Vector3 TravelDirection
    {
        get
        {
            Vector3 measured = _measuredVelocity;
            if (measured.sqrMagnitude >= MIN_MEASURED_SPEED * MIN_MEASURED_SPEED)
            {
                return measured.normalized;
            }

            return _rootHeading;
        }
    }

    // What a rider must add to its own move: the real velocity of the roof, XZ only, and zero when
    // the obstacle is not moving or is inside its release window.
    public Vector3 GroundVelocity
    {
        get
        {
            if (!_isMoving || _speed <= 0f || _remainingDistance / _speed < releaseSecondsBeforeDespawn)
            {
                return Vector3.zero;
            }

            return _measuredVelocity;
        }
    }

    private void Awake()
    {
        if (motionSource != null)
        {
            return;
        }

        foreach (Collider candidate in GetComponentsInChildren<Collider>(true))
        {
            if (!candidate.isTrigger)
            {
                motionSource = candidate.transform;
                break;
            }
        }

        if (motionSource == null)
        {
            Debug.LogWarning($"[TrafficObstacle] {name}: no solid collider found, the roof ride will follow the root transform instead.");
            motionSource = transform;
        }
    }

    // Called by TrafficSpawner right after Instantiate — the obstacle itself doesn't
    // know its path, the spawner owns spawn/despawn points per street segment.
    public void Launch(Vector3 targetPosition, float travelDuration, float maxLifetime)
    {
        _targetPosition = targetPosition;
        _maxLifetime = maxLifetime;
        _aliveTime = 0f;

        float distance = Vector3.Distance(transform.position, targetPosition);
        _speed = distance / Mathf.Max(0.01f, travelDuration);
        _remainingDistance = distance;
        _isMoving = true;

        Vector3 heading = _targetPosition - transform.position;
        heading.y = 0f;
        _rootHeading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.zero;

        if (faceMovementDirection && _rootHeading != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_rootHeading);
        }

        //Debug.Log($"[TrafficObstacle] {name} launched: {distance:F1} units in {travelDuration:F1}s ({_speed:F1} u/s).");d
    }

    private void Update()
    {
        if (!_isMoving)
        {
            return;
        }

        _aliveTime += Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _speed * Time.deltaTime);
        _remainingDistance = Vector3.Distance(transform.position, _targetPosition);

        // MoveTowards clamps exactly onto the target, so an exact match is the normal arrival.
        if (transform.position == _targetPosition)
        {
            Despawn("reached its despawn point");
            return;
        }

        // Safety net: nothing should outlive this, whatever the tuning or whatever else
        // moved the transform. Without it a mistuned duration silently piles obstacles up.
        if (_aliveTime >= _maxLifetime)
        {
            Despawn($"hit its {_maxLifetime:F0}s max lifetime {Vector3.Distance(transform.position, _targetPosition):F1} units short of the despawn point");
        }
    }

    // LateUpdate, not Update: the art's Animator moves the collider after every Update has run, so
    // this is the first point where the frame's final position is known. It is a measured VELOCITY
    // handed out as a cached value, which keeps the rider independent of Update order (a position
    // delta read at the rider's own time would depend on who ran first).
    private void LateUpdate()
    {
        if (!_isMoving || motionSource == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 position = motionSource.position;
        if (_hasLastSourcePosition)
        {
            Vector3 delta = position - _lastSourcePosition;
            delta.y = 0f;
            _measuredVelocity = delta / Time.deltaTime;
        }

        _lastSourcePosition = position;
        _hasLastSourcePosition = true;
    }

    private void Despawn(string reason)
    {
        _isMoving = false;
        //Debug.Log($"[TrafficObstacle] {name} {reason}, destroying.");
        Destroy(gameObject);
    }
}
