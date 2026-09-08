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
    public bool lockedByAnimation = false;


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
