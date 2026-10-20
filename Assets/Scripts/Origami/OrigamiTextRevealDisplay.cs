using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("The paper behind the text. Origamis can put their own picture on it (Ariel's letter, the cafe ticket); the others get it as authored here.")]
    [SerializeField] Image _paper;

    [Tooltip("Largest the paper grows to when it shows an origami's picture. It keeps the picture's proportions.")]
    [SerializeField] Vector2 _maxPaperSize = new Vector2(1000f, 860f);

    [Tooltip("Fade-in duration in seconds")]
    [SerializeField] float _fadeInDuration = 0.3f;

    [Tooltip("Fade-out duration in seconds")]
    [SerializeField] float _fadeOutDuration = 0.3f;

    Sprite _authoredPaperSprite;
    Color _authoredPaperColor;
    Vector2 _authoredPaperSize;

    bool _visible;
    bool _canBeDismissed;
    bool _lockedKami; //this panel took LevelManager.inCutscene, so only it gives it back
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

        if (_paper == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: _paper is not assigned, origamis can't show their own picture");
        }
        else
        {
            _authoredPaperSprite = _paper.sprite;
            _authoredPaperColor = _paper.color;
            _authoredPaperSize = _paper.rectTransform.sizeDelta;
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

    /// <summary>Fades the panel in and writes the localized text for <paramref name="localizationKey"/>
    /// (none when it is empty: the picture is the whole message) on <paramref name="paper"/> (null = the authored paper).</summary>
    public void Show(string localizationKey, Sprite paper)
    {
        bool hasText = !string.IsNullOrEmpty(localizationKey);
        if (!hasText && paper == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: Show called with neither a text key nor a picture, ignoring");
            return;
        }

        if (hasText && _revealedText == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: _revealedText is not assigned, can't show '{localizationKey}'");
            return;
        }

        _visible = true;
        _canBeDismissed = false;
        SetKamiLocked(true);
        SetPaper(paper);
        Debug.Log($"[OrigamiTextRevealDisplay] {gameObject.name}: showing '{(hasText ? localizationKey : "(picture only)")}' on {(paper != null ? paper.name : "the plain paper")}, readable in {_readDelay:F1}s");

        if (hasText)
        {
            StartCoroutine(LocalizedText.Escribir(_revealedText, localizationKey, _tableName, new LocalizedText.Opciones
            {
                escribirSinTabla = true,
                avisarSinTabla = true,
                avisarSinClave = true,
                origen = "OrigamiTextRevealDisplay"
            }));
        }
        else if (_revealedText != null)
        {
            //out of LocalizedText's registry too, or a device change would write the last reveal back
            LocalizedText.Limpiar(_revealedText);
            _revealedText.text = "";
        }

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
        SetKamiLocked(false);

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

    //A picture keeps its proportions inside _maxPaperSize, shown untinted; no picture = the paper as authored
    void SetPaper(Sprite picture)
    {
        if (_paper == null)
        {
            return;
        }

        if (picture == null)
        {
            _paper.sprite = _authoredPaperSprite;
            _paper.color = _authoredPaperColor;
            _paper.rectTransform.sizeDelta = _authoredPaperSize;
            return;
        }

        Vector2 size = picture.rect.size;
        float fit = Mathf.Min(_maxPaperSize.x / size.x, _maxPaperSize.y / size.y);
        _paper.sprite = picture;
        _paper.color = Color.white;
        _paper.rectTransform.sizeDelta = size * fit;
    }

    //Kami stays put while she reads (#172), like in a dialogue. inCutscene, the lock AbuelaEntrance and the
    //cutscenes use, not inDialogue: DialogueManager clears that one whenever any dialogue ends. The dismiss
    //press still works, it is read straight from InputHub in Update. Same "only release what I took" rule
    //as AbuelaEntrance, so a cutscene that locked her first is never cut short.
    void SetKamiLocked(bool locked)
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null)
        {
            Debug.LogWarning($"[OrigamiTextRevealDisplay] {gameObject.name}: no LevelManager, Kami is not locked while reading");
            return;
        }

        if (locked)
        {
            if (!levelManager.inCutscene)
            {
                levelManager.inCutscene = true;
                _lockedKami = true;
            }
            return;
        }

        if (_lockedKami)
        {
            levelManager.inCutscene = false;
            _lockedKami = false;
        }
    }

    void OnDisable()
    {
        //switched off mid-read: never leave Kami frozen
        if (_lockedKami)
        {
            SetKamiLocked(false);
        }
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
