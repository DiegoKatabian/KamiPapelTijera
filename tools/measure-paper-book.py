"""Measures each level's open book for the paper page turns (spec 025, tasks 0.B and 0.C).

Writes tools/data/paper-book-level1.json and tools/data/paper-book-level2.json:
  - spine_x: where the two page halves meet (world x; the spine runs along world z);
  - page_z: the pages' extent along the spine;
  - profiles: the top surface of each half as (outward distance from the spine, world y), outward = +x on the right
    half and -x on the left; this is the "rest" shape of the procedural sheet (D23);
  - uv_map: per half, the affine map (world x, world z, 1) -> mesh uv of its top triangles and the material slot,
    so the sheet's faces show exactly what the book half under them shows;
  - book: the renderer's scene path hint, world matrix and material slots.

Reads the book meshes from their import artifacts (Library/Artifacts) and the transforms from the scene YAML, through
tools/measure-book-pages.py's helpers. Re-run if a book, its transform or its scene changes.

    python tools/measure-paper-book.py
"""
import importlib.util
import json
import os

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location('mbp', os.path.join(ROOT, 'tools', 'measure-book-pages.py'))
mbp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mbp)

LEVELS = {
    'level1': {'scene': 'Assets/Scenes/Nivel1_KamiPapelTijera.unity', 'fbx': 'Assets/3D/LibroUVsfbx.fbx',
               'mesh': 'polySurface2', 'marker': [b'LibroUVsfbx', b'polySurface2'], 'halves': {'left': 1, 'right': 2}},
    'level2': {'scene': 'Assets/Scenes/Level2_Newspaper.unity', 'fbx': 'Assets/3D/Libros/Diario Kami/Meshes/Diario.fbx',
               'mesh': 'Diario', 'marker': [b'HojaA_IMPAR'], 'halves': {'right': 1, 'left': 2},
               # the half submeshes also hold the page stack under the top sheet, which stops at uv v 0.9
               'top_min_v': 0.95},
}
STEP = 0.5  # sampling step of the surface profile, world units


def book_mesh(cfg):
    for art in sorted(mbp.find_artifacts(cfg['marker']), key=os.path.getmtime, reverse=True):
        for cid, obj in mbp.artifact_objects(art):
            if cid == 43 and obj['m_Name'] == cfg['mesh']:
                return obj
    raise RuntimeError(f'no imported mesh {cfg["mesh"]} found')


def book_matrix(cfg):
    scene = mbp.unity_yaml(cfg['scene'])
    guid = mbp.guid_of(cfg['fbx'])
    insts = [v for k, (t, v) in scene.items() if t == 1001 and v['m_SourcePrefab']['guid'] == guid]
    if len(insts) != 1:
        raise RuntimeError(f'{cfg["scene"]}: expected one instance of {cfg["fbx"]}, found {len(insts)}')
    inst = insts[0]
    parent = mbp.scene_transform(scene, str(inst['m_Modification']['m_TransformParent']['fileID']))
    root = {'m_LocalPosition': {'x': 0, 'y': 0, 'z': 0}, 'm_LocalRotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1},
            'm_LocalScale': {'x': 1, 'y': 1, 'z': 1}}
    return parent @ mbp.instance_trs(inst, root)


def top_triangles(P, idx, mesh, slot, UV, min_v):
    sm = mesh['m_SubMeshes'][slot]
    first = sm['firstByte'] // (2 if mesh['m_IndexFormat'] == 0 else 4)
    tris = (idx[first:first + sm['indexCount']] + sm['baseVertex']).reshape(-1, 3)
    out = []
    for t in tris:
        n = np.cross(P[t[1]] - P[t[0]], P[t[2]] - P[t[0]])
        if n[1] > 0 and abs(n[1]) > 0.5 * np.linalg.norm(n) and (min_v is None or UV[t][:, 1].max() > min_v):
            out.append(t)
    return np.array(out)


