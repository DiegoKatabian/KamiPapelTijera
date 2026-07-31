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

    bool _isOpen = false;
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
    }
    public void OpenFlap()
    {
        //Debug.Log("open");
        AudioManager.instance.PlayByName("PageTurn02", 2.6f, 0.01f);
        StopAllCoroutines();
        StartCoroutine(MoveFlap(_posXOpen, _posYOpen, _transitionDuration));
        _isOpen = true;
    }
    public void CloseFlap()
    {
        //Debug.Log("close");
        AudioManager.instance.PlayByName("PageTurn01", 2.6f, 0.01f);
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
    public void ChangeCamera(int cameraIndex)
    {
        CameraManager.Instance.SetCamera((CameraMode)cameraIndex);
        CameraManager.Instance.PlaySetCameraSound();

        switch (cameraIndex)
        {
            case 0: //close up
                FakeSelectButton(0);
                break;
                case 1: //close up origami
                //FakeSelectButton(1);
                break;
                case 2: //normal
                FakeSelectButton(1);
                break;
                case 3: //general
                FakeSelectButton(2);
                break;
                case 4: //book
                FakeSelectButton(3);
                break;
                case 5: //receive reward
                //FakeSelectButton(5);
                break;
        }
    }
    public void FakeSelectButton(params object[] parameters)
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (i == (int)parameters[0])
            {
                _buttons[i].button.Select();
            }
        }
    }
}
