using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Level 2's opening (spec 020, #274): Kami drops onto page 1 in her classic outfit and changes into the
/// detective one, magical girl style: her Spine animation, a sound, a sparkle aura, a white-silver fill whose
/// peak hides the skin swap, a sparkle burst at the swap and fast cuts between the level's own cameras.
/// Then she is free and the level plays as before.
///
/// Every beat is placed in normalized time (0 = the animation starts, 1 = it ends), so when the real
/// animation lands with another length nothing has to be retimed. Until it lands a placeholder plays.
///
/// Placed once at Level 2's scene root (Prefabs/Level2/MagicalGirlChange.prefab). Runs only when the scene
/// starts on page 1: test starts on later pages keep the loadout's detective look.
/// </summary>
[DefaultExecutionOrder(100)] //Start after Player.Start, which composes her first skin from the starting loadout
public class MagicalGirlChange : MonoBehaviour
{
    [System.Serializable]
    public struct Shot
    {
        [Tooltip("When this cut happens, in normalized time (0 = the animation starts, 1 = it ends).")]
        [Range(0f, 1f)] public float at;
        public CameraMode mode;
    }

    [SerializeField, Tooltip("Starting the scene on this page (PageScrollerManager.startingPage) plays the change. Any other page skips it.")]
    int _pageNumber = 1;

    [Header("Drop in")]
    [SerializeField, Tooltip("World units Kami is lifted above her scene spot at level start; she falls from there. Her respawn point stays the scene spot.")]
    float _dropHeight = 6f;
    [SerializeField, Tooltip("Seconds in the air before the landing is checked (the controller still reads grounded on the first frames after the lift).")]
    float _minAirSeconds = 0.25f;
    [SerializeField, Tooltip("Seconds to wait for the landing before starting anyway, so nothing can leave Kami locked.")]
    float _maxWaitForLanding = 5f;

    [Header("Looks")]
    [SerializeField, Tooltip("What she wears when she drops in: her classic outfit and its shoes. After the swap she wears the starting loadout's items (Level2_StartLoadout).")]
    GearItem[] _classicLook;

    [Header("Animation")]
    [SerializeField, Tooltip("Kami's Spine animation for the change. Name it exactly this in Spine, or type the exported name here.")]
    string _animationName = "MagicalGirlChangeOutfit";
    [SerializeField, Tooltip("Played (looped) while the skeleton has no animation called Animation Name.")]
    string _placeholderAnimation = "Casting";
    [SerializeField, Tooltip("How long the beat lasts while the placeholder plays. With the real animation the beat lasts as long as it.")]
    float _placeholderSeconds = 4f;

    [Header("Sound")]
    [SerializeField, Tooltip("AudioBank id of the transformation sound.")]
    string _sound = AudioId.KamiMagicalChange;
    [SerializeField, Range(0f, 1f), Tooltip("When the sound starts, normalized time.")]
    float _soundAt = 0f;

    [Header("White-silver fill (the skin swaps in the middle of it)")]
    [SerializeField] Color _fillColor = new Color(0.902f, 0.925f, 0.961f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("Normalized time the fill starts rising.")]
    float _fillStart = 0.3f;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized time the fill is gone again. The skin swaps exactly halfway between start and end.")]
    float _fillEnd = 0.7f;
    [SerializeField, Tooltip("Fill amount (0-1) across the fill interval (x 0 = Fill Start, x 1 = Fill End). Keep it at 1 around x 0.5: that is where the skin swaps.")]
    AnimationCurve _fillCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.4f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

    [Header("Cameras (the level's own CameraManager modes)")]
    [SerializeField, Tooltip("Cuts in order. After the last one the camera goes back to Normal when the beat ends.")]
    Shot[] _shots =
    {
        new Shot { at = 0f, mode = CameraMode.CloseUp },
        new Shot { at = 0.25f, mode = CameraMode.OrigamiCasting },
        new Shot { at = 0.5f, mode = CameraMode.ReceiveReward },
        new Shot { at = 0.78f, mode = CameraMode.CloseUp },
    };
    [SerializeField, Tooltip("Blend between the cuts while the beat runs, seconds. The brain's own blends come back for the return to Normal.")]
    float _cutBlendSeconds = 0.35f;

