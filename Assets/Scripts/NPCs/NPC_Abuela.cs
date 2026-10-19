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

    [Header("Level 1 rocoso fight (Spine poses)")]
    [SerializeField, Tooltip("Pose she stands in while Kami fights the rocoso (rest pose: it survives her walking). Empty = none.")]
    string _fightPose = "Scared";

    [SerializeField, Tooltip("Pose played once when the rocoso is beaten. Empty = none.")]
    string _fightWonPose = "Celebrate";

    protected override void Start()
    {
        base.Start();
        _fsm.AddState(State.Abuela_Dropoff, new Abuela_DropoffState(_fsm, this));
        originalScale = transform.localScale;

        EventManager.Subscribe(Evento.OnEncounterStart, OnEncounterStarted);
        EventManager.Subscribe(Evento.OnEncounterEnd, OnEncounterEnded);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnEncounterStart, OnEncounterStarted);
        EventManager.Unsubscribe(Evento.OnEncounterEnd, OnEncounterEnded);
    }

    //Level 1's only encounter is the rocoso boss (the Level 2 follower never sees one)
    void OnEncounterStarted(params object[] parameters)
    {
        if (Character != null && !string.IsNullOrEmpty(_fightPose))
        {
            Character.SetRestPose(_fightPose);
        }
    }

    void OnEncounterEnded(params object[] parameters)
    {
        if (Character == null)
        {
            return;
        }

        if (Character.RestPoseId != null && string.Equals(Character.RestPoseId, _fightPose, System.StringComparison.OrdinalIgnoreCase))
        {
            Character.ClearRestPose();
        }

        if (!string.IsNullOrEmpty(_fightWonPose))
        {
            Character.PlayPose(_fightWonPose);
        }
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
        SetVisible(false);
    }

    public void GetUnfolded()
    {
        //Debug.Log("get unfolded");
        SetVisible(true);
        StartAbuelaDropoff();
    }

    //she is drawn by her Spine skeleton since 2026-10-19; _sr is kept for a sprite-only instance
    void SetVisible(bool visible)
    {
        Renderer body = _sr;
        if (body == null && Character != null && Character.SkeletonAnimation != null)
        {
            body = Character.SkeletonAnimation.GetComponent<Renderer>();
        }

        if (body == null)
        {
            Debug.LogWarning($"[NPC_Abuela] {gameObject.name}: no SpriteRenderer or Spine skeleton to {(visible ? "show" : "hide")}");
            return;
        }

        body.enabled = visible;
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
        ResetSkeletonPhysics();
    }


    //private void OnDestroy()
    //{
    //    if (!gameObject.scene.isLoaded)
    //    {

    //    }
    //}
}
