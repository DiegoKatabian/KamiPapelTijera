using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The build's version number (Player Settings > Version) as small, quiet text in the bottom-left corner,
/// shown only while the kimmiarts logo is: its alpha follows the logo image, so it fades in and dissolves
/// with it, and once the logo is gone the label switches itself off for good (never over Kami or the intro
/// dialogue). Both references are authored in the main menu scene. Plain text on purpose: a version string
/// is not localized and must not go through the text-highlight/prompt funnel.
/// </summary>
public class VersionLabel : MonoBehaviour
{
    [SerializeField, Tooltip("The label itself (the TextMeshPro on this GameObject).")]
    TMP_Text _text;

    [SerializeField, Tooltip("The kimmiarts logo image: the label is exactly as visible as this is.")]
    Graphic _followLogo;

    [SerializeField, Tooltip("Written before the version number.")]
    string _prefix = "v";

    [Range(0f, 1f)]
    [SerializeField, Tooltip("How visible the label is while the logo is fully visible. Low on purpose: it is there for bug reports.")]
    float _opacity = 0.55f;

    bool _wasVisible;

    void Awake()
    {
        if (_text == null || _followLogo == null)
        {
            Debug.LogWarning("[VersionLabel] _text or _followLogo is not assigned: the version label stays hidden");
            enabled = false;
            if (_text != null)
            {
                _text.alpha = 0f;
            }
            return;
        }

        _text.text = _prefix + Application.version;
        _text.alpha = 0f;
        Debug.Log($"[VersionLabel] '{_text.text}', follows '{_followLogo.name}'");
    }

    //after the logo's own fade scripts have written this frame's alpha
    void LateUpdate()
    {
        float logoAlpha = _followLogo.color.a;
        _text.alpha = logoAlpha * _opacity;

        //the logo's two fade scripts fight for its first frame (one sets alpha 1, the other restarts the
        //fade-in from 0), so "it has been visible" only counts once it is clearly up, after that first moment
        if (logoAlpha > 0.5f && Time.timeSinceLevelLoad > 0.5f)
        {
            _wasVisible = true;
        }
        else if (_wasVisible && logoAlpha < 0.05f)
        {
            gameObject.SetActive(false); //the logo has dissolved: done for this session
        }
    }
}
