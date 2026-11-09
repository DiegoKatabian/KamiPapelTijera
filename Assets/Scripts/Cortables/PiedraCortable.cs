using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PiedraCortable : PickupCortable
{
    [Header("Bounce feedback (spec 024 3.A)")]
    [Tooltip("Sparks where the normal scissors bounce off (Prefabs/Particulas/RocosoBounceSparks). Empty = no sparks.")]
    [SerializeField] ParticleSystem _bounceSparks;

    [Tooltip("Seconds before the spawned sparks are destroyed.")]
    [SerializeField] float _sparksLifetime = 1.5f;

    [Tooltip("Kami plays her take-hit animation when the scissors bounce (no damage).")]
    [SerializeField] bool _kamiReacts = true;

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
            if (isCortable)
            {
                CutBounce.Play(this, _bounceSparks, _sparksLifetime, _kamiReacts);
            }
        }
    }
}
