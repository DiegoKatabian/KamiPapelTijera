"""The intro books (spec 025 D9, task 2.A): for each level a copy of its open book that can close, built from the
level book's own imported mesh in world space (its non-uniform scale baked into the vertices), split at the spine:

  IntroBook_LevelN (root, world origin)
    Fixed          the right half (back cover + right page), the level book's materials
    Hinge          on the spine, at the page surface's height; closed = rotated -180 about z
      Moving       the left half (front cover + left page), plus a generated front-cover face (own submesh, the
                   title) and a spine face

Writes Assets/Generated/IntroBooks/IntroBook_Level1|2.kgen (+ Cover_LevelN.mat), and tools/data/intro-books.json
(hinge, the cover edge the hand grips, the closed and open extents) for tools/make-book-intro-clips.py.
The open pose is checked against the level book: every original vertex must be present (V3).

    python tools/make-intro-books.py
"""
import importlib.util
import json
import os
import re
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402

_spec = importlib.util.spec_from_file_location('mpb', os.path.join(ROOT, 'tools', 'measure-paper-book.py'))
mpb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mpb)
mbp = mpb.mbp

OUT_DIR = 'Assets/Generated/IntroBooks'
LEVELS = {'Level1': 'level1', 'Level2': 'level2'}


def scene_materials(cfg):
    """Material asset path per slot of the level book's renderer (scene overrides on its fbx instance)."""
    mats = {}
    G = {}
    for base, _, files in os.walk(ua.path('Assets')):
        for f in files:
            if f.endswith('.mat.meta'):
                g = re.search(r'guid: (\w+)', open(os.path.join(base, f), encoding='utf-8', errors='ignore').read(300))
                if g:
                    G[g.group(1)] = os.path.relpath(os.path.join(base, f[:-5]), ROOT).replace(os.sep, '/')
    fbx = mbp.guid_of(cfg['fbx'])
    s = open(ua.path(cfg['scene']), encoding='utf-8').read()
    for m in re.finditer(r'--- !u!1001 &(\d+)\nPrefabInstance:(?:(?!--- !u!).)*?m_SourcePrefab: \{fileID: 100100000, guid: ' + fbx, s, re.S):
        for mm in re.finditer(r'propertyPath: m_Materials\.Array\.data\[(\d+)\]\n      value: *\n      objectReference: \{fileID: (-?\d+), guid: (\w+)', m.group(0)):
            mats[int(mm.group(1))] = G.get(mm.group(3))
    return mats


def clip_triangle(P, N, U, tri, s, keep_left):
    """Sutherland-Hodgman of one triangle against x = s; returns polygons (lists of (p, n, uv)) on the kept side."""
    pts = [(P[i], N[i], U[i]) for i in tri]

    def inside(p):
        return p[0] <= s + 1e-6 if keep_left else p[0] >= s - 1e-6

    out = []
    for k in range(3):
        a, b = pts[k], pts[(k + 1) % 3]
        ia, ib = inside(a[0]), inside(b[0])
        if ia:
            out.append(a)
        if ia != ib:
            t = (s - a[0][0]) / (b[0][0] - a[0][0])
            out.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]), a[2] + t * (b[2] - a[2])))
    return out


