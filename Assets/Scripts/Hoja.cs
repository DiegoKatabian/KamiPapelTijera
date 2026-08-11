using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hoja : MonoBehaviour
{
    //hueso de la punta de la cadena de curl (rig Mecanim, no Spine: PN000pageJoint00..30 son Transforms normales).
    //kami se engancha aca durante el giro (ver Player.StartRidingPage / PageScrollerManager.CerrarPaginaCoroutine).
    const string EDGE_BONE_NAME = "PN000pageJoint30";
    [SerializeField] Transform _edgeBone;
    public Transform EdgeBone => _edgeBone;

    void Awake()
    {
        bool wasPreassigned = _edgeBone != null;
        if (_edgeBone == null)
        {
            _edgeBone = FindDeepChild(transform, EDGE_BONE_NAME);
            if (_edgeBone == null)
            {
                Debug.LogWarning($"[Hoja] no encontre el hueso '{EDGE_BONE_NAME}' en la jerarquia: kami no podra agarrarse del borde");
            }
        }

        Debug.Log(_edgeBone != null
            ? $"[Hoja] Awake en '{gameObject.name}': edgeBone = '{_edgeBone.name}' @ {_edgeBone.position} ({(wasPreassigned ? "wireado en el inspector" : "encontrado por busqueda")})"
            : $"[Hoja] Awake en '{gameObject.name}': SIN edgeBone");
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
            Transform found = FindDeepChild(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    public void HojaIdleStart()
    {
        //print("esta hoja termino de doblarse y arranco su animacion de idle");
        EventManager.Trigger(Evento.OnPageFinishTurning);
        Destroy(gameObject);
    }
}
