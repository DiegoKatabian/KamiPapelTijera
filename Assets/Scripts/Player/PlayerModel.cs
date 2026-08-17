using System.Collections;
using UnityEngine;

public class PlayerModel
{
    Player _player;

    float _coyoteTimer;
    float _verticalVelocity;
    float _fallingTimer;
    float _landingTimer;
    float _fullSpeedTimer;

    float _safePosTimer;
    Vector3 _candidateSafePos;
    bool _hasSafeCandidate;

    const float COYOTE_TIME = 0.2f;
    const float FALL_VELOCITY_THRESHOLD = -2f;
    const float HARD_FALL_TIME = 0.2f;

    Vector3 _move;
    Vector3 auxForwardVector;
    float auxOriginalImpulse;

    public PlayerModel(Player player)
    {
        _player = player;
        auxOriginalImpulse = _player.planeoImpulse;
    }

    public void Tick(PlayerInputs input)
    {
        if (_player.IsRidingPage)
            return;

        bool grounded = _player.cc.isGrounded;

        UpdateLandingTimer();

        if (IsInputLocked())
        {
            input = default;
        }
        else if (_player.IsPullingSolapa)
        {
            input = default;
            _fullSpeedTimer = 0f;
        }
        else if (_player.IsInHitStun)
        {
            if (_player.hitFeedback.blockMovement)
            {
                input.hor = 0f;
                input.ver = 0f;
                input.horRaw = 0f;
                input.verRaw = 0f;
            }
            if (_player.hitFeedback.blockJump)
            {
                input.jumpDown = false;
                input.jumpUp = false;
            }
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
            return;

        _landingTimer -= Time.deltaTime;
        if (_landingTimer <= 0f)
            _player.SetState(PlayerState.Idle);
    }

    void UpdateVerticalState(bool grounded)
    {
        if (grounded)
        {
            _coyoteTimer = COYOTE_TIME;
            UpdateSafePosition();

            PlayerState state = _player.CurrentState;
            if (state == PlayerState.Jumping || state == PlayerState.Falling)
            {
                if (_fallingTimer > HARD_FALL_TIME)
                    _player.BrieflySlowDown();
                _fallingTimer = 0f;
                _landingTimer = _player.landingDuration;
                _player.SetState(PlayerState.Landing);
            }

            if (_verticalVelocity <= 0f)
            {
                _verticalVelocity = 0f;
                if (_player.augmentedJumpsLeft == 0)
                    _player.DestroyPaperPlaneHat();
            }
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;

            if (_player.CurrentState == PlayerState.Falling)
                _fallingTimer += Time.deltaTime;
            else if (_verticalVelocity <= FALL_VELOCITY_THRESHOLD && CanStartFalling())
                _player.SetState(PlayerState.Falling);
        }
    }

    void UpdateSafePosition()
    {
        if (_player.isGettingWet || _player.CurrentState == PlayerState.Dead)
            return;

        _safePosTimer += Time.deltaTime;
        if (_safePosTimer < _player.safeSnapshotInterval)
            return;
        _safePosTimer = 0f;

        if (_hasSafeCandidate)
            _player.LastSafePosition = _candidateSafePos;
        _candidateSafePos = _player.transform.position;
        _hasSafeCandidate = true;
    }

    public void InvalidateSafeCandidate()
    {
        _hasSafeCandidate = false;
        _safePosTimer = 0f;
        Debug.Log("[PlayerModel] candidata de lugar seguro descartada por muerte");
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
        if (!grounded) return;

        switch (_player.CurrentState)
        {
            case PlayerState.Idle:
            case PlayerState.Walking:
            case PlayerState.Skipping:
            case PlayerState.Running:
            case PlayerState.Landing:
                break;
            default:
                return;
        }

        float magnitude = new Vector2(input.horRaw, input.verRaw).magnitude;

        // Timer de velocidad máxima para runstop
        PlayerState state = _player.CurrentState;
        if (state == PlayerState.Skipping || state == PlayerState.Running)
            _fullSpeedTimer += Time.deltaTime;
        else
            _fullSpeedTimer = 0f;
        _player.runstopReady = _fullSpeedTimer >= _player.runstopMinFullSpeedTime;

        if (magnitude < 0.01f)
        {
            if (_player.CurrentState != PlayerState.Landing)
            {
                // Ya no se desactiva IsSprinting aquí; se mantiene según la entrada del jugador.
                _player.SetState(PlayerState.Idle);
            }
        }
        else if (_player.isSprinting && _player.hasSprintBoots)
        {
            _player.SetState(PlayerState.Running);
        }
        else if (magnitude < _player.walkThreshold)
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
            OnJump();

        if (input.jumpUp && _verticalVelocity > 0f)
            _verticalVelocity *= _player.jumpCutMultiplier;
    }

    bool CanJumpFromCurrentState()
    {
        switch (_player.CurrentState)
        {
            case PlayerState.Idle:
            case PlayerState.Walking:
            case PlayerState.Skipping:
            case PlayerState.Running:
            case PlayerState.Landing:
                return true;
            default:
                return false;
        }
    }

    void OnJump()
    {
        _coyoteTimer = 0f;
        _verticalVelocity += Mathf.Sqrt(_player.jumpForce * 2 * _player.gravityValue);
        _player.SetState(PlayerState.Jumping);

        if (_player.isPaperPlaneHat)
        {
            _player.AddPlaning();
            _player.augmentedJumpsLeft--;
        }
    }

    void ApplyPhysics(float hor, float ver)
    {
        _verticalVelocity -= _player.gravityValue * Time.deltaTime;

        _move = _player.transform.right * hor + _player.transform.forward * ver;
        if (_move.magnitude > 1)
            _move = _move.normalized;

        // Usamos CurrentSpeed en lugar de Speed
        _move *= _player.CurrentSpeed;
        _move.y = _verticalVelocity;
        _player.cc.Move(_move * Time.deltaTime);

        if (hor != 0 || ver != 0)
        {
            _player.lastDirection = new Vector3(_move.x, 0, _move.z);
            _player._view.SetFacing(hor);
        }
    }

    public void EnableTijeraHitbox()
    {
        _player.miTijeraHitbox.gameObject.SetActive(true);
    }

    public void DisableTijeraHitbox()
    {
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