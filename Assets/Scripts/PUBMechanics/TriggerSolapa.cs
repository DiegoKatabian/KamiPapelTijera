using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerSolapa : TriggerScript
{
    //Interactuable: aca el boton de accion sirve para abrir o cerrar la solapa del pop-up.
    public override bool EsInteractuable => true;

    //esto se lo pones a una solapa
    //cuando la interactuas 

    [SerializeField] Solapa solapaAfectada;
    [SerializeField] GameObject objetoParaMostrar;
    [SerializeField] float tiempoHastaMostrarObjeto;
    bool isShowing;
    Coroutine _toggleRoutine;

    public override void Interact(params object[] parameter)
    {
        if (triggerBool)
        {
            //the same press that advances a dialogue line also reaches here: without this, closing
            //Natalia's comment about the glove also slammed the trash can shut
            if (LevelManager.Instance != null && LevelManager.Instance.inDialogue)
            {
                return;
            }

            if (solapaAfectada == null)
            {
                Debug.LogWarning("[TriggerSolapa] No hay solapa afectada asignada, se ignora la interaccion");
                return;
            }

            // Si estaba abierta, kami la va a cerrar y la animacion de tironear va en reversa
            bool estabaAbierta = solapaAfectada.IsOpen;
            LevelManager.Instance.player.PlayPullSolapa(estabaAbierta);
            solapaAfectada.CambiarEstado();
            _toggleRoutine = StartCoroutine(ToggleObject(objetoParaMostrar));
        }
    }

    /// <summary>
    /// Closed and full again (page 4's trash cans after a capture, #219): the flap shuts silently, a
    /// pending reveal is cancelled and the contents are refilled and hidden.
    /// </summary>
    public void ResetToFresh()
    {
        if (_toggleRoutine != null)
        {
            StopCoroutine(_toggleRoutine);
            _toggleRoutine = null;
        }
        isShowing = false;

        if (solapaAfectada != null)
        {
            solapaAfectada.ResetClosed();
        }
        else
        {
            Debug.LogWarning($"[TriggerSolapa] {gameObject.name}: no solapaAfectada to close on reset");
        }

        if (objetoParaMostrar == null)
        {
            return;
        }

        GrantResourcePickup pickup = objetoParaMostrar.GetComponentInChildren<GrantResourcePickup>(true);
        if (pickup != null)
        {
            pickup.ResetPickup();
        }

        objetoParaMostrar.SetActive(false);
        Debug.Log($"[TriggerSolapa] {gameObject.name}: closed and refilled");
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
