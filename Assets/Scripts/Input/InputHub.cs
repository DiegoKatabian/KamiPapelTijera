using UnityEngine;

/// <summary>
/// Fachada unica de input del juego. El resto del codigo pregunta por ACCIONES
/// ("aprete saltar", "aprete accion") y nunca por teclas ni por botones de joystick.
///
/// Por que existe: antes cada script leia Input.GetKeyDown / GetButtonDown con el nombre
/// del eje a mano, asi que sumar el joystick significaba tocar diez archivos y acordarse
/// de todos. Ahora los nombres de ejes viven SOLO aca.
///
/// Los ejes en si estan definidos en ProjectSettings/InputManager.asset (input viejo de
/// Unity, este proyecto no usa el paquete nuevo Input System).
/// </summary>
public static class InputHub
{
    //nombres de los ejes del Input Manager. si cambian alla, se cambian aca y listo.
    const string EJE_INTERACT = "Interact";           //E / Enter / boton B del joystick
    const string EJE_FIRE1 = "Fire1";                 //click izq / ctrl (solo teclado+mouse)
    const string EJE_JUMP = "Jump";                   //espacio / boton A del joystick
    const string EJE_RUN = "Run";                     //shift / L1
    const string EJE_ACCION_GAMEPAD = "GamepadAction";//SOLO el boton A del joystick: hace falta
                                                      //distinguirlo de E/Espacio para resolver el
                                                      //A contextual (ver PlayerController)
    const string EJE_ATAQUE_GAMEPAD = "GamepadAttack";//boton B: atacar, siempre
    const string EJE_CAMARA = "CameraToggle";         //click del medio / R1
    const string EJE_CAMARA_TRIGGER = "CameraTrigger";//L2 (gatillo, es un EJE y no un boton)
    const string EJE_MUTE = "Mute";                   //M (solo teclado: en el joystick no hay boton de sobra)
    const string EJE_OPCIONES = "Options";            //Esc / Start
    const string EJE_INVENTARIO = "Inventory";        //I (solo teclado)
    const string EJE_QUESTS = "Quests";               //U (solo teclado)
    const string EJE_HORIZONTAL = "Horizontal";       //A/D + stick izquierdo (ya venia mapeado)
    const string EJE_VERTICAL = "Vertical";           //W/S + stick izquierdo (ya venia mapeado)

    //Ejes que leen SOLO el stick, sin el teclado. Existen para una sola cosa: saber que device
    //esta usando el jugador. Los Horizontal/Vertical de arriba MEZCLAN WASD con el stick, asi que
    //preguntarles "se movio el stick?" daba true al caminar con el teclado, y el juego se quedaba
    //convencido de que estabas con joystick (los textos mostraban botones en vez de teclas).
    const string EJE_DETECCION_X = "JoystickDetectX";
    const string EJE_DETECCION_Y = "JoystickDetectY";

    /// <summary>Cuanto hay que apretar L2 para que cuente como "apretado".</summary>
    const float UMBRAL_GATILLO = 0.5f;

    /// <summary>Por debajo de esto el gatillo se considera suelto (histeresis: evita repeticiones).</summary>
    const float UMBRAL_GATILLO_SUELTO = 0.3f;

    /// <summary>Zona muerta del stick para el cursor virtual del origami (drift de sticks gastados).</summary>
    public const float ZONA_MUERTA_STICK = 0.19f;

    // ---------------------------------------------------------------- acciones

    public static bool InteractDown => GetButtonDownSeguro(EJE_INTERACT);
    public static bool AtaqueTecladoDown => GetButtonDownSeguro(EJE_FIRE1);
    public static bool SaltoDown => GetButtonDownSeguro(EJE_JUMP);
    public static bool SaltoUp => GetButtonUpSeguro(EJE_JUMP);
    public static bool CorrerDown => GetButtonDownSeguro(EJE_RUN);
    public static bool CorrerUp => GetButtonUpSeguro(EJE_RUN);
    public static bool MuteDown => GetButtonDownSeguro(EJE_MUTE);
    public static bool OpcionesDown => GetButtonDownSeguro(EJE_OPCIONES);
    public static bool InventarioDown => GetButtonDownSeguro(EJE_INVENTARIO);
    public static bool QuestsDown => GetButtonDownSeguro(EJE_QUESTS);

    /// <summary>
    /// El boton A del joystick, solito. Es CONTEXTUAL: si hay algo con que interactuar interactua,
    /// y si no, salta (ver PlayerController). Tambien es el Submit de la UI y el agarre del origami.
    /// </summary>
    public static bool AccionGamepadDown => GetButtonDownSeguro(EJE_ACCION_GAMEPAD);

    /// <summary>El boton A mantenido apretado (lo usa el arrastre del origami).</summary>
    public static bool AccionGamepadHeld => GetButtonSeguro(EJE_ACCION_GAMEPAD);

    public static bool AccionGamepadUp => GetButtonUpSeguro(EJE_ACCION_GAMEPAD);

