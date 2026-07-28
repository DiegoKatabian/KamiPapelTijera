using Spine.Unity;
using System;
using UnityEngine;

public class PlayerView
{
    //el View del playerMVC: animaciones, sonidos y particulas.
    //no decide nada: reacciona a los cambios de estado que le avisa el player (OnStateChanged)
    //y a los eventos que vienen adentro de las animaciones de spine (OnSpineAnimationEvent).

    Player _player;
    Vector3 _lastDirection = Vector3.zero;

    SkeletonAnimation _skeletonAnimation;

    //tracks de spine: el cuerpo va abajo y el resto se superpone encima, en este orden:
    //body < noscissors < wind < attack < paperplane < hit
    //los overrides (noscissors, wind, paperplane) quedan loopeando mientras dure su condicion; attack y hit son one-shots.
    const int TRACK_BODY = 0;
    const int TRACK_NOSCISSORS = 1; //override mientras kami no tiene la tijera
    const int TRACK_WIND = 2; //override mientras kami está siendo afectada por viento
    const int TRACK_ATTACK = 3;
    const int TRACK_PAPERPLANE = 4; //override mientras kami tiene el paper plane hat (va encima del ataque tambien)
    const int TRACK_HIT = 5;
    //los tiempos de mezcla de estos tracks viven en player.animMix (tweakeables en el inspector)

    //en el skeleton (Atlas 4) existen: Attack, AttackMOVE, Casting, falling, Hit, Idle, IdleNoScissors (no se usa),
    //IdleToCasting, jump, jumpComplete, landing, NoScissortsOverride, PaperPlaneOverride, Reward, RewardLoop, Run, Skip, walk, walk2

    const string ANIMATION_IDLE = "Idle";
    const string ANIMATION_WALK = "walk";
    const string ANIMATION_JUMP = "jump";
    const string ANIMATION_FALLING = "falling";
    const string ANIMATION_LANDING = "landing";
    const string ANIMATION_CASTING = "Casting";
    const string ANIMATION_RECEIVE_REWARD = "Reward";
    const string ANIMATION_RECEIVE_REWARD_LOOP = "RewardLoop";

    const string ANIMATION_ATTACK = "Attack";
    const string ANIMATION_ATTACK_MOVE = "AttackMOVE"; //ataque para usar en movimiento o en el aire (no keyea las piernas)
    const string ANIMATION_IDLE_TO_CASTING = "IdleToCasting";
    const string ANIMATION_SKIP = "Skip";
    const string ANIMATION_RUN = "Run";
    const string ANIMATION_TAKE_HIT = "Hit";
    const string ANIMATION_NOSCISSORS_OVERRIDE = "NoScissortsOverride"; //ojo: "Scissorts" es typo del skeleton, no corregir aca
    const string ANIMATION_WIND = "Wind";
    const string ANIMATION_PAPERPLANE_OVERRIDE = "PaperPlaneOverride";
    const string ANIMATION_PULLSOLAPA = "PullSolapas";
    const string ANIMATION_RUNSTOP = "RunStop";
    const string ANIMATION_DEATH = "Death";

    //estado aplicado de cada override, para no re-setear el track (y reiniciar el loop) en cada cambio de estado
    bool _noscissorsApplied;
    bool _windApplied;
    bool _paperplaneApplied;
    bool _affectedByWind = false;

    public PlayerView(Player player)
    {
        _player = player;
        _skeletonAnimation = player.SkeletonAnimation;

        // Subscribe to Spine animation events
        _skeletonAnimation.AnimationState.Event += OnSpineAnimationEvent;

        //arranca en idle (el SetState inicial no dispara OnStateChanged porque ya nace en Idle)
        SetBodyAnimation(ANIMATION_IDLE, true);
        RefreshOverrides(); //si arranca sin tijera, el override noscissors va desde el frame 0
    }

