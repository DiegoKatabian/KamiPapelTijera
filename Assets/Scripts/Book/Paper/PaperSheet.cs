using Kami.Paper;
using UnityEngine;

/// <summary>
/// The procedural turning sheet's look (spec 025 D23): four strip meshes rebuilt every frame from PaperTurnModel's
/// curve (the front and back faces, and over each the capture of the flat pieces riding it), plus the Edge Kami hangs
/// from. PaperTurn creates one per turn from Assets/Prefabs/Book/PaperSheet.prefab and drives it; the prefab only
/// holds the renderers so their shadow and probe settings can be edited.
/// </summary>
public class PaperSheet : MonoBehaviour
{
    [SerializeField] MeshFilter _front;
    [SerializeField] MeshFilter _back;
    [SerializeField] MeshFilter _frontOverlay;
    [SerializeField] MeshFilter _backOverlay;
    [Tooltip("The flat pieces of the page arriving, lying still on the half the sheet lifts from.")]
    [SerializeField] MeshFilter _startOverlay;
    [Tooltip("The flat pieces of the page leaving, lying still on the half the sheet lands on.")]
    [SerializeField] MeshFilter _endOverlay;
    [Tooltip("Kami hangs from this while the sheet turns (Player.StartRidingPage).")]
    [SerializeField] Transform _edge;

    public Transform Edge => _edge;
    public Renderer FrontRenderer => _front != null ? _front.GetComponent<Renderer>() : null;
    public Renderer BackRenderer => _back != null ? _back.GetComponent<Renderer>() : null;

    const int N = PaperCurl.Segments + 1;
    Mesh _frontMesh, _backMesh, _frontOverlayMesh, _backOverlayMesh, _startOverlayMesh, _endOverlayMesh;
    PaperFrame _frame;
    float _z0, _z1, _lift;
    readonly Vector3[] _v = new Vector3[N * 2];
    readonly Vector3[] _n = new Vector3[N * 2];
    readonly Vector3[] _canonNormal = new Vector3[N];

    /// <summary>
    /// Builds the meshes. captures/rects are indexed by Kami.Paper.LeafKind (StartStatic, EndStatic, SheetFront,
    /// SheetBack): the flat pieces of that page and half, captured from above over the world x/z rectangle
    /// (x0, z0, width, depth). A null capture leaves that overlay off.
    /// </summary>
    public void Setup(PaperTurnModel model, PaperFrame frame, PaperBookData book, Material frontMaterial, Material backMaterial,
                      Material overlayMaterial, RenderTexture[] captures, Rect[] rects)
    {
        if (_front == null || _back == null || _edge == null)
        {
            Debug.LogWarning($"[PaperSheet] '{name}' is missing its face or edge references: check PaperSheet.prefab");
            return;
        }
        _frame = frame;
        _z0 = book.pageZ.x;
        _z1 = book.pageZ.y;
        _lift = book.sheetLift;
        bool startRight = frame.Dir > 0;
        _frontMesh = NewMesh("Paper sheet front", false, model.RestFrontPoints, startRight, book, null, default);
        _backMesh = NewMesh("Paper sheet back", true, model.RestBackPoints, !startRight, book, null, default);
        Attach(_front, _frontMesh, frontMaterial);
        Attach(_back, _backMesh, backMaterial);
        _frontOverlayMesh = Overlay(_frontOverlay, "Paper sheet front overlay", false, model.RestFrontPoints, startRight, book,
                                    overlayMaterial, captures, rects, (int)LeafKind.SheetFront);
        _backOverlayMesh = Overlay(_backOverlay, "Paper sheet back overlay", true, model.RestBackPoints, !startRight, book,
                                   overlayMaterial, captures, rects, (int)LeafKind.SheetBack);
        _startOverlayMesh = Overlay(_startOverlay, "Paper start half overlay", false, model.RestFrontPoints, startRight, book,
                                    overlayMaterial, captures, rects, (int)LeafKind.StartStatic);
        _endOverlayMesh = Overlay(_endOverlay, "Paper end half overlay", true, model.RestBackPoints, !startRight, book,
                                  overlayMaterial, captures, rects, (int)LeafKind.EndStatic);
        // the two static overlays lie still on the book, on the sheet's rest shapes, under the sheet's own faces
        if (_startOverlayMesh != null)
        {
            Fill(_startOverlayMesh, model.RestFrontPoints, RestNormals(model.RestFrontPoints), 0.5f * _lift, 1);
        }
        if (_endOverlayMesh != null)
        {
            Fill(_endOverlayMesh, model.RestBackPoints, RestNormals(model.RestBackPoints), -0.5f * _lift, -1);
        }
        UpdateShape(model);
    }

    Mesh Overlay(MeshFilter mf, string meshName, bool backFace, V2[] rest, bool restOnRight, PaperBookData book,
                 Material overlayMaterial, RenderTexture[] captures, Rect[] rects, int index)
    {
        if (mf == null)
        {
            return null;
        }
        RenderTexture capture = captures != null && index < captures.Length ? captures[index] : null;
        if (capture == null || overlayMaterial == null)
        {
            mf.gameObject.SetActive(false);
            return null;
        }
        Mesh m = NewMesh(meshName, backFace, rest, restOnRight, book, capture, rects[index]);
        Attach(mf, m, OverlayFor(overlayMaterial, capture));
        return m;
    }

