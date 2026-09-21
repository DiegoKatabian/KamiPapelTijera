using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Pooled, data-driven audio. Configuration lives in Resources/AudioBank.asset (one row per
/// sound); playback goes through a fixed pool of AudioSources instead of ~60 always-alive ones.
///
/// The public surface kept the shape of the old string-keyed manager so the migration could
/// happen one folder at a time behind Obsolete shims. Those shims are gone: every call site now
/// uses Play/StopById with an AudioId constant, and the project compiling without them is the
/// proof that none was missed.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [SerializeField, Tooltip("How many AudioSources the pool creates. Raise it if the pool " +
                             "warns about stealing sources.")]
    int _poolSize = 24;

    AudioPool _pool;
    float _globalVolume = 1f;
    bool _soundOn = true;
    readonly Dictionary<AudioBus, float> _busVolumes = new Dictionary<AudioBus, float>();
    readonly HashSet<string> _unknownIdsReported = new HashSet<string>();

    public bool SoundOn
    {
        get { return _soundOn; }
        set
        {
            _soundOn = value;
            if (_soundOn)
            {
                UnmuteAll();
            }
            else
            {
                MuteAll();
            }
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        _pool = new AudioPool(transform, _poolSize);

        foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
        {
            _busVolumes[bus] = 1f;
        }

        if (AudioBank.Instance == null)
        {
            Debug.LogError("[AudioManager] started with no AudioBank: nothing will play.");
        }
    }

    void Update()
    {
        //a duplicate instance destroys itself in Awake, but Destroy only takes effect at the end
        //of the frame -- so this Update can still run once on a manager that never built a pool.
        //Every scene instances AudioManager.prefab, so a duplicate happens on every scene load.
        if (_pool == null)
        {
            return;
        }

        //returns finished one-shots to the pool. See AudioPool.ReclaimFinished for why this is a
        //poll and not a coroutine per sound.
        _pool.ReclaimFinished();
    }

    // ------------------------------------------------------------------ play

    public SoundHandle Play(string id)
    {
        return PlayInternal(id, null, null, false, Vector3.zero);
    }

    public SoundHandle Play(string id, float pitch)
    {
        return PlayInternal(id, pitch, null, false, Vector3.zero);
    }

    public SoundHandle Play(string id, float centralPitch, float pitchVariation)
    {
        return PlayInternal(id, centralPitch, pitchVariation, false, Vector3.zero);
    }

    /// <summary>Positional one-shot. Used by the chickens; the rest of the game is 2D.</summary>
    public SoundHandle PlayAt(string id, Vector3 position)
    {
        return PlayInternal(id, null, null, true, position);
    }

    SoundHandle PlayInternal(string id, float? pitchOverride, float? variationOverride,
                             bool positional, Vector3 position)
    {
        if (!TryGetEntry(id, out SoundEntry entry))
        {
            return SoundHandle.None;
        }

        AudioClip clip = entry.PickClip();
        if (clip == null)
        {
            //a bank row with no clip is a legitimate "not recorded yet" state (Dialogue_Sigh
            //ships that way), so warn once per id and stay silent instead of erroring
            if (_unknownIdsReported.Add($"noclip:{id}"))
            {
                Debug.LogWarning($"[AudioManager] '{id}' has no clips in the bank: staying silent.");
            }
            return SoundHandle.None;
        }

        if (entry.maxSimultaneous > 0 && _pool.CountPlaying(id) >= entry.maxSimultaneous)
        {
            if (!entry.interruptSelf)
            {
                return SoundHandle.None; //at the cap: drop this play, by design
            }
            AudioSource oldest = _pool.OldestOf(id);
            if (oldest != null)
            {
                _pool.StopSource(oldest);
            }
        }

        float basePitch = pitchOverride ?? entry.pitch;
        float variation = variationOverride ?? entry.pitchVariation;
        float finalPitch = variation > 0f
            ? Random.Range(basePitch - variation, basePitch + variation)
            : basePitch;

        double expectedEnd = AudioSettings.dspTime +
                             (clip.length / Mathf.Max(0.01f, Mathf.Abs(finalPitch)));

        if (!_pool.TryAcquire(id, entry.loop, entry.volume, expectedEnd,
                              out AudioSource source, out int generation))
        {
            return SoundHandle.None;
        }

        source.clip = clip;
        source.loop = entry.loop;
        source.pitch = finalPitch;
        source.priority = entry.priority;
        source.spatialBlend = positional ? Mathf.Max(entry.spatialBlend, 1f) : entry.spatialBlend;
        source.volume = entry.volume * _globalVolume * BusVolume(entry.bus);
        source.mute = !_soundOn;
        source.transform.position = positional ? position : Vector3.zero;

        AudioMixerGroup group = AudioBank.Instance != null ? AudioBank.Instance.GroupFor(entry.bus) : null;
        source.outputAudioMixerGroup = group; //null until the mixer exists: volume stays code-side

        source.Play();
        return new SoundHandle(source, generation);
    }

    bool TryGetEntry(string id, out SoundEntry entry)
    {
        entry = null;
        if (AudioBank.Instance == null)
        {
            return false;
        }
        if (!AudioBank.Instance.TryGet(id, out entry))
        {
            //the old manager threw KeyNotFoundException here, which is how TriggerSound's call for
            //the nonexistent "4S_MarimbaLoop" could take down a whole coroutine. Warn once instead.
            if (_unknownIdsReported.Add(id))
            {
                Debug.LogWarning($"[AudioManager] no sound '{id}' in the bank. " +
                                 "Check Resources/AudioBank.asset. (Warned once per id.)");
            }
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ stop

    public void Stop(SoundHandle handle)
    {
        if (!handle.IsValid || !_pool.IsCurrent(handle.source, handle.generation))
        {
            return; //stale handle: the source now plays something else. Never stop it.
        }
        _pool.StopSource(handle.source);
    }

    public void StopById(string id)
    {
        _pool.StopId(id);
    }

    public void StopAll()
    {
        _pool.StopEverything();
    }

    public bool IsPlaying(string id)
    {
        return _pool.IsPlayingId(id);
    }

    // ------------------------------------------------------------------ volume

    public void MuteAll()
    {
        _pool.SetMuted(true);
    }

    public void UnmuteAll()
    {
        _pool.SetMuted(false);
    }

    public void SetGlobalVolume(float volume)
    {
        _globalVolume = volume;
        _pool.ApplyGlobalVolume(_globalVolume);
        OnGlobalVolumeChanged();
    }

    public void SetBusVolume(AudioBus bus, float volume)
    {
        _busVolumes[bus] = volume;
        //code-side volume: re-apply to whatever of this bus is currently playing. Task 23 moves
        //this onto the mixer exposed parameters now that the four groups exist.
        if (AudioBank.Instance == null)
        {
            return;
        }
        foreach (SoundEntry entry in AudioBank.Instance.Entries)
        {
            if (entry.bus == bus)
            {
                _pool.ApplyVolumeToId(entry.id, _globalVolume * volume);
            }
        }
    }

    float BusVolume(AudioBus bus)
    {
        return _busVolumes.TryGetValue(bus, out float volume) ? volume : 1f;
    }

    /// <summary>Menu ducking. Same signature as before; now one bus instead of a hardcoded list.</summary>
    public void SetBGMVolumes(float volume)
    {
        SetBusVolume(AudioBus.Music, volume);
    }

    public void ResetBGMVolumes()
    {
        SetBusVolume(AudioBus.Music, 1f);
    }

    void OnGlobalVolumeChanged()
    {
        if (!IsPlaying(AudioId.Gallina_Evade_VolumeTest))
        {
            PlayGallinaSound();
        }
    }

    /// <summary>Audition sound when the volume slider moves. Behaviour preserved from before.</summary>
    public void PlayGallinaSound()
    {
        Play(AudioId.Gallina_Evade_VolumeTest);
    }

    // ------------------------------------------------------------------ sequencing

    public void PlayOnEnd(string idToEnd, string idToPlay)
    {
        StartCoroutine(PlayOnOtherSoundEnd(idToEnd, idToPlay));
    }

    public IEnumerator PlayOnOtherSoundEnd(string idToEnd, string idToPlay)
    {
        while (IsPlaying(idToEnd))
        {
            yield return null;
        }
        StopById(idToEnd);
        Play(idToPlay);
    }
}
