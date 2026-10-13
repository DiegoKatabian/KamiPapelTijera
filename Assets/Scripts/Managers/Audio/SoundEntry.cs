using System;
using UnityEngine;

/// <summary>
/// Everything about one sound: which clips it can use and how it plays. This replaces a child
/// GameObject with an AudioSource in AudioManager.prefab, which is why the field defaults mirror
/// Unity's AudioSource defaults -- AudioBankBuilder copies the prefab values over them.
/// </summary>
[Serializable]
public class SoundEntry
{
    [Tooltip("Id used from code, via the generated AudioId constants. Matches the old child " +
             "GameObject name in AudioManager.prefab, which is why the migration is mechanical.")]
    public string id;

    [Tooltip("More than one clip = a random one is picked per play. This replaces PlayRandom.")]
    public AudioClip[] clips = new AudioClip[0];

    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitch = 1f;

    [Tooltip("Random pitch spread around pitch, applied per play. 0 = always exactly pitch.")]
    [Range(0f, 1f)] public float pitchVariation = 0f;

    public bool loop;
    public AudioBus bus = AudioBus.SFX;

    [Tooltip("Max instances of THIS id playing at once. 0 = unlimited. Keeps footsteps and " +
             "rapid-fire UI sounds from stacking into noise.")]
    public int maxSimultaneous = 4;

    [Tooltip("0 = 2D (the default for almost everything here), 1 = fully positional.")]
    [Range(0f, 1f)] public float spatialBlend = 0f;

    [Tooltip("Positional sounds only: full volume within this distance of the listener. The " +
             "listener is on the camera, 13-45 units from Kami, so a 3D sound meant to be heard " +
             "near her needs a min distance around that size. Default = Unity's AudioSource default.")]
    public float minDistance = 1f;

    [Tooltip("Positional sounds only: with linear rolloff, silent past this distance.")]
    public float maxDistance = 500f;

    [Tooltip("Positional sounds only: fade linearly from min to max distance. Off = Unity's " +
             "logarithmic rolloff, which is nearly silent at camera distance with a small min.")]
    public bool linearRolloff;

    [Range(0, 256)] public int priority = 128;

    [Tooltip("If already at maxSimultaneous, stop the oldest instance of this id instead of " +
             "dropping the new play.")]
    public bool interruptSelf;

    /// <summary>Random clip, or null (with no log -- the caller reports) if there are none.</summary>
    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }
        if (clips.Length == 1)
        {
            return clips[0];
        }
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }
}
