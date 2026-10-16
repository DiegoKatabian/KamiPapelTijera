using UnityEngine;

/// <summary>
/// The siren on a police car: the lights spin because a looping Animator clip turns their parent, so this
/// script never runs per frame. It only applies the turn speed, and switches the lights and the
/// Animator on and off (the cutscenes call <see cref="SetSiren"/>).
/// </summary>
public class PoliceLights : MonoBehaviour
{
    [SerializeField, Tooltip("The Animator that spins the lights (PoliceLights_Spin, 1 second per turn at speed 1).")]
    Animator _animator;

    [SerializeField, Tooltip("The spot lights, switched off together with the spin.")]
    Light[] _lights;

    [SerializeField, Tooltip("Seconds for one full turn.")]
    float _spinSeconds = 1f;

    [SerializeField, Tooltip("Siren running when the car appears. Off for the parked patrol cars (a cutscene turns it on), on for the one at the crime scene.")]
    bool _onAtStart;

    void OnEnable()
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[PoliceLights] {name}: no _animator assigned, the lights will not spin");
        }
        else if (_spinSeconds > 0f)
        {
            //the clip is authored at one turn per second
            _animator.speed = 1f / _spinSeconds;
        }
        else
        {
            Debug.LogWarning($"[PoliceLights] {name}: _spinSeconds must be above 0, keeping the clip's own speed");
        }

        SetSiren(_onAtStart);
    }

    public void SetSiren(bool on)
    {
        if (_animator != null)
        {
            //the cutscenes park the car by disabling every Animator on it; the siren's is ours to run
            _animator.enabled = on;
        }

        if (_lights == null || _lights.Length == 0)
        {
            Debug.LogWarning($"[PoliceLights] {name}: no _lights assigned");
            return;
        }

        foreach (Light light in _lights)
        {
            if (light != null)
            {
                light.enabled = on;
            }
        }
    }
}
