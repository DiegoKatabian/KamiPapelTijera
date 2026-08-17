using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TijeraEquipment
{
    Normal,    // Spine skin: "Tijera_Normal"
    Mejorada   // Spine skin: "Tijera_Upgrade_1"
}

public enum RespawnBehavior
{
    LastSafePosition, // ultimo lugar seguro (snapshot con antiguedad, no literal al borde del agua)
    LevelSpawnPoint   // punto de entrada de kami a la pagina actual (el respawn comun de siempre)
}

public class Player : Entity, IMojable, IGolpeable, ICurable, IWindable
{
    [Header("Stats")]
    public bool hasTijera = false;
    public TijeraEquipment currentTijera = TijeraEquipment.Normal;
    public bool hasSprintBoots = false;
    public bool hasWaterBoots = false;

    [Header("Respawn")]
    [Tooltip("donde respawnea kami al morir ahogada. las demas muertes usan siempre el respawn comun (entrada de la pagina)")]
    [SerializeField] RespawnBehavior drowningRespawnMode = RespawnBehavior.LastSafePosition;
    [Tooltip("cada cuantos segundos en el suelo se confirma un snapshot de lugar seguro. mas alto = respawn mas lejos del peligro")]
    public float safeSnapshotInterval = 2f;

    public float weaponCooldown;
    [SerializeField] float tijeraHitBoxDuration = 0.1f;
    [Tooltip("fallback por timer para cuando AttackMOVE no traia el evento HandleAttack. desde Atlas 5 el evento existe: -1 = apagado. si un atlas viejo no lo trae, poner el timing del evento (0.4667)")]
    [SerializeField] float attackMoveHitboxDelay = -1f;
    [Tooltip("a que distancia de kami se pone la hitbox al atacar. -1 = auto: usa la distancia a la que quedo puesta en el prefab")]
    [SerializeField] float hitboxDistance = -1f;
    [Tooltip("a que altura (desde los pies de kami) se pone la hitbox al atacar. -1 = auto: usa la altura del prefab")]
    [SerializeField] float hitboxHeight = -1f;
    [Tooltip("offset de rotación para la hitbox (grados). Prueba con -90 si la hitbox está rotada 90° incorrectamente")]
    [SerializeField] float hitboxRotationOffset = 0f;
    public float jumpForce = 50f;
    public float gravityValue; //gravedad extra para que quede linda la caida del salto
    public float sprintingSpeedModifier = 1.5f;
    public float landingLagModifier = 0.75f;
    public float landingLagTime = 0.25f;
    public float rewardAnimationWaitTime = 2;
    [Tooltip("cuanto tarda en aparecer el overlay de derrota despues de morir. mientras tanto se ve la anim de muerte")]
    public float defeatOverlayDelay = 2f;
    [Tooltip("cuantos segundos seguidos a velocidad maxima (skip/run) hacen falta para que al frenar en seco se vea la anim de runstop")]
    public float runstopMinFullSpeedTime = 2f;

    [Header("Hit Feedback")]
    public HitFeedbackConfig hitFeedback = new HitFeedbackConfig();

    [Header("Mezcla de animaciones")]
    public AnimationMixConfig animMix = new AnimationMixConfig();

    [Header("Movimiento y Salto")]
    [Range(0f, 1f)] public float walkThreshold = 0.5f; //con joystick: debajo de esta magnitud camina (walk), arriba trota (skip)
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.4f; //al soltar BARRA subiendo se corta la velocidad: tap = saltito, hold = salto completo
    public float landingDuration = 0.15f; //cuanto dura el estado Landing (bloquea inputs)

    [Header("Componentes")]
    public CharacterController cc;
    public Transform particleAnchor; //ancla que sigue el hueso Smoke del skeleton (BoneFollower). ya no se usa para salto/aterrizaje: esas van a FeetPosition
    public Transform footstepAnchor; //GO vacío en los pies de Kami, para instanciar partículas de pasos y runstop. editable en el prefab para tuning visual
    public TijeraHitbox miTijeraHitbox;
    public ParticleShooter particleShooter;
    [SerializeField] GameObject myPaperPlaneHat;
    [SerializeField] TijeraManager tijeraManager;
    public InventorySlot rewardSticker;
    public Material waterBootsMaterial; //TODO: sin uso hasta que las botas de agua tengan feedback visual sobre el spine (skin?)
    public SkeletonAnimation SkeletonAnimation;