class MeshBuilder:
    def __init__(self, name, submeshes):
        self.name = name
        self.V, self.N, self.UV = [], [], []
        self.sub = [[] for _ in range(submeshes)]

    def poly(self, sub, pts, origin):
        if len(pts) < 3:
            return
        base = len(self.V)
        for p, n, uv in pts:
            self.V.append(p - origin)
            nn = n / max(np.linalg.norm(n), 1e-9)
            self.N.append(nn)
            self.UV.append(uv)
        for k in range(1, len(pts) - 1):
            self.sub[sub] += [base, base + k, base + k + 1]

    def quad(self, sub, corners, normal, uvs, origin):
        """corners in order; flips the winding if needed so cross(b-a, c-a) follows `normal`."""
        a, b, c, d = [np.array(x, float) for x in corners]
        if np.dot(np.cross(b - a, c - a), normal) < 0:
            a, b, c, d = a, d, c, b
            uvs = [uvs[0], uvs[3], uvs[2], uvs[1]]
        n = np.array(normal, float)
        self.poly(sub, [(a, n, np.array(uvs[0])), (b, n, np.array(uvs[1])), (c, n, np.array(uvs[2])), (d, n, np.array(uvs[3]))], origin)

    def to_json(self):
        idx = []
        starts = []
        for s in self.sub:
            starts.append(len(idx))
            idx += s
        return {'name': self.name, 'vertices': np.array(self.V).reshape(-1).round(5).tolist(),
                'normals': np.array(self.N).reshape(-1).round(5).tolist(),
                'uv': np.array(self.UV).reshape(-1).round(6).tolist(), 'subMeshStarts': starts, 'indices': idx}


