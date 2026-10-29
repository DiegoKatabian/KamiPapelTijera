using Spine.Unity;
using System;
using UnityEngine;

public class PlayerView
{
    Player _player;
    Vector3 _lastDirection = Vector3.zero;
    SkeletonAnimation _skeletonAnimation;

    const int TRACK_BODY = 0;
    const int TRACK_NOSCISSORS = 1;
    const int TRACK_WIND = 2;
    const int TRACK_ATTACK = 3;
    const int TRACK_PAPERPLANE = 4;
    const int TRACK_HIT = 5;
    const int TRACK_FACE = 6;

    const string ANIMATION_IDLE = "Idle";
    const string ANIMATION_WALK = "walk";
    const string ANIMATION_JUMP = "jump";
    const string ANIMATION_FALLING = "falling";
    const string ANIMATION_LANDING = "landing";
    const string ANIMATION_CASTING = "Casting";
    const string ANIMATION_RECEIVE_REWARD = "Reward";
    const string ANIMATION_RECEIVE_REWARD_LOOP = "RewardLoop";
    const string ANIMATION_ATTACK = "Attack";
    const string ANIMATION_ATTACK_MOVE = "AttackMOVE";
    const string ANIMATION_IDLE_TO_CASTING = "IdleToCasting";
    const string ANIMATION_SKIP = "Skip";
    const string ANIMATION_RUN = "Run";
    const string ANIMATION_TAKE_HIT = "Hit";
    const string ANIMATION_NOSCISSORS_OVERRIDE = "NoScissortsOverride";
    const string ANIMATION_WIND = "Wind";
    const string ANIMATION_PAPERPLANE_OVERRIDE = "PaperPlaneOverride";
    const string ANIMATION_PULLSOLAPA = "PullSolapas";
    const string ANIMATION_PULLSOLAPA_REVERSE = "DownSolapas";
    const string ANIMATION_RUNSTOP = "RunStop";
    const string ANIMATION_DEATH = "Death";
    const string ANIMATION_DROWNING = "Drowning";
    const string ANIMATION_RIDING_PAGE = "RidePage";

    //Atlas 15 (2026-10-29). None of them has a NoScissors version: while she holds no scissors,
    //NoScissortsOverride is layered on top of them instead (see RefreshOverrides)
    const string ANIMATION_IDLE_BREAK = "Idle Break";
    const string ANIMATION_IDLE_TO_SIT = "Idle to Sit";
    const string ANIMATION_SIT = "Sit Tijera";
    const string ANIMATION_SIT_TO_IDLE = "Sit to Idle";
    const string ANIMATION_LOOK_INTO_REFLECTION = "Look into reflection";
    const string ANIMATION_SCARED_FACE = "Asustada Override";

    const string ANIMATION_IDLE_NOSCISSORS = "IdleNoScissors";
    const string ANIMATION_WALK_NOSCISSORS = "walkNoScissors";
    const string ANIMATION_RUNSTOP_NOSCISSORS = "RunStopNoScissors";
    const string ANIMATION_SKIP_NOSCISSORS = "SkipNoScissors";
    const string ANIMATION_FALLING_NOSCISSORS = "fallingNoScissors";
    const string ANIMATION_JUMP_NOSCISSORS = "jumpNoScissors";
    const string ANIMATION_LANDING_NOSCISSORS = "landingNoScissors";
    const string ANIMATION_RUN_NOSCISSORS = "RunNoScissors";

    const int PARTICLE_SPRINT = 0;
    const int PARTICLE_JUMP = 1;
    const int PARTICLE_REWARD = 2;
    const int PARTICLE_SPLASH = 3;
    const int PARTICLE_WIND = 4;
    const int PARTICLE_FOOTSTEP = 5;
    const int PARTICLE_RUNSTOP = 6;

    bool _windApplied;
    bool _paperplaneApplied;
    bool _affectedByWind = false;
    bool _skinDirty;

    //what track 0 plays outside the state machine's own animations (idle break, sitting, a pose...)
    bool _extraBodyPlaying;
    float _extraBodyEndsAt = -1f; //a one-shot hands back to idle at this time; -1 = until the state changes
    bool _noScissorsApplied;
    bool _scaredFaceWanted;
    bool _scaredFaceApplied;