    [Header("Planeo")]
    public float augmentedJumpForce = 20f;
    public int augmentedJumpsMax = 3;
    public float planeoImpulse = 2;
    public float planeoDuration = 1;
    public float planeoDelayTime = 1;

    //originals
    float originalJumpForce;
    float _maxHp;
    float originalMaxSpeed;

    //auxiliars
    bool _readyToAttack = true;
    float _hitStunEndTime; //hasta cuando duran los bloqueos configurados en hitFeedback
    Coroutine _flashCoroutine;
    Vector3 _lastSafePosition;
    DeathCause _lastDeathCause = DeathCause.Generic;

    //flash de daño sobre el skeleton de spine
    MeshRenderer _skeletonMeshRenderer; //el mesh renderer del SkeletonAnimation, lo agarro solo en Start
    MaterialPropertyBlock _fillPropertyBlock; //para el modo SpineFill, sin instanciar materiales
    static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    static readonly int FillPhaseId = Shader.PropertyToID("_FillPhase");
    [HideInInspector] public bool isGettingWet = false;
    [HideInInspector] public bool isAttacking = false;
    [HideInInspector] public bool isPaperPlaneHat = false; //pph es paper plane hat
    [HideInInspector] public bool isSprinting; //cuando tocas shift
    [HideInInspector] public int augmentedJumpsLeft;
    [HideInInspector] public Vector3 lastDirection;
    [HideInInspector] public bool runstopReady; //lo mantiene el model: true si kami lleva el tiempo suficiente a velocidad maxima

    // --- Nuevos campos para velocidad calculada ---
    private float _sprintMultiplier = 1f;
    private float _landingLagFactor = 1f;

    float _pullSolapaLockEndTime; //mientras dura la anim de pullsolapa se bloquean movimiento y salto
    public bool IsPullingSolapa
    {
        get
        {
            return Time.time < _pullSolapaLockEndTime;
        }
    }

    [Header("Riding Page")]
    [Tooltip("ajuste fino de la posicion de kami respecto al hueso de borde, por si la animacion de riding no calza justo con el pivot del hueso")]
    public Vector3 RideRootOffset = new Vector3(2, 4, 0);
    [HideInInspector] public Vector3 OriginalRideRootOffset;
    public bool TurnOffTijeraTrailsDuringChangePage = true;

    public bool IsRidingPage { get; private set; }
    Transform _rideEdgeBone;
    float _rideZOffset;
    bool _rideLoggedFirstFrame;

    //State Machine
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    //la base de la capsula del CharacterController en world space: donde apoyan los pies de kami.
    //center y height son locales al transform, asi que se ajustan por lossyScale por si el prefab esta escalado.
    public Vector3 FeetPosition
    {
        get
        {
            if (cc == null)
            {
                Debug.LogWarning("[Player] FeetPosition: no hay CharacterController asignado, devuelvo transform.position como fallback");
                return transform.position;
            }

            Vector3 scale = transform.lossyScale;
            Vector3 worldCenter = transform.position + Vector3.Scale(cc.center, scale);
            return worldCenter - Vector3.up * (cc.height * 0.5f * Mathf.Abs(scale.y));
        }
    }

    public bool IsInHitStun
    {
        get
        {
            return Time.time < _hitStunEndTime;
        }
    }

    public Vector3 LastSafePosition
    {
        get { return _lastSafePosition; }
        set { _lastSafePosition = value; }
    }

    public DeathCause LastDeathCause
    {
        get { return _lastDeathCause; }
    }

    //MVC
    PlayerModel _model;
    [HideInInspector] public PlayerView _view;
    PlayerController _controller;

    public GameObject nuevoTooltipPapelSalto;

    //Properties
    public float Speed
    {
        get => _maxSpeed;
        set => _maxSpeed = value;
    }

    // Nueva propiedad que calcula la velocidad real
    public float CurrentSpeed => originalMaxSpeed * _sprintMultiplier * _landingLagFactor;

    public float Vida
    {
        get => _hp;
        set
        {
            _hp = value;
            if (_hp > _maxHp) _hp = _maxHp;
            if (_hp < 0) _hp = 0;
            EventManager.Trigger(Evento.OnPlayerChangeVida, _hp, _maxHp);
        }
    }

