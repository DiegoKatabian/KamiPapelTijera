using System.Collections.Generic;
using System.Text;
using Kami.Paper;
using UnityEngine;

/// <summary>
/// The paper page turn (spec 025 Part A). On the PageScrollerManager's object; PageScrollerManager calls Begin when its
/// `_paperPages` is on, instead of instantiating HojaMaster.
///
/// One turn: the two pages' renderers are grouped into pieces and copied into draw-only proxies (PaperPieces), the real
/// renderers are hidden (forceRenderingOff), and every frame the procedural sheet (PaperSheet) moves on PaperTurnModel's
/// curve while each piece rides its leaf and folds just enough to stay under the other page. Flat pieces riding the
/// sheet are captured from above once and drawn on it as a texture. At the end OnPageFinishTurning is raised, the
/// proxies are dropped and the real renderers shown again: nothing real ever moved (D25).
///
/// Debugging (D41): the Debug header pauses, slows or scrubs a turn and switches piece classes off; every log line is
/// "[PaperTurn]" and names objects by their hierarchy path.
/// </summary>
[DefaultExecutionOrder(-50)] // before Player (0) reads the sheet's Edge and PageRideFollowers (100)
public class PaperTurn : MonoBehaviour
{
    [Header("Book")]
    [Tooltip("This level's measured book (Assets/Data/PaperBook/).")]
    [SerializeField] PaperBookData _book;
    [Tooltip("The open book's renderer: the sheet's faces use its half materials, so they show the same picture.")]
    [SerializeField] Renderer _bookRenderer;

    [Header("Sheet")]
    [SerializeField] PaperSheet _sheetPrefab;
    [Tooltip("Sparkles on the sheet's edge (the old sheet's GlitterHoja). Empty = none.")]
    [SerializeField] GameObject _edgeEffect;
    [Tooltip("Material drawing the flat pieces' capture on the sheet (Kami/Paper Overlay).")]
    [SerializeField] Material _overlayMaterial;

    [Header("Flat pieces")]
    [Tooltip("Capture the flat pieces riding the sheet from above, so they bend with it. Off = they ride it rigid.")]
    [SerializeField] bool _captureFlat = true;
    [Tooltip("An unused layer for the capture camera (it only renders the proxies drawn for it).")]
    [SerializeField] int _captureLayer = 31;
    [Tooltip("Capture texture width in pixels (the depth follows the page's proportions).")]
    [SerializeField] int _captureWidth = 1024;

    [Header("Debug")]
    [Tooltip("Log every piece with its class and leaf at the start of each turn.")]
    [SerializeField] bool _logPieces;
    [Tooltip("Hold the turn where it is (F7 in Play mode in the Editor).")]
    [SerializeField] bool _pause;
    [Tooltip("Turn speed multiplier (F8 toggles 0.2 in the Editor).")]
    [SerializeField, Range(0.05f, 2f)] float _speed = 1f;
    [Tooltip("Drive the turn from the slider below instead of time.")]
    [SerializeField] bool _useScrub;
    [SerializeField, Range(0f, 1f)] float _scrub;
    [SerializeField] bool _drawCards = true;
    [SerializeField] bool _drawVolumes = true;
    [SerializeField] bool _drawRiders = true;
    [SerializeField] bool _drawFlat = true;
    [Tooltip("Gizmos: the sheet's curve, each piece's bounds and hinge.")]
    [SerializeField] bool _gizmos = true;

    public bool IsTurning => _sheet != null;
    public PaperBookData Book => _book;
    public Renderer BookRenderer => _bookRenderer;
    public Transform Edge => _sheet != null ? _sheet.Edge : null;

    PaperTurnModel _model;
    PaperFrame _frame;
    PaperSheet _sheet;
    readonly List<PaperPiece> _pieces = new List<PaperPiece>();
    readonly Dictionary<Renderer, bool> _hidden = new Dictionary<Renderer, bool>();
    readonly List<string> _unfitted = new List<string>();
    float _elapsed;
    bool _finishRaised;
    // indexed by LeafKind: the flat pieces of one page over one half (StartStatic and SheetBack = the page arriving,
    // EndStatic and SheetFront = the page leaving)
    readonly Camera[] _captureCams = new Camera[4];
    readonly RenderTexture[] _captures = new RenderTexture[4];
    readonly Rect[] _captureRects = new Rect[4];
    bool _capturing;
    int _captureFrames;
    int _from, _to;
    bool _introMode;   // the level intro's cover (BookIntro): time comes from the hinge, no sheet faces, no page events
    double _introT;
    readonly Transform[] _folders = new Transform[2];
    readonly List<Renderer> _scan = new List<Renderer>();
    int _lateHidden;

