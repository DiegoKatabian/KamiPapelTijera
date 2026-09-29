using System.Collections;
using UnityEngine;

/// <summary>
/// Picks the ambience loop per page (sibling of PageMusicManager, but for the Ambience bus, so the
/// music and the room tone never fight over one slot). One entry per page, in page order
/// (index 0 = page 1); an empty id means no ambience on that page. Level 2: the café's murmur on
/// page 1, the jazz bar's on page 3.
///
/// It only switches when the target is not already playing, and stops the other ids it owns, so a
/// page that shares its ambience with the previous one never restarts the loop.
/// </summary>
public class PageAmbience : MonoBehaviour
{
    [SerializeField, Tooltip("Ambience id per page (an AudioId constant name, e.g. CafeAmbience), index 0 = page 1. Empty = none on that page. Pages past the end of the list have none either.")]
    string[] _ambiencePerPage = { AudioId.CafeAmbience, "", AudioId.BarAmbience, "", "" };

    void Start()
    {
        EventManager.Subscribe(Evento.OnNewPageOpen, OnNewPageOpen);
        StartCoroutine(ApplyAfterLevelStart());
    }

    //same one-frame wait as PageMusicManager: PageScrollerManager.startingPage is applied during
    //start, so the first read of activePageIndex must come after it
    IEnumerator ApplyAfterLevelStart()
    {
        yield return null;
        ApplyForActivePage();
    }

    void OnNewPageOpen(params object[] parameters)
    {
        ApplyForActivePage();
    }

    void ApplyForActivePage()
    {
        if (PageScrollerManager.Instance == null || AudioManager.instance == null)
        {
            Debug.LogWarning("[PageAmbience] no PageScrollerManager or AudioManager in the scene, cannot pick the page ambience.");
            return;
        }

        if (_ambiencePerPage == null || _ambiencePerPage.Length == 0)
        {
            Debug.LogWarning("[PageAmbience] _ambiencePerPage is empty, nothing to play.");
            return;
        }

        int page = PageScrollerManager.Instance.activePageIndex;
        string target = page >= 0 && page < _ambiencePerPage.Length ? _ambiencePerPage[page] : "";

        StopAllExcept(target);

        if (string.IsNullOrEmpty(target))
        {
            Debug.Log($"[PageAmbience] page {page + 1}: no ambience");
            return;
        }

        if (!AudioManager.instance.IsPlaying(target))
        {
            Debug.Log($"[PageAmbience] page {page + 1}: playing {target}");
            AudioManager.instance.Play(target);
        }
    }

    void StopAllExcept(string keep)
    {
        if (_ambiencePerPage == null || AudioManager.instance == null)
        {
            return;
        }

        foreach (string id in _ambiencePerPage)
        {
            if (!string.IsNullOrEmpty(id) && id != keep && AudioManager.instance.IsPlaying(id))
            {
                AudioManager.instance.StopById(id);
            }
        }
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnNewPageOpen, OnNewPageOpen);

        //a loop must not leak into the next scene (the pool outlives this one)
        if (!gameObject.scene.isLoaded)
        {
            StopAllExcept("");
        }
    }
}
