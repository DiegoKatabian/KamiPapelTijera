using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum State
{
    //Enemy: rocoso
    RocosoSleep,
    RocosoStart,
    RocosoWalk,
    RocosoAttack,
    RocosoDeath,

    //NPCs (Abuela_Idle/Abuela_FollowPlayer removed 2026-09-22: Abuela uses the shared
    //NPC_Idle/NPC_FollowPlayer states now, same as Natalia. Dropoff is still hers alone.)
    NPC_Idle,
    NPC_FollowPlayer,
    Abuela_Dropoff,

    //Barquito
    BarquitoIdle,
    BarquitoMoving,

    //Enemy: gallina
    GallinaWalk,
    GallinaEvade
}
public class FiniteStateMachine
{
    IState _currentState;
    Dictionary<State, IState> allStates = new Dictionary<State, IState>();

    public void Update()
    {
        _currentState.OnUpdate();
    }
    public void ChangeState(State state)
    {
        if (_currentState != null)
        {
            _currentState.OnExit();
        }
        _currentState = allStates[state];
        _currentState.OnEnter();
    }
    public void AddState(State key, IState value)
    {
        if (!allStates.ContainsKey(key))
        {
            allStates.Add(key, value);
        }
        else
        {
            allStates[key] = value;
        }
    }
}
