using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Resalta las PALABRAS DE MECANICA en cualquier texto que le mostremos al jugador, con
/// negrita + un color por categoria (y escala en las dos categorias mas importantes).
///
/// Por que existe: Kami la juegan chicos, y la mayoria de lo que tienen que aprender
/// ("cortar", "doblar", "papel", "agua") esta escrito en medio de un parrafo, con el mismo
/// peso visual que el resto. Un codigo de colores CONSTANTE en todo el juego hace que la
/// mecanica se aprenda leyendo, sin tener que explicarla aparte.
///
/// Es una clase ESTATICA y sin dependencias del proyecto (solo UnityEngine para el Debug),
/// por el mismo motivo que <see cref="InputPromptSystem"/>: la llama LocalizedText, que es
/// el unico embudo de texto del juego, y no puede depender de que alguien se acuerde de
/// poner un GameObject en cada escena.
///
/// ORDEN DE EJECUCION (importante): esto corre ANTES que InputPromptSystem, sobre el mismo
/// string. Ver la seccion "vocabulario prohibido" mas abajo para entender por que eso
/// condiciona que palabras pueden entrar en la tabla.
/// </summary>
public static class ResaltadorDeConceptos
{
    // ------------------------------------------------------------------- API

    /// <summary>
    /// Kill switch. Si algo se ve mal en pantalla (una linea que quedo ilegible, un idioma
    /// nuevo sin revisar), se apaga desde codigo y todo el texto vuelve a salir plano, sin
    /// tocar tablas ni prefabs.
    /// </summary>
    public static bool Activo { get; set; } = true;

    /// <summary>
    /// Devuelve el texto con las palabras clave envueltas en tags de TMP. Es seguro
    /// llamarlo con cualquier string: si no hay nada que resaltar devuelve LA MISMA
    /// instancia, sin allocar (Regex.Replace ya se comporta asi cuando no hay match).
    /// </summary>
    public static string Resaltar(string texto)
    {
        if (!Activo || string.IsNullOrEmpty(texto))
        {
            return texto;
        }

        return _rx.Replace(texto, Envolver);
    }

    // -------------------------------------------------------------- categorias

    /// <summary>
    /// El codigo de colores del juego. Es un contrato con el jugador: el mismo concepto
    /// tiene que salir SIEMPRE del mismo color, en los tres idiomas y en todas las
    /// pantallas. Por eso el color vive aca y no en cada tabla de localizacion.
    /// </summary>
    enum Categoria
    {
        Corte,      //la tijera y todo lo que se corta
        Origami,    //doblar, plegar, el minijuego
        Recurso,    //lo que se junta y se gasta (inventario)
        Peligro,    //lo que mata o hace dano
        Movimiento  //como se desplaza Kami
    }

    static string ColorDe(Categoria categoria)
    {
        switch (categoria)
        {
            case Categoria.Corte: return "#D6453D";
            case Categoria.Origami: return "#2D7DD2";
            case Categoria.Recurso: return "#3E9B4F";

            //ambar OSCURO a proposito: los post-its del tutorial son amarillos, y un ambar
            //claro sobre amarillo no se lee. El color se eligio contra ese fondo, no contra
            //el fondo negro de los dialogos.
            case Categoria.Peligro: return "#B5651D";

            case Categoria.Movimiento: return "#8155BA";
            default: return "#FFFFFF";
        }
    }

    /// <summary>
    /// Solo Corte y Origami crecen. Son las DOS mecanicas que definen el juego (es "Kami:
    /// Papel y Tijera"): si agrandamos las cinco categorias, agrandar deja de significar
    /// nada y las frases con muchos sustantivos quedan con los renglones desparejos.
    /// </summary>
    static bool CreceDe(Categoria categoria)
    {
        return categoria == Categoria.Corte || categoria == Categoria.Origami;
    }

    // ------------------------------------------------------------- vocabulario

    // DE DONDE SALEN ESTAS PALABRAS: se barrieron las tablas reales de
    // Assets/Localization Settings/Tables/ en los tres idiomas -- DialogueTable_es/en/pt,
    // TooltipTable_es/en/pt, UITexts_es/en/pt, ItemTable_es/en/pt y QuestTable_es/en/pt --
    // y se anotaron SOLO las formas que aparecen escritas de verdad, conjugaciones
    // incluidas ("cortá" del voseo, "cortás", "corte" del portugues). No hay ninguna
    // palabra inventada "por las dudas": una palabra que no esta en ninguna tabla no
    // resalta nada y solo agranda la regex.
    //
    // VOCABULARIO PROHIBIDO (esto es una regla, no una preferencia): NINGUNA palabra de
    // input puede entrar aca -- E, U, I, O, M, WASD, click/clic/clique, shift, ctrl, esc,
    // espacio/espaço/space, barra, tecla/key, stick, A, B, L1, L2, start -- ni los VERBOS
    // ni el relleno que InputPromptSystem usa como testigo para reconocerlas
    // (Tocá/Apretá/Mantené/Usá/Press/Hold/Use/Tap/Pressione/Segure, "la tecla", "para",
    // "to"), ni los verbos anclados al INICIO del string (Arrastrá/Arrastra/Drag/Arraste,
    // que RxArrastrarAlInicio matchea con "^"). Motivo: InputPromptSystem corre DESPUES
    // sobre este mismo string con sus propias regex; si nosotros envolvemos una de esas
    // palabras en tags primero, su regex deja de matchear y el jugador con joystick vuelve
    // a leer "Tocá E" con un joystick en la mano. Los anclados a "^" se rompen incluso sin
    // tocarlos a ellos: alcanza con meter un "<b>" adelante.
    static readonly Dictionary<string, Categoria> _porPalabra =
        new Dictionary<string, Categoria>(StringComparer.OrdinalIgnoreCase);

