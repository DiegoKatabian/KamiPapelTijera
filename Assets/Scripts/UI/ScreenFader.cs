using System.Collections;
using UnityEngine;

/// <summary>
/// A full-screen black panel that fades in and out (Level 2's catapult ending: the boarding cut and
/// the final fade to black). Visibility is only the CanvasGroup's alpha, never SetActive, same rule
/// as the post-its: switching the GameObject off would kill a running fade.
///
/// Keep it the LAST child of its canvas so it draws over everything else.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    CanvasGroup _group;
    Coroutine _fade;

    public bool IsBlack => _group != null && _group.alpha >= 1f;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    /// <summary>To black over the given seconds. The coroutine can be yielded on.</summary>
    public Coroutine FadeOut(float seconds)
    {
        return FadeTo(1f, seconds);
    }

    /// <summary>Back to the game over the given seconds.</summary>
    public Coroutine FadeIn(float seconds)
    {
        return FadeTo(0f, seconds);
    }

    Coroutine FadeTo(float target, float seconds)
    {
        if (_fade != null)
        {
            StopCoroutine(_fade);
        }

        _fade = StartCoroutine(FadeRoutine(target, seconds));
        return _fade;
    }

    IEnumerator FadeRoutine(float target, float seconds)
    {
        float start = _group.alpha;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            _group.alpha = Mathf.Lerp(start, target, elapsed / Mathf.Max(0.01f, seconds));
            yield return null;
        }

        _group.alpha = target;
        _fade = null;
    }
}
