using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 4: the Abuela drops out of the sky next to Kami's cell, the cell fence is gone
/// (a reveal: its GameObjects are switched off, nothing is really destroyed), she says a line and
/// starts following Kami.
///
/// PoliceStationPage decides WHEN (Schedule) and puts everything back after a capture (ResetEntrance):
/// this only knows how to perform the entrance. Kami is locked from the start of the countdown until
/// the Abuela's line ends (#172, 2026-10-13; it used to be only the fall and the line).
///
/// #232: the countdown is a rumble, as if a plane flew right over the station: camera shake pulses
/// from the same impulse source as the crash, growing from _rumbleStartForce to _rumbleEndForce, over a
/// low rumble loop. With the camera (the first time), _fallShot blends in during the rumble, looking up
/// at the sky she drops from, follows her down (its Composer looks at her) and stays on her through her
/// line; then the camera goes back to the player's. A capture restart has the rumble without the shot.
/// </summary>
public class AbuelaEntrance : MonoBehaviour
{
    [SerializeField, Tooltip("The Abuela (the Abuela_Follower prefab instance). Starts inactive; she appears when she falls.")]
    NPC _abuela;

    [SerializeField, Tooltip("Where she lands: just outside the cell fence, ON the NavMesh, so she can walk after Kami right away.")]
    Transform _landingPoint;

    [SerializeField, Tooltip("How high above the landing point she starts falling, in world units.")]
    float _dropHeight = 45f;

    [SerializeField, Tooltip("Seconds the fall takes. Long enough for the fall shot to follow her down (#232).")]
    float _fallSeconds = 1.4f;

    [SerializeField, Tooltip("Everything that disappears on impact: Kami's cell fence.")]
    GameObject[] _hideOnImpact;

    [SerializeField, Tooltip("Particles played at the landing point on impact (optional).")]
    ParticleSystem _impactParticles;

    //new field names on purpose: the scene still carries the old serialized _impactSound
    //(RockSmash), which would have overridden these defaults
    [SerializeField, Tooltip("Falling whistle, played as she starts to drop. Leave empty for none.")]
    string _fallSound = AudioId.AbuelaFall;

    [SerializeField, Tooltip("Sound played on impact. Leave empty for none.")]
    string _crashSound = AudioId.AbuelaCrash;

    [SerializeField, Tooltip("Rattle of the cell fence giving way, played on impact. Leave empty for none.")]
    string _fenceSound = AudioId.FenceRattle;

    [SerializeField, Tooltip("Camera shake on impact (#227). Amplitude Gain and Impulse Duration on this component are the knobs; every pickable camera carries a CinemachineImpulseListener, so it shakes whichever one is live. Optional.")]
    CinemachineImpulseSource _shake;

    [SerializeField, Tooltip("What she says after landing (optional).")]
    DialogueSO _landingLine;

    [SerializeField, Tooltip("Pose (on her SpineCharacter) looped while she drops. Empty = none.")]
    string _fallPose = "Falling";

    [SerializeField, Tooltip("Pose played once on impact, then she goes back to idle. Empty = none.")]
    string _landPose = "Land";

    [Header("Rumble and fall camera (#232)")]
    [SerializeField, Tooltip("Shake of the first rumble pulse, as a fraction of the crash (1 = the crash's shake).")]
    float _rumbleStartForce = 0.05f;

    [SerializeField, Tooltip("Shake of the last rumble pulse, just before she falls. Pulses overlap, so the felt shake is a bit stronger than this.")]
    float _rumbleEndForce = 0.35f;

    [SerializeField, Tooltip("Seconds between rumble pulses.")]
    float _rumblePulseSeconds = 0.25f;

    [SerializeField, Tooltip("Low rumble loop under the countdown, stopped by the crash. Leave empty for none.")]
    string _rumbleSound = AudioId.PlaneRumbleLoop;

    [SerializeField, Tooltip("The shot that looks up at her and follows her down (its Composer looks at the Abuela, set in code). Drag it to reframe. Empty = the fall plays in the player's camera.")]
    CinemachineVirtualCamera _fallShot;

    [SerializeField, Tooltip("Field of view the fall shot zooms to once she has landed, so the camera is close on her through her lines (#232 follow-up, Diego 2026-10-20). Zero or the shot's own value = no zoom.")]
    float _landFieldOfView = 18f;

    [SerializeField, Tooltip("Seconds the zoom from the shot's own field of view to the close-up takes after the impact.")]
    float _landZoomSeconds = 0.6f;

    Coroutine _running;
    float _fallShotFieldOfView; //the vcam's authored value, kept to put it back before the next use
    bool _lockedKami;

    public bool HasLanded { get; private set; }
    public NPC Abuela => _abuela;

