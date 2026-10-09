using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AI;

public enum Gait
{
    Idle,
    Walk,
    Run
}

/// <summary>
/// A named animation beat of a SpineCharacter: Natalia's shock, Ariel's point, a cop's arrest.
/// Played by cues (dialogue lines) or code.
/// </summary>
[System.Serializable]
public class SpinePose
{
    [Tooltip("Name cues and code use (Shock, Point, Arrest...). Not case-sensitive.")]
    public string id;

    [SpineAnimation, Tooltip("Played once before Main (IdleToShock, IdleThinking). Empty = straight to Main.")]
    public string intro;

    [SpineAnimation, Tooltip("The pose itself. Empty = the character's idle animation (a pose that only adds an overlay, like Ariel's smug face).")]
    public string main;

    [Tooltip("On: Main loops until the pose is ended (by a cue, by code, by moving or by its hold time). Off: Main plays once, then Next.")]
    public bool loop = true;

    [Tooltip("Only when Loop is off: the pose to chain into once Main has played (Ariel's Point -> Smug). Empty = back to idle / walk / rest pose.")]
    public string next;

    [SpineAnimation, Tooltip("Layered on top (track 1) while this pose plays, e.g. Ariel's Smug face. Empty = nothing on top (the cops put their lantern away to arrest).")]
    public string overlay;

    [Tooltip("Starting to move ends the pose. Off: the pose holds even while the character is moved around.")]
    public bool endsWhenMoving = true;

    [Tooltip("Only matters when this pose is the REST pose (the mood while standing still): the next page turn clears it. Natalia's page 2 happiness.")]
    public bool clearOnPageTurn;
}

/// <summary>
/// Animates an NPC's Spine skeleton (spec 014, D1): idle / walk / run from how fast it really moves,
/// facing via Skeleton.ScaleX, named poses, a "rest pose" (mood) for when it stands still, overlays on
/// track 1 (the cops' lantern), and an optional idle break.
///
/// Speed is MEASURED (the NavMeshAgent's velocity when it drives the character, otherwise the
/// transform's own movement), so a character slid by a cutscene or carried by a ride animates too
/// without anyone telling it to.
///
/// Story beats reach it by actor id (AnimationCue), so a DialogueSO can trigger "Ariel points" on the
/// exact line without a scene reference.
///
/// Track 0 = the body (locomotion, poses), track 1 = overlays. Same split as Kami's PlayerView.
/// </summary>
[DisallowMultipleComponent]
public class SpineCharacter : MonoBehaviour
{
    const int BodyTrack = 0;
    const int OverlayTrack = 1;

    static readonly List<SpineCharacter> ActiveCharacters = new List<SpineCharacter>();

    [Header("Identity")]
    [SerializeField, Tooltip("Who this is for animation cues: Natalia, Ariel, Police, Guard... Several characters can share an id (both cops of a scene answer 'Police').")]
    string _actorId;

    [SerializeField, Tooltip("The skeleton to animate. Empty = the first SkeletonAnimation in the children.")]
    SkeletonAnimation _skeleton;

    [Header("Locomotion")]
    [SerializeField, SpineAnimation, Tooltip("Standing still.")]
    string _idleAnimation = "Idle";

    [SerializeField, SpineAnimation, Tooltip("Moving below Run Speed.")]
    string _walkAnimation = "Walk";

    [SerializeField, SpineAnimation, Tooltip("Moving at Run Speed or faster. Empty = never runs.")]
    string _runAnimation = "";

    [SerializeField, Tooltip("World units/second above which the character counts as moving (it stops again under half of it).")]
    float _movingSpeed = 1f;

    [SerializeField, Tooltip("World units/second at which walking turns into running. For reference, Kami walks under 10, skips 10-20 and sprints at 28.")]
    float _runSpeed = 12f;

    [SerializeField, Tooltip("The speed (units/s) at which the walk cycle's feet match the ground: the walk animation is sped up or slowed down around it. 0 = always normal speed.")]
    float _walkAnimationSpeed = 0f;

