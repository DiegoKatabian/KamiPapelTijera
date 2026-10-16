using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CamWheelManager : Singleton<CamWheelManager>, IFlap
{
    [SerializeField] GameObject camWheelParent;
    [SerializeField] float _posXOpen;
    [SerializeField] float _posYOpen;
    [SerializeField] float _transitionDuration;

    [Header("Cutscenes (#226)")]
    [SerializeField, Tooltip("The CanvasGroup on this prefab's root. Faded out while something owns Kami (LevelManager.inCutscene), the same rule as the Flap's HUD strip (#188), and an open wheel is closed first.")]
    CanvasGroup _hudGroup;

    [SerializeField, Tooltip("Seconds the wheel takes to fade out when Kami is locked, and back in when she is free.")]
    float _hudFadeSeconds = 0.25f;

    bool _isOpen = false;
    bool _hudHidden;
    float _posXClosed, _posYClosed;
    CamWheelButton[] _buttons;

    public void Start()
    {
        _posXClosed = camWheelParent.transform.localPosition.x;
        _posYClosed = camWheelParent.transform.localPosition.y;

        _posXOpen += 960;
        _posYOpen += 540;

        _buttons = GetComponentsInChildren<CamWheelButton>();

        EventManager.Subscribe(Evento.OnCameraChange, FakeSelectButton);

        if (_hudGroup == null)
        {
            Debug.LogWarning("[CamWheelManager] _hudGroup is not assigned: the camera selector stays on screen during cutscenes");
        }
    }

    //#226: alpha + raycasts only, never SetActive (the wheel's coroutines live here) and never
    //interactable (Selectable caches it, see "Fugas de foco" in controles-y-gamepad.md)
    void Update()
    {
        if (_hudGroup == null)
        {
            return;
        }

        bool hide = LevelManager.Instance != null && LevelManager.Instance.inCutscene;

        if (hide != _hudHidden)
        {
            _hudHidden = hide;
            _hudGroup.blocksRaycasts = !hide;
            Debug.Log($"[CamWheelManager] camera selector {(hide ? "hidden: Kami is locked by a cutscene" : "shown")}");

            if (hide && _isOpen)
            {
                CloseFlap();
            }
        }

        float step = _hudFadeSeconds > 0f ? Time.unscaledDeltaTime / _hudFadeSeconds : 1f;
        _hudGroup.alpha = Mathf.MoveTowards(_hudGroup.alpha, hide ? 0f : 1f, step);
    }
    public void OpenFlap()
    {
        //Debug.Log("open");
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
        StopAllCoroutines();
        StartCoroutine(MoveFlap(_posXOpen, _posYOpen, _transitionDuration));
        _isOpen = true;
    }
    public void CloseFlap()
    {
        //Debug.Log("close");
        AudioManager.instance.Play(AudioId.PageTurn01, 2.6f, 0.01f);
        StopAllCoroutines();
        StartCoroutine(MoveFlap(_posXClosed, _posYClosed, _transitionDuration));
        _isOpen = false;
        
    }
    public IEnumerator MoveFlap(float targetX, float targetY, float transitionDuration)
    {
        //Debug.Log("move");
        Vector3 startPosition = camWheelParent.transform.localPosition;
        Vector3 targetPosition = new Vector3(targetX, targetY, camWheelParent.transform.localPosition.z);
        float elapsedTime = 0f;
        float t;

        while (elapsedTime < transitionDuration)
        {
            t = Mathf.SmoothStep(0, 1, elapsedTime / transitionDuration);
            camWheelParent.transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        camWheelParent.transform.localPosition = targetPosition;
    }
    public void ToggleFlap()
    {
        if (_isOpen)
        {
            CloseFlap();
        }
        else
        {
            OpenFlap();
        }
        //Debug.Log("isopen" + _isOpen);

    }
    //Resalta el boton de la camara activa. NO usa el EventSystem: ver el comentario de
    //CamWheelButton.Activate(). Antes hacia button.Select() y la rueda se quedaba con el foco
    //despues de cada cambio de camara, asi que el Submit del joystick la volvia a apretar.
    //The button is found by the CameraMode it declares, never by its position in the wheel.
    //While a game-driven camera is live (origami, reward) no button matches, so none is lit.
    public void FakeSelectButton(params object[] parameters)
    {
        if (parameters == null || parameters.Length == 0 || !(parameters[0] is CameraMode))
        {
            Debug.LogWarning("[CamWheelManager] FakeSelectButton without a CameraMode, nothing highlighted");
            return;
        }

        CameraMode live = (CameraMode)parameters[0];

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null)
            {
                continue;
            }

            if (_buttons[i].Mode == live)
            {
                _buttons[i].Activate();
            }
            else
            {
                _buttons[i].Deactivate();
            }
        }
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnCameraChange, FakeSelectButton);
    }
}
