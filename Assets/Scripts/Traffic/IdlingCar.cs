using System.Collections;
using UnityEngine;

/// <summary>
/// A parked car stuck in a traffic jam (Level 2 page 3, #177): its engine runs and the driver
/// honks now and then. Both sounds are positional, played from the car through the AudioManager
/// pool; how far away they are heard is set on their AudioBank rows (min/max distance, linear
/// rolloff), not here. The exhaust smoke is the car prefab's own particle system.
///
/// Runs only while its page is active: the page folder switching off stops the engine.
/// </summary>
public class IdlingCar : MonoBehaviour
{
    [SerializeField, Tooltip("Looping engine sound (an AudioId constant name). Empty = no engine.")]
    string _engineLoop = AudioId.CarEngineIdleLoop;

    [SerializeField, Tooltip("Horn sound (an AudioId constant name). Empty = the driver never honks.")]
    string _horn = AudioId.CarHornIdling;

    [SerializeField, Tooltip("Shortest wait between two honks of this car, in seconds.")]
    float _hornMinSeconds = 20f;

    [SerializeField, Tooltip("Longest wait between two honks of this car, in seconds. The first honk waits a random time up to this too, so the cars of a jam don't honk together.")]
    float _hornMaxSeconds = 45f;

    SoundHandle _engine;

    void OnEnable()
    {
        StartCoroutine(Run());
    }

    void OnDisable()
    {
        StopEngine();
    }

    IEnumerator Run()
    {
        //a frame first: at scene start AudioManager may not have set itself up yet
        yield return null;

        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[IdlingCar] {gameObject.name}: no AudioManager, the car stays silent");
            yield break;
        }

        if (!string.IsNullOrEmpty(_engineLoop))
        {
            _engine = AudioManager.instance.PlayAt(_engineLoop, transform.position);
        }

        if (string.IsNullOrEmpty(_horn))
        {
            yield break;
        }

        float min = Mathf.Max(0.5f, _hornMinSeconds);
        float max = Mathf.Max(min, _hornMaxSeconds);
        yield return new WaitForSeconds(Random.Range(0f, max));

        while (true)
        {
            AudioManager.instance.PlayAt(_horn, transform.position);
            yield return new WaitForSeconds(Random.Range(min, max));
        }
    }

    void StopEngine()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Stop(_engine); //a stale handle stops nothing, by design
        }
        _engine = SoundHandle.None;
    }
}
