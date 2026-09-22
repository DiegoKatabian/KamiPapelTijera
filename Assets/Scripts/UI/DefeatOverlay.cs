using System.Collections;
using TMPro;
using UnityEngine;

public class DefeatOverlay : Overlay
{
    //el defeat overlay sabe mostrar la causa de muerte: recibe el DeathCause y resuelve
    //la key de localizacion (tabla UITexts). el texto real se edita en los localization sheets.

    [Tooltip("el texto donde va la causa de la muerte (ej: 'te ahogaste'). si falta, el overlay funciona igual pero sin causa")]
    [SerializeField] TextMeshProUGUI causeText;

    //keys de la tabla UITexts, una por causa. agregar aca cuando aparezcan nuevas causas
    const string KEY_DEFEAT_DROWNING = "DefeatDrowning";
    const string KEY_DEFEAT_ROCOSO = "DefeatRocoso";
    const string KEY_DEFEAT_GENERIC = "DefeatGeneric";
    const string KEY_DEFEAT_CAUGHT = "DefeatCaught";

    public void ShowCause(DeathCause cause)
    {
        if (causeText == null)
        {
            Debug.LogWarning("[DefeatOverlay] ShowCause: no hay causeText asignado en el inspector, no muestro causa");
            return;
        }

        string key = GetKeyForCause(cause);
        Debug.Log($"[DefeatOverlay] ShowCause: causa {cause} -> key '{key}'");
        StartCoroutine(SetLocalizedText(key)); //el GO ya esta activo: OverlayManager hace SetActive(true) antes de llamar aca
    }

    string GetKeyForCause(DeathCause cause)
    {
        switch (cause)
        {
            case DeathCause.Drowning:
                return KEY_DEFEAT_DROWNING;
            case DeathCause.Rocoso:
                return KEY_DEFEAT_ROCOSO;
            case DeathCause.Caught:
                return KEY_DEFEAT_CAUGHT;
            default:
                return KEY_DEFEAT_GENERIC;
        }
    }

    //la corrutina se fue a LocalizedText (era una de siete copias identicas). Se mantiene el
    //fallback de siempre: si la key no esta en la tabla, se muestra la key cruda + warning.
    IEnumerator SetLocalizedText(string key)
    {
        return LocalizedText.Escribir(causeText, key, "UITexts", new LocalizedText.Opciones
        {
            escribirSinTabla = true,
            avisarSinTabla = true,
            avisarSinClave = true,
            origen = "DefeatOverlay"
        });
    }
}
