using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

public enum TijeraType
{
    Normal,
    Mejorada
}

public class TijeraManager : MonoBehaviour
{
    //este script se encarga de ponerle la tijera correcta a kami
    //le cargas las hitbox y listo

    public TijeraHitbox tijeraHitbox;
    public TijeraHitbox tijeraMejoradaHitbox;

    public ParticleSystem tijeraParticles, tijeraTrail, tijeraMejoradaParticles, tijeraMejoradaTrail;

    [SerializeField] string tipBoneName = "tijera_front3"; //el hueso de la punta de la tijera en el skeleton de spine

    TijeraType currentTijera;

    public void InitTipFollowers(SkeletonAnimation kamiSkeleton) //lo llama player en su start
    {
        //los trails quedan pegados a la punta de la tijera de spine, sin importar de quien sean hijos
        AttachTipFollower(tijeraTrail, kamiSkeleton);
        AttachTipFollower(tijeraMejoradaTrail, kamiSkeleton);
    }

    void AttachTipFollower(ParticleSystem trail, SkeletonAnimation kamiSkeleton)
    {
        if (trail == null || kamiSkeleton == null)
        {
            return;
        }

        SpineBoneTipFollower follower = trail.GetComponent<SpineBoneTipFollower>();
        if (follower == null)
        {
            follower = trail.gameObject.AddComponent<SpineBoneTipFollower>();
        }
        follower.Init(kamiSkeleton, tipBoneName);
    }

    public void SetTijera(params object[] parameters)
    {
        //Debug.Log("prendo la tijera");
        tijeraHitbox.transform.parent.gameObject.SetActive(true);
        currentTijera = TijeraType.Normal;
    }

    public void SetTijeraMejorada(params object[] parameters)
    {
        //Debug.Log("prendo la tijera mejorada");
        tijeraHitbox.transform.parent.gameObject.SetActive(false);
        tijeraMejoradaHitbox.transform.parent.gameObject.SetActive(true);
        currentTijera = TijeraType.Mejorada;
    }

    public void EnableTijeraParticles()
    {
        switch (currentTijera)
        {
            case TijeraType.Normal:
                tijeraParticles.gameObject.SetActive(true);
                break;
            case TijeraType.Mejorada:
                tijeraMejoradaParticles.gameObject.SetActive(true);
                break;
        }
    }

    public void DisableTijeraParticles()
    {
        switch (currentTijera)
        {
            case TijeraType.Normal:
                tijeraParticles.gameObject.SetActive(false);
                break;
            case TijeraType.Mejorada:
                tijeraMejoradaParticles.gameObject.SetActive(false);
                break;
        }
    }

    public void SetTrailRadius(float newRadius)
    {
        ParticleSystem.ShapeModule shape = tijeraTrail.shape;
        shape.radius = newRadius;
    }
}
