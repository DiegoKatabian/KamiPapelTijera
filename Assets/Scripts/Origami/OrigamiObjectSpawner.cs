using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrigamiObjectSpawner : Origami
{
    //este script va en la RUTA, no en el objeto puente
    [SerializeField]
    GameObject objetoParaSpawnear;

    public override void Apply()
    {
        base.Apply();
        objetoParaSpawnear.SetActive(true);
        AudioManager.instance.Play(AudioId.BridgeCompleted);
        //soniditos y particulas de puente spawneado
    }
}
