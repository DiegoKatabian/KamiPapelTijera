using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The build's version number (Player Settings > Version) as small, quiet text in the bottom-left corner.
/// It builds its own overlay canvas, so dropping this component on an empty GameObject in the main menu is
/// all the setup it needs, and it can't disturb the menu's own canvas layout. Plain text on purpose: a
/// version string is not localized and must not go through the text-highlight/prompt funnel.
/// </summary>
public class VersionLabel : MonoBehaviour
{
    [Tooltip("Written before the version number.")]
    [SerializeField] string _prefix = "v";

    [Tooltip("Font size in canvas units (the canvas scales to a 1920x1080 reference).")]
    [SerializeField] float _fontSize = 16f;

    [Tooltip("Distance from the left and bottom edges, in canvas units.")]
    [SerializeField] Vector2 _margin = new Vector2(14f, 10f);

    [Range(0f, 1f)]
    [Tooltip("How visible the text is. Low on purpose: it is there for bug reports, not for the player to read.")]
    [SerializeField] float _opacity = 0.55f;

    [Tooltip("Optional. Empty = TextMeshPro's default font.")]
    [SerializeField] TMP_FontAsset _font;

    void Start()
    {
        GameObject canvasObject = new GameObject("Version Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("Version Text");
        textObject.transform.SetParent(canvasObject.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (_font != null)
        {
            text.font = _font;
        }
        text.text = _prefix + Application.version;
        text.fontSize = _fontSize;
        text.color = new Color(1f, 1f, 1f, _opacity);
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.raycastTarget = false; //never in the way of a menu button

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = _margin;
        rect.sizeDelta = new Vector2(400f, _fontSize * 2f);

        Debug.Log($"[VersionLabel] showing '{text.text}'");
    }
}
