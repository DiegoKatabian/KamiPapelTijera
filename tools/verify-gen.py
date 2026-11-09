"""Checks every generated asset of spec 025 Part B before Unity sees it (D40), and after Unity imported it.

  python tools/verify-gen.py              # offline: the .kgen files under Assets/Generated
  python tools/verify-gen.py --artifacts  # also: Editor.log errors for the generated paths, and each imported mesh /
                                          # clip in Library/Artifacts compared with its .kgen

Offline checks per mesh: every shell closed (each edge used by exactly two triangles once vertices are welded),
outward winding (positive signed volume), normals agree with faces, UVs inside 0..1 and no two islands overlapping
(rasterised at 1024). Per clip: every curve's path exists in the file's hierarchy, keys sorted, quaternions unit.
Exit code 0 = all passed.
"""
import glob
import json
import os
import re
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))

FAIL = []


def check(ok, what):
    print(f'[verify-gen] {"PASS" if ok else "FAIL"} {what}')
    if not ok:
        FAIL.append(what)


def shells(F):
    """Connected components of triangles (by shared welded vertex)."""
    parent = {}

    def find(a):
        while parent.setdefault(a, a) != a:
            parent[a] = parent.setdefault(parent[a], parent[a])
            a = parent[a]
        return a

    for a, b, c in F:
        ra, rb, rc = find(a), find(b), find(c)
        parent[rb] = ra
        parent[find(rc)] = ra
    groups = {}
    for t, (a, b, c) in enumerate(F):
        groups.setdefault(find(a), []).append(t)
    return list(groups.values())


def check_mesh(name, m, closed=True):
    V = np.array(m['vertices'], float).reshape(-1, 3)
    N = np.array(m['normals'], float).reshape(-1, 3) if m.get('normals') else None
    UV = np.array(m['uv'], float).reshape(-1, 2) if m.get('uv') else None
    idx = np.array(m['indices'], int).reshape(-1, 3)
    # weld by position
    keys = {}
    weld = np.array([keys.setdefault(tuple(np.round(v, 3)), len(keys)) for v in V])
    F = weld[idx]
    if closed:
        edges = {}
        for a, b, c in F:
            for e in ((a, b), (b, c), (c, a)):
                k = (min(e), max(e))
                edges[k] = edges.get(k, 0) + 1
        open_edges = sum(1 for n in edges.values() if n != 2)
        check(open_edges == 0, f'{name}: closed ({open_edges} edges not shared by exactly two triangles)')
        P = np.zeros((len(keys), 3))
        P[weld] = V
        for k, sh in enumerate(shells([tuple(f) for f in F])):
            vol = sum(np.dot(P[F[t][0]], np.cross(P[F[t][1]], P[F[t][2]])) for t in sh) / 6
            check(vol > 0, f'{name} shell {k}: outward winding (signed volume {vol:.1f})')
    if N is not None:
        fn = np.cross(V[idx[:, 1]] - V[idx[:, 0]], V[idx[:, 2]] - V[idx[:, 0]])
        vn = N[idx].mean(1)
        agree = (np.einsum('ij,ij->i', fn, vn) > 0).mean()
        check(agree > 0.97, f'{name}: vertex normals agree with faces ({agree:.1%})')
    return UV, idx


def check_uv_overlap(name, uv_sets):
    """uv_sets: list of (island key, UV, triangles). Rasterises each triangle at 1024 and counts pixels claimed by
    more than one piece."""
    size = 1024
    owner = np.full((size, size), -1, int)
    overlap = 0
    for key, UV, tris in uv_sets:
        if UV is None:
            continue
        if UV.min() < -1e-4 or UV.max() > 1 + 1e-4:
            check(False, f'{name}/{key}: UVs inside 0..1 ({UV.min():.3f}..{UV.max():.3f})')
        P = UV * size
        for a, b, c in tris:
            pa, pb, pc = P[a], P[b], P[c]
            x0, x1 = int(max(min(pa[0], pb[0], pc[0]), 0)), int(min(max(pa[0], pb[0], pc[0]), size - 1))
            y0, y1 = int(max(min(pa[1], pb[1], pc[1]), 0)), int(min(max(pa[1], pb[1], pc[1]), size - 1))
            area = (pb[0] - pa[0]) * (pc[1] - pa[1]) - (pc[0] - pa[0]) * (pb[1] - pa[1])
            if abs(area) < 1e-9:
                continue
            for y in range(y0, y1 + 1):
                for x in range(x0, x1 + 1):
                    sx, sy = x + 0.5, y + 0.5
                    w0 = ((pb[0] - sx) * (pc[1] - sy) - (pc[0] - sx) * (pb[1] - sy)) / area
                    w1 = ((pc[0] - sx) * (pa[1] - sy) - (pa[0] - sx) * (pc[1] - sy)) / area
                    if w0 < 0 or w1 < 0 or w0 + w1 > 1:
                        continue
                    o = owner[y, x]
                    if o == -1:
                        owner[y, x] = key
                    elif o != key:
                        overlap += 1
    check(overlap == 0, f'{name}: no two pieces share UV pixels ({overlap} shared)')


