"""Measures where Level 2's book pages and page-turn sheets sit in the world, for
make-level2-newspaper-pages.py, which needs it to fit its pictures to them exactly.

Writes tools/data/level2-book-pages.json:
  - each half of the open book (Diario.fbx, Level2_Newspaper.unity): its UV rectangle and its top
    triangles as (world x, world z, u, v);
  - each face of the turning sheet (HojaMaster[_Rev] Newspaper.prefab) at the moment it lies flat on
    the book: forward-start (on the right half), forward-end (left), backward-start (left),
    backward-end (right), as (u, v, world x, world z) triangles.

Everything is read in Unity's own space: meshes, bind poses and the fbx clip from the import
artifacts in Library/Artifacts (so the Editor must have imported the project once), transforms from
the prefab and scene YAML, idle poses from the .anim YAML. Re-run it if the book, the sheet rig, the
sheet prefabs or the book's transform in the scene change.

    python tools/measure-book-pages.py
"""
import json
import os
import re
import struct
import zlib

import numpy as np
import yaml

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'tools', 'data', 'level2-book-pages.json')
SCENE = 'Assets/Scenes/Level2_Newspaper.unity'
BOOK_FBX = 'Assets/3D/Libros/Diario Kami/Meshes/Diario.fbx'
SHEETS = {'forward': 'Assets/Prefabs/Hoja/HojaMaster Newspaper.prefab',
          'backward': 'Assets/Prefabs/Hoja/HojaMaster_Rev Newspaper.prefab'}
# Diario's submeshes: 0 = cover (Libro), 1 = right page (HojaA_IMPAR), 2 = left page (HojaB_PAR)
BOOK_HALVES = {'right': 1, 'left': 2}


def path(rel):
    return os.path.join(ROOT, rel)


def guid_of(rel):
    return re.search(r'guid: (\w+)', open(path(rel) + '.meta', encoding='utf-8').read()).group(1)


def asset_of_guid(guid):
    for base, _, files in os.walk(path('Assets')):
        for f in files:
            if f.endswith('.meta') and ('guid: ' + guid) in open(os.path.join(base, f), encoding='utf-8', errors='ignore').read():
                return os.path.relpath(os.path.join(base, f[:-5]), ROOT).replace('\\', '/')
    raise KeyError(guid)


# ---------------------------------------------------------------------------------------------
# Unity serialized files (import artifacts, v22 with type trees), read generically
# ---------------------------------------------------------------------------------------------

COMMON = ['AABB', 'AnimationClip', 'AnimationCurve', 'AnimationState', 'Array', 'Base', 'BitField', 'bitset', 'bool',
          'char', 'ColorRGBA', 'Component', 'data', 'deque', 'double', 'dynamic_array', 'FastPropertyName', 'first',
          'float', 'Font', 'GameObject', 'Generic Mono', 'GradientNEW', 'GUID', 'GUIStyle', 'int', 'list', 'long long',
          'map', 'Matrix4x4f', 'MdFour', 'MonoBehaviour', 'MonoScript', 'm_ByteSize', 'm_Curve',
          'm_EditorClassIdentifier', 'm_EditorHideFlags', 'm_Enabled', 'm_ExtensionPtr', 'm_GameObject', 'm_Index',
          'm_IsArray', 'm_IsStatic', 'm_MetaFlag', 'm_Name', 'm_ObjectHideFlags', 'm_PrefabInternal',
          'm_PrefabParentObject', 'm_Script', 'm_StaticEditorFlags', 'm_Type', 'm_Version', 'Object', 'pair',
          'PPtr<Component>', 'PPtr<GameObject>', 'PPtr<Material>', 'PPtr<MonoBehaviour>', 'PPtr<MonoScript>',
          'PPtr<Object>', 'PPtr<Prefab>', 'PPtr<Sprite>', 'PPtr<TextAsset>', 'PPtr<Texture>', 'PPtr<Texture2D>',
          'PPtr<Transform>', 'Prefab', 'Quaternionf', 'Rectf', 'RectInt', 'RectOffset', 'second', 'set', 'short', 'size',
          'SInt16', 'SInt32', 'SInt64', 'SInt8', 'staticvector', 'string', 'TextAsset', 'TextMesh', 'Texture',
          'Texture2D', 'Transform', 'TypelessData', 'UInt16', 'UInt32', 'UInt64', 'UInt8', 'unsigned int',
          'unsigned long long', 'unsigned short', 'vector', 'Vector2f', 'Vector3f', 'Vector4f']
