using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Kami's reflection in a reflecting pool (spec 024 3.F, Diego 2026-11-09: "her, but in another look"). A second Kami
/// skeleton lies flat on the water's surface at her feet, upside down, mirrored from the real Kami every frame. It plays her
/// animations under its own skin (LoadoutSkin on the mirror: another outfit) and then copies her bones' world pose, so it
/// moves exactly like her, physics included. It is seen only on the pool: the pool's inside (Kami/Pool Hole) writes stencil 1
/// and the mirror's materials draw only where the stencil is 1. An optional animation on a track of its own gives the
/// reflection something the real Kami doesn't do (another face).
/// </summary>
public class ReflectionMirror : MonoBehaviour
{
    [SerializeField, Tooltip("The mirror Kami: a SkeletonAnimation on Kami's skeleton data, Update Timing = In Late Update (so the real Kami has moved first this frame).")]
    SkeletonAnimation _mirror;

    [SerializeField, Tooltip("The reflection lies at this mark's world height: just above the pool's inside, under its Surface layer.")]
    Transform _surface;

    [SerializeField, Tooltip("Colour of the reflection (darker and bluer than Kami reads as water).")]
    Color _tint = new Color(0.72f, 0.82f, 0.95f, 0.9f);

    [SerializeField, Tooltip("An animation only the reflection plays, on its own track over the copied ones (e.g. a face override). Empty = none.")]
    string _extraAnimation = "";

    [SerializeField, Tooltip("Track for the extra animation; must be above every track the real Kami uses (she uses 0-6).")]
    int _extraTrack = 8;

    [SerializeField, Tooltip("Kami's materials, and the reflection's copies of them (same order). The copies draw only on the pool (stencil Equal 1) and receive no shadows.")]
    Material[] _kamiMaterials;

    [SerializeField]
    Material[] _mirrorMaterials;

    [SerializeField, Tooltip("Beyond this horizontal distance from the pool the reflection isn't drawn at all.")]
    float _maxDistance = 30f;

    Player _kami;
    SkeletonAnimation _real;
    MeshRenderer _mirrorRenderer;

    void Start()
    {
        if (_mirror == null || _surface == null)
        {
            Debug.LogWarning($"[ReflectionMirror] {gameObject.name}: _mirror or _surface not assigned, no reflection");
            enabled = false;
            return;
        }
        _mirrorRenderer = _mirror.GetComponent<MeshRenderer>();
        if (_mirror.UpdateTiming != UpdateTiming.InLateUpdate)
        {
            Debug.LogWarning($"[ReflectionMirror] {gameObject.name}: the mirror should update In Late Update, set it so");
            _mirror.UpdateTiming = UpdateTiming.InLateUpdate;
        }
        int n = Mathf.Min(_kamiMaterials != null ? _kamiMaterials.Length : 0, _mirrorMaterials != null ? _mirrorMaterials.Length : 0);
        for (int i = 0; i < n; i++)
        {
            if (_kamiMaterials[i] != null && _mirrorMaterials[i] != null)
            {
                _mirror.CustomMaterialOverride[_kamiMaterials[i]] = _mirrorMaterials[i];
            }
        }
        if (_mirror.Skeleton != null)
        {
            _mirror.Skeleton.R = _tint.r;
            _mirror.Skeleton.G = _tint.g;
            _mirror.Skeleton.B = _tint.b;
            _mirror.Skeleton.A = _tint.a;
        }
        if (!string.IsNullOrEmpty(_extraAnimation) && _mirror.Skeleton != null)
        {
            if (_mirror.Skeleton.Data.FindAnimation(_extraAnimation) != null)
            {
                _mirror.AnimationState.SetAnimation(_extraTrack, _extraAnimation, true);
            }
            else
            {
                Debug.LogWarning($"[ReflectionMirror] {gameObject.name}: no animation '{_extraAnimation}' in the skeleton");
            }
        }
        _mirror.BeforeApply += CopyTracks;
        _mirror.UpdateComplete += CopyPose;
    }

