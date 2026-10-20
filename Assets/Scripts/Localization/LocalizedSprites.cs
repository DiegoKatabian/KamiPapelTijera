using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// One picture, or one flipbook's frames, per language: art with words printed on it (the café ticket).
/// The first entry is the fallback and the one prefabs and animation clips are authored with, so
/// <see cref="Localize"/> can map any of its frames to the same frame of the current language.
/// Plain asset rather than a Localization asset table: those need Addressables entries per sprite.
/// </summary>
[CreateAssetMenu(fileName = "LocalizedSprites", menuName = "Kami/Localized Sprites")]
public class LocalizedSprites : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Locale code as in Localization Settings: es, en, pt.")]
        public string localeCode;

        [Tooltip("The frames, in the same order for every language. A single picture is one frame.")]
        public Sprite[] sprites;
    }

    [Tooltip("First entry = fallback, and the frames the prefabs and animation clips use.")]
    [SerializeField] Entry[] _entries;

    /// <summary>The selected locale's code, or null while Localization is still starting up.</summary>
    public static string CurrentLocaleCode
    {
        get
        {
            if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone)
            {
                return null;
            }

            var locale = LocalizationSettings.SelectedLocale;
            return locale != null ? locale.Identifier.Code : null;
        }
    }

    /// <summary>Frame <paramref name="index"/> in the current language (the fallback entry's when that language has none).</summary>
    public Sprite Get(int index = 0)
    {
        Entry entry = CurrentEntry();
        if (entry == null)
        {
            Debug.LogWarning($"[LocalizedSprites] {name}: no entries");
            return null;
        }

        if (entry.sprites == null || index < 0 || index >= entry.sprites.Length)
        {
            Debug.LogWarning($"[LocalizedSprites] {name}: no frame {index} for '{entry.localeCode}'");
            return null;
        }

        return entry.sprites[index];
    }

    /// <summary>The current language's version of <paramref name="authored"/>, a fallback-entry frame. Anything else comes back unchanged.</summary>
    public Sprite Localize(Sprite authored)
    {
        if (authored == null || _entries == null || _entries.Length == 0 || _entries[0].sprites == null)
        {
            return authored;
        }

        int index = Array.IndexOf(_entries[0].sprites, authored);
        if (index < 0)
        {
            return authored;
        }

        Entry entry = CurrentEntry();
        if (entry == null || entry.sprites == null || index >= entry.sprites.Length || entry.sprites[index] == null)
        {
            return authored;
        }

        return entry.sprites[index];
    }

    Entry CurrentEntry()
    {
        if (_entries == null || _entries.Length == 0)
        {
            return null;
        }

        string code = CurrentLocaleCode;
        if (!string.IsNullOrEmpty(code))
        {
            foreach (Entry entry in _entries)
            {
                if (entry != null && entry.localeCode == code)
                {
                    return entry;
                }
            }
        }

        return _entries[0];
    }
}
