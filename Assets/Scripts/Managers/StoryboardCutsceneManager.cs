using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoryboardCutsceneManager : MonoBehaviour
{
    //para todas las escenas de storyboard
    //la idea es que haya una dps de cada nivel
    //la del mainmenu no cuenta jeje


    [SerializeField] AutoDialogue _autoDialogo;
    [SerializeField, Tooltip("Scene to load when the dialogue ends.")]
    GameScene _sceneToLoadOnDialogueEnd = GameScene.MainMenu;
    [SerializeField] GameObject _endingSplash; //aparece cuando termina el dialogo

    bool _waitingForInput;
    public bool closeAppOnDialogueEnd = true;

    //Set when the main menu's level select sent the player here on the way into a level (SceneFlow).
    bool _leadsIntoLevel;
    GameScene _requestedScene;

    private void Start()
    {
        Debug.Log("storyboardmanager start");
        Cursor.lockState = CursorLockMode.Confined;
        EventManager.Subscribe(Evento.OnDialogueEnd, ShowEndingSplash);

        if (SceneFlow.TryConsumeSceneAfterCutscene(out _requestedScene))
        {
            //The ending splash is the book's logo + "to be continued": right before the level starts it would
            //read as the end of the game, so on this path there is only black behind the dialogue and no splash.
            _leadsIntoLevel = true;
            if (_endingSplash != null)
            {
                _endingSplash.SetActive(false);
            }
            Debug.Log($"[StoryboardCutsceneManager] reached from the level select: no ending splash, '{_requestedScene}' loads when the dialogue ends");
        }
        AudioManager.instance.StopAll();
        AudioManager.instance.Play(AudioId.IntroStoryboardLoop);

        //arranca el dialogo de una
        _autoDialogo.StartDialogue();
    }

    void Update()
    {
        if (InputHub.InteractDown)
        {
            if (_waitingForInput)
            {
                _waitingForInput = false;
                ChangeScene();
            }

            EventManager.Trigger(Evento.OnPlayerPressedE); //como no tengo PlayerController, lo hago aca.
        }
    }

    public void ShowEndingSplash(params object[] parameter)
    {
        //funciona sin parametros
        //porque solo hay un dialogo posible en este tipo de escena de storyboard

        if (_leadsIntoLevel)
        {
            LevelManager.Instance.GoToScene(_requestedScene);
            return;
        }

        if (_endingSplash == null)
        {
            Debug.LogWarning("[StoryboardCutsceneManager] no _endingSplash wired: going on without it");
            ChangeScene();
            return;
        }

        _endingSplash.SetActive(true);
        _waitingForInput = true; //se queda todo quieto hasta q apretes E de nuevo
        
    }

    public void ChangeScene(params object[] parameter)
    {
        //print("change scene");

        if (closeAppOnDialogueEnd)
        {
            StartCoroutine(CloseApp());
        }
        else
        {
            LevelManager.Instance.GoToScene(_sceneToLoadOnDialogueEnd);
        }

    }

    //a couroutine that waits 1 second and closes the app
    public IEnumerator CloseApp()
    {
        yield return new WaitForSeconds(1);
        Debug.Log("app closed");
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, ShowEndingSplash);
        }
    }

}