    void OnDestroy()
    {
        if (_mirror != null)
        {
            _mirror.BeforeApply -= CopyTracks;
            _mirror.UpdateComplete -= CopyPose;
        }
    }

    bool FindKami()
    {
        if (_real != null)
        {
            return true;
        }
        _kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        _real = _kami != null ? _kami.SkeletonAnimation : null;
        if (_real != null && _real.SkeletonDataAsset != _mirror.SkeletonDataAsset)
        {
            Debug.LogWarning($"[ReflectionMirror] {gameObject.name}: the mirror's skeleton data is not Kami's ({_real.SkeletonDataAsset.name}): the pose can't be copied");
            _real = null;
        }
        return _real != null;
    }

    //the same animations at the same times, under the mirror's own skin: attachments, draw order and face swaps come out right
    void CopyTracks(ISkeletonAnimation animated)
    {
        if (!FindKami() || _real.AnimationState == null)
        {
            return;
        }
        Spine.AnimationState from = _real.AnimationState, to = _mirror.AnimationState;
        int count = from.Tracks.Count;
        for (int i = 0; i < Mathf.Max(count, to.Tracks.Count); i++)
        {
            if (i == _extraTrack && !string.IsNullOrEmpty(_extraAnimation))
            {
                continue;
            }
            TrackEntry src = i < count ? from.GetCurrent(i) : null;
            TrackEntry dst = to.GetCurrent(i);
            if (src == null)
            {
                if (dst != null)
                {
                    to.ClearTrack(i);
                }
                continue;
            }
            if (dst == null || dst.Animation != src.Animation)
            {
                dst = to.SetAnimation(i, src.Animation, src.Loop);
                dst.MixDuration = 0f;
            }
            dst.TimeScale = 0f; //driven from the real Kami's time below
            dst.TrackTime = src.TrackTime;
            dst.Alpha = src.Alpha;
            dst.MixBlend = src.MixBlend;
        }
    }

    //her exact pose (mixes and physics included), and the mirror's place under the surface
    void CopyPose(ISkeletonAnimation animated)
    {
        if (!FindKami() || _real.Skeleton == null || _mirror.Skeleton == null)
        {
            return;
        }
        Transform realT = _real.transform;
        Vector3 p = realT.position;
        float h = _surface.position.y;
        bool near = new Vector2(p.x - _surface.position.x, p.z - _surface.position.z).magnitude <= _maxDistance;
        if (_mirrorRenderer != null && _mirrorRenderer.enabled != near)
        {
            _mirrorRenderer.enabled = near;
        }
        if (!near)
        {
            return;
        }

        // laid flat on the water at her feet, upside down (her head towards the camera), face up (Diego, 2026-11-09: a mirror
        // standing below the plane went down through the book). Rotating her up axis onto the floor (+90 on x) keeps her
        // front facing the sky, and the negative y scale turns her head towards the camera, as a reflection on the ground.
        Transform mirrorT = _mirror.transform;
        mirrorT.position = new Vector3(p.x, h, p.z);
        mirrorT.rotation = realT.rotation * Quaternion.Euler(90f, 0f, 0f);
        Vector3 s = realT.lossyScale;
        Vector3 ps = mirrorT.parent != null ? mirrorT.parent.lossyScale : Vector3.one; //the pool is scaled uniformly
        mirrorT.localScale = new Vector3(s.x / ps.x, -s.y / ps.y, s.z / ps.z);

        ExposedList<Bone> src = _real.Skeleton.Bones, dst = _mirror.Skeleton.Bones;
        int n = Mathf.Min(src.Count, dst.Count);
        for (int i = 0; i < n; i++)
        {
            Bone a = src.Items[i], b = dst.Items[i];
            b.A = a.A;
            b.B = a.B;
            b.C = a.C;
            b.D = a.D;
            b.WorldX = a.WorldX;
            b.WorldY = a.WorldY;
        }
    }
}
