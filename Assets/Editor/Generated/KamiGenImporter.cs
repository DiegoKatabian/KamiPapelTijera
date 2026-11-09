using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a GameObject hierarchy with meshes and its animation clips from a ".kgen" JSON file written by a Python
/// generator (spec 025 D1: tools/make-narrator-hand.py, tools/make-intro-books.py, tools/make-book-intro-clips.py).
/// Python computes every number; this importer only turns them into Unity objects, so Unity writes the meshes and
/// clips itself (a hand-written .anim can import broken without an error). The outputs are read-only sub-assets of
/// the .kgen file, and Claude reads them back from Library/Artifacts (tools/verify-gen.py --artifacts).
///
/// A clips-only file (no nodes) imports as an empty GameObject carrying the clips as sub-assets.
/// </summary>
[ScriptedImporter(3, "kgen")]
public class KamiGenImporter : ScriptedImporter
{
    [Serializable]
    class GenFile
    {
        public string name;
        public bool skinned;
        public GenNode[] nodes;
        public GenMesh[] meshes;
        public GenClip[] clips;
    }

    [Serializable]
    class GenNode
    {
        public string name;
        public int parent = -1;
        public float[] pos;
        public float[] rot;
        public float[] scale;
        public int mesh = -1;
        public string[] materials;
        public bool castShadows = true;
    }

    [Serializable]
    class GenMesh
    {
        public string name;
        public float[] vertices;
        public float[] normals;
        public float[] uv;
        public int[] subMeshStarts;
        public int[] indices;
    }

    [Serializable]
    class GenClip
    {
        public string name;
        public float length;
        public bool loop;
        public GenCurve[] curves;
    }

    [Serializable]
    class GenCurve
    {
        public string path;
        public string property; // localPosition | localRotation | localScale
        public float[] times;
        public float[] values; // 3 or 4 per time
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        GenFile file;
        try
        {
            file = JsonUtility.FromJson<GenFile>(File.ReadAllText(ctx.assetPath));
        }
        catch (Exception e)
        {
            ctx.LogImportError($"[KamiGenImporter] {ctx.assetPath}: not valid JSON ({e.Message})");
            return;
        }
        string rootName = string.IsNullOrEmpty(file.name) ? Path.GetFileNameWithoutExtension(ctx.assetPath) : file.name;
        GameObject root = new GameObject(rootName);

        Mesh[] meshes = new Mesh[file.meshes?.Length ?? 0];
        for (int i = 0; i < meshes.Length; i++)
        {
            meshes[i] = BuildMesh(file.meshes[i], ctx);
            if (!file.skinned)
            {
                ctx.AddObjectToAsset("mesh " + file.meshes[i].name, meshes[i]);
            }
        }

        Transform[] nodes = new Transform[file.nodes?.Length ?? 0];
        for (int i = 0; i < nodes.Length; i++)
        {
            GenNode n = file.nodes[i];
            GameObject go = new GameObject(n.name);
            Transform parent = n.parent >= 0 && n.parent < i ? nodes[n.parent] : root.transform;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = V3(n.pos, Vector3.zero);
            go.transform.localRotation = n.rot != null && n.rot.Length == 4 ? new Quaternion(n.rot[0], n.rot[1], n.rot[2], n.rot[3]) : Quaternion.identity;
            go.transform.localScale = V3(n.scale, Vector3.one);
            nodes[i] = go.transform;
        }

        if (file.skinned)
        {
            BuildSkinned(root, file, nodes, meshes, ctx);
        }
        else
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                GenNode n = file.nodes[i];
                if (n.mesh < 0 || n.mesh >= meshes.Length)
                {
                    continue;
                }
                MeshFilter mf = nodes[i].gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = meshes[n.mesh];
                MeshRenderer mr = nodes[i].gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterials = LoadMaterials(n.materials, meshes[n.mesh].subMeshCount, ctx);
                mr.shadowCastingMode = n.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        if (file.clips != null)
        {
            foreach (GenClip c in file.clips)
            {
                AnimationClip clip = BuildClip(c, ctx);
                ctx.AddObjectToAsset("clip " + c.name, clip);
            }
        }

        ctx.AddObjectToAsset("root", root);
        ctx.SetMainObject(root);
    }

    static Vector3 V3(float[] a, Vector3 fallback) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : fallback;

