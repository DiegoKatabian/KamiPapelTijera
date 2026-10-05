using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public struct FlapDisplay
{
    public int number;
    public GameObject display;
    public FlapDisplayButton flapButton;
}

//Spec 011 FR-301: the only source of truth for the Flap. Nothing infers "open" from the paper's
//position or from a slide having ended.
public enum FlapState
{
    Closed,
    Opening,
    Open,
    Closing
}

public class FlapManager : Singleton<FlapManager>
{
    [SerializeField] float _posYOpen = 350;
    [SerializeField] float _flapTransitionDuration = 0.5f; // Tiempo de transici�n en segundos
    [SerializeField] GameObject _seguroOverlay;
    [SerializeField] Slider _sliderBrillo, _sliderContraste, _sliderVolumen;
    [SerializeField] FlapDisplay[] _flapDisplays;
    [SerializeField] Image _tiritaPull, _tiritaPush;

    float _posYClosed = 0;
    FlapState _state = FlapState.Closed;
    float _valueBeforeMute = 1;
    int _currentDisplayIndex = 0;

    //FR-303: one per menu root (each display, its tab button, the exit confirm), added by code
    readonly List<CanvasGroup> _menuGroups = new List<CanvasGroup>();
    //FR-305: each leaked selection is reported once, not every frame
    readonly HashSet<GameObject> _leaksReported = new HashSet<GameObject>();

    /// <summary>
    /// Spec 011 FR-306 (Q14): the menu owns the player's input from the moment it starts opening
    /// until the moment it starts closing, so Esc freezes Kami at once and closing gives her back at
    /// once. PlayerController (issue #41.3), TriggerOrigami and Player read it as "the pause".
    /// </summary>
    public bool IsMenuOpen => _state == FlapState.Opening || _state == FlapState.Open;

    /// <summary>
    /// The paper is all the way down (Time.timeScale is 0). The only state where the menu takes its
    /// own input (R1/L1/B), gets selected by code, or takes clicks (FR-302, FR-303).
    /// </summary>
    public bool IsFullyOpen => _state == FlapState.Open;

