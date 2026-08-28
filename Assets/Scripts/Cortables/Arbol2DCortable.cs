using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arbol2DCortable : ObjetoCortable
{
    //cuando cortas a este arbol, dispara su animacion
    //y un cambio de camara. seguro tambien sonidos y particulas

    [SerializeField] Animator _anim;
    [SerializeField] GameObject _hitbox;

    [SerializeField] GameObject _particulasHojas;
    [SerializeField] GameObject _particulasPolvo;

    bool _falldownEnded;

    protected override void ApplyCut()
    {
        base.ApplyCut();
        CameraManager.Instance.SetCamera(CameraMode.General);
        _anim.SetTrigger("FallDown");
        StartCoroutine(AplastadorHitboxCoroutine());

        AudioManager.instance.PlayByName("AxeHit", 1f, 0.05f);
        AudioManager.instance.PlayByName("TreeFall");
        _particulasHojas.SetActive(true);
    }

    public void FinalizarAplastadorHitbox()
    {
        //Debug.Log("finalizar aplastador hitbox");
        _falldownEnded = true;
        _particulasPolvo.SetActive(true);
    }

    IEnumerator AplastadorHitboxCoroutine()
    {
        //la idea es que la hitbox solo este prendida mientras cae
        //la animacion de caida me va a avisar cuando termine

        while (!_falldownEnded)
        {
            yield return null;
        }

        //Debug.Log("apago la hitbox");
        _hitbox.SetActive(false);

        //disparar evento para quest tiburcio (gallinas caminan a rancho + cambian anim)
        AudioManager.instance.PlayByName("QuestCompleted02");
        EventManager.Trigger(Evento.OnTreeCutForChickens);
    }

}