    [Header("Particles")]
    [SerializeField, Tooltip("Looping sparkles around Kami for the whole beat.")]
    ParticleSystem _aura;
    [SerializeField, Tooltip("One-shot sparkle burst at the skin swap.")]
    ParticleSystem _burst;
    [SerializeField, Tooltip("Where the sparkles sit relative to the centre of Kami's body (negative z = toward the camera).")]
    Vector3 _particleOffset = new Vector3(0f, 0f, -1f);

    Player _player;
    CinemachineBrain _brain;
    CinemachineBlendDefinition _savedBlend;
    CinemachineBlenderSettings _savedCustomBlends;
    bool _blendOverridden;
    bool _running;
    bool _swapped;

    void Start()
    {
        if (PageScrollerManager.Instance != null && PageScrollerManager.Instance.startingPage != _pageNumber)
        {
            Debug.Log($"[MagicalGirlChange] scene starts on page {PageScrollerManager.Instance.startingPage}, not {_pageNumber}: no outfit change");
            return;
        }

        _player = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (_player == null)
        {
            _player = FindObjectOfType<Player>();
        }
        if (_player == null)
        {
            Debug.LogWarning("[MagicalGirlChange] no Player in the scene: no outfit change");
            return;
        }

        StartCoroutine(Run());
    }

    void OnDisable()
    {
        //switched off mid-way: finish at once, so Kami is never left locked, white or in the wrong outfit.
        //Not while the scene unloads: Finish touches cameras and Kami, which may already be gone
        if (_running && gameObject.scene.isLoaded)
        {
            Debug.LogWarning("[MagicalGirlChange] disabled mid-way: finishing at once");
            Finish();
        }
    }

