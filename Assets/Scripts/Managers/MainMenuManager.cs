using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] AutoDialogue _autoDialogo;

    bool _isNewGameButtonDown = false;
    bool _dialogueStarted = false;

    [SerializeField] string sceneToLoadOnDialogueEnd;

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

        UISelector.SeleccionarPrimeroSiJoystick(botonNuevoJuego);
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
