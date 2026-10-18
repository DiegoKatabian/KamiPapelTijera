using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CameraMode
{
    CloseUp,
    OrigamiCasting,
    Normal,
    General,
    BookCenter,
    ReceiveReward
}

public class CameraManager : Singleton<CameraManager>
{
    //este script hace que con mouse3 (centro) cambies de camara, a la proxima en la lista.

    [SerializeField] Cinemachine.CinemachineVirtualCamera[] _virtualCameras;

    int currentCamera = 0;

    [SerializeField] int startingCamera;
    [SerializeField] float levelStartDelayTime = 1;

    [Header("Casos Especiales")]
    [SerializeField] DialogueSO[] dialoguesEspeciales;
    [SerializeField] CameraMode[] camarasEspeciales;

    [Header("Pages that keep their camera (#237)")]
    [SerializeField, Tooltip("Page numbers (1 = first page) where the gameplay beats (folding an origami, receiving a reward, putting on the paper plane hat) leave the camera where it is, instead of cutting to their own shot and back to Normal. Level 2: page 4, which stays on BookCenter. Empty = every page switches, as always.")]
    int[] _pagesThatKeepTheirCamera;

    protected override void Awake()
    {
        base.Awake();

        EventManager.Subscribe(Evento.OnEncounterStart, SetCamera);
        EventManager.Subscribe(Evento.OnOrigamiGivePaperPlaneHat, SetCameraForBeat);
        EventManager.Subscribe(Evento.OnOrigamiCameraChange, SetCameraForBeat);

        currentCamera = startingCamera;
        StartCoroutine(LevelStartCameraMovement());
    }

    IEnumerator LevelStartCameraMovement()
    {
        yield return new WaitForSeconds(levelStartDelayTime);
        SetCamera(CameraMode.Normal);
    }

    void Update()
    {
        //click del medio (como siempre) o el gatillo R2 = camara siguiente; el gatillo L2 = la anterior.
        //Los nombres de los ejes viven en InputHub, no aca.
        if (InputHub.CambiarCamaraDown)
        {
            ToggleNextCamera();
        }
        else if (InputHub.CambiarCamaraAtrasDown)
        {
            TogglePreviousCamera();
        }
    }

    public CameraMode CurrentMode => (CameraMode)currentCamera;

    //The game drives these two (PlayerView, while casting an origami and while receiving a
    //reward): the player can't pick them, and can't leave them either, or a press of L2 would
    //pull the camera off the fold/reward mid-way (#43). Everything else is the player's.
    public static bool IsPlayerSelectable(CameraMode mode)
    {
        return mode != CameraMode.OrigamiCasting && mode != CameraMode.ReceiveReward;
    }

    public void ToggleNextCamera()
    {
        CycleCamera(1);
    }

    public void TogglePreviousCamera()
    {
        CycleCamera(-1);
    }

    //direction is +1 (next) or -1 (previous); both skip the game-driven modes and wrap around
    void CycleCamera(int direction)
    {
        if (!IsPlayerSelectable(CurrentMode))
        {
            Debug.Log($"[CameraManager] Cycle ignored: {CurrentMode} is game-driven");
            return;
        }

        int count = _virtualCameras.Length;
        for (int step = 1; step < count; step++)
        {
            int index = ((currentCamera + direction * step) % count + count) % count;
            CameraMode candidate = (CameraMode)index;
            if (IsPlayerSelectable(candidate))
            {
                SetCamera(candidate);
                PlaySetCameraSound();
                return;
            }
        }

        Debug.LogWarning("[CameraManager] Cycle: no other player-selectable camera in _virtualCameras");
    }

    //A pick from the camera wheel: same rules as cycling.
    public void SelectCamera(CameraMode mode)
    {
        if (!IsPlayerSelectable(CurrentMode))
        {
            Debug.Log($"[CameraManager] Pick of {mode} ignored: {CurrentMode} is game-driven");
            return;
        }

        if (!IsPlayerSelectable(mode))
        {
            Debug.LogWarning($"[CameraManager] {mode} is game-driven, a wheel button should not select it");
            return;
        }

        SetCamera(mode);
        PlaySetCameraSound();
    }
    public void TurnOffAllVirtualCameras()
    {
        foreach (Cinemachine.CinemachineVirtualCamera cam in _virtualCameras)
        {
            cam.gameObject.SetActive(false);
        }
    }

    public void SetCamera(int index)
    {
        SetCamera((CameraMode)index);
    }

    //Every camera change goes through here, game-driven or not, so OnCameraChange always
    //reports the live mode and the wheel's highlight can't go stale.
    public void SetCamera(CameraMode cam)
    {
        TurnOffAllVirtualCameras();
        currentCamera = (int)cam;
        if (currentCamera >= 0 && currentCamera < _virtualCameras.Length)
        {
            _virtualCameras[currentCamera].gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError($"[CameraManager] SetCamera: CameraMode index {currentCamera} is out of range (array length: {_virtualCameras.Length})");
        }
        EventManager.Trigger(Evento.OnCameraChange, cam);
    }

    /// <summary>
    /// A camera change that belongs to a gameplay beat (origami, reward, the paper plane hat), not to the
    /// story or the player: skipped on the pages listed in _pagesThatKeepTheirCamera.
    /// </summary>
    public void SetCameraForBeat(CameraMode cam)
    {
        if (CurrentPageKeepsItsCamera())
        {
            Debug.Log($"[CameraManager] {cam} skipped: this page keeps its camera ({CurrentMode})");
            return;
        }

        SetCamera(cam);
    }

    void SetCameraForBeat(params object[] parameters)
    {
        if (CurrentPageKeepsItsCamera())
        {
            Debug.Log($"[CameraManager] beat camera skipped: this page keeps its camera ({CurrentMode})");
            return;
        }

        SetCamera(parameters);
    }

    bool CurrentPageKeepsItsCamera()
    {
        if (_pagesThatKeepTheirCamera == null || _pagesThatKeepTheirCamera.Length == 0 || PageScrollerManager.Instance == null)
        {
            return false;
        }

        return System.Array.IndexOf(_pagesThatKeepTheirCamera, PageScrollerManager.Instance.activePageIndex + 1) >= 0;
    }

    public void SetCamera(params object[] parameters)
    {
        if (parameters != null && parameters.Length > 0)
        {
            if (parameters[0] is CameraMode)
            {
                SetCamera((CameraMode)parameters[0]);
            }
            else if (parameters[0] is int)
            {
                SetCamera((int)parameters[0]);
            }
            else
            {
                Debug.LogWarning($"[CameraManager] SetCamera(params): unknown parameter type {parameters[0]?.GetType()}");
            }
        }
    }

    public void PlaySetCameraSound()
    {
        AudioManager.instance.Play(AudioId.PickupSFX, 1.8f - (currentCamera / 100f));
    }
    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnEncounterStart, SetCamera);
            EventManager.Unsubscribe(Evento.OnOrigamiGivePaperPlaneHat, SetCameraForBeat);
            EventManager.Unsubscribe(Evento.OnOrigamiCameraChange, SetCameraForBeat);
        }
    }
}
