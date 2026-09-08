using TMPro;
using UnityEngine;

/// <summary>
/// Para los TMP que tienen el texto ESCRITO A MANO en el prefab y nunca pasan por codigo.
/// El caso que lo motivo: el "E para avanzar" del globo de dialogo, que estaba hardcodeado
/// en <c>DialogueGlobe.prefab</c>, asi que no se traducia ni al ingles ni al portugues, y
/// mucho menos al joystick.
///
/// Se le pone la clave y la tabla en el inspector (o por cirugia de prefab) y el resto lo
/// hace <see cref="LocalizedText"/>: localiza, resuelve los prompts del device y deja el
/// texto registrado para que se reescriba solo si el jugador cambia de teclado a joystick.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextoLocalizadoFijo : MonoBehaviour
{
    [SerializeField, Tooltip("Nombre de la tabla de localizacion (DialogueTable, UITexts, TooltipTable...)")]
    string tabla = "UITexts";

    [SerializeField, Tooltip("Clave dentro de esa tabla. Si no existe se muestra la clave cruda y se loguea un warning")]
    string clave = "";

    TMP_Text _texto;

    void Awake()
    {
        _texto = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (string.IsNullOrEmpty(clave))
        {
            Debug.LogWarning($"[TextoLocalizadoFijo] '{name}' no tiene clave asignada: dejo el texto del prefab como esta");
            return;
        }

        //en OnEnable y no en Start: el globo de dialogo se prende y apaga todo el tiempo, y
        //asi el texto se reescribe cada vez que reaparece (idioma o device pudieron cambiar)
        StartCoroutine(LocalizedText.Escribir(_texto, clave, tabla, new LocalizedText.Opciones
        {
            //si la clave falta queremos VERLA en pantalla y en consola: un globo que dice
            //"e_para_avanzar" se arregla en un minuto, uno que dice el texto viejo no se nota
            escribirSinTabla = true,
            avisarSinTabla = true,
            avisarSinClave = true,
            origen = "TextoLocalizadoFijo"
        }));
    }
}