    public bool IsSprinting
    {
        get => isSprinting;
        set
        {
            if (isSprinting == value) return;
            isSprinting = value;
            // Actualizar multiplicador de sprint, sin modificar la velocidad directamente
            _sprintMultiplier = (isSprinting && hasSprintBoots) ? sprintingSpeedModifier : 1f;
            // NOTA: Las partículas de sprint ya no se controlan aquí, ahora las gestiona PlayerView al entrar/salir de Running.
        }
    }

    void Awake()
    {
        _model = new PlayerModel(this);
        _view = new PlayerView(this);
        _controller = new PlayerController(this);
    }

    private void Start()
    {
        _maxHp = _hp;
        Vida = _maxHp;

        augmentedJumpsLeft = augmentedJumpsMax;
        originalJumpForce = jumpForce;
        originalMaxSpeed = _maxSpeed;
        _lastSafePosition = transform.position; //arranque: si muere antes del primer snapshot, respawnea donde nacio
        OriginalRideRootOffset = RideRootOffset;

        if (SkeletonAnimation == null)
        {
            SkeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        }

        _skeletonMeshRenderer = SkeletonAnimation.GetComponent<MeshRenderer>();
        _fillPropertyBlock = new MaterialPropertyBlock();

        CalibrateHitboxPlacement(); //los -1 de hitboxDistance/hitboxHeight se completan con la pose del prefab

        tijeraManager.InitTipFollowers(SkeletonAnimation); //los trails de tijera siguen la punta de la tijera de spine

        EventManager.Subscribe(Evento.OnOrigamiStart, StartOrigamiCast);
        EventManager.Subscribe(Evento.OnOrigamiEnd, EndOrigamiCast);
        EventManager.Subscribe(Evento.OnPlayerGetTijera, GetTijera);
        EventManager.Subscribe(Evento.OnOrigamiGivePaperPlaneHat, GetPaperPlaneHat);
        EventManager.Subscribe(Evento.OnQuestRewardedStart, StartReceiveReward);
        EventManager.Subscribe(Evento.OnQuestRewardedEnd, EndReceiveReward);
        EventManager.Subscribe(Evento.OnPlayerPlaced, OnPlayerPlaced);
    }

    private void Update()
    {
        _controller.CheckControls();

        if (_controller.Inputs.hor != 0 || _controller.Inputs.ver != 0)
        {
            EventManager.Trigger(Evento.OnPlayerMove, _controller.Inputs.hor, _controller.Inputs.ver);
        }

        _model.Tick(_controller.Inputs);
    }

    void LateUpdate()
    {
        if (!IsRidingPage || _rideEdgeBone == null) return;

        Vector3 bonePos = _rideEdgeBone.position;
        Vector3 newPos = new Vector3(bonePos.x, bonePos.y, bonePos.z + _rideZOffset) + RideRootOffset;

        if (!_rideLoggedFirstFrame)
        {
            _rideLoggedFirstFrame = true;
            Debug.Log($"[Player] RidingPage LateUpdate arranco: hueso '{_rideEdgeBone.name}' en {bonePos}, kami va a {newPos}");
        }
        else if (Time.frameCount % 15 == 0)
        {
            Debug.Log($"[Player] RidingPage LateUpdate: hueso en {bonePos} -> kami en {newPos}");
        }

        transform.position = newPos;
    }

    public void SetRideRootOffsetAccordingToForcedFacing(bool faceRight)
    {
        RideRootOffset = faceRight ? OriginalRideRootOffset : new Vector3(-OriginalRideRootOffset.x, OriginalRideRootOffset.y, OriginalRideRootOffset.z);
    }

    //State Machine
    public void SetState(PlayerState newState)
    {
        if (newState == CurrentState) return;
        PlayerState previous = CurrentState;
        CurrentState = newState;
        _view.OnStateChanged(previous, newState);
    }

    bool CanAttack()
    {
        switch (CurrentState)
        {
            case PlayerState.Casting:
            case PlayerState.ReceivingReward:
            case PlayerState.Dead:
            case PlayerState.RidingPage:
                return false;
            default:
                return true;
        }
    }

    public void OnPrimaryClick()
    {
        if (LevelManager.Instance.inDialogue) return;
        if (IsInHitStun && hitFeedback.blockAttack)
        {
            Debug.Log("[Player] ataque bloqueado por hit stun");
            return;
        }
        if (_readyToAttack && hasTijera && CanAttack())
        {
            _readyToAttack = false;
            isAttacking = true;
            _view.StartAttack();
        }
    }

    public void PlayPullSolapa(bool closing = false)
    {
        float duration = _view.PlayPullSolapa(closing);
        _pullSolapaLockEndTime = Time.time + duration;
    }