COMMON_AT = {}
_off = 0
for _w in COMMON:
    COMMON_AT[_off] = _w
    _off += len(_w) + 1

PRIM = {('float', 4): '<f', ('double', 8): '<d', ('int', 4): '<i', ('SInt32', 4): '<i', ('UInt32', 4): '<I',
        ('unsigned int', 4): '<I', ('SInt64', 8): '<q', ('UInt64', 8): '<Q', ('long long', 8): '<q',
        ('unsigned long long', 8): '<Q', ('SInt16', 2): '<h', ('UInt16', 2): '<H', ('short', 2): '<h',
        ('unsigned short', 2): '<H', ('UInt8', 1): '<B', ('SInt8', 1): '<b', ('char', 1): '<B', ('bool', 1): '<?'}


class TNode:
    def __init__(self, type_, name, size, flags, meta, level):
        self.type, self.name, self.size, self.flags, self.meta, self.level = type_, name, size, flags, meta, level
        self.children = []


def _tree(data, p):
    n, sbuf = struct.unpack_from('<ii', data, p)
    p += 8
    raw = [struct.unpack_from('<HBBIIiiiQ', data, p + 32 * i) for i in range(n)]
    p += 32 * n
    strings = data[p:p + sbuf]
    p += sbuf

    def s(off):
        if off & 0x80000000:
            return COMMON_AT.get(off & 0x7FFFFFFF, '?')
        return strings[off:strings.index(b'\0', off)].decode()

    nodes = [TNode(s(t), s(nm), size, flags, meta, level) for _, level, flags, t, nm, size, _, meta, _ in raw]
    stack = []
    for node in nodes:
        while stack and stack[-1].level >= node.level:
            stack.pop()
        if stack:
            stack[-1].children.append(node)
        stack.append(node)
    return nodes[0], p


def _read(node, data, p):
    if node.flags & 1:
        n = struct.unpack_from('<i', data, p)[0]
        p += 4
        el = node.children[1]
        if not el.children and el.size > 0:
            val = data[p:p + n * el.size] if el.size == 1 else [struct.unpack_from(PRIM[(el.type, el.size)], data, p + i * el.size)[0] for i in range(n)]
            p += n * el.size
        else:
            val = []
            for _ in range(n):
                v, p = _read(el, data, p)
                val.append(v)
    elif node.children:
        if node.type == 'string':
            n = struct.unpack_from('<i', data, p)[0]
            val = data[p + 4:p + 4 + n].decode('utf-8', 'replace')
            p += 4 + n
            if node.children[0].meta & 0x4000:
                p = (p + 3) & ~3
        else:
            val = {}
            for c in node.children:
                val[c.name], p = _read(c, data, p)
            if list(val) == ['Array']:
                val = val['Array']
    else:
        fmt = PRIM.get((node.type, node.size))
        val = struct.unpack_from(fmt, data, p)[0] if fmt else data[p:p + node.size]
        p += node.size
    if node.meta & 0x4000:
        p = (p + 3) & ~3
    return val, p


def artifact_objects(file):
    data = open(file, 'rb').read()
    if struct.unpack_from('>I', data, 8)[0] < 22:
        return []
    data_offset = struct.unpack_from('>Q', data, 32)[0]
    p = data.index(b'\0', 48) + 1 + 4
    has_trees = data[p]
    p += 1
    if not has_trees:
        return []
    types = []
    ntypes = struct.unpack_from('<i', data, p)[0]
    p += 4
    for _ in range(ntypes):
        cid = struct.unpack_from('<i', data, p)[0]
        p += 7 + (16 if cid < 0 or cid == 114 else 0) + 16
        tree, p = _tree(data, p)
        p += 4 + 4 * struct.unpack_from('<i', data, p)[0]
        types.append((cid, tree))
    nobj = struct.unpack_from('<i', data, p)[0]
    p += 4
    out = []
    for _ in range(nobj):
        p = (p + 3) & ~3
        pid, start, size, tidx = struct.unpack_from('<qQIi', data, p)
        p += 24
        cid, tree = types[tidx]
        out.append((cid, _read(tree, data, data_offset + start)[0]))
    return out