    IEnumerator Run()
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogWarning("[MagicalGirlChange] no LevelManager: can't lock Kami, no outfit change");
            yield break;
        }

        _running = true;
        _swapped = false;
        LevelManager.Instance.inCutscene = true;

        Wear(_classicLook);
        LiftKami();

        yield return WaitForLanding();
        yield return WaitForLevelStartCamera();

        Spine.TrackEntry entry = PlayAnimation(out float duration);
        Debug.Log($"[MagicalGirlChange] change starts ({duration:0.00}s, '{(entry != null ? entry.Animation.Name : "none")}')");

        OverrideBlend();
        PlaceParticles();
        if (_aura != null)
        {
            _aura.Play(true);
        }

        float swapAt = (_fillStart + _fillEnd) * 0.5f;
        bool soundPlayed = false;
        int nextShot = 0;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            if (!soundPlayed && t >= _soundAt)
            {
                soundPlayed = true;
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.Play(_sound);
                }
            }

            while (nextShot < _shots.Length && t >= _shots[nextShot].at)
            {
                if (CameraManager.Instance != null)
                {
                    CameraManager.Instance.SetCamera(_shots[nextShot].mode);
                }
                nextShot++;
            }

            _player.SetSkinFill(_fillColor, FillAt(t));

            if (!_swapped && t >= swapAt)
            {
                SwapToDetective();
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        Finish();
    }

    void LiftKami()
    {
        CharacterController cc = _player.cc;
        bool ccWasOn = cc != null && cc.enabled;
        if (cc != null)
        {
            cc.enabled = false;
        }

        _player.transform.position += Vector3.up * _dropHeight;

        if (cc != null)
        {
            cc.enabled = ccWasOn;
        }

        //a teleport: the hair/cape physics must not read it as movement, and a trigger she was in sends no exit
        _player.ResetSkeletonPhysicsMemory();
        TriggerScript.ExitAll();
        Debug.Log($"[MagicalGirlChange] Kami lifted {_dropHeight} units, dropping in with her classic outfit");
    }

    IEnumerator WaitForLanding()
    {
        float waited = 0f;
        while (waited < _maxWaitForLanding)
        {
            bool landed = waited >= _minAirSeconds
                          && _player.cc != null && _player.cc.isGrounded
                          && _player.CurrentState == PlayerState.Idle;
            if (landed)
            {
                yield break;
            }

            yield return null;
            waited += Time.deltaTime;
        }

        Debug.LogWarning($"[MagicalGirlChange] Kami didn't land within {_maxWaitForLanding}s (state {_player.CurrentState}): starting anyway");
    }

    IEnumerator WaitForLevelStartCamera()
    {
        if (CameraManager.Instance == null)
        {
            yield break;
        }

        //CameraManager cuts to Normal this long after load: starting earlier, it would cut over the first shot
        while (Time.timeSinceLevelLoad <= CameraManager.Instance.LevelStartDelayTime + 0.05f)
        {
            yield return null;
        }
    }

    Spine.TrackEntry PlayAnimation(out float duration)
    {
        Spine.Unity.SkeletonAnimation skeleton = _player.SkeletonAnimation;
        Spine.SkeletonData data = skeleton.Skeleton.Data;

        Spine.Animation animation = data.FindAnimation(_animationName);
        if (animation != null)
        {
            duration = Mathf.Max(animation.Duration, 0.1f);
            return skeleton.AnimationState.SetAnimation(0, animation, false);
        }

        duration = _placeholderSeconds;
        Spine.Animation placeholder = data.FindAnimation(_placeholderAnimation);
        if (placeholder == null)
        {
            Debug.LogWarning($"[MagicalGirlChange] Kami's skeleton has neither '{_animationName}' nor the placeholder '{_placeholderAnimation}': she keeps her idle");
            return null;
        }

        Debug.LogWarning($"[MagicalGirlChange] Kami's skeleton has no '{_animationName}' animation yet: playing '{_placeholderAnimation}' for {_placeholderSeconds}s");
        return skeleton.AnimationState.SetAnimation(0, placeholder, true);
    }

    float FillAt(float t)
    {
        if (_fillEnd <= _fillStart || t <= _fillStart || t >= _fillEnd)
        {
            return 0f;
        }

        return Mathf.Clamp01(_fillCurve.Evaluate((t - _fillStart) / (_fillEnd - _fillStart)));
    }

    void SwapToDetective()
    {
        _swapped = true;

        GearLoadout loadout = _player.StartingLoadout;
        if (loadout == null)
        {
            Debug.LogWarning("[MagicalGirlChange] the Player has no starting loadout: she keeps the classic outfit");
        }
        else
        {
            foreach (GearItem item in loadout.Equipped)
            {
                if (item != null)
                {
                    _player.Gear.Equip(item);
                }
            }
        }

        if (_burst != null)
        {
            PlaceParticles();
            _burst.Play(true);
        }
        Debug.Log("[MagicalGirlChange] skin swap under the white: detective outfit on");
    }

    void Wear(GearItem[] items)
    {
        if (items == null || items.Length == 0)
        {
            Debug.LogWarning("[MagicalGirlChange] Classic Look is empty: she drops in already dressed as a detective");
            return;
        }

        foreach (GearItem item in items)
        {
            if (item != null)
            {
                _player.Gear.Equip(item);
            }
        }
    }

    void PlaceParticles()
    {
        Vector3 center = _player.cc != null ? _player.cc.bounds.center : _player.transform.position;
        transform.position = center + _particleOffset;
    }

    void OverrideBlend()
    {
        _brain = FindObjectOfType<CinemachineBrain>();
        if (_brain == null)
        {
            Debug.LogWarning("[MagicalGirlChange] no CinemachineBrain: the cuts use whatever blend there is");
            return;
        }

        _savedBlend = _brain.m_DefaultBlend;
        _savedCustomBlends = _brain.m_CustomBlends;
        _brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, _cutBlendSeconds);
        _brain.m_CustomBlends = null; //its per-pair blends would win over the short one
        _blendOverridden = true;
    }

    void RestoreBlend()
    {
        if (!_blendOverridden || _brain == null)
        {
            return;
        }

        _brain.m_DefaultBlend = _savedBlend;
        _brain.m_CustomBlends = _savedCustomBlends;
        _blendOverridden = false;
    }

    void Finish()
    {
        if (!_running)
        {
            return;
        }
        _running = false;

        if (!_swapped && _player != null)
        {
            SwapToDetective();
        }

        RestoreBlend();

        if (_aura != null)
        {
            _aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_player != null)
        {
            _player.SetSkinFill(_fillColor, 0f);
            _player.ResumeBodyAnimation();
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCamera(CameraMode.Normal);
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.inCutscene = false;
        }
        Debug.Log("[MagicalGirlChange] done: Kami is free");
    }
}