    public void StartRidingPage(Transform edgeBone, bool? faceRight = null)
    {
        if (edgeBone == null)
        {
            Debug.LogWarning("[Player] StartRidingPage: edgeBone nulo, kami no se engancha a la hoja");
            return;
        }

        _rideEdgeBone = edgeBone;
        _rideZOffset = transform.position.z - edgeBone.position.z;
        _rideLoggedFirstFrame = false;
        cc.enabled = false;
        IsRidingPage = true;

        if (TurnOffTijeraTrailsDuringChangePage)
            tijeraManager.TurnOffTrailsDuringChangePage();

        if (faceRight.HasValue)
            _view.ForceFacing(!faceRight.Value);
        _view.SetSprintParticlesState(true);

        SetState(PlayerState.RidingPage);
        Debug.Log($"[Player] StartRidingPage: enganchada a '{edgeBone.name}' (bone pos {edgeBone.position}, kami pos {transform.position}, offset Z {_rideZOffset:F2}), cc.enabled={cc.enabled}, estado={CurrentState}");
    }

    public void StopRidingPage()
    {
        if (!IsRidingPage) return;

        IsRidingPage = false;
        _rideEdgeBone = null;
        cc.enabled = true;

        if (TurnOffTijeraTrailsDuringChangePage)
            tijeraManager.TurnBackOnTrailsAfterChangePage();

        _view.SetSprintParticlesState(false);
        SetState(PlayerState.Idle);
        Debug.Log($"[Player] StopRidingPage: kami se suelta de la hoja en {transform.position}, cc.enabled={cc.enabled}");
    }

    void CalibrateHitboxPlacement()
    {
        if (tijeraManager == null || tijeraManager.hitboxParent == null) return;

        Vector3 local = transform.InverseTransformPoint(tijeraManager.hitboxParent.position);
        if (hitboxDistance < 0f)
            hitboxDistance = new Vector2(local.x, local.z).magnitude;
        if (hitboxHeight < 0f)
            hitboxHeight = local.y;
    }

    void OrientTijeraHitbox()
    {
        if (tijeraManager == null || tijeraManager.hitboxParent == null) return;

        Vector3 dir;
        if (CurrentState == PlayerState.Idle)
        {
            float facing = SkeletonAnimation.Skeleton.ScaleX >= 0f ? 1f : -1f;
            dir = Vector3.right * facing;
        }
        else
        {
            dir = lastDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                float facing = SkeletonAnimation.Skeleton.ScaleX >= 0f ? 1f : -1f;
                dir = Vector3.right * facing;
            }
            dir.Normalize();
        }