    [SerializeField, Tooltip("Same as Walk Animation Speed, for the run cycle. 0 = always normal speed.")]
    float _runAnimationSpeed = 0f;

    [SerializeField, Tooltip("Seconds every animation change blends over.")]
    float _mixSeconds = 0.2f;

    [Header("Overlays (track 1)")]
    [SerializeField, SpineAnimation, Tooltip("Layered on top while standing still (the cops' Lantern). Empty = nothing. Cues can change it at runtime.")]
    string _idleOverlay = "";

    [SerializeField, SpineAnimation, Tooltip("Layered on top while walking or running (the page 4 guards' Lantern). Empty = nothing. Cues can change it at runtime.")]
    string _walkOverlay = "";

    [Header("Facing")]
    [SerializeField, Tooltip("Which way the art is drawn in Spine. Kami's faces right; turn off for a character drawn facing left.")]
    bool _artFacesRight = true;

    [SerializeField, Tooltip("Turn to face where it moves. Off when other code owns the facing (the page 4 guards turn with their vision cone).")]
    bool _faceMovement = true;

    [SerializeField, Tooltip("While standing still, turn toward Kami.")]
    bool _faceKamiWhenIdle = false;

    [SerializeField, Tooltip("Sideways speed (units/s), or distance to Kami (units) when facing her, under which the facing is left as it is. Stops flickering on arrival.")]
    float _facingDeadzone = 0.3f;

    [Header("Poses")]
    [SerializeField, Tooltip("Named beats this character can play. Cues and code refer to them by Id.")]
    List<SpinePose> _poses = new List<SpinePose>();

    [SerializeField, Tooltip("Rest pose (mood) from the start: what it does while standing still until something clears it. Empty = plain idle. Natalia starts Angry on page 1.")]
    string _initialRestPose = "";

    [Header("Idle break")]
    [SerializeField, Tooltip("Pose played now and then while standing in plain idle (no mood, no pose, no dialogue, no cutscene). Empty = never.")]
    string _idleBreakPose = "";

    [SerializeField, Tooltip("Seconds of plain idle before an idle break: a random value between X and Y each time.")]
    Vector2 _idleBreakEverySeconds = new Vector2(8f, 14f);

    [SerializeField, Tooltip("How long the idle break pose plays before going back to idle.")]
    float _idleBreakSeconds = 4f;

    [Header("Debug")]
    [SerializeField, Tooltip("Log every animation change, not only pose changes.")]
    bool _debugAnimations = false;

    //movement read from the world, smoothed
    NavMeshAgent _agent;
    Vector3 _lastPosition;
    float _speed;
    float _velocityX;
    bool _moving;

    //what is on the skeleton now (so nothing is restarted every frame)
    string _bodyAnimation;
    bool _bodyLoop;
    string _overlayAnimation;

    SpinePose _pose;
    float _poseOnceLeft;
    float _poseEndTime = -1f;
    SpinePose _restPose;
    Gait? _forcedGait;
    Gait _gaitCap = Gait.Run;
    float _idleTime;
    float _nextIdleBreak;
    readonly HashSet<string> _warnedMissing = new HashSet<string>();

    const float TeleportSpeed = 150f; //units/s: anything faster in one frame is a warp, not a walk

    public string ActorId => _actorId;
    public SkeletonAnimation SkeletonAnimation => _skeleton;
    public bool IsMoving => _moving;
    public float Speed => _speed;
    public string CurrentPoseId => _pose != null ? _pose.id : null;
    public string RestPoseId => _restPose != null ? _restPose.id : null;

    /// <summary>True when the skeleton is drawn facing right (after the flip).</summary>
    public bool FacingRight => _skeleton == null || _skeleton.Skeleton == null || ((_skeleton.Skeleton.ScaleX > 0f) == _artFacesRight);

    // ---------------------------------------------------------------- lifecycle

