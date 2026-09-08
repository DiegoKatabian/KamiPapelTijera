using UnityEngine;
using UnityEngine.UI;

public class CamWheelButton : MonoBehaviour
{
    [HideInInspector] public Button button;

    [SerializeField, Tooltip("Cuanto se agranda el boton de la camara activa")]
    float _escalaActiva = 1.15f;

    Vector3 _escalaOriginal;

    void Awake()
    {
        //en Awake y no en Start: CamWheelManager lo puede resaltar apenas arranca la escena
        button = GetComponent<Button>();
        _escalaOriginal = transform.localScale;
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
