using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Entity
{
    public SpriteRenderer _sr; //y mi sprite renderer. esto y el metodo EnrojecerSprite deberia estar separado, tipo creado como EnemyView o algo asi
    public bool endsEncounter = false; //si este enemigo hace terminar el encuentro o no

    public float Speed
    {
        get { return _maxSpeed; }
        set { _maxSpeed = value; }
    }
    public override void TakeDamage(float dmg)
    {
        base.TakeDamage(dmg);
        StartCoroutine(EnrojecerSprite());
    }

    public virtual IEnumerator EnrojecerSprite()
    {
        //print("enrojeci el sprite");
        _sr.material.color = Color.red;
        yield return new WaitForSeconds(0.25f);
        _sr.material.color = Color.white;
    }

    public override void Die(DeathCause cause = DeathCause.Generic)
    {
        float pitch = 1;
        if (endsEncounter)
        {
            EventManager.Trigger(Evento.OnEncounterEnd, CameraMode.Normal);
            pitch = 0.6f;
        }
        AudioManager.instance.PlayByName("Death", pitch, 0.1f);
        base.Die();
    }
}