using UnityEngine;
using UnityEngine.UI;

public class CamWheelButton : MonoBehaviour
{
    [HideInInspector] public Button button;

    [SerializeField, Tooltip("The camera this button selects. The only place that ties a wheel button to a camera: " +
        "clicking it and highlighting it both read this. Never OrigamiCasting or ReceiveReward (game-driven).")]
    CameraMode _mode;

    [SerializeField, Tooltip("Cuanto se agranda el boton de la camara activa")]
    float _escalaActiva = 1.15f;

    Vector3 _escalaOriginal;

    public CameraMode Mode => _mode;

    void Awake()
    {
        //en Awake y no en Start: CamWheelManager lo puede resaltar apenas arranca la escena
        button = GetComponent<Button>();
        _escalaOriginal = transform.localScale;
    }

    //Wired to this button's own OnClick (CamWheelButton.prefab).
    public void BTN_SelectCamera()
    {
        if (CameraManager.Instance == null)
        {
            Debug.LogWarning($"[CamWheelButton] '{name}': no CameraManager in the scene, {_mode} not selected");
            return;
        }

        CameraManager.Instance.SelectCamera(_mode);
    }

    //Resaltar por ESCALA y no por color ni por EventSystem, a proposito:
    //- el color lo pisa el ColorBlock del propio Button cuando el mouse pasa por encima;
    //- y la version anterior usaba button.Select(), que es literalmente
    //  EventSystem.SetSelectedGameObject: eso dejaba la rueda con el foco del EventSystem
    //  despues de CADA cambio de camara (incluido el L2 del joystick y los automaticos del
    //  cambio de pagina), asi que el siguiente Submit volvia a apretar el boton solo.
    //  La rueda se usa con el mouse; nunca tiene que tomar foco.
    public void Activate()
    {
        transform.localScale = _escalaOriginal * _escalaActiva;
    }

    public void Deactivate()
    {
        transform.localScale = _escalaOriginal;
    }
}
