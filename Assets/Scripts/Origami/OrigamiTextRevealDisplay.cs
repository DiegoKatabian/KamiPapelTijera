using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Text panel for an Origami fold/unfold that ends in "read this" instead of "produce/spawn
/// something" (the trap letter, the cafe ticket). Same fade-by-CanvasGroup shape as
/// PedestalCanvasDisplay (see docs/claude/origami-y-tooltips.md): the canvas stays always
/// active, visibility is alpha only, so a stray SetActive can never race a running fade.
///
/// Reading flow, per Diego's 2026-09-22 design call: the text appears when the last fold
/// completes, the panel does NOT close on its own, and input is IGNORED for _readDelay
/// seconds so a player still holding the fold button can't blow past the text they just
/// earned. Only after that does the close prompt appear and Interact dismiss it -- the same
/// "read, then press to continue" rhythm as a dialogue line.
///
/// Content goes through LocalizedText (Assets/Scripts/UI/LocalizedText.cs), the project's
/// single funnel for localized TMP text, so the revealed text and the close prompt both get
/// device-correct button icons and highlighted-concept coloring for free.
/// </summary>
public class OrigamiTextRevealDisplay : MonoBehaviour
{
    [SerializeField] CanvasGroup _canvasGroup; //fallback: GetComponent in Awake
    [SerializeField] TMP_Text _revealedText;

    [Tooltip("Optional 'press X to close' line. Shown only once the read delay is over. Its text comes from _closePromptKey, so the button icon matches the active device.")]
    [SerializeField] TMP_Text _closePrompt;

    [Tooltip("Localization table (Assets/Localization Settings/Tables/) queried for the revealed text key.")]
    [SerializeField] string _tableName = "Level2_Dialogues";

    [Tooltip("Key for the close prompt line. Lives in UITexts so every reveal shares one string.")]
    [SerializeField] string _closePromptKey = "OrigamiReadClose";

    [Tooltip("Seconds of reading time before the close prompt appears and Interact is accepted. Stops the fold button press from closing the text in the same breath.")]
    [SerializeField] float _readDelay = 2f;

    [Tooltip("Fade-in duration in seconds")]
    [SerializeField] float _fadeInDuration = 0.3f;

    [Tooltip("Fade-out duration in seconds")]
    [SerializeField] float _fadeOutDuration = 0.3f;

    bool _visible;
    bool _canBeDismissed;
    Coroutine _fadeCoroutine;
    Coroutine _readDelayCoroutine;

    /// <summary>True while the panel is up — the origami/page flow can gate on this to know the player is still reading.</summary>
    public bool IsReading => _visible;

    void Awake()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: no CanvasGroup assigned or found, fades won't work");
        }
        else
        {
            //arranca invisible: el texto se muestra recien cuando ShowText lo pide
            _canvasGroup.alpha = 0f;
        }

        if (_closePrompt != null)
        {
            _closePrompt.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!_visible || !_canBeDismissed)
        {
            return;
        }

        if (InputHub.InteractDown)
        {
            Debug.Log($"[OrigamiTextRevealDisplay] {gameObject.name}: dismissed by the player");
            Hide();
        }
    }

    /// <summary>Fades the panel in and writes the localized text for <paramref name="localizationKey"/>.</summary>
    public void ShowText(string localizationKey)
    {
        if (string.IsNullOrEmpty(localizationKey))
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: ShowText called with an empty key, ignoring");
            return;
        }

        if (_revealedText == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: _revealedText is not assigned, can't show '{localizationKey}'");
            return;
        }

        _visible = true;
        _canBeDismissed = false;
        Debug.Log($"[OrigamiTextRevealDisplay] {gameObject.name}: showing '{localizationKey}' from table '{_tableName}', readable in {_readDelay:F1}s");

        StartCoroutine(LocalizedText.Escribir(_revealedText, localizationKey, _tableName, new LocalizedText.Opciones
        {
            escribirSinTabla = true,
            avisarSinTabla = true,
            avisarSinClave = true,
            origen = "OrigamiTextRevealDisplay"
        }));

        StartFade(1f, _fadeInDuration);

        if (_readDelayCoroutine != null)
        {
            StopCoroutine(_readDelayCoroutine);
        }
        _readDelayCoroutine = StartCoroutine(AllowDismissAfterReadDelay());
    }

    public void Hide()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;
        _canBeDismissed = false;

        if (_readDelayCoroutine != null)
        {
            StopCoroutine(_readDelayCoroutine);
            _readDelayCoroutine = null;
        }

        if (_closePrompt != null)
        {
            _closePrompt.gameObject.SetActive(false);
        }

        StartFade(0f, _fadeOutDuration);
    }

    IEnumerator AllowDismissAfterReadDelay()
    {
        yield return new WaitForSeconds(_readDelay);
        _readDelayCoroutine = null;
        _canBeDismissed = true;

        if (_closePrompt == null)
        {
            Debug.Log($"[OrigamiTextRevealDisplay] {gameObject.name}: readable now (no _closePrompt assigned, so no on-screen hint)");
            yield break;
        }

        _closePrompt.gameObject.SetActive(true);
        yield return LocalizedText.Escribir(_closePrompt, _closePromptKey, "UITexts", new LocalizedText.Opciones
        {
            escribirSinTabla = true,
            avisarSinTabla = true,
            avisarSinClave = true,
            origen = "OrigamiTextRevealDisplay"
        });
    }

    void StartFade(float targetAlpha, float duration)
    {
        if (_canvasGroup == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: no CanvasGroup, ignoring fade to {targetAlpha}");
            return;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        //partimos del alpha ACTUAL: si interrumpen un fade a mitad de camino, el nuevo arranca desde donde quedo
        float startAlpha = _canvasGroup.alpha;

        if (duration <= 0f)
        {
            _canvasGroup.alpha = targetAlpha;
            _fadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _fadeCoroutine = null;
    }
}
