using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverlayManager : Singleton<OverlayManager>
{
    [SerializeField] Overlay _defeatOverlay, _victoryOverlay, _mainQuestOverlay;

    public bool isLocked;
    Vector3? _pendingRespawnOverride; //posicion que el player resolvio al morir (null = respawn comun en la entrada de la pagina)

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
            Lock();
        }

        if ((DialogueSO)parameter[1] == mainQuestTriggeringDialogue)
        {
            _mainQuestOverlay.gameObject.SetActive(true);
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
        LevelManager.Instance.inDialogue = false;
        isLocked = false;
        AudioManager.instance.PlayByName("PickupSFX", 0.66f);

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
        LevelManager.Instance.GoToScene("Nivel1_EndCutscene");
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
