using System.Collections;
using System.Collections.Generic;
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

    [SerializeField, Tooltip("Velocidad del efecto maquina de escribir del dialogo, en caracteres por segundo")]
    float _charsPerSecond = 25f;

    //true mientras el typewriter esta revelando la linea actual. Mientras dura, el boton de
    //avance no pasa de linea: solo completa el texto (convencion estandar en juegos con
    //typewriter, evita que el primer apriete "se pierda" un texto entero)
    bool _isTyping = false;
    TMPro.TextMeshProUGUI _typewriterTarget;
    int _typewriterTotalCaracteres;

    [SerializeField, Tooltip("Pencil sound while a line is written: clips of this AudioBank row chained one after another, picked so the stream lasts about as long as the typewriter takes to write the line. Empty = silent.")]
    string _pencilSound = AudioId.Dialogue_PencilWrite;

    Coroutine _pencilRoutine;
    SoundHandle _pencilClip;

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
        if (!waitingForInput || lockedByAnimation)
        {
            return;
        }

        if (_isTyping)
        {
            //primer apriete mientras el typewriter esta corriendo: solo destapa el texto ya
            //escrito, el avance de linea real queda para el proximo apriete
            CompletarTypewriter();
            PlayEToInteractSound();
            return;
        }

        input = true;
        PlayEToInteractSound();
    }

    public void BUTTON_NextText()
    {
        CheckPlayerInput();
    }

    /// <summary>
    /// True when ShowDialogue would really open a dialogue right now. ShowDialogue silently drops a
    /// request while anything else holds the screen, so code that must be heard waits on this.
    /// </summary>
    public bool CanShowDialogueNow =>
        !isShowing
        && LevelManager.Instance != null && !LevelManager.Instance.inDialogue
        && (OverlayManager.Instance == null || !OverlayManager.Instance.isLocked);

    public void ShowDialogue(DialogueSO dialogue)
    {
        if (OverlayManager.Instance != null && OverlayManager.Instance.isLocked)
        {
            return;
        }

        if (!isShowing && !LevelManager.Instance.inDialogue)
        {
            dialogue.currentText = 0;
            //whatever post-it is up belongs to the trigger Kami stands in (the trash can's "look inside" one stayed
            //over Natalia's comment about the glove), and nothing else can move it while she is frozen
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }
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
        StopPencil();
        dialogueGlobeText.text = "";
        dialogueGlobeSpeaker.text = "";
        LevelManager.Instance.inDialogue = false;
        dialogueGlobe.SetActive(false);
        isShowing = false;
        AnimationCue.RunAll(dialogue.cuesOnEnd, $"{dialogue.name} end");
        EventManager.Trigger(Evento.OnDialogueEnd, CameraMode.Normal, dialogue);
    }

    public IEnumerator WriteText(DialogueSO dialogue)
    {
        string previousSpeakerKey = null;
        for (int i = 0; i < dialogue.events.Length; i++)
        {
            dialogue.currentText++;

            //#245: the two localization lookups below take a frame or more each, and until they finish the globe kept
            //the previous line's portrait, name and text (the opening line showed the LAST dialogue's portrait). So the
            //portrait is set before any wait, the old text is hidden, and a name that changes is blanked until the new
            //one arrives (a speaker who keeps talking keeps his name, so it never blinks)
            npcQueTeHablaImage.sprite = dialogue.events[i].sprite;
            SetNativeSize(npcQueTeHablaImage.sprite);
            dialogueGlobeText.maxVisibleCharacters = 0;
            if (dialogue.events[i].speakerName != previousSpeakerKey)
            {
                LocalizedText.Limpiar(dialogueGlobeSpeaker);
                dialogueGlobeSpeaker.text = "";
            }
            previousSpeakerKey = dialogue.events[i].speakerName;

            yield return StartCoroutine(SetLocalizedText(dialogue.events[i].text, dialogueGlobeText));

            //arranca tapado en 0 ANTES del WaitForEndOfFrame: si no, el texto completo queda
            //con el maxVisibleCharacters de la linea anterior (que ya llego a full) y se
            //alcanza a renderizar un frame entero antes de que el typewriter lo vuelva a tapar
            dialogueGlobeText.maxVisibleCharacters = 0;

            yield return StartCoroutine(SetLocalizedText(dialogue.events[i].speakerName, dialogueGlobeSpeaker));
            EventManager.Trigger(Evento.OnDialogueWriteText, dialogue);

            //per-line sound (Level 2 SFX pass): plays as the line starts being written, so a
            //cutscene beat like the handcuffs or the radio lands on the right sentence
            PlayLineSound(dialogue.events[i].soundOnLine);

            //per-line animation beats (spec 014): same moment as the sound, so "Ariel points" lands
            //on "Officers! There they are..."
            AnimationCue.RunAll(dialogue.events[i].animationCues, $"{dialogue.name} line {i}");

            yield return new WaitForEndOfFrame();
            waitingForInput = true;

            yield return StartCoroutine(EjecutarTypewriter(dialogueGlobeText));

            while (!input)
                yield return null;

            input = false;
            waitingForInput = false;
        }
        HideDialogue(dialogue);
    }

    void PlayLineSound(string soundId)
    {
        if (string.IsNullOrEmpty(soundId))
        {
            return;
        }
        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[DialogueManager] line sound '{soundId}' skipped: no AudioManager in the scene.");
            return;
        }
        Debug.Log($"[DialogueManager] line sound '{soundId}'");
        AudioManager.instance.Play(soundId);
    }

    //la corrutina se fue a LocalizedText (era una de siete copias). El dialogo se resuelve
    //COMPLETO de una sola vez antes de escribirlo al TMP: la maquina de escribir (issue #41.9,
    //ver EjecutarTypewriter) corre sobre ESE resultado via maxVisibleCharacters, nunca
    //reconstruyendo el string letra por letra (eso romperia los tags de TMP y dejaria
    //prompts de InputPromptSystem a medio escribir).
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

    /// <summary>
    /// Efecto maquina de escribir (issue #41.9): el texto YA esta completo y localizado en el
    /// TMP (ver comentario en SetLocalizedText), asi que solo vamos revelando
    /// maxVisibleCharacters de a poco -- mas barato que reconstruir el string y TMP ya ignora
    /// los tags de rich text al contar caracteres visibles, asi que no hace falta parsearlos
    /// a mano.
    /// </summary>
    IEnumerator EjecutarTypewriter(TMPro.TextMeshProUGUI textElement)
    {
        //spec 024 2.A: the globe text is vertically centred, and by default TMP centres it on the lines
        //revealed so far, so every new line pushed the whole block up. Centre on the full line instead.
        textElement.useMaxVisibleDescender = false;

        //ForceMeshUpdate para poder leer characterCount YA: sin esto TMP recalcula recien en
        //su propio LateUpdate y characterCount todavia tendria el valor de la linea anterior
        textElement.ForceMeshUpdate();
        int totalCaracteres = textElement.textInfo.characterCount;

        if (totalCaracteres <= 0 || _charsPerSecond <= 0f)
        {
            textElement.maxVisibleCharacters = totalCaracteres;
            yield break;
        }

        _isTyping = true;
        _typewriterTarget = textElement;
        _typewriterTotalCaracteres = totalCaracteres;

        float segundosPorCaracter = 1f / _charsPerSecond;
        StartPencil(totalCaracteres * segundosPorCaracter);
        float acumulado = 0f;
        int visibles = 0;

        //el while interno permite "ponerse al dia" si un frame tardo mas de lo normal
        //(hitch) en vez de mostrar un caracter por frame nada mas
        while (visibles < totalCaracteres && _isTyping)
        {
            acumulado += Time.deltaTime;
            while (acumulado >= segundosPorCaracter && visibles < totalCaracteres)
            {
                visibles++;
                acumulado -= segundosPorCaracter;
            }
            textElement.maxVisibleCharacters = visibles;
            yield return null;
        }

        //skipped: the text is all there now, so the pencil stops with it. Written to the end, the
        //clip that is playing was picked to end about now and is left to finish
        if (visibles < totalCaracteres)
        {
            StopPencil();
        }

        textElement.maxVisibleCharacters = totalCaracteres;
        _isTyping = false;
    }

    void StartPencil(float seconds)
    {
        StopPencil();
        if (string.IsNullOrEmpty(_pencilSound) || AudioManager.instance == null)
        {
            return;
        }
        _pencilRoutine = StartCoroutine(PencilStream(seconds));
    }

    void StopPencil()
    {
        if (_pencilRoutine != null)
        {
            StopCoroutine(_pencilRoutine);
            _pencilRoutine = null;
        }
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Stop(_pencilClip); //a stale handle stops nothing, by design
        }
        _pencilClip = SoundHandle.None;
    }

    /// <summary>
    /// The pencil writing a line: Diego's clips (exported to chain in any order) played back to back
    /// with a little pitch spread, until about <paramref name="seconds"/> have been filled. Each clip
    /// is picked among the ones that still fit what is left, so a long line gets long strokes and the
    /// end of any line gets short ones, and the stream stops about when the text does.
    /// </summary>
    IEnumerator PencilStream(float seconds)
    {
        AudioClip[] clips = AudioManager.instance.ClipsOf(_pencilSound);
        float shortest = float.MaxValue;
        foreach (AudioClip clip in clips)
        {
            if (clip != null)
            {
                shortest = Mathf.Min(shortest, clip.length);
            }
        }
        if (shortest == float.MaxValue)
        {
            _pencilRoutine = null;
            yield break;
        }

        float end = Time.unscaledTime + seconds;
        AudioClip last = null;

        //stop once what is left is under half the shortest stroke: closer to the text than overshooting
        while (end - Time.unscaledTime >= shortest * 0.5f)
        {
            AudioClip clip = PickPencilClip(clips, end - Time.unscaledTime, last);
            _pencilClip = AudioManager.instance.PlayClip(_pencilSound, clip, out float pitch);
            if (!_pencilClip.IsValid)
            {
                break;
            }
            last = clip;
            yield return new WaitForSecondsRealtime(clip.length / Mathf.Max(0.01f, pitch));
        }

        _pencilRoutine = null;
    }

    //random among the clips that still fit, never the same one twice in a row; when none fits, the
    //one closest to what is left
    static AudioClip PickPencilClip(AudioClip[] clips, float remaining, AudioClip last)
    {
        List<AudioClip> fitting = new List<AudioClip>();
        AudioClip closest = null;
        float closestGap = float.MaxValue;

        foreach (AudioClip clip in clips)
        {
            if (clip == null || (clip == last && clips.Length > 1))
            {
                continue;
            }
            if (clip.length <= remaining)
            {
                fitting.Add(clip);
            }
            float gap = Mathf.Abs(clip.length - remaining);
            if (gap < closestGap)
            {
                closestGap = gap;
                closest = clip;
            }
        }

        return fitting.Count > 0 ? fitting[Random.Range(0, fitting.Count)] : closest;
    }

    /// <summary>
    /// Skip del typewriter: lo llama CheckPlayerInput cuando el jugador aprieta el boton de
    /// avance MIENTRAS se esta escribiendo. Corta el while de EjecutarTypewriter (via
    /// _isTyping) y completa el texto ya, sin esperar el frame que tardaria la corrutina en
    /// despertarse sola.
    /// </summary>
    void CompletarTypewriter()
    {
        _isTyping = false;
        if (_typewriterTarget != null)
        {
            _typewriterTarget.maxVisibleCharacters = _typewriterTotalCaracteres;
        }
    }

    public void PlayEToInteractSound()
    {
        AudioManager.instance.Play(AudioId.PickupSFX, 0.5f);
    }

    private void OnDisable()
    {
        StopPencil();
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, CheckPlayerInput);
        }
    }
}
