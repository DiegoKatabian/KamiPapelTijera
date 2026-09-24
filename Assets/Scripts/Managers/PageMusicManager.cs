using System.Collections;
using UnityEngine;

/// <summary>
/// Picks the music loop per page. One entry per page, in page order (index 0 = page 1); an
/// empty id means silence on that page. Level 2: Bohren on pages 1-2, silence on page 3, its
/// own track on page 4 (empty until that music exists), Bohren again on page 5.
///
/// It only switches when the target is not already playing, so walking between two pages
/// that share a track never restarts it.
/// </summary>
public class PageMusicManager : MonoBehaviour
{
    [SerializeField, Tooltip("Music id per page (an AudioId constant name, e.g. BohrenDestroyingAngels), index 0 = page 1. Empty = silence on that page. Pages past the end of the list are silent too.")]
    string[] _musicPerPage;

    void Start()
    {
        EventManager.Subscribe(Evento.OnNewPageOpen, OnNewPageOpen);
        StartCoroutine(ApplyAfterLevelStart());
    }

    //LevelManager.Start plays the level's opening track; running a frame later makes sure a
    //test started on a silent page (page 3) really ends up silent, whatever the Start order
    IEnumerator ApplyAfterLevelStart()
    {
        yield return null;
        ApplyForActivePage();
    }

    void OnNewPageOpen(params object[] parameters)
    {
        //parameters[0] carries a +1 offset by convention (see PageScrollerManager); read the real index
        ApplyForActivePage();
    }

    void ApplyForActivePage()
    {
        if (PageScrollerManager.Instance == null || AudioManager.instance == null)
        {
            Debug.LogWarning("[PageMusicManager] no PageScrollerManager or AudioManager in the scene, cannot pick the page music.");
            return;
        }

        if (_musicPerPage == null || _musicPerPage.Length == 0)
        {
            Debug.LogWarning("[PageMusicManager] _musicPerPage is empty, nothing to play.");
            return;
        }

        int page = PageScrollerManager.Instance.activePageIndex;
        string target = page >= 0 && page < _musicPerPage.Length ? _musicPerPage[page] : "";

        //stop every other track this manager knows about
        foreach (string id in _musicPerPage)
        {
            if (!string.IsNullOrEmpty(id) && id != target && AudioManager.instance.IsPlaying(id))
            {
                AudioManager.instance.StopById(id);
            }
        }

        if (string.IsNullOrEmpty(target))
        {
            Debug.Log($"[PageMusicManager] page {page + 1}: silence");
            return;
        }

        if (!AudioManager.instance.IsPlaying(target))
        {
            Debug.Log($"[PageMusicManager] page {page + 1}: playing {target}");
            AudioManager.instance.Play(target);
        }
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnNewPageOpen, OnNewPageOpen);
    }
}
