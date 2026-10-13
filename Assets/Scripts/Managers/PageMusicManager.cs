using System.Collections;
using UnityEngine;

/// <summary>
/// Picks the music loop per page. One entry per page, in page order (index 0 = page 1); an
/// empty id means silence on that page. Level 2: Bohren on pages 1-2, silence on page 3,
/// DeadEndAngels on page 4, Bohren again on page 5.
///
/// A story track can override every page for a stretch of the story (Level 2: DeadEndAngels from
/// the arrest until the painting is returned), so walking between pages during the escape never
/// switches back to the page's own music.
///
/// It only switches when the target is not already playing, so walking between two pages
/// that share a track never restarts it.
/// </summary>
public class PageMusicManager : MonoBehaviour
{
    [SerializeField, Tooltip("Music id per page (an AudioId constant name, e.g. BohrenDestroyingAngels), index 0 = page 1. Empty = silence on that page. Pages past the end of the list are silent too.")]
    string[] _musicPerPage;

    [Header("Story music")]
    [SerializeField, Tooltip("While the story music is on, every page plays this instead of its own track. Empty = no story music.")]
    string _storyMusic;

    [SerializeField, Tooltip("The event that starts the story music.")]
    Evento _storyMusicStartsOn = Evento.OnArrestStarted;

    [SerializeField, Tooltip("The event that ends it: from then on each page plays its own track again.")]
    Evento _storyMusicEndsOn = Evento.OnPaintingReturned;

    bool _storyMusicOn;
    bool _storyMusicDone; //latched: the start event firing again later must not bring it back

    void Start()
    {
        EventManager.Subscribe(Evento.OnNewPageOpen, OnNewPageOpen);

        if (!string.IsNullOrEmpty(_storyMusic))
        {
            EventManager.Subscribe(_storyMusicStartsOn, OnStoryMusicStarts);
            EventManager.Subscribe(_storyMusicEndsOn, OnStoryMusicEnds);
        }

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

    void OnStoryMusicStarts(params object[] parameters)
    {
        if (_storyMusicOn || _storyMusicDone)
        {
            return;
        }

        _storyMusicOn = true;
        Debug.Log($"[PageMusicManager] {_storyMusicStartsOn}: story music {_storyMusic} on every page");
        ApplyForActivePage();
    }

    void OnStoryMusicEnds(params object[] parameters)
    {
        if (!_storyMusicOn)
        {
            return;
        }

        _storyMusicOn = false;
        _storyMusicDone = true;
        Debug.Log($"[PageMusicManager] {_storyMusicEndsOn}: story music ends, back to each page's own track");
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
        string target = _storyMusicOn
            ? _storyMusic
            : (page >= 0 && page < _musicPerPage.Length ? _musicPerPage[page] : "");

        //stop every other track this manager knows about
        foreach (string id in _musicPerPage)
        {
            StopUnlessTarget(id, target);
        }
        StopUnlessTarget(_storyMusic, target);

        if (string.IsNullOrEmpty(target))
        {
            Debug.Log($"[PageMusicManager] page {page + 1}: silence");
            return;
        }

        if (!AudioManager.instance.IsPlaying(target))
        {
            Debug.Log($"[PageMusicManager] page {page + 1}: playing {target}{(_storyMusicOn ? " (story music)" : "")}");
            AudioManager.instance.Play(target);
        }
    }

    static void StopUnlessTarget(string id, string target)
    {
        if (!string.IsNullOrEmpty(id) && id != target && AudioManager.instance.IsPlaying(id))
        {
            AudioManager.instance.StopById(id);
        }
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnNewPageOpen, OnNewPageOpen);

        if (!string.IsNullOrEmpty(_storyMusic))
        {
            EventManager.Unsubscribe(_storyMusicStartsOn, OnStoryMusicStarts);
            EventManager.Unsubscribe(_storyMusicEndsOn, OnStoryMusicEnds);
        }
    }
}
