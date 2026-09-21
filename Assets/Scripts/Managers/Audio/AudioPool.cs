using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed pool of AudioSources. Replaces the ~60 always-alive AudioSource children of
/// AudioManager.prefab: sources are generic here, and a SoundEntry configures one at play time.
///
/// Generation counters make stale SoundHandles safe (see SoundHandle): a handle can only ever
/// stop the exact instance it was issued for.
/// </summary>
public class AudioPool
{
    class PooledSource
    {
        public AudioSource source;
        public int generation;
        public string id;
        public bool inUse;
        public float baseVolume;   //entry volume, before the global multiplier
        public double releaseTime; //AudioSettings.dspTime when a one-shot is expected to end
        public bool isLoop;
    }

    readonly List<PooledSource> _sources = new List<PooledSource>();
    readonly Transform _parent;
    bool _warnedExhausted;

    public AudioPool(Transform parent, int size)
    {
        _parent = parent;
        for (int i = 0; i < size; i++)
        {
            _sources.Add(CreateSource(i));
        }
    }

    PooledSource CreateSource(int index)
    {
        var go = new GameObject($"PooledAudioSource_{index}");
        go.transform.SetParent(_parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return new PooledSource { source = source, generation = 0, inUse = false };
    }

    public int CountPlaying(string id)
    {
        int count = 0;
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse && _sources[i].id == id)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Oldest live instance of an id, or null. Used by interruptSelf.</summary>
    public AudioSource OldestOf(string id)
    {
        PooledSource oldest = null;
        for (int i = 0; i < _sources.Count; i++)
        {
            PooledSource candidate = _sources[i];
            if (!candidate.inUse || candidate.id != id)
            {
                continue;
            }
            if (oldest == null || candidate.releaseTime < oldest.releaseTime)
            {
                oldest = candidate;
            }
        }
        return oldest?.source;
    }

    public bool TryAcquire(string id, bool isLoop, float baseVolume, double expectedEnd,
                           out AudioSource source, out int generation)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (!_sources[i].inUse)
            {
                Occupy(_sources[i], id, isLoop, baseVolume, expectedEnd);
                source = _sources[i].source;
                generation = _sources[i].generation;
                return true;
            }
        }

        //everything busy: steal the oldest non-looping source. Stealing a loop would silence
        //music or ambience, which is far more noticeable than dropping one sound effect.
        PooledSource victim = null;
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].isLoop)
            {
                continue;
            }
            if (victim == null || _sources[i].releaseTime < victim.releaseTime)
            {
                victim = _sources[i];
            }
        }

        if (victim == null)
        {
            source = null;
            generation = 0;
            return false;
        }

        if (!_warnedExhausted)
        {
            _warnedExhausted = true;
            Debug.LogWarning($"[AudioPool] pool exhausted ({_sources.Count} sources): stealing " +
                             $"the source playing '{victim.id}' to play '{id}'. If this happens " +
                             "often, raise poolSize on the AudioManager or lower maxSimultaneous " +
                             "on the sounds that stack up. (Warned once per session.)");
        }

        victim.source.Stop();
        Occupy(victim, id, isLoop, baseVolume, expectedEnd);
        source = victim.source;
        generation = victim.generation;
        return true;
    }

    void Occupy(PooledSource pooled, string id, bool isLoop, float baseVolume, double expectedEnd)
    {
        pooled.inUse = true;
        pooled.id = id;
        pooled.isLoop = isLoop;
        pooled.baseVolume = baseVolume;
        pooled.releaseTime = expectedEnd;
        pooled.generation++;
    }

    /// <summary>
    /// Called every frame by AudioManager: returns finished one-shots to the pool. A poll rather
    /// than a coroutine per sound, on purpose -- EncounterManager calls
    /// AudioManager.instance.StopAllCoroutines() from outside, which would otherwise strand
    /// every source it had already handed out.
    /// </summary>
    public void ReclaimFinished()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            PooledSource pooled = _sources[i];
            if (pooled.inUse && !pooled.isLoop && !pooled.source.isPlaying)
            {
                Release(pooled);
            }
        }
    }

    void Release(PooledSource pooled)
    {
        pooled.inUse = false;
        pooled.id = null;
        pooled.isLoop = false;
        pooled.source.clip = null;
        pooled.source.loop = false;
        pooled.source.pitch = 1f;
        pooled.source.volume = 1f;
        pooled.source.spatialBlend = 0f;
        pooled.source.outputAudioMixerGroup = null;
        pooled.source.transform.localPosition = Vector3.zero;
    }

    public bool IsCurrent(AudioSource source, int generation)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].source == source)
            {
                return _sources[i].inUse && _sources[i].generation == generation;
            }
        }
        return false;
    }

    public void StopId(string id)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse && _sources[i].id == id)
            {
                _sources[i].source.Stop();
                Release(_sources[i]);
            }
        }
    }

    public void StopSource(AudioSource source)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].source == source)
            {
                _sources[i].source.Stop();
                Release(_sources[i]);
                return;
            }
        }
    }

    public void StopEverything()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse)
            {
                _sources[i].source.Stop();
                Release(_sources[i]);
            }
        }
    }

    public bool IsPlayingId(string id) => CountPlaying(id) > 0;

    public void SetMuted(bool muted)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            _sources[i].source.mute = muted;
        }
    }

    /// <summary>Re-applies a global multiplier over each live source's own entry volume.</summary>
    public void ApplyGlobalVolume(float globalVolume)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse)
            {
                _sources[i].source.volume = _sources[i].baseVolume * globalVolume;
            }
        }
    }

    public void ApplyVolumeToId(string id, float multiplier)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse && _sources[i].id == id)
            {
                _sources[i].source.volume = _sources[i].baseVolume * multiplier;
            }
        }
    }
}
