using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Highlights MECHANIC WORDS in any text shown to the player, with bold + a colour per
/// category (and a size bump on the most important ones).
///
/// Why it exists: Kami is played by kids, and most of what they need to learn ("cortar",
/// "doblar", "papel", "agua") is written in the middle of a paragraph with the same visual
/// weight as everything else. A colour code that is CONSISTENT across the whole game lets the
/// mechanic be learned by reading, without a separate explanation.
///
/// All the design values — colours, vocabulary, how strong the emphasis is — live in an
/// inspector-editable asset, <see cref="TextHighlightSettings"/>. This class is only the
/// engine; it holds a built-in copy of those values purely as a fallback so a missing or
/// broken asset can never take the game down.
///
/// EXECUTION ORDER (important): this runs BEFORE InputPromptSystem, on the same string. See
/// "forbidden vocabulary" in the settings asset tooltips for why that constrains which words
/// are allowed in the table.
/// </summary>
public static class ResaltadorDeConceptos
{
    // ------------------------------------------------------------------- API

    /// <summary>
    /// Manual kill switch, from code. Kept SEPARATE from the asset's own
    /// <c>highlightingEnabled</c> and from the cutscene rule, so none of them silently
    /// overwrite the others.
    /// </summary>
    public static bool Activo { get; set; } = true;

    /// <summary>
    /// Raised after the settings are rebuilt, so whatever is already on screen can repaint
    /// itself. LocalizedText listens to this — same pattern as InputHub.OnDeviceCambio.
    /// </summary>
    public static event Action OnSettingsChanged;

    /// <summary>
    /// Returns the text with keywords wrapped in TMP tags. Safe with any string: when there
    /// is nothing to highlight it returns THE SAME instance without allocating (Regex.Replace
    /// already behaves that way when there is no match).
    /// </summary>
    public static string Resaltar(string texto)
    {
        if (!Activo || !_sceneAllowsHighlighting || string.IsNullOrEmpty(texto))
        {
            return texto;
        }

        EnsureBuilt();

        if (!_enabled || _rx == null)
        {
            return texto;
        }

        return _rx.Replace(texto, Envolver);
    }

    /// <summary>
    /// Drops the cached table so it is rebuilt from the asset on the next use, and asks
    /// everything on screen to repaint. Called by <see cref="TextHighlightSettings"/> on every
    /// inspector edit, which is what makes tuning colours during Play feel immediate.
    /// </summary>
    public static void Invalidate()
    {
        _built = false;

        Action handler = OnSettingsChanged;
        if (handler != null)
        {
            handler();
        }
    }

    // ------------------------------------------------------------------ state

    class Style
    {
        public string open;
        public string close;
    }

    static readonly Dictionary<string, Style> _byWord = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
    static readonly List<string> _cinematicKeywords = new List<string>();

    static Regex _rx;
    static bool _built;
    static bool _enabled = true;
    static bool _sceneAllowsHighlighting = true;

    // -------------------------------------------------------------- building