    //flapdisplays:
    //0 es quests
    //1 es inventory
    //2 es settings
    //3 es controles
    //4 is the Wardrobe (spec 011): appended last so the hardcoded indexes above keep working

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return; //a duplicate, being destroyed
        }

        AddMenuGroups();
    }

    private void Start()
    {
        _posYClosed = transform.position.y;
        SetTabsVisible(false); //the Flap starts closed
        EventManager.Subscribe(Evento.OnPlayerPressedEsc, OpenSettings);
        EventManager.Subscribe(Evento.OnPlayerPressedM, ToggleMute);
        EventManager.Subscribe(Evento.OnPlayerPressedI, OpenInventory);
        EventManager.Subscribe(Evento.OnPlayerPressedU, OpenQuests);
    }

    //funcionamiento del flap
    public void OpenFlap()
    {
        //Debug.Log("FlapManager: open flap");
        AudioManager.instance.Play(AudioId.PageTurn02, 1.6f, 0.01f);
        AudioManager.instance.SetBGMVolumes(0.4f);
        _tiritaPull.gameObject.SetActive(false);
        _tiritaPush.gameObject.SetActive(true);
        SetTabsVisible(true);

        StopAllCoroutines();
        StartCoroutine(MoveFlap(true));
    }   
    public void CloseFlap()
    {
        //Debug.Log("FlapManager: close flap");
        AudioManager.instance.Play(AudioId.PageTurn01, 1.6f, 0.01f);
        AudioManager.instance.ResetBGMVolumes();
        _tiritaPull.gameObject.SetActive(true);
        _tiritaPush.gameObject.SetActive(false);

        StopAllCoroutines();
        StartCoroutine(MoveFlap(false));
    }

    /// <summary>
    /// Issue #41.1: con el menu abierto, R1/L1 ciclan de tab y B cierra el Flap (contextual,
    /// mismo patron que "B cancela" en el origami). Ninguno de los tres pasa por
    /// PlayerController: ese script ya se auto-gatea cuando el menu esta abierto (issue
    /// #41.3), asi que leerlos directo aca no puede pisarle un ataque o un B de gameplay.
    /// </summary>
    private void Update()
    {
        //FR-302: not during the slides either. While closing, L1 is also sprint, and "close the menu
        //and start running" used to switch tabs and select inside a menu that was going away (F6).
        if (!IsFullyOpen)
        {
            DeselectLeakedMenuSelection();
            return;
        }

        //el seguro de "salir del juego" es un dialogo modal ENCIMA del flap: mientras esta
        //abierto, B tiene que contestarle a EL (como el boton "No"), no cerrar el flap entero
        //por atras dejando la pregunta sin responder
        if (_seguroOverlay != null && _seguroOverlay.activeSelf)
        {
            if (InputHub.AtaqueGamepadDown)
            {
                Debug.Log("[FlapManager] B cierra el seguro de salir (equivalente a 'No')");
                BTN_No();
            }
            return;
        }

        if (InputHub.AtaqueGamepadDown)
        {
            Debug.Log("[FlapManager] B cierra el Flap");
            CloseFlap();
            return;
        }

        if (InputHub.TabSiguienteDown)
        {
            CambiarTab(1);
        }
        else if (InputHub.TabAnteriorDown)
        {
            CambiarTab(-1);
        }
    }

    /// <summary>Cicla entre las secciones del Flap (Tareas/Morral/Settings/Controles/Wardrobe) con R1/L1.</summary>
    void CambiarTab(int direccion)
    {
        if (_flapDisplays == null || _flapDisplays.Length == 0)
        {
            return;
        }

        //modulo "a mano" porque el % de C# puede devolver negativo con direccion=-1
        int nuevoIndex = ((_currentDisplayIndex + direccion) % _flapDisplays.Length + _flapDisplays.Length) % _flapDisplays.Length;

        Debug.Log($"[FlapManager] cambio de tab con joystick: {_currentDisplayIndex} -> {nuevoIndex}");
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
        ShowDesiredDisplay(_flapDisplays[nuevoIndex]);
    }
    public IEnumerator MoveFlap(bool opening)
    {
        float targetY = opening ? _posYOpen : _posYClosed;
        SetState(opening ? FlapState.Opening : FlapState.Closing);

        //the slide runs on Time.deltaTime: at timeScale 0 (the Flap pauses once Open) it would never move
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
        SetState(opening ? FlapState.Open : FlapState.Closed);
        if (opening)
        {
            Time.timeScale = 0;

            //recien aca el menu esta realmente abierto: es el unico momento en que es seguro
            //dejar algo seleccionado para que el joystick pueda navegarlo
            SeleccionarDisplayVisible();
        }
        else
        {
            SetTabsVisible(false);
        }
    }

    void SetState(FlapState newState)
    {
        if (newState == _state)
        {
            return;
        }

        FlapState previous = _state;
        _state = newState;
        Debug.Log($"[FlapManager] {previous} -> {newState}");

        //FR-302: a selection belongs to the fully open menu. Left behind, the EventSystem keeps
        //driving it with the gameplay axes: walking moves a selected slider, and A/E (Submit)
        //presses a selected button. Limpiar also cancels UISelector's pending one-frame retry.
        //Every slide start clears, which covers leaving Open, and also the pull tab: a click
        //selects it (Automatic navigation), and WASD during the opening slide would navigate from
        //it into the menu.
        if (newState == FlapState.Opening || newState == FlapState.Closing)
        {
            UISelector.Limpiar();
        }

        ApplyMenuInteraction();
    }

    //FR-303: the menu takes clicks only while Open (blocksRaycasts), and takes navigation and Submit
    //(interactable) unless Closed. interactable stays on during the slides on purpose: off, it switches
    //every Selectable to its Disabled tint (0.78 gray, half alpha), which would flash while the paper
    //moves on screen. During the slides "no clicks + no selection by code" is enough.
    void ApplyMenuInteraction()
    {
        foreach (CanvasGroup group in _menuGroups)
        {
            if (group == null)
            {
                continue;
            }
            group.blocksRaycasts = _state == FlapState.Open;
            group.interactable = _state != FlapState.Closed;
        }
    }

    void AddMenuGroups()
    {
        if (_flapDisplays == null || _flapDisplays.Length == 0)
        {
            Debug.LogWarning("[FlapManager] no _flapDisplays: the menu can't be guarded while closed");
        }
        else
        {
            foreach (FlapDisplay d in _flapDisplays)
            {
                AddMenuGroup(d.display, $"display {d.number}");
                AddMenuGroup(d.flapButton != null ? d.flapButton.gameObject : null, $"tab button {d.number}");
            }
        }
        AddMenuGroup(_seguroOverlay, "_seguroOverlay");

        ApplyMenuInteraction(); //the Flap starts Closed
    }

    void AddMenuGroup(GameObject root, string what)
    {
        if (root == null)
        {
            Debug.LogWarning($"[FlapManager] {what} is not assigned: it can't be guarded while the Flap is closed");
            return;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }
        _menuGroups.Add(group);
    }

    //uGUI 1.0 caches, per Selectable, whether its CanvasGroups allow interaction, and refreshes it only
    //when a group changes while the Selectable is active (Selectable.OnEnable doesn't). A display that
    //was hidden while the Flap closed and opened would come back with a stale answer: greyed out and
    //unclickable in an open Flap, or live in a closed one. Flipping the group makes every active
    //Selectable under it read it again. Call it right after showing a menu root, before selecting in it.
    void RefreshMenuGroup(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            return;
        }

        bool interactable = group.interactable;
        group.interactable = !interactable;
        group.interactable = interactable;
    }

    //FR-305, the safety net: nothing in the menu should be selected unless the Flap is Open. If
    //something is, it is deselected before walking can drive it, and named once so the path that
    //selected it can be found.
    void DeselectLeakedMenuSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (!IsInMenu(selected.transform))
        {
            return; //the HUD strip (the pull tab) can stay selected: it only opens the Flap
        }

        if (_leaksReported.Add(selected))
        {
            Debug.LogWarning($"[FlapManager] '{selected.name}' was selected while the Flap is {_state}: deselected (FR-305). Something still selects inside the menu outside Open: report it");
        }
        UISelector.Limpiar();
    }

    bool IsInMenu(Transform t)
    {
        foreach (CanvasGroup group in _menuGroups)
        {
            if (group != null && t.IsChildOf(group.transform))
            {
                return true;
            }
        }
        return false;
    }

    //Spec 011 task 4.A: the tab buttons only exist while the Flap is showing. Closed, the paper sits
    //off screen but the HUD strip hanging under it (pull tab, health, paper) stays visible, and a tab
    //placed low enough peeks into it (the Wardrobe one did): clickable during gameplay, and a clicked
    //tab stays selected, so walking would then navigate the hidden menu. Inactive, a tab can't be seen,
    //clicked or reached by navigation.
    void SetTabsVisible(bool visible)
    {
        foreach (FlapDisplay d in _flapDisplays)
        {
            if (d.flapButton == null)
            {
                Debug.LogWarning($"[FlapManager] display {d.number} has no tab button: nothing to show or hide");
                continue;
            }
            d.flapButton.gameObject.SetActive(visible);
        }
    }


    //teclas del jugador
    public void ToggleFlap(params object[] parameters)
    {
        //Q15: a toggle mid-slide reverses it (Esc while opening closes, while closing reopens).
        //MoveFlap lerps from wherever the paper is, so reversing is just starting the other slide.
        if (_state == FlapState.Opening || _state == FlapState.Open)
        {
            CloseFlap();
        }
        else
        {
            OpenFlap();
        }
    }

    /// <summary>
    /// Issue #41 ronda 2, punto 1: wrapper sin argumentos para el boton de UI de la tirita
    /// (Assets/Prefabs/UI/FlapManager.prefab, GO "Tirita fondo"). El Editor solo lista, para
    /// bindear un OnClick de Button, metodos de 0 parametros: ToggleFlap(params object[]) tiene
    /// 1 parametro en runtime (el array), asi que nunca aparecia como opcion y el OnClick habia
    /// quedado vacio (con A/click no pasaba nada, aunque la tirita fuera navegable).
    /// </summary>
    public void BTN_ToggleFlap()
    {
        ToggleFlap();
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
        RefreshMenuGroup(_seguroOverlay);
        Debug.Log("prendo el overlay");
        AudioManager.instance.Play(AudioId.PickupSFX, 1.25f);

        //el seguro es un dialogo modal (Si/No) encima del menu: si no seleccionamos uno de sus
        //botones, con joystick no habria forma de contestarle. UISelector avisa si _seguroOverlay
        //fuera null o no tuviera botones, no explota.
        //Only fully open (FR-302): Exit is a menu button, so this can't run otherwise once 4.C
        //blocks clicks, but a selection made by code outside Open is exactly the leak F6 describes.
        if (IsFullyOpen)
        {
            UISelector.SeleccionarPrimeroSiJoystick(_seguroOverlay);
        }
    }
    public void BTN_Settings()
    {
        ShowDesiredDisplay(_flapDisplays[0]);
        //Debug.Log("prendo el overlay");
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
    }
    public void BTN_Inventory()
    {
        ShowDesiredDisplay(_flapDisplays[1]);
        //Debug.Log("muestro el inventario");
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
    }
    public void BTN_Quests()
    {
        ShowDesiredDisplay(_flapDisplays[2]);
        //Debug.Log("muestro las quests");
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
    }
    public void BTN_Controles()
    {
        ShowDesiredDisplay(_flapDisplays[3]);
        //Debug.Log("muestro las quests");
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
    }
    public void BTN_Wardrobe()
    {
        const int wardrobeIndex = 4;
        if (_flapDisplays == null || _flapDisplays.Length <= wardrobeIndex)
        {
            Debug.LogWarning("[FlapManager] BTN_Wardrobe: no Wardrobe display at index 4 of _flapDisplays");
            return;
        }

        ShowDesiredDisplay(_flapDisplays[wardrobeIndex]);
        AudioManager.instance.Play(AudioId.PageTurn02, 2.6f, 0.01f);
    }
    public void BTN_Si()
    {
        Debug.Log("chau :(");
        AudioManager.instance.Play(AudioId.PickupSFX, 1.25f);

        Application.Quit();
    }
    public void BTN_No()
    {
        _seguroOverlay.SetActive(false);
        AudioManager.instance.Play(AudioId.PickupReversedSFX, 2.5f);

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
        RefreshMenuGroup(flapDisplay.display); //after its OnEnable (the Wardrobe builds its buttons there), before selecting
        flapDisplay.flapButton.Activate();

        //registrado aca (y no solo en CambiarTab) porque BTN_Settings/Inventory/Quests/Controles
        //y OpenQuests/Inventory/Settings tambien llegan a este metodo: sin esto, R1/L1 arrancarian
        //ciclando siempre desde el tab con el que se abrio el Flap la primera vez, no desde el
        //que el jugador esta viendo ahora
        _currentDisplayIndex = flapDisplay.number;

        //Con joystick hace falta que HAYA algo seleccionado para que el stick pueda navegar el menu,
        //pero SOLO si el menu esta efectivamente abierto. Antes seleccionabamos siempre, y como
        //OpenQuests/OpenInventory/OpenSettings llaman aca ANTES de ToggleFlap, quedaba un boton
        //seleccionado con el flap cerrado: el boton A es Submit, asi que el jugador terminaba
        //apretando botones fantasma del menu mientras jugaba. Cuando el flap se abre desde cero,
        //la seleccion la hace MoveFlap al terminar de abrirse.
        if (IsFullyOpen)
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
        if (!IsFullyOpen)
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
