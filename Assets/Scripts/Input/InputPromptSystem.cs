using System.Text.RegularExpressions;

/// <summary>
/// Traduce los textos que le mostramos al jugador para que digan el control REAL del
/// device que esta usando. Con un joystick en la mano, "Apreta E para hablar" es
/// informacion falsa: no hay tecla E.
///
/// Es una clase ESTATICA a proposito: la usa LocalizedText (el unico lugar del juego que
/// escribe texto localizado en un TMP), y no puede depender de que alguien se acuerde de
/// poner un GameObject en cada escena.
///
/// Tiene dos caminos, y conviene entender por que existen los dos:
///
///  1) PLACEHOLDERS {INPUT:*} - el camino limpio, para texto NUEVO. La tabla de
///     localizacion escribe "{INPUT:accion} para hablar" y aca se resuelve al prompt
///     del device activo. Funciona con teclado Y con joystick.
///
///  2) TOKENS LEGACY - las tablas de hoy NO tienen placeholders: tienen las teclas
///     escritas a mano ("Toca E", "Manten SHIFT", "Usa Click"). Reescribirlas es
///     contenido de Diego/Valentino, no nuestro. Entonces, y SOLO cuando el jugador
///     esta usando joystick, tambien traducimos un set chico y cerrado de tokens.
///     Con teclado el texto sale byte por byte igual que siempre.
///
/// El camino 2 es deuda tecnica consciente: se puede borrar entero el dia que las
/// tablas migren a placeholders.
/// </summary>
public static class InputPromptSystem
{
    // ------------------------------------------------------------- acciones

    /// <summary>Las acciones que tienen prompt. Es el vocabulario de {INPUT:*}.</summary>
    enum Accion
    {
        Saltar,
        Interactuar, //hablar, agarrar, pasar de pagina, avanzar el dialogo
        Atacar,      //cortar con la tijera
        Correr,
        Camara,
        Menu,
        Mover,
        CambiarTab   //L1/R1: ciclar secciones del Flap (issue #41 ronda 2, punto 3). SOLO joystick: estos ejes no tienen binding de teclado (ver InputHub.TabSiguienteDown/TabAnteriorDown), asi que el cartelito que usa este placeholder tiene que estar oculto con teclado (ver SoloConJoystick)
    }

    // -------------------------------------------------- iconos

    /// <summary>
    /// Kill switch de los iconos. En false, todo el sistema vuelve EXACTAMENTE al
    /// comportamiento de texto que tenia antes ("(A)", "E", "Shift"...). Esta aca para que
    /// si los iconos se ven mal en algun texto se pueda apagar todo desde un solo lugar,
    /// sin revertir codigo.
    /// </summary>
    public static bool UsarIconos = true;

    // Los ids se los pasamos a IconosDeBoton, que arma el tag <sprite name="..."> y el atlas.
    // Si el atlas fallara (shader que no aparece, textura que no se pudo crear), Tag()
    // devuelve vacio y CAEMOS SOLOS al texto de siempre: por eso cada prompt de abajo
    // pregunta primero por el icono y despues devuelve su string historico.
    static string IconoTeclado(Accion accion)
    {
        switch (accion)
        {
            case Accion.Saltar: return IconosDeBoton.Tag("espacio");
            case Accion.Interactuar: return IconosDeBoton.Tag("e");
            case Accion.Atacar: return IconosDeBoton.Tag("mouse_izq");
            case Accion.Correr: return IconosDeBoton.Tag("shift");
            case Accion.Camara: return IconosDeBoton.Tag("mouse_medio");
            case Accion.Menu: return IconosDeBoton.Tag("esc");
            case Accion.Mover: return IconosDeBoton.Tag("wasd");
            default: return "";
        }
    }

