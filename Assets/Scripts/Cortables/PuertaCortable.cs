using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuertaCortable : MonoBehaviour, ICortable
{
    public void GetCut(float dmg)
    {
        print("cortaste la puerta");
        AudioManager.instance.Play(AudioId.TijeraHit);
        //AudioManager.instance.PlayByName("MagicSuccess");


        //TooltipManager.instance.HideTooltip();
        Destroy(gameObject);
    }
}
