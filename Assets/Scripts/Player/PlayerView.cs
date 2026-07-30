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
    const string ANIMATION_PULLSOLAPA = "PullSolapas"; //abrir solapa
    const string ANIMATION_PULLSOLAPA_REVERSE = "DownSolapas"; //cerrar solapa (pendiente de exportar del skeleton; mientras no exista se cae a PullSolapas)
    const string ANIMATION_RUNSTOP = "RunStop";
    const string ANIMATION_DEATH = "Death";

    //versiones sin tijera de las 7 animaciones principales (ahora se resuelven automaticamente en SetBodyAnimation)
    const string ANIMATION_IDLE_NOSCISSORS = "IdleNoScissors";
    const string ANIMATION_WALK_NOSCISSORS = "walkNoScissors";
    const string ANIMATION_RUNSTOP_NOSCISSORS = "RunStopNoScissors";
    const string ANIMATION_SKIP_NOSCISSORS = "SkipNoScissors";
    const string ANIMATION_FALLING_NOSCISSORS = "fallingNoScissors";
    const string ANIMATION_JUMP_NOSCISSORS = "jumpNoScissors";
    const string ANIMATION_LANDING_NOSCISSORS = "landingNoScissors";

    //indices del array particleSystemGameObject del ParticleShooter (el orden vive en el prefab de Kami)
    const int PARTICLE_SPRINT = 0;
    const int PARTICLE_JUMP = 1; //se usa para salto Y aterrizaje
    const int PARTICLE_REWARD = 2;
    const int PARTICLE_SPLASH = 3; //pasos sobre agua
    const int PARTICLE_WIND = 4;
    const int PARTICLE_FOOTSTEP = 5; //pasos secos
    const int PARTICLE_RUNSTOP = 6; //frenada en seco

    //estado aplicado de cada override, para no re-setear el track (y reiniciar el loop) en cada cambio de estado
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
        else if (e.Data.Name == "HandleFootstep" && !_affectedByWind)
        {
            //we could get an int from the animation?
            StartPasoSFX(UnityEngine.Random.Range(0, 2));
            Debug.Log("[PlayerView] footstep SFX/particle disparado");
        }
        else if (e.Data.Name == "HandleFootstep" && _affectedByWind)
        {
            Debug.Log("[PlayerView] footstep ignorado: Kami está siendo arrastrada por viento");
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
                //frenada en seco: solo si venia a velocidad maxima hace un rato (runstopReady, lo arma el model
                //con runstopMinFullSpeedTime). runstop entero y recien despues idle (encolado, asi nadie lo pisa).
                //si el player vuelve a moverse en el medio, el proximo cambio de estado lo interrumpe solo.
                if ((previous == PlayerState.Running || previous == PlayerState.Skipping) && _player.runstopReady)
                {
                    Spine.TrackEntry runstopEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, ANIMATION_RUNSTOP, false);
                    _player.particleShooter.Create(PARTICLE_RUNSTOP, GetRunstopParticlePosition());
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_IDLE, true, runstopEntry.Animation.Duration);
                    Debug.Log($"[PlayerView] runstop -> idle (duracion {runstopEntry.Animation.Duration:F2}s)");
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
                {
                    AudioManager.instance.PlayByName("Jump_Paperplane", 1f, 0.02f);
                }
                AudioManager.instance.PlayByName("JumpStart", 1f, 0.02f);
                _player.particleShooter.Create(PARTICLE_JUMP, _player.FeetPosition); //en los pies, world-space (el anchor del hueso Smoke quedaba a la altura del torso)
                SetBodyAnimation(ANIMATION_JUMP, false);
                break;

            case PlayerState.Falling:
                SetBodyAnimation(ANIMATION_FALLING, true);
                break;

            case PlayerState.Landing:
                if (previous == PlayerState.Falling)
                {
                    AudioManager.instance.PlayByName("JumpLand", 1f, 0.02f);
                    _player.particleShooter.Create(PARTICLE_JUMP, _player.FeetPosition); //polvito de aterrizaje en los pies
                }
                if (_affectedByWind)
                {
                    //Si kami está siendo arrastrada por viento, Landing es one-shot pero después encolo Idle
                    //para que no quede colgada sin animación mientras espera el siguiente cambio de estado.
                    Spine.TrackEntry landingEntry = _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, ANIMATION_LANDING, false);
                    _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_IDLE, true, landingEntry.Animation.Duration);
                    Debug.Log($"[PlayerView] landing en viento: encolo Idle despues (duracion landing {landingEntry.Animation.Duration:F2}s)");
                }
                else
                {
                    SetBodyAnimation(ANIMATION_LANDING, false);
                }
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
                _player.particleShooter.Enable(PARTICLE_REWARD, true);
                SetBodyAnimation(ANIMATION_RECEIVE_REWARD, false);
                _skeletonAnimation.AnimationState.AddAnimation(TRACK_BODY, ANIMATION_RECEIVE_REWARD_LOOP, true, 0);

                break;

            case PlayerState.Dead:
                //non-loop y sin nada encolado despues: spine deja el ultimo frame puesto hasta que el respawn ponga Idle.
                //el timing del overlay y el respawn viven en Player.Die() / OverlayManager, no aca.
                SetBodyAnimation(ANIMATION_DEATH, false);
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
        //resuelve si usar la version normal o NoScissors segun hasTijera en el momento
        //si !hasTijera, intenta sustituir por version NoScissors; si no existe, usa la normal

        if (_player.hasTijera)
        {
            return animationName; //con tijera, siempre la version normal
        }

        //sin tijera: intenta usar la version NoScissors para las 7 anims principales
        string noscissorsName = animationName switch
        {
            ANIMATION_IDLE => ANIMATION_IDLE_NOSCISSORS,
            ANIMATION_WALK => ANIMATION_WALK_NOSCISSORS,
            ANIMATION_RUNSTOP => ANIMATION_RUNSTOP_NOSCISSORS,
            ANIMATION_SKIP => ANIMATION_SKIP_NOSCISSORS,
            ANIMATION_FALLING => ANIMATION_FALLING_NOSCISSORS,
            ANIMATION_JUMP => ANIMATION_JUMP_NOSCISSORS,
            ANIMATION_LANDING => ANIMATION_LANDING_NOSCISSORS,
            _ => animationName //otras anims (Attack, Casting, etc.) sin version NoScissors
        };

        //si la version NoScissors existe en el skeleton, usarla; sino fallback a la normal
        if (noscissorsName != animationName && _skeletonAnimation.Skeleton.Data.FindAnimation(noscissorsName) == null)
        {
            Debug.LogWarning($"[PlayerView] skeleton no tiene '{noscissorsName}', usando '{animationName}' en su lugar");
            return animationName;
        }

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
        if (resolvedName != animationName)
        {
            Debug.Log($"[PlayerView] anim resuelta: {animationName} -> {resolvedName} (hasTijera: {_player.hasTijera})");
        }

        _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, resolvedName, loop);
    }

    //---------- Overrides (noscissors y paperplane: loopean encima del cuerpo mientras dure su condicion) ----------

    public void RefreshBodyAnimation()
    {
        //se llama cuando hasTijera cambia mid-state para actualizar la anim del cuerpo
        //de la version NoScissors a la normal (o viceversa, si algun dia el player pierde la tijera)
        if (TryGetBodyAnimation(_player.CurrentState, out string bodyAnim, out bool bodyLoop))
        {
            SetBodyAnimation(bodyAnim, bodyLoop);
            Debug.Log($"[PlayerView] RefreshBodyAnimation en estado {_player.CurrentState} (hasTijera: {_player.hasTijera})");
        }
    }

    public void RefreshOverrides()
    {
        //el player me llama cuando cambian hasTijera / isPaperPlaneHat, y yo tambien en cada cambio de estado
        //nota: el override noscissors fue eliminado; ahora SetBodyAnimation() resuelve automaticamente
        //cual version de la anim usar (normal o NoScissors) segun hasTijera en el momento
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
                return false; //casting, reward, dead: el cuerpo hace otra cosa, no van los overrides
        }
    }

    void SetOverride(int track, string animationName, bool wanted, ref bool applied, float customMixOut = -1f)
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
            float mixOut = customMixOut >= 0f ? customMixOut : _player.animMix.overrideMixOut;
            _skeletonAnimation.AnimationState.SetEmptyAnimation(track, mixOut); //mix out suave hacia las anims de abajo
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

    public float PlayPullSolapa(bool closing = false)
    {
        //devuelve la duracion para que player sepa cuanto bloquear el movimiento.
        //closing = true pide la anim en reversa (cerrar la solapa).
        //apertura usa PullSolapas; cierre usa PullSolapasReverse (export dedicado del skeleton).
        //si el skeleton todavia no trae la de cierre, caemos a la normal con un warning (asi el juego no explota con atlas viejos).
        string animName = ANIMATION_PULLSOLAPA;
        if (closing)
        {
            if (_skeletonAnimation.Skeleton.Data.FindAnimation(ANIMATION_PULLSOLAPA_REVERSE) != null)
            {
                animName = ANIMATION_PULLSOLAPA_REVERSE;
            }
            else
            {
                Debug.LogWarning($"[PlayerView] el skeleton no trae '{ANIMATION_PULLSOLAPA_REVERSE}' todavia: el cierre usa '{ANIMATION_PULLSOLAPA}' normal");
            }
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
            //fallback por timer para atlas viejos sin el evento HandleAttack en AttackMOVE.
            //desde Atlas 5 el evento existe y attackMoveHitboxDelay va en -1 (la corrutina sale sola)
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
        _player.particleShooter.Enable(PARTICLE_SPRINT, true);
        AudioManager.instance.PlayByName("BootsOn", 2f, 0.01f);
    }

    public void EndSprint()
    {
        _player.particleShooter.Enable(PARTICLE_SPRINT, false);
        AudioManager.instance.PlayByName("BootsOff", 2f, 0.01f);
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
                case 0:
                    AudioManager.instance.PlayRandom("Pasos_KamiMojados_01", "Pasos_KamiMojados_03");
                    break;
                case 1:
                    AudioManager.instance.PlayRandom("Pasos_KamiMojados_02", "Pasos_KamiMojados_04");
                    break;
                default:
                    break;
            }
            _player.particleShooter.Shoot(PARTICLE_SPLASH);
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

            //polvito seco en los pies en cada paso. ojo performance: los pasos son frecuentes y Create
            //instancia + destruye con corrutina a los 2s; para este juego alcanza, pero si algun dia molesta
            //en el profiler, este es el candidato numero uno a pooling.
            _player.particleShooter.Create(PARTICLE_FOOTSTEP, _player.FeetPosition);
        }
    }

    internal void StartAffectedByWind(float windForce, Vector3 windDirection)
    {
        _affectedByWind = true;
        RefreshOverrides();

        Debug.Log("view start affected by wind // windDirection: " + windDirection);
        _player.particleShooter.Enable(PARTICLE_WIND, true);

        //rotate particle system so that it faces the wind direction
        _player.particleShooter.particleSystemGameObject[PARTICLE_WIND].transform.forward = windDirection;

        float minWindForce = 0f;
        float maxWindForce = 0.5f;
        float minParticleSize = 0f;
        float maxParticleSize = 10f;

        float windForceNormalized = (windForce - minWindForce) / (maxWindForce - minWindForce);
        windForceNormalized = minParticleSize + (windForceNormalized * (maxParticleSize - minParticleSize));

        _player.particleShooter.particleSystemGameObject[PARTICLE_WIND].GetComponent<ParticleSizeUpdater>()?.UpdateSize(windForceNormalized);
        Debug.Log("windforcenormalized = " + windForceNormalized);
    }

    internal void EndAffectedByWind()
    {
        _affectedByWind = false;
        // Apagar Wind con suavidad aumentada (0.5s de fade-out) para que no sea abrupto
        SetOverride(TRACK_WIND, ANIMATION_WIND, false, ref _windApplied, customMixOut: 0.5f);
        _player.particleShooter.Enable(PARTICLE_WIND, false);
        Debug.Log("[PlayerView] EndAffectedByWind: Wind apagado con suavidad aumentada (mix-out: 0.5s)");
    }

    //---------- Runstop particles ----------

    Vector3 GetRunstopParticlePosition()
    {
        if (_skeletonAnimation == null)
        {
            Debug.LogWarning("[PlayerView] GetRunstopParticlePosition: _skeletonAnimation es null, devuelvo FeetPosition sin offset");
            return _player.FeetPosition;
        }

        //determinar la dirección forward segun el facing actual
        Vector3 forward = _skeletonAnimation.Skeleton.ScaleX > 0 ? Vector3.forward : Vector3.back;
        float offset = 0.5f; //tuneable: qué tan delante de los pies disparar las partículas
        Vector3 position = _player.FeetPosition + forward * offset;

        Debug.Log($"[PlayerView] runstop particles fired at {position} (FeetPosition: {_player.FeetPosition}, ScaleX: {_skeletonAnimation.Skeleton.ScaleX})");
        return position;
    }
}
