using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC_FollowPlayerState : IState
{
    FiniteStateMachine _fsm;
    NPC _npc;


    public NPC_FollowPlayerState(FiniteStateMachine fsm, NPC npc)
    {
        _fsm = fsm;
        _npc = npc;

    }

    public void OnEnter()
    {
        Debug.Log("[NPC] entro al follow player state");
        _npc.SetWalkAnimation(true);
    }
    public void OnUpdate()
    {
        _npc.MoveTowards(_npc.player.transform.position);
        _npc.UpdateSpriteFlip();

        //stop the walk cycle once the agent is parked next to Kami, instead of moonwalking in place
        _npc.SetWalkAnimation(!_npc.HasArrived());

        //an NPC with extra states of its own (Abuela's dropoff) gets first say
        if (_npc.TryExtraTransitions())
        {
            return;
        }

        if (!_npc.isFollowing)
        {
            _fsm.ChangeState(State.NPC_Idle);
        }

    }
    public void OnExit()
    {
        Debug.Log("[NPC] salgo del followplayer state");
        _npc.SetWalkAnimation(false);
    }
}
