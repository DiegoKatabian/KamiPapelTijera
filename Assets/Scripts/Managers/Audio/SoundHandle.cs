using UnityEngine;

/// <summary>
/// A reference to one playing instance. The generation counter is the point: pooled sources get
/// reused, so a stale handle held by some coroutine must NOT be able to stop whatever is playing
/// on that source now. AudioPool bumps the generation on every acquire.
/// </summary>
public struct SoundHandle
{
    public readonly AudioSource source;
    public readonly int generation;

    public SoundHandle(AudioSource source, int generation)
    {
        this.source = source;
        this.generation = generation;
    }

    public bool IsValid => source != null;

    public static SoundHandle None => new SoundHandle(null, 0);
}