    /// <summary>
    /// Starts a turn from page index `from` to `to` (folders from PageScrollerManager). Returns the sheet's object
    /// (PageScrollerManager destroys it when the turn ends), or null if the turn can't run (then the caller should fall
    /// back to the old sheet).
    /// </summary>
    public GameObject Begin(int from, int to, bool forward, GameObject fromFolder, GameObject toFolder)
    {
        return BeginInternal(from, to, forward, fromFolder, toFolder, false);
    }

    /// <summary>
    /// The level intro (spec 025 D34): the intro book's cover opens like a page turning forward with no page leaving,
    /// so `page` (page 1's folder) unfolds with it. The cover is a rigid board (the curl's lead is 0), the intro book
    /// draws the paper itself, and BookIntro drives the progress (SetIntroProgress) from the hinge's angle. No page
    /// events are raised. Returns false if it can't run (BookIntro then uses its pop-up fallback).
    /// </summary>
    public bool BeginIntro(GameObject page)
    {
        return BeginInternal(0, 0, true, null, page, true) != null;
    }

    /// <summary>How far the intro's cover has opened: 0 closed, 1 open.</summary>
    public void SetIntroProgress(float open)
    {
        // the curl blends with smoothstep(t): invert it so the pieces follow the hinge's own angle
        double target = PaperMath.Clamp01(open);
        double lo = 0, hi = 1;
        for (int i = 0; i < 30; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (PaperMath.Smoothstep(mid) < target)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        _introT = 0.5 * (lo + hi);
    }

    /// <summary>Ends the intro's unfold: real page objects shown again.</summary>
    public void EndIntro()
    {
        if (_introMode)
        {
            Finish();
        }
    }

    GameObject BeginInternal(int from, int to, bool forward, GameObject fromFolder, GameObject toFolder, bool intro)
    {
        if (_book == null || _sheetPrefab == null)
        {
            Debug.LogWarning($"[PaperTurn] '{name}': book data or sheet prefab not assigned, the old page turn runs instead");
            return null;
        }
        if (_sheet != null)
        {
            Debug.LogWarning("[PaperTurn] Begin while a turn is running: the old one is finished first");
            Finish();
        }
        _from = from;
        _to = to;
        _frame = new PaperFrame { SpineX = _book.spineX, Dir = forward ? 1 : -1 };
        _introMode = intro;
        _introT = 0;
        CurlParams curlParams = intro ? new CurlParams { lead = 0, ease = 0, shape = 1, duration = 1, heightScale = 1 } : _book.curl;
        PaperCurl curl = new PaperCurl(_book.Profile(forward), _book.Profile(!forward), curlParams);
        _model = new PaperTurnModel(curl, _book.cardFallSign);
        _elapsed = 0;
        _finishRaised = false;
        _unfitted.Clear();

        _folders[0] = fromFolder != null ? fromFolder.transform : null;
        _folders[1] = toFolder != null ? toFolder.transform : null;
        _lateHidden = 0;
        StringBuilder log = new StringBuilder();
        BuildPieces(fromFolder, false, log);
        BuildPieces(toFolder, true, log);
        PaperPieces.ResolveRiders(_pieces, log); // a Rider with nothing under it becomes a Volume, so before Prepare
        SetLeafThickness();
        foreach (PaperPiece p in _pieces)
        {
            if (p.Class != PagePiece.PieceClass.Rider)
            {
                p.Rest.Kind = p.Class == PagePiece.PieceClass.Volume ? FoldKind.Volume : FoldKind.Card;
                p.Rest.Folds = p.Class == PagePiece.PieceClass.Card || p.Class == PagePiece.PieceClass.Volume;
                _model.Prepare(p.Rest);
            }
        }

        _capturing = _captureFlat && _overlayMaterial != null;
        if (_capturing)
        {
            // flat pieces never move as meshes: each page's are captured from above over each half, and the captures lie
            // on the halves (static) or ride the sheet; a flat piece across the spine is split by the two rectangles
            _captureRects[(int)LeafKind.SheetFront] = _captureRects[(int)LeafKind.StartStatic] = HalfRect(forward);
            _captureRects[(int)LeafKind.SheetBack] = _captureRects[(int)LeafKind.EndStatic] = HalfRect(!forward);
            for (int i = 0; i < 4; i++)
            {
                LeafKind leaf = (LeafKind)i;
                _captures[i] = MakeCapture($"Paper capture {leaf}", _captureRects[i], out _captureCams[i], IsArriving(leaf));
            }
            _captureFrames = 1;
        }

        _sheet = Instantiate(_sheetPrefab, transform);
        _sheet.name = intro ? "PaperSheet (level intro)" : $"PaperSheet {from + 1} -> {to + 1}";
        bool startRight = forward;
        Material frontMat = HalfMaterial(startRight);
        Material backMat = HalfMaterial(!startRight);
        _sheet.Setup(_model, _frame, _book, frontMat, backMat, _overlayMaterial, _capturing ? _captures : null, _captureRects);
        if (intro)
        {
            _sheet.SetFacesVisible(false); // the intro book's cover is the paper
        }
        else
        {
            if (_edgeEffect != null && _sheet.Edge != null)
            {
                Instantiate(_edgeEffect, _sheet.Edge, false);
            }
            if (BookPageSpread.Instance != null)
            {
                BookPageSpread.Instance.DressPaperSheet(_sheet.FrontRenderer, _sheet.BackRenderer, from, to, forward);
            }
        }

        Summarize(log, forward);
        // shape only: the proxies are drawn by this frame's LateUpdate (a coroutine calls Begin before it), once
        _model.SetTime(0);
        _sheet.UpdateShape(_model);
        return _sheet.gameObject;
    }

    /// <summary>Each leaf counts as a share of its tallest standing piece thick (PaperBookData.pageThicknessShare).</summary>
    void SetLeafThickness()
    {
        for (int i = 0; i < 4; i++)
        {
            _model.LeafThickness[i] = 0;
        }
        foreach (PaperPiece p in _pieces)
        {
            if (p.Class != PagePiece.PieceClass.Card && p.Class != PagePiece.PieceClass.Volume)
            {
                continue;
            }
            PaperProfile surface = _book.Profile(_frame.Dir * (p.Canon.center.x >= 0 ? 1 : -1) > 0);
            double height = p.Canon.max.y - surface.Height(System.Math.Abs(p.Canon.center.x));
            int leaf = (int)p.Leaf;
            _model.LeafThickness[leaf] = System.Math.Max(_model.LeafThickness[leaf], height * _book.pageThicknessShare);
        }
    }

    Material HalfMaterial(bool right)
    {
        if (_bookRenderer == null)
        {
            Debug.LogWarning("[PaperTurn] no book renderer: the sheet has no material");
            return null;
        }
        Material[] mats = _bookRenderer.sharedMaterials;
        int slot = _book.Slot(right);
        if (slot < 0 || slot >= mats.Length)
        {
            Debug.LogWarning($"[PaperTurn] the book has no material slot {slot} for its {(right ? "right" : "left")} half");
            return mats.Length > 0 ? mats[0] : null;
        }
        return mats[slot];
    }

    /// <summary>World rectangle (x, z, width, depth) of a half, for its capture.</summary>
    Rect HalfRect(bool right)
    {
        PaperProfile p = _book.Profile(right);
        float outer = (float)p.OuterX;
        float x0 = right ? _book.spineX : _book.spineX - outer;
        return new Rect(x0, _book.pageZ.x, outer, _book.pageZ.y - _book.pageZ.x);
    }

    static bool IsArriving(LeafKind leaf) => leaf == LeafKind.StartStatic || leaf == LeafKind.SheetBack;

    /// <summary>A capture of one page's flat pieces over one half, or null (and no camera) when that page has none.</summary>
    RenderTexture MakeCapture(string captureName, Rect rect, out Camera cam, bool arriving)
    {
        cam = null;
        bool any = false;
        foreach (PaperPiece p in _pieces)
        {
            if (p.Class == PagePiece.PieceClass.Flat && p.Arriving == arriving && p.Proxies.Count > 0)
            {
                any = true;
                break;
            }
        }
        if (!any)
        {
            return null;
        }
        int w = Mathf.Max(64, _captureWidth);
        int h = Mathf.Max(64, Mathf.RoundToInt(w * rect.height / Mathf.Max(rect.width, 0.01f)));
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = captureName };
        GameObject go = new GameObject(captureName);
        go.transform.SetParent(transform, false);
        cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = rect.height * 0.5f;
        cam.aspect = rect.width / rect.height;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = 1 << _captureLayer;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;
        cam.depth = -100;
        cam.targetTexture = rt;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        // looking straight down: image right = +world x, image up = +world z (PaperSheet's overlay UVs assume it)
        go.transform.position = new Vector3(rect.center.x, 200f, rect.center.y);
        go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        return rt;
    }

