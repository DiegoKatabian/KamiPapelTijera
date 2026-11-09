using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// The level intro (spec 025 Part B): the book starts closed, the Narrator's hand opens it, page 1 unfolds with the
/// cover, and the hand brings Kami and lets her go over her spawn. On the root of Prefabs/BookIntro/BookIntro_LevelN
/// (built by Kami > Book Intro > Build), with the PlayableDirector and CutsceneDirector that play its Timeline.
///
/// Runs only when the scene starts on page 1, once per load. The Timeline moves the hand, the cover and the cameras;
/// this script does what a Timeline can't, at the beat times below (they match tools/make-book-intro-clips.py):
/// hides the level's book and swaps it back, carries Kami (Player.StartCarry), unfolds page 1 on the cover (PaperTurn
/// intro mode, or the old pop-up as a fallback), plays the sounds, and lets the player skip by holding Interact.
/// Every beat is idempotent and the skip runs them all, so a skip at any moment leaves the same end state (D18).
///
/// Debugging (D41): the overlay shows the step, the time and the cover's angle; _startAtSeconds starts mid-way;
/// in the Editor F9 pauses, F10 plays x4, F11 restarts. Logs are "[BookIntro]".
/// </summary>
[DefaultExecutionOrder(-40)]
public class BookIntro : MonoBehaviour
{
    /// <summary>True from Awake until the intro is over when this scene plays one (MagicalGirlChange waits for it).</summary>
    public static bool Pending { get; private set; }

    [Header("When")]
    [Tooltip("Plays only when the scene starts on this page (PageScrollerManager.startingPage).")]
    [SerializeField] int _pageNumber = 1;

    [Header("Parts (wired by Kami > Book Intro > Build)")]
    [SerializeField] PlayableDirector _playable;
    [SerializeField] CutsceneDirector _cutscene;
    [SerializeField] GameObject _hand;
    [Tooltip("The hand's Carry socket: Kami hangs from it.")]
    [SerializeField] Transform _carrySocket;
    [SerializeField] GameObject _introBook;
    [Tooltip("The intro book's Hinge: its angle tells how far the cover has opened.")]
    [SerializeField] Transform _hinge;
    [SerializeField] Renderer _introFixed;
    [SerializeField] Renderer _introMoving;
    [Tooltip("The skip prompt's object (it holds the key icon and the HoldToSkipPrompt).")]
    [SerializeField] TMP_Text _skipPrompt;
    [Tooltip("The ring that fills while Interact is held. Empty = looked up on the Skip Prompt object.")]
    [SerializeField] HoldToSkipPrompt _holdPrompt;

    [Header("Kami")]
    [Tooltip("Kami's root relative to the Carry socket while she hangs from the pinch.")]
    [SerializeField] Vector3 _carryOffset = new Vector3(0, -7, 0);

    [Header("Beats (seconds on the Timeline, from tools/make-book-intro-clips.py)")]
    [SerializeField] float _handEnters = 2f;
    [SerializeField] float _coverGripped = 3f;
    [SerializeField] float _coverOpened = 5.45f;
    [SerializeField] float _showKami = 6.6f;
    [SerializeField] float _handReturns = 7f;
    [SerializeField] float _releaseKami = 10.1f;
    [SerializeField] float _end = 12f;

    [Header("Page 1")]
    [Tooltip("Page 1's objects unfold with the cover (PaperTurn intro mode). Off, or without a PaperTurn: they are " +
             "hidden while the book is closed and pop up when the cover is half open (the old pop-up).")]
    [SerializeField] bool _unfoldWithPaper = true;
    [Tooltip("Fallback pop-up: how open the cover is (0..1) when page 1 appears.")]
    [SerializeField, Range(0f, 1f)] float _popAtOpen = 0.5f;

    [Header("Skip")]
    [Tooltip("Seconds into the intro before a hold can skip it.")]
    [SerializeField] float _skipPromptAfter = 1f;
    [Tooltip("Seconds Interact must be held to skip.")]
    [SerializeField] float _holdToSkip = 1f;

