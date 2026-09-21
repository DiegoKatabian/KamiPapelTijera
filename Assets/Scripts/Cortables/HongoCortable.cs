using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HongoCortable : PickupCortable
{
    //el hongo cortable debe cambiar la animacion de la gallina
    [SerializeField] GallinaSounds gallinaSounds;

    protected override void ApplyCut()
    {
        print("cortaste el hongo!");
        AudioManager.instance.Play(AudioId.TijeraHit);
        AudioManager.instance.Play(AudioId.PaperCut);
        AudioManager.instance.Play(AudioId.MagicSuccess, 2f);
        gallinaSounds.PlayCortadaSound();

        SepararSprites();

        isCortable = false;
        LevelManager.Instance.AddResource(pickupType, pickupAmount);
        StartCoroutine(WaitForActionCoroutine(selfDestructTime, SelfDestruct));

        if (doesRespawn)
        {
            StartCoroutine(WaitForActionCoroutine(respawnTime, Respawn));
        }
    }
}