    void Awake()
    {
        if (_skeleton == null)
        {
            _skeleton = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (_skeleton == null)
        {
            Debug.LogWarning($"[SpineCharacter] {name}: no SkeletonAnimation in its children, it will not animate.");
        }

        _agent = GetComponent<NavMeshAgent>();
        _lastPosition = transform.position;

        //in Awake, not Start: other scripts' Start may clear it (a test started past page 1), and it
        //needs no skeleton, only the Poses list
        if (!string.IsNullOrEmpty(_initialRestPose))
        {
            SetRestPose(_initialRestPose);
        }
    }

    void OnEnable()
    {
        if (!ActiveCharacters.Contains(this))
        {
            ActiveCharacters.Add(this);
        }

        _lastPosition = transform.position;
    }

    void OnDisable()
    {
        ActiveCharacters.Remove(this);
    }

    void Start()
    {
        EventManager.Subscribe(Evento.OnNewPageOpen, OnNewPageOpen);
        ScheduleIdleBreak();

        //right away, so the prefab's preview animation (a looping Arrest, a Point) never shows for a frame
        ApplyAnimation();
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnNewPageOpen, OnNewPageOpen);
    }

    void Update()
    {
        if (!IsSkeletonReady())
        {
            return;
        }

        float dt = Time.deltaTime;
        MeasureMovement(dt);
        UpdatePose(dt);
        UpdateIdleBreak(dt);
        ApplyAnimation();
        UpdateFacing();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Plays a pose (intro, then main). <paramref name="holdSeconds"/> &gt; 0 ends a looping pose by itself after that long.</summary>
    public bool PlayPose(string id, float holdSeconds = 0f)
    {
        SpinePose pose = FindPose(id);
        if (pose == null)
        {
            Debug.LogWarning($"[SpineCharacter] {name}: no pose '{id}' in its Poses list, ignored.");
            return false;
        }

        if (!IsSkeletonReady())
        {
            return false;
        }

        _pose = pose;
        _poseEndTime = holdSeconds > 0f ? Time.time + holdSeconds : -1f;
        _idleTime = 0f;

        string main = string.IsNullOrEmpty(pose.main) ? _idleAnimation : pose.main;
        float introSeconds = 0f;

        Spine.Animation intro = FindAnimation(pose.intro);
        Spine.Animation mainAnimation = FindAnimation(main);

        if (intro != null)
        {
            SetBodyNow(pose.intro, false);
            introSeconds = intro.Duration;

            if (mainAnimation != null)
            {
                //queued after the intro: delay 0 = when the intro ends (minus the mix)
                Spine.TrackEntry queued = _skeleton.AnimationState.AddAnimation(BodyTrack, main, pose.loop, 0f);
                queued.SetMixDuration(_mixSeconds, 0f);
            }
            _bodyAnimation = main;
            _bodyLoop = pose.loop;
        }
        else if (mainAnimation != null)
        {
            SetBodyNow(main, pose.loop);
        }

        _poseOnceLeft = introSeconds + (!pose.loop && mainAnimation != null ? mainAnimation.Duration : 0f);
        SetOverlay(pose.overlay);

        Debug.Log($"[SpineCharacter] {name}: pose '{pose.id}'{(holdSeconds > 0f ? $" for {holdSeconds:F1}s" : "")}");
        return true;
    }

    /// <summary>Stops the pose that is playing, back to idle / walk / rest pose.</summary>
    public void EndPose()
    {
        if (_pose == null)
        {
            return;
        }

        Debug.Log($"[SpineCharacter] {name}: pose '{_pose.id}' ends");
        _pose = null;
        _poseEndTime = -1f;
        _idleTime = 0f;
        ApplyAnimation();
    }

    /// <summary>The pose to stand in while not moving, until cleared (a mood: Natalia angry, happy).</summary>
    public bool SetRestPose(string id)
    {
        SpinePose pose = FindPose(id);
        if (pose == null)
        {
            Debug.LogWarning($"[SpineCharacter] {name}: no pose '{id}' to rest in, ignored.");
            return false;
        }

        _restPose = pose;
        Debug.Log($"[SpineCharacter] {name}: rest pose '{pose.id}'");
        return true;
    }

    public void ClearRestPose()
    {
        if (_restPose == null)
        {
            return;
        }

        Debug.Log($"[SpineCharacter] {name}: rest pose '{_restPose.id}' cleared");
        _restPose = null;
        _idleTime = 0f;
    }

    /// <summary>Never faster than this gait (the arrest escort is walked, never run).</summary>
    public void CapGait(Gait max)
    {
        _gaitCap = max;
    }

    public void ClearGaitCap()
    {
        _gaitCap = Gait.Run;
    }

    /// <summary>Plays this gait whatever the measured speed says (riding a page turn or the paper plane: Walk).</summary>
    public void ForceGait(Gait gait)
    {
        _forcedGait = gait;
    }

    public void ClearForcedGait()
    {
        _forcedGait = null;
    }

    public void SetIdleOverlay(string animationName)
    {
        _idleOverlay = animationName ?? "";
    }

    public void SetWalkOverlay(string animationName)
    {
        _walkOverlay = animationName ?? "";
    }

    /// <summary>Turns the skeleton to face right or left.</summary>
    public void Face(bool right)
    {
        if (!IsSkeletonReady())
        {
            return;
        }

        _skeleton.Skeleton.ScaleX = right == _artFacesRight ? 1f : -1f;
    }

    /// <summary>Turns toward a point (on the world X axis, like every facing in the game).</summary>
    public void FaceTowards(Vector3 point)
    {
        float dx = point.x - transform.position.x;
        if (Mathf.Abs(dx) > _facingDeadzone)
        {
            Face(dx > 0f);
        }
    }

    /// <summary>
    /// Call right after a teleport (NavMeshAgent.Warp, a ride's landing): the physics constraints (hair)
    /// forget the old position, and the jump is not read as a burst of speed.
    /// </summary>
    public void ResetPhysics()
    {
        _lastPosition = transform.position;
        _speed = 0f;
        _velocityX = 0f;

        if (_skeleton != null)
        {
            _skeleton.ResetLastPositionAndRotation();
        }
    }

    /// <summary>Tints the whole skeleton (a knocked-out cop greys out).</summary>
    public void SetTint(Color color)
    {
        if (!IsSkeletonReady())
        {
            return;
        }

        Spine.Skeleton skeleton = _skeleton.Skeleton;
        skeleton.R = color.r;
        skeleton.G = color.g;
        skeleton.B = color.b;
        skeleton.A = color.a;
    }

    public bool HasPose(string id)
    {
        return FindPose(id) != null;
    }

    /// <summary>Applies a cue to every active character with its actor id. Returns how many got it.</summary>
    public static int ApplyCue(AnimationCue cue)
    {
        int reached = 0;

        //copied: a cue can enable or disable characters through the code it triggers
        foreach (SpineCharacter character in ActiveCharacters.ToArray())
        {
            if (character == null || !character.isActiveAndEnabled)
            {
                continue;
            }

            if (!string.Equals(character._actorId, cue.actor, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            character.Apply(cue);
            reached++;
        }

        return reached;
    }

    void Apply(AnimationCue cue)
    {
        Debug.Log($"[SpineCharacter] {name}: cue {cue}");

        switch (cue.action)
        {
            case AnimationCueAction.PlayPose:
                PlayPose(cue.value);
                break;
            case AnimationCueAction.EndPose:
                EndPose();
                break;
            case AnimationCueAction.SetRestPose:
                SetRestPose(cue.value);
                break;
            case AnimationCueAction.ClearRestPose:
                ClearRestPose();
                break;
            case AnimationCueAction.SetIdleOverlay:
                SetIdleOverlay(cue.value);
                break;
            case AnimationCueAction.SetWalkOverlay:
                SetWalkOverlay(cue.value);
                break;
            default:
                Debug.LogWarning($"[SpineCharacter] {name}: unknown cue action {cue.action}");
                break;
        }
    }

    // ---------------------------------------------------------------- every frame

    void MeasureMovement(float dt)
    {
        Vector3 position = transform.position;
        if (dt <= 0f)
        {
            //paused (the Flap): nothing moved, keep the last reading
            _lastPosition = position;
            return;
        }

        Vector3 velocity;
        if (_agent != null && _agent.enabled && _agent.updatePosition && _agent.isOnNavMesh)
        {
            velocity = _agent.velocity;
        }
        else
        {
            velocity = (position - _lastPosition) / dt;
        }
        _lastPosition = position;
        velocity.y = 0f;

        if (velocity.magnitude > TeleportSpeed)
        {
            velocity = Vector3.zero;
        }

        float blend = 1f - Mathf.Exp(-dt * 12f);
        _speed = Mathf.Lerp(_speed, velocity.magnitude, blend);
        _velocityX = Mathf.Lerp(_velocityX, velocity.x, blend);

        //hysteresis: an agent braking into its stop would otherwise flicker between walk and idle
        _moving = _moving ? _speed > _movingSpeed * 0.5f : _speed > _movingSpeed;
    }

    void UpdatePose(float dt)
    {
        if (_pose == null)
        {
            return;
        }

        if (_pose.endsWhenMoving && _moving)
        {
            Debug.Log($"[SpineCharacter] {name}: pose '{_pose.id}' ends, it started moving");
            _pose = null;
            return;
        }

        if (_poseEndTime >= 0f && Time.time >= _poseEndTime)
        {
            EndPose();
            return;
        }

        if (_pose.loop)
        {
            return;
        }

        _poseOnceLeft -= dt;
        if (_poseOnceLeft > 0f)
        {
            return;
        }

        string next = _pose.next;
        _pose = null;
        if (!string.IsNullOrEmpty(next))
        {
            PlayPose(next);
        }
    }

    void UpdateIdleBreak(float dt)
    {
        bool plainIdle = _pose == null && _restPose == null && _forcedGait == null && !_moving && !IsStoryFrozen();
        if (!plainIdle || string.IsNullOrEmpty(_idleBreakPose))
        {
            _idleTime = 0f;
            return;
        }

        _idleTime += dt;
        if (_idleTime < _nextIdleBreak)
        {
            return;
        }

        _idleTime = 0f;
        ScheduleIdleBreak();
        PlayPose(_idleBreakPose, _idleBreakSeconds);
    }

    void ScheduleIdleBreak()
    {
        float min = Mathf.Max(0.5f, Mathf.Min(_idleBreakEverySeconds.x, _idleBreakEverySeconds.y));
        float max = Mathf.Max(min, Mathf.Max(_idleBreakEverySeconds.x, _idleBreakEverySeconds.y));
        _nextIdleBreak = Random.Range(min, max);
    }

    //no thinking breaks while the story has the stage: a line, a cutscene, a page turn
    static bool IsStoryFrozen()
    {
        LevelManager levelManager = LevelManager.Instance;
        return levelManager != null && (levelManager.inDialogue || levelManager.inCutscene);
    }

    void ApplyAnimation()
    {
        if (!IsSkeletonReady())
        {
            return;
        }

        if (_pose != null)
        {
            //the pose already set track 0 (and queued its main after the intro)
            SetOverlay(_pose.overlay);
            return;
        }

        Gait gait = _forcedGait ?? (_moving ? (CanRun() && _speed >= _runSpeed ? Gait.Run : Gait.Walk) : Gait.Idle);
        if (gait == Gait.Run && (_gaitCap != Gait.Run || !CanRun()))
        {
            gait = Gait.Walk;
        }

        switch (gait)
        {
            case Gait.Idle:
                if (_restPose != null)
                {
                    SetBody(string.IsNullOrEmpty(_restPose.main) ? _idleAnimation : _restPose.main, true, 1f);
                    SetOverlay(_restPose.overlay);
                }
                else
                {
                    SetBody(_idleAnimation, true, 1f);
                    SetOverlay(_idleOverlay);
                }
                break;
            case Gait.Walk:
                SetBody(_walkAnimation, true, CycleTimeScale(_walkAnimationSpeed));
                SetOverlay(_walkOverlay);
                break;
            case Gait.Run:
                SetBody(_runAnimation, true, CycleTimeScale(_runAnimationSpeed));
                SetOverlay(_walkOverlay);
                break;
        }
    }

    bool CanRun()
    {
        return !string.IsNullOrEmpty(_runAnimation);
    }

    float CycleTimeScale(float referenceSpeed)
    {
        if (referenceSpeed <= 0f || _forcedGait != null)
        {
            return 1f;
        }

        return Mathf.Clamp(_speed / referenceSpeed, 0.5f, 2f);
    }

    void UpdateFacing()
    {
        if (!_faceMovement)
        {
            return;
        }

        if (_moving)
        {
            if (Mathf.Abs(_velocityX) > _facingDeadzone)
            {
                Face(_velocityX > 0f);
            }
            return;
        }

        if (_faceKamiWhenIdle && _forcedGait == null)
        {
            Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
            if (kami != null)
            {
                FaceTowards(kami.transform.position);
            }
        }
    }

    void OnNewPageOpen(params object[] parameters)
    {
        if (_restPose != null && _restPose.clearOnPageTurn)
        {
            ClearRestPose();
        }
    }

    // ---------------------------------------------------------------- skeleton plumbing

    bool IsSkeletonReady()
    {
        return _skeleton != null && _skeleton.valid && _skeleton.AnimationState != null && _skeleton.Skeleton != null;
    }

    SpinePose FindPose(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (SpinePose pose in _poses)
        {
            if (pose != null && string.Equals(pose.id, id, System.StringComparison.OrdinalIgnoreCase))
            {
                return pose;
            }
        }
        return null;
    }

    Spine.Animation FindAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName) || !IsSkeletonReady())
        {
            return null;
        }

        Spine.Animation animation = _skeleton.Skeleton.Data.FindAnimation(animationName);
        if (animation == null && _warnedMissing.Add(animationName))
        {
            Debug.LogWarning($"[SpineCharacter] {name}: the skeleton has no animation '{animationName}' (renamed in a re-export?). Pick it again in the Inspector.");
        }
        return animation;
    }

