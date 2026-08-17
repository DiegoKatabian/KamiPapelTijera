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

    [Tooltip("las hitbox y las particulas de ataque viven aca abajo. el player orienta este transform al atacar y todo se alinea junto")]
    public Transform hitboxParent;

    [SerializeField] string tipBoneName = "Trail"; //el hueso de la punta de la tijera en el skeleton de spine

    TijeraType currentTijera;

    bool tijeraTrailIsOn, tijeraMejoradaTrailIsOn;

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
        currentTijera = TijeraType.Normal;
        tijeraTrail.gameObject.SetActive(true); //el trail queda prendido de aca en adelante (sigue la punta de la tijera de spine)
        tijeraMejoradaTrail.gameObject.SetActive(false);
    }

    public void SetTijeraMejorada(params object[] parameters)
    {
        //Debug.Log("prendo la tijera mejorada");
        currentTijera = TijeraType.Mejorada;
        tijeraTrail.gameObject.SetActive(false);
        tijeraMejoradaTrail.gameObject.SetActive(true);
    }

    //las particulas de ataque estan siempre activas (hijas del hitboxParent): aca solo se les da play/stop
    public void EnableTijeraParticles()
    {
        CurrentAttackParticles().Play();
    }

    public void DisableTijeraParticles()
    {
        CurrentAttackParticles().Stop();
    }

    ParticleSystem CurrentAttackParticles()
    {
        return currentTijera == TijeraType.Mejorada ? tijeraMejoradaParticles : tijeraParticles;
    }

    public void SetTrailRadius(float newRadius)
    {
        ParticleSystem.ShapeModule shape = tijeraTrail.shape;
        shape.radius = newRadius;
    }

    public void TurnOffTrailsDuringChangePage()
    {
        //get the current state of the tijeratrail and mejoradatrail, if theyre on turn them off

        tijeraTrailIsOn = tijeraTrail.gameObject.activeSelf;
        tijeraMejoradaTrailIsOn = tijeraMejoradaTrail.gameObject.activeSelf;

        if (tijeraTrailIsOn)
        {
            tijeraTrail.gameObject.SetActive(false);
        }

        if (tijeraMejoradaTrailIsOn)
        {
            tijeraMejoradaTrail.gameObject.SetActive(false);
        }
    }

    public void TurnBackOnTrailsAfterChangePage()
    {
        //if any of the trail had to be turned off, turn it on again

        if (tijeraTrailIsOn)
        {
            tijeraTrail.gameObject.SetActive(true);
        }

        if (tijeraMejoradaTrailIsOn)
        {
            tijeraMejoradaTrail.gameObject.SetActive(true);
        }
    }
}
