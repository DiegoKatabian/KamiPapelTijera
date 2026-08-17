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

    const string ANIMATION_IDLE_NOSCISSORS = "IdleNoScissors";
    const string ANIMATION_WALK_NOSCISSORS = "walkNoScissors";
    const string ANIMATION_RUNSTOP_NOSCISSORS = "RunStopNoScissors";
    const string ANIMATION_SKIP_NOSCISSORS = "SkipNoScissors";
    const string ANIMATION_FALLING_NOSCISSORS = "fallingNoScissors";
    const string ANIMATION_JUMP_NOSCISSORS = "jumpNoScissors";
    const string ANIMATION_LANDING_NOSCISSORS = "landingNoScissors";

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

    public PlayerView(Player player)
    {
        _player = player;
        _skeletonAnimation = player.SkeletonAnimation;
        _skeletonAnimation.AnimationState.Event += OnSpineAnimationEvent;
        SetBodyAnimation(ANIMATION_IDLE, true);
        RefreshOverrides();
    }

    private void OnSpineAnimationEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "HandleAttack")
        {
            _player.StartTijeraCoroutine();
        }
        else if (e.Data.Name == "HandleFootstep" && !_affectedByWind)
        {
            StartPasoSFX(UnityEngine.Random.Range(0, 2));
        }
        else if (e.Data.Name == "HandleFootstep" && _affectedByWind)
        {
            // footstep ignorado en viento
        }
    }

    //---------- State machine ----------

    public void OnStateChanged(PlayerState previous, PlayerState next)
    {
        ExitState(previous, next);
        EnterState(previous, next);
        RefreshOverrides();

        // --- Lógica de partículas de sprint: se activan/desactivan según el estado Running ---
        if (next == PlayerState.Running)
        {
            SetSprintParticlesState(true);
            AudioManager.instance.PlayByName("BootsOn", 2f, 0.01f);
        }
        else if (previous == PlayerState.Running)
        {
            SetSprintParticlesState(false);
            AudioManager.instance.PlayByName("BootsOff", 2f, 0.01f);
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
                if (_player.isPaperPlaneHat)
                    AudioManager.instance.PlayByName("Jump_Paperplane", 1f, 0.02f);
                AudioManager.instance.PlayByName("JumpStart", 1f, 0.02f);
                ShootFootAnchorParticles(PARTICLE_JUMP);
                SetBodyAnimation(ANIMATION_JUMP, false);
                break;

            case PlayerState.Falling:
                SetBodyAnimation(ANIMATION_FALLING, true);
                break;

            case PlayerState.Landing:
                if (previous == PlayerState.Falling)
                {
                    AudioManager.instance.PlayByName("JumpLand", 1f, 0.02f);
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
                CameraManager.Instance.SetCamera(CameraMode.OrigamiCasting);
                break;

            case PlayerState.ReceivingReward:
                AudioManager.instance.PlayByName("Receive_Reward");
                CameraManager.Instance.SetCamera(CameraMode.ReceiveReward);
                _player.particleShooter.Enable(PARTICLE_REWARD, true);
                SetBodyAnimation(ANIMATION_RECEIVE_REWARD, false);
                _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_RECEIVE_REWARD_LOOP, true, 0);
                break;

            case PlayerState.Dead:
                SetDeathAnimation(_player.LastDeathCause);
                break;

            case PlayerState.RidingPage:
                if (_skeletonAnimation.Skeleton.Data.FindAnimation(ANIMATION_RIDING_PAGE) != null)
                    SetBodyAnimation(ANIMATION_RIDING_PAGE, true);
                else
                {
                    Debug.LogWarning($"[PlayerView] el skeleton no trae '{ANIMATION_RIDING_PAGE}' todavia: kami queda en Idle mientras viaja agarrada a la hoja");
                    SetBodyAnimation(ANIMATION_IDLE, true);
                }
                break;
        }
    }

    void ExitState(PlayerState previous, PlayerState next)
    {
        switch (previous)
        {
            case PlayerState.Casting:
                CameraManager.Instance.SetCamera(CameraMode.Normal);
                break;
            case PlayerState.ReceivingReward:
                CameraManager.Instance.SetCamera(CameraMode.Normal);
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
            _ => animationName
        };

        if (noscissorsName != animationName && _skeletonAnimation.Skeleton.Data.FindAnimation(noscissorsName) == null)
            return animationName;

        return noscissorsName;
    }

    void SetBodyAnimation(string animationName, bool loop)
    {
        if (_player == null || animationName == null)
        {
            Debug.LogWarning("[PlayerView] SetBodyAnimation: _player o animationName es null");
            return;
        }

        string resolvedName = ResolveAnimationName(animationName);
        _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, resolvedName, loop);
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
            SetBodyAnimation(bodyAnim, bodyLoop);
            Debug.Log($"[PlayerView] RefreshBodyAnimation en estado {_player.CurrentState} (hasTijera: {_player.hasTijera})");
        }
    }

    public void RefreshOverrides()
    {
        bool allowed = OverridesAllowed(_player.CurrentState);
        SetOverride(TRACK_WIND, ANIMATION_WIND, allowed && _affectedByWind, ref _windApplied);
        SetOverride(TRACK_PAPERPLANE, ANIMATION_PAPERPLANE_OVERRIDE, allowed && _player.isPaperPlaneHat, ref _paperplaneApplied);
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
        AudioManager.instance.PlayByName("ActionWind", 1f, 0.01f);

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
        AudioManager.instance.PlayByName("BigWaterSplash", 1.2f);
        _player.particleShooter.Shoot(PARTICLE_SPLASH, 0);
    }

    public void StartGetGolpeadoAnimation()
    {
        AudioManager.instance.PlayByName("HurtPaper", 1.2f);
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

        if (_player.isGettingWet)
        {
            switch (step)
            {
                case 0: AudioManager.instance.PlayRandom("Pasos_KamiMojados_01", "Pasos_KamiMojados_03"); break;
                case 1: AudioManager.instance.PlayRandom("Pasos_KamiMojados_02", "Pasos_KamiMojados_04"); break;
                default: break;
            }
            _player.particleShooter.Shoot(PARTICLE_SPLASH, 0);
        }
        else
        {
            switch (step)
            {
                case 0: AudioManager.instance.PlayRandom("Pasos_Kami_01", "Pasos_Kami_03"); break;
                case 1: AudioManager.instance.PlayRandom("Pasos_Kami_02", "Pasos_Kami_04"); break;
                default: AudioManager.instance.PlayRandom("Pasos_Kami_01", "Pasos_Kami_02", "Pasos_Kami_03", "Pasos_Kami_04"); break;
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