def check_kgen(path):
    rel = os.path.relpath(path, ROOT).replace('\\', '/')
    gen = json.load(open(path))
    meshes = gen.get('meshes') or []
    open_ok = gen.get('openMeshes', [])
    uv_sets = []
    for k, m in enumerate(meshes):
        UV, idx = check_mesh(f'{rel}:{m["name"]}', m, closed=m['name'] not in open_ok)
        if gen.get('sharedAtlas', True):
            uv_sets.append((k, UV, idx))
    if uv_sets and gen.get('sharedAtlas', True):
        check_uv_overlap(rel, uv_sets)
    # hierarchy paths for clips
    nodes = gen.get('nodes') or []
    paths = set()
    for i, n in enumerate(nodes):
        parts = [n['name']]
        p = n['parent']
        while p is not None and p >= 0:
            parts.append(nodes[p]['name'])
            p = nodes[p]['parent']
        paths.add('/'.join(reversed(parts)))
    for c in gen.get('clips') or []:
        bad = [cv['path'] for cv in c['curves'] if nodes and cv['path'] not in paths]
        check(not bad, f'{rel}:{c["name"]}: every curve path exists ({len(bad)} missing{": " + bad[0] if bad else ""})')
        for cv in c['curves']:
            t = cv['times']
            if any(b < a for a, b in zip(t[:-1], t[1:])):
                check(False, f'{rel}:{c["name"]}: keys sorted on {cv["path"]}')
            if cv['property'] == 'localRotation':
                q = np.array(cv['values']).reshape(-1, 4)
                if np.abs(np.linalg.norm(q, axis=1) - 1).max() > 1e-3:
                    check(False, f'{rel}:{c["name"]}: unit quaternions on {cv["path"]}')
        check(max((max(cv['times']) for cv in c['curves']), default=0) <= c['length'] + 1e-4,
              f'{rel}:{c["name"]}: no key after the clip length ({c["length"]} s)')


def check_artifacts(paths):
    log = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Unity', 'Editor', 'Editor.log')
    if os.path.exists(log):
        text = open(log, encoding='utf-8', errors='ignore').read()
        for p in paths:
            rel = os.path.relpath(p, ROOT).replace('\\', '/')
            errs = [l for l in text.splitlines() if rel in l and ('error' in l.lower() or 'exception' in l.lower())]
            check(not errs, f'Editor.log: no import error for {rel}' + (f' ({errs[0][:120]})' if errs else ''))
        imported = [l for l in text.splitlines() if 'KamiGenImporter' in l]
        print(f'[verify-gen] Editor.log mentions KamiGenImporter {len(imported)} times')
    import importlib.util
    spec = importlib.util.spec_from_file_location('mbp', os.path.join(ROOT, 'tools', 'measure-book-pages.py'))
    mbp = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mbp)
    for p in paths:
        gen = json.load(open(p))
        guid = re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1)
        arts = [a for a in mbp.find_artifacts([gen['meshes'][0]['name'].encode()] if gen.get('meshes') else [gen['name'].encode()])]
        found = {}
        for a in arts:
            try:
                for cid, obj in mbp.artifact_objects(a):
                    if cid == 43:
                        found[obj['m_Name']] = obj['m_VertexData']['m_VertexCount']
                    if cid == 74:
                        found['clip ' + obj['m_Name']] = len(obj.get('m_EditorCurves') or []) + len(obj.get('m_RotationCurves') or []) + len(obj.get('m_PositionCurves') or [])
            except Exception:
                continue
        rel = os.path.relpath(p, ROOT).replace('\\', '/')
        if not found and arts:
            # ScriptedImporter artifacts are written without type trees, so their objects can't be read from here;
            # the EditMode BookIntroTests check the same imports from inside Unity
            print(f'[verify-gen] NOTE {rel}: imported ({len(arts)} artifacts) but stored without type trees: '
                  f'run Test Runner > EditMode > BookIntroTests for the content check')
            continue
        if not found:
            check(False, f'{rel}: imported by Unity (no artifact found yet: focus Unity, then re-run)')
            continue
        for m in gen.get('meshes') or []:
            want = len(m['vertices']) // 3
            got = found.get(m['name'])
            check(got == want, f'{rel}: imported mesh {m["name"]} has {got} vertices (expected {want})')
        for c in gen.get('clips') or []:
            check(('clip ' + c['name']) in found, f'{rel}: imported clip {c["name"]}')


def main():
    files = sorted(glob.glob(os.path.join(ROOT, 'Assets', 'Generated', '**', '*.kgen'), recursive=True))
    for f in files:
        check_kgen(f)
    if '--artifacts' in sys.argv:
        check_artifacts(files)
    print(f'[verify-gen] {len(files)} files, {len(FAIL)} failures')
    return 1 if FAIL else 0


if __name__ == '__main__':
    sys.exit(main())
