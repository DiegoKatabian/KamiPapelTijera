using Spine.Unity;
using System;
using UnityEngine;

public class PlayerView
{
    //el View del playerMVC: animaciones, sonidos y particulas.
    //no decide nada: reacciona a los cambios de estado que le avisa el player (OnStateChanged)
    //y a los eventos que vienen adentro de las animaciones de spine (OnSpineAnimationEvent).

    Animator _anim; //el animator del modelo 3d viejo. ya no maneja animaciones: solo lo usamos como anchor de transform para particulas
    Player _player;
    Vector3 _lastDirection = Vector3.zero;

    SkeletonAnimation _skeletonAnimation;

    //tracks de spine: el cuerpo va abajo, y ataque/golpe se superponen encima
    const int TRACK_BODY = 0;
    const int TRACK_ATTACK = 1;
    const int TRACK_HIT = 2;
    const float TRACK_MIX_OUT = 0.2f; //cuanto tarda en desvanecerse un track superpuesto al terminar

    //THEY ARE Attack, Casting, Idle, IdleToCasting, ReceiveReward, Run, Skip, falling, jump, jumpComplete, landing, walk

    const string ANIMATION_IDLE = "Idle";
    const string ANIMATION_WALK = "walk";
    const string ANIMATION_JUMP = "jump";
    const string ANIMATION_FALLING = "falling";
    const string ANIMATION_LANDING = "landing";
    const string ANIMATION_CASTING = "Casting";
    const string ANIMATION_RECEIVE_REWARD = "ReceiveReward";
    const string ANIMATION_ATTACK = "Attack";
    const string ANIMATION_IDLE_TO_CASTING = "IdleToCasting";
    const string ANIMATION_SKIP = "Skip";
    //const string ANIMATION_JUMP_COMPLETE = "jumpComplete";
    const string ANIMATION_RUN = "Run";
    const string ANIMATION_TAKE_HIT = "TakeHit"; //TODO: todavia no existe en el skeleton. cuando la anim girl la agregue, funciona solo

    public PlayerView(Player player)
    {
        _anim = player.anim;
        _player = player;
        _skeletonAnimation = player.SkeletonAnimation;

        // Subscribe to Spine animation events
        _skeletonAnimation.AnimationState.Event += OnSpineAnimationEvent;

        //arranca en idle (el SetState inicial no dispara OnStateChanged porque ya nace en Idle)
        SetBodyAnimation(ANIMATION_IDLE, true);
    }

    private void OnSpineAnimationEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "StartTijeraCoroutine")
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
        ExitState(previous);
        EnterState(previous, next);
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
                _player.particleShooter.Create(1, _anim.transform);
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
                break;

            case PlayerState.ReceivingReward:
                AudioManager.instance.PlayByName("Receive_Reward");
                CameraManager.Instance.SetCamera(CameraMode.ReceiveReward);
                _player.particleShooter.Enable(2, true);
                SetBodyAnimation(ANIMATION_RECEIVE_REWARD, true);
                break;

            case PlayerState.Dead:
                //TODO: animacion de muerte cuando exista en el skeleton
                break;
        }
    }

    void ExitState(PlayerState previous)
    {
        switch (previous)
        {
            case PlayerState.ReceivingReward:
                CameraManager.Instance.SetCamera(CameraMode.Normal);
                _player.particleShooter.Enable(2, false);
                break;
        }
    }

    void SetBodyAnimation(string animationName, bool loop)
    {
        _skeletonAnimation.AnimationState.SetAnimation(TRACK_BODY, animationName, loop);
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

    //---------- Ataque (track 1, se superpone al cuerpo) ----------

    public void StartAttack()
    {
        AudioManager.instance.PlayByName("ActionWind", 1f, 0.01f);
        _skeletonAnimation.AnimationState.SetAnimation(TRACK_ATTACK, ANIMATION_ATTACK, false);
        _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_ATTACK, TRACK_MIX_OUT, 0f); //limpia el track al terminar
        _player.StartTijeraParticles();
    }

    public void EndAttack()
    {
        _player.StopTijeraParticles();
    }

    //---------- Take hit (track 2, se superpone a todo) ----------

    public void PlayTakeHitAnimation()
    {
        if (_skeletonAnimation.Skeleton.Data.FindAnimation(ANIMATION_TAKE_HIT) == null)
        {
            return; //TODO: sacar este guard cuando la animacion exista en el skeleton
        }

        _skeletonAnimation.AnimationState.SetAnimation(TRACK_HIT, ANIMATION_TAKE_HIT, false);
        _skeletonAnimation.AnimationState.AddEmptyAnimation(TRACK_HIT, TRACK_MIX_OUT, 0f);
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

    public void EnableTijeraParticles()
    {
        _player.tijeraParticles.gameObject.SetActive(true);
    }

    public void DisableTijeraParticles()
    {
        _player.tijeraParticles.gameObject.SetActive(false);
    }

    internal void StartAffectedByWind(float windForce, Vector3 windDirection)
    {
        Debug.Log("view start affected by wind // windDirection: " + windDirection);
        _player.particleShooter.Enable(4, true);

        //rotate _player.particleShooter.particleSystemGameObject[4] so that it faces the wind direction
        _player.particleShooter.particleSystemGameObject[4].transform.forward = windDirection;


        float minWindForce = 0f;
        float maxWindForce = 0.5f;
        float minParticleSize = 0f;
        float maxParticleSize = 10f;

        float windForceNormalized = (windForce - minWindForce) / (maxWindForce - minWindForce);

        //now scale windforcenormalized to the range of minparticlesize to maxparticlesize
        windForceNormalized = minParticleSize + (windForceNormalized * (maxParticleSize - minParticleSize));

        _player.particleShooter.particleSystemGameObject[4].GetComponent<ParticleSizeUpdater>()?.UpdateSize(windForceNormalized);
        Debug.Log("windforcenormalized = " + windForceNormalized);

    }
    internal void EndAffectedByWind()
    {
        //Debug.Log("view end affected by wind");
        _player.particleShooter.Enable(4, false);
    }
}
