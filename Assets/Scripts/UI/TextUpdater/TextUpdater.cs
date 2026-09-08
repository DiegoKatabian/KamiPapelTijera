using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public abstract class TextUpdater : MonoBehaviour
{
    //los text updaters de cosas de la ui agarran la info usando eventos


    protected TextMeshProUGUI myText;

    [SerializeField] protected Evento eventoQueMeInteresa;
    [SerializeField] protected string textoInicial;

    protected virtual void Awake()
    {
        myText = GetComponent<TextMeshProUGUI>();
        EventManager.Subscribe(eventoQueMeInteresa, UpdateText);
    }

    protected virtual void UpdateText(params object[] parameter)
    {
        //print("updateo el text");
    }

    //la corrutina se fue a LocalizedText (era una de siete copias identicas). Aca el fallback
    //SIEMPRE escribe, y arrastra el secondPart: la UI de pagina/pliegue tiene que mostrar el
    //numero aunque la clave del prefijo no exista.
    protected IEnumerator SetLocalizedText(string key, string secondPart = "")
    {
        return LocalizedText.Escribir(myText, key, "UITexts", new LocalizedText.Opciones
        {
            sufijo = secondPart,
            sufijoEnFallback = true,
            escribirSinTabla = true,
            origen = "TextUpdater"
        });
    }

    protected virtual void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(eventoQueMeInteresa, UpdateText);
        }
    }
}
