using System.Collections;
using UnityEngine;

public class PlayerModel
{
    //el Model del playerMVC
    //recibe los inputs ya leidos por el controller y decide que puede hacer kami segun su estado actual.
    //las transiciones de estado se deciden aca; el view solo reacciona a los cambios.

    Player _player;

    float _coyoteTimer; //ventanita para poder saltar apenas despues de pisar un borde
    float _verticalVelocity;
    float _fallingTimer;
    float _landingTimer;

    const float COYOTE_TIME = 0.2f;
    const float FALL_VELOCITY_THRESHOLD = -2f; //velocidad vertical a partir de la cual consideramos que kami cae
    const float HARD_FALL_TIME = 0.2f; //caidas mas largas que esto aplican landing lag

    Vector3 _move; //el vector en el que guardo la suma de todo el movimiento para finalmente aplicarsela al character controller
    Vector3 auxForwardVector;
    float auxOriginalImpulse;

    public PlayerModel(Player player)
    {
        _player = player;
        auxOriginalImpulse = _player.planeoImpulse;
    }

    public void Tick(PlayerInputs input)
    {
        bool grounded = _player.cc.isGrounded;

        UpdateLandingTimer();

        if (IsInputLocked())
        {
            input = default; //estados bloqueantes: se ignora todo input, pero la fisica sigue corriendo
        }

        UpdateVerticalState(grounded);
        UpdateLocomotionState(input, grounded);
        HandleJump(input);
        ApplyPhysics(input.hor, input.ver);
    }

    bool IsInputLocked()
    {
        switch (_player.CurrentState)
        {
            case PlayerState.Landing:
            case PlayerState.Casting:
            case PlayerState.ReceivingReward:
            case PlayerState.Dead:
                return true;
            default:
                return false;
        }
    }

    void UpdateLandingTimer()
    {
        if (_player.CurrentState != PlayerState.Landing)
        {
            return;
        }

        _landingTimer -= Time.deltaTime;
        if (_landingTimer <= 0f)
        {
            _player.SetState(PlayerState.Idle); //si hay input, el locomotion lo pisa en este mismo frame
        }
    }

    void UpdateVerticalState(bool grounded)
    {
        if (grounded)
        {
            _coyoteTimer = COYOTE_TIME;

            PlayerState state = _player.CurrentState;
            if (state == PlayerState.Jumping || state == PlayerState.Falling)
            {
                //recien toco el suelo viniendo del aire
                if (_fallingTimer > HARD_FALL_TIME)
                {
                    _player.BrieflySlowDown();
                }
                _fallingTimer = 0f;
                _landingTimer = _player.landingDuration;
                _player.SetState(PlayerState.Landing);
            }

            if (_verticalVelocity <= 0f)
            {
                _verticalVelocity = 0f;

                if (_player.augmentedJumpsLeft == 0)
                {
                    _player.DestroyPaperPlaneHat();
                }
            }
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;

            if (_player.CurrentState == PlayerState.Falling)
            {
                _fallingTimer += Time.deltaTime;
            }
            else if (_verticalVelocity <= FALL_VELOCITY_THRESHOLD && CanStartFalling())
            {
                _player.SetState(PlayerState.Falling);
            }
        }
    }

    bool CanStartFalling()
    {
        switch (_player.CurrentState)
        {
            case PlayerState.Idle:
            case PlayerState.Walking:
            case PlayerState.Skipping:
            case PlayerState.Running:
            case PlayerState.Jumping:
            case PlayerState.Landing:
                return true;
            default:
                return false;
        }
    }

    void UpdateLocomotionState(PlayerInputs input, bool grounded)
    {
        if (!grounded)
        {
            return;
        }

        switch (_player.CurrentState) //solo los estados de locomocion se pisan entre si
        {
            case PlayerState.Idle:
            case PlayerState.Walking:
            case PlayerState.Skipping:
            case PlayerState.Running:
                break;
            default:
                return;
        }

        float magnitude = new Vector2(input.hor, input.ver).magnitude;

        if (magnitude < 0.01f)
        {
            _player.SetState(PlayerState.Idle);
        }
        else if (_player.isSprinting && _player.hasSprintBoots)
        {
            _player.SetState(PlayerState.Running);
        }
        else if (magnitude < _player.walkThreshold) //joystick apenas inclinado
        {
            _player.SetState(PlayerState.Walking);
        }
        else
        {
            _player.SetState(PlayerState.Skipping);
        }
    }

    void HandleJump(PlayerInputs input)
    {
        if (input.jumpDown && _coyoteTimer > 0f && CanJumpFromCurrentState())
        {
            OnJump();
        }

        //salto variable: soltar la barra mientras sube corta el impulso. tap = saltito, hold = salto completo
        if (input.jumpUp && _verticalVelocity > 0f)
        {
            _verticalVelocity *= _player.jumpCutMultiplier;
        }
    }

    bool CanJumpFromCurrentState()
    {
        switch (_player.CurrentState)
        {
            case PlayerState.Idle:
            case PlayerState.Walking:
            case PlayerState.Skipping:
            case PlayerState.Running:
                return true;
            default:
                return false;
        }
    }

    void OnJump()
    {
        _coyoteTimer = 0f;
        _verticalVelocity += Mathf.Sqrt(_player.jumpForce * 2 * _player.gravityValue); //saltar en realidad le da velocidad vertical nomas
        _player.SetState(PlayerState.Jumping);

        if (_player.isPaperPlaneHat)
        {
            _player.AddPlaning();
            _player.augmentedJumpsLeft--;
        }
    }

    void ApplyPhysics(float hor, float ver)
    {
        _verticalVelocity -= _player.gravityValue * Time.deltaTime; //aplica gravedad extra

        _move = _player.transform.right * hor + _player.transform.forward * ver; //cargo mi vector movimiento

        if (_move.magnitude > 1) //normalizo
        {
            _move = _move.normalized;
        }

        _move *= _player.Speed;
        _move.y = _verticalVelocity; //sigo cargando el vector movimiento
        _player.cc.Move(_move * Time.deltaTime); //aplico el vector movimiento al character controller, con el metodo .Move

        if (hor != 0 || ver != 0)
        {
            _player.lastDirection = new Vector3(_move.x, 0, _move.z); //la usan el planeo y el viento
            _player._view.SetFacing(hor); //flip del sprite via spine
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
