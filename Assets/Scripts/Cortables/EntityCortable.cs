using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityCortable : MonoBehaviour, ICortable
{
    [SerializeField]
    Entity _thisEntity;

    public virtual void GetCut(float dmg)
    {
        //print("entity: me cortaron");
        AudioManager.instance.Play(AudioId.TijeraHit);
        //_thisEntity.TakeDamage(dmg);
    }
}