    static readonly Regex _rx;

    static ResaltadorDeConceptos()
    {
        // --- CORTE ------------------------------------------------------------
        // es: "Cortá 3 flores", "por qué no cortás la represa", "necesito que cortes ese
        // árbol". pt: "Corte 3 flores roxas", "você não corta a represa". en: "cut".
        Agregar(Categoria.Corte,
            "cortar", "cortá", "cortás", "cortes", "corte", "corta",
            "tijera", "tijeras",
            "cut", "scissors",
            "tesoura", "tesouras");

        // --- ORIGAMI ----------------------------------------------------------
        // "pliegue" sale del contador del minijuego (UITexts, clave del PliegueTextUpdater);
        // "Fold: " / "Dobrar:" son ese mismo contador en en/pt.
        Agregar(Categoria.Origami,
            "origami", "origamis",
            "doblar", "doblada", "doblarías", "desdoblar", "pliegue",
            "fold", "folding", "unfold",
            "dobrar", "desdobrar");

        // --- RECURSO ----------------------------------------------------------
        // Son los sustantivos del inventario. "flor" en singular es seguro con \b: no
        // matchea adentro de "Florista" (la 'i' que sigue es caracter de palabra).
        Agregar(Categoria.Recurso,
            "papel", "papeles", "papéis",
            "flor", "flores",
            "hongo", "hongos", "fungo", "fungos",
            "botas",
            "paper", "papers", "flower", "flowers",
            "mushroom", "mushrooms", "boots", "shoes");

        // --- PELIGRO ----------------------------------------------------------
        // El Rocoso y el agua son las dos formas de morir del Nivel 1 (ver DeathCause).
        // "piedra"/"rock"/"pedra" entran porque las tablas las usan para ENSENAR la regla
        // ("la piedra le gana a las tijeras, pero no a un árbol de papel"), no como decorado.
        Agregar(Categoria.Peligro,
            "agua", "água", "water",
            "río", "rio", "river",
            "rocoso", "rocosos",
            "piedra", "piedras", "rocas",
            "rock", "rocks",
            "pedra", "pedras", "rochas", "rochosas");

        // --- MOVIMIENTO -------------------------------------------------------
        // Ojo: "correr" NO esta en la lista porque no aparece en ninguna tabla de hoy --
        // las botas de sprint se explican como "ir más rápido" / "ir mais rápido". El
        // unico idioma que tiene el verbo es el ingles ("run faster"). Si algun dia una
        // tabla dice "correr", se agrega aca y listo.
        Agregar(Categoria.Movimiento,
            "saltar", "saltá", "salto",
            "mover", "caminar", "caminando",
            "pular", "pule",
            "jump", "move", "run", "walking");

        _rx = ConstruirRegex();

        //un solo log, en el arranque. NO se loguea por texto procesado: esto corre por cada
        //linea de dialogo y cada tooltip, y llenaria la consola en dos minutos de juego.
        Debug.Log($"[ResaltadorDeConceptos] tabla armada con {_porPalabra.Count} palabras en 5 categorias");
    }

    static void Agregar(Categoria categoria, params string[] palabras)
    {
        for (int i = 0; i < palabras.Length; i++)
        {
            string palabra = palabras[i];

            //una palabra en dos categorias seria un color inestable segun el orden en que
            //se armo la tabla: mejor gritarlo ahora que perseguirlo en pantalla despues
            if (_porPalabra.ContainsKey(palabra))
            {
                Debug.LogWarning($"[ResaltadorDeConceptos] '{palabra}' ya estaba en la categoria {_porPalabra[palabra]}, ignoro la de {categoria}");
                continue;
            }

            _porPalabra.Add(palabra, categoria);
        }
    }

    // ------------------------------------------------------------------ regex

