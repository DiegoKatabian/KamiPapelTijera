using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct FlapDisplay
{
    public int number;
    public GameObject display;
    public FlapDisplayButton flapButton;
}

public class FlapManager : Singleton<FlapManager>
{
    [SerializeField] float _posYOpen = 350;
    [SerializeField] float _flapTransitionDuration = 0.5f; // Tiempo de transición en segundos
    [SerializeField] GameObject _seguroOverlay;
    [SerializeField] Slider _sliderBrillo, _sliderContraste, _sliderVolumen;
    [SerializeField] FlapDisplay[] _flapDisplays;
    [SerializeField] Image _tiritaPull, _tiritaPush;

    float _posYClosed = 0;
    bool _isOpen = false;
    float _valueBeforeMute = 1;

    //flapdisplays:
    //0 es quests
    //1 es inventory
    //2 es settings
    //3 es controles

    private void Start()
    {
        _posYClosed = transform.position.y;
        EventManager.Subscribe(Evento.OnPlayerPressedEsc, OpenSettings);
        EventManager.Subscribe(Evento.OnPlayerPressedM, ToggleMute);
        EventManager.Subscribe(Evento.OnPlayerPressedI, OpenInventory);
        EventManager.Subscribe(Evento.OnPlayerPressedU, OpenQuests);
    }

