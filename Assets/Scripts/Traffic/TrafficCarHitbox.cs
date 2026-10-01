using UnityEngine;

// The damage side of a car: a trigger box that hurts whatever IImpactReceiver meets the car's FRONT.
// Sides and tail do nothing here, the car's solid collider keeps shoving as it always did.
//
// Lives on a child of the car's SOLID collider object (the "Mesh" under the animated art), not on the
// TrafficObstacle root: the art's own Animator slides the solid collider relative to the root, and
// this box has to follow the collider. Needs a trigger BoxCollider and a kinematic Rigidbody on the
// same GameObject: two triggers only report a hit if one side carries a Rigidbody
// (PoliceOfficerCortable's precedent).
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class TrafficCarHitbox : MonoBehaviour
{
    [Header("Hit")]
    [Tooltip("HP taken per front hit. Kami has 120 HP: at 25 the 5th hit defeats her.")]
    [SerializeField] private float damage = 25f;

    [Tooltip("Death cause if the hit kills. Generic reuses the existing defeat text; a dedicated cause needs an enum value plus UITexts in 3 languages.")]
    [SerializeField] private DeathCause cause = DeathCause.Generic;

    [Tooltip("Only the front part of the box hurts. 0.35 = the front 35% along the direction of travel. The test runs at runtime from the car's real direction, so the same prefab works driven either way.")]
    [Range(0.05f, 1f)] [SerializeField] private float frontFraction = 0.35f;

    [Header("Knockback")]
    [Tooltip("Total sideways displacement of the push, in units. Kami walks 20 u/s, so 8 is about 0.4 s of walking.")]
    [SerializeField] private float pushDistance = 8f;

    [Tooltip("Seconds the push lasts. Its speed starts at 2 x distance / duration and decays to zero.")]
    [SerializeField] private float pushDuration = 0.3f;

    [Tooltip("Seconds during which no further impact counts, so one crash (or two cars in a row) cannot double-hit.")]
    [SerializeField] private float immunitySeconds = 1f;

    [Tooltip("When the victim is this close (units) to the car's axis, 'which side' is ambiguous: she is pushed against her own sideways movement instead (or toward +lateral if she is standing still).")]
    [SerializeField] private float axisDeadZone = 0.5f;

    [Header("Box fit")]
    [Tooltip("Fit the box to the solid collider's mesh bounds when the car spawns, so it always sits on the real car whatever the art's pivot is. Turn off to author the BoxCollider by hand with the gizmo.")]
    [SerializeField] private bool fitToSolidCollider = true;

    [Tooltip("Fraction of the car's height (from the bottom) the box covers. Keep it below the roof/hood line so standing on top never overlaps it.")]
    [Range(0.1f, 1f)] [SerializeField] private float heightFraction = 0.6f;

    [Tooltip("World units the box extends past the car's mesh at each horizontal end and side, so the damage fires BEFORE the solid collider shoves her.")]
    [SerializeField] private float padding = 1f;

    [Header("Feedback")]
    [Tooltip("AudioId played on a front hit. Empty = silent.")]
    [SerializeField] private string hitSound = AudioId.CarHorn;

    private TrafficObstacle _obstacle;
    private BoxCollider _box;

    private void Reset()
    {
        // Authoring convenience: a fresh component starts with the setup it needs.
        GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Awake()
    {
        _obstacle = GetComponentInParent<TrafficObstacle>();
        _box = GetComponent<BoxCollider>();
        Rigidbody body = GetComponent<Rigidbody>();

        if (_obstacle == null)
        {
            Debug.LogWarning($"[TrafficCarHitbox] {name}: no TrafficObstacle above this object, it cannot tell which way the car is going and will never hurt anyone.");
        }

        if (!_box.isTrigger)
        {
            Debug.LogWarning($"[TrafficCarHitbox] {name}: the BoxCollider is not a trigger, it would block Kami instead of hurting her.");
        }

        if (!body.isKinematic)
        {
            Debug.LogWarning($"[TrafficCarHitbox] {name}: the Rigidbody is not kinematic, physics would drag the hitbox off the car.");
        }

        if (fitToSolidCollider)
        {
            FitToSolidCollider();
        }
    }

    // Works in the mesh's own local space: this object sits at the identity under the solid collider's
    // transform, so the mesh bounds ARE the box, no matter where the art's pivot is or how it is scaled.
    private void FitToSolidCollider()
    {
        MeshCollider solid = GetComponentInParent<MeshCollider>();
        if (solid == null || solid.sharedMesh == null)
        {
            Debug.LogWarning($"[TrafficCarHitbox] {name}: no MeshCollider with a mesh above this object, keeping the authored box.");
            return;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        Bounds mesh = solid.sharedMesh.bounds;
        Vector3 lossy = solid.transform.lossyScale;
        Vector3 size = mesh.size;
        size.x += 2f * padding / Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
        size.z += 2f * padding / Mathf.Max(0.0001f, Mathf.Abs(lossy.z));
        size.y = mesh.size.y * heightFraction;

        Vector3 center = mesh.center;
        center.y = mesh.min.y + size.y * 0.5f;

        _box.size = size;
        _box.center = center;
        Debug.Log($"[TrafficCarHitbox] {name}: fitted to the car mesh, box {size} at {center} (mesh-local units).");
    }

    private void OnTriggerEnter(Collider other)
    {
        IImpactReceiver receiver = other.GetComponentInParent<IImpactReceiver>();
        if (receiver == null || _obstacle == null)
        {
            return;
        }

        // Standing on (or landing on) the roof is never a hit: her feet are at or above the box top.
        if (other.bounds.min.y >= _box.bounds.max.y)
        {
            Debug.Log($"[TrafficCarHitbox] {name}: ignored {other.name}, feet at or above the hitbox (roof).");
            return;
        }

        Vector3 direction = _obstacle.TravelDirection;
        if (direction == Vector3.zero)
        {
            Debug.Log($"[TrafficCarHitbox] {name}: ignored {other.name}, the car has no direction of travel.");
            return;
        }

        Transform boxTransform = _box.transform;
        Vector3 center = boxTransform.TransformPoint(_box.center);
        Vector3 halfSize = _box.size * 0.5f;

        // Half-length of the box measured along the travel direction, valid for any rotation/scale.
        float halfLength =
            Mathf.Abs(Vector3.Dot(direction, boxTransform.TransformVector(new Vector3(halfSize.x, 0f, 0f)))) +
            Mathf.Abs(Vector3.Dot(direction, boxTransform.TransformVector(new Vector3(0f, halfSize.y, 0f)))) +
            Mathf.Abs(Vector3.Dot(direction, boxTransform.TransformVector(new Vector3(0f, 0f, halfSize.z))));

        Vector3 toOther = other.bounds.center - center;
        toOther.y = 0f;

        float along = Vector3.Dot(toOther, direction);
        float frontStart = halfLength * (1f - 2f * frontFraction);
        if (along < frontStart)
        {
            Debug.Log($"[TrafficCarHitbox] {name}: ignored {other.name}, side or tail contact (along {along:F1}, front starts at {frontStart:F1}).");
            return;
        }

        ImpactInfo impact = new ImpactInfo
        {
            damage = damage,
            pushDirection = ChoosePushDirection(other, toOther, direction),
            pushDistance = pushDistance,
            pushDuration = pushDuration,
            immunitySeconds = immunitySeconds,
            cause = cause
        };

        Debug.Log($"[TrafficCarHitbox] {name}: front hit on {other.name}, {damage} damage, push {impact.pushDirection}.");
        receiver.ReceiveImpact(impact);

        if (!string.IsNullOrEmpty(hitSound) && AudioManager.instance != null)
        {
            AudioManager.instance.Play(hitSound);
        }
    }

    // Perpendicular to the travel direction, toward the side of the car's axis the victim is on, so a
    // crash costs HP and also takes her out of the lane.
    private Vector3 ChoosePushDirection(Collider other, Vector3 toOther, Vector3 direction)
    {
        Vector3 lateral = Vector3.Cross(Vector3.up, direction);
        float side = Vector3.Dot(toOther, lateral);

        if (Mathf.Abs(side) >= axisDeadZone)
        {
            return lateral * Mathf.Sign(side);
        }

        // Dead centre: go against her own sideways movement, else a fixed side.
        CharacterController controller = other.GetComponent<CharacterController>();
        float sideways = controller != null ? Vector3.Dot(controller.velocity, lateral) : 0f;
        if (Mathf.Abs(sideways) > 0.01f)
        {
            return lateral * -Mathf.Sign(sideways);
        }

        return lateral;
    }
}