    static string IconoJoystick(Accion accion)
    {
        switch (accion)
        {
            case Accion.Saltar: return IconosDeBoton.Tag("a");
            case Accion.Interactuar: return IconosDeBoton.Tag("a");
            case Accion.Atacar: return IconosDeBoton.Tag("b");
            case Accion.Correr: return IconosDeBoton.Tag("l1");
            case Accion.Camara: return IconosDeBoton.Tag("l2");
            case Accion.Menu: return IconosDeBoton.Tag("start");
            case Accion.Mover: return IconosDeBoton.Tag("stick");

            //los dos bumpers juntos: es un solo "control" conceptual (ciclar secciones),
            //asi que se muestran los dos iconos separados por la barra, igual que el texto
            case Accion.CambiarTab:
            {
                string l1 = IconosDeBoton.Tag("l1");
                string r1 = IconosDeBoton.Tag("r1");
                if (string.IsNullOrEmpty(l1) || string.IsNullOrEmpty(r1))
                {
                    return "";
                }
                return l1 + " / " + r1;
            }

            default: return "";
        }
    }

    // -------------------------------------------------- tablas de prompts

    // TABLA DE TECLADO. El texto de abajo es el historico y sigue siendo la red de
    // seguridad: con UsarIconos en false, o si el atlas no se pudo construir, el jugador ve
    // exactamente lo de siempre.
    static string PromptTeclado(Accion accion)
    {
        if (UsarIconos)
        {
            string icono = IconoTeclado(accion);
            if (!string.IsNullOrEmpty(icono))
            {
                return icono;
            }
        }

        switch (accion)
        {
            case Accion.Saltar: return "Espacio";
            case Accion.Interactuar: return "E";
            case Accion.Atacar: return "Click";
            case Accion.Correr: return "Shift";
            case Accion.Camara: return "Click del medio";
            case Accion.Menu: return "Esc";
            case Accion.Mover: return "WASD";
            //no hay tecla para esto: cambiar de tab del Flap con teclado se hace con mouse
            //(click directo en el icono de la seccion), no hay eje dedicado. Nunca deberia
            //verse: el cartel que usa este placeholder esta gateado a SoloConJoystick.
            case Accion.CambiarTab: return "";
            default: return "";
        }
    }

    // TABLA DE JOYSTICK.
    //
    // MAPEO VIGENTE (cambio en septiembre 2026, antes era al reves): el boton A es
    // CONTEXTUAL y hace las dos cosas que el teclado separa en E y Espacio -- si hay algo
    // con que interactuar interactua, y si no, salta (ver InteractionContext). El B queda
    // solo para atacar, que en teclado es el click / CTRL.
    static string PromptJoystick(Accion accion)
    {
        if (UsarIconos)
        {
            string icono = IconoJoystick(accion);
            if (!string.IsNullOrEmpty(icono))
            {
                return icono;
            }
        }

        switch (accion)
        {
            case Accion.Saltar: return "(A)";
            case Accion.Interactuar: return "(A)";
            case Accion.Atacar: return "(B)";
            case Accion.Correr: return "(L1)";
            case Accion.Camara: return "(L2)";
            case Accion.Menu: return "(Start)";
            case Accion.Mover: return "stick";
            case Accion.CambiarTab: return "L1 / R1";
            default: return "";
        }
    }

    static string PromptDe(Accion accion)
    {
        return InputHub.UltimoDeviceFueJoystick ? PromptJoystick(accion) : PromptTeclado(accion);
    }

    // ------------------------------------------------------------- API

    /// <summary>
    /// Devuelve el texto listo para mostrarle al jugador. Es seguro llamarlo con
    /// cualquier string: si no hay nada que traducir devuelve el mismo string, sin allocar.
    /// </summary>
    public static string Procesar(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return texto;
        }

        bool joystick = InputHub.UltimoDeviceFueJoystick;

