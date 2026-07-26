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

    Player _player;
    protected FiniteStateMachine _fsm;
    protected bool isDrowning;

    protected virtual void Start()
    {
        //Debug.Log("Rocoso Start");
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