using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerSolapa : TriggerScript
{
    //esto se lo pones a una solapa
    //cuando la interactuas 

    [SerializeField] Solapa solapaAfectada;
    [SerializeField] GameObject objetoParaMostrar;
    [SerializeField] float tiempoHastaMostrarObjeto;
    bool isShowing;

    public override void Interact(params object[] parameter)
    {
        if (triggerBool)
        {
            LevelManager.Instance.player.PlayPullSolapa();
            solapaAfectada.CambiarEstado();
            StartCoroutine(ToggleObject(objetoParaMostrar));
        }
    }

    public IEnumerator ToggleObject(GameObject objeto)
    {
        yield return new WaitForSeconds(tiempoHastaMostrarObjeto);

        if (objetoParaMostrar != null)
        {
            if (isShowing)
            {
                objeto.SetActive(false);
            }
            else
            {
                objeto.SetActive(true);
            }
            isShowing = !isShowing;
        }
    }
}
