using System.Collections;
using UnityEngine;

public class PlayerModel
{
    //el Model del playerMVC
    //aca en model dice que hace move y jump, y van a estar otras mecanicas seguramente

    Player _player;
    float _groundedTimer;
    float _verticalVelocity;

    Vector3 _move; //el vector en el que guardo la suma de todo el movimiento para finalmente aplicarsela al character controller
    Vector3 auxForwardVector;
    float auxOriginalImpulse;

    bool isFalling = false;
    bool _wasGroundedLastFrame = false;

    float fallingTimer = 0f;

    public PlayerModel(Player player)
    {
        _player = player;
        auxOriginalImpulse = _player.planeoImpulse;
    }

    public void NewMove(float hor, float ver)
    {
        bool groundedPlayer = _player.cc.isGrounded;
        if (groundedPlayer)
        {
            OnGrounded();
        }

        if (_groundedTimer > 0)
        {
            _groundedTimer -= Time.deltaTime;
        }

        if (!groundedPlayer && _verticalVelocity <= -2f) //si esta cayendo pero no tocando el suelo empieza a caer
        {
            OnStartFalling();
        }

        if (groundedPlayer && _verticalVelocity <= 0) //si acabo de estar grounded
        {
            OnTouchGround();
        }

        _verticalVelocity -= _player.gravityValue * Time.deltaTime; //aplica gravedad extra
        _move = _player.transform.right * hor + _player.transform.forward * ver; //cargo mi vector movimiento


        if (_move.magnitude > 1) //normalizo
        {
            _move = _move.normalized;
        }

        if (_player.isJumpButtonDown)
        {
            if (_groundedTimer > 0)
            {
                OnJump();
            }
        }

        _move *= _player.Speed;
        _move.y = _verticalVelocity; //sigo cargando el vector movieminto
        _player.cc.Move(_move * Time.deltaTime); //aplico el vector movieminto al character controller, con el metodo .Move

        if (hor != 0 || ver != 0)
        {
            _player._view.CheckCanRotateModel(_move);
        }

        //Debug.Log(_move);
    }

    private void OnGrounded()
    {
        //Debug.Log("on grounded");
        _groundedTimer = 0.2f; //mientras este en el suelo
        //_player._view.StopJump();

        // Only trigger landing animations when transitioning from not-grounded to grounded
        if (!_wasGroundedLastFrame)
        {
            if (isFalling)
            {
                //Debug.Log("jump land sfx - falling");
                AudioManager.instance.PlayByName("JumpLand", 1f, 0.02f);

                if (fallingTimer > 0.2f)
                {
                    _player.BrieflySlowDown();
                }

                fallingTimer = 0f;
            }
            isFalling = false;

            _player._view.StopFalling();
            _player._view.StartLanding();
        }

        _wasGroundedLastFrame = true;
    }
    private void OnStartFalling()
    {
        //Debug.Log("on start falling");
        isFalling = true;
        _wasGroundedLastFrame = false;
        _player._view.StopJump();
        _player._view.StartFalling();
        //Debug.Log("falling timer" + fallingTimer);
        fallingTimer += Time.deltaTime;
    }
    private void OnTouchGround()
    {
        //Debug.Log("ongrounded y vertical velocity => 0");
        _verticalVelocity = 0f;

        //_player._view.StopLanding();

        if (_player.augmentedJumpsLeft == 0)
        {
            _player.DestroyPaperPlaneHat();
        }
    }
    private void OnJump()
    {
        _groundedTimer = 0;
        _verticalVelocity += Mathf.Sqrt(_player.jumpForce * 2 * _player.gravityValue); //saltar en realidad le da velocidad vertical nomas
        _player.isJumpButtonDown = false;
        _player._view.StartJumpAnimation(_player.isPaperPlaneHat);
        _player._view.StopLanding();

        if (_player.isPaperPlaneHat)
        {
            _player.AddPlaning();
            _player.augmentedJumpsLeft--;
        }
    }

    public void EnableTijeraHitbox()
    {
        //Debug.Log("prendo la tijera");
        _player.miTijeraHitbox.gameObject.SetActive(true);
    }
    public void DisableTijeraHitbox()
    {
        //Debug.Log("apago la tijera");
        _player.miTijeraHitbox.gameObject.SetActive(false);
    }

    public IEnumerator AddExtraForwardForce(float delayTime, float duration, float planeoImpulse, Vector3 lastDirection)
    {
        yield return new WaitForSeconds(delayTime);

        planeoImpulse = auxOriginalImpulse;
        float elapsedTime = 0f;
        float startImpulse = planeoImpulse;

        while (elapsedTime < duration)
        {
            auxForwardVector = lastDirection * planeoImpulse;
            _player.cc.Move(auxForwardVector * Time.deltaTime);

            planeoImpulse = Mathf.Lerp(startImpulse, 0f, elapsedTime / duration);

            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }

        planeoImpulse = 0f;
    }
    public void ForcedMove(Vector3 move)
    {
        _player.cc.Move(move);
    }
}
