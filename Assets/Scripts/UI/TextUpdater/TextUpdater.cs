using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

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

    protected IEnumerator SetLocalizedText(string key, string secondPart = "")
    {
        if (!string.IsNullOrEmpty(key))
        {
            var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync("UITexts");
            yield return tableOperation;

            StringTable stringTable = tableOperation.Result;
            if (stringTable != null)
            {
                var entry = stringTable.GetEntry(key);
                if (entry != null && !string.IsNullOrEmpty(entry.GetLocalizedString()))
                {
                    myText.text = entry.GetLocalizedString() + secondPart;
                    yield break;
                }
            }
        }
        myText.text = key + secondPart;
    }

    protected virtual void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(eventoQueMeInteresa, UpdateText);
        }
    }
}