    void Awake()
    {
        if (_fallShot != null)
        {
            _fallShotFieldOfView = _fallShot.m_Lens.FieldOfView;
        }

        if (_abuela == null || _landingPoint == null)
        {
            Debug.LogWarning($"[AbuelaEntrance] {gameObject.name}: _abuela or _landingPoint is not assigned, the Abuela will never come");
        }
    }

    /// <summary>
    /// Starts the countdown (the rumble) to her fall, cancelling any countdown already running.
    /// <paramref name="withCamera"/>: the fall shot follows her down (the first time; not after a capture).
    /// </summary>
    public void Schedule(float delaySeconds, bool withCamera)
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            StopRumble();
        }
        _running = StartCoroutine(EntranceRoutine(delaySeconds, withCamera));
        Debug.Log($"[AbuelaEntrance] the Abuela falls in {delaySeconds}s (fall camera: {withCamera})");
    }

    /// <summary>Back to before she came: she is gone, the fence is closed again.</summary>
    public void ResetEntrance()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }

        //a reset in the middle of the fall must not leave Kami locked, the rumble going or the shot live
        SetKamiLocked(false);
        StopRumble();
        CutsceneCamera.Hide(_fallShot);

        HasLanded = false;

        if (_abuela != null)
        {
            _abuela.StopFollowingPlayer();
            _abuela.gameObject.SetActive(false);
        }

        SetImpactObjectsActive(true);
    }

    IEnumerator EntranceRoutine(float delaySeconds, bool withCamera)
    {
        if (_abuela == null || _landingPoint == null)
        {
            _running = null;
            yield break;
        }

        //Kami waits for her from the start of the countdown (#172), not from the fall: walking around the
        //closed cell for a few seconds read as "nothing happens, go explore". A lock is only taken on a
        //calm screen, so it never sits on top of a dialogue, overlay, page turn or another cutscene's lock
        yield return WaitForCalmMoment();
        SetKamiLocked(true);

        //the landing point is on the floor, but her transform sits at the sprite's pivot (the waist):
        //end the fall with her FEET on the floor, where the agent will put her anyway. Read from the
        //component: she is still inactive, so NPC.Awake (which fills navAgent) has not run the first time
        NavMeshAgent agent = _abuela.GetComponent<NavMeshAgent>();
        float pivotAboveFeet = agent != null ? agent.baseOffset * _abuela.transform.lossyScale.y : 0f;
        Vector3 landing = _landingPoint.position + Vector3.up * pivotAboveFeet;
        Vector3 start = landing + Vector3.up * _dropHeight;

        //she waits up there, still hidden, so the shot already looks at the sky she will drop from. Her
        //agent goes off first: enabling one this far from the NavMesh fails with an engine warning
        if (agent != null)
        {
            agent.enabled = false;
        }
        _abuela.transform.position = start;

        bool useShot = withCamera && _fallShot != null;
        if (useShot)
        {
            _fallShot.LookAt = _abuela.transform;
            SetFallShotFieldOfView(_fallShotFieldOfView); //undo the last use's close-up (it is left on while the camera blends away)
        }

        yield return Rumble(delaySeconds, useShot);

        //something may have opened during the wait (a dialogue): never drop her in the middle of it
        yield return WaitForCalmMoment();
        SetKamiLocked(true); //again: a cutscene that ended meanwhile cleared the shared flag
        if (useShot)
        {
            CutsceneCamera.Show(_fallShot); //no-op if the rumble already did; covers a zero-second countdown
        }
        Debug.Log("[AbuelaEntrance] the Abuela falls");

        _abuela.gameObject.SetActive(true);
        _abuela.transform.position = start;
        _abuela.ResetSkeletonPhysics(); //she was parked up there by hand: her bun must not swing from it
        PlayPose(_fallPose);
        PlaySound(_fallSound);

        float elapsed = 0f;
        while (elapsed < _fallSeconds)
        {
            float t = elapsed / Mathf.Max(0.01f, _fallSeconds);
            _abuela.transform.position = Vector3.Lerp(start, landing, t * t); //t*t: accelerates like a real fall
            elapsed += Time.deltaTime;
            yield return null;
        }
        _abuela.transform.position = landing;

        Impact();
        PlayPose(_landPose);

        if (useShot)
        {
            StartCoroutine(ZoomFallShot());
        }

        if (_abuela.navAgent != null)
        {
            _abuela.navAgent.enabled = true;
        }
        _abuela.WarpTo(_landingPoint.position);

        if (_landingLine != null)
        {
            while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
            {
                yield return null;
            }
            DialogueManager.Instance.ShowDialogue(_landingLine);
            yield return null;

            //the line is over when the dialogue manager is free again
            while (!DialogueManager.Instance.CanShowDialogueNow)
            {
                yield return null;
            }
        }

        //the camera starts blending back as Kami gets control: waiting for the blend would only feel stuck
        if (useShot)
        {
            CutsceneCamera.Hide(_fallShot);
        }
        SetKamiLocked(false);

        //a prefab cannot hold a scene reference to Kami, and the Abuela's base prefab comes from Level 1
        if (_abuela.player == null)
        {
            _abuela.player = LevelManager.Instance.player;
        }
        _abuela.StartFollowingPlayer();
        HasLanded = true;
        _running = null;
    }

    //the countdown: shake pulses that grow toward the crash, over the rumble loop. The fall shot is
    //raised one blend before the end, so it has arrived (looking up) when she appears
    IEnumerator Rumble(float seconds, bool useShot)
    {
        PlaySound(_rumbleSound);

        if (_shake == null)
        {
            Debug.LogWarning($"[AbuelaEntrance] {gameObject.name}: _shake is not assigned, the rumble has no camera shake");
        }

        float showShotAt = useShot ? Mathf.Max(0f, seconds - CutsceneCamera.BlendSeconds) : float.MaxValue;
        bool shotShown = false;
        float nextPulse = 0f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!shotShown && elapsed >= showShotAt)
            {
                CutsceneCamera.Show(_fallShot);
                shotShown = true;
            }

            if (_shake != null && elapsed >= nextPulse)
            {
                float t = seconds > 0f ? elapsed / seconds : 1f;
                _shake.GenerateImpulseWithForce(Mathf.Lerp(_rumbleStartForce, _rumbleEndForce, t * t));
                nextPulse += Mathf.Max(0.05f, _rumblePulseSeconds);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    //the close-up on her: the same vcam, narrower lens (its Composer keeps her centred). Not restored
    //on Hide, or the picture would pop wider while the camera blends back to Kami's; the next use
    //of the shot starts from the authored value again
    IEnumerator ZoomFallShot()
    {
        float from = _fallShot.m_Lens.FieldOfView;
        if (_landFieldOfView <= 0f || Mathf.Approximately(from, _landFieldOfView))
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < _landZoomSeconds)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, _landZoomSeconds));
            SetFallShotFieldOfView(Mathf.Lerp(from, _landFieldOfView, t));
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetFallShotFieldOfView(_landFieldOfView);
    }

    void SetFallShotFieldOfView(float fieldOfView)
    {
        if (_fallShot == null || fieldOfView <= 0f)
        {
            return;
        }

        LensSettings lens = _fallShot.m_Lens;
        lens.FieldOfView = fieldOfView;
        _fallShot.m_Lens = lens;
    }

    void StopRumble()
    {
        if (!string.IsNullOrEmpty(_rumbleSound) && AudioManager.instance != null)
        {
            AudioManager.instance.StopById(_rumbleSound);
        }
    }

    void Impact()
    {
        Debug.Log("[AbuelaEntrance] impact: the cell fence is open");
        StopRumble();
        SetImpactObjectsActive(false);

        if (_impactParticles != null)
        {
            _impactParticles.transform.position = _landingPoint.position;
            _impactParticles.Play(true);
        }

        PlaySound(_crashSound);
        PlaySound(_fenceSound);

        if (_shake != null)
        {
            _shake.GenerateImpulse();
        }
        else
        {
            Debug.LogWarning($"[AbuelaEntrance] {gameObject.name}: _shake is not assigned, the crash has no camera shake");
        }
    }

    void PlayPose(string poseId)
    {
        if (string.IsNullOrEmpty(poseId))
        {
            return;
        }

        if (_abuela.Character == null)
        {
            Debug.LogWarning($"[AbuelaEntrance] {_abuela.name} has no SpineCharacter, pose '{poseId}' skipped");
            return;
        }

        _abuela.Character.PlayPose(poseId);
    }

    static void PlaySound(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }
        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[AbuelaEntrance] no AudioManager, '{id}' skipped");
            return;
        }
        AudioManager.instance.Play(id);
    }

    //a dialogue, overlay or page turn (inDialogue), or a cutscene that is not our own lock
    IEnumerator WaitForCalmMoment()
    {
        while (LevelManager.Instance == null
               || LevelManager.Instance.inDialogue
               || (LevelManager.Instance.inCutscene && !_lockedKami))
        {
            yield return null;
        }
    }

    //only ever releases a lock this component took, so it can't cut another cutscene short. Locking
    //always writes the flag, so a re-lock restores it if something else cleared it in between.
    void SetKamiLocked(bool locked)
    {
        if (LevelManager.Instance == null || (!locked && !_lockedKami))
        {
            return;
        }

        _lockedKami = locked;
        LevelManager.Instance.inCutscene = locked;
    }

    void SetImpactObjectsActive(bool active)
    {
        if (_hideOnImpact == null)
        {
            return;
        }

        foreach (GameObject target in _hideOnImpact)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
