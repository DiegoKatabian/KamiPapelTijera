using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Abuela_DropoffState : IState
{
    FiniteStateMachine _fsm;
    NPC_Abuela _abuela;

    public Abuela_DropoffState(FiniteStateMachine fsm, NPC_Abuela npc)
    {
        _fsm = fsm;
        _abuela = npc;
    }

    public void OnEnter()
    {
        //Debug.Log("[NPC] entro al dropoff state");
        _abuela.SetWalkAnimation(true);
        //force the first path immediately: the dropoff point never moves, so waiting out the
        //repath interval would just be a visible pause before she sets off
        _abuela.MoveTowards(_abuela.dropoffPoint.position, force: true);
    }
    public void OnUpdate()
    {
        //Debug.Log("[NPC]  state");

        _abuela.MoveTowards(_abuela.dropoffPoint.position);
        _abuela.UpdateSpriteFlip();

        if (_abuela.HasArrived())
        {
            _abuela.StopAgent();
            _abuela.isDropoff = false;
            _fsm.ChangeState(State.NPC_Idle);
        }
    }
    public void OnExit()
    {
        //Debug.Log("[NPC] salgo del dropoff state");
        _abuela.SetWalkAnimation(false);
    }
}
