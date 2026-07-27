using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerTijeraPickup : TriggerScript
{
    [SerializeField] bool isAutoPickup = true;

    public override void OnEnterBehaviour(Collider other)
    {
        base.OnEnterBehaviour(other);

        if (isAutoPickup)
        {
            PickupTijera();
        }
    }


    public override void Interact(params object[] parameter)
    {
        if (isAutoPickup)
        {
            return;
        }

        if (triggerBool)
        {
            PickupTijera();
        }
    }

    public void PickupTijera()
    {
        EventManager.Trigger(Evento.OnPlayerGetTijera);
        AudioManager.instance.PlayByName("PickupSpecial", 1f);
        //CameraManager.Instance.SetCamera(CameraMode.General);

        OnExitBehaviour();
        Destroy(gameObject);
    }
}
