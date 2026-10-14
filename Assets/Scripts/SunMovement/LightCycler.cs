using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct LightState
{
    public Vector3 lightPosition;
    public Vector3 lightRotation;
    public Color lightColor;
    public float lightIntensity;
}

public class LightCycler : MonoBehaviour
{
    private new Light light; //la luz que se va a mover en cuestion
    [SerializeField] LightState[] _lightStates; //los distintos tipos de luz que va a haber en cada pag
    LightState _currentLightState;

    [Tooltip("Seconds the light takes to move to the next page's state.")]
    [SerializeField] float _transitionDuration = 2f;

    // One handle for the running transition: a new page turn must replace it, not fight it.
    private Coroutine _transition;

    private void Start()
    {
        light = GetComponent<Light>();
        if (light == null)
        {
            Debug.LogWarning("[LightCycler] No Light component on this GameObject, the cycler does nothing.");
            return;
        }

        if (_lightStates == null || _lightStates.Length == 0)
        {
            Debug.LogWarning("[LightCycler] _lightStates is empty, the cycler does nothing.");
            return;
        }

        // Instant on start: the scene's authored light is just a placeholder for the starting page.
        if (PageScrollerManager.Instance != null)
        {
            ApplyPageState(PageScrollerManager.Instance.activePageIndex, false);
        }
        else
        {
            Debug.LogWarning("[LightCycler] No PageScrollerManager yet, starting with the first light state.");
            ApplyPageState(0, false);
        }

        EventManager.Subscribe(Evento.OnPageTurnStart, OnChangeLightState); //param0 es activeindex
    }

    public void OnChangeLightState(params object[] parameters)
    {
        if (parameters == null || parameters.Length == 0 || !(parameters[0] is int))
        {
            Debug.LogWarning("[LightCycler] OnPageTurnStart arrived without an int page index, keeping the current light.");
            return;
        }

        ApplyPageState((int)parameters[0], true);
    }

    private void ApplyPageState(int pageIndex, bool animate)
    {
        if (light == null || _lightStates == null || _lightStates.Length == 0)
        {
            return;
        }

        if (pageIndex < 0 || pageIndex >= _lightStates.Length)
        {
            Debug.LogWarning($"[LightCycler] Page {pageIndex} has no light state (only {_lightStates.Length} configured), keeping the current light.");
            return;
        }

        _currentLightState = _lightStates[pageIndex];
        Debug.Log($"[LightCycler] Page {pageIndex}: applying its light state ({(animate ? "transition" : "instant")}).");

        if (_transition != null)
        {
            StopCoroutine(_transition);
            _transition = null;
        }

        if (!animate || _transitionDuration <= 0f || !isActiveAndEnabled)
        {
            SetLightState(_currentLightState);
            return;
        }

        _transition = StartCoroutine(TransitionCoroutine(_currentLightState, _transitionDuration));
    }

    private void SetLightState(LightState state)
    {
        transform.position = state.lightPosition;
        transform.rotation = Quaternion.Euler(state.lightRotation);
        light.color = state.lightColor;
        light.intensity = state.lightIntensity;
    }

    private IEnumerator TransitionCoroutine(LightState target, float duration)
    {
        // Read from the light as it is now, so interrupting a transition continues from where it stopped.
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Color startColor = light.color;
        float startIntensity = light.intensity;
        Quaternion targetRotation = Quaternion.Euler(target.lightRotation);

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = Mathf.SmoothStep(0, 1, elapsedTime / duration);
            transform.position = Vector3.Lerp(startPosition, target.lightPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            light.color = Color.Lerp(startColor, target.lightColor, t);
            light.intensity = Mathf.Lerp(startIntensity, target.lightIntensity, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        SetLightState(target);
        _transition = null;
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPageTurnStart, OnChangeLightState);
        }
    }
}