    static void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }

        //set first: if anything below throws, we must not retry the whole build on every
        //single line of dialogue for the rest of the session
        _built = true;

        _byWord.Clear();
        _cinematicKeywords.Clear();
        _rx = null;

        var protectedPhrases = new List<string>();

        TextHighlightSettings settings = Resources.Load<TextHighlightSettings>(TextHighlightSettings.ResourcesPath);

        if (settings == null)
        {
            Debug.LogWarning($"[ResaltadorDeConceptos] no encontre el asset " +
                             $"'Assets/Resources/{TextHighlightSettings.ResourcesPath}.asset'. " +
                             "Uso los valores de fallback que estan en este script: el juego anda igual, " +
                             "pero lo que edites en el inspector no va a tener efecto hasta que el asset exista.");
            LoadBuiltInDefaults(protectedPhrases);
        }
        else
        {
            LoadFrom(settings, protectedPhrases);
        }

        _rx = BuildRegex(protectedPhrases);

        //re-evaluate: the cutscene keyword list may have changed with the settings
        EvaluateActiveScene(true);
    }

    static void LoadFrom(TextHighlightSettings settings, List<string> protectedPhrases)
    {
        _enabled = settings.highlightingEnabled;

        for (int i = 0; i < settings.categories.Count; i++)
        {
            TextHighlightSettings.Category category = settings.categories[i];
            if (category == null)
            {
                continue;
            }

            Style style = BuildStyle(category.color, category.bold, category.enlarge, category.sizePercent);
            AddWords(style, SplitList(category.words), category.categoryName);
        }

        protectedPhrases.AddRange(SplitList(settings.protectedPhrases));
        _cinematicKeywords.AddRange(SplitList(settings.cinematicSceneKeywords));

        Debug.Log($"[ResaltadorDeConceptos] settings cargados del asset: {_byWord.Count} palabras " +
                  $"en {settings.categories.Count} categorias, {protectedPhrases.Count} frases protegidas");
    }

    static Style BuildStyle(Color color, bool bold, bool enlarge, float sizePercent)
    {
        //tags are precomputed once per category instead of per match: this runs on every line
        //of dialogue and every tooltip
        var open = new StringBuilder();
        var close = new StringBuilder();

        if (bold)
        {
            open.Append("<b>");
            close.Insert(0, "</b>");
        }

        open.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>');
        close.Insert(0, "</color>");

        if (enlarge)
        {
            //the size tag goes INSIDE the colour one: TMP closes tags by nesting, and leaving
            //it outside makes the size change swallow the whole line
            open.Append("<size=").Append(Mathf.RoundToInt(sizePercent)).Append("%>");
            close.Insert(0, "</size>");
        }

        return new Style { open = open.ToString(), close = close.ToString() };
    }

    static void AddWords(Style style, IEnumerable<string> words, string categoryName)
    {
        foreach (string word in words)
        {
            //a word in two categories would be an unstable colour depending on the order the
            //table was built in: better to shout about it now than chase it on screen later
            if (_byWord.ContainsKey(word))
            {
                Debug.LogWarning($"[ResaltadorDeConceptos] '{word}' esta repetida en mas de una categoria " +
                                 $"(la de '{categoryName}' se ignora, gana la primera que se cargo)");
                continue;
            }

            _byWord.Add(word, style);
        }
    }

    /// <summary>Splits a comma- or newline-separated list, trimming and dropping empties.</summary>
    static List<string> SplitList(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(raw))
        {
            return result;
        }

        string[] pieces = raw.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i].Trim();
            if (piece.Length > 0)
            {
                result.Add(piece);
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ regex

    /// <summary>
    /// ONE regex for everything, and a single Replace pass. Deliberately not N chained passes
    /// (one per category): with N passes, the second one sweeps over the tags the first one
    /// wrote and ends up highlighting inside a "&lt;color=...&gt;".
    ///
    /// The alternation has TWO branches and the order matters, because the engine tries the
    /// alternatives left to right at each position:
    ///
    ///  1) "protegido" — a protected phrase, an {INPUT:*} placeholder or a TMP tag &lt;...&gt;.
    ///     When it matches it is returned AS IS and, above all, CONSUMED whole, so what is
    ///     inside can never be seen by branch 2.
    ///
    ///     This is not paranoia: "{INPUT:cortar}" is a real InputPromptSystem alias. If we
    ///     highlighted that "cortar", the placeholder would become
    ///     "{INPUT:&lt;b&gt;&lt;color=#D6453D&gt;cortar&lt;/color&gt;&lt;/b&gt;}",
    ///     InputPromptSystem would no longer recognise it, and the player would see the raw
    ///     placeholder on screen. Same reasoning for tags: if a translation already carries a
    ///     hand-written "&lt;color=...&gt;", what is inside the tag is syntax, not text.
    ///
    ///  2) "palabra" — a word from the table, between \b...\b.
    /// </summary>
    static Regex BuildRegex(List<string> protectedPhrases)
    {
        if (_byWord.Count == 0)
        {
            return null;
        }

        var words = new List<string>(_byWord.Keys);

        //longest first. With \b on both sides the order no longer changes the result ("flor"
        //cannot eat the start of "flores"), but it avoids backtracking.
        words.Sort((a, b) => b.Length.CompareTo(a.Length));

        var sb = new StringBuilder();
        sb.Append("(?<protegido>");

        //protected phrases go FIRST inside the protected branch: after them the engine would
        //already have matched "Papel" on its own and the title would be split anyway
        for (int i = 0; i < protectedPhrases.Count; i++)
        {
            sb.Append(EscapePhrase(protectedPhrases[i])).Append('|');
        }

        // an unclosed {..} or a stray "<" matches nothing and consumes nothing: the text keeps
        // being processed normally instead of eating the rest of the string
        sb.Append(@"\{[^}]*\}|<[^>]*>)|\b(?<palabra>");

        for (int i = 0; i < words.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('|');
            }
            sb.Append(Regex.Escape(words[i]));
        }

        sb.Append(@")\b");

        //Compiled because this runs on every line of dialogue and every tooltip; IgnoreCase
        //because the tables are written by people and the same word shows up with and without
        //capitals ("FLORES VIOLETAS" and "las flores"). In .NET \b is Unicode-aware, so the
        //accents of the voseo ("cortá") close the word correctly with nothing special.
        try
        {
            return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        catch (ArgumentException e)
        {
            //this can only really happen through a bad edit in the asset. Losing the highlight
            //is annoying; throwing on every line of dialogue would be a broken game.
            Debug.LogError($"[ResaltadorDeConceptos] la tabla de palabras genero una regex invalida, " +
                           $"apago el resaltado: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Turns a plain phrase written by a human into a safe pattern: everything is escaped (so
    /// it can never be read as a regular expression) and runs of whitespace become \s+, so
    /// "Kami Papel y Tijera" also matches when a translation spaces or punctuates it slightly
    /// differently.
    /// </summary>
    static string EscapePhrase(string phrase)
    {
        string[] tokens = phrase.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var sb = new StringBuilder();
        for (int i = 0; i < tokens.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(@"[\s:,]+");
            }
            sb.Append(Regex.Escape(tokens[i]));
        }

        return sb.ToString();
    }

    static string Envolver(Match m)
    {
        if (m.Groups["protegido"].Success)
        {
            return m.Value;
        }

        string original = m.Groups["palabra"].Value;

        Style style;
        if (!_byWord.TryGetValue(original, out style))
        {
            //should never happen (the regex is built from the SAME dictionary keys), but if it
            //does the text comes out plain instead of with an invented colour
            return original;
        }

        // 'original' is re-inserted, NEVER the canonical form from the table: the player has to
        // read exactly what the translator wrote, with their capitals and their accents. The
        // table is there to DECIDE the colour, not to rewrite the text.
        return style.open + original + style.close;
    }

    // ------------------------------------------------------- cinematic scenes

    // Cutscenes are cinematic: colour-coded teaching words break the tone there, so highlighting
    // is switched off for the whole scene. Deliberately SEPARATE from the manual `Activo` kill
    // switch — the scene rule must not silently overwrite a choice made by hand.

    static bool IsCinematicScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        for (int i = 0; i < _cinematicKeywords.Count; i++)
        {
            if (sceneName.IndexOf(_cinematicKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneChanges()
    {
        //-= before += so subscribing twice is impossible even if this runs again
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureBuilt();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //We evaluate the ACTIVE scene rather than the one that just loaded: on an additive load
        //the base level is still the one that sets the tone, and it stays active.
        EvaluateActiveScene(false);
    }

    static void EvaluateActiveScene(bool silent)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        bool cinematic = IsCinematicScene(sceneName);
        bool allow = !cinematic;

        if (allow == _sceneAllowsHighlighting)
        {
            return;
        }

        _sceneAllowsHighlighting = allow;

        if (cinematic && !silent)
        {
            Debug.Log($"[ResaltadorDeConceptos] '{sceneName}' is a cutscene: keyword highlighting off, text stays plain");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        //with "Enter Play Mode" and no domain reload, statics survive between runs
        _built = false;
        _sceneAllowsHighlighting = true;
        Activo = true;
        OnSettingsChanged = null;
    }

    // ------------------------------------------------------- built-in fallback

    // Copia de respaldo de los valores del asset. NO es la fuente de verdad: si el asset existe,
    // gana el asset. Esto solo evita que borrar o romper el asset deje el juego sin texto util.
    //
    // DE DONDE SALEN ESTAS PALABRAS: se barrieron las tablas reales de
    // Assets/Localization Settings/Tables/ en los tres idiomas -- DialogueTable_es/en/pt,
    // TooltipTable_es/en/pt, UITexts_es/en/pt, ItemTable_es/en/pt y QuestTable_es/en/pt -- y se
    // anotaron SOLO las formas que aparecen escritas de verdad, conjugaciones incluidas ("cortá"
    // del voseo, "cortás", "corte" del portugues).
    static void LoadBuiltInDefaults(List<string> protectedPhrases)
    {
        _enabled = true;

        //plateado: gris metalico, oscurecido para que siga leyendose sobre un post-it amarillo
        AddWords(BuildStyle(HexColor("7A828C"), true, true, 110f), SplitList(
            "cortar, cortá, cortás, cortes, corte, corta, tijera, tijeras, cut, scissors, tesoura, tesouras"), "Tijera");

        //dorado: la palabra "papel" en si NO va aca -- tiene su propio color como recurso, ver
        //"Recurso - Papel" mas abajo. Esta categoria es el MECANISMO (origami/doblar), no el material.
        AddWords(BuildStyle(HexColor("A67C00"), true, true, 110f), SplitList(
            "origami, origamis, doblar, doblada, doblarías, desdoblar, pliegue, fold, folding, unfold, dobrar, desdobrar"), "Papel y Origami");

        AddWords(BuildStyle(HexColor("2D7DD2"), true, false, 110f), SplitList(
            "agua, água, water, río, rio, river, mojar, mojarte, mojado, mojada, mojarse, molhar, molhado, molhada"), "Rio y Mojar");

        AddWords(BuildStyle(HexColor("D6453D"), true, false, 110f), SplitList(
            "rocoso, rocosos, piedra, piedras, rocas, rock, rocks, pedra, pedras, rochas, rochosas, " +
            "muerte, morte, death"), "Rocoso y Muerte");

        //ceniza: gris calido, distinto del plateado de la tijera y del verde de los demas recursos
        AddWords(BuildStyle(HexColor("8C8577"), true, false, 110f), SplitList(
            "papel, papeles, papéis, paper, papers"), "Recurso - Papel");

        AddWords(BuildStyle(HexColor("9B3D8F"), true, false, 110f), SplitList(
            "flor, flores, flower, flowers"), "Recurso - Flores");

        AddWords(BuildStyle(HexColor("3E9B4F"), true, false, 110f), SplitList(
            "hongo, hongos, fungo, fungos, botas, mushroom, mushrooms, boots, shoes"), "Recursos varios");

        //teal: antes era violeta, pero el violeta ahora es de las flores -- evita que dos
        //categorias distintas compartan color
        AddWords(BuildStyle(HexColor("1E8A78"), true, false, 110f), SplitList(
            "saltar, saltá, salto, mover, caminar, caminando, pular, pule, jump, move, run, walking"), "Movement");

        protectedPhrases.Add("Kami Papel y Tijera");
        protectedPhrases.Add("Kami Papel Tijera");
        protectedPhrases.Add("Kami Paper Scissors");
        protectedPhrases.Add("Kami Papel e Tesoura");

        _cinematicKeywords.Add("Cutscene");
    }

    static Color HexColor(string rrggbb)
    {
        Color color;
        return ColorUtility.TryParseHtmlString("#" + rrggbb, out color) ? color : Color.white;
    }
}
