using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrigamiShip : Origami
{
    //este script va en la RUTA, no en el objeto barco
    //spawnea un gameobject

    [SerializeField]
    GameObject barquito;

    public override void Apply()
    {
        base.Apply();
        barquito.SetActive(true);
        AudioManager.instance.Play(AudioId.ShipSpawn);
        //particulas
    }
}
