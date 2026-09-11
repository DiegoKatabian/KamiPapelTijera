using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The single source of truth for sound configuration. Self-loading from Resources (same pattern
/// as TextHighlightSettings) so nothing needs wiring, and generated from AudioManager.prefab by
/// AudioBankBuilder so the values match what the game shipped with.
/// </summary>
[CreateAssetMenu(fileName = "AudioBank", menuName = "Kami/Audio Bank")]
public class AudioBank : ScriptableObject
{
    const string RESOURCE_NAME = "AudioBank";

    [SerializeField] List<SoundEntry> entries = new List<SoundEntry>();

    [Header("Mixer routing (optional until the mixer exists)")]
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup uiGroup;
    [SerializeField] AudioMixerGroup ambienceGroup;

    static AudioBank _instance;
    static bool _missingReported;
    Dictionary<string, SoundEntry> _byId;

    public IReadOnlyList<SoundEntry> Entries => entries;

    public static AudioBank Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<AudioBank>(RESOURCE_NAME);
                if (_instance == null && !_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError($"[AudioBank] could not find Resources/{RESOURCE_NAME}.asset: " +
                                   "nothing will play. Generate it with Kami/Audio/Rebuild Audio Bank.");
                }
            }
            return _instance;
        }
    }

    public IEnumerable<string> Ids
    {
        get
        {
            for (int i = 0; i < entries.Count; i++)
            {
                yield return entries[i].id;
            }
        }
    }

    public bool TryGet(string id, out SoundEntry entry)
    {
        if (_byId == null)
        {
            BuildIndex();
        }
        return _byId.TryGetValue(id ?? "", out entry);
    }

    public AudioMixerGroup GroupFor(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Music: return musicGroup;
            case AudioBus.SFX: return sfxGroup;
            case AudioBus.UI: return uiGroup;
            case AudioBus.Ambience: return ambienceGroup;
            default: return null;
        }
    }

    void BuildIndex()
    {
        _byId = new Dictionary<string, SoundEntry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            SoundEntry entry = entries[i];
            if (string.IsNullOrEmpty(entry.id))
            {
                Debug.LogWarning($"[AudioBank] row {i} has no id, skipping it.");
                continue;
            }
            if (_byId.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[AudioBank] duplicate id '{entry.id}', keeping the first one.");
                continue;
            }
            _byId[entry.id] = entry;
        }
    }

    void OnValidate()
    {
        _byId = null; //force a rebuild after an inspector edit
    }

#if UNITY_EDITOR
    /// <summary>Editor-only mutation used by AudioBankBuilder. Never call at runtime.</summary>
    public List<SoundEntry> EditorEntries => entries;
#endif
}