    /// <summary>El boton B del joystick: atacar. Sin contexto, siempre ataca.</summary>
    public static bool AtaqueGamepadDown => GetButtonDownSeguro(EJE_ATAQUE_GAMEPAD);

    /// <summary>Atacar con cualquier device: click/ctrl del teclado, o boton B del joystick.</summary>
    public static bool AtaqueDown => AtaqueTecladoDown || AtaqueGamepadDown;

    /// <summary>Cambiar de camara: click del medio, R1, o el gatillo L2.</summary>
    public static bool CambiarCamaraDown => GetButtonDownSeguro(EJE_CAMARA) || GatilloIzquierdoDown;

    /// <summary>Movimiento con el smoothing de Unity: para la fisica (aceleracion suave).</summary>
    public static Vector2 Movimiento =>
        new Vector2(GetAxisSeguro(EJE_HORIZONTAL), GetAxisSeguro(EJE_VERTICAL));

    /// <summary>Movimiento sin smoothing: para decidir estados (al soltar cae a 0 al instante).</summary>
    public static Vector2 MovimientoRaw =>
        new Vector2(GetAxisRawSeguro(EJE_HORIZONTAL), GetAxisRawSeguro(EJE_VERTICAL));

    /// <summary>Stick izquierdo / WASD, con la zona muerta ya aplicada. Para el cursor del origami.</summary>
    public static Vector2 StickIzquierdo
    {
        get
        {
            Vector2 raw = MovimientoRaw;
            return raw.magnitude < ZONA_MUERTA_STICK ? Vector2.zero : raw;
        }
    }

    // ------------------------------------------------- gatillo L2 (es un eje)

    // L2 no es un boton: es un eje analogico, asi que el "recien apretado" lo detectamos a mano
    // comparando contra el valor de la ultima vez que alguien pregunto. Cacheamos por frame para
    // que dos consultas en el mismo cuadro no se coman el flanco entre ellas.
    static bool _gatilloEstabaApretado = false;
    static int _frameGatillo = -1;
    static bool _gatilloDownEsteFrame = false;

    static bool GatilloIzquierdoDown
    {
        get
        {
            if (_frameGatillo == Time.frameCount)
            {
                return _gatilloDownEsteFrame;
            }

            _frameGatillo = Time.frameCount;

            float valor = Mathf.Abs(GetAxisRawSeguro(EJE_CAMARA_TRIGGER));
            if (!_gatilloEstabaApretado && valor >= UMBRAL_GATILLO)
            {
                _gatilloEstabaApretado = true;
                _gatilloDownEsteFrame = true;
            }
            else
            {
                if (_gatilloEstabaApretado && valor <= UMBRAL_GATILLO_SUELTO)
                {
                    _gatilloEstabaApretado = false;
                }
                _gatilloDownEsteFrame = false;
            }

            return _gatilloDownEsteFrame;
        }
    }

    // ------------------------------------------------------ deteccion de device

    static int _frameJoystick = -1;
    static bool _hayJoystickConectado = false;

    /// <summary>
    /// Cada cuantos frames se re-chequea si hay joystick enchufado. Input.GetJoystickNames()
    /// ALLOCA un string[] en cada llamada, asi que preguntarlo todos los frames es basura para
    /// el GC a cambio de nada: media docena de frames de latencia para notar que enchufaron un
    /// joystick no la percibe nadie.
    /// </summary>
    const int FRAMES_ENTRE_CHEQUEOS_DE_JOYSTICK = 30;

    /// <summary>Hay al menos un joystick enchufado. Se re-chequea cada tanto, no todos los frames.</summary>
    public static bool HayJoystickConectado
    {
        get
        {
            if (_frameJoystick >= 0 && Time.frameCount - _frameJoystick < FRAMES_ENTRE_CHEQUEOS_DE_JOYSTICK)
            {
                return _hayJoystickConectado;
            }
            _frameJoystick = Time.frameCount;

            _hayJoystickConectado = false;
            string[] nombres = Input.GetJoystickNames();
            for (int i = 0; i < nombres.Length; i++)
            {
                //Unity deja el slot con string vacio cuando el joystick se desconecto
                if (!string.IsNullOrEmpty(nombres[i]))
                {
                    _hayJoystickConectado = true;
                    break;
                }
            }
            return _hayJoystickConectado;
        }
    }

    static bool _ultimoDeviceFueJoystick = false;

    /// <summary>
    /// El ultimo input que hizo el jugador vino del joystick. Lo usan los prompts de UI y el
    /// cursor virtual del origami para decidir que mostrar. Teclado y joystick funcionan siempre
    /// los dos: esto es solo para la presentacion, nunca para habilitar/deshabilitar controles.
    /// </summary>
    public static bool UltimoDeviceFueJoystick
    {
        get
        {
            ActualizarUltimoDevice();
            return _ultimoDeviceFueJoystick;
        }
    }

