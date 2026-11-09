using System.Collections.Generic;
using System.Text;
using Kami.Paper;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Canonical book space for one turn (spec 025): x across the page from the spine with the turn's start side positive,
/// y up, z along the spine. Going forward the start side is the right half (+world x), going back the left half.
/// </summary>
public struct PaperFrame
{
    public float SpineX;
    public int Dir; // +1 forward (start side = +world x), -1 backward

    public Vector3 ToCanon(Vector3 w) => new Vector3(Dir * (w.x - SpineX), w.y, w.z);
    public Vector3 ToWorld(Vector3 c) => new Vector3(SpineX + Dir * c.x, c.y, c.z);

    /// <summary>World matrix of a canonical affine map: B * M * B^-1 (the x mirror appears twice, so it is a proper map).</summary>
    public Matrix4x4 WorldMap(Affine3 m)
    {
        Matrix4x4 c = Matrix4x4.identity;
        c.m00 = (float)m.m00; c.m01 = (float)m.m01; c.m02 = (float)m.m02; c.m03 = (float)m.t0;
        c.m10 = (float)m.m10; c.m11 = (float)m.m11; c.m12 = (float)m.m12; c.m13 = (float)m.t1;
        c.m20 = (float)m.m20; c.m21 = (float)m.m21; c.m22 = (float)m.m22; c.m23 = (float)m.t2;
        Matrix4x4 b = Matrix4x4.Translate(new Vector3(SpineX, 0, 0)) * Matrix4x4.Scale(new Vector3(Dir, 1, 1));
        Matrix4x4 bInv = Matrix4x4.Scale(new Vector3(Dir, 1, 1)) * Matrix4x4.Translate(new Vector3(-SpineX, 0, 0));
        return b * c * bInv;
    }
}

/// <summary>A draw-only copy of one renderer (spec 025 D25): drawn with Graphics.DrawMesh, so nothing real moves.</summary>
public class PaperProxy
{
    public Renderer Source;
    public Mesh Mesh;
    public bool OwnsMesh;
    public Material[] Materials;
    public MaterialPropertyBlock Block;
    public Matrix4x4 Rest;
    public ShadowCastingMode Shadows;
    public bool ReceiveShadows;
    public int Layer;

    public void Draw(Matrix4x4 map, Camera camera, int layerOverride = -1)
    {
        if (Mesh == null || Materials == null || Materials.Length == 0)
        {
            return;
        }
        Matrix4x4 m = map * Rest;
        int subs = Mesh.subMeshCount;
        int layer = layerOverride >= 0 ? layerOverride : Layer;
        for (int i = 0; i < subs; i++)
        {
            Material mat = Materials[Mathf.Min(i, Materials.Length - 1)];
            if (mat == null)
            {
                continue;
            }
            Graphics.DrawMesh(Mesh, m, mat, layer, camera, i, Block, Shadows, ReceiveShadows, null, LightProbeUsage.BlendProbes, null);
        }
    }

    public void Release()
    {
        if (OwnsMesh && Mesh != null)
        {
            Object.Destroy(Mesh);
        }
        Mesh = null;
    }
}

/// <summary>A group of renderers that folds as one (spec 025 D27).</summary>
public class PaperPiece
{
    public string Path;
    public PagePiece.PieceClass Class;
    public bool Arriving; // false = the page being left
    public LeafKind Leaf;
    public PaperPieceRest Rest = new PaperPieceRest();
    public PaperPiece Host;
    public PagePiece HostTag; // a Rider's host as set on its PagePiece
    public readonly List<PaperProxy> Proxies = new List<PaperProxy>();
    public Bounds Canon; // canonical bounds
    public Matrix4x4 Map = Matrix4x4.identity;
    public bool AutoClassified;

    public bool OnSheet => Leaf == LeafKind.SheetFront || Leaf == LeafKind.SheetBack;
}

