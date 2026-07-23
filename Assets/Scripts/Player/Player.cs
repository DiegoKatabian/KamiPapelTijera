using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Spine.Unity;


public class Player : Entity, IMojable, IGolpeable, ICurable, IWindable
{

    //player esta partido en 4. aca solo pongo lo que quiero que pase.
    //model se encarga de pensar, controller de recibir los controles, y view de animaciones, sonidos, particulas etc
    //aca contruyo a los 3, y les paso mi refe. ellos hablan conmigo, no entre ellos.
    //ademas, yo tengo start y update, ellos no.
    //CurrentState vive aca y es la unica fuente de verdad sobre que esta haciendo kami.

    [Header("Stats")]
    public bool hasTijera = false;
    public bool hasTijeraMejorada = false;
    public bool hasSprintBoots = false;
    public bool hasWaterBoots = false;

    public float weaponCooldown;
    [SerializeField] float tijeraHitBoxDuration = 0.1f;
    public float jumpForce = 50f;
    public float gravityValue; //gravedad extra para que quede linda la caida del salto
    public float sprintingSpeedModifier = 1.5f;
    public float landingLagModifier = 0.75f;
    public float landingLagTime = 0.25f;
    public float rewardAnimationWaitTime = 2;

    [Header("Movimiento y Salto")]
    [Range(0f, 1f)] public float walkThreshold = 0.5f; //con joystick: debajo de esta magnitud camina (walk), arriba trota (skip)
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.4f; //al soltar BARRA subiendo se corta la velocidad: tap = saltito, hold = salto completo
    public float landingDuration = 0.15f; //cuanto dura el estado Landing (bloquea inputs)


    [Header("Componentes")]
    public CharacterController cc;
    public Animator anim;
    public TijeraHitbox miTijeraHitbox;
    public ParticleShooter particleShooter;
    public Renderer kamiRenderer;
    [SerializeField] GameObject myPaperPlaneHat;
    [SerializeField] TijeraManager tijeraManager;
    public ParticleSystem tijeraParticles, tijeraTrail;
    public InventorySlot rewardSticker;
    public Material waterBootsMaterial;
    public SkeletonAnimation SkeletonAnimation;


    [Header("Planeo")]
    public float augmentedJumpForce = 20f;
    public int augmentedJumpsMax = 3;
    public float planeoImpulse = 2;
    public float planeoDuration = 1;
    public float planeoDelayTime = 1;

    //originals
    Color originalColor;
    float originalJumpForce;
    float _maxHp;
    float originalMaxSpeed;

    //auxiliars
    bool _readyToAttack = true;
    [HideInInspector] public bool isGettingWet = false;
    [HideInInspector] public bool isAttacking = false;
    [HideInInspector] public bool isPaperPlaneHat = false; //pph es paper plane hat
    [HideInInspector] public bool isSprinting; //cuando tocas shift
    [HideInInspector] public int augmentedJumpsLeft;
    [HideInInspector] public Vector3 lastDirection;

    //State Machine
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    //MVC
    PlayerModel _model;
    [HideInInspector] public PlayerView _view;
    PlayerController _controller;

    public GameObject nuevoTooltipPapelSalto;



