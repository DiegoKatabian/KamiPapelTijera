using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small "hold to skip" prompt for cutscenes: the Interact key's icon inside a ring that fills while the key is held
/// (Diego, 2026-11-09). Hidden until the player presses; it fades in on the press, fills with the hold, drains when let go
/// and fades out after a moment without a press. The cutscene owns the hold and calls SetHold every frame.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HoldToSkipPrompt : MonoBehaviour
{
    [SerializeField, Tooltip("The ring that fills (an Image of type Filled, Radial 360).")]
    Image _fill;

    [SerializeField, Tooltip("Shows the Interact key's icon for the device in use.")]
    TMP_Text _icon;

    [SerializeField, Tooltip("Seconds to appear after the first press.")]
    float _fadeInSeconds = 0.15f;

    [SerializeField, Tooltip("Seconds it stays after the key is let go, before fading out.")]
    float _lingerSeconds = 1.2f;

    [SerializeField, Tooltip("Seconds to fade out.")]
    float _fadeOutSeconds = 0.4f;

    [SerializeField, Tooltip("How fast the ring empties when the key is let go (fills per second).")]
    float _drainPerSecond = 3f;

    CanvasGroup _group;
    float _shown;      // the ring's current fill
    float _lastHeldAt = float.NegativeInfinity;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        if (_fill == null)
        {
            Debug.LogWarning($"[HoldToSkipPrompt] {gameObject.name}: no fill ring assigned, the prompt shows no progress");
        }
    }

    void OnEnable()
    {
        WriteIcon();
        InputHub.OnDeviceCambio += WriteIcon;
    }

    void OnDisable()
    {
        InputHub.OnDeviceCambio -= WriteIcon;
    }

    void WriteIcon()
    {
        if (_icon != null)
        {
            _icon.text = InputPromptSystem.Procesar("{INPUT:accion}");
        }
    }

    /// <summary>Call every frame while the cutscene can be skipped. progress = hold time / hold needed (0-1).</summary>
    public void SetHold(float progress, bool held)
    {
        float dt = Time.unscaledDeltaTime;
        if (held)
        {
            _lastHeldAt = Time.unscaledTime;
            _shown = Mathf.Clamp01(progress);
        }
        else
        {
            _shown = Mathf.MoveTowards(_shown, 0f, _drainPerSecond * dt);
        }
        if (_fill != null)
        {
            _fill.fillAmount = _shown;
        }

        bool visible = Time.unscaledTime - _lastHeldAt < _lingerSeconds;
        float target = visible ? 1f : 0f;
        float seconds = visible ? _fadeInSeconds : _fadeOutSeconds;
        _group.alpha = Mathf.MoveTowards(_group.alpha, target, dt / Mathf.Max(0.01f, seconds));
    }

    /// <summary>Gone at once (the cutscene ended or was skipped).</summary>
    public void Hide()
    {
        _shown = 0f;
        _lastHeldAt = float.NegativeInfinity;
        if (_group != null)
        {
            _group.alpha = 0f;
        }
        if (_fill != null)
        {
            _fill.fillAmount = 0f;
        }
    }
}