    //funcionamiento del flap
    public void OpenFlap()
    {
        //Debug.Log("FlapManager: open flap");
        AudioManager.instance.PlayByName("PageTurn02", 1.6f, 0.01f);
        AudioManager.instance.SetBGMVolumes(0.4f);
        _tiritaPull.gameObject.SetActive(false);
        _tiritaPush.gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(MoveFlap(_posYOpen));
    }   
    public void CloseFlap()
    {
        //Debug.Log("FlapManager: close flap");
        AudioManager.instance.PlayByName("PageTurn01", 1.6f, 0.01f);
        AudioManager.instance.ResetBGMVolumes();
        _tiritaPull.gameObject.SetActive(true);
        _tiritaPush.gameObject.SetActive(false);

        //sin esto queda un boton del menu seleccionado y, como el boton B del joystick es Submit,
        //el jugador lo seguiria apretando sin querer mientras juega
        UISelector.Limpiar();

        StopAllCoroutines();
        StartCoroutine(MoveFlap(_posYClosed));
    }
    public IEnumerator MoveFlap(float targetY)
    {
        Time.timeScale = 1;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        float t;

        while (elapsedTime < _flapTransitionDuration)
        {
            t = Mathf.SmoothStep(0, 1, elapsedTime / _flapTransitionDuration);
            transform.position = Vector3.Lerp(startPosition, new Vector3(transform.position.x, targetY, transform.position.z), t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        _isOpen = (targetY == _posYOpen);
        if (_isOpen)
        {
            Time.timeScale = 0;

            //recien aca el menu esta realmente abierto: es el unico momento en que es seguro
            //dejar algo seleccionado para que el joystick pueda navegarlo
            SeleccionarDisplayVisible();
        }
    }


    //teclas del jugador
    public void ToggleFlap(params object[] parameters)
    {
        if (_isOpen)
        {
            CloseFlap();
        }
        else
        {
            OpenFlap();
        }
    }
    public void OpenQuests(params object[] parameters)
    {
        ShowDesiredDisplay(_flapDisplays[0]);
        ToggleFlap();
    }
    public void OpenInventory(params object[] parameters)
    {
        ShowDesiredDisplay(_flapDisplays[1]);
        ToggleFlap();
    }
    public void OpenSettings(params object[] parameters)
    {
        ShowDesiredDisplay(_flapDisplays[2]);
        ToggleFlap();
    }



    //botones y sliders
    public void BTN_Salir()
    {
        _seguroOverlay.SetActive(true);
        Debug.Log("prendo el overlay");
        AudioManager.instance.PlayByName("PickupSFX", 1.25f);

        //el seguro es un dialogo modal (Si/No) encima del menu: si no seleccionamos uno de sus
        //botones, con joystick no habria forma de contestarle. UISelector avisa si _seguroOverlay
        //fuera null o no tuviera botones, no explota.
        UISelector.SeleccionarPrimeroSiJoystick(_seguroOverlay);
    }
    public void BTN_Settings()
    {
        ShowDesiredDisplay(_flapDisplays[0]);
        //Debug.Log("prendo el overlay");
        AudioManager.instance.PlayByName("PageTurn02", 2.6f, 0.01f);
    }
    public void BTN_Inventory()
    {
        ShowDesiredDisplay(_flapDisplays[1]);
        //Debug.Log("muestro el inventario");
        AudioManager.instance.PlayByName("PageTurn02", 2.6f, 0.01f);
    }
    public void BTN_Quests()
    {
        ShowDesiredDisplay(_flapDisplays[2]);
        //Debug.Log("muestro las quests");
        AudioManager.instance.PlayByName("PageTurn02", 2.6f, 0.01f);
    }
    public void BTN_Controles()
    {
        ShowDesiredDisplay(_flapDisplays[3]);
        //Debug.Log("muestro las quests");
        AudioManager.instance.PlayByName("PageTurn02", 2.6f, 0.01f);
    }
    public void BTN_Si()
    {
        Debug.Log("chau :(");
        AudioManager.instance.PlayByName("PickupSFX", 1.25f);

        Application.Quit();
    }
    public void BTN_No()
    {
        _seguroOverlay.SetActive(false);
        AudioManager.instance.PlayByName("PickupReversedSFX", 2.5f);

        Debug.Log("apago el overlay");

        //el boton que estaba seleccionado (el "No" del seguro) se acaba de desactivar: si no
        //devolvemos el foco al display visible, el joystick se queda sin nada que navegar
        SeleccionarDisplayVisible();
    }
    public void SLIDER_Volumen()
    {
        AudioManager.instance.SetGlobalVolume(_sliderVolumen.value);
    }
    public void SLIDER_Brillo()
    {
        PostProcessManager.Instance.SetBrightnessValue(_sliderBrillo.value);
    }
    public void SLIDER_Contraste()
    {
        PostProcessManager.Instance.SetContrastValue(_sliderContraste.value);
    }
    public void ToggleMute(params object[] parameters)
    {
        Debug.Log("toggle mute");

        if (_sliderVolumen.value == 0)
        {
            _sliderVolumen.value = _valueBeforeMute;
        }
        else
        {
            _valueBeforeMute = _sliderVolumen.value;
            _sliderVolumen.value = 0;
        }
    }



    //auxiliares
    public void ShowDesiredDisplay(FlapDisplay flapDisplay)
    {
        foreach (FlapDisplay d in _flapDisplays)
        {
            //Debug.Log("apago display");
            d.display.SetActive(false);
            d.flapButton.Deactivate();
        }

        //Debug.Log("show desired display - " + flapDisplay);
        flapDisplay.display.SetActive(true);
        flapDisplay.flapButton.Activate();

        //Con joystick hace falta que HAYA algo seleccionado para que el stick pueda navegar el menu,
        //pero SOLO si el menu esta efectivamente abierto. Antes seleccionabamos siempre, y como
        //OpenQuests/OpenInventory/OpenSettings llaman aca ANTES de ToggleFlap, quedaba un boton
        //seleccionado con el flap cerrado: el boton A es Submit, asi que el jugador terminaba
        //apretando botones fantasma del menu mientras jugaba. Cuando el flap se abre desde cero,
        //la seleccion la hace MoveFlap al terminar de abrirse.
        if (_isOpen)
        {
            SeleccionarDentroDe(flapDisplay);
        }
    }

    /// <summary>
    /// Deja el foco adentro de ese display; y si el display no tiene NADA navegable (el caso de
    /// Tareas cuando no hay quests: son slots de texto, no botones), cae en la solapa del propio
    /// display. Sin ese fallback el jugador entraba a Tareas y quedaba trabado: no habia nada
    /// seleccionado, asi que el stick no movia nada y no habia forma de volver a las otras solapas.
    /// </summary>
    void SeleccionarDentroDe(FlapDisplay flapDisplay)
    {
        //avisarSiNoHay en false: que un display no tenga botones es una configuracion valida,
        //no un error. El fallback de abajo se encarga.
        if (UISelector.SeleccionarPrimeroSiJoystick(flapDisplay.display, false))
        {
            return;
        }

        if (flapDisplay.flapButton != null)
        {
            UISelector.SeleccionarPrimeroSiJoystick(flapDisplay.flapButton.gameObject);
        }
    }

    /// <summary>
    /// Vuelve a poner el foco en el display que se este viendo (lo usa BTN_No al apagar el seguro
    /// de salir). Si el flap ya no esta abierto no selecciona nada: un boton seleccionado con el
    /// menu cerrado seria un boton fantasma que el B del joystick apretaria durante el gameplay.
    /// </summary>
    void SeleccionarDisplayVisible()
    {
        if (!_isOpen)
        {
            UISelector.Limpiar();
            return;
        }

        foreach (FlapDisplay d in _flapDisplays)
        {
            if (d.display != null && d.display.activeInHierarchy)
            {
                SeleccionarDentroDe(d);
                return;
            }
        }

        //ningun display prendido: mejor sin seleccion que con una que no se ve
        UISelector.Limpiar();
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedEsc, OpenSettings);
            EventManager.Unsubscribe(Evento.OnPlayerPressedM, ToggleMute);
            EventManager.Unsubscribe(Evento.OnPlayerPressedI, OpenInventory);
            EventManager.Unsubscribe(Evento.OnPlayerPressedU, OpenQuests);
        }
    }
}
