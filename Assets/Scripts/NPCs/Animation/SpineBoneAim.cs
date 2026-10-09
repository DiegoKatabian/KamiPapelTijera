using Spine.Unity;
using UnityEngine;

/// <summary>
/// Turns one bone of a Spine skeleton toward a target on top of whatever the animation does (spec 014,
/// D4): Ariel points AT Kami instead of straight ahead, with no change to the export.
///
/// Runs on SkeletonAnimation.UpdateLocal, i.e. after the animation pose is applied and before the
/// world transforms are computed, so the bones under the aimed one (forearm, hand) keep their authored
/// pose relative to it. The correction is the angle between where the bone's chain points now (bone
/// origin to the tip of the chain) and where the target is, clamped to Max Degrees and blended in and
/// out, so a Kami far above or behind never twists the arm into something broken.
/// </summary>
public class SpineBoneAim : MonoBehaviour
{
    [SerializeField, Tooltip("The skeleton. Empty = the first SkeletonAnimation in the children.")]
    SkeletonAnimation _skeleton;

    [SerializeField, SpineBone, Tooltip("The bone that turns (Ariel: brazo_front2, the upper arm that points).")]
    string _bone = "brazo_front2";

    [SerializeField, SpineBone, Tooltip("The bone whose tip marks where the arm points (Ariel: mano_back, the hand). Empty = the aimed bone's own tip.")]
    string _tipBone = "mano_back";

    [SerializeField, Tooltip("Who to point at. Empty = Kami.")]
    Transform _target;

    [SerializeField, Tooltip("Aim at this height above the target's pivot, in world units (Kami's pivot is at her feet' level only roughly; aim at her chest).")]
    float _targetHeight = 2f;

    [SerializeField, SpineAnimation, Tooltip("Only aims while this animation plays on track 0 (Point). Empty = always.")]
    string _onlyDuring = "Point";

    [SerializeField, Range(0f, 1f), Tooltip("How much of the correction is applied: 1 = points exactly at the target (within Max Degrees).")]
    float _weight = 1f;

    [SerializeField, Tooltip("The arm never turns further than this from its authored pose, in degrees.")]
    float _maxDegrees = 40f;

    [SerializeField, Tooltip("Seconds to blend the aim in when the animation starts, and out when it ends.")]
    float _blendSeconds = 0.25f;

    Spine.Bone _aimed;
    Spine.Bone _tip;
    float _blend;
    bool _warned;

    void Awake()
    {
        if (_skeleton == null)
        {
            _skeleton = GetComponentInChildren<SkeletonAnimation>(true);
        }
    }

    void OnEnable()
    {
        if (_skeleton == null)
        {
            Debug.LogWarning($"[SpineBoneAim] {name}: no SkeletonAnimation, nothing to aim.");
            return;
        }

        _skeleton.UpdateLocal += AimBone;
    }

    void OnDisable()
    {
        if (_skeleton != null)
        {
            _skeleton.UpdateLocal -= AimBone;
        }
    }

    void Update()
    {
        float goal = IsAimingAnimationPlaying() ? 1f : 0f;
        float step = _blendSeconds <= 0f ? 1f : Time.deltaTime / _blendSeconds;
        _blend = Mathf.MoveTowards(_blend, goal, step);
    }

    bool IsAimingAnimationPlaying()
    {
        if (string.IsNullOrEmpty(_onlyDuring))
        {
            return true;
        }

        if (_skeleton == null || _skeleton.AnimationState == null)
        {
            return false;
        }

        Spine.TrackEntry current = _skeleton.AnimationState.GetCurrent(0);
        return current != null && current.Animation != null && current.Animation.Name == _onlyDuring;
    }

    void AimBone(ISkeletonAnimation animated)
    {
        if (_blend <= 0f || _weight <= 0f || !ResolveBones())
        {
            return;
        }

        Transform target = _target;
        if (target == null)
        {
            Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
            target = kami != null ? kami.transform : null;
        }
        if (target == null)
        {
            return;
        }

        Spine.Skeleton skeleton = _skeleton.Skeleton;

        //the local pose has just been applied, but world transforms are still last frame's: bring them
        //up to date so the chain's current direction is read from THIS frame's animation
        skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.None);

        Vector3 targetLocal = _skeleton.transform.InverseTransformPoint(target.position + (Vector3.up * _targetHeight));

        Vector2 origin = new Vector2(_aimed.WorldX, _aimed.WorldY);
        Vector2 tip = TipPosition();
        Vector2 toTip = tip - origin;
        Vector2 toTarget = new Vector2(targetLocal.x, targetLocal.y) - origin;
        if (toTip.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        //world (skeleton-space) angle difference; with the skeleton flipped (ScaleX -1) a local rotation
        //turns the other way on screen, hence the sign
        float delta = Vector2.SignedAngle(toTip, toTarget);
        delta = Mathf.Clamp(delta, -_maxDegrees, _maxDegrees) * _weight * _blend;
        float flip = Mathf.Sign(skeleton.ScaleX) * Mathf.Sign(skeleton.ScaleY);
        if (_aimed.Parent != null)
        {
            //a parent with a mirroring world matrix (negative determinant) also flips the local rotation's sense
            float determinant = (_aimed.Parent.A * _aimed.Parent.D) - (_aimed.Parent.B * _aimed.Parent.C);
            flip = Mathf.Sign(determinant);
        }

        _aimed.Rotation += delta * flip;
    }

    Vector2 TipPosition()
    {
        if (_tip != null && _tip != _aimed)
        {
            //the far end of the tip bone, so the whole arm (not only the elbow) lines up with the target
            float rad = _tip.WorldRotationX * Mathf.Deg2Rad;
            float length = _tip.Data.Length * new Vector2(_tip.A, _tip.C).magnitude;
            return new Vector2(_tip.WorldX + (Mathf.Cos(rad) * length), _tip.WorldY + (Mathf.Sin(rad) * length));
        }

        float aimedRad = _aimed.WorldRotationX * Mathf.Deg2Rad;
        float aimedLength = _aimed.Data.Length * new Vector2(_aimed.A, _aimed.C).magnitude;
        return new Vector2(_aimed.WorldX + (Mathf.Cos(aimedRad) * aimedLength), _aimed.WorldY + (Mathf.Sin(aimedRad) * aimedLength));
    }

    bool ResolveBones()
    {
        if (_aimed != null)
        {
            return true;
        }

        if (_skeleton == null || _skeleton.Skeleton == null)
        {
            return false;
        }

        _aimed = _skeleton.Skeleton.FindBone(_bone);
        _tip = string.IsNullOrEmpty(_tipBone) ? null : _skeleton.Skeleton.FindBone(_tipBone);

        if (_aimed == null && !_warned)
        {
            _warned = true;
            Debug.LogWarning($"[SpineBoneAim] {name}: the skeleton has no bone '{_bone}', the arm keeps its authored pose.");
        }
        return _aimed != null;
    }
}