        tijeraManager.hitboxParent.position = transform.position + dir * hitboxDistance + Vector3.up * hitboxHeight;
        Vector3 rotatedDir = Quaternion.Euler(0, hitboxRotationOffset, 0) * dir;
        tijeraManager.hitboxParent.rotation = Quaternion.LookRotation(rotatedDir, Vector3.up);
    }

    public void StartPasoSFX(int step) => _view.StartPasoSFX(step);
    public void StartTijeraCoroutine() => StartCoroutine(TijeraCoroutine());
    public void StartTijeraCoroutineDelayed() => StartCoroutine(TijeraDelayedCoroutine());

    IEnumerator TijeraDelayedCoroutine()
    {
        if (attackMoveHitboxDelay < 0f) yield break;
        yield return new WaitForSeconds(attackMoveHitboxDelay);
        Debug.Log("[Player] hitbox disparada por timer (AttackMOVE)");
        yield return TijeraCoroutine();
    }

    public IEnumerator TijeraCoroutine()
    {
        OrientTijeraHitbox();
        _model.EnableTijeraHitbox();
        yield return new WaitForSeconds(tijeraHitBoxDuration);
        _model.DisableTijeraHitbox();

        yield return new WaitForSeconds(weaponCooldown);
        _readyToAttack = true;
        isAttacking = false;
        _view.EndAttack();
    }

    public void StartTijeraParticles() => tijeraManager.EnableTijeraParticles();
    public void StopTijeraParticles() => tijeraManager.DisableTijeraParticles();

    //Entity
    public override void TakeDamage(float dmg) => TakeDamage(dmg, DeathCause.Generic);

    public override void TakeDamage(float dmg, DeathCause cause)
    {
        Vida -= dmg;
        if (Vida <= 0)
        {
            Die(cause);
            isGettingWet = false;
        }
        else
        {
            _hitStunEndTime = Time.time + hitFeedback.stunDuration;
            _view.PlayTakeHitAnimation();
            Debug.Log($"[Player] take hit: stun {hitFeedback.stunDuration}s (blockMov: {hitFeedback.blockMovement} / blockAtk: {hitFeedback.blockAttack} / blockJump: {hitFeedback.blockJump})");
        }

        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(EnrojecerSprite());
    }

    public IEnumerator EnrojecerSprite()
    {
        SetFlash(1f);
        yield return new WaitForSeconds(hitFeedback.flashHoldDuration);

        float elapsed = 0f;
        while (elapsed < hitFeedback.flashFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetFlash(1f - (elapsed / hitFeedback.flashFadeDuration));
            yield return null;
        }
        SetFlash(0f);
        _flashCoroutine = null;
    }

    void SetFlash(float intensity)
    {
        switch (hitFeedback.flashMode)
        {
            case HitFlashMode.SpineFill:
                _fillPropertyBlock.SetColor(FillColorId, hitFeedback.flashColor);
                _fillPropertyBlock.SetFloat(FillPhaseId, hitFeedback.fillPhase * intensity);
                _skeletonMeshRenderer.SetPropertyBlock(_fillPropertyBlock);
                break;
            case HitFlashMode.VertexTint:
                Color tint = Color.Lerp(Color.white, hitFeedback.flashColor, intensity);
                Spine.Skeleton skeleton = SkeletonAnimation.Skeleton;
                skeleton.R = tint.r;
                skeleton.G = tint.g;
                skeleton.B = tint.b;
                break;
        }
    }

    public override void Die(DeathCause cause = DeathCause.Generic)
    {
        if (CurrentState == PlayerState.Dead) return;
        isGettingWet = false;

        _lastDeathCause = cause;
        _model.InvalidateSafeCandidate();
        Debug.Log($"[Player] Die: causa {cause}");
        SetState(PlayerState.Dead);
        AudioManager.instance.PlayByName("GameOverOrchestral");
        EventManager.Trigger(Evento.OnPlayerDie, cause);
        StartCoroutine(DeathSequence(cause));
    }

    IEnumerator DeathSequence(DeathCause cause = DeathCause.Generic)
    {
        yield return new WaitForSeconds(defeatOverlayDelay);

        Vector3? respawnOverride = null;
        if (cause == DeathCause.Drowning && drowningRespawnMode == RespawnBehavior.LastSafePosition)
            respawnOverride = LastSafePosition;

        Debug.Log($"[Player] DeathSequence: muestro overlay (causa {cause}, respawn {(respawnOverride.HasValue ? $"lugar seguro {respawnOverride.Value}" : "entrada de pagina")})");
        OverlayManager.Instance.ShowDefeatOverlay(cause, respawnOverride);
    }

    private void OnPlayerPlaced(object[] parameters)
    {
        isGettingWet = false;
        if (CurrentState == PlayerState.Dead)
        {
            Vida = _maxHp;
            SetState(PlayerState.Idle);
            Debug.Log($"[Player] OnPlayerPlaced: revivida en {transform.position} (vida llena, estado Idle)");
        }
    }

    //Interfaces
    public void GetWet(float wetDamage)
    {
        isGettingWet = true;
        if (!hasWaterBoots)
        {
            Debug.Log("[Player] GetWet: me mojé, feedback + muerte por ahogo");
            _view.StartGetWetAnimation();
            Die(DeathCause.Drowning);
        }
        else
        {
            Debug.Log("[Player] GetWet: me mojé pero tengo las botas de agua, no muero");
        }
    }

    public void StopGettingWet() => isGettingWet = false;

    public void GetGolpeado(float dmg)
    {
        _view.StartGetGolpeadoAnimation();
        TakeDamage(dmg, DeathCause.Rocoso);
    }

    public void GetCured(int curacion) => Vida += curacion;

    //Equipamiento
    public void SetTijeraEquipment(TijeraEquipment newTijera)
    {
        if (currentTijera == newTijera) return;
        currentTijera = newTijera;

        string skinName = newTijera == TijeraEquipment.Mejorada ? "Tijera_Upgrade_1" : "Tijera_Normal";
        SkeletonAnimation.Skeleton.SetSkin(skinName);
        SkeletonAnimation.Skeleton.SetSlotsToSetupPose();

        if (newTijera == TijeraEquipment.Mejorada)
        {
            tijeraManager.SetTijeraMejorada();
            miTijeraHitbox = tijeraManager.tijeraMejoradaHitbox;
        }
        else
        {
            tijeraManager.SetTijera();
            miTijeraHitbox = tijeraManager.tijeraHitbox;
        }
        Debug.Log($"[Player] Tijera equipada: {newTijera}");
    }

    //Eventos
    public void GetTijera(params object[] parameters)
    {
        hasTijera = true;
        LevelManager.Instance.AddResource(ResourceType.tijera, 1);
        tijeraManager.SetTijera();
        StartReceiveRewardAutoEnd();
        _view.RefreshBodyAnimation();
        _view.RefreshOverrides();
    }

    public void GetTijeraMejorada(params object[] parameters) => SetTijeraEquipment(TijeraEquipment.Mejorada);

    public void StartOrigamiCast(params object[] parameters) => SetState(PlayerState.Casting);
    public void EndOrigamiCast(params object[] parameters)
    {
        if (CurrentState == PlayerState.Casting)
            SetState(PlayerState.Idle);
    }

    public void GetPaperPlaneHat(params object[] parameters)
    {
        isPaperPlaneHat = true;
        jumpForce = augmentedJumpForce;
        augmentedJumpsLeft = augmentedJumpsMax;
        myPaperPlaneHat.SetActive(true);
        AudioManager.instance.PlayByName("ShipSpawn", 2f);
        nuevoTooltipPapelSalto.SetActive(true);
        _view.RefreshOverrides();
    }

    public void DestroyPaperPlaneHat(params object[] parameters)
    {
        isPaperPlaneHat = false;
        jumpForce = originalJumpForce;
        myPaperPlaneHat.SetActive(false);
        TooltipManager.Instance.HideTooltip();
        nuevoTooltipPapelSalto.SetActive(false);
        _view.RefreshOverrides();
    }

    private void StartReceiveReward(object[] parameters)
    {
        SetState(PlayerState.ReceivingReward);
        StartCoroutine(WaitForReceiveRewardAnimation(rewardAnimationWaitTime));
    }

    private void StartReceiveRewardAutoEnd()
    {
        SetState(PlayerState.ReceivingReward);
        StartCoroutine(WaitForReceiveRewardAnimationAutoEnd(rewardAnimationWaitTime));
    }

    public IEnumerator WaitForReceiveRewardAnimation(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        DialogueManager.Instance.lockedByAnimation = false;
    }

    public IEnumerator WaitForReceiveRewardAnimationAutoEnd(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        DialogueManager.Instance.lockedByAnimation = false;
        EndReceiveReward(new object[] { });
    }

    private void EndReceiveReward(object[] parameters)
    {
        if (CurrentState == PlayerState.ReceivingReward)
            SetState(PlayerState.Idle);
    }

    //Utilities
    public void AddPlaning() => StartCoroutine(_model.AddExtraForwardForce(planeoDelayTime, planeoDuration, planeoImpulse, lastDirection));

    public void GetAffectedByWind(float windForce, Vector3 windDirection) => _model.ForcedMove(windDirection * windForce);

    public void StartAffectedByWind(float windForce, Vector3 windDirection)
    {
        Debug.Log("player start affected");
        _view.StartAffectedByWind(windForce, windDirection);
    }

    public void EndAffectedByWind()
    {
        Debug.Log("player end affected");
        _view.EndAffectedByWind();
    }

    public void BrieflySlowDown()
    {
        StartCoroutine(SlowDownCoroutine(landingLagModifier, landingLagTime));
    }

    public IEnumerator SlowDownCoroutine(float speedModifier, float time)
    {
        _landingLagFactor = speedModifier;
        yield return new WaitForSeconds(time);
        _landingLagFactor = 1f;
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnOrigamiStart, StartOrigamiCast);
            EventManager.Unsubscribe(Evento.OnOrigamiEnd, EndOrigamiCast);
            EventManager.Unsubscribe(Evento.OnPlayerGetTijera, GetTijera);
            EventManager.Unsubscribe(Evento.OnOrigamiGivePaperPlaneHat, GetPaperPlaneHat);
            EventManager.Unsubscribe(Evento.OnQuestRewardedStart, StartReceiveReward);
            EventManager.Unsubscribe(Evento.OnQuestRewardedEnd, EndReceiveReward);
            EventManager.Unsubscribe(Evento.OnPlayerPlaced, OnPlayerPlaced);
        }
    }
}