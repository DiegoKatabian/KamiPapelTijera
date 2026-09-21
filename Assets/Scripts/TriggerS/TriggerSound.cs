using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerSound : TriggerScript
{
    [SerializeField]
    string soundName;

    public override void OnEnterBehaviour(Collider other)
    {
        //print("entro el player");
        triggerBool = true;
        TooltipManager.Instance.ShowTooltip(tooltipTextToShow, PostItColor.Verde);
        
        if (soundName == "4S_MarimbaLoopConPiano")
        {

            AudioManager.instance.PlayOnEnd("4S_MarimbaLoop", "4S_MarimbaLoopConPiano");
        }
        else
        {
            AudioManager.instance.Play(soundName);
        }

        Destroy(gameObject);
    }

}
