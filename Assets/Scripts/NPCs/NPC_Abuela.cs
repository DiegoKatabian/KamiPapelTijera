using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC_Abuela : NPC
{
    //este script maneja los states y movimientos de la abuela
    //nada que ver con los dialogos. eso esta en el dialoguetrigger


    //creo que solo va a moverse desde ser entregada hasta la mesa.

    //Follow/idle now come from the shared NPC FSM (same behaviour Natalia uses) instead of the
    //duplicated Abuela_IdleState/Abuela_FollowPlayerState this class used to own -- those two were
    //deleted 2026-09-22. Her walk animation and sprite flip moved onto the base as options, and
    //the dropoff, the one genuinely Abuela-specific state, stays here via TryExtraTransitions().
    //This also fixed a latent crash: the old follow state changed to State.NPC_Idle, which this
    //class never registered, and FiniteStateMachine.ChangeState throws on a missing key.

    [HideInInspector] public bool isDropoff;
    public Transform dropoffPoint;
    public Transform unfoldPoint;
    Vector3 originalScale;

    protected override void Start()
    {
        base.Start();
        _fsm.AddState(State.Abuela_Dropoff, new Abuela_DropoffState(_fsm, this));
        originalScale = transform.localScale;
    }

    protected internal override bool TryExtraTransitions()
    {
        if (!isDropoff)
        {
            return false;
        }

        //being carried to the table outranks following: stop the follow first so that finishing
        //the dropoff returns her to a genuine idle instead of bouncing straight back into follow
        StopFollowingPlayer();
        _fsm.ChangeState(State.Abuela_Dropoff);
        return true;
    }

    public void GetFolded()
    {
        //Debug.Log("get folded");
        _sr.enabled = false;
    }

    public void GetUnfolded()
    {
        //Debug.Log("get unfolded");
        _sr.enabled = true;
        StartAbuelaDropoff();
    }

    public void StartAbuelaDropoff(params object[] parameter)
    {
        isDropoff = true;
    }
    public void PlaceAbuelaAtUnfoldPoint(params object[] parameter)
    {
        transform.parent = unfoldPoint.transform;
        transform.position = unfoldPoint.position;
        transform.localScale = originalScale;
    }


    //private void OnDestroy()
    //{
    //    if (!gameObject.scene.isLoaded)
    //    {

    //    }
    //}
}