    void BuildPieces(GameObject folder, bool arriving, StringBuilder log)
    {
        if (folder == null)
        {
            return;
        }
        List<KeyValuePair<Transform, List<Renderer>>> groups = new List<KeyValuePair<Transform, List<Renderer>>>();
        List<Renderer> untouched = new List<Renderer>();
        PaperPieces.Group(folder.transform, _book, groups, untouched);
        List<PaperPiece> built = new List<PaperPiece>();
        foreach (KeyValuePair<Transform, List<Renderer>> g in groups)
        {
            PaperPiece piece = new PaperPiece
            {
                Path = PathOf(g.Key),
                Arriving = arriving,
                Canon = PaperPieces.CanonBounds(g.Value, _frame),
            };
            PagePiece tag = g.Key.GetComponent<PagePiece>();
            Bounds world = PaperPieces.WorldBounds(g.Value);
            if (tag != null && tag.pieceClass != PagePiece.PieceClass.Auto)
            {
                piece.Class = tag.pieceClass;
            }
            else
            {
                piece.Class = PaperPieces.Classify(g.Value, world, _book);
                piece.AutoClassified = true;
            }
            bool startSide = piece.Canon.center.x >= 0;
            piece.Leaf = arriving
                ? (startSide ? LeafKind.StartStatic : LeafKind.SheetBack)
                : (startSide ? LeafKind.SheetFront : LeafKind.EndStatic);
            piece.Rest.Leaf = piece.Leaf;
            piece.Rest.Kind = piece.Class == PagePiece.PieceClass.Volume ? FoldKind.Volume : FoldKind.Card;
            piece.Rest.Folds = piece.Class == PagePiece.PieceClass.Card || piece.Class == PagePiece.PieceClass.Volume;
            Vector3 mn = piece.Canon.min, mx = piece.Canon.max;
            int k = 0;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        piece.Rest.Corners[k++] = new V3(x == 0 ? mn.x : mx.x, y == 0 ? mn.y : mx.y, z == 0 ? mn.z : mx.z);
                    }
                }
            }
            if (tag != null && tag.pieceClass == PagePiece.PieceClass.Rider && tag.host != null)
            {
                piece.HostTag = tag.host;
            }

            foreach (Renderer r in g.Value)
            {
                Hide(r);
                if (piece.Class == PagePiece.PieceClass.Hidden)
                {
                    continue;
                }
                PaperProxy proxy = PaperPieces.MakeProxy(r, out string why);
                if (proxy != null)
                {
                    piece.Proxies.Add(proxy);
                }
                else
                {
                    log.AppendLine($"  hidden only: {PathOf(r.transform)} ({why})");
                }
            }
            built.Add(piece);
        }
        // automatic riders: a piece lifted off the page that sits on another piece's top
        foreach (PaperPiece p in built)
        {
            if (!p.AutoClassified || p.Class == PagePiece.PieceClass.Flat)
            {
                continue;
            }
            PaperProfile surface = _book.Profile(_frame.Dir * Mathf.Sign(p.Canon.center.x == 0 ? 1 : p.Canon.center.x) > 0);
            float lift = p.Canon.min.y - (float)surface.Height(Mathf.Abs(p.Canon.center.x));
            if (lift < _book.riderMinLift)
            {
                continue;
            }
            foreach (PaperPiece h in built)
            {
                if (h == p || h.Class == PagePiece.PieceClass.Flat)
                {
                    continue;
                }
                Vector3 c = p.Canon.center;
                if (c.x >= h.Canon.min.x && c.x <= h.Canon.max.x && c.z >= h.Canon.min.z && c.z <= h.Canon.max.z
                    && Mathf.Abs(p.Canon.min.y - h.Canon.max.y) < 1.5f)
                {
                    p.Class = PagePiece.PieceClass.Rider;
                    p.Host = h;
                    break;
                }
            }
        }
        // explicit riders' hosts
        foreach (PaperPiece p in built)
        {
            if (p.HostTag == null)
            {
                continue;
            }
            foreach (PaperPiece h in built)
            {
                if (h.Path == PathOf(p.HostTag.transform))
                {
                    p.Host = h;
                }
            }
        }
        _pieces.AddRange(built);
        foreach (Renderer r in untouched)
        {
            if (_logPieces)
            {
                log.AppendLine($"  left alone: {PathOf(r.transform)}");
            }
        }
    }

    void Hide(Renderer r)
    {
        if (r == null || _hidden.ContainsKey(r))
        {
            return;
        }
        _hidden[r] = r.forceRenderingOff;
        r.forceRenderingOff = true;
    }

    void Summarize(StringBuilder log, bool forward)
    {
        int[] count = new int[7];
        int proxies = 0;
        foreach (PaperPiece p in _pieces)
        {
            count[(int)p.Class]++;
            proxies += p.Proxies.Count;
        }
        Debug.Log($"[PaperTurn] page {_from + 1} -> {_to + 1} ({(forward ? "forward" : "back")}): {_pieces.Count} pieces " +
                  $"({count[1]} card, {count[2]} volume, {count[3]} rider, {count[4]} flat, {count[6]} hidden), {proxies} proxies, " +
                  $"{_hidden.Count} renderers hidden" + (log.Length > 0 ? "\n" + log : ""));
        if (_logPieces)
        {
            StringBuilder all = new StringBuilder("[PaperTurn] pieces:\n");
            foreach (PaperPiece p in _pieces)
            {
                all.AppendLine($"  {p.Path}: {p.Class}{(p.AutoClassified ? " (auto)" : "")}, {p.Leaf}, {p.Proxies.Count} proxies, " +
                               $"bounds {p.Canon.min:F1}..{p.Canon.max:F1}" + (p.Host != null ? $", on {p.Host.Path}" : ""));
            }
            Debug.Log(all.ToString());
        }
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F7))
        {
            _pause = !_pause;
            Debug.Log($"[PaperTurn] debug pause {(_pause ? "on" : "off")}");
        }
        if (Input.GetKeyDown(KeyCode.F8))
        {
            _speed = _speed < 0.99f ? 1f : 0.2f;
            Debug.Log($"[PaperTurn] debug speed x{_speed}");
        }