    [Header("Debug")]
    [Tooltip("Start the intro this many seconds in (testing a late beat without waiting).")]
    [SerializeField] float _startAtSeconds;
    [SerializeField] bool _overlay;

    Player _player;
    Vector3 _spawn;
    GameObject _page;
    PaperTurn _paper;
    bool _paperUnfold;
    Renderer _levelBook;
    readonly List<Renderer> _hiddenKami = new List<Renderer>();
    readonly List<Renderer> _hiddenPage = new List<Renderer>();
    bool _running, _shownKami, _released, _pageShown, _coverLandedSound, _gripSound, _enterSound, _returnSound;
    float _holdTime;
    bool _holdStartedBefore;
    string _step = "idle";
#if UNITY_EDITOR
    float _debugSpeed = 1f;
#endif

    void Awake()
    {
        PageScrollerManager psm = FindObjectOfType<PageScrollerManager>();
        bool plays = psm != null && psm.startingPage == _pageNumber;
        Pending = plays;
        if (!plays)
        {
            Debug.Log($"[BookIntro] scene starts on page {(psm != null ? psm.startingPage : -1)}, not {_pageNumber}: no intro");
            SetVisible(false);
            enabled = false;
        }
    }

    void Start()
    {
        if (!enabled)
        {
            return;
        }
        _player = LevelManager.Instance != null ? LevelManager.Instance.player : FindObjectOfType<Player>();
        if (_player == null || _playable == null || _cutscene == null || _hand == null || _carrySocket == null)
        {
            Debug.LogWarning("[BookIntro] missing Kami or a part of the prefab (rebuild with Kami > Book Intro > Build): no intro");
            Pending = false;
            SetVisible(false);
            enabled = false;
            return;
        }
        _spawn = _player.transform.position;
        _holdStartedBefore = InputHub.InteractHeld; // Level 1's storyboard press must not count as a skip
        PageScrollerManager psm = PageScrollerManager.Instance;
        _page = psm != null ? psm.PageFolder(_pageNumber - 1) : null;

        // the level's own book hides behind the intro book (renderers only: Level 2's book collider is the floor)
        _paper = FindObjectOfType<PaperTurn>();
        _levelBook = _paper != null ? _paper.BookRenderer : null;
        if (_levelBook != null)
        {
            _levelBook.forceRenderingOff = true;
        }
        else
        {
            Debug.LogWarning("[BookIntro] no PaperTurn with the level book's renderer: the level book stays visible under the intro book");
        }
        if (BookPageSpread.Instance != null)
        {
            BookPageSpread.Instance.DressIntroBook(_introFixed, _introMoving, _pageNumber - 1);
        }

        // page 1 is hidden while the book is closed; next frame it unfolds with the cover (or pops up later)
        HidePage();

        // Kami is hidden until the hand brings her
        foreach (Renderer r in _player.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.forceRenderingOff)
            {
                r.forceRenderingOff = true;
                _hiddenKami.Add(r);
            }
        }
        if (_skipPrompt != null)
        {
            _skipPrompt.gameObject.SetActive(false);
        }
        SetVisible(true);
        // the closed pose from the very first frame (the prefab holds the open one)
        _playable.time = 0;
        _playable.Evaluate();
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.inCutscene = true;
        }
        StartCoroutine(BeginNextFrame());
    }

    // This Start runs before Player's (execution order -40): her skeleton, her safe position and the spawn manager's
    // respawn point are set up in their own Starts, so she is picked up one frame later.
    System.Collections.IEnumerator BeginNextFrame()
    {
        yield return null;
        if (_unfoldWithPaper && _paper != null && _page != null)
        {
            // PaperTurn copies only what is drawn, and page 1's Spine characters have a mesh only after their Start:
            // show the page again this same frame (nothing renders in between) and let the paper turn take it
            ShowHiddenPage();
            _paperUnfold = _paper.BeginIntro(_page);
            if (!_paperUnfold)
            {
                Debug.LogWarning("[BookIntro] PaperTurn could not unfold page 1: it pops up instead");
                HidePage();
            }
        }
        _player.StartCarry(_carrySocket, _carryOffset);
        _running = true;
        _cutscene.Play();
        _playable.Evaluate();
        if (_startAtSeconds > 0)
        {
            _playable.time = _startAtSeconds;
            _playable.Evaluate();
        }
        Debug.Log($"[BookIntro] intro starts (page {_pageNumber}, paper unfold {_paperUnfold}, Kami's spawn {_spawn})");
    }

    void Update()
    {
        if (!_running)
        {
            return;
        }
#if UNITY_EDITOR
        DebugKeys();
#endif
        float t = (float)_playable.time;
        float open = CoverOpen();
        if (_paperUnfold && !_pageShown)
        {
            _paper.SetIntroProgress(open);
        }

        if (t >= _handEnters && !_enterSound)
        {
            _enterSound = true;
            Play(AudioId.NarratorHandMove);
        }
        if (t >= _coverGripped && !_gripSound)
        {
            _gripSound = true;
            Play(AudioId.BookCoverOpen);
            _step = "opening the cover";
        }
        if (!_paperUnfold && open >= _popAtOpen)
        {
            ShowPage();
        }
        if (t >= _coverOpened)
        {
            CoverLanded();
        }
        if (t >= _showKami)
        {
            ShowKami();
        }
        if (t >= _handReturns && !_returnSound)
        {
            _returnSound = true;
            Play(AudioId.NarratorHandMove);
            _step = "bringing Kami";
        }
        if (t >= _releaseKami)
        {
            ReleaseKami();
        }

        UpdateSkip(t);

        if (t >= _end - 0.02f || !_cutscene.IsPlaying)
        {
            Finish();
        }
    }

    float CoverOpen()
    {
        if (_hinge == null)
        {
            return 1f;
        }
        float a = Mathf.Abs(Mathf.DeltaAngle(0f, _hinge.localEulerAngles.z));
        return 1f - a / 180f;
    }

    void UpdateSkip(float t)
    {
        bool held = InputHub.InteractHeld;
        if (!held)
        {
            _holdStartedBefore = false;
        }
        // the prompt object is live once skipping is allowed; it stays invisible until the player presses (HoldToSkipPrompt)
        if (_skipPrompt != null && t >= _skipPromptAfter && !_skipPrompt.gameObject.activeSelf)
        {
            _skipPrompt.gameObject.SetActive(true);
            if (_holdPrompt == null)
            {
                _holdPrompt = _skipPrompt.GetComponent<HoldToSkipPrompt>();
                if (_holdPrompt == null)
                {
                    Debug.LogWarning("[BookIntro] the Skip Prompt has no HoldToSkipPrompt: the skip works but shows nothing");
                }
            }
        }
        if (held && !_holdStartedBefore && t >= _skipPromptAfter)
        {
            _holdTime += Time.unscaledDeltaTime;
            if (_holdPrompt != null)
            {
                _holdPrompt.SetHold(_holdTime / Mathf.Max(0.01f, _holdToSkip), true);
            }
            if (_holdTime >= _holdToSkip)
            {
                Debug.Log($"[BookIntro] skipped at {t:0.00}s");
                ApplyEndState();
            }
        }
        else
        {
            _holdTime = 0;
            if (_holdPrompt != null && _skipPrompt != null && _skipPrompt.gameObject.activeSelf)
            {
                _holdPrompt.SetHold(0f, false);
            }
        }
    }

    /// <summary>Jumps to the end: every beat not reached yet runs now, the Timeline is put on its last frame and stops.</summary>
    public void ApplyEndState()
    {
        if (!_running)
        {
            return;
        }
        _playable.time = _end;
        _playable.Evaluate();
        ShowPage();
        CoverLanded();
        ShowKami();
        ReleaseKami();
        _playable.Stop(); // CutsceneDirector gives Kami back
        Finish();
    }

    void ShowPage()
    {
        if (_pageShown)
        {
            return;
        }
        _pageShown = true;
        if (_paperUnfold)
        {
            _paper.SetIntroProgress(1f);
            _paper.EndIntro();
        }
        ShowHiddenPage();
        if (!_paperUnfold && _page != null)
        {
            // the old pop-up: replay the page folder's Enlarge from its start
            Animator anim = _page.GetComponent<Animator>();
            if (anim != null)
            {
                anim.Play("Objetos_Paginas_Enlarge", 0, 0f);
            }
        }
        Debug.Log("[BookIntro] page shown");
    }

    void HidePage()
    {
        if (_page == null)
        {
            return;
        }
        foreach (Renderer r in _page.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.forceRenderingOff)
            {
                r.forceRenderingOff = true;
                _hiddenPage.Add(r);
            }
        }
    }

    void ShowHiddenPage()
    {
        foreach (Renderer r in _hiddenPage)
        {
            if (r != null)
            {
                r.forceRenderingOff = false;
            }
        }
        _hiddenPage.Clear();
    }

    void CoverLanded()
    {
        if (_coverLandedSound)
        {
            return;
        }
        _coverLandedSound = true;
        Play(AudioId.BookCoverLand);
        ShowPage(); // the paper unfold ends with the cover flat open
        _step = "cover open";
    }

    void ShowKami()
    {
        if (_shownKami)
        {
            return;
        }
        _shownKami = true;
        foreach (Renderer r in _hiddenKami)
        {
            if (r != null)
            {
                r.forceRenderingOff = false;
            }
        }
        _hiddenKami.Clear();
    }

    void ReleaseKami()
    {
        if (_released)
        {
            return;
        }
        _released = true;
        ShowKami();
        if (_player != null)
        {
            _player.StopCarry(_spawn);
            if (PlayerPageSpawnManager.Instance != null)
            {
                PlayerPageSpawnManager.Instance.SavePosition(_spawn); // her scene position stays her respawn point
            }
        }
        _step = "Kami let go";
        Debug.Log($"[BookIntro] Kami released over {_spawn}");
    }

    void Finish()
    {
        if (!_running)
        {
            return;
        }
        _running = false;
        ShowPage();
        ReleaseKami();
        if (_levelBook != null)
        {
            _levelBook.forceRenderingOff = false;
        }
        if (_holdPrompt != null)
        {
            _holdPrompt.Hide();
        }
        if (_skipPrompt != null)
        {
            _skipPrompt.gameObject.SetActive(false);
        }
        SetVisible(false);
        if (_cutscene != null)
        {
            _cutscene.Release();
        }
        Pending = false;
        _step = "done";
        Debug.Log("[BookIntro] intro done");
    }

    void SetVisible(bool visible)
    {
        if (_hand != null)
        {
            _hand.SetActive(visible);
        }
        if (_introBook != null)
        {
            _introBook.SetActive(visible);
        }
    }

    static void Play(string id)
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(id);
        }
    }

    void OnDestroy()
    {
        if (_running)
        {
            Pending = false;
        }
    }

#if UNITY_EDITOR
    void DebugKeys()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            _debugSpeed = _debugSpeed > 0 ? 0 : 1;
            SetSpeed(_debugSpeed);
            Debug.Log($"[BookIntro] debug {(_debugSpeed > 0 ? "resumed" : "paused")} at {_playable.time:0.00}s");
        }
        if (Input.GetKeyDown(KeyCode.F10))
        {
            _debugSpeed = _debugSpeed > 1 ? 1 : 4;
            SetSpeed(_debugSpeed);
            Debug.Log($"[BookIntro] debug speed x{_debugSpeed}");
        }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            _playable.time = 0;
            _playable.Evaluate();
            Debug.Log("[BookIntro] debug restart (beats already run stay done)");
        }
    }

    void SetSpeed(float speed)
    {
        if (_playable.playableGraph.IsValid())
        {
            _playable.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }
    }

    void OnGUI()
    {
        if (!_overlay || !_running)
        {
            return;
        }
        GUI.Label(new Rect(10, 10, 600, 22), $"[BookIntro] {_step}  t {_playable.time:0.00}s  cover open {CoverOpen():0.00}  skip hold {_holdTime:0.0}");
    }
#endif
}
