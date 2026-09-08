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
        Mover
    }

    // -------------------------------------------------- tablas de prompts

    // TABLA DE TECLADO. Lo de siempre, y no se toca: si esto cambia, cambia la
    // experiencia que ya funciona.
    static string PromptTeclado(Accion accion)
    {
        switch (accion)
        {
            case Accion.Saltar: return "Espacio";
            case Accion.Interactuar: return "E";
            case Accion.Atacar: return "Click";
            case Accion.Correr: return "Shift";
            case Accion.Camara: return "Click del medio";
            case Accion.Menu: return "Esc";
            case Accion.Mover: return "WASD";
            default: return "";
        }
    }

    // TABLA DE JOYSTICK. ESTA es la unica tabla que hay que tocar para pasar a iconos.
    //
    // Hoy devuelve TEXTO ("(A)", "(B)"...) y no iconos de TextMeshPro porque TODAVIA NO
    // EXISTE EL ATLAS DE SPRITES DE BOTONES: es dependencia de arte (Valentino). Cuando
    // exista, se cambia SOLO este switch por los tags de TMP y todo el resto del sistema
    // (placeholders, tokens legacy, los dos enganches de UI) sigue igual:
    //
    //     case Accion.Saltar:      return "<sprite name=button_A>";
    //     case Accion.Interactuar: return "<sprite name=button_A>";
    //     case Accion.Atacar:      return "<sprite name=button_B>";
    //     case Accion.Correr:      return "<sprite name=button_L1>";
    //     case Accion.Camara:      return "<sprite name=button_L2>";
    //     case Accion.Menu:        return "<sprite name=button_Start>";
    //     case Accion.Mover:       return "<sprite name=stick_left>";
    //
    // Ojo cuando llegue ese dia: el sprite asset tiene que estar en el fallback global de
    // TMP (o en la fuente de cada TMP que muestre prompts), si no se ve el tag crudo.
    //
    // MAPEO VIGENTE (cambio en septiembre 2026, antes era al reves): el boton A es
    // CONTEXTUAL y hace las dos cosas que el teclado separa en E y Espacio -- si hay algo
    // con que interactuar interactua, y si no, salta (ver InteractionContext). El B queda
    // solo para atacar, que en teclado es el click / CTRL.
    static string PromptJoystick(Accion accion)
    {
        switch (accion)
        {
            case Accion.Saltar: return "(A)";
            case Accion.Interactuar: return "(A)";
            case Accion.Atacar: return "(B)";
            case Accion.Correr: return "(L1)";
            case Accion.Camara: return "(L2)";
            case Accion.Menu: return "(Start)";
            case Accion.Mover: return "stick";
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

        // CAMINO RAPIDO. Sin '{' no hay placeholders, y con teclado no hay nada mas que
        // hacer: se devuelve LA MISMA instancia. Esta linea es tambien la garantia dura de
        // que con teclado el texto nunca cambia salvo por placeholders explicitos.
        bool puedeTenerPlaceholder = texto.IndexOf('{') >= 0;
        if (!puedeTenerPlaceholder && !joystick)
        {
            return texto;
        }

        string resultado = texto;

        if (puedeTenerPlaceholder)
        {
            resultado = RxPlaceholder.Replace(resultado, ResolverPlaceholder);
        }

        if (joystick)
        {
            resultado = TraducirTokensLegacy(resultado);
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
    static readonly Regex RxTeclaEnListaDeControles = new Regex(
        @"(?m)^(?<letra>[EUI])(?=\s*[-–—]\s)",
        RegexOptions.Compiled);

    static string TraducirTokensLegacy(string texto)
    {
        //Regex.Replace devuelve el MISMO string cuando no hay match, asi que un texto sin
        //tokens (la mayoria de los dialogos) no allocan nada aca.
        string r = texto;

        r = RxCamaraMultiPalabra.Replace(r, PromptJoystick(Accion.Camara));
        r = RxClickOCtrl.Replace(r, PromptJoystick(Accion.Atacar));
        r = RxClick.Replace(r, PromptJoystick(Accion.Atacar));
        r = RxCtrl.Replace(r, PromptJoystick(Accion.Atacar));
        r = RxShift.Replace(r, PromptJoystick(Accion.Correr));
        r = RxEspacio.Replace(r, PromptJoystick(Accion.Saltar));
        r = RxEsc.Replace(r, PromptJoystick(Accion.Menu));
        r = RxWasd.Replace(r, "stick izquierdo");

        r = RxTeclaConVerbo.Replace(r, ReemplazarTeclaConVerbo);
        r = RxTeclaAlInicio.Replace(r, PromptJoystick(Accion.Interactuar));
        r = RxTeclaEnListaDeControles.Replace(r, ReemplazarTeclaDeLista);

        return r;
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
        switch (letra)
        {
            case "E":
                return PromptJoystick(Accion.Interactuar);

            // U (Tareas) e I (Morral) no tienen boton propio en el joystick: se llega a las
            // dos por el menu con Start. Mandar al jugador al menu es lo mas honesto que
            // podemos decirle.
            case "U":
            case "I":
                return PromptJoystick(Accion.Menu);

            default:
                return letra;
        }
    }
}
