using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TijeraHitbox : MonoBehaviour
{
    //la tijera necesita collider (trigger)
    //cuando toca a algo ICortable, adivina? si, lo corta. 

    public float tijeraDamage;
    bool missed;

    public ParticleSystem HitboxStartParticles;

    private void OnTriggerEnter(Collider other)
    {
        //print("entre a un collider...");
        if (other.GetComponent<ICortable>() != null)
        {
            //print("...cortable");
            ICortable objetoCortable = other.GetComponent<ICortable>();
            objetoCortable.GetCut(tijeraDamage);
            missed = false;
        }
        else
        {
            missed = true;
        }
    }

    private void OnEnable()
    {
        if (HitboxStartParticles != null)
        {
            HitboxStartParticles.Play();
        }
    }

    private void OnDisable()
    {
        if (HitboxStartParticles != null)
        {
            HitboxStartParticles.Stop(); //corta la emision al apagar la hitbox; las particulas vivas terminan su vida sola (sin esto se acumulaban ataque tras ataque)
        }

        if (missed)
        {
            AudioManager.instance.PlayByName("TijeraMiss", 1.1f);
            missed = false;
        }
    }

}
