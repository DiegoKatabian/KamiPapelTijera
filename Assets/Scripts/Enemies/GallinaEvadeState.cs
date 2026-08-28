using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GallinaEvadeState : IState
{
    FiniteStateMachine _fsm;
    GallinaAI _gallina;

    public GallinaEvadeState(FiniteStateMachine fsm, GallinaAI r)
    {
        _fsm = fsm;
        _gallina = r;
    }

    public void OnEnter()
    {
        //Debug.Log("gallina - entre a evade");
        _gallina.gallinaSounds.PlayEvadeSound();
    }

    public void OnUpdate()
    {
        //si completamos la quest (arbol cayo), ignora al player y vuelve a Walk
        if (_gallina.questCompleted)
        {
            _fsm.ChangeState(State.GallinaWalk);
            return;
        }

        _gallina.RotateAccordingly();
        WalkAwayFromPlayer();
        if (!_gallina.playerIsInRange)
        {
            _fsm.ChangeState(State.GallinaWalk);
        }
    }

    public void OnExit()
    {
        //Debug.Log("salgo de walk");
        //Debug.Log("gallina - entre a walk");
    }

    public void WalkAwayFromPlayer()
    {
        Vector3 dir = _gallina._player.transform.position - _gallina.transform.position;
        dir.y = 0;
        _gallina.rb.AddForce(_gallina.evadeSpeed * Time.deltaTime * -dir.normalized, ForceMode.VelocityChange);
    }
}
