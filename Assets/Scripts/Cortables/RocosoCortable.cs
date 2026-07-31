using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocosoCortable : MonoBehaviour, ICortable
{
    //el rocoso solo puede ser cortado por la tijera mejorada (daño alto).
    //la tijera comun (daño bajo) le rebota; el umbral de abajo es lo que separa una de otra.

    [SerializeField] Rocoso _thisRocoso;

    [Tooltip("Daño minimo para que el corte lo lastime. La tijera comun pega menos que esto; la mejorada pega esto o mas")]
    [SerializeField] float _minDamageParaCortarlo = 100f;

    [Tooltip("Si esta prendido, este rocoso NO muere ni con la tijera mejorada (para rocosos de puzzle que se matan de otra forma)")]
    [SerializeField] bool _isImmuneToTijeraMejorada = false;

    public virtual void GetCut(float receivedDamage)
    {
        if (_thisRocoso == null)
        {
            Debug.LogWarning($"[RocosoCortable] {gameObject.name}: falta la referencia a _thisRocoso, el corte no hace nada");
            return;
        }

        if (_isImmuneToTijeraMejorada)
        {
            Debug.Log($"[RocosoCortable] {gameObject.name}: corte ignorado, este rocoso es inmune a la tijera mejorada");
            return;
        }

        if (receivedDamage < _minDamageParaCortarlo)
        {
            //tijera comun: no le hace nada (el umbral distingue comun de mejorada)
            Debug.Log($"[RocosoCortable] {gameObject.name}: corte de {receivedDamage} no alcanza el minimo ({_minDamageParaCortarlo}), rebota");
            return;
        }

        Debug.Log($"[RocosoCortable] {gameObject.name}: corte de {receivedDamage} aceptado, recibe daño");
        AudioManager.instance.PlayRandom("TijeraHit01", "TijeraHit02");
        _thisRocoso.TakeDamage(receivedDamage);
    }
}