#endif
        if (_sheet == null)
        {
            return;
        }
        if (!_pause && !_useScrub && !_introMode)
        {
            _elapsed += Time.deltaTime * _speed;
        }
        HideLateRenderers();
        Step();
        if (_captureFrames > 0)
        {
            _captureFrames--;
        }
        else
        {
            foreach (Camera c in _captureCams)
            {
                if (c != null)
                {
                    c.enabled = false;
                }
            }
        }

        double duration = System.Math.Max(_book.curl.duration, 0.1);
        if (!_introMode && !_finishRaised && !_useScrub && _elapsed >= duration)
        {
            _finishRaised = true;
            Debug.Log($"[PaperTurn] page {_from + 1} -> {_to + 1} landed" +
                      (_unfitted.Count > 0 ? $"; could not fit (folded fully and squashed): {string.Join(", ", _unfitted)}" : ""));
            EventManager.Trigger(Evento.OnPageFinishTurning); // PageScrollerManager destroys the sheet, Kami lands
            Finish();
        }
    }

    void Step()
    {
        double duration = System.Math.Max(_book.curl.duration, 0.1);
        double t = _introMode ? _introT : (_useScrub ? _scrub : _elapsed / duration);
        _model.SetTime(t);
        _sheet.UpdateShape(_model);
        bool captureFrame = _capturing && _captureFrames > 0;
        foreach (PaperPiece p in _pieces)
        {
            if (p.Class == PagePiece.PieceClass.Rider || p.Class == PagePiece.PieceClass.Hidden)
            {
                continue;
            }
            p.Map = _frame.WorldMap(_model.Solve(p.Rest));
            if (p.Rest.Folds && !p.Rest.Last.Fitted && !_unfitted.Contains(p.Path))
            {
                _unfitted.Add(p.Path);
            }
        }
        foreach (PaperPiece p in _pieces)
        {
            if (p.Class == PagePiece.PieceClass.Rider)
            {
                p.Map = p.Host != null ? p.Host.Map : Matrix4x4.identity;
            }
        }
        foreach (PaperPiece p in _pieces)
        {
            if (p.Class == PagePiece.PieceClass.Flat && _capturing)
            {
                if (captureFrame)
                {
                    // into both captures of its page (one per half): each camera keeps what lies over its half
                    for (int i = 0; i < 4; i++)
                    {
                        if (_captureCams[i] != null && _captureCams[i].enabled && IsArriving((LeafKind)i) == p.Arriving)
                        {
                            foreach (PaperProxy proxy in p.Proxies)
                            {
                                proxy.Draw(Matrix4x4.identity, _captureCams[i], _captureLayer);
                            }
                        }
                    }
                }
                continue; // drawn by the overlays
            }
            if (!ShouldDraw(p.Class))
            {
                continue;
            }
            foreach (PaperProxy proxy in p.Proxies)
            {
                proxy.Draw(p.Map, null);
            }
        }
    }

    /// <summary>
    /// Renderers that show up under either page after the turn started (a car the traffic spawner makes, an object a
    /// script switches on when the arriving page opens) have no proxy: drawn as they are, they would stand still while
    /// the paper folds around them. They are hidden until the turn ends, like everything else.
    /// </summary>
    void HideLateRenderers()
    {
        foreach (Transform folder in _folders)
        {
            if (folder == null)
            {
                continue;
            }
            folder.GetComponentsInChildren(true, _scan);
            foreach (Renderer r in _scan)
            {
                if (_hidden.ContainsKey(r) || !PaperPieces.IsLive(r, folder) || PaperPieces.IsUntouchable(r, folder))
                {
                    continue;
                }
                Hide(r);
                _lateHidden++;
                if (_logPieces)
                {
                    Debug.Log($"[PaperTurn] appeared during the turn, hidden until it ends: {PathOf(r.transform)}");
                }
            }
        }
    }

    bool ShouldDraw(PagePiece.PieceClass c)
    {
        switch (c)
        {
            case PagePiece.PieceClass.Card:
                return _drawCards;
            case PagePiece.PieceClass.Volume:
                return _drawVolumes;
            case PagePiece.PieceClass.Rider:
                return _drawRiders;
            case PagePiece.PieceClass.Flat:
                return _drawFlat;
            default:
                return false;
        }
    }

    /// <summary>Drops the proxies and shows every hidden renderer again; checks nothing stayed hidden.</summary>
    void Finish()
    {
        foreach (PaperPiece p in _pieces)
        {
            foreach (PaperProxy proxy in p.Proxies)
            {
                proxy.Release();
            }
        }
        _pieces.Clear();
        int restored = 0, missing = 0;
        foreach (KeyValuePair<Renderer, bool> h in _hidden)
        {
            if (h.Key == null)
            {
                missing++;
                continue;
            }
            h.Key.forceRenderingOff = h.Value;
            restored++;
        }
        _hidden.Clear();
        for (int i = 0; i < 4; i++)
        {
            if (_captureCams[i] != null)
            {
                Destroy(_captureCams[i].gameObject);
            }
            if (_captures[i] != null)
            {
                _captures[i].Release();
                Destroy(_captures[i]);
            }
            _captureCams[i] = null;
            _captures[i] = null;
        }
        _capturing = false;
        _introMode = false;
        _folders[0] = _folders[1] = null;
        if (_sheet != null)
        {
            Destroy(_sheet.gameObject);
            _sheet = null;
        }
        Debug.Log($"[PaperTurn] cleaned up: {restored} renderers shown again" +
                  (_lateHidden > 0 ? $" ({_lateHidden} of them appeared during the turn)" : "") +
                  (missing > 0 ? $", {missing} were destroyed during the turn" : ""));
    }

    void OnDestroy()
    {
        if (_sheet != null || _hidden.Count > 0)
        {
            Finish();
        }
    }

    static string PathOf(Transform t)
    {
        StringBuilder sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            sb.Insert(0, p.name + "/");
        }
        return sb.ToString();
    }

    void OnDrawGizmos()
    {
        if (!_gizmos || _model == null || _sheet == null)
        {
            return;
        }
        float zMid = 0.5f * (_book.pageZ.x + _book.pageZ.y);
        V2[] pts = _model.SheetPoints;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Gizmos.DrawLine(_frame.ToWorld(new Vector3((float)pts[i].x, (float)pts[i].y, zMid)),
                            _frame.ToWorld(new Vector3((float)pts[i + 1].x, (float)pts[i + 1].y, zMid)));
        }
        foreach (PaperPiece p in _pieces)
        {
            Gizmos.color = p.Class == PagePiece.PieceClass.Card ? Color.cyan
                : p.Class == PagePiece.PieceClass.Volume ? Color.magenta
                : p.Class == PagePiece.PieceClass.Rider ? Color.green : Color.white;
            Gizmos.matrix = p.Map;
            Vector3 c = _frame.ToWorld(p.Canon.center);
            Vector3 s = p.Canon.size;
            Gizmos.DrawWireCube(c, new Vector3(s.x, s.y, s.z));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