    static int _frameDevice = -1;

    /// <summary>
    /// Salta cada vez que el jugador cambia de device (agarra el joystick, vuelve al teclado).
    /// Lo escuchan los textos de la UI, que tienen que reescribirse mostrando el control correcto:
    /// sin esto, un tooltip escrito con teclado se quedaba diciendo "E" para siempre.
    /// </summary>
    public static event System.Action OnDeviceCambio;

    static void ActualizarUltimoDevice()
    {
        if (_frameDevice == Time.frameCount)
        {
            return;
        }
        _frameDevice = Time.frameCount;

        bool antes = _ultimoDeviceFueJoystick;

        if (!HayJoystickConectado)
        {
            _ultimoDeviceFueJoystick = false;
        }
        else if (HuboInputDeJoystick())
        {
            //cualquier boton del joystick o movimiento de stick pasa el foco al joystick
            _ultimoDeviceFueJoystick = true;
        }
        else if (HuboInputDeTeclado())
        {
            //cualquier tecla o movimiento de mouse lo devuelve al teclado
            _ultimoDeviceFueJoystick = false;
        }

        if (antes != _ultimoDeviceFueJoystick && OnDeviceCambio != null)
        {
            Debug.Log($"[InputHub] cambio de device: ahora {(_ultimoDeviceFueJoystick ? "joystick" : "teclado/mouse")}");
            OnDeviceCambio();
        }
    }

    static bool HuboInputDeTeclado()
    {
        if (Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f)
        {
            return true;
        }

        //anyKeyDown incluye los botones del joystick, pero a este punto ya sabemos que el joystick
        //no toco nada este frame (lo chequeo el else-if de arriba), asi que cualquier tecla es real
        return Input.anyKeyDown;
    }

    static bool HuboInputDeJoystick()
    {
        //los KeyCode JoystickButton0..19 cubren los botones de cualquier joystick conectado
        for (int i = (int)KeyCode.JoystickButton0; i <= (int)KeyCode.JoystickButton19; i++)
        {
            if (Input.GetKey((KeyCode)i))
            {
                return true;
            }
        }

        //ojo: aca van los ejes de deteccion (solo stick), NO los Horizontal/Vertical, que
        //tambien devuelven valor cuando el jugador camina con WASD
        return Mathf.Abs(GetAxisRawSeguro(EJE_DETECCION_X)) >= ZONA_MUERTA_STICK
            || Mathf.Abs(GetAxisRawSeguro(EJE_DETECCION_Y)) >= ZONA_MUERTA_STICK
            || Mathf.Abs(GetAxisRawSeguro(EJE_CAMARA_TRIGGER)) >= UMBRAL_GATILLO;
    }

    // ------------------------------------------------------------- envoltorios

    // Input.GetButton* y GetAxis* TIRAN EXCEPCION si el eje no existe en InputManager.asset.
    // Si alguien abre el proyecto con un ProjectSettings viejo (o se pierde el merge de ese
    // archivo), no queremos que el juego reviente cada frame: avisamos una sola vez por eje
    // y seguimos como si el eje no se hubiera apretado. El teclado sigue andando igual.
    static readonly System.Collections.Generic.HashSet<string> _ejesRotos =
        new System.Collections.Generic.HashSet<string>();

    static bool EjeRoto(string eje)
    {
        return _ejesRotos.Contains(eje);
    }

    static void MarcarEjeRoto(string eje)
    {
        if (_ejesRotos.Add(eje))
        {
            Debug.LogWarning($"[InputHub] el eje '{eje}' no existe en ProjectSettings/InputManager.asset: " +
                             "ignoro esa accion. Si es del joystick, revisa que el InputManager este actualizado.");
        }
    }

    static bool GetButtonDownSeguro(string eje)
    {
        if (EjeRoto(eje)) { return false; }
        try { return Input.GetButtonDown(eje); }
        catch (System.ArgumentException) { MarcarEjeRoto(eje); return false; }
    }

    static bool GetButtonUpSeguro(string eje)
    {
        if (EjeRoto(eje)) { return false; }
        try { return Input.GetButtonUp(eje); }
        catch (System.ArgumentException) { MarcarEjeRoto(eje); return false; }
    }

    static bool GetButtonSeguro(string eje)
    {
        if (EjeRoto(eje)) { return false; }
        try { return Input.GetButton(eje); }
        catch (System.ArgumentException) { MarcarEjeRoto(eje); return false; }
    }

    static float GetAxisSeguro(string eje)
    {
        if (EjeRoto(eje)) { return 0f; }
        try { return Input.GetAxis(eje); }
        catch (System.ArgumentException) { MarcarEjeRoto(eje); return 0f; }
    }

    static float GetAxisRawSeguro(string eje)
    {
        if (EjeRoto(eje)) { return 0f; }
        try { return Input.GetAxisRaw(eje); }
        catch (System.ArgumentException) { MarcarEjeRoto(eje); return 0f; }
    }
}