def mesh_data(m):
    vd = m['m_VertexData']
    count = vd['m_VertexCount']
    blob = vd['m_DataSize']
    streams = {}
    for ci, c in enumerate(vd['m_Channels']):
        if c['dimension'] & 0xF:
            streams.setdefault(c['stream'], []).append((ci, c['offset'], c['format'], c['dimension'] & 0xF))
    size = {0: 4, 1: 2, 2: 1, 3: 1, 4: 2, 5: 2, 6: 1, 7: 1, 8: 2, 9: 2, 10: 4, 11: 4}
    base, out = 0, {}
    for s in sorted(streams):
        stride = max(off + size[fmt] * dim for _, off, fmt, dim in streams[s])
        for ci, off, fmt, dim in streams[s]:
            code = {0: 'f', 10: 'I', 11: 'i'}.get(fmt)
            if code:
                out[ci] = np.array([struct.unpack_from('<%d%s' % (dim, code), blob, base + v * stride + off) for v in range(count)])
        base = (base + stride * count + 15) & ~15
    idx = np.frombuffer(m['m_IndexBuffer'], dtype='<u2' if m['m_IndexFormat'] == 0 else '<u4').astype(int)
    return out, idx


def find_artifacts(must_contain):
    hits = []
    for base, _, files in os.walk(path('Library/Artifacts')):
        for f in files:
            full = os.path.join(base, f)
            if os.path.getsize(full) > 4_000_000:
                continue
            data = open(full, 'rb').read()
            if all(m in data for m in must_contain):
                hits.append(full)
    return hits


# ---------------------------------------------------------------------------------------------
# A small binary fbx reader, only to tell which imported mesh is which fbx (by its normals)
# ---------------------------------------------------------------------------------------------

def fbx_first_normal(rel):
    data = open(path(rel), 'rb').read()
    wide = struct.unpack_from('<I', data, 23)[0] >= 7500

    def node(p):
        end, nprops, plen = struct.unpack_from('<QQQ' if wide else '<III', data, p)
        p += 24 if wide else 12
        name = data[p + 1:p + 1 + data[p]].decode()
        p += 1 + data[p]
        return end, nprops, plen, name, p

    def walk(p, stop):
        while p < stop:
            end, nprops, plen, name, q = node(p)
            if end == 0:
                return None
            if name == 'Normals':
                t = chr(data[q])
                n, enc, clen = struct.unpack_from('<III', data, q + 1)
                raw = data[q + 13:q + 13 + clen]
                raw = zlib.decompress(raw) if enc else raw
                return np.frombuffer(raw, dtype='<f8' if t == 'd' else '<f4', count=3)
            found = walk(q + plen, end)
            if found is not None:
                return found
            p = end
        return None

    return walk(27, len(data))


# ---------------------------------------------------------------------------------------------
# YAML (prefabs, scene, .anim) and transforms
# ---------------------------------------------------------------------------------------------

def unity_yaml(rel):
    txt = open(path(rel), encoding='utf-8').read()
    docs = {}
    for m in re.finditer(r'--- !u!(\d+) &(-?\d+)( stripped)?\n(.*?)(?=\n--- !u!|\Z)', txt, re.S):
        body = yaml.safe_load(m.group(4)) if not m.group(3) else None
        docs[m.group(2)] = (int(m.group(1)), list(body.values())[0] if body else {})
    return docs


