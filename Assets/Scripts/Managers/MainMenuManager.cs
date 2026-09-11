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

    //el boton de nuevo juego es una instancia de Assets/Prefabs/UI/Button.prefab renombrada en la escena
    const string NOMBRE_BOTON_NUEVO_JUEGO = "NewGameButton";

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
        EventManager.Subscribe(Evento.OnDialogueEnd, ChangeScene);
        AudioManager.instance.StopAll();
        AudioManager.instance.PlayByName("4S_IntroBigChords");
        AudioManager.instance.PlayByName("ForestAtNight");

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
        if (_dialogueStarted)
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

        SeleccionarBotonNuevoJuego();
    }

    public void OnNewGameButtonDown()
    {
        _isNewGameButtonDown = true;
        if (_isNewGameButtonDown && !_dialogueStarted)
        {
            _autoDialogo.StartDialogue();
            AudioManager.instance.StopByName("4S_IntroBigChords");
            AudioManager.instance.StopByName("ForestAtNight");

            AudioManager.instance.PlayByName("IntroStoryboardLoop");

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