    /// <summary>Track 0, only when it changes (a looping animation set every frame would restart every frame).</summary>
    void SetBody(string animationName, bool loop, float timeScale)
    {
        if (animationName == _bodyAnimation && loop == _bodyLoop)
        {
            Spine.TrackEntry current = _skeleton.AnimationState.GetCurrent(BodyTrack);
            if (current != null)
            {
                current.TimeScale = timeScale;
            }
            return;
        }

        if (SetBodyNow(animationName, loop))
        {
            Spine.TrackEntry current = _skeleton.AnimationState.GetCurrent(BodyTrack);
            if (current != null)
            {
                current.TimeScale = timeScale;
            }
        }
    }

    bool SetBodyNow(string animationName, bool loop)
    {
        if (FindAnimation(animationName) == null)
        {
            //remember it anyway, so a missing animation is not retried (and warned about) every frame
            _bodyAnimation = animationName;
            _bodyLoop = loop;
            return false;
        }

        Spine.TrackEntry entry = _skeleton.AnimationState.SetAnimation(BodyTrack, animationName, loop);
        entry.MixDuration = _mixSeconds;
        _bodyAnimation = animationName;
        _bodyLoop = loop;

        if (_debugAnimations)
        {
            Debug.Log($"[SpineCharacter] {name}: body '{animationName}'{(loop ? " (loop)" : "")}");
        }
        return true;
    }

    void SetOverlay(string animationName)
    {
        animationName = animationName ?? "";
        if (animationName == (_overlayAnimation ?? ""))
        {
            return;
        }

        _overlayAnimation = animationName;

        if (string.IsNullOrEmpty(animationName))
        {
            _skeleton.AnimationState.SetEmptyAnimation(OverlayTrack, _mixSeconds);
        }
        else if (FindAnimation(animationName) != null)
        {
            Spine.TrackEntry entry = _skeleton.AnimationState.SetAnimation(OverlayTrack, animationName, true);
            entry.MixDuration = _mixSeconds;
        }

        if (_debugAnimations)
        {
            Debug.Log($"[SpineCharacter] {name}: overlay '{animationName}'");
        }
    }
}