    //Properties
    public float Speed
    {
        get
        {
            return _maxSpeed;
        }
        set
        {
            _maxSpeed = value;
        }
    }
    public float Vida
    {
        get
        {
            return _hp;
        }
        set
        {
            _hp = value;
            if (_hp > _maxHp)
            {
                _hp = _maxHp;
            }

            if (_hp < 0)
            {
                _hp = 0;
            }
            EventManager.Trigger(Evento.OnPlayerChangeVida, _hp, _maxHp);
        }
    }
    public bool IsSprinting
    {
        get
        {
            return isSprinting;
        }
        set
        {
            isSprinting = value;
            if (!hasSprintBoots)
            {
                return;
            }
            if (isSprinting)
            {
                //Debug.Log("voy rapidin");
                Speed *= sprintingSpeedModifier;
                _view.StartSprint();
            }
            else
            {
                //Debug.Log("voy normal");
                Speed = originalMaxSpeed;
                _view.EndSprint();
            }
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
        //originalColor = _renderer.material.color;
        originalColor = kamiRenderer.material.GetColor("_DiffuseColor");

        augmentedJumpsLeft = augmentedJumpsMax;
        originalJumpForce = jumpForce;
        originalMaxSpeed = _maxSpeed;

        if (SkeletonAnimation == null)
        {
            SkeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        }

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
        //todo el tiempo chequeo los controles. eso seguro.
        _controller.CheckControls();

        if (_controller.Inputs.hor != 0 || _controller.Inputs.ver != 0) //si estoy tocando cualquier WASD, triggerea evento
        {
            EventManager.Trigger(Evento.OnPlayerMove, _controller.Inputs.hor, _controller.Inputs.ver);
        }

        _model.Tick(_controller.Inputs); //el model decide que hacer con los inputs segun el estado actual
    }

    //State Machine
    public void SetState(PlayerState newState)
    {
        if (newState == CurrentState)
        {
            return;
        }

        PlayerState previous = CurrentState;
        CurrentState = newState;
        _view.OnStateChanged(previous, newState);
    }

    bool CanAttack()
    {
        switch (CurrentState)
        {
            case PlayerState.Landing:
            case PlayerState.Casting:
            case PlayerState.ReceivingReward:
            case PlayerState.Dead:
                return false;
            default:
                return true; //idle, walk, skip, run, jump, fall: se puede atacar
        }
    }

    public void OnPrimaryClick()
    {
        if (LevelManager.Instance.inDialogue)
        {
            return;
        }

        if (_readyToAttack && hasTijera && CanAttack()) //readytoattack esta false cuando estoy en cooldown
        {
            _readyToAttack = false;
            isAttacking = true;
            _view.StartAttack();
        }
    }

    public void StartPasoSFX(int step) //del spine event me dicen en que paso de la animation estoy.
    {
        _view.StartPasoSFX(step); //le paso el valor y el view se encarga
    }
    public void StartTijeraCoroutine() //disparado por el spine event de la animacion de ataque
    {
        StartCoroutine(TijeraCoroutine());
    }
    public IEnumerator TijeraCoroutine() //prendo y apago rapidamente la hitbox en el momento justo para simular un ataque
    {
        _model.EnableTijeraHitbox();
        yield return new WaitForSeconds(tijeraHitBoxDuration);
        _model.DisableTijeraHitbox();

        yield return new WaitForSeconds(weaponCooldown); //los ataques tienen cooldown, asi que espero
        _readyToAttack = true;
        isAttacking = false;
        _view.EndAttack();
    }
    public void StartTijeraParticles()
    {
        tijeraManager.EnableTijeraParticles();
    }
    public void StopTijeraParticles()
    {
        tijeraManager.DisableTijeraParticles();
    }


    //Entity
    public override void TakeDamage(float dmg)
    {
        Vida -= dmg;
        if (Vida <= 0)
        {
            Die();
            isGettingWet = false;
        }
        else
        {
            _view.PlayTakeHitAnimation(); //se superpone en el track 2, no bloquea nada
        }
        StartCoroutine(EnrojecerSprite());
    }
    public IEnumerator EnrojecerSprite()
    {
        //print("enrojeci el sprite");
        kamiRenderer.material.SetColor("_DiffuseColor", Color.red);
        yield return new WaitForSeconds(0.25f);
        kamiRenderer.material.SetColor("_DiffuseColor", originalColor);
    }
    public override void Die()
    {
        //print("player: me mori");
        SetState(PlayerState.Dead); //no puede hacer nada hasta que respawnee
        Vida = _maxHp;
        AudioManager.instance.PlayByName("GameOverOrchestral");
        EventManager.Trigger(Evento.OnPlayerDie);
        PlayerPageSpawnManager.Instance.RespawnPlayer(); //spawnea al player en el inicio de la pagina actual
    }
    private void OnPlayerPlaced(object[] parameters)
    {
        //el spawn manager avisa cuando reposiciono al player: si estaba muerto, revive
        if (CurrentState == PlayerState.Dead)
        {
            SetState(PlayerState.Idle);
        }
    }

    //Interfaces
    public void GetWet(float wetDamage)
    {
        //mojarse se moja siempre. solo que con las botas no te hace da�o
        isGettingWet = true;

        if (!hasWaterBoots)
        {
            _view.StartGetWetAnimation(); //solo el trigger de arranque. los pasos van aparte
            StartCoroutine(DrowningCoroutine(wetDamage));
        }
    }
    public void StopGettingWet()
    {
        isGettingWet = false;
    }
    public IEnumerator DrowningCoroutine(float wetDamage)
    {
        while (isGettingWet)
        {
            TakeDamage(wetDamage);
            yield return new WaitForSeconds(0.8f);
        }
    }
    public void GetGolpeado(float dmg)
    {
        //print("me han golpeao");
        _view.StartGetGolpeadoAnimation(); //es solo x el sonido por ahora
        TakeDamage(dmg);
    }
    public void GetCured(int curacion)
    {
        Vida += curacion;
    }

    //Eventos
    public void GetTijera(params object[] parameters)
    {
        hasTijera = true;
        LevelManager.Instance.AddResource(ResourceType.tijera, 1);
        tijeraManager.SetTijera();
    }
    public void GetTijeraMejorada()
    {
        //Debug.Log("player - get tijera mejorada");
        hasTijeraMejorada = true;
        tijeraManager.SetTijeraMejorada();
        miTijeraHitbox = tijeraManager.tijeraMejoradaHitbox;
    }
    public void StartOrigamiCast(params object[] parameters)
    {
        SetState(PlayerState.Casting); //no puede hacer nada mientras castea
    }
    public void EndOrigamiCast(params object[] parameters)
    {
        if (CurrentState == PlayerState.Casting) //guard: si murio en el medio, no lo revivas
        {
            SetState(PlayerState.Idle);
        }
    }
    public void GetPaperPlaneHat(params object[] parameters)
    {
        isPaperPlaneHat = true;
        jumpForce = augmentedJumpForce;
        augmentedJumpsLeft = augmentedJumpsMax;
        myPaperPlaneHat.SetActive(true);
        AudioManager.instance.PlayByName("ShipSpawn", 2f);
        nuevoTooltipPapelSalto.SetActive(true);
    }
    public void DestroyPaperPlaneHat(params object[] parameters)
    {
        isPaperPlaneHat = false;
        jumpForce = originalJumpForce;
        //sfx de hacer bollo y destruir
        myPaperPlaneHat.SetActive(false);
        TooltipManager.Instance.HideTooltip();
        nuevoTooltipPapelSalto.SetActive(false);
    }
    private void StartReceiveReward(object[] parameters)
    {
        SetState(PlayerState.ReceivingReward);
        StartCoroutine(WaitForReceiveRewardAnimation(rewardAnimationWaitTime));
    }
    public IEnumerator WaitForReceiveRewardAnimation(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        DialogueManager.Instance.lockedByAnimation = false;
    }
    private void EndReceiveReward(object[] parameters)
    {
        if (CurrentState == PlayerState.ReceivingReward)
        {
            SetState(PlayerState.Idle);
        }
    }

    //Utilities
    public void AddPlaning()
    {
        StartCoroutine(_model.AddExtraForwardForce(planeoDelayTime, planeoDuration, planeoImpulse, lastDirection));
    }
    public void GetAffectedByWind(float windForce, Vector3 windDirection)
    {
        _model.ForcedMove(windDirection * windForce);
    }
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
        particleShooter.Create(1, anim.transform);
    }
    public IEnumerator SlowDownCoroutine(float speedModifier, float time)
    {
        Speed = originalMaxSpeed * speedModifier;
        yield return new WaitForSeconds(time);
        Speed = originalMaxSpeed;
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
