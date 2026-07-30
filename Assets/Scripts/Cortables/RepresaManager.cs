using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RepresaManager : MonoBehaviour
{
    //este script se entera cuando son cortadas las represas
    //disparo el gameobject activator cuando todas las represas fueron cortadas

    [SerializeField] List<GameObject> troncos, represasCortables;
    [SerializeField] GameObject particulasSplash;
    int represasCortadas = 0;

    [SerializeField] GameObject rioVertical, rioAbajo;
    [SerializeField] float delayTimeEntreRios = 1f;
    [SerializeField] float delayTimeRepresaFall = 1f;
    [SerializeField] float delayTimeHastaCamaraBackToNormal = 1f;

    [SerializeField] List<GameObject> objectsToEliminate = new List<GameObject>();
    [SerializeField] List<GameObject> objectsToActivate = new List<GameObject>();



    void Start()
    {
        EventManager.Subscribe(Evento.OnRepresaWasCut, OnRepresaWasCut);
    }

    void OnRepresaWasCut(params object[] parameter)
    {
        represasCortadas++;
        if (represasCortadas >= represasCortables.Count)
        {
            AudioManager.instance.PlayByName("RepresaFall", 1f);
            AudioManager.instance.PlayByName("MagicSuccess", 0.9f);
            CameraManager.Instance.SetCamera(CameraMode.BookCenter);
            particulasSplash.SetActive(true);
            StartCoroutine(ActivarRiosCoroutine());
        }
    }

    IEnumerator ActivarRiosCoroutine()
    {
        //desaparece la represa y quedan solo los troncos
        yield return new WaitForSeconds(delayTimeRepresaFall);

        LevelManager.Instance.GameObjectActivator(troncos, represasCortables);
        LevelManager.Instance.GameObjectActivator(objectsToActivate, objectsToEliminate);


        //aparecen los rios en orden
        rioVertical.SetActive(true);
        yield return new WaitForSeconds(delayTimeEntreRios);
        rioAbajo.SetActive(true);
        yield return new WaitForSeconds(delayTimeHastaCamaraBackToNormal);
        CameraManager.Instance.SetCamera(CameraMode.Normal);


    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnRepresaWasCut, OnRepresaWasCut);
        }
    }
}