    static Mesh BuildMesh(GenMesh g, AssetImportContext ctx)
    {
        Mesh m = new Mesh { name = g.name };
        int count = g.vertices.Length / 3;
        Vector3[] v = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            v[i] = new Vector3(g.vertices[i * 3], g.vertices[i * 3 + 1], g.vertices[i * 3 + 2]);
        }
        if (count > 65535)
        {
            m.indexFormat = IndexFormat.UInt32;
        }
        m.vertices = v;
        if (g.uv != null && g.uv.Length == count * 2)
        {
            Vector2[] uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                uv[i] = new Vector2(g.uv[i * 2], g.uv[i * 2 + 1]);
            }
            m.uv = uv;
        }
        int subs = g.subMeshStarts != null && g.subMeshStarts.Length > 0 ? g.subMeshStarts.Length : 1;
        m.subMeshCount = subs;
        for (int s = 0; s < subs; s++)
        {
            int start = g.subMeshStarts != null && g.subMeshStarts.Length > 0 ? g.subMeshStarts[s] : 0;
            int end = g.subMeshStarts != null && s + 1 < g.subMeshStarts.Length ? g.subMeshStarts[s + 1] : g.indices.Length;
            int[] tris = new int[end - start];
            Array.Copy(g.indices, start, tris, 0, end - start);
            m.SetTriangles(tris, s);
        }
        if (g.normals != null && g.normals.Length == count * 3)
        {
            Vector3[] n = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                n[i] = new Vector3(g.normals[i * 3], g.normals[i * 3 + 1], g.normals[i * 3 + 2]);
            }
            m.normals = n;
        }
        else
        {
            m.RecalculateNormals();
        }
        if (m.uv != null && m.uv.Length == count)
        {
            m.RecalculateTangents();
        }
        m.RecalculateBounds();
        return m;
    }

    static Material[] LoadMaterials(string[] paths, int count, AssetImportContext ctx)
    {
        Material[] mats = new Material[Mathf.Max(count, 1)];
        for (int i = 0; i < mats.Length; i++)
        {
            string p = paths != null && paths.Length > 0 ? paths[Mathf.Min(i, paths.Length - 1)] : null;
            if (string.IsNullOrEmpty(p))
            {
                continue;
            }
            ctx.DependsOnSourceAsset(p);
            mats[i] = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mats[i] == null)
            {
                ctx.LogImportWarning($"[KamiGenImporter] material not found: {p}");
            }
        }
        return mats;
    }

    /// <summary>All node meshes merged into one SkinnedMeshRenderer, every vertex fully on its node (spec 025 D2 fallback).</summary>
    static void BuildSkinned(GameObject root, GenFile file, Transform[] nodes, Mesh[] meshes, AssetImportContext ctx)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<BoneWeight> w = new List<BoneWeight>();
        List<int> tris = new List<int>();
        List<Material> mats = new List<Material>();
        Matrix4x4 rootInv = root.transform.worldToLocalMatrix;
        Matrix4x4[] bind = new Matrix4x4[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            bind[i] = nodes[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
        }
        for (int i = 0; i < nodes.Length; i++)
        {
            GenNode g = file.nodes[i];
            if (g.mesh < 0)
            {
                continue;
            }
            Mesh m = meshes[g.mesh];
            Matrix4x4 toRoot = rootInv * nodes[i].localToWorldMatrix;
            int baseIndex = v.Count;
            Vector3[] mv = m.vertices;
            Vector3[] mn = m.normals;
            Vector2[] muv = m.uv;
            for (int k = 0; k < mv.Length; k++)
            {
                v.Add(toRoot.MultiplyPoint3x4(mv[k]));
                n.Add(toRoot.MultiplyVector(mn[k]).normalized);
                uv.Add(muv != null && muv.Length == mv.Length ? muv[k] : Vector2.zero);
                w.Add(new BoneWeight { boneIndex0 = i, weight0 = 1 });
            }
            foreach (int t in m.triangles)
            {
                tris.Add(t + baseIndex);
            }
            if (mats.Count == 0)
            {
                mats.AddRange(LoadMaterials(g.materials, 1, ctx));
            }
        }
        Mesh skinned = new Mesh { name = file.name + " (skinned)" };
        skinned.SetVertices(v);
        skinned.SetNormals(n);
        skinned.SetUVs(0, uv);
        skinned.SetTriangles(tris, 0);
        skinned.boneWeights = w.ToArray();
        skinned.bindposes = bind;
        skinned.RecalculateBounds();
        skinned.RecalculateTangents();
        ctx.AddObjectToAsset("mesh skinned", skinned);
        GameObject body = new GameObject("Skin");
        body.transform.SetParent(root.transform, false);
        SkinnedMeshRenderer smr = body.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = skinned;
        smr.bones = nodes;
        smr.rootBone = nodes.Length > 0 ? nodes[0] : root.transform;
        smr.sharedMaterials = mats.ToArray();
        smr.updateWhenOffscreen = true;
    }

    static AnimationClip BuildClip(GenClip c, AssetImportContext ctx)
    {
        AnimationClip clip = new AnimationClip { name = c.name, frameRate = 30 };
        if (c.curves != null)
        {
            foreach (GenCurve curve in c.curves)
            {
                int stride = curve.property == "localRotation" ? 4 : 3;
                string prefix = curve.property == "localRotation" ? "m_LocalRotation"
                    : curve.property == "localScale" ? "m_LocalScale" : "m_LocalPosition";
                if (curve.times == null || curve.values == null || curve.values.Length != curve.times.Length * stride)
                {
                    ctx.LogImportWarning($"[KamiGenImporter] clip {c.name}: curve {curve.path}.{curve.property} has mismatched keys, skipped");
                    continue;
                }
                string[] axes = { "x", "y", "z", "w" };
                for (int a = 0; a < stride; a++)
                {
                    Keyframe[] keys = new Keyframe[curve.times.Length];
                    for (int k = 0; k < keys.Length; k++)
                    {
                        keys[k] = new Keyframe(curve.times[k], curve.values[k * stride + a]);
                    }
                    AnimationCurve ac = new AnimationCurve(keys);
                    for (int k = 0; k < keys.Length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(ac, k, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(ac, k, AnimationUtility.TangentMode.ClampedAuto);
                    }
                    EditorCurveBinding b = EditorCurveBinding.FloatCurve(curve.path, typeof(Transform), $"{prefix}.{axes[a]}");
                    AnimationUtility.SetEditorCurve(clip, b, ac);
                }
            }
        }
        clip.EnsureQuaternionContinuity();
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = c.loop;
        settings.stopTime = c.length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }
}
