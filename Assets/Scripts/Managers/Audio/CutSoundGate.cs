using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cut sounds (TijeraHit, PaperCut) go through here instead of AudioManager.Play. Every cuttable
/// plays its own sound, so one swing through two bushes used to start the same clip twice at the same
/// instant and the two copies phased into a doubled thump (spec 024 1.B). Cuts of one id that land
/// within the burst window are one burst: the first plays as always, the next ones are delayed a few
/// milliseconds (that is what breaks the phasing) and pitched up, and every voice of the burst is scaled
/// to 1/sqrt(N) so the sum is about as loud as one cut. Lives on AudioManager.prefab.
/// </summary>
public class CutSoundGate : MonoBehaviour
{
    [SerializeField, Tooltip("Seconds after a burst's first cut during which further cuts of the same sound join that burst.")]
    float _burstWindow = 0.1f;

    [SerializeField, Tooltip("Voices per burst; cuts beyond this play no extra sound.")]
    int _maxVoices = 3;

    [SerializeField, Tooltip("Shortest delay (seconds) of the 2nd and later voices of a burst.")]
    float _minDelay = 0.015f;

    [SerializeField, Tooltip("Longest delay (seconds) of the 2nd and later voices of a burst.")]
    float _maxDelay = 0.04f;

    [SerializeField, Tooltip("Pitch shift in semitones per voice of a burst (index 0 = the first cut). Missing entries reuse the last one.")]
    float[] _semitones = { 0f, 2f, 4f };

    class Burst
    {
        public float startTime;
        public int count;
        public float gain = 1f; //what every voice of the burst plays at now, 1/sqrt(count)
        public readonly List<SoundHandle> live = new List<SoundHandle>();
    }

    struct PendingVoice
    {
        public string id;
        public float playAt;
        public float pitchRatio;
        public Burst burst;
    }

    static CutSoundGate _instance;
    static bool _missingWarned;

    readonly Dictionary<string, Burst> _bursts = new Dictionary<string, Burst>();
    readonly List<PendingVoice> _pending = new List<PendingVoice>();

    /// <summary>Plays a cut sound, thinned if other cuts of the same sound landed a moment ago.</summary>
    public static void Play(string id)
    {
        if (AudioManager.instance == null)
        {
            return;
        }
        if (_instance == null)
        {
            if (!_missingWarned)
            {
                _missingWarned = true;
                Debug.LogWarning("[CutSoundGate] no CutSoundGate on the AudioManager: cut sounds play unthinned.");
            }
            AudioManager.instance.Play(id);
            return;
        }
        _instance.Enqueue(id);
    }

    void Awake()
    {
        //a duplicate AudioManager destroys its whole object at the end of this frame: keep the first gate
        if (_instance != null && _instance != this)
        {
            enabled = false;
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    void Enqueue(string id)
    {
        //unscaled: a voice queued just before the Flap pauses the game must not hang until it closes
        float now = Time.unscaledTime;
        if (!_bursts.TryGetValue(id, out Burst burst) || now - burst.startTime > _burstWindow)
        {
            burst = new Burst { startTime = now };
            _bursts[id] = burst;
        }

        if (burst.count >= _maxVoices)
        {
            return; //enough voices already: a fourth copy would only add level
        }

        int index = burst.count;
        burst.count++;
        float newGain = 1f / Mathf.Sqrt(burst.count);
        if (burst.count > 1)
        {
            //incoherent copies add by power, so N voices at 1/sqrt(N) sound about as loud as one
            float factor = newGain / burst.gain;
            foreach (SoundHandle handle in burst.live)
            {
                AudioManager.instance.ScaleVolume(handle, factor);
            }
            Debug.Log($"[CutSoundGate] '{id}' burst now {burst.count} voices, each at {newGain:0.00}");
        }
        burst.gain = newGain;

        float semitones = _semitones != null && _semitones.Length > 0
            ? _semitones[Mathf.Min(index, _semitones.Length - 1)]
            : 0f;
        float pitchRatio = Mathf.Pow(2f, semitones / 12f);

        if (index == 0)
        {
            PlayVoice(id, pitchRatio, burst);
            return;
        }

        _pending.Add(new PendingVoice
        {
            id = id,
            playAt = now + Random.Range(_minDelay, _maxDelay),
            pitchRatio = pitchRatio,
            burst = burst
        });
    }

    void Update()
    {
        if (_pending.Count == 0)
        {
            return;
        }
        float now = Time.unscaledTime;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            PendingVoice voice = _pending[i];
            if (voice.playAt > now)
            {
                continue;
            }
            _pending.RemoveAt(i);
            PlayVoice(voice.id, voice.pitchRatio, voice.burst);
        }
    }

    void PlayVoice(string id, float pitchRatio, Burst burst)
    {
        if (AudioManager.instance == null)
        {
            return;
        }

        SoundHandle handle;
        if (Mathf.Approximately(pitchRatio, 1f) || AudioBank.Instance == null ||
            !AudioBank.Instance.TryGet(id, out SoundEntry entry))
        {
            handle = AudioManager.instance.Play(id);
        }
        else
        {
            //the row's own pitch spread still applies around the shifted pitch
            handle = AudioManager.instance.Play(id, entry.pitch * pitchRatio);
        }

        if (!Mathf.Approximately(burst.gain, 1f))
        {
            AudioManager.instance.ScaleVolume(handle, burst.gain);
        }
        burst.live.Add(handle);
    }
}
