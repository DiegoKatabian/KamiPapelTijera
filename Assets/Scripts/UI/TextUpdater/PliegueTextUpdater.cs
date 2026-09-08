using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PliegueTextUpdater : TextUpdater
{
    protected override void UpdateText(params object[] parameter)
    {
        string secondPart = "";

        if (parameter[0] is int foldActual && parameter[1] is int foldsTotales)
        {
            secondPart = foldActual.ToString() + "/" + foldsTotales.ToString();
        }

        StartCoroutine(SetLocalizedText(textoInicial, myText, secondPart));
    }

    //la corrutina se fue a LocalizedText (era una de siete copias identicas). Esta variante
    //se diferenciaba de la de TextUpdater en una sola cosa, y se respeta: cuando la clave no
    //existe escribe SOLO el fallback, sin pegarle el "2/5" atras.
    private IEnumerator SetLocalizedText(string fallbackText, TMPro.TextMeshProUGUI textElement, string secondPart)
    {
        return LocalizedText.Escribir(textElement, fallbackText, "UITexts", new LocalizedText.Opciones
        {
            sufijo = secondPart,
            origen = "PliegueTextUpdater"
        });
    }
}
