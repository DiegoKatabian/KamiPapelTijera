using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocosoStartState : IState
{
    FiniteStateMachine _fsm;
    Rocoso _rocoso;

    public RocosoStartState(FiniteStateMachine fsm, Rocoso r)
    {
        _fsm = fsm;
        _rocoso = r;
    }

    public void OnEnter()
    {
        _rocoso.startAnimationHasFinished = false;
        _rocoso.anim.SetTrigger("isWakeUp");
        EventManager.Trigger(Evento.OnRocosoWokeUp, _rocoso.endsEncounter);
        if (_rocoso.endsEncounter)
        {
            AudioManager.instance.Play(AudioId.RocosoWakeUp);
        }

    }

    public void OnUpdate()
    {
        if (_rocoso.startAnimationHasFinished) //en el rocoso esto se va a poner true en el ultimo frame de la anim
        {
            //Debug.Log("start: me paso a walk");
            _fsm.ChangeState(State.RocosoWalk);
        }

        if (_rocoso.isDead)
        {
            //Debug.Log("start - ONUPDATE - me cambio a death porque me mori");
            _fsm.ChangeState(State.RocosoDeath);
        }
    }

    public void OnExit()
    {
        //Debug.Log("salgo de start");
    }
}