def height_at(P, tris, x, z):
    """Highest top-surface y at (x, z), or None."""
    best = None
    for t in tris:
        a, b, c = P[t]
        # barycentric in the xz projection
        d = (b[2] - c[2]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[2] - c[2])
        if abs(d) < 1e-12:
            continue
        l1 = ((b[2] - c[2]) * (x - c[0]) + (c[0] - b[0]) * (z - c[2])) / d
        l2 = ((c[2] - a[2]) * (x - c[0]) + (a[0] - c[0]) * (z - c[2])) / d
        l3 = 1 - l1 - l2
        if min(l1, l2, l3) < -1e-6:
            continue
        y = l1 * a[1] + l2 * b[1] + l3 * c[1]
        best = y if best is None else max(best, y)
    return best


def measure(name, cfg):
    mesh = book_mesh(cfg)
    arr, idx = mbp.mesh_data(mesh)
    M = book_matrix(cfg)
    P = (M @ np.hstack([arr[0], np.ones((len(arr[0]), 1))]).T).T[:, :3]
    UV = arr[4]
    halves = {}
    for half, slot in cfg['halves'].items():
        tris = top_triangles(P, idx, mesh, slot, UV, cfg.get('top_min_v'))
        vs = np.unique(tris)
        halves[half] = {'slot': slot, 'tris': tris, 'x': (P[vs, 0].min(), P[vs, 0].max()),
                        'z': (P[vs, 2].min(), P[vs, 2].max())}
        # affine map (x, z, 1) -> uv, least squares over the top vertices
        A = np.hstack([P[vs][:, [0, 2]], np.ones((len(vs), 1))])
        coef, res, *_ = np.linalg.lstsq(A, UV[vs], rcond=None)
        err = np.abs(A @ coef - UV[vs]).max()
        halves[half]['uv'] = coef.T  # 2 x 3
        halves[half]['uv_err'] = float(err)
    spine_x = 0.5 * (halves['left']['x'][1] + halves['right']['x'][0])
    z0 = max(halves['left']['z'][0], halves['right']['z'][0])
    z1 = min(halves['left']['z'][1], halves['right']['z'][1])
    zs = np.linspace(z0 + 0.1 * (z1 - z0), z1 - 0.1 * (z1 - z0), 7)
    profiles = {}
    for half, sign in (('right', 1), ('left', -1)):
        h = halves[half]
        outer = (h['x'][1] - spine_x) if sign > 0 else (spine_x - h['x'][0])
        pts = []
        xo = 0.0
        while xo <= outer + 1e-6:
            x = spine_x + sign * min(xo, outer - 1e-4) + sign * (1e-4 if xo == 0 else 0)
            ys = [height_at(P, h['tris'], x, z) for z in zs]
            ys = [y for y in ys if y is not None]
            if ys:
                pts.append([round(xo, 4), round(float(np.mean(ys)), 5)])
            xo = round(xo + STEP, 6) if xo + STEP <= outer else (outer if xo < outer else outer + 1)
        profiles[half] = pts
    out = {
        'level': name, 'scene': cfg['scene'], 'fbx': cfg['fbx'],
        'spine_x': round(float(spine_x), 5),
        'page_z': [round(float(z0), 5), round(float(z1), 5)],
        'profiles': profiles,
        'uv_map': {half: {'slot': halves[half]['slot'], 'affine': np.round(halves[half]['uv'], 9).tolist(),
                          'max_error': round(halves[half]['uv_err'], 6)} for half in halves},
        'book_matrix': np.round(M, 6).tolist(),
    }
    path = os.path.join(ROOT, 'tools', 'data', f'paper-book-{name}.json')
    json.dump(out, open(path, 'w'), indent=1)
    for half in ('left', 'right'):
        pr = np.array(profiles[half])
        print(f'[measure-paper-book] {name} {half}: outer {pr[-1, 0]:.2f}, y {pr[:, 1].min():.3f}..{pr[:, 1].max():.3f}, '
              f'uv fit error {halves[half]["uv_err"]:.5f}')
    print(f'[measure-paper-book] {name}: spine x {spine_x:.3f}, page z {z0:.2f}..{z1:.2f} -> {path}')


if __name__ == '__main__':
    for name, cfg in LEVELS.items():
        measure(name, cfg)
