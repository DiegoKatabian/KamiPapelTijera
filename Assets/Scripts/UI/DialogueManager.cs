using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : Singleton<DialogueManager>
{
    [SerializeField] GameObject dialogueGlobe;
    [SerializeField] TMPro.TextMeshProUGUI dialogueGlobeText;
    [SerializeField] TMPro.TextMeshProUGUI dialogueGlobeSpeaker;
    [SerializeField] Image npcQueTeHablaImage;

    bool input = false;
    bool waitingForInput = false;
    [HideInInspector] public bool isShowing = false;
    bool _lockedByAnimation = false;

    [SerializeField, Tooltip("Segundos maximos que el dialogo puede quedar bloqueado por una animacion antes de destrabarse solo")]
    float _maxSegundosBloqueadoPorAnimacion = 8f;

    Coroutine _watchdogBloqueo;

    /// <summary>
    /// Mientras esta en true ningun boton avanza el dialogo: lo prende el flujo de recompensa
    /// de quest y lo apaga el Player cuando termina la animacion.
    ///
    /// Tiene watchdog A PROPOSITO: si por lo que sea el Player nunca lo apaga (murio, cambio de
    /// escena, una excepcion se comio su corrutina), el jugador quedaba con el dialogo abierto y
    /// SIN NINGUN BOTON que respondiera, o sea el juego colgado. Preferimos destrabar con un
    /// warning ruidoso antes que dejarlo trabado.
    /// </summary>
    public bool lockedByAnimation
    {
        get { return _lockedByAnimation; }
        set
        {
            _lockedByAnimation = value;

            if (_watchdogBloqueo != null)
            {
                StopCoroutine(_watchdogBloqueo);
                _watchdogBloqueo = null;
            }

            if (value && isActiveAndEnabled)
            {
                _watchdogBloqueo = StartCoroutine(DestrabarSiNadieDestraba());
            }
        }
    }

    IEnumerator DestrabarSiNadieDestraba()
    {
        yield return new WaitForSeconds(_maxSegundosBloqueadoPorAnimacion);

        if (_lockedByAnimation)
        {
            Debug.LogWarning($"[DialogueManager] el dialogo quedo bloqueado por animacion mas de " +
                             $"{_maxSegundosBloqueadoPorAnimacion}s y nadie lo destrabo: lo destrabo yo para que el " +
                             "jugador no quede colgado. Revisar el flujo de recompensa de la quest que estaba activa.");
            _lockedByAnimation = false;
        }

        _watchdogBloqueo = null;
    }


    void Start()
    {
        EventManager.Subscribe(Evento.OnPlayerPressedE, CheckPlayerInput);
    }

    public void CheckPlayerInput(params object[] parameter)
    {
        if (waitingForInput && !lockedByAnimation)
        {
            input = true;
            PlayEToInteractSound();
        }
    }

    public void BUTTON_NextText()
    {
        CheckPlayerInput();
    }

    public void ShowDialogue(DialogueSO dialogue)
    {
        if (OverlayManager.Instance != null && OverlayManager.Instance.isLocked)
        {
            return;
        }

        if (!isShowing && !LevelManager.Instance.inDialogue)
        {
            dialogue.currentText = 0;
            dialogueGlobe.SetActive(true);
            PlayEToInteractSound();
            LevelManager.Instance.inDialogue = true;
            isShowing = true;
            StartCoroutine(WriteText(dialogue));
        }
    }

    public void HideDialogue(DialogueSO dialogue)
    {
        //los sacamos del registro de LocalizedText antes de vaciarlos: si no, un cambio de
        //device con el globo cerrado les reescribiria la ultima linea del dialogo
        LocalizedText.Limpiar(dialogueGlobeText);
        LocalizedText.Limpiar(dialogueGlobeSpeaker);
        dialogueGlobeText.text = "";
        dialogueGlobeSpeaker.text = "";
        LevelManager.Instance.inDialogue = false;
        dialogueGlobe.SetActive(false);
        isShowing = false;
        EventManager.Trigger(Evento.OnDialogueEnd, CameraMode.Normal, dialogue);
    }

    public IEnumerator WriteText(DialogueSO dialogue)
    {
        for (int i = 0; i < dialogue.events.Length; i++)
        {
            dialogue.currentText++;
            yield return StartCoroutine(SetLocalizedText(dialogue.events[i].text, dialogueGlobeText));
            yield return StartCoroutine(SetLocalizedText(dialogue.events[i].speakerName, dialogueGlobeSpeaker));
            npcQueTeHablaImage.sprite = dialogue.events[i].sprite;
            SetNativeSize(npcQueTeHablaImage.sprite);
            EventManager.Trigger(Evento.OnDialogueWriteText, dialogue);

            yield return new WaitForEndOfFrame();
            waitingForInput = true;

            while (!input)
                yield return null;

            input = false;
            waitingForInput = false;
        }
        HideDialogue(dialogue);
    }

    //la corrutina se fue a LocalizedText (era una de siete copias). El dialogo se resuelve
    //COMPLETO de una sola vez antes de escribirlo al TMP: si algun dia se agrega maquina de
    //escribir tiene que correr sobre ESE resultado, nunca sobre el texto crudo (romperia los
    //tags de TMP y dejaria prompts a medio escribir).
    private IEnumerator SetLocalizedText(string fallbackText, TMPro.TextMeshProUGUI textElement)
    {
        //sin escribirSinTabla: si la tabla no carga, el globo se queda con lo que tenia,
        //que es como venia funcionando
        return LocalizedText.Escribir(textElement, fallbackText, "DialogueTable", new LocalizedText.Opciones
        {
            origen = "DialogueManager"
        });
    }

    public void SetNativeSize(Sprite sprite)
    {
        //un DialogueEvent sin retrato asignado tiraba NullReference aca adentro, y como esto
        //corre dentro de la corrutina WriteText, la corrutina moria y el dialogo quedaba abierto
        //para siempre (inDialogue en true, ningun boton avanza)
        if (sprite == null)
        {
            Debug.LogWarning("[DialogueManager] este texto del dialogo no tiene sprite de retrato asignado, dejo el tamano como estaba");
            return;
        }

        float spriteRatio = sprite.rect.width / sprite.rect.height;
        npcQueTeHablaImage.rectTransform.sizeDelta = new Vector2(npcQueTeHablaImage.rectTransform.sizeDelta.y * spriteRatio, npcQueTeHablaImage.rectTransform.sizeDelta.y);
    }

    public void PlayEToInteractSound()
    {
        AudioManager.instance.PlayByName("PickupSFX", 0.5f);
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, CheckPlayerInput);
        }
    }
}
