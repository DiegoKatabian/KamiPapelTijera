using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;

public class PlayerView
{
    Animator _anim;
    Player _player;
    bool canRotate;
    Vector3 _lastDirection = Vector3.zero;
    public bool tabIsPressed;

    SkeletonAnimation _skeletonAnimation;

    // Track movement state to only trigger animations on state change
    bool _isCurrentlyMoving = false;

    //THEY ARE Attack, Casting, Idle, IdleToCasting, ReceiveReward, Run, Skip, falling, jump, jumpComplete, landing, walk

    const string ANIMATION_IDLE = "Idle";
    //const string ANIMATION_WALK = "walk";
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

    public PlayerView(Player player)
    {
        _anim = player.anim;
        _player = player;
        _skeletonAnimation = player.SkeletonAnimation;

        // Subscribe to Spine animation events
        _skeletonAnimation.AnimationState.Event += OnSpineAnimationEvent;
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

    public void CheckMagnitude(float hor, float ver)
    {
        bool isMoving = hor != 0 || ver != 0;
        
             // Only trigger animation when state changes
             if (isMoving != _isCurrentlyMoving)
                 {
            _isCurrentlyMoving = isMoving;
            
                     if (isMoving)
                         {
                StartMoveAnimation();
                canRotate = true;
                         }
                     else
                         {
                StartIdleAnimation();
                canRotate = false;
                         }
                 }
    }

    public void CheckCanRotateModel(Vector3 move)
    {
        if (canRotate)
        {
            RotateModel(move);
        }
    }

    public void RotateModel(Vector3 move)
    {
        _lastDirection = new Vector3(move.x, _anim.transform.forward.y, move.z);
        _anim.transform.forward = _lastDirection;
        _player.lastDirection = _lastDirection;
    }

    public void StartTijeraAnimation()
    {
        _anim.SetTrigger("isAttack");
    }

    public void StartAttack()
    {
        Debug.Log("start attack");
        _anim.SetBool("isAttacking", true);
        AudioManager.instance.PlayByName("ActionWind", 1f, 0.01f);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_ATTACK, false);
        _player.StartTijeraParticles();
    }

    public void EndAttack()
    {
        Debug.Log("end attack");
        _anim.SetBool("isAttacking", false);
        _player.StopTijeraParticles();
    }

    public void StartGetWetAnimation()
    {
        //solo el sonido de arranque
        //los pasos mojados se disparan abajo desde otro metodo
        AudioManager.instance.PlayByName("BigWaterSplash", 1.2f);
        //animacioncita de asco con las manos
        //_skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_GETWET, true);

    }

    public void StartGetGolpeadoAnimation()
    {
        AudioManager.instance.PlayByName("HurtPaper", 1.2f);
        //_skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_GETHURT, true);
        
    }

    public void StartMoveAnimation()
    {
        Debug.Log("start move animation");
        _anim.SetBool("isWalk", true);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_SKIP, true);

    }

    public void StartIdleAnimation()
    {
        Debug.Log("start idle animation");
        _anim.SetBool("isWalk", false);
        _anim.SetTrigger("Idle");
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_IDLE, true);
    }

    public void StartJumpAnimation(bool isPaperPlaneHat)
    {
        Debug.Log("start jump animation");
        //Debug.Log("anim start jump");
        if (isPaperPlaneHat)
        {
            AudioManager.instance.PlayByName("Jump_Paperplane", 1f, 0.02f);
        }
        AudioManager.instance.PlayByName("JumpStart", 1f, 0.02f);
        _player.particleShooter.Create(1, _anim.transform);
        _anim.SetBool("isWalk", false);
        _anim.SetBool("isJump", true);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_JUMP, false);

    }
    public void StopJump()
    {
        Debug.Log("stop jump");
        //Debug.Log("anim stop jump");
        _anim.SetBool("isJump", false);
    }

    public void StartFalling()
    {
        Debug.Log("start falling");
        //Debug.Log("anim start falling");
        _anim.SetBool("isFalling", true);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_FALLING, true);
    }
    public void StopFalling()
    {
        Debug.Log("stop falling");
        //Debug.Log("anim stop falling");
        _anim.SetBool("isFalling", false);
    }

    public void StartLanding()
    {
        Debug.Log("start landing");
        //Debug.Log("anim start landing");
        _anim.SetBool("isLanding", true);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_LANDING, false);
    }
    public void StopLanding()
    {
        Debug.Log("stop landing");
        //Debug.Log("anim stop landing");
        _anim.SetBool("isLanding", false);

        if (_isCurrentlyMoving)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_SKIP, true);
        }
        else
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_IDLE, true);
        }
    }

    public void StartCast()
    {
        Debug.Log("start cast");
        _anim.SetBool("isCasting", true);

        if (!_isCurrentlyMoving)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_IDLE_TO_CASTING, false);
            _skeletonAnimation.AnimationState.AddAnimation(0, ANIMATION_CASTING, true, 0);
        }
        else
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_CASTING, true);
        }
    }

    public void EndCast()
    {
        Debug.Log("end cast");
        _anim.SetBool("isCasting", false);

        if (_isCurrentlyMoving)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_SKIP, true);
        }
        else
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_IDLE, true);
        }
    }

    public void StartSprint()
    {
        Debug.Log("start sprint");
        _player.particleShooter.Enable(0, true);
        AudioManager.instance.PlayByName("BootsOn", 2f, 0.01f);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_RUN, true);
    }

    public void EndSprint()
    {
        Debug.Log("end sprint");
        _player.particleShooter.Enable(0, false);
        AudioManager.instance.PlayByName("BootsOff", 2f, 0.01f);

        if (_isCurrentlyMoving)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_SKIP, true);
        }
        else
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_IDLE, true);
        }
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
            switch(step)
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

    public void StartReceiveReward()
    {
        Debug.Log("start receive reward");
        AudioManager.instance.PlayByName("Receive_Reward");
        CameraManager.Instance.SetCamera(CameraMode.ReceiveReward);
        RotateModel(Vector3.back);
        _anim.SetBool("isReceivingReward", true);
        _player.particleShooter.Enable(2, true);
        _skeletonAnimation.AnimationState.SetAnimation(0, ANIMATION_RECEIVE_REWARD, true);
        //_player.rewardSticker.gameObject.SetActive(true);
        //_player.rewardSticker.StartLerpSequence(_player.rewardAnimationWaitTime);
    }

    

    public void EndReceiveReward()
    {
        Debug.Log("end receive reward");
        CameraManager.Instance.SetCamera(CameraMode.Normal);
        _anim.SetBool("isReceivingReward", false);
        _player.particleShooter.Enable(2, false);
        //_player.rewardSticker.gameObject.SetActive(false);
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
