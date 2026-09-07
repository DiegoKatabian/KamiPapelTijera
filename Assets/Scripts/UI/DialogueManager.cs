using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Settings;

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

    private IEnumerator SetLocalizedText(string fallbackText, TMPro.TextMeshProUGUI textElement)
    {
        //textElement.text = fallbackText;

        if (!string.IsNullOrEmpty(fallbackText))
        {
            var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync("DialogueTable");
            yield return tableOperation;

            StringTable stringTable = tableOperation.Result;
            if (stringTable != null)
            {
                var entry = stringTable.GetEntry(fallbackText);
                if (entry != null && !string.IsNullOrEmpty(entry.GetLocalizedString()))
                {
                    //InputPromptSystem: unico punto donde se resuelve el string COMPLETO del
                    //dialogo antes de escribirlo al TMP. Se procesa aca (una sola vez) y no
                    //letra por letra: si algun dia se agrega maquina de escribir, tiene que
                    //correr sobre ESTE resultado, nunca sobre el texto crudo (romperia los
                    //tags de TMP y los prompts a medio escribir).
                    textElement.text = InputPromptSystem.Procesar(entry.GetLocalizedString());
                }
                else
                {
                    textElement.text = InputPromptSystem.Procesar(fallbackText);
                }
            }
        }
        else
        {
            textElement.text = fallbackText;
        }
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
