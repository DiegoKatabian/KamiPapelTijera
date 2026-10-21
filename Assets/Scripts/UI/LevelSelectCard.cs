using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The level select's hover/selection feedback: the card grows a little while the mouse is over it or the
/// joystick has it selected, and eases back when neither. Scale only, never Select(): highlighting must not
/// move the EventSystem's focus (see "Fugas de foco" in docs/claude/controles-y-gamepad.md).
/// </summary>
public class LevelSelectCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField, Tooltip("Scale while hovered or selected (1 = no change).")]
    float _highlightScale = 1.05f;

    [SerializeField, Tooltip("How fast the scale eases toward its target (higher = snappier).")]
    float _easeSpeed = 12f;

    Vector3 _baseScale;
    bool _hovered;
    bool _selected;

    void Awake()
    {
        _baseScale = transform.localScale;
    }

    void OnDisable()
    {
        _hovered = false;
        _selected = false;
        transform.localScale = _baseScale;
    }

    void Update()
    {
        float target = (_hovered || _selected) ? _highlightScale : 1f;
        //unscaled: the menu never pauses time, but this must not depend on it
        float blend = 1f - Mathf.Exp(-_easeSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * target, blend);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
    }
}
