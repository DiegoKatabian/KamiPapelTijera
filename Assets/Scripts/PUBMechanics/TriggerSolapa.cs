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
            if (solapaAfectada == null)
            {
                Debug.LogWarning("[TriggerSolapa] No hay solapa afectada asignada, se ignora la interaccion");
                return;
            }

            // Si estaba abierta, kami la va a cerrar y la animacion de tironear va en reversa
            bool estabaAbierta = solapaAfectada.IsOpen;
            LevelManager.Instance.player.PlayPullSolapa(estabaAbierta);
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