/// <summary>
/// Turns a page folder into pieces and proxies for a paper page turn (spec 025 D25, D27, D29). Shared by the runtime
/// (PaperTurn) and the Editor's tagging tool, so both classify the same way.
/// </summary>
public static class PaperPieces
{
    static readonly Vector3[] Unit = new Vector3[8];
    static readonly Dictionary<long, Mesh> SpriteMeshes = new Dictionary<long, Mesh>();

    /// <summary>The world corners of a renderer's own bounds (works on inactive objects, unlike Renderer.bounds).</summary>
    public static bool WorldCorners(Renderer r, Vector3[] corners)
    {
        Bounds local;
        if (r is SpriteRenderer sr)
        {
            if (sr.sprite == null)
            {
                return false;
            }
            local = sr.sprite.bounds;
        }
        else if (r is SkinnedMeshRenderer smr)
        {
            local = smr.localBounds;
        }
        else
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                return false;
            }
            local = mf.sharedMesh.bounds;
        }
        Matrix4x4 m = r.transform.localToWorldMatrix;
        Vector3 c = local.center, e = local.extents;
        int k = 0;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    corners[k++] = m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z)));
                }
            }
        }
        return true;
    }

    /// <summary>
    /// True if the renderer will be drawn while its page folder is on: enabled, and every object from it up to the
    /// folder active by itself (the folder itself may be off: the page arriving).
    /// </summary>
    public static bool IsLive(Renderer r, Transform folder)
    {
        if (r == null || !r.enabled || r.forceRenderingOff)
        {
            return false;
        }
        for (Transform t = r.transform; t != null && t != folder; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Renderers the turn must never touch: Kami, anyone following her, and Excluded pieces.</summary>
    public static bool IsUntouchable(Renderer r, Transform folder)
    {
        for (Transform t = r.transform; t != null && t != folder; t = t.parent)
        {
            if (t.GetComponent<Player>() != null)
            {
                return true;
            }
            NPC npc = t.GetComponent<NPC>();
            if (npc != null && npc.isFollowing)
            {
                return true;
            }
            PagePiece pp = t.GetComponent<PagePiece>();
            if (pp != null && (pp.pieceClass == PagePiece.PieceClass.Excluded || !pp.includeInTurn))
            {
                return true;
            }
        }
        return false;
    }

    static bool CanCopy(Renderer r) => r is MeshRenderer || r is SpriteRenderer || r is SkinnedMeshRenderer;

    /// <summary>
    /// Groups a page folder's renderers into pieces: a PagePiece starts a piece; otherwise a child whose renderers span
    /// less than maxPieceSize is one piece, a bigger group is split into its children.
    /// </summary>
    public static void Group(Transform folder, PaperBookData data, List<KeyValuePair<Transform, List<Renderer>>> groups,
                             List<Renderer> untouched)
    {
        Renderer[] all = folder.GetComponentsInChildren<Renderer>(true);
        HashSet<Renderer> live = new HashSet<Renderer>();
        foreach (Renderer r in all)
        {
            if (!IsLive(r, folder))
            {
                continue;
            }
            if (IsUntouchable(r, folder))
            {
                untouched.Add(r);
                continue;
            }
            live.Add(r);
        }
        Visit(folder, folder, data, live, groups);
    }

    static void Visit(Transform t, Transform folder, PaperBookData data, HashSet<Renderer> live,
                      List<KeyValuePair<Transform, List<Renderer>>> groups)
    {
        bool explicitPiece = t != folder && t.GetComponent<PagePiece>() != null;
        List<Renderer> subtree = new List<Renderer>();
        Collect(t, t, live, subtree);
        if (subtree.Count == 0)
        {
            return;
        }
        if (!explicitPiece)
        {
            Bounds b = WorldBounds(subtree);
            bool tooBig = b.size.x > data.maxPieceSize || b.size.z > data.maxPieceSize;
            if (t == folder || (tooBig && t.childCount > 0))
            {
                List<Renderer> own = new List<Renderer>();
                foreach (Renderer r in t.GetComponents<Renderer>())
                {
                    if (live.Contains(r))
                    {
                        own.Add(r);
                    }
                }
                if (own.Count > 0)
                {
                    groups.Add(new KeyValuePair<Transform, List<Renderer>>(t, own));
                }
                foreach (Transform c in t)
                {
                    Visit(c, folder, data, live, groups);
                }
                return;
            }
        }
        groups.Add(new KeyValuePair<Transform, List<Renderer>>(t, subtree));
        // nested PagePieces are their own pieces
        VisitNestedPieces(t, folder, data, live, groups);
    }

    static void VisitNestedPieces(Transform t, Transform folder, PaperBookData data, HashSet<Renderer> live,
                                  List<KeyValuePair<Transform, List<Renderer>>> groups)
    {
        foreach (Transform c in t)
        {
            if (c.GetComponent<PagePiece>() != null)
            {
                Visit(c, folder, data, live, groups);
            }
            else
            {
                VisitNestedPieces(c, folder, data, live, groups);
            }
        }
    }

    /// <summary>The live renderers under t, stopping at nested PagePieces (they are pieces of their own).</summary>
    static void Collect(Transform t, Transform root, HashSet<Renderer> live, List<Renderer> into)
    {
        if (t != root && t.GetComponent<PagePiece>() != null)
        {
            return;
        }
        foreach (Renderer r in t.GetComponents<Renderer>())
        {
            if (live.Contains(r))
            {
                into.Add(r);
            }
        }
        foreach (Transform c in t)
        {
            Collect(c, root, live, into);
        }
    }

    static readonly Vector3[] Corners8 = new Vector3[8];

    public static Bounds WorldBounds(List<Renderer> renderers)
    {
        bool any = false;
        Bounds b = new Bounds();
        foreach (Renderer r in renderers)
        {
            if (!WorldCorners(r, Corners8))
            {
                continue;
            }
            foreach (Vector3 p in Corners8)
            {
                if (!any)
                {
                    b = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    b.Encapsulate(p);
                }
            }
        }
        return b;
    }

    public static Bounds CanonBounds(List<Renderer> renderers, PaperFrame frame)
    {
        bool any = false;
        Bounds b = new Bounds();
        foreach (Renderer r in renderers)
        {
            if (!WorldCorners(r, Corners8))
            {
                continue;
            }
            foreach (Vector3 p in Corners8)
            {
                Vector3 c = frame.ToCanon(p);
                if (!any)
                {
                    b = new Bounds(c, Vector3.zero);
                    any = true;
                }
                else
                {
                    b.Encapsulate(c);
                }
            }
        }
        return b;
    }

    /// <summary>The automatic class of a piece (spec 025 D27), from its renderers and its world-aligned bounds.</summary>
    public static PagePiece.PieceClass Classify(List<Renderer> renderers, Bounds world, PaperBookData data)
    {
        bool allSprites = true;
        foreach (Renderer r in renderers)
        {
            if (!(r is SpriteRenderer))
            {
                allSprites = false;
            }
        }
        Vector3 s = world.size;
        float footprint = Mathf.Min(s.x, s.z);
        if (s.y < data.flatMaxThickness && s.y < data.flatThicknessRatio * Mathf.Max(footprint, 0.01f))
        {
            return PagePiece.PieceClass.Flat;
        }
        if (allSprites || (s.z < data.cardThicknessRatio * Mathf.Min(s.x, s.y) && s.z < 2f))
        {
            return PagePiece.PieceClass.Card;
        }
        return PagePiece.PieceClass.Volume;
    }

    /// <summary>Builds the proxy of one renderer, or returns null (and says why) when it can only be hidden.</summary>
    public static PaperProxy MakeProxy(Renderer r, out string whyNot)
    {
        whyNot = null;
        if (!CanCopy(r))
        {
            whyNot = $"a {r.GetType().Name} is hidden during the turn, not copied";
            return null;
        }
        if (r.isPartOfStaticBatch)
        {
            whyNot = "static batched (spec 025 D37: remove its Batching Static flag)";
            return null;
        }
        PaperProxy p = new PaperProxy
        {
            Source = r,
            Materials = r.sharedMaterials,
            Shadows = r.shadowCastingMode,
            ReceiveShadows = r.receiveShadows,
            Layer = r.gameObject.layer,
            Rest = r.transform.localToWorldMatrix,
        };
        if (r.HasPropertyBlock())
        {
            p.Block = new MaterialPropertyBlock();
            r.GetPropertyBlock(p.Block);
        }
        if (r is SpriteRenderer sr)
        {
            if (sr.sprite == null)
            {
                whyNot = "a SpriteRenderer without a sprite";
                return null;
            }
            p.Mesh = SpriteMesh(sr.sprite, sr.color);
            if (p.Block == null)
            {
                p.Block = new MaterialPropertyBlock();
            }
            p.Block.SetTexture("_MainTex", sr.sprite.texture);
            p.Rest = r.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(sr.flipX ? -1 : 1, sr.flipY ? -1 : 1, 1));
        }
        else if (r is SkinnedMeshRenderer smr)
        {
            if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
            {
                whyNot = "a skinned mesh on a page that is off (it can't be baked): it appears when the page opens";
                return null;
            }
            Mesh baked = new Mesh { name = smr.sharedMesh.name + " (paper turn)" };
            smr.BakeMesh(baked, false);
            p.Mesh = baked;
            p.OwnsMesh = true;
        }
        else
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                whyNot = "no mesh";
                return null;
            }
            if (r.GetComponent<Spine.Unity.SkeletonRenderer>() != null)
            {
                // Spine rewrites (and swaps) its mesh every frame: freeze a copy of this frame's pose
                if (!r.gameObject.activeInHierarchy)
                {
                    whyNot = "a Spine character on a page that is off: it appears when the page opens";
                    return null;
                }
                p.Mesh = Object.Instantiate(mf.sharedMesh);
                p.OwnsMesh = true;
            }
            else
            {
                p.Mesh = mf.sharedMesh;
            }
        }
        return p;
    }

    static Mesh SpriteMesh(Sprite sprite, Color color)
    {
        long key = ((long)sprite.GetInstanceID() << 32) ^ color.GetHashCode();
        if (SpriteMeshes.TryGetValue(key, out Mesh cached) && cached != null)
        {
            return cached;
        }
        Vector2[] v2 = sprite.vertices;
        Vector3[] v3 = new Vector3[v2.Length];
        Color[] colors = new Color[v2.Length];
        for (int i = 0; i < v2.Length; i++)
        {
            v3[i] = v2[i];
            colors[i] = color;
        }
        ushort[] tri = sprite.triangles;
        int[] tris = new int[tri.Length];
        for (int i = 0; i < tri.Length; i++)
        {
            tris[i] = tri[i];
        }
        Mesh m = new Mesh { name = sprite.name + " (paper turn)" };
        m.vertices = v3;
        m.uv = sprite.uv;
        m.colors = colors;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        SpriteMeshes[key] = m;
        return m;
    }

    /// <summary>Finds each Rider's host: the non-flat piece whose footprint holds the rider's centre and whose top it rests on.</summary>
    public static void ResolveRiders(List<PaperPiece> pieces, StringBuilder log)
    {
        foreach (PaperPiece p in pieces)
        {
            if (p.Class != PagePiece.PieceClass.Rider || p.Host != null)
            {
                continue;
            }
            PaperPiece best = null;
            float bestGap = float.MaxValue;
            foreach (PaperPiece h in pieces)
            {
                if (h == p || h.Class == PagePiece.PieceClass.Flat || h.Class == PagePiece.PieceClass.Rider || h.Arriving != p.Arriving)
                {
                    continue;
                }
                Vector3 c = p.Canon.center;
                if (c.x < h.Canon.min.x || c.x > h.Canon.max.x || c.z < h.Canon.min.z || c.z > h.Canon.max.z)
                {
                    continue;
                }
                float gap = Mathf.Abs(p.Canon.min.y - h.Canon.max.y);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = h;
                }
            }
            if (best == null)
            {
                p.Class = PagePiece.PieceClass.Volume;
                log?.AppendLine($"  {p.Path}: marked Rider but nothing under it, folds as a Volume");
            }
            else
            {
                p.Host = best;
            }
        }
    }
}