    float _idleSeconds;
    float _nextIdleBreakAt;
    bool _lookingIntoReflection;
    KamiPose _pose;
    float _poseEndsAt = -1f;
    readonly System.Collections.Generic.HashSet<string> _missingWarned = new System.Collections.Generic.HashSet<string>();

    public PlayerView(Player player)
    {
        _player = player;
        _skeletonAnimation = player.SkeletonAnimation;
        _skeletonAnimation.AnimationState.Event += OnSpineAnimationEvent;
        _skeletonAnimation.BeforeApply += ApplyPendingSkin;
        _player.OnGearChanged += MarkSkinDirty;
        SetBodyAnimation(ANIMATION_IDLE, true);
        ResetIdleClock();
        RefreshOverrides();
    }

    //---------- Gear skin (spec 011) ----------

    void MarkSkinDirty()
    {
        _skinDirty = true;
    }

    //Runs inside SkeletonAnimation's update, right before Spine applies the animations. Swapping the
    //skin here means that same Apply re-hides the scissors keyed empty by the NoScissors animations,
    //so they never flash for a frame. An extra AnimationState.Apply after SetSkin would do that too,
    //but it re-fires every Spine event of the frame (HandleAttack, HandleFootstep), because
    //animationLast only advances in AnimationState.Update.
    void ApplyPendingSkin(ISkeletonAnimation animated)
    {
        if (!_skinDirty)
        {
            return;
        }
        _skinDirty = false;

        GearCatalog catalog = GearCatalog.Instance;
        if (catalog == null)
        {
            return; //the catalog already warned once: Kami keeps the skin she has
        }

        Spine.Skeleton skeleton = _skeletonAnimation.Skeleton;
        skeleton.SetSkin(SpineSkinComposer.Compose(skeleton.Data, _player.Gear.EquippedInCompositionOrder(), catalog));
        skeleton.SetSlotsToSetupPose();
        Debug.Log("[PlayerView] gear skin recomposed");
    }

