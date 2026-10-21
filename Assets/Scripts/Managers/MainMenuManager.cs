using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] AutoDialogue _autoDialogo;

    bool _isNewGameButtonDown = false;
    bool _dialogueStarted = false;

    [SerializeField, Tooltip("Scene to load when the intro dialogue ends.")]
    GameScene sceneToLoadOnDialogueEnd = GameScene.Level1;

    public GameObject LoadingAnimationCanvas;

    [SerializeField, Tooltip("The black plate that fades the menu out when a level is picked (it used to be called by " +
                             "the New Game button's own OnClick, before the level select).")]
    ImageFadeIn _placaNegra;

    [SerializeField, Tooltip("The Level 1 / Level 2 cards the New Game button opens. Empty, or a build without " +
                             "Level 2 in it (the Steam demo), = New Game starts Level 1 directly, as before.")]
    LevelSelectMenu _levelSelect;

    [SerializeField, Tooltip("Seconds of fade to black after picking Level 2, before Level 1's end cutscene loads " +
                             "(the loading animation covers the rest of the fade).")]
    float _secondsBeforeLoadingLevel2 = 1.5f;

    bool _levelChosen; //a level was picked: no second pick, no reselecting menu buttons

    //el boton de nuevo juego es una instancia de Assets/Prefabs/UI/Button.prefab renombrada en la escena
    const string NOMBRE_BOTON_NUEVO_JUEGO = "NewGameButton";

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
        EventManager.Subscribe(Evento.OnDialogueEnd, ChangeScene);
        AudioManager.instance.StopAll();
        AudioManager.instance.Play(AudioId._4S_IntroBigChords);
        AudioManager.instance.Play(AudioId.ForestAtNight);

        Debug.Log($"[MainMenuManager] Start() en el frame {Time.frameCount} " +
                  $"(HayJoystickConectado={InputHub.HayJoystickConectado})");
        SeleccionarBotonNuevoJuego();
    }

    /// <summary>
    /// Deja seleccionado el boton de nuevo juego para que se pueda arrancar con el joystick
    /// (sin seleccion, el stick no navega y el boton B no le pega a nada).
    ///
    /// Lo busca POR NOMBRE porque este manager no tiene ninguna referencia al boton en el
    /// inspector y no queremos que Diego tenga que cablear nada: si tomaramos "el primer
    /// Selectable del canvas" podriamos agarrar uno de los botones de idioma, que son hermanos
    /// suyos. Si el boton se renombra, solo se pierde la seleccion inicial (avisa por consola).
    /// </summary>
    void SeleccionarBotonNuevoJuego()
    {
        GameObject botonNuevoJuego = GameObject.Find(NOMBRE_BOTON_NUEVO_JUEGO);
        if (botonNuevoJuego == null)
        {
            Debug.LogWarning($"[MainMenuManager] no encontre el boton '{NOMBRE_BOTON_NUEVO_JUEGO}' en la escena: " +
                             "el menu no va a quedar seleccionado para el joystick");
            return;
        }

        if (UISelector.SeleccionarPrimeroSiJoystick(botonNuevoJuego))
        {
            Debug.Log($"[MainMenuManager] '{NOMBRE_BOTON_NUEVO_JUEGO}' seleccionado en el frame {Time.frameCount}");
        }
    }

    /// <summary>
    /// Reintento por frame de la seleccion inicial (issue #41.4, causa raiz real).
    ///
    /// El unico intento en Start() no alcanza: Input.GetJoystickNames() (lo que consulta
    /// InputHub.HayJoystickConectado) puede devolver vacio en los primerisimos frames aunque
    /// el joystick ya este fisicamente enchufado -- en Windows, sobre todo con XInput, el SO
    /// tarda unos frames en terminar de enumerar el dispositivo. El menu principal es la
    /// PRIMERA escena que carga el juego, asi que es justo donde mas chances hay de pisar esa
    /// ventana. A diferencia del Flap (que reintenta la seleccion cada vez que se abre), nadie
    /// mas volvia a llamar SeleccionarBotonNuevoJuego() despues de un Start() fallido: la
    /// seleccion se perdia para siempre en esa sesion. Mismo patron que la fix de
    /// CursorManager para el issue #41.7: hace falta algo que PREGUNTE cada frame (un Update),
    /// no alcanza con suscribirse a InputHub.OnDeviceCambio (ese evento solo salta cuando algo
    /// LEE UltimoDeviceFueJoystick, y aca nada lo estaba leyendo despues del Start).
    ///
    /// Para de pollear apenas hay algo seleccionado (ya cumplio su proposito) o apenas arranca
    /// el dialogo automatico (`_dialogueStarted`), que a proposito limpia la seleccion con
    /// UISelector.Limpiar() -- reseleccionar despues de eso resucitaria un boton fantasma que
    /// el B del joystick seguiria "apretando" mientras corre el dialogo.
    /// </summary>
    void ReintentarSeleccionSiHaceFalta()
    {
        if (_dialogueStarted || _levelChosen)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            return; //ya hay seleccion (la de Start() sirvio, o esta reintento ya la logro antes)
        }

        //si nunca hubo joystick a la vista, no hay nada que reintentar: evita hacer
        //GameObject.Find() todos los frames durante una sesion pura de teclado/mouse
        if (!InputHub.HayJoystickConectado && !InputHub.UltimoDeviceFueJoystick)
        {
            return;
        }

        //while the level select is up, its cards are the menu: the hidden New Game button must not get the focus back
        if (_levelSelect != null && _levelSelect.IsOpen)
        {
            _levelSelect.SelectFirstCard();
            return;
        }

        SeleccionarBotonNuevoJuego();
    }

    /// <summary>The level select closed: the joystick goes back to New Game.</summary>
    public void SelectNewGameButtonAgain()
    {
        SeleccionarBotonNuevoJuego();
    }

    /// <summary>New Game: opens the level select, or starts Level 1 directly when there is nothing to choose.</summary>
    public void OnNewGameButtonDown()
    {
        if (_dialogueStarted || _levelChosen)
        {
            return;
        }

        if (_levelSelect == null)
        {
            Debug.LogWarning("[MainMenuManager] no _levelSelect wired: New Game starts Level 1 directly");
            BTN_StartLevel1();
            return;
        }

        if (!_levelSelect.IsAvailable)
        {
            Debug.Log("[MainMenuManager] Level 2 is not in this build (demo): New Game starts Level 1 directly");
            BTN_StartLevel1();
            return;
        }

        _levelSelect.Open();
    }

    /// <summary>Level 1 card: the original New Game flow (the intro storyboard dialogue, then Level 1).</summary>
    public void BTN_StartLevel1()
    {
        if (_levelChosen)
        {
            return;
        }
        _levelChosen = true;
        Debug.Log("[MainMenuManager] Level 1 picked: intro dialogue, then Level 1");

        if (_levelSelect != null)
        {
            _levelSelect.LockChoice();
        }
        FadeToBlack();
        StartIntroDialogue();
    }

    /// <summary>
    /// Level 2 card: no saves, so Level 2 is reached the way the story reaches it, through Level 1's end
    /// cutscene (the Narrator sends Kami into "Kami Tinta y Condena"), which then loads Level 2 (SceneFlow).
    /// </summary>
    public void BTN_StartLevel2()
    {
        if (_levelChosen)
        {
            return;
        }
        _levelChosen = true;
        Debug.Log("[MainMenuManager] Level 2 picked: Level 1's end cutscene, then Level 2");

        if (_levelSelect != null)
        {
            _levelSelect.LockChoice();
        }
        UISelector.Limpiar();
        AudioManager.instance.StopById(AudioId._4S_IntroBigChords);
        AudioManager.instance.StopById(AudioId.ForestAtNight);
        FadeToBlack();
        SceneFlow.RequestSceneAfterCutscene(GameScene.Level2);
        StartCoroutine(LoadLevel2CutsceneAfterFade());
    }

    IEnumerator LoadLevel2CutsceneAfterFade()
    {
        yield return new WaitForSeconds(_secondsBeforeLoadingLevel2);
        if (LoadingAnimationCanvas != null)
        {
            LoadingAnimationCanvas.SetActive(true);
            yield return null; //let the loading animation draw once before the synchronous load freezes the frame
        }
        LevelManager.Instance.GoToScene(GameScene.Level1EndCutscene);
    }

    void FadeToBlack()
    {
        if (_placaNegra == null)
        {
            Debug.LogWarning("[MainMenuManager] no _placaNegra wired: the menu will not fade to black");
            return;
        }
        _placaNegra.PlacaNegraOn();
    }

    void StartIntroDialogue()
    {
        _isNewGameButtonDown = true;
        if (_isNewGameButtonDown && !_dialogueStarted)
        {
            _autoDialogo.StartDialogue();
            AudioManager.instance.StopById(AudioId._4S_IntroBigChords);
            AudioManager.instance.StopById(AudioId.ForestAtNight);

            AudioManager.instance.Play(AudioId.IntroStoryboardLoop);

            //arranco el dialogo: ya no hay menu que navegar y el dialogo avanza con E / boton B.
            //sin esto el boton quedaria seleccionado y cada B tambien seguiria "apretandolo".
            UISelector.Limpiar();

            _dialogueStarted = true;
        }
    }

    void Update()
    {
        if (InputHub.InteractDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedE); //como no tengo PlayerController, lo hago aca.
        }

        ReintentarSeleccionSiHaceFalta();
    }

    public void ChangeScene(params object[] parameter)
    {
        //print("change scene");

        LoadingAnimationCanvas.SetActive(true);
        LevelManager.Instance.GoToScene(sceneToLoadOnDialogueEnd);
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, ChangeScene);
            //AudioManager.instance.StopByName("IntroStoryboardLoop");
        }
    }

}
