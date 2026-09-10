using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-editable configuration for the keyword highlighter (see ResaltadorDeConceptos).
///
/// Why this exists: colours, which words count as "mechanics" and how strong the emphasis is
/// are design decisions, not code. Having them in a script meant editing C# and reading hex
/// strings to answer "is this red readable on a yellow post-it?". Here you get a colour picker
/// and the whole vocabulary in one place.
///
/// HOW IT IS LOADED: there is no field to drag anywhere. The asset must live at
/// Assets/Resources/TextHighlightSettings.asset and is loaded by name at runtime, so the game
/// works on Play with nothing wired in any scene. If the asset is missing or fails to parse,
/// the highlighter falls back to a built-in copy of these values and logs a warning — the game
/// never breaks because of a bad edit here.
///
/// EDITING DURING PLAY: changes take effect immediately. OnValidate tells the highlighter to
/// rebuild, and every text currently on screen is rewritten, so you can tune colours while a
/// dialogue is open and watch it change.
/// </summary>
[CreateAssetMenu(fileName = "TextHighlightSettings", menuName = "Kami/Text Highlight Settings")]
public class TextHighlightSettings : ScriptableObject
{
    /// <summary>Name of the asset inside a Resources folder. No extension, no path.</summary>
    public const string ResourcesPath = "TextHighlightSettings";

    /// <summary>
    /// One colour code entry: a group of words that mean the same kind of thing to the player.
    /// </summary>
    [Serializable]
    public class Category
    {
        [Tooltip("Just a label so you can tell the entries apart in this list. Not used at runtime.")]
        public string categoryName = "New category";

        [Tooltip("Colour every word in this category is painted. This is the didactic contract " +
                 "with the player: the same idea must always be the same colour. Remember the " +
                 "tutorial post-its are yellow, so very light colours will not read on them.")]
        public Color color = Color.white;

        [Tooltip("Bold. On for every category by default; it is what makes the word pop out of " +
                 "the paragraph even for a player who cannot tell the colours apart.")]
        public bool bold = true;

        [Tooltip("Also make these words slightly bigger. Use it on one or two categories at most: " +
                 "if everything grows, growing stops meaning anything and the lines get uneven.")]
        public bool enlarge = false;

        [Range(100f, 160f)]
        [Tooltip("How much bigger, in percent, when 'enlarge' is on.")]
        public float sizePercent = 110f;

        [Tooltip("The words, separated by commas or one per line. Case-insensitive, and matched " +
                 "as WHOLE words, so 'flor' will not match inside 'Florista'.\n\n" +
                 "Put every language here (es/en/pt) and every conjugation that actually appears " +
                 "in the localization tables. A word that is in no table just makes the search " +
                 "bigger for nothing.\n\n" +
                 "NEVER put input words here (E, U, I, O, M, WASD, click, shift, ctrl, esc, " +
                 "space, stick, A, B, L1, L2, Start) or the verbs around them (Tocá, Press, " +
                 "Mantené, Arrastrá...): InputPromptSystem runs after this and needs to find " +
                 "them intact to turn them into button icons.")]
        [TextArea(2, 8)]
        public string words = "";
    }

    [Header("Master switch")]
    [Tooltip("Off means every text is drawn plain, with no colours at all. The per-scene cutscene " +
             "rule below is separate from this.")]
    public bool highlightingEnabled = true;

    [Header("Colour code")]
    [Tooltip("One entry per kind of idea. Order does not matter; a word must not appear in two " +
             "categories (if it does, the first one wins and you get a warning in the console).")]
    public List<Category> categories = new List<Category>();

    [Header("Exceptions")]
    [Tooltip("Phrases that are NEVER highlighted even though they contain keywords, because they " +
             "are proper nouns and not mechanics. One per line, or separated by commas.\n\n" +
             "The game title is the reason this exists: 'Kami Papel Tijera' would otherwise come " +
             "out with one word green and another red in the middle of a dialogue, which reads " +
             "like a bug. Write them as plain text, not as a regular expression.")]
    [TextArea(2, 5)]
    public string protectedPhrases = "";

    [Tooltip("Highlighting is switched off completely in any scene whose name contains one of " +
             "these, because those dialogues are cinematic and colour-coded teaching words break " +
             "the tone. One per line, or separated by commas. 'Cutscene' covers Nivel1_EndCutscene " +
             "and any future one.")]
    [TextArea(1, 4)]
    public string cinematicSceneKeywords = "Cutscene";

    /// <summary>
    /// Any edit in the Inspector rebuilds the highlighter and repaints the text already on
    /// screen. Editor-only: OnValidate does not run in a build.
    /// </summary>
    void OnValidate()
    {
        for (int i = 0; i < categories.Count; i++)
        {
            Category category = categories[i];
            if (category == null)
            {
                continue;
            }

            //a fully transparent colour is almost certainly a mistake (a new entry starts at
            //alpha 0 if you build it by hand) and would make the word invisible in game
            if (category.color.a <= 0f)
            {
                category.color.a = 1f;
            }
        }

        ResaltadorDeConceptos.Invalidate();
    }
}
