using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerAbuelaDropoff : TriggerScript
{
    [SerializeField] NPC_Abuela abuela;
    [SerializeField] Transform dropOffPoint;

    public override void OnEnterBehaviour(Collider other)
    {
        if (abuela.isFollowing)
        {
            EventManager.Trigger(Evento.OnAbuelaDropoff, dropOffPoint);
            AudioManager.instance.Play(AudioId.MagicSuccess, 0.6f);
            base.OnEnterBehaviour(other);
        }
    }
}