def quat_m(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def trs(p, q, s):
    m = np.eye(4)
    m[:3, :3] = quat_m(q) * np.array(s, float)[None, :]
    m[:3, 3] = p
    return m


def v3(d):
    return [d['x'], d['y'], d['z']]


def q4(d):
    return [d['x'], d['y'], d['z'], d['w']]


def instance_trs(instance, source_root):
    """Local TRS of a nested prefab instance: the source root's values with the instance's overrides."""
    p, q, s = v3(source_root['m_LocalPosition']), q4(source_root['m_LocalRotation']), v3(source_root['m_LocalScale'])
    vals = {'m_LocalPosition': p, 'm_LocalRotation': q, 'm_LocalScale': s}
    for mod in instance['m_Modification']['m_Modifications']:
        prop = mod['propertyPath']
        for key, arr in vals.items():
            for i, axis in enumerate('xyzw'[:len(arr)]):
                if prop == f'{key}.{axis}':
                    arr[i] = float(mod['value'])
    return trs(p, q, s)


def scene_transform(docs, transform_id):
    m = np.eye(4)
    while transform_id and transform_id != '0':
        t = docs[transform_id][1]
        m = trs(v3(t['m_LocalPosition']), q4(t['m_LocalRotation']), v3(t['m_LocalScale'])) @ m
        transform_id = str(t['m_Father']['fileID'])
    return m


class Rig:
    """An unpacked fbx hierarchy prefab (Hoja.prefab): transforms, animation paths, the skinned renderer."""

    def __init__(self, rel):
        docs = unity_yaml(rel)
        self.tf = {k: v for k, (t, v) in docs.items() if t == 4}
        names = {k: v['m_Name'] for k, (t, v) in docs.items() if t == 1}
        self.name = lambda tid: names[str(self.tf[tid]['m_GameObject']['fileID'])]
        self.root_id = [k for k, v in self.tf.items() if v['m_Father']['fileID'] == 0][0]
        self.root = self.tf[self.root_id]
        smr = [v for k, (t, v) in docs.items() if t == 137][0]
        self.bones = [str(b['fileID']) for b in smr['m_Bones']]
        self.mesh_guid = smr['m_Mesh']['guid']
        self.paths = {}

        def walk(tid, prefix):
            self.paths[tid] = prefix
            for c in self.tf[tid]['m_Children']:
                cid = str(c['fileID'])
                walk(cid, (prefix + '/' if prefix else '') + self.name(cid))

        walk(self.root_id, '')

    def bone_matrices(self, root_matrix, pose):
        out = {}

        def rec(tid, parent):
            t = self.tf[tid]
            o = pose.get(self.paths[tid], {})
            local = trs(o.get('pos', v3(t['m_LocalPosition'])), o.get('rot', q4(t['m_LocalRotation'])),
                        o.get('scale', v3(t['m_LocalScale'])))
            m = root_matrix if tid == self.root_id else parent @ local
            out[tid] = m
            for c in t['m_Children']:
                rec(str(c['fileID']), m)

        rec(self.root_id, None)
        return out


def clip_pose(clip, which):
    """The first or last key of each rotation/position/scale curve: path -> overrides."""
    pose = {}
    for key, field, conv in (('m_RotationCurves', 'rot', q4), ('m_PositionCurves', 'pos', v3), ('m_ScaleCurves', 'scale', v3)):
        for c in clip.get(key) or []:
            keys = c['curve']['m_Curve']
            pose.setdefault(c['path'], {})[field] = conv(keys[0 if which == 'first' else -1]['value'])
    return pose


def state_clips(controller_rel):
    """(turning clip, idle clip) of a sheet controller: its default state's motion and the one it moves to."""
    docs = unity_yaml(controller_rel)
    sm = [v for k, (t, v) in docs.items() if t == 1107][0]
    turn = docs[str(sm['m_DefaultState']['fileID'])][1]
    idle = docs[str(docs[str(turn['m_Transitions'][0]['fileID'])][1]['m_DstState']['fileID'])][1]
    return turn['m_Motion'], idle['m_Motion']


# ---------------------------------------------------------------------------------------------

def main():
    scene = unity_yaml(SCENE)
    meshes = {}  # fbx guid -> (positions, uvs, bone index, bind poses, triangles)

    def load_mesh(fbx_guid, extra):
        if fbx_guid in meshes:
            return meshes[fbx_guid]
        fbx = asset_of_guid(fbx_guid)
        want = fbx_first_normal(fbx)
        for art in find_artifacts([b'lambert1', b'PN000pageJoint30'] + extra):
            for cid, obj in artifact_objects(art):
                if cid == 43:
                    arr, idx = mesh_data(obj)
                    # Unity mirrors x on import: an fbx normal (x, y, z) becomes (-x, y, z)
                    if np.allclose(arr[1][0], [-want[0], want[1], want[2]], atol=1e-3):
                        sub = obj['m_SubMeshes'][0]
                        bind = [np.array([[b['e%d%d' % (r, c)] for c in range(4)] for r in range(4)]) for b in obj['m_BindPose']]
                        meshes[fbx_guid] = (arr[0], arr[4], arr[13][:, 0].astype(int), bind,
                                            idx[:sub['indexCount']].reshape(-1, 3), art)
                        return meshes[fbx_guid]
        raise RuntimeError(f'no import artifact found for {fbx}')

    clips = {}

    def fbx_clip(motion):
        key = (motion['guid'], motion['fileID'])
        if key not in clips:
            for art in find_artifacts([b'PN000pageJoint30', b'Take 001']):
                for cid, obj in artifact_objects(art):
                    if cid == 74:
                        clips.setdefault((motion['guid'], motion['fileID']), obj)
        return clips[key]

    def clip_of(motion):
        rel = asset_of_guid(motion['guid'])
        if rel.endswith('.anim'):
            return [v for k, (t, v) in unity_yaml(rel).items() if t == 74][0]
        return fbx_clip(motion)

    # --- the book
    book_guid = guid_of(BOOK_FBX)
    inst = [(k, v) for k, (t, v) in scene.items() if t == 1001 and v['m_SourcePrefab']['guid'] == book_guid][0][1]
    book_m = scene_transform(scene, str(inst['m_Modification']['m_TransformParent']['fileID']))
    art = find_artifacts([b'HojaB_PAR', b'HojaA_IMPAR'])
    book_mesh = None
    for a in sorted(art, key=os.path.getmtime, reverse=True):
        for cid, obj in artifact_objects(a):
            if cid == 43 and obj['m_Name'] == 'Diario':
                book_mesh = obj
                break
        if book_mesh:
            break
    # the instance's own TRS over the fbx root (identity in the fbx)
    root_go = {'m_LocalPosition': {'x': 0, 'y': 0, 'z': 0}, 'm_LocalRotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1},
               'm_LocalScale': {'x': 1, 'y': 1, 'z': 1}}
    book_m = book_m @ instance_trs(inst, root_go)
    arr, idx = mesh_data(book_mesh)
    P = (book_m @ np.hstack([arr[0], np.ones((len(arr[0]), 1))]).T).T[:, :3]
    UV = arr[4]
    out = {'book': {}, 'sheet': {}}
    for half, si in BOOK_HALVES.items():
        sm = book_mesh['m_SubMeshes'][si]
        first = sm['firstByte'] // (2 if book_mesh['m_IndexFormat'] == 0 else 4)
        tris = (idx[first:first + sm['indexCount']] + sm['baseVertex']).reshape(-1, 3)
        # the top page only: up-facing triangles that reach the far edge (the stack under it stops at v 0.9)
        top = [t for t in tris if UV[t][:, 1].max() > 0.95 and np.cross(P[t[1]] - P[t[0]], P[t[2]] - P[t[0]])[1] > 0]
        vs = np.unique(np.array(top))
        rect = [UV[vs, 0].min(), UV[vs, 1].min(), UV[vs, 0].max(), UV[vs, 1].max()]
        out['book'][half] = {
            'submesh': si, 'uv_rect': [round(float(x), 6) for x in rect],
            'world_x': [round(float(P[vs, 0].min()), 4), round(float(P[vs, 0].max()), 4)],
            'world_z': [round(float(P[vs, 2].min()), 4), round(float(P[vs, 2].max()), 4)],
            'tris': [[[round(float(P[i, 0]), 4), round(float(P[i, 2]), 4), round(float(UV[i, 0]), 6), round(float(UV[i, 1]), 6)] for i in t] for t in top]}
        print(f'[measure-book-pages] book {half}: uv {out["book"][half]["uv_rect"]}, x {out["book"][half]["world_x"]}, z {out["book"][half]["world_z"]}')

    # --- the sheets: the PageScrollerManager parents them (Instantiate(hojaMaster, transform))
    psm_guid = re.search(r'guid: (\w+)', open(path('Assets/Scripts/Managers/PageScrollerManager.cs.meta')).read()).group(1)
    psm = [v for k, (t, v) in scene.items() if t == 114 and v.get('m_Script', {}).get('guid') == psm_guid][0]
    psm_go = str(psm['m_GameObject']['fileID'])
    psm_tf = [k for k, (t, v) in scene.items() if t == 4 and str(v.get('m_GameObject', {}).get('fileID')) == psm_go][0]
    psm_m = scene_transform(scene, psm_tf)
    sheet_rect = None
    for direction, prefab in SHEETS.items():
        docs = unity_yaml(prefab)
        root_tf = [v for k, (t, v) in docs.items() if t == 4 and v and v['m_Father']['fileID'] == 0][0]
        master_m = psm_m @ trs(v3(root_tf['m_LocalPosition']), q4(root_tf['m_LocalRotation']), v3(root_tf['m_LocalScale']))
        for k, (t, inst) in docs.items():
            if t != 1001:
                continue
            src = asset_of_guid(inst['m_SourcePrefab']['guid'])
            if 'GlitterHoja' in src:
                continue
            rig = Rig(src)
            controller = None
            for mod in inst['m_Modification']['m_Modifications']:
                if mod['propertyPath'] == 'm_Controller':
                    controller = asset_of_guid(mod['objectReference']['guid'])
            if controller is None:
                src_docs = unity_yaml(src)
                anim = [v for kk, (tt, v) in src_docs.items() if tt == 95][0]
                controller = asset_of_guid(anim['m_Controller']['guid'])
            turn, idle = state_clips(controller)
            root_m = master_m @ instance_trs(inst, rig.root)
            P0, UV0, BI, bind, tris, _ = load_mesh(rig.mesh_guid, [])
            name = [m['value'] for m in inst['m_Modification']['m_Modifications'] if m['propertyPath'] == 'm_Name'][0]
            for moment, clip, which in (('start', clip_of(turn), 'first'), ('end', clip_of(idle), 'first')):
                bones = rig.bone_matrices(root_m, clip_pose(clip, which))
                W = np.array([(bones[rig.bones[BI[i]]] @ bind[BI[i]] @ np.append(P0[i], 1))[:3] for i in range(len(P0))])
                up = [np.cross(W[t[1]] - W[t[0]], W[t[2]] - W[t[0]])[1] > 0 for t in tris]
                if not all(up):
                    continue  # this mesh faces down at this moment: the other face is the one seen
                half = 'right' if W[:, 0].mean() > 0 else 'left'
                key = f'{direction}_{moment}'
                out['sheet'][key] = {
                    'face': name, 'half': half,
                    'tris': [[[round(float(UV0[i, 0]), 6), round(float(UV0[i, 1]), 6), round(float(W[i, 0]), 4), round(float(W[i, 2]), 4)] for i in t] for t in tris]}
                sheet_rect = [float(UV0[:, 0].min()), float(UV0[:, 1].min()), float(UV0[:, 0].max()), float(UV0[:, 1].max())]
                print(f'[measure-book-pages] sheet {key}: face {name} lies on the {half} half, '
                      f'x [{W[:, 0].min():.3f}, {W[:, 0].max():.3f}] z [{W[:, 2].min():.3f}, {W[:, 2].max():.3f}]')
    out['sheet_uv_rect'] = [round(x, 6) for x in sheet_rect]
    missing = {'forward_start', 'forward_end', 'backward_start', 'backward_end'} - set(out['sheet'])
    if missing:
        raise RuntimeError(f'sheet faces not found: {missing}')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump(out, open(OUT, 'w'), indent=1)
    print(f'[measure-book-pages] wrote {OUT}')


if __name__ == '__main__':
    main()
