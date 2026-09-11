using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PiedraCortable : PickupCortable
{
    //las piedras cortables chequean q esten siendo cortadas por
    //la tijera mejorada

    public override void GetCut(float dmg)
    {
        if (isCortable && dmg >= 100) //re trucho. solo funca xq la tijera mejorada pega 100 jajajaj
        {
            ApplyCut();
        }
        else
        {
            AudioManager.instance.Play(AudioId.Tijera_Miss_Roca);
        }
    }
}
