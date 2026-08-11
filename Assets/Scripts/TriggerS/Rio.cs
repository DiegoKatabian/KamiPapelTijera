using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rio : TriggerScript
{
    //este no muestra el tooltip, solo moja
    [SerializeField] float wetDamage = 20;

    [Tooltip("Cuánto tiempo tiene que estar algo adentro del agua antes de mojarse; si sale antes, no pasa nada")]
    [SerializeField] float activationDelay = 1f;

    //coroutines de delay pendientes por collider: si el objeto sale antes de que venza
    //el delay, la buscamos acá para cancelarla. si ya no está, es que el delay venció y se mojó.
    readonly Dictionary<Collider, Coroutine> _pendingWetCoroutines = new Dictionary<Collider, Coroutine>();

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        //cualquier IMojable (Kami incluida) arranca su delay de mojado. Rio no conoce a Player:
        //el que decide qué le pasa al mojarse es cada IMojable en su GetWet (SOLID)
        if (other.gameObject.GetComponent<IMojable>() is IMojable mojable)
        {
            Debug.Log($"[Rio] {other.gameObject.name} entro al agua, esperando {activationDelay}s");
            _pendingWetCoroutines[other] = StartCoroutine(WetAfterDelay(other, mojable));
        }
    }

    protected override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);

        if (other.gameObject.GetComponent<IMojable>() is not IMojable mojable)
        {
            return;
        }

        if (_pendingWetCoroutines.TryGetValue(other, out Coroutine pending))
        {
            //entró y salió rápido: el delay no venció, no se moja
            StopCoroutine(pending);
            _pendingWetCoroutines.Remove(other);
            Debug.Log($"[Rio] {other.gameObject.name} salio antes del delay, cancelado");
        }
        else
        {
            //el delay ya había vencido: estaba mojado y ahora sale del agua
            Debug.Log($"[Rio] {other.gameObject.name} salio del agua");
            mojable.StopGettingWet();
        }
    }

    IEnumerator WetAfterDelay(Collider other, IMojable mojable)
    {
        yield return new WaitForSeconds(activationDelay);

        //el delay venció con el objeto todavía adentro: recién ahora se moja.
        //lo saco del diccionario ANTES de mojarlo, para que un exit posterior sepa que ya se mojó
        _pendingWetCoroutines.Remove(other);

        //guard: si el objeto fue destruido adentro del agua (ej: un rocoso cortado), en esta version
        //de Unity (2021.3) NO se dispara OnTriggerExit al destruir, asi que nadie canceló la coroutine.
        //hay que abortar acá para no tocar un objeto muerto (MissingReferenceException)
        if (other == null)
        {
            Debug.Log("[Rio] el objeto fue destruido antes de cumplir el delay, no se moja nadie");
            yield break;
        }

        Debug.Log($"[Rio] delay cumplido: mojando a {other.gameObject.name}");
        mojable.GetWet(wetDamage);
    }
}