    Vector3[] RestNormals(V2[] p)
    {
        Vector3[] n = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            int a = i == 0 ? 0 : i - 1, b = i == N - 1 ? N - 1 : i + 1;
            double ang = System.Math.Atan2(p[b].y - p[a].y, p[b].x - p[a].x);
            n[i] = new Vector3((float)-System.Math.Sin(ang), (float)System.Math.Cos(ang), 0);
        }
        return n;
    }

    static Material OverlayFor(Material template, RenderTexture capture)
    {
        Material m = new Material(template) { name = template.name + " (paper turn)" };
        m.mainTexture = capture;
        return m;
    }

    static void Attach(MeshFilter mf, Mesh mesh, Material material)
    {
        mf.sharedMesh = mesh;
        Renderer r = mf.GetComponent<Renderer>();
        if (r != null && material != null)
        {
            r.sharedMaterial = material;
        }
    }

    /// <summary>
    /// A strip mesh: two vertices (page's near and far edge along the spine) per point of the curve. UVs come from where
    /// each vertex lies when the sheet rests on its half: the book mesh's own uv there (so the face shows exactly the
    /// picture under it) or, for an overlay, its place in the capture's rectangle.
    /// </summary>
    Mesh NewMesh(string meshName, bool backFace, V2[] rest, bool restOnRight, PaperBookData book, RenderTexture capture, Rect rect)
    {
        Mesh m = new Mesh { name = meshName };
        m.MarkDynamic();
        Vector2[] uv = new Vector2[N * 2];
        for (int i = 0; i < N; i++)
        {
            Vector3 w = _frame.ToWorld(new Vector3((float)rest[i].x, (float)rest[i].y, 0));
            for (int e = 0; e < 2; e++)
            {
                float z = e == 0 ? _z0 : _z1;
                uv[i * 2 + e] = capture != null
                    ? new Vector2((w.x - rect.x) / rect.width, (z - rect.y) / rect.height)
                    : book.UvAt(restOnRight, w.x, z);
            }
        }
        m.vertices = new Vector3[N * 2];
        m.uv = uv;
        m.triangles = Triangles(backFace);
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
        return m;
    }

    int[] Triangles(bool back)
    {
        int[] t = new int[(N - 1) * 6];
        // front faces the curve's normal; the world mirror (going backward) and the back face each flip the winding
        bool flip = back ^ (_frame.Dir < 0);
        for (int i = 0; i < N - 1; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
            int k = i * 6;
            if (!flip)
            {
                t[k] = a; t[k + 1] = b; t[k + 2] = c;
                t[k + 3] = b; t[k + 4] = d; t[k + 5] = c;
            }
            else
            {
                t[k] = a; t[k + 1] = c; t[k + 2] = b;
                t[k + 3] = b; t[k + 4] = c; t[k + 5] = d;
            }
        }
        return t;
    }

    /// <summary>Hides the sheet's own two faces (the level intro: the intro book's cover is the paper).</summary>
    public void SetFacesVisible(bool visible)
    {
        foreach (MeshFilter mf in new[] { _front, _back })
        {
            Renderer r = mf != null ? mf.GetComponent<Renderer>() : null;
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }

    /// <summary>Moves every vertex and the Edge to the model's current curve.</summary>
    public void UpdateShape(PaperTurnModel model)
    {
        if (_frontMesh == null)
        {
            return;
        }
        V2[] p = model.SheetPoints;
        double[] a = model.SheetAngles;
        for (int i = 0; i < N; i++)
        {
            // vertex normal: the average of the segments on each side
            double ang = i == 0 ? a[0] : (i == N - 1 ? a[N - 2] : 0.5 * (a[i - 1] + a[i]));
            _canonNormal[i] = new Vector3((float)-System.Math.Sin(ang), (float)System.Math.Cos(ang), 0);
        }
        Fill(_frontMesh, p, _canonNormal, _lift, 1);
        Fill(_backMesh, p, _canonNormal, -_lift, -1);
        if (_frontOverlayMesh != null)
        {
            Fill(_frontOverlayMesh, p, _canonNormal, 2 * _lift, 1);
        }
        if (_backOverlayMesh != null)
        {
            Fill(_backOverlayMesh, p, _canonNormal, -2 * _lift, -1);
        }
        V2 tip = p[N - 1];
        _edge.position = _frame.ToWorld(new Vector3((float)tip.x, (float)tip.y, 0.5f * (_z0 + _z1)));
    }

    void Fill(Mesh m, V2[] p, Vector3[] normals, float offset, int normalSign)
    {
        for (int i = 0; i < N; i++)
        {
            Vector3 cn = normals[i];
            Vector3 c = new Vector3((float)p[i].x + cn.x * offset, (float)p[i].y + cn.y * offset, 0);
            Vector3 wn = new Vector3(_frame.Dir * cn.x * normalSign, cn.y * normalSign, 0);
            for (int e = 0; e < 2; e++)
            {
                c.z = e == 0 ? _z0 : _z1;
                _v[i * 2 + e] = _frame.ToWorld(c);
                _n[i * 2 + e] = wn;
            }
        }
        m.vertices = _v;
        m.normals = _n;
    }

    void OnDestroy()
    {
        foreach (Mesh m in new[] { _frontMesh, _backMesh, _frontOverlayMesh, _backOverlayMesh, _startOverlayMesh, _endOverlayMesh })
        {
            if (m != null)
            {
                Destroy(m);
            }
        }
        foreach (MeshFilter mf in new[] { _frontOverlay, _backOverlay, _startOverlay, _endOverlay })
        {
            Renderer r = mf != null ? mf.GetComponent<Renderer>() : null;
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.name.EndsWith("(paper turn)"))
            {
                Destroy(r.sharedMaterial);
            }
        }
    }
}
