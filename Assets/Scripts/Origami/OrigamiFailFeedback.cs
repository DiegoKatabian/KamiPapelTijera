using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player sees when a fold goes wrong: the sheet flashes red for a split second and a red
/// "wrong fold, try again" message floats up from the sheet's centre and fades out.
///
/// Why it exists: a failed fold used to close the whole origami, so one slip threw away every fold
/// done so far and the paper's worth of effort with it. Now the origami stays open, only the failed
/// fold restarts (<see cref="Origami.RestartCurrentFold"/>), and this component is how the player
/// learns it happened.
///
/// It lives in CurrentFold.prefab, which every origami route already nests, so Level 1 and Level 2
/// get it with no per-route wiring. <see cref="Origami"/> finds it among its children.
/// </summary>
public class OrigamiFailFeedback : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("The floating message. Uses the cutscene font asset (the one with an outline) so red text reads on any background.")]
    [SerializeField] TMP_Text _message;

    [Tooltip("UITexts key of the message.")]
    [SerializeField] string _messageKey = "origami_fail_retry";

    [Tooltip("String table the key lives in.")]
    [SerializeField] string _messageTable = "UITexts";

    [Header("Sheet flash")]
    [Tooltip("Tint multiplied onto the sheet at the instant of the failure.")]
    [SerializeField] Color _flashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Tooltip("Seconds the tint takes to ease back to the sheet's normal colour.")]
    [SerializeField, Min(0.01f)] float _flashSeconds = 0.2f;

    [Header("Floating message")]
    [Tooltip("Colour of the message (alpha is the maximum opacity).")]
    [SerializeField] Color _messageColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("How far the message rises, in canvas pixels (reference resolution), over its whole life.")]
    [SerializeField, Min(0f)] float _risePixels = 140f;

    [Tooltip("Total seconds from appearing to being fully faded out.")]
    [SerializeField, Min(0.1f)] float _messageSeconds = 1.5f;

    [Tooltip("Fraction of the message's life it stays fully opaque before it starts to fade.")]
    [SerializeField, Range(0f, 0.9f)] float _holdFraction = 0.2f;

    Coroutine _flashRoutine;
    Coroutine _messageRoutine;

    //the sheet being tinted and the colour to give back. Kept so that a second failure in the middle of
    //a flash does not capture the red-ish colour as the "normal" one
    Graphic _flashedSheet;
    Color _sheetBaseColor;

    void Awake()
    {
        if (_message == null)
        {
            Debug.LogWarning($"[OrigamiFailFeedback] {name}: _message is not wired, the sheet will flash but no text will float up");
            return;
        }

        //hidden until a failure: the prefab keeps it off, this covers an object left on by hand
        _message.gameObject.SetActive(false);
    }

    /// <summary>
    /// Plays flash + message. Calling it again while it is still running restarts both, so repeated
    /// failures never stack messages.
    /// </summary>
    /// <param name="sheet">The image to flash (the route's flipbook). May be null: only the message plays.</param>
    /// <param name="sheetCenterWorld">World position the message rises from.</param>
    public void Play(Graphic sheet, Vector3 sheetCenterWorld)
    {
        ResetNow();

        Debug.Log($"[OrigamiFailFeedback] failed fold: flash {(sheet != null ? sheet.name : "(no sheet)")} + message '{_messageKey}'");

        if (sheet != null)
        {
            _flashRoutine = StartCoroutine(FlashRoutine(sheet));
        }

        if (_message != null)
        {
            _messageRoutine = StartCoroutine(MessageRoutine(sheetCenterWorld));
        }
    }

    /// <summary>
    /// Stops everything and puts the sheet and the message back as they were. The origami calls it when it
    /// closes: a coroutine stops when its object is switched off, which would leave the sheet tinted and
    /// the message half-faded for the next time the origami opens.
    /// </summary>
    public void ResetNow()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        if (_messageRoutine != null)
        {
            StopCoroutine(_messageRoutine);
            _messageRoutine = null;
        }

        if (_flashedSheet != null)
        {
            _flashedSheet.color = _sheetBaseColor;
        }
        _flashedSheet = null;

        if (_message != null)
        {
            _message.gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        ResetNow();
    }

    IEnumerator FlashRoutine(Graphic sheet)
    {
        _flashedSheet = sheet;
        _sheetBaseColor = sheet.color;
        sheet.color = _flashColor;

        for (float elapsed = 0f; elapsed < _flashSeconds; elapsed += Time.unscaledDeltaTime)
        {
            if (sheet == null)
            {
                yield break;
            }

            float t = elapsed / _flashSeconds;
            sheet.color = Color.Lerp(_flashColor, _sheetBaseColor, EaseOut(t));
            yield return null;
        }

        if (sheet != null)
        {
            sheet.color = _sheetBaseColor;
        }
        _flashedSheet = null;
        _flashRoutine = null;
    }

    IEnumerator MessageRoutine(Vector3 startWorld)
    {
        //invisible while the string loads: it is asynchronous, and the old text (or none) must not flicker
        _message.gameObject.SetActive(true);
        _message.color = new Color(_messageColor.r, _messageColor.g, _messageColor.b, 0f);

        //sinResaltado: the concept highlighter would wrap "fold" in its own colour tag and break the all-red look
        yield return StartCoroutine(LocalizedText.Escribir(_message, _messageKey, _messageTable, new LocalizedText.Opciones
        {
            avisarSinTabla = true,
            avisarSinClave = true,
            escribirSinTabla = true,
            sinResaltado = true,
            origen = "OrigamiFailFeedback"
        }));

        if (_message == null)
        {
            yield break;
        }

        //canvas pixels -> world units: the message sits under the canvas, whose scale maps one to the other
        Transform parent = _message.transform.parent;
        float worldPerPixel = parent != null ? parent.lossyScale.y : 1f;
        float riseWorld = _risePixels * worldPerPixel;

        for (float elapsed = 0f; elapsed < _messageSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / _messageSeconds;

            _message.transform.position = startWorld + Vector3.up * (riseWorld * EaseOut(t));

            //fully opaque during the hold, then a straight fade to nothing by the end
            float fadeT = Mathf.InverseLerp(_holdFraction, 1f, t);
            float alpha = _messageColor.a * (1f - fadeT);
            _message.color = new Color(_messageColor.r, _messageColor.g, _messageColor.b, alpha);

            yield return null;
        }

        _message.gameObject.SetActive(false);
        _messageRoutine = null;
    }

    //decelerating: the message drifts up quickly at first and settles, which reads as "floating"
    static float EaseOut(float t)
    {
        float inverse = 1f - Mathf.Clamp01(t);
        return 1f - inverse * inverse;
    }
}
