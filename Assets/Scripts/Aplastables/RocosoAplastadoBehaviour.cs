using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocosoAplastadoBehaviour : AplastadoBehaviour
{
    [SerializeField] Rocoso _miRocoso;

    public override void Aplastar()
    {
        //Debug.Log("Aplastado: Aplastar");
        StartCoroutine(LerpScaleY(transform, lerpTime));
        _miRocoso.StartCoroutine(_miRocoso.MorirCoroutine());
        AudioManager.instance.Play(AudioId.RockSmash);
    }
}