    /// <summary>
    /// UNA sola regex para todo, y una sola pasada de Replace. No son N pasadas encadenadas
    /// (una por categoria) a proposito: con N pasadas, la segunda vuelve a barrer los tags
    /// que escribio la primera y termina resaltando adentro de un "&lt;color=...&gt;".
    ///
    /// La alternancia tiene DOS ramas y el orden importa, porque el motor prueba las
    /// alternativas de izquierda a derecha en cada posicion:
    ///
    ///  1) "protegido" -- un placeholder {INPUT:*} o un tag de TMP &lt;...&gt;. Cuando
    ///     matchea se devuelve TAL CUAL y, sobre todo, se CONSUME entero: asi el contenido
    ///     de adentro nunca puede ser visto por la rama 2.
    ///
    ///     Esto no es paranoia: "{INPUT:cortar}" es un alias real de InputPromptSystem. Si
    ///     resaltaramos ese "cortar", el placeholder quedaria
    ///     "{INPUT:&lt;b&gt;&lt;color=#D6453D&gt;cortar&lt;/color&gt;&lt;/b&gt;}", InputPromptSystem
    ///     ya no lo reconoceria y el jugador veria el placeholder crudo en pantalla.
    ///     Mismo razonamiento para los tags: si una traduccion ya trae "&lt;color=...&gt;"
    ///     escrito a mano, el contenido del tag no es texto, es sintaxis.
    ///
    ///  2) "palabra" -- una palabra de la tabla, entre \b...\b.
    /// </summary>
    /// <summary>
    /// Frases que NO se resaltan aunque contengan palabras de la tabla, porque son nombres
    /// propios y no mecanica. Hoy es solo el titulo del juego: "Kami Papel Tijera" tiene
    /// adentro dos palabras clave ("papel" y "tijera"), y sin esto el titulo del libro sale
    /// con una palabra verde y otra roja en medio de un dialogo -- se lee como un error, no
    /// como una ensenanza. Van en la rama "protegido", asi que se consumen enteras.
    /// El \s+ es porque cada idioma lo escribe distinto ("Papel y Tijera" / "Paper Scissors").
    /// </summary>
    static readonly string[] _nombresPropios =
    {
        @"Kami[\s:,]+Papel(?:\s+y)?\s+Tijera",
        @"Kami[\s:,]+Paper\s+Scissors",
        @"Kami[\s:,]+Papel(?:\s+e)?\s+Tesoura"
    };

    static Regex ConstruirRegex()
    {
        var palabras = new List<string>(_porPalabra.Keys);

        //mas largas primero. Con \b a los dos lados el orden ya no cambia el resultado
        //("flor" no puede comerse el principio de "flores"), pero evita el backtracking.
        palabras.Sort((a, b) => b.Length.CompareTo(a.Length));

        var sb = new StringBuilder();

        // {..} sin cerrar o un "<" suelto no matchean ni consumen nada: el texto sigue
        // procesandose normal en vez de comerse el resto del string.
        //
        // Los nombres propios van PRIMEROS dentro de la rama protegida: si fueran despues de
        // la rama de palabras, el motor ya habria matcheado "Papel" solo y el titulo quedaria
        // partido igual.
        sb.Append(@"(?<protegido>");
        for (int i = 0; i < _nombresPropios.Length; i++)
        {
            sb.Append(_nombresPropios[i]).Append('|');
        }
        sb.Append(@"\{[^}]*\}|<[^>]*>)|\b(?<palabra>");

        for (int i = 0; i < palabras.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('|');
            }
            sb.Append(Regex.Escape(palabras[i]));
        }

        sb.Append(@")\b");

        //Compiled porque esto corre por cada linea de dialogo y cada tooltip; IgnoreCase
        //porque las tablas las escriben personas y la misma palabra aparece con y sin
        //mayuscula ("FLORES VIOLETAS" y "las flores"). En .NET \b es Unicode-aware, asi que
        //los acentos del voseo ("cortá") cierran palabra bien sin hacer nada especial.
        return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    static string Envolver(Match m)
    {
        if (m.Groups["protegido"].Success)
        {
            return m.Value;
        }

        string original = m.Groups["palabra"].Value;

        Categoria categoria;
        if (!_porPalabra.TryGetValue(original, out categoria))
        {
            //no deberia pasar nunca (la regex se arma de las MISMAS claves del diccionario),
            //pero si pasa el texto sale plano en vez de salir con un color inventado
            return original;
        }

        // Se re-inserta 'original', NUNCA la forma canonica de la tabla: el jugador tiene
        // que leer exactamente lo que escribio el traductor, con sus mayusculas y sus
        // acentos. La tabla sirve para DECIDIR el color, no para reescribir el texto.
        string abre = "<b><color=" + ColorDe(categoria) + ">";
        string cierra = "</color></b>";

        if (CreceDe(categoria))
        {
            //el size va ADENTRO del color: TMP cierra los tags por anidado, y dejarlo
            //afuera hace que el cambio de tamano se coma el renglon entero
            return abre + "<size=110%>" + original + "</size>" + cierra;
        }

        return abre + original + cierra;
    }
}
