using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Rocoso : Enemy
{
    public Rigidbody myRigidbody;
    public Animator anim; //mi animator
    public RocosoHeadbuttHitBox _hitBox;
    [SerializeField] float hitboxDuration = 0.2f;
    public float enterAttackRange = 11; //si el pj se acerca a 11, le pego
    public float exitAttackRange = 30; //si se aleja a 30, dejo de pegarle y lo vuelvo a perseguir
    public float viewRange = 60; //me despierto si el pj se acerca a 50 o menos. me duermo si se aleja eso
    [SerializeField] protected GameObject _particulasSplash;

    public bool startAnimationHasFinished = false;
    public bool playerEnteredWakeUpCollider = false;
    [HideInInspector] public Vector3 target;
    [HideInInspector] public bool isHitting;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool deathAnimationEnded = false;

    [Header("Facing")]
    [SerializeField]
    [Tooltip("Anclas que tienen que quedar SIEMPRE delante del sprite (las particulas de rayos). " +
             "Si se deja vacio, se usan todos los ParticleSystem hijos.")]
    Transform[] _anclasFrontales;

    //posicion local original de cada ancla, para poder espejar su Z sin acumular error
    readonly Dictionary<Transform, Vector3> _posicionesBaseAnclas = new Dictionary<Transform, Vector3>();
    bool _mirandoAlaDerecha;
    bool _facingInicializado;

    Player _player;
    protected FiniteStateMachine _fsm;
    protected bool isDrowning;

    protected virtual void Start()
    {
        //Debug.Log("Rocoso Start");
        CachearAnclasFrontales();
        _fsm = new FiniteStateMachine();
        _fsm.AddState(State.RocosoSleep, new RocosoSleepState(_fsm, this));
        _fsm.AddState(State.RocosoStart, new RocosoStartState(_fsm, this));
        _fsm.AddState(State.RocosoWalk, new RocosoWalkState(_fsm, this));
        _fsm.AddState(State.RocosoAttack, new RocosoAttackState(_fsm, this));
        _fsm.AddState(State.RocosoDeath, new RocosoDeathState(_fsm, this));
        _fsm.ChangeState(State.RocosoSleep);
        _hitBox.headbuttDamage = _attackDamage;
    }

    protected void Update()
    {
        _fsm.Update();

        if (_player != null)
        {
            target = _player.transform.position;
        }
    }

    void CachearAnclasFrontales()
    {
        //si nadie las wireo en el inspector, agarramos las particulas hijas: hoy son los rayos
        //de enojo, que estan puestas en la escena sobre la instancia (no vienen en el prefab)
        if (_anclasFrontales == null || _anclasFrontales.Length == 0)
        {
            ParticleSystem[] particulas = GetComponentsInChildren<ParticleSystem>(true);
            _anclasFrontales = new Transform[particulas.Length];
            for (int i = 0; i < particulas.Length; i++)
            {
                _anclasFrontales[i] = particulas[i].transform;
            }

            if (_anclasFrontales.Length == 0)
            {
                Debug.LogWarning($"[Rocoso] {name} no tiene anclas frontales ni particulas hijas: " +
                                 "si le agregas particulas delante de la cabeza, van a quedar detras del sprite al girar.");
            }
        }

        foreach (Transform ancla in _anclasFrontales)
        {
            if (ancla == null)
            {
                Debug.LogWarning($"[Rocoso] {name} tiene un ancla frontal vacia en el inspector, la salteo.");
                continue;
            }

            _posicionesBaseAnclas[ancla] = ancla.localPosition;
        }
    }

    /// <summary>
    /// Unico punto que decide hacia donde mira el Rocoso. Rota el transform como siempre (de eso
    /// dependen los offsets en X de RocosoAttackHitBox y MojableCollider, asi que NO se puede
    /// reemplazar por un flipX del sprite) y ademas corrige la profundidad de las anclas frontales.
    ///
    /// POR QUE lo segundo: girar 180 grados en Y invierte el eje Z local, no solo el X. El X que se
    /// invierta es lo que queremos (los rayos acompanan a la cabeza), pero el Z hace que las
    /// particulas, que estan 0.18 delante del plano del sprite, terminen 0.18 DETRAS y las tape el
    /// propio sprite. Por eso se les espeja la Z a contramano del giro: asi quedan siempre al frente.
    /// </summary>
    public void SetFacing(bool mirandoAlaDerecha)
    {
        if (_facingInicializado && _mirandoAlaDerecha == mirandoAlaDerecha)
        {
            return; //ya estabamos mirando para ese lado, no rehacemos nada
        }

        _mirandoAlaDerecha = mirandoAlaDerecha;
        _facingInicializado = true;

        transform.rotation = mirandoAlaDerecha ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        foreach (KeyValuePair<Transform, Vector3> par in _posicionesBaseAnclas)
        {
            Transform ancla = par.Key;
            if (ancla == null)
            {
                continue; //la escena pudo destruirla (ej. al morir el Rocoso)
            }

            Vector3 baseLocal = par.Value;
            float z = mirandoAlaDerecha ? -baseLocal.z : baseLocal.z;
            ancla.localPosition = new Vector3(baseLocal.x, baseLocal.y, z);
        }
    }

    //triggered by animator
    public void OnStartAnimationEnd() //disparada por el final de la animacion de start
    {
        startAnimationHasFinished = true;
    }
    protected void PlayHeadbuttSound() //se dispara por la animacion
    {
        AudioManager.instance.PlayByName("RocosoHeadbutt", 1f, 0.02f);
    }
    protected void PlayPasoSound()
    {
        AudioManager.instance.PlayByName("RocosoPaso", 1f, 0.05f);
    }
    protected IEnumerator HeadbuttCoroutine() //esto se dispara en el momento correcto de la animacion de cabezazo
    {
        EnableHeadbuttHitbox();
        yield return new WaitForSeconds(hitboxDuration);
        DisableHeadbuttHitbox();
    }
    public void DeathAnimationEnd() //esto lo dispara el animator (especificamente, el final de clip de death)
    {
        //Debug.Log("termina el clip de muerte");
        deathAnimationEnded = true;
    }
    
    //utilities
    public void EnableHeadbuttHitbox()
    {
        //Debug.Log("prendo el headbutt");
        _hitBox.gameObject.SetActive(true);
    }
    public void DisableHeadbuttHitbox()
    {
        //Debug.Log("apago el headbutt");
        _hitBox.gameObject.SetActive(false);
        isHitting = false;
    }

    //interfaces and triggers
    public virtual void OnPlayerEnterWakeUpCollider(Player player) //este metodo es disparado por el trigger, solo la primera vez
    {
        _player = player;
        playerEnteredWakeUpCollider = true;
    }
    public void GetWet(float wetDamage) //esto se dispara cuando collisiona con el rio
    {
        //Debug.Log("rocoso se moja");

        _particulasSplash.SetActive(true);
        AudioManager.instance.PlayByName("RocosoMojado");

        isDrowning = true;
        StartCoroutine(DrowningCoroutine(wetDamage));
    }
    public void StopGettingWet()
    {
        isDrowning = false;
    }
    public IEnumerator DrowningCoroutine(float wetDamage)
    {
        while (isDrowning)
        {
            TakeDamage(wetDamage * 20);
            yield return new WaitForSeconds(0.8f);
        }
    }

    //dmg and death
    public override void TakeDamage(float dmg)
    {
        AudioManager.instance.PlayByName("PaperCut01", 0.8f);
        _hp -= dmg;
        if (_hp <= 0)
        {
            StartCoroutine(MorirCoroutine());
        }
        StartCoroutine(EnrojecerSprite());
    }
    public IEnumerator MorirCoroutine() //la corrutina esta es solo para esperar a la anim antes de disparar Die()
    {
        //Debug.Log("arranco corrutina de morir");
        isDead = true; //necesito un bool para que se haga el cambio de state. pero todavia no quiero morir posta
        float timer = 0;
        //espero a que termine la animacion de muerte
        while (!deathAnimationEnded && timer < 3)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        Die();
    }
    protected void OnDisable()
    {
        //cuando cambias de pagina el rocoso se apaga
        //cuando volves a la pagina en la que estaba, el animator se reinicia y vuelve a estar dormido
        //esta linea es para que despues de reiniciarse, vuelva al estado en el que estaba antes

        if (isDead)
        {
            Die();
            return;
        }

        //Debug.Log("me deshabilitaron");
        _fsm.ChangeState(State.RocosoSleep);
    }

    protected void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enterAttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, exitAttackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, viewRange);

        if (_player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _player.transform.position);
        }
    }

    public float DistanceToPlayer()
    {
        return Vector3.Distance(target, transform.position);
    }
    public bool PlayerIsInViewRange()
    {
        return DistanceToPlayer() < viewRange;
    }
}