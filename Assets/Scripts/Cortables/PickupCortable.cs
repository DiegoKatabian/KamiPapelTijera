using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PickupCortable : ObjetoCortable, IAplastable
{
    //los pickups cortables dan algun recurso, 
    //desaparecen despues de cortarlos,
    //y pueden respawnear

    [SerializeField] protected ResourceType pickupType;
    [SerializeField] protected int pickupAmount;

    [SerializeField, Tooltip("Optional AudioId played on top of the base cut sound (e.g. TypewriterClack on the paper typewriters). Empty = none.")]
    string _extraCutSound;

    protected override void ApplyCut()
    {
        base.ApplyCut();

        if (!string.IsNullOrEmpty(_extraCutSound) && AudioManager.instance != null)
        {
            AudioManager.instance.Play(_extraCutSound);
        }

        EventManager.Trigger(Evento.OnObjectWasCut, transform.position);
        
        LevelManager.Instance.AddResource(pickupType, pickupAmount);
        StartCoroutine(WaitForActionCoroutine(selfDestructTime, SelfDestruct));

        if (doesRespawn)
        {
            StartCoroutine(WaitForActionCoroutine(respawnTime, Respawn));
        }
    }
    public void Aplastar()
    {
        Debug.Log("este cortable fue aplastado");
        ApplyCut();
    }
}