    private void OnSpineAnimationEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "HandleAttack")
        {
            _player.StartTijeraCoroutine();
        }
        else if (e.Data.Name == "HandleFootstep")
        {
            //antes se ignoraba el paso dentro del viento, como parche contra el falso loop
            //Falling->Landing que dejaba a Kami "tosqueando". El efecto colateral era que Kami
            //caminaba MUDA en el viento. Arreglada la causa raiz (ver Player.GetAffectedByWind),
            //el parche ya no hace falta y recupera el sonido de pasos
            StartPasoSFX(UnityEngine.Random.Range(0, 2));
        }
    }

    //---------- State machine ----------

    public void OnStateChanged(PlayerState previous, PlayerState next)
    {
        //the new state's animation replaces whatever extra track 0 was playing (walking ends a pose)
        ClearExtraBody();
        ResetIdleClock();

        ExitState(previous, next);
        EnterState(previous, next);
        RefreshOverrides();

        // --- Lógica de partículas de sprint: se activan/desactivan según el estado Running ---
        if (next == PlayerState.Running)
        {
            // resincronizo el flip aca tambien: si venimos de un ChangePage, el flip de las sprint
            // particles pudo haber quedado seteado para esa direccion y quedaria pegado en la
            // proxima corrida normal si no lo refrescamos con el facing actual.
            SyncSprintParticlesFlipToFacing();
            SetSprintParticlesState(true);
            AudioManager.instance.Play(AudioId.BootsOn, 2f, 0.01f);
        }
        else if (previous == PlayerState.Running)
        {
            SetSprintParticlesState(false);
            AudioManager.instance.Play(AudioId.BootsOff, 2f, 0.01f);
        }
    }

    void EnterState(PlayerState previous, PlayerState next)
    {
        switch (next)
        {
            case PlayerState.Idle:
                if ((previous == PlayerState.Running || previous == PlayerState.Skipping) && _player.runstopReady)
                {
                    string resolvedRunstop = ResolveAnimationName(ANIMATION_RUNSTOP);
                    string resolvedIdle = ResolveAnimationName(ANIMATION_IDLE);
                    Spine.TrackEntry runstopEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, resolvedRunstop, false);
                    ShootFootAnchorParticles(PARTICLE_RUNSTOP);
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, resolvedIdle, true, runstopEntry.Animation.Duration);
                }
                else
                {
                    SetBodyAnimation(ANIMATION_IDLE, true);
                }
                break;

            case PlayerState.Walking:
                SetBodyAnimation(ANIMATION_WALK, true);
                break;

            case PlayerState.Skipping:
                SetBodyAnimation(ANIMATION_SKIP, true);
                break;

            case PlayerState.Running:
                SetBodyAnimation(ANIMATION_RUN, true);
                break;

            case PlayerState.Jumping:
            {
                if (_player.isPaperPlaneHat)
                    AudioManager.instance.Play(AudioId.Jump_Paperplane, 1f, 0.02f);
                AudioManager.instance.Play(AudioId.JumpStart, 1f, 0.02f);
                ShootFootAnchorParticles(PARTICLE_JUMP);
                Spine.TrackEntry jumpEntry = SetBodyAnimation(ANIMATION_JUMP, false);
                if (jumpEntry != null)
                {
                    jumpEntry.MixDuration = _player.animMix.jumpMixDuration;
                    Debug.Log($"[PlayerView] Jump: mix forzado a {jumpEntry.MixDuration}s hacia '{jumpEntry.Animation.Name}'");
                }
                break;
            }

            case PlayerState.Falling:
                SetBodyAnimation(ANIMATION_FALLING, true);
                break;

            case PlayerState.Landing:
                if (previous == PlayerState.Falling)
                {
                    AudioManager.instance.Play(AudioId.JumpLand, 1f, 0.02f);
                    ShootFootAnchorParticles(PARTICLE_JUMP);
                }
                if (_affectedByWind)
                {
                    string resolvedLanding = ResolveAnimationName(ANIMATION_LANDING);
                    string resolvedIdleWind = ResolveAnimationName(ANIMATION_IDLE);
                    Spine.TrackEntry landingEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, resolvedLanding, false);
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, resolvedIdleWind, true, landingEntry.Animation.Duration);
                }
                else
                {
                    SetBodyAnimation(ANIMATION_LANDING, false);
                }
                break;

            case PlayerState.Casting:
                if (previous == PlayerState.Idle)
                {
                    SetBodyAnimation(ANIMATION_IDLE_TO_CASTING, false);
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_CASTING, true, 0);
                }
                else
                {
                    SetBodyAnimation(ANIMATION_CASTING, true);
                }
                CameraManager.Instance.SetCameraForBeat(CameraMode.OrigamiCasting);
                break;

            case PlayerState.ReceivingReward:
                AudioManager.instance.Play(AudioId.Receive_Reward);
                CameraManager.Instance.SetCameraForBeat(CameraMode.ReceiveReward);
                _player.particleShooter.Enable(PARTICLE_REWARD, true);
                SetBodyAnimation(ANIMATION_RECEIVE_REWARD, false);
                _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_RECEIVE_REWARD_LOOP, true, 0);
                break;

            case PlayerState.Dead:
                SetDeathAnimation(_player.LastDeathCause);
                break;

            case PlayerState.RidingPage:
                if (_skeletonAnimation.Skeleton.Data.FindAnimation(ANIMATION_RIDING_PAGE) != null)
                {
                    // ChangePage: primero pasa por Jump (mix 0f, misma excepcion que el salto normal)
                    // y desde ahi mezcla hacia RidePage con un mix configurable, para que se vea como
                    // que kami "salta" a la hoja y despues queda rideandola.
                    Spine.TrackEntry jumpEntry = SetBodyAnimation(ANIMATION_JUMP, false);
                    if (jumpEntry != null)
                    {
                        jumpEntry.MixDuration = _player.animMix.jumpMixDuration;
                        Spine.TrackEntry rideEntry = _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_RIDING_PAGE, true, jumpEntry.Animation.Duration);
                        rideEntry.MixDuration = _player.animMix.changePageJumpToRideMix;
                        Debug.Log($"[PlayerView] ChangePage: '{jumpEntry.Animation.Name}' (mix-in {jumpEntry.MixDuration}s) -> '{ANIMATION_RIDING_PAGE}' (mix {rideEntry.MixDuration}s)");
                    }
                }
                else
                {
                    Debug.LogWarning($"[PlayerView] el skeleton no trae '{ANIMATION_RIDING_PAGE}' todavia: kami queda en Idle mientras viaja agarrada a la hoja");
                    SetBodyAnimation(ANIMATION_IDLE, true);
                }
                break;

            case PlayerState.Sitting:
                if (HasAnimation(ANIMATION_IDLE_TO_SIT) && HasAnimation(ANIMATION_SIT))
                {
                    Spine.TrackEntry sitDown = PlayExtraBody(ANIMATION_IDLE_TO_SIT, false, false);
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_SIT, true, sitDown.Animation.Duration);
                }
                break;
        }
    }

    void ExitState(PlayerState previous, PlayerState next)
    {
        switch (previous)
        {
            case PlayerState.Casting:
                CameraManager.Instance.SetCameraForBeat(CameraMode.Normal);
                break;
            case PlayerState.ReceivingReward:
                CameraManager.Instance.SetCameraForBeat(CameraMode.Normal);
                _player.particleShooter.Enable(PARTICLE_REWARD, false);
                break;
        }
    }

    string ResolveAnimationName(string animationName)
    {
        if (_player.hasTijera) return animationName;

        string noscissorsName = animationName switch
        {
            ANIMATION_IDLE => ANIMATION_IDLE_NOSCISSORS,
            ANIMATION_WALK => ANIMATION_WALK_NOSCISSORS,
            ANIMATION_RUNSTOP => ANIMATION_RUNSTOP_NOSCISSORS,
            ANIMATION_SKIP => ANIMATION_SKIP_NOSCISSORS,
            ANIMATION_FALLING => ANIMATION_FALLING_NOSCISSORS,
            ANIMATION_JUMP => ANIMATION_JUMP_NOSCISSORS,
            ANIMATION_LANDING => ANIMATION_LANDING_NOSCISSORS,
            ANIMATION_RUN => ANIMATION_RUN_NOSCISSORS,
            _ => animationName
        };

        if (noscissorsName != animationName && _skeletonAnimation.Skeleton.Data.FindAnimation(noscissorsName) == null)
            return animationName;

        return noscissorsName;
    }

    Spine.TrackEntry SetBodyAnimation(string animationName, bool loop)
    {
        if (_player == null || animationName == null)
        {
            Debug.LogWarning("[PlayerView] SetBodyAnimation: _player o animationName es null");
            return null;
        }

        string resolvedName = ResolveAnimationName(animationName);
        return _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, resolvedName, loop);
    }

    void SetDeathAnimation(DeathCause cause)
    {
        string deathAnim = cause == DeathCause.Drowning ? ANIMATION_DROWNING : ANIMATION_DEATH;
        SetBodyAnimation(deathAnim, false);
    }

    //---------- Overrides ----------

    public void RefreshBodyAnimation()
    {
        if (TryGetBodyAnimation(_player.CurrentState, out string bodyAnim, out bool bodyLoop))
        {
            ClearExtraBody();
            ResetIdleClock();
            SetBodyAnimation(bodyAnim, bodyLoop);
            Debug.Log($"[PlayerView] RefreshBodyAnimation en estado {_player.CurrentState} (hasTijera: {_player.hasTijera})");
        }
    }

    public void RefreshOverrides()
    {
        bool allowed = OverridesAllowed(_player.CurrentState);
        SetOverride(TRACK_WIND, ANIMATION_WIND, allowed && _affectedByWind, ref _windApplied);
        SetOverride(TRACK_PAPERPLANE, ANIMATION_PAPERPLANE_OVERRIDE, allowed && _player.isPaperPlaneHat, ref _paperplaneApplied);

        //the scissors stay in her skin when they are confiscated (page 4): the state animations have NoScissors
        //versions that hide them, the Atlas 15 extras don't, so the override hides them on top
        SetOverride(TRACK_NOSCISSORS, ANIMATION_NOSCISSORS_OVERRIDE, _extraBodyPlaying && !_player.hasTijera, ref _noScissorsApplied);
        SetOverride(TRACK_FACE, ANIMATION_SCARED_FACE,
                    _scaredFaceWanted && _player.CurrentState != PlayerState.Dead && HasAnimation(ANIMATION_SCARED_FACE), ref _scaredFaceApplied);
    }

    bool OverridesAllowed(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Idle:
            case PlayerState.Walking:
            case PlayerState.Skipping:
            case PlayerState.Running:
            case PlayerState.Jumping:
            case PlayerState.Falling:
            case PlayerState.Landing:
                return true;
            default:
                return false;
        }
    }

    void SetOverride(int track, string animationName, bool wanted, ref bool applied, float customMixOut = -1f)
    {
        if (wanted == applied) return;
        applied = wanted;

        if (wanted)
            _skeletonAnimation.AnimationState.SetAnimation(track, animationName, true).MixDuration = _player.animMix.overrideMixIn;
        else
        {
            float mixOut = customMixOut >= 0f ? customMixOut : _player.animMix.overrideMixOut;
            _skeletonAnimation.AnimationState.SetEmptyAnimation(track, mixOut);
        }
        Debug.Log($"[PlayerView] override {animationName}: {(wanted ? "ON" : "OFF")} (track {track})");
    }

    //---------- Idle variety, sitting and story poses (Atlas 15) ----------

    /// <summary>Every frame from Player.Update: the idle break, looking into a reflection and a pose's hold time.</summary>
    public void Tick(float deltaTime)
    {
        if (_extraBodyEndsAt >= 0f && Time.time >= _extraBodyEndsAt)
        {
            //a one-shot (the idle break) is over: its AddAnimation already queued the idle, only the overlay is left
            ClearExtraBody();
            RefreshOverrides();
        }

        if (_player.CurrentState != PlayerState.Idle)
        {
            return;
        }

        if (_pose != null)
        {
            if (_poseEndsAt >= 0f && Time.time >= _poseEndsAt)
            {
                EndPose();
            }
            return;
        }

        //a cutscene may be playing its own animation on her body (Level 2's outfit change)
        if (LevelManager.Instance != null && LevelManager.Instance.inCutscene)
        {
            ResetIdleClock();
            return;
        }

        if (_lookingIntoReflection)
        {
            return;
        }

        _idleSeconds += deltaTime;

        if (_player.IsNearReflection)
        {
            if (_idleSeconds >= _player.kamiAnims.lookIntoReflectionDelay && HasAnimation(ANIMATION_LOOK_INTO_REFLECTION))
            {
                PlayExtraBody(ANIMATION_LOOK_INTO_REFLECTION, true, false);
                _lookingIntoReflection = true;
                RefreshOverrides();
                Debug.Log("[PlayerView] looks into the reflection");
            }
            return;
        }

        if (_idleSeconds >= _nextIdleBreakAt && HasAnimation(ANIMATION_IDLE_BREAK))
        {
            Spine.TrackEntry idleBreak = PlayExtraBody(ANIMATION_IDLE_BREAK, false, true);
            _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ResolveAnimationName(ANIMATION_IDLE), true, idleBreak.Animation.Duration);
            RefreshOverrides();
            ResetIdleClock();
            Debug.Log("[PlayerView] idle break");
        }
    }

    /// <summary>Plays a story pose from the Player's kamiAnims.poses. Only while she stands still: walking ends it.</summary>
    public bool PlayPose(string id)
    {
        KamiPose pose = FindPose(id);
        if (pose == null)
        {
            Debug.LogWarning($"[PlayerView] no Kami pose '{id}' in the Player's Kami Anims > Poses: skipped");
            return false;
        }

        if (_player.CurrentState != PlayerState.Idle)
        {
            Debug.LogWarning($"[PlayerView] pose '{id}' skipped: Kami plays poses only while standing still (state {_player.CurrentState})");
            return false;
        }

        if (!HasAnimation(pose.main))
        {
            return false;
        }

        float length;
        if (!string.IsNullOrEmpty(pose.intro) && HasAnimation(pose.intro))
        {
            Spine.TrackEntry intro = PlayExtraBody(pose.intro, false, false);
            Spine.TrackEntry main = _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, pose.main, pose.loop, intro.Animation.Duration);
            length = intro.Animation.Duration + main.Animation.Duration;
        }
        else
        {
            length = PlayExtraBody(pose.main, pose.loop, false).Animation.Duration;
        }

        _pose = pose;
        if (pose.holdSeconds > 0f)
        {
            _poseEndsAt = Time.time + pose.holdSeconds;
        }
        else
        {
            _poseEndsAt = pose.loop ? -1f : Time.time + length;
        }

        RefreshOverrides();
        Debug.Log($"[PlayerView] pose '{pose.id}' ({(pose.loop ? "loops" : "once")}, hold {pose.holdSeconds}s)");
        return true;
    }

    public void EndPose()
    {
        if (_pose == null)
        {
            return;
        }

        Debug.Log($"[PlayerView] pose '{_pose.id}' ends");
        ClearExtraBody();
        ResetIdleClock();
        if (TryGetBodyAnimation(_player.CurrentState, out string bodyAnim, out bool bodyLoop))
        {
            SetBodyAnimation(bodyAnim, bodyLoop);
        }
        RefreshOverrides();
    }

    /// <summary>Sit to Idle on her body. Returns its length: Player switches her to Idle when it is over.</summary>
    public float PlayStandUp()
    {
        if (!HasAnimation(ANIMATION_SIT_TO_IDLE))
        {
            return 0f;
        }

        float duration = PlayExtraBody(ANIMATION_SIT_TO_IDLE, false, false).Animation.Duration;
        RefreshOverrides();
        return duration;
    }

    public void SetScaredFace(bool on)
    {
        _scaredFaceWanted = on;
        RefreshOverrides();
    }

    Spine.TrackEntry PlayExtraBody(string animationName, bool loop, bool backToIdleWhenDone)
    {
        ClearExtraBody();
        Spine.TrackEntry entry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, animationName, loop);
        _extraBodyPlaying = true;
        if (backToIdleWhenDone && !loop)
        {
            _extraBodyEndsAt = Time.time + entry.Animation.Duration;
        }
        return entry;
    }

    void ClearExtraBody()
    {
        _extraBodyPlaying = false;
        _extraBodyEndsAt = -1f;
        _pose = null;
        _poseEndsAt = -1f;
    }

    void ResetIdleClock()
    {
        _idleSeconds = 0f;
        _lookingIntoReflection = false;
        KamiAnimationConfig config = _player.kamiAnims;
        _nextIdleBreakAt = UnityEngine.Random.Range(config.idleBreakMinSeconds, Mathf.Max(config.idleBreakMinSeconds, config.idleBreakMaxSeconds));
    }

    KamiPose FindPose(string id)
    {
        if (string.IsNullOrEmpty(id) || _player.kamiAnims.poses == null)
        {
            return null;
        }

        foreach (KamiPose pose in _player.kamiAnims.poses)
        {
            if (pose != null && string.Equals(pose.id, id, StringComparison.OrdinalIgnoreCase))
            {
                return pose;
            }
        }
        return null;
    }

    //warns once per name: an older export (or a renamed animation) must not spam every frame
    bool HasAnimation(string animationName)
    {
        if (!string.IsNullOrEmpty(animationName) && _skeletonAnimation.Skeleton.Data.FindAnimation(animationName) != null)
        {
            return true;
        }

        if (_missingWarned.Add(animationName ?? ""))
        {
            Debug.LogWarning($"[PlayerView] Kami's skeleton has no animation '{animationName}': that beat is skipped");
        }
        return false;
    }

    //---------- Flip ----------

    public void SetFacing(float hor)
    {
        if (hor > 0.01f)
            _skeletonAnimation.Skeleton.ScaleX = 1f;
        else if (hor < -0.01f)
            _skeletonAnimation.Skeleton.ScaleX = -1f;
    }

    public void ForceFacing(bool faceRight)
    {
        _skeletonAnimation.Skeleton.ScaleX = faceRight ? 1f : -1f;
        _player.SetRideRootOffsetAccordingToForcedFacing(faceRight);
    }

    // ChangePage: footstep (Shoot) y ForceFacing manejan el flip por separado, asi que las sprint
    // particles (que solo se prenden/apagan con Enable, nunca pasan por Shoot) no seguian el ForceFacing
    // y quedaban espejadas. Sincronizo el flip de las sprint particles con el ScaleX ya seteado arriba.
    // Uso solo en el punto de ChangePage (Player.StartRidingPage) para no tocar el resto de los casos.
    public void SyncSprintParticlesFlipToFacing()
    {
        bool flipped = _skeletonAnimation.Skeleton.ScaleX < 0f;
        _player.particleShooter.SetFlip(PARTICLE_SPRINT, flipped);
        Debug.Log($"[PlayerView] ChangePage: sprint particles flip sincronizado (ScaleX={_skeletonAnimation.Skeleton.ScaleX:F0} -> flipped={flipped})");
    }

    //---------- Ataque ----------

    public float PlayPullSolapa(bool closing = false)
    {
        string animName = ANIMATION_PULLSOLAPA;
        if (closing)
        {
            if (_skeletonAnimation.Skeleton.Data.FindAnimation(ANIMATION_PULLSOLAPA_REVERSE) != null)
                animName = ANIMATION_PULLSOLAPA_REVERSE;
            else
                Debug.LogWarning($"[PlayerView] el skeleton no trae '{ANIMATION_PULLSOLAPA_REVERSE}' todavia: el cierre usa '{ANIMATION_PULLSOLAPA}' normal");
        }

        Spine.TrackEntry solapaEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_ATTACK, animName, false);
        solapaEntry.MixDuration = _player.animMix.attackMixIn;
        float duration = solapaEntry.Animation.Duration;
        _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_ATTACK, _player.animMix.attackMixOut, duration);

        Debug.Log($"[PlayerView] pullsolapa triggered con '{animName}' ({(closing ? "cierre" : "apertura")})");
        return duration;
    }

    public void StartAttack()
    {
        AudioManager.instance.Play(AudioId.ActionWind, 1f, 0.01f);

        bool moving = _player.CurrentState != PlayerState.Idle;
        string attackAnim = moving ? ANIMATION_ATTACK_MOVE : ANIMATION_ATTACK;

        Spine.TrackEntry attackEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_ATTACK, attackAnim, false);
        attackEntry.MixDuration = _player.animMix.attackMixIn;
        _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_ATTACK, _player.animMix.attackMixOut, attackEntry.Animation.Duration);

        if (moving)
            _player.StartTijeraCoroutineDelayed();

        _player.StartTijeraParticles();
    }

    public void EndAttack() => _player.StopTijeraParticles();

    //---------- Take hit ----------

    public void PlayTakeHitAnimation()
    {
        if (_player.hitFeedback.hitReplacesBodyAnimation && TryGetBodyAnimation(_player.CurrentState, out string bodyAnim, out bool bodyLoop))
        {
            Spine.TrackEntry hitEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, ANIMATION_TAKE_HIT, false);
            hitEntry.MixDuration = _player.animMix.hitMixIn;
            Spine.TrackEntry backEntry = _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, bodyAnim, bodyLoop, hitEntry.Animation.Duration);
            backEntry.MixDuration = _player.animMix.hitMixOut;
        }
        else
        {
            Spine.TrackEntry hitEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_HIT, ANIMATION_TAKE_HIT, false);
            hitEntry.MixDuration = _player.animMix.hitMixIn;
            _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_HIT, _player.animMix.hitMixOut, hitEntry.Animation.Duration);
            Debug.Log("[PlayerView] hit como OVERRIDE encima del cuerpo");
        }
    }

    bool TryGetBodyAnimation(PlayerState state, out string animationName, out bool loop)
    {
        loop = true;
        switch (state)
        {
            case PlayerState.Idle: animationName = ANIMATION_IDLE; return true;
            case PlayerState.Walking: animationName = ANIMATION_WALK; return true;
            case PlayerState.Skipping: animationName = ANIMATION_SKIP; return true;
            case PlayerState.Running: animationName = ANIMATION_RUN; return true;
            case PlayerState.Falling: animationName = ANIMATION_FALLING; return true;
            case PlayerState.Jumping: animationName = ANIMATION_JUMP; loop = false; return true;
            case PlayerState.Landing: animationName = ANIMATION_LANDING; loop = false; return true;
            default: animationName = null; return false;
        }
    }

    //---------- Sonidos y particulas ----------

    public void StartGetWetAnimation()
    {
        AudioManager.instance.Play(AudioId.BigWaterSplash, 1.2f);
        _player.particleShooter.Shoot(PARTICLE_SPLASH, 0);
    }

    public void StartGetGolpeadoAnimation()
    {
        AudioManager.instance.Play(AudioId.HurtPaper, 1.2f);
    }

    // Método para activar/desactivar partículas de sprint (usado internamente y desde Player)
    public void SetSprintParticlesState(bool state)
    {
        _player.particleShooter.Enable(PARTICLE_SPRINT, state);
    }

    public void StartPasoSFX(int step)
    {
        if (_affectedByWind)
        {
            Debug.Log("[PlayerView] StartPasoSFX paranoia guard: Kami está en viento, retornando sin crear SFX");
            return;
        }

        //the _A / _B bank entries keep the original pairing (clips 1+3 and 2+4) rather than one
        //four-clip entry, so the odds of each clip are exactly what they were before the refactor
        if (_player.isGettingWet)
        {
            switch (step)
            {
                case 0: AudioManager.instance.Play(AudioId.Pasos_KamiMojados_A); break;
                case 1: AudioManager.instance.Play(AudioId.Pasos_KamiMojados_B); break;
                default: break;
            }
            _player.particleShooter.Shoot(PARTICLE_SPLASH, 0);
        }
        else
        {
            switch (step)
            {
                case 0: AudioManager.instance.Play(AudioId.Pasos_Kami_A); break;
                case 1: AudioManager.instance.Play(AudioId.Pasos_Kami_B); break;
                default: AudioManager.instance.Play(AudioId.Pasos_Kami); break;
            }
            ShootFootAnchorParticles(PARTICLE_FOOTSTEP);
        }
    }

    internal void StartAffectedByWind(float windForce, Vector3 windDirection)
    {
        _affectedByWind = true;
        RefreshOverrides();

        Debug.Log("view start affected by wind // windDirection: " + windDirection);
        _player.particleShooter.Enable(PARTICLE_WIND, true);
        _player.particleShooter.particleSystemGameObject[PARTICLE_WIND].transform.forward = windDirection;

        float windForceNormalized = Mathf.InverseLerp(0f, 0.5f, windForce) * 10f;
        _player.particleShooter.particleSystemGameObject[PARTICLE_WIND].GetComponent<ParticleSizeUpdater>()?.UpdateSize(windForceNormalized);
        Debug.Log("windforcenormalized = " + windForceNormalized);
    }

    internal void EndAffectedByWind()
    {
        _affectedByWind = false;
        SetOverride(TRACK_WIND, ANIMATION_WIND, false, ref _windApplied, customMixOut: 0.5f);
        _player.particleShooter.Enable(PARTICLE_WIND, false);
        Debug.Log("[PlayerView] EndAffectedByWind: Wind apagado con suavidad aumentada (mix-out: 0.5s)");
    }

    //---------- Particulas del Footstep Anchor ----------

    void ShootFootAnchorParticles(int index)
    {
        if (_player.footstepAnchor == null)
        {
            Debug.LogWarning("[PlayerView] footstepAnchor es null: no se disparan particulas");
            return;
        }

        float facingY = _skeletonAnimation.Skeleton.ScaleX >= 0 ? 0f : 180f;
        _player.footstepAnchor.localRotation = Quaternion.Euler(0f, facingY, 0f);
        _player.particleShooter.Shoot(index, facingY);
    }
}