        // CAMINO RAPIDO. Sin '{' no hay placeholders; con teclado Y SIN iconos tampoco hay
        // nada que hacer, asi que se devuelve LA MISMA instancia.
        //
        // OJO, esto cambio en la tanda de iconos (septiembre 2026): ANTES los tokens legacy
        // corrian solo con joystick, y la regla de oro era "con teclado el texto sale
        // identico a hoy". Ahora tambien corren con teclado, PORQUE ES EL PUNTO: es lo que
        // convierte el "E" y el "ESPACIO" escritos a mano en las tablas en iconos de tecla.
        // La red de seguridad es UsarIconos: en false volvemos exactamente al comportamiento
        // viejo, teclado incluido.
        bool puedeTenerPlaceholder = texto.IndexOf('{') >= 0;
        if (!puedeTenerPlaceholder && !joystick && !UsarIconos)
        {
            return texto;
        }

        string resultado = texto;

        if (puedeTenerPlaceholder)
        {
            resultado = RxPlaceholder.Replace(resultado, ResolverPlaceholder);
        }

        if (joystick || UsarIconos)
        {
            resultado = TraducirTokensLegacy(resultado, joystick);
        }

        return resultado;
    }

    // ------------------------------------------------- 1) placeholders {INPUT:*}

    // Los nombres se aceptan en espanol Y en ingles, y sin distinguir mayusculas, porque
    // las tablas de localizacion las escriben personas (y en tres idiomas).
    static readonly Regex RxPlaceholder = new Regex(
        @"\{INPUT:\s*(?<accion>[a-zA-Z_]+)\s*\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static string ResolverPlaceholder(Match m)
    {
        string nombre = m.Groups["accion"].Value.ToLowerInvariant();

        switch (nombre)
        {
            case "saltar":
            case "jump":
                return PromptDe(Accion.Saltar);

            // "accion" es el A contextual del joystick: interactua si hay algo cerca y si
            // no salta. En teclado no se puede resolver desde aca sin saber si el jugador
            // tiene algo al lado, asi que damos el prompt de interactuar ("E"), que es el
            // caso de casi todos los tooltips.
            case "accion":
            case "action":
            case "interactuar":
            case "interact":
                return PromptDe(Accion.Interactuar);

            case "atacar":
            case "attack":
            case "cortar":
            case "cut":
                return PromptDe(Accion.Atacar);

            case "correr":
            case "run":
            case "sprint":
                return PromptDe(Accion.Correr);

            case "camara":
            case "camera":
                return PromptDe(Accion.Camara);

            case "menu":
            case "opciones":
            case "options":
                return PromptDe(Accion.Menu);

            case "mover":
            case "move":
            case "movimiento":
                return PromptDe(Accion.Mover);

            case "cambiartab":
            case "cambiarseccion":
            case "changetab":
            case "switchtab":
                return PromptDe(Accion.CambiarTab);

            default:
                //placeholder mal escrito en la tabla: lo dejamos crudo A PROPOSITO, asi se
                //ve en pantalla y alguien lo arregla. Comerselo en silencio seria peor.
                UnityEngine.Debug.LogWarning(
                    $"[InputPromptSystem] no conozco el placeholder '{m.Value}': lo dejo tal cual");
                return m.Value;
        }
    }

    // --------------------------------------------- 2) tokens legacy (solo joystick)

    // REGLAS DURAS de esta parte, para que no se vuelva un desastre:
    //  - Solo corre con joystick (lo garantiza el 'if (joystick)' de Procesar).
    //  - Coincidencia por PALABRA COMPLETA (\b), preservando todo lo de alrededor.
    //  - Lista chica y cerrada. Si un patron no matchea exacto, el texto queda como esta:
    //    un texto viejo es mucho mejor que un texto roto.
    //  - Regex compiladas una sola vez: esto corre por cada tooltip y cada linea de dialogo.

    // Los multi-palabra van PRIMERO: si "Click" se reemplazara antes, "Middle Click" o
    // "Click Central" quedarian como "Middle (B)" / "(B) Central".
    static readonly Regex RxCamaraMultiPalabra = new Regex(
        @"\b(?:middle\s+click|click\s+del\s+medio|click\s+central|clique\s+do\s+meio)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "Click / CTRL" (la pantalla de controles lista las DOS formas de atacar del teclado)
    // colapsa a un solo boton: sin esta regla quedaba "(B) / (B) - Cortar".
    static readonly Regex RxClickOCtrl = new Regex(
        @"\b(?:clicks?|clics?|cliques?)\s*/\s*ctrl\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex RxClick = new Regex(
        @"\b(?:clicks?|clics?|cliques?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex RxCtrl = new Regex(@"\bctrl\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex RxShift = new Regex(@"\bshift\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ESPACIO va SIN IgnoreCase, y no es un olvido: en portugues "espaço" es una palabra
    // comun ("guarde espaço para a sobremesa" = "guarda lugar para el postre"). En MAYUSCULA
    // siempre es el nombre de la tecla; en minuscula casi nunca lo es.
    // El "a" opcional de adelante es SOLO para la variante portuguesa "a BARRA DE ESPACO":
    // ese articulo es de "barra", asi que sin comerlo quedaria "Pule usando a (A)".
    static readonly Regex RxEspacio = new Regex(
        @"\b(?:(?:a\s+)?BARRA\s+DE\s+ESPA\u00C7O|ESPACIO|ESPA\u00C7O|SPACE)\b",
        RegexOptions.Compiled);

    static readonly Regex RxEsc = new Regex(@"\besc\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex RxWasd = new Regex(@"\bwasd\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // LAS LETRAS SUELTAS SON EL CASO PELIGROSO. En espanol "e" es una conjuncion y aparece
    // adentro de mil frases, asi que NO existe ningun reemplazo de "\bE\b" a secas: solo
    // traducimos la letra cuando viene pegada a un VERBO DE INSTRUCCION explicito
    // ("Toca E", "Press E", "Pressiona a tecla E"). Ademas la letra se matchea en
    // MAYUSCULA (el (?i:) envuelve solo al verbo): "toca e cantar" no matchea.
    static readonly Regex RxTeclaConVerbo = new Regex(
        @"\b(?<verbo>(?i:" +
            // espanol
            @"Tocar|Toc\u00E1|Toca|Apretar|Apret\u00E1|Apreta|Presionar|Presion\u00E1|Presiona|" +
            @"Mantener|Manten\u00E9|Manten|Usar|Us\u00E1|Usa|" +
            // ingles
            @"Press|Hold\s+down|Hold|Use|Tap|" +
            // portugues
            @"Pressionar|Pressione|Pressiona|Toque|Apertar|Aperte|Aperta|Segure" +
        @"))" +
        // relleno opcional: "a tecla", "la tecla", "the key"...
        @"(?i:\s+(?:a|la|el|o|the)\s+(?:tecla|key)|\s+(?:tecla|key))?" +
        @"\s+(?<letra>[EUI])\b",
        RegexOptions.Compiled);

    // Caso aparte: la entrada que arranca DIRECTO con la letra ("E para avanzar",
    // "E to advance"). Anclado al principio del string y con "para"/"to" pegado atras,
    // que es lo unico que lo hace seguro.
    static readonly Regex RxTeclaAlInicio = new Regex(
        @"^E(?=\s+(?:para|to)\b)",
        RegexOptions.Compiled);

    // La pantalla de controles es una lista "TECLA - que hace" con una linea por control.
    // Ahi la letra sola SI es la tecla, y se reconoce por el guion que viene atras. Va
    // anclada al arranque de RENGLON (Multiline) y con el guion como testigo obligatorio:
    // sin las dos cosas esto se comeria cualquier "e" suelta del castellano.
    // Se amplio de [EUI] a [EUIOM] en la tanda de iconos (septiembre 2026): las lineas
    // "O - Abrir Controles" y "M - Control de sonido" eran el gap conocido de esta regex
    // (estaba anotado en docs/claude/controles-y-gamepad.md) y quedaban mostrando la letra
    // pelada. Con iconos ahora se dibujan como keycap igual que el resto de la lista.
    static readonly Regex RxTeclaEnListaDeControles = new Regex(
        @"(?m)^(?<letra>[EUIOM])(?=\s*[-–—]\s)",
        RegexOptions.Compiled);

    // El tooltip de "arrastrar" del origami (Origami.tooltipMessage, mostrado por
    // MultipleRectCheck.StartOrigami al arrancar el minijuego -- issue #41.5) no menciona
    // ninguna tecla: con mouse alcanza con el verbo solo, porque el click que agarra la
    // flecha y el arrastre son el mismo gesto. Con joystick NO: hay que apretar el boton de
    // accion SOBRE la flecha antes de poder arrastrarla con el stick (ver
    // InputHub.AccionGamepadDown en MultipleRectCheck.PunteroDown), y sin avisarlo el
    // jugador no tiene forma de saber que boton usar.
    // Anclado al inicio del string, mismo patron que RxTeclaAlInicio. Cubre los 3 idiomas de
    // la tabla (TooltipTable, clave origami_guide: "Arrastra la flecha..." / "Drag the green
    // arrow..." / "Arraste a seta...") porque las tres arrancan con el verbo arrastrar/drag.
    // Tambien cubre el texto hardcodeado (no la clave) que usan hoy la mayoria de los
    // OrigamiRoute.prefab: arranca igual, asi que el patron los alcanza sin tocar prefabs.
    static readonly Regex RxArrastrarAlInicio = new Regex(
        @"^(?<verbo>Arrastrá|Arrastra|Drag|Arraste)\b",
        RegexOptions.Compiled);

    //Que device se esta resolviendo en ESTA pasada. Es un campo y no un parametro porque los
    //MatchEvaluator de abajo (ReemplazarTeclaDeLista, ReemplazarTeclaConVerbo) tienen firma
    //fija y necesitan saberlo. Unity corre todo esto en el hilo principal y la pasada es
    //sincronica de punta a punta, asi que no hay carrera posible.
    static bool _joystickEnCurso;

    static string TraducirTokensLegacy(string texto, bool joystick)
    {
        _joystickEnCurso = joystick;

        //Regex.Replace devuelve el MISMO string cuando no hay match, asi que un texto sin
        //tokens (la mayoria de los dialogos) no allocan nada aca.
        string r = texto;

        r = RxCamaraMultiPalabra.Replace(r, PromptDe(Accion.Camara));
        r = RxClickOCtrl.Replace(r, PromptDe(Accion.Atacar));
        r = RxClick.Replace(r, PromptDe(Accion.Atacar));
        r = RxCtrl.Replace(r, PromptDe(Accion.Atacar));
        r = RxShift.Replace(r, PromptDe(Accion.Correr));
        r = RxEspacio.Replace(r, PromptDe(Accion.Saltar));
        r = RxEsc.Replace(r, PromptDe(Accion.Menu));
        //"stick izquierdo" (y no el "stick" pelado de la tabla de prompts) es la redaccion
        //historica de este reemplazo puntual: se conserva tal cual para cuando no hay iconos.
        r = RxWasd.Replace(r, joystick
            ? IconoOTexto(true, Accion.Mover, "stick izquierdo")
            : IconoOTexto(false, Accion.Mover, "WASD"));

        r = RxTeclaConVerbo.Replace(r, ReemplazarTeclaConVerbo);
        r = RxTeclaAlInicio.Replace(r, PromptDe(Accion.Interactuar));
        r = RxTeclaEnListaDeControles.Replace(r, ReemplazarTeclaDeLista);

        //ESTA se queda SOLO con joystick, a diferencia de todas las de arriba: no traduce un
        //boton, agrega la explicacion de que con joystick hay que APRETAR algo antes de poder
        //arrastrar (con mouse el click y el arrastre son el mismo gesto y la frase original ya
        //esta bien). Con teclado/mouse meter esto seria empeorar un texto que ya funciona.
        if (joystick)
        {
            r = RxArrastrarAlInicio.Replace(r, ReemplazarArrastrarAlInicio);
        }

        return r;
    }

    //Para los reemplazos donde el texto de reserva NO es el de la tabla de prompts sino una
    //redaccion propia de ese reemplazo puntual: si hay icono lo usa, y si no deja la frase
    //historica intacta.
    static string IconoOTexto(bool joystick, Accion accion, string textoOriginal)
    {
        if (!UsarIconos)
        {
            return textoOriginal;
        }

        string icono = joystick ? IconoJoystick(accion) : IconoTeclado(accion);
        return string.IsNullOrEmpty(icono) ? textoOriginal : icono;
    }

    // Con joystick anteponemos "Toca (A) y..."/"Press (A) and..."/"Toca (A) e..." al verbo de
    // arrastrar, en vez de traducir el verbo en si (no hay un boton que "sea" arrastrar: el
    // arrastre lo sigue haciendo el stick, lo que faltaba explicar es CON QUE boton se agarra).
    static string ReemplazarArrastrarAlInicio(Match m)
    {
        string boton = PromptJoystick(Accion.Interactuar);

        switch (m.Groups["verbo"].Value)
        {
            case "Drag":
                return "Press " + boton + " and drag";
            case "Arraste":
                return "Toca " + boton + " e arraste";
            default: //"Arrastrá" o "Arrastra" (espanol)
                return "Tocá " + boton + " y arrastrá";
        }
    }

    static string ReemplazarTeclaDeLista(Match m)
    {
        return PromptDeLetra(m.Groups["letra"].Value);
    }

    static string ReemplazarTeclaConVerbo(Match m)
    {
        //conservamos el verbo tal cual lo escribio la traduccion y tiramos el relleno
        //("a tecla"), que con un boton de joystick ya no tiene sentido.
        return m.Groups["verbo"].Value + " " + PromptDeLetra(m.Groups["letra"].Value);
    }

    static string PromptDeLetra(string letra)
    {
        //CON TECLADO la letra sigue siendo la letra: no se traduce nada, solo se dibuja como
        //keycap. Este camino no existia antes de los iconos (los tokens legacy corrian solo
        //con joystick), y es el que hace que la pantalla de Controles se vea con teclitas.
        if (!_joystickEnCurso)
        {
            switch (letra)
            {
                case "E": return TeclaSuelta("e", "E");
                case "U": return TeclaSuelta("u", "U");
                case "I": return TeclaSuelta("i", "I");
                case "O": return TeclaSuelta("o", "O");
                case "M": return TeclaSuelta("m", "M");
                default: return letra;
            }
        }

        switch (letra)
        {
            case "E":
                return PromptJoystick(Accion.Interactuar);

            // U (Tareas), I (Morral) y O (Controles) no tienen boton propio en el joystick:
            // se llega a las tres por el menu con Start. Mandar al jugador al menu es lo mas
            // honesto que podemos decirle.
            case "U":
            case "I":
            case "O":
                return PromptJoystick(Accion.Menu);

            // Mutear NO tiene equivalente en joystick, y es a proposito (no hay binding).
            // Seguimos mostrando la tecla aunque el jugador este con joystick: es un control
            // de teclado, y decirle "(Start)" seria mentirle.
            case "M":
                return TeclaSuelta("m", "M");

            default:
                return letra;
        }
    }

    //Una tecla que se muestra como si misma: con iconos sale el keycap dibujado, sin iconos
    //sale la letra de siempre.
    static string TeclaSuelta(string idIcono, string textoOriginal)
    {
        if (!UsarIconos)
        {
            return textoOriginal;
        }

        string icono = IconosDeBoton.Tag(idIcono);
        return string.IsNullOrEmpty(icono) ? textoOriginal : icono;
    }
}
