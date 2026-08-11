using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : Entity
{
    //los npcs tienen 2 estados, idle o seguirte. por ahora.
    //tambien tienen vida, speed y atk damage (heredan de entities)
    //pueden recibir daño pero no pueden morir

    protected FiniteStateMachine _fsm;
    public SpriteRenderer _sr;

    public bool isFollowing;

    public Player player; //seria genial que no dependan del player para laburar
    public float playerOffsetDistance = 10;
    public float arriveRadius = 5;

    [HideInInspector] public Vector3 velocity;
    public float maxForce = 50;

    protected virtual void Start()
    {
        _fsm = new FiniteStateMachine();
        _fsm.AddState(State.NPC_Idle, new NPC_IdleState(_fsm, this));
        _fsm.AddState(State.NPC_FollowPlayer, new NPC_FollowPlayerState(_fsm, this));
        _fsm.ChangeState(State.NPC_Idle);
    }

    protected void Update()
    {
        transform.position += velocity * Time.deltaTime;
        _fsm.Update();
    }

    public override void TakeDamage(float dmg)
    {
        base.TakeDamage(dmg);
        StartCoroutine(EnrojecerSprite());
    }
    public IEnumerator EnrojecerSprite()
    {
        //print("enrojeci el sprite");
        _sr.material.color = Color.red;
        yield return new WaitForSeconds(0.25f);
        _sr.material.color = Color.white;
    }
    public override void Die(DeathCause cause = DeathCause.Generic)
    {
        TooltipManager.Instance.ShowTooltip("Este NPC no puede morir", PostItColor.Verde);
    }

    public void AddForce(Vector3 force)
    {
        velocity = Vector3.ClampMagnitude(velocity + force, _maxSpeed);
    }
    public Vector3 FollowPlayer()
    {
        Vector3 offset = player.transform.position + (player.transform.forward * playerOffsetDistance);
        return Arrive(offset);
    }
    public Vector3 Arrive(Vector3 pos)
    {
        Vector3 desired = pos - transform.position;
        if (desired.magnitude <= arriveRadius)
        {
            //estoy dentro del radio, me voy frenando
            float newSpeed = _maxSpeed * (desired.magnitude / arriveRadius);
            desired.Normalize();
            desired *= newSpeed;
        }
        else
        {
            //seek normal mientras estoy lejos del objeto
            desired.Normalize();
            desired *= _maxSpeed;
        }

        Vector3 steering = desired - velocity;
        steering = Vector3.ClampMagnitude(steering, maxForce);
        return steering;
    }
}