def build(level, key):
    cfg = mpb.LEVELS[key]
    data = json.load(open(os.path.join(ROOT, 'tools', 'data', f'paper-book-{key}.json')))
    mesh = mpb.book_mesh(cfg)
    arr, idx = mbp.mesh_data(mesh)
    M = mpb.book_matrix(cfg)
    P = (M @ np.hstack([arr[0], np.ones((len(arr[0]), 1))]).T).T[:, :3]
    Nrm = (np.linalg.inv(M[:3, :3]).T @ arr[1].T).T
    U = arr[4]
    s = data['spine_x']
    top = max(max(p[1] for p in data['profiles']['left']), max(p[1] for p in data['profiles']['right']))
    hinge = np.array([s, top, 0.0])
    mats = scene_materials(cfg)
    slots = len(mesh['m_SubMeshes'])
    x_outer = float(P[:, 0].min())
    y_bottom = float(P[:, 1].min())
    z0, z1 = float(P[:, 2].min()), float(P[:, 2].max())

    fixed = MeshBuilder(f'IntroBook_{level}_Fixed', slots)
    moving = MeshBuilder(f'IntroBook_{level}_Moving', slots + 1)  # last submesh = the front cover face
    for si, sm in enumerate(mesh['m_SubMeshes']):
        first = sm['firstByte'] // (2 if mesh['m_IndexFormat'] == 0 else 4)
        tris = (idx[first:first + sm['indexCount']] + sm['baseVertex']).reshape(-1, 3)
        for t in tris:
            fn = np.cross(P[t[1]] - P[t[0]], P[t[2]] - P[t[0]])
            # the left half's underside (any face turned down near the bottom) becomes the generated front cover
            down = fn[1] < -0.5 * np.linalg.norm(fn) and P[t, 1].max() < y_bottom + 0.35 * (top - y_bottom)
            # faces lying in the bottom plane (inner panels facing up) would z-fight the generated cover once closed
            down = down or P[t, 1].max() < y_bottom + 0.05
            xs = P[t, 0]
            # the left half's underside becomes the generated front cover (its own submesh), so drop the original
            if down and xs.max() <= s + 1e-6:
                continue
            if xs.max() <= s + 1e-6:
                moving.poly(si, [(P[i], Nrm[i], U[i]) for i in t], hinge)
            elif xs.min() >= s - 1e-6:
                fixed.poly(si, [(P[i], Nrm[i], U[i]) for i in t], np.zeros(3))
            else:
                left = clip_triangle(P, Nrm, U, t, s, True)
                right = clip_triangle(P, Nrm, U, t, s, False)
                if not down:
                    moving.poly(si, left, hinge)
                fixed.poly(si, right, np.zeros(3))
    # front cover face: the left half's underside, uv 0..1 reading correctly once the book is closed (spine on the left)
    cover = slots
    moving.quad(cover, [(x_outer, y_bottom, z0), (s, y_bottom, z0), (s, y_bottom, z1), (x_outer, y_bottom, z1)],
                (0, -1, 0), [(1, 0), (0, 0), (0, 1), (1, 1)], hinge)
    # spine faces closing the cut, cover material
    moving.quad(0, [(s, y_bottom, z0), (s, top, z0), (s, top, z1), (s, y_bottom, z1)], (1, 0, 0),
                [(0.75, 0), (0.75, 0.02), (0.77, 0.02), (0.77, 0)], hinge)
    fixed.quad(0, [(s, y_bottom, z0), (s, top, z0), (s, top, z1), (s, y_bottom, z1)], (-1, 0, 0),
               [(0.75, 0), (0.75, 0.02), (0.77, 0.02), (0.77, 0)], np.zeros(3))

    # V3: every original vertex that stays (not the dropped cover underside) is in the open pose
    built = np.vstack([np.array(fixed.V), np.array(moving.V) + hinge])
    worst = 0.0
    for p in P:
        if p[0] <= s and p[1] < y_bottom + 0.35 * (top - y_bottom):
            continue  # the dropped underside
        worst = max(worst, float(np.min(np.linalg.norm(built - p, axis=1))))

    cover_tex = ua.guid_of(f'{OUT_DIR}/Cover_{level}.png')
    cover_mat = f'{OUT_DIR}/Cover_{level}.mat'
    ua.write(cover_mat, ua.urp_lit(f'Cover_{level}', cover_tex, (1, 1, 1, 1), smoothness=0.15))
    ua.ensure_meta(cover_mat, ua.NATIVE, main=2100000)
    slot_mats = [mats.get(i) for i in range(slots)]
    nodes = [
        {'name': f'IntroBook_{level}', 'parent': -1, 'pos': [0, 0, 0], 'rot': [0, 0, 0, 1], 'scale': [1, 1, 1], 'mesh': -1, 'materials': []},
        {'name': 'Fixed', 'parent': 0, 'pos': [0, 0, 0], 'rot': [0, 0, 0, 1], 'scale': [1, 1, 1], 'mesh': 0,
         'materials': slot_mats, 'castShadows': key != 'level1'},
        {'name': 'Hinge', 'parent': 0, 'pos': hinge.round(5).tolist(), 'rot': [0, 0, 0, 1], 'scale': [1, 1, 1], 'mesh': -1, 'materials': []},
        {'name': 'Moving', 'parent': 2, 'pos': [0, 0, 0], 'rot': [0, 0, 0, 1], 'scale': [1, 1, 1], 'mesh': 1,
         'materials': slot_mats + [cover_mat], 'castShadows': key != 'level1'},
    ]
    gen = {'name': f'IntroBook_{level}', 'skinned': False, 'sharedAtlas': False,
           'openMeshes': [fixed.name, moving.name], 'nodes': nodes,
           'meshes': [fixed.to_json(), moving.to_json()], 'clips': []}
    rel = f'{OUT_DIR}/IntroBook_{level}.kgen'
    ua.write(rel, json.dumps(gen, separators=(',', ':')))
    ua.ensure_meta(rel, ua.SCRIPTED, importer=ua.guid_of('Assets/Editor/Generated/KamiGenImporter.cs'))
    print(f'[make-intro-books] {rel}: fixed {len(fixed.V)} / moving {len(moving.V)} vertices, materials {slot_mats}; '
          f'open pose vs level book: worst vertex {worst:.4f} units')
    return {'hinge': hinge.tolist(), 'spine_x': s, 'top': top, 'bottom': y_bottom, 'x_outer': x_outer,
            'x_outer_right': float(P[:, 0].max()), 'z0': z0, 'z1': z1, 'v3_worst': worst,
            'book_slots': slot_mats, 'cover_material': cover_mat}


def main():
    out = {}
    for level, key in LEVELS.items():
        out[level] = build(level, key)
    json.dump(out, open(os.path.join(ROOT, 'tools', 'data', 'intro-books.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
