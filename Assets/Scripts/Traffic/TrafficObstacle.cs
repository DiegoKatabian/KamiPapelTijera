using UnityEngine;

// Moves a spawned obstacle (car, and eventually a pedestrian once that art exists)
// in a straight line from its spawn point to a target point, then despawns itself.
// Deliberately simpler than BarquitoBehaviour/Pathfinding's node-graph steering:
// this is ambient decoration on a fixed street segment, not a navigated NPC.
[DisallowMultipleComponent]
public class TrafficObstacle : MonoBehaviour
{
    [Tooltip("Rotates to face the movement direction when launched. Turn off for art that's already oriented correctly (e.g. a sprite baked facing one way).")]
    [SerializeField] private bool faceMovementDirection = true;

    private Vector3 _targetPosition;
    private float _speed;
    private float _maxLifetime;
    private float _aliveTime;
    private bool _isMoving;

    // Called by TrafficSpawner right after Instantiate — the obstacle itself doesn't
    // know its path, the spawner owns spawn/despawn points per street segment.
    public void Launch(Vector3 targetPosition, float travelDuration, float maxLifetime)
    {
        _targetPosition = targetPosition;
        _maxLifetime = maxLifetime;
        _aliveTime = 0f;

        float distance = Vector3.Distance(transform.position, targetPosition);
        _speed = distance / Mathf.Max(0.01f, travelDuration);
        _isMoving = true;

        if (faceMovementDirection)
        {
            Vector3 direction = _targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        Debug.Log($"[TrafficObstacle] {name} launched: {distance:F1} units in {travelDuration:F1}s ({_speed:F1} u/s).");
    }

    private void Update()
    {
        if (!_isMoving)
        {
            return;
        }

        _aliveTime += Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _speed * Time.deltaTime);

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

    private void Despawn(string reason)
    {
        _isMoving = false;
        Debug.Log($"[TrafficObstacle] {name} {reason}, destroying.");
        Destroy(gameObject);
    }
}