    private void OnSpineAnimationEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "HandleAttack")
        {
            _player.StartTijeraCoroutine();
        }
        else if (e.Data.Name == "HandleFootstep")
        {
            //we could get an int from the animation?
            StartPasoSFX(UnityEngine.Random.Range(0, 2));
        }
    }

    //---------- State machine ----------

    public void OnStateChanged(PlayerState previous, PlayerState next)
    {
        ExitState(previous, next);
        EnterState(previous, next);
        RefreshOverrides(); //los overrides solo van sobre idle/movimiento/aire: entrar a casting/reward/dead los apaga
    }

    void EnterState(PlayerState previous, PlayerState next)
    {
        switch (next)
        {
            case PlayerState.Idle:
                SetBodyAnimation(ANIMATION_IDLE, true);
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
                {
                    AudioManager.instance.PlayByName("Jump_Paperplane", 1f, 0.02f);
                }
                AudioManager.instance.PlayByName("JumpStart", 1f, 0.02f);
                _player.particleShooter.Create(1, _player.particleAnchor);
                SetBodyAnimation(ANIMATION_JUMP, false);
                break;

            case PlayerState.Falling:
                SetBodyAnimation(ANIMATION_FALLING, true);
                break;

            case PlayerState.Landing:
                if (previous == PlayerState.Falling)
                {
                    AudioManager.instance.PlayByName("JumpLand", 1f, 0.02f);
                }
                SetBodyAnimation(ANIMATION_LANDING, false);
                break;

            case PlayerState.Casting:
                if (previous == PlayerState.Idle) //desde idle hay animacion de transicion
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
                _player.particleShooter.Enable(2, true);
                SetBodyAnimation(ANIMATION_RECEIVE_REWARD, false);
                _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_RECEIVE_REWARD_LOOP, true, 0);

                break;

            case PlayerState.Dead:
                Spine.TrackEntry deathEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, ANIMATION_DEATH, false);

                float deathDuration = 2f;
                if (deathEntry?.Animation != null)
                {
                    deathDuration = deathEntry.Animation.Duration;
                    if (deathDuration <= 0)
                    {
                        Debug.LogWarning($"[PlayerView] Death animation duration is {deathDuration}, using fallback 2.0s");
                        deathDuration = 2f;
                    }
                }
                else
                {
                    Debug.LogError($"[PlayerView] Death animation '{ANIMATION_DEATH}' not found or failed to set!");
                    deathDuration = 2f;
                }

                Debug.Log($"[PlayerView] Entering Dead state, overlay will show after {deathDuration}s");
                _player.ShowDefeatOverlayDelayed(deathDuration);
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
                _player.particleShooter.Enable(2, false);
                break;
            case PlayerState.Running:
            case PlayerState.Skipping:
                //si sale de correr/saltar hacia idle, dispara animación de frenada
                if (next == PlayerState.Idle)
                {
                    Spine.TrackEntry runstopEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, ANIMATION_RUNSTOP, false);
                    runstopEntry.MixDuration = 0;
                    float runstopDuration = runstopEntry.Animation.Duration;
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_IDLE, true, runstopDuration);
                    Debug.Log($"[PlayerView] runstop played, duration: {runstopDuration}");
                }
                break;
        }
    }

    void SetBodyAnimation(string animationName, bool loop)
    {
        _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, animationName, loop);
    }

    //---------- Overrides (noscissors y paperplane: loopean encima del cuerpo mientras dure su condicion) ----------

    public void RefreshOverrides()
    {
        //el player me llama cuando cambian hasTijera / isPaperPlaneHat, y yo tambien en cada cambio de estado
        bool allowed = OverridesAllowed(_player.CurrentState);
        SetOverride(TRACK_NOSCISSORS, ANIMATION_NOSCISSORS_OVERRIDE, allowed && !_player.hasTijera, ref _noscissorsApplied);
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
                return false; //casting, reward, dead: el cuerpo hace otra cosa, no van los overrides
        }
    }

    void SetOverride(int track, string animationName, bool wanted, ref bool applied)
    {
        if (wanted == applied)
        {
            return; //ya esta como tiene que estar, no reinicio el loop
        }
        applied = wanted;

        if (wanted)
        {
            _skeletonAnimation.AnimationState.SetAnimation(track, animationName, true).MixDuration = _player.animMix.overrideMixIn;
        }
        else
        {
            _skeletonAnimation.AnimationState.SetEmptyAnimation(track, _player.animMix.overrideMixOut); //mix out suave hacia las anims de abajo
        }
        Debug.Log($"[PlayerView] override {animationName}: {(wanted ? "ON" : "OFF")} (track {track})");
    }

    //---------- Flip ----------

    public void SetFacing(float hor)
    {
        //flip del skeleton segun se mueva a derecha o izquierda. si queda invertido, dar vuelta los signos.
        if (hor > 0.01f)
        {
            _skeletonAnimation.Skeleton.ScaleX = 1f;
        }
        else if (hor < -0.01f)
        {
            _skeletonAnimation.Skeleton.ScaleX = -1f;
        }
    }

    //---------- Ataque (track propio, se superpone al cuerpo) ----------

    public void PlayPullSolapa()
    {
        Spine.TrackEntry solapaEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_ATTACK, ANIMATION_PULLSOLAPA, false);
        solapaEntry.MixDuration = _player.animMix.attackMixIn;
        _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_ATTACK, _player.animMix.attackMixOut, solapaEntry.Animation.Duration);
        Debug.Log($"[PlayerView] pullsolapa triggered");
    }

    public void StartAttack()
    {
        AudioManager.instance.PlayByName("ActionWind", 1f, 0.01f);

        //parado usamos el ataque completo; moviendose o en el aire, AttackMOVE (que no keyea las piernas)
        bool moving = _player.CurrentState != PlayerState.Idle;
        string attackAnim = moving ? ANIMATION_ATTACK_MOVE : ANIMATION_ATTACK;

        //el delay explicito (duracion de la anim) hace que el fade-out arranque recien cuando el ataque TERMINO.
        //con delay 0, spine restaba el mix y se comia el final de la animacion (el bug que reporto animacion).
        Spine.TrackEntry attackEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_ATTACK, attackAnim, false);
        attackEntry.MixDuration = _player.animMix.attackMixIn;
        _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_ATTACK, _player.animMix.attackMixOut, attackEntry.Animation.Duration);

        if (moving)
        {
            //AttackMOVE (todavia) no tiene el evento HandleAttack en el skeleton,
            //asi que la hitbox se dispara por timer imitando el timing del evento de Attack
            _player.StartTijeraCoroutineDelayed();
        }

        Debug.Log($"[PlayerView] ataque con '{attackAnim}' (estado: {_player.CurrentState})");
        _player.StartTijeraParticles();
    }

    public void EndAttack()
    {
        _player.StopTijeraParticles();
    }

    //---------- Take hit ----------

    public void PlayTakeHitAnimation()
    {
        if (_player.hitFeedback.hitReplacesBodyAnimation && TryGetBodyAnimation(_player.CurrentState, out string bodyAnim, out bool bodyLoop))
        {
            Spine.TrackEntry hitEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, ANIMATION_TAKE_HIT, false);
            hitEntry.MixDuration = _player.animMix.hitMixIn;
            Spine.TrackEntry backEntry = _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, bodyAnim, bodyLoop, hitEntry.Animation.Duration);
            backEntry.MixDuration = _player.animMix.hitMixOut;
            Debug.Log($"[PlayerView] hit REEMPLAZA al cuerpo, despues vuelve a '{bodyAnim}'");
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
        //que animacion base corresponde a cada estado, para poder volver a ella despues del hit en modo replace
        loop = true;
        switch (state)
        {
            case PlayerState.Idle:
                animationName = ANIMATION_IDLE;
                return true;
            case PlayerState.Walking:
                animationName = ANIMATION_WALK;
                return true;
            case PlayerState.Skipping:
                animationName = ANIMATION_SKIP;
                return true;
            case PlayerState.Running:
                animationName = ANIMATION_RUN;
                return true;
            case PlayerState.Falling:
                animationName = ANIMATION_FALLING;
                return true;
            case PlayerState.Jumping:
                animationName = ANIMATION_JUMP;
                loop = false;
                return true;
            case PlayerState.Landing:
                animationName = ANIMATION_LANDING;
                loop = false;
                return true;
            default:
                animationName = null; //casting, reward, dead: no pisamos esas anims, el hit va como override
                return false;
        }
    }

    //---------- Sonidos y particulas ----------

    public void StartGetWetAnimation()
    {
        //solo el sonido de arranque
        //los pasos mojados se disparan abajo desde otro metodo
        AudioManager.instance.PlayByName("BigWaterSplash", 1.2f);
    }

    public void StartGetGolpeadoAnimation()
    {
        AudioManager.instance.PlayByName("HurtPaper", 1.2f);
    }

    public void StartSprint()
    {
        //solo particulas y sonido: la animacion de correr la maneja el estado Running
        _player.particleShooter.Enable(0, true);
        AudioManager.instance.PlayByName("BootsOn", 2f, 0.01f);
    }

    public void EndSprint()
    {
        _player.particleShooter.Enable(0, false);
        AudioManager.instance.PlayByName("BootsOff", 2f, 0.01f);
    }

    public void StartPasoSFX(int step)
    {
        if (_player.isGettingWet)
        {
            switch (step)
            {
                case 0:
                    AudioManager.instance.PlayRandom("Pasos_KamiMojados_01", "Pasos_KamiMojados_03");
                    break;
                case 1:
                    AudioManager.instance.PlayRandom("Pasos_KamiMojados_02", "Pasos_KamiMojados_04");
                    break;
                default:
                    break;
            }
            _player.particleShooter.Shoot(3); //la 3 es la particula de splash
        }
        else
        {
            switch (step)
            {
                case 0:
                    AudioManager.instance.PlayRandom("Pasos_Kami_01", "Pasos_Kami_03");
                    break;
                case 1:
                    AudioManager.instance.PlayRandom("Pasos_Kami_02", "Pasos_Kami_04");
                    break;
                default:
                    AudioManager.instance.PlayRandom("Pasos_Kami_01", "Pasos_Kami_02", "Pasos_Kami_03", "Pasos_Kami_04");
                    break;
            }
        }
    }

    internal void StartAffectedByWind(float windForce, Vector3 windDirection)
    {
        _affectedByWind = true;
        RefreshOverrides();

        Debug.Log("view start affected by wind // windDirection: " + windDirection);
        _player.particleShooter.Enable(4, true);

        //rotate particle system so that it faces the wind direction
        _player.particleShooter.particleSystemGameObject[4].transform.forward = windDirection;

        float minWindForce = 0f;
        float maxWindForce = 0.5f;
        float minParticleSize = 0f;
        float maxParticleSize = 10f;

        float windForceNormalized = (windForce - minWindForce) / (maxWindForce - minWindForce);
        windForceNormalized = minParticleSize + (windForceNormalized * (maxParticleSize - minParticleSize));

        _player.particleShooter.particleSystemGameObject[4].GetComponent<ParticleSizeUpdater>()?.UpdateSize(windForceNormalized);
        Debug.Log("windforcenormalized = " + windForceNormalized);
    }

    internal void EndAffectedByWind()
    {
        _affectedByWind = false;
        RefreshOverrides();
        _player.particleShooter.Enable(4, false);
    }
}
