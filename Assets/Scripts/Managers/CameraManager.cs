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

    protected override void Awake()
    {
        base.Awake();

        EventManager.Subscribe(Evento.OnEncounterStart, SetCamera);
        EventManager.Subscribe(Evento.OnOrigamiGivePaperPlaneHat, SetCamera);
        EventManager.Subscribe(Evento.OnOrigamiCameraChange, SetCamera);

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
        //click del medio (como siempre), R1 o el gatillo L2 del joystick. Los tres nombres de
        //botones viven en InputHub, no aca.
        if (InputHub.CambiarCamaraDown)
        {
            ToggleNextCamera();
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
        if (!IsPlayerSelectable(CurrentMode))
        {
            Debug.Log($"[CameraManager] Cycle ignored: {CurrentMode} is game-driven");
            return;
        }

        for (int step = 1; step < _virtualCameras.Length; step++)
        {
            CameraMode candidate = (CameraMode)((currentCamera + step) % _virtualCameras.Length);
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
            EventManager.Unsubscribe(Evento.OnOrigamiGivePaperPlaneHat, SetCamera);
            EventManager.Unsubscribe(Evento.OnOrigamiCameraChange, SetCamera);
        }
    }
}
