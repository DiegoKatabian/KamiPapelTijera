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
        if (Input.GetKeyDown(KeyCode.Mouse2))
        {
            ToggleNextCamera();
        }
    }

    public void ToggleNextCamera()
    {
        //prendo la nueva. uso un index para saber cual tengo que encender.
        currentCamera++;
        if (currentCamera >= _virtualCameras.Length) //por si me paso del array
        {
            currentCamera = 0;
        }
        _virtualCameras[currentCamera].gameObject.SetActive(true);


        //apago la anterior. pero el index es distinto, asi que me hago un nuevo int
        int previousCamera = currentCamera - 1;
        if (previousCamera < 0) //si yo le pido index -1 al array crashea unity y se me apaga la compu todo mal. asi que primero chequeo que eso no pase
        {
            previousCamera = _virtualCameras.Length - 1;
        }

        _virtualCameras[previousCamera].gameObject.SetActive(false);
        EventManager.Trigger(Evento.OnCameraChange, currentCamera);
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
        TurnOffAllVirtualCameras();
        currentCamera = index;
        _virtualCameras[currentCamera].gameObject.SetActive(true);

    }
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
        AudioManager.instance.PlayByName("PickupSFX", 1.8f - (currentCamera / 100f));
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
