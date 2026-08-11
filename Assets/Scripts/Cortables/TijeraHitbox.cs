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
        ICortable objetoCortable = other.GetComponent<ICortable>();
        if (objetoCortable == null)
        {
            return; //tocar algo no-cortable (pared, suelo) no cambia el estado del miss
        }

        //print("...cortable");
        objetoCortable.GetCut(tijeraDamage);
        missed = false; //corto algo: este swing ya no cuenta como miss
    }

    private void OnEnable()
    {
        //semantica del flag: cada activacion arranca asumiendo miss, y solo se
        //desmiente si OnTriggerEnter corta algo ICortable. Si la hitbox no toca
        //ningun collider, OnTriggerEnter nunca corre, asi que el default tiene
        //que ser true para que el swing al aire suene igual.
        missed = true;

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
            //Debug.Log("[TijeraHitbox] Swing sin corte: reproduciendo TijeraMiss");
            AudioManager.instance.PlayByName("TijeraMiss", 1.1f);
            missed = false;
        }
    }

}
