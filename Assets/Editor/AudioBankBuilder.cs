using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates Resources/AudioBank.asset from the ~60 AudioSource children of
/// AudioManager.prefab, which already hold every clip, volume, pitch and loop value the game
/// shipped with. Generating rather than hand-typing is what makes the audio refactor safe: the
/// new system starts with exactly the old mix.
///
/// Idempotent: re-running only ADDS missing ids and reports orphans. Existing rows keep every
/// hand-edited field (bus, maxSimultaneous, extra clips), because reviewing 60 buses by hand is
/// work nobody wants to do twice.
/// </summary>
public static class AudioBankBuilder
{
    const string PREFAB_PATH = "Assets/Prefabs/AudioManager.prefab";
    const string BANK_PATH = "Assets/Resources/AudioBank.asset";

    [MenuItem("Kami/Audio/Rebuild Audio Bank")]
    public static void Rebuild()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[AudioBankBuilder] could not find {PREFAB_PATH}.");
            return;
        }

        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(BANK_PATH);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<AudioBank>();
            AssetDatabase.CreateAsset(bank, BANK_PATH);
            Debug.Log($"[AudioBankBuilder] created {BANK_PATH}.");
        }

        List<SoundEntry> entries = bank.EditorEntries;
        var existing = new Dictionary<string, SoundEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (!string.IsNullOrEmpty(entries[i].id))
            {
                existing[entries[i].id] = entries[i];
            }
        }

        AudioSource[] sources = prefab.GetComponentsInChildren<AudioSource>(true);
        int added = 0, skipped = 0;
        var seen = new HashSet<string>();

        foreach (AudioSource source in sources)
        {
            string id = source.gameObject.name;
            if (string.IsNullOrEmpty(id) || id == "AudioManager")
            {
                continue; //the root itself carries an AudioSource in some revisions
            }
            if (!seen.Add(id))
            {
                Debug.LogWarning($"[AudioBankBuilder] '{id}' appears twice in the prefab.");
                continue;
            }
            if (existing.ContainsKey(id))
            {
                skipped++;
                continue; //preserve hand edits
            }

            entries.Add(new SoundEntry
            {
                id = id,
                clips = source.clip != null ? new[] { source.clip } : new AudioClip[0],
                volume = source.volume,
                pitch = source.pitch,
                pitchVariation = 0f,
                loop = source.loop,
                bus = GuessBus(id, source.loop),
                maxSimultaneous = source.loop ? 1 : 4,
                spatialBlend = source.spatialBlend,
                priority = source.priority,
                interruptSelf = false
            });
            added++;

            if (source.clip == null)
            {
                Debug.LogWarning($"[AudioBankBuilder] '{id}' has no clip in the prefab: " +
                                 "the row is left without a clip.");
            }
        }

        foreach (string id in existing.Keys)
        {
            if (!seen.Contains(id))
            {
                Debug.Log($"[AudioBankBuilder] '{id}' is in the bank but no longer in the prefab. " +
                          "Not deleting it (it could be a sound added by hand).");
            }
        }

        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AudioBankBuilder] done: {added} new rows, {skipped} preserved, " +
                  $"{entries.Count} total. Check the 'bus' column by hand once.");
    }

    /// <summary>
    /// First guess at routing, from the id. Deliberately a guess: Diego reviews it once
    /// (design doc section 8.3). Only affects which fader the sound sits under, never whether
    /// it plays.
    /// </summary>
    static AudioBus GuessBus(string id, bool loop)
    {
        if (id.StartsWith("MemoFlora") || id.StartsWith("Bohren") || id.StartsWith("4S_") ||
            id.StartsWith("EstampesPagodes") || id.StartsWith("GameOver") ||
            id.StartsWith("IntroStoryboard"))
        {
            return AudioBus.Music;
        }
        if (id.StartsWith("Forest"))
        {
            return AudioBus.Ambience;
        }
        if (id.StartsWith("PageTurn") || id.StartsWith("PickupSFX") ||
            id.StartsWith("PickupReversed") || id == "Action_Hover")
        {
            return AudioBus.UI;
        }
        return AudioBus.SFX;
    }
}
