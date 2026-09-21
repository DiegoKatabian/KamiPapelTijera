using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverlayManager : Singleton<OverlayManager>
{
    [SerializeField] Overlay _defeatOverlay, _victoryOverlay, _mainQuestOverlay;

    public bool isLocked;
    Vector3? _pendingRespawnOverride; //posicion que el player resolvio al morir (null = respawn comun en la entrada de la pagina)

    //el mismo apreton de A que cierra el overlay (via su boton, procesado por el EventSystem como
    //Submit) puede ADEMAS llegar a PlayerController.CheckControls() ESTE MISMO frame y disparar
    //OnPlayerPressedE hacia el mundo: si el jugador quedo parado en el trigger de un NPC (ej. la
    //abuela, justo despues del victory overlay del boss fight), le abre un dialogo nuevo de arranque
    //apenas cierra el overlay. Unity no garantiza el orden de Update() entre el EventSystem y
    //PlayerController (no hay ScriptExecutionOrder.asset), asi que a veces Unlock() ya corrio cuando
    //CheckControls() lee ese mismo boton: mismo patron y misma solucion que
    //Player._frameSalidaDeEstadoQueBloqueaAtaque (issue #41.2).
    int _frameDesbloqueado = -1;
    public bool SeDesbloqueoEsteFrame => Time.frameCount == _frameDesbloqueado;

    [SerializeField] DialogueSO victoryTriggeringDialogue, mainQuestTriggeringDialogue;

    protected override void Awake()
    {
        base.Awake();
        //DontDestroyOnLoad(this);
        //ojo: aca NO nos suscribimos a OnPlayerDie. el defeat overlay lo muestra Player.Die() despues de su delay (defeatOverlayDelay)
        EventManager.Subscribe(Evento.OnDialogueEnd, ShowOverlay);
        EventManager.Subscribe(Evento.OnPlayerPressedE, RequestUnlock);
    }

    public void ShowDefeatOverlay(DeathCause cause = DeathCause.Generic, Vector3? respawnOverride = null)
    {
        //el player ya resolvio DONDE respawnear segun su politica; aca solo guardamos y ejecutamos al cerrar
        _pendingRespawnOverride = respawnOverride;
        _defeatOverlay.gameObject.SetActive(true);

        //el texto de la causa lo maneja el propio overlay (necesita el componente DefeatOverlay + causeText asignado)
        if (_defeatOverlay is DefeatOverlay defeatOverlay)
        {
            defeatOverlay.ShowCause(cause); //despues del SetActive: el GO tiene que estar activo para la coroutine de localizacion
        }
        else
        {
            Debug.LogWarning("[OverlayManager] el defeat overlay no tiene el componente DefeatOverlay: no se muestra la causa de muerte");
        }

        //el defeat overlay hoy no tiene botones (se cierra con E / boton B via RequestUnlock), pero si
        //algun dia se le agrega uno, con joystick tiene que quedar seleccionado. avisarSiNoHay=false
        //para no tirar un warning en cada muerte mientras no tenga botones.
        UISelector.SeleccionarPrimeroSiJoystick(_defeatOverlay.gameObject, false);

        Lock();
        Debug.Log($"[OverlayManager] ShowDefeatOverlay: causa {cause}, respawn {(respawnOverride.HasValue ? respawnOverride.Value.ToString() : "entrada de pagina")}");
    }

    public void ShowOverlay(params object[] parameter)
    {
        //por ahora muestra el victory o el mainquest

        if ((DialogueSO)parameter[1] == victoryTriggeringDialogue)
        {
            //Debug.Log("overlay manager: show victory overlay");
            _victoryOverlay.gameObject.SetActive(true);
            _victoryOverlay.isShowing = true;

            //este es el caso critico: RequestUnlock ignora la E cuando el victory esta arriba, asi que
            //la UNICA salida son sus botones. Sin seleccion, el jugador con joystick queda trabado.
            UISelector.SeleccionarPrimeroSiJoystick(_victoryOverlay.gameObject);

            Lock();
        }

        if ((DialogueSO)parameter[1] == mainQuestTriggeringDialogue)
        {
            _mainQuestOverlay.gameObject.SetActive(true);

            //hoy no tiene botones (se cierra con E / boton B), por eso avisarSiNoHay=false
            UISelector.SeleccionarPrimeroSiJoystick(_mainQuestOverlay.gameObject, false);

            Lock();
        }
    }

    public void Lock(params object[] parameter)
    {
        //Debug.Log("overlay manager: lock");
        LevelManager.Instance.inDialogue = true;
        isLocked = true;
    }

    public void RequestUnlock(params object[] parameter)
    {
        if (_victoryOverlay.isShowing) //pues yo quiero q no se pueda desbloquear el victory con E, sino solo con los botones
        {
            return;
        }
        else if (isLocked)
        {
            Unlock();
        }
    }

    public void Unlock()
    {
        //Debug.Log("overlay unlock: set indialogue y islocked false");
        _frameDesbloqueado = Time.frameCount; //ver SeDesbloqueoEsteFrame
        LevelManager.Instance.inDialogue = false;
        isLocked = false;
        AudioManager.instance.Play(AudioId.PickupSFX, 0.66f);

        //se vuelve al juego: no puede quedar un boton seleccionado, o el B del joystick (Submit)
        //lo apretaria mientras el jugador juega
        UISelector.Limpiar();

        bool wasDefeatShowing = _defeatOverlay.gameObject.activeSelf;
        _defeatOverlay.gameObject.SetActive(false);
        _mainQuestOverlay.gameObject.SetActive(false);

        if (wasDefeatShowing)
        {
            //el respawn recien sucede aca: cuando el jugador cierra el overlay de derrota con E
            if (_pendingRespawnOverride.HasValue)
            {
                Debug.Log($"[OverlayManager] Unlock: respawn en lugar seguro {_pendingRespawnOverride.Value}");
                PlayerPageSpawnManager.Instance.PositionPlayerAtPoint(_pendingRespawnOverride.Value);
            }
            else
            {
                Debug.Log("[OverlayManager] Unlock: respawn comun (entrada de la pagina)");
                PlayerPageSpawnManager.Instance.RespawnPlayer();
            }
            _pendingRespawnOverride = null; //consumido: la proxima muerte trae el suyo
        }
    }

    public void BTN_ContinueGame()
    {
        //Debug.Log("continua el juego");
        _victoryOverlay.gameObject.SetActive(false);
        _victoryOverlay.isShowing = false;
        EventManager.Trigger(Evento.OnPlayerChooseContinueGame);
        Unlock();
    }

    public void BTN_GoToCutscene()
    {
        //Debug.Log("go to cutscene");
        _victoryOverlay.gameObject.SetActive(false);
        InitializeCutscene();
    }

    public void InitializeCutscene()
    {
        //Debug.Log("arranca la cutscene");
        LevelManager.Instance.GoToScene(GameScene.Level1EndCutscene);
    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, ShowOverlay);
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, RequestUnlock);
        }
    }
}
