"""Preview of the intro books closing (spec 025 task 2.C): each level's book closed, half open and open, from the
cameras' side (looking along +z, from above). Submeshes are coloured: cover rim grey, right page white, left page
cream, front cover red. Writes specs/025-paper-book/previews/intro-books.png.

    python tools/preview-intro-books.py
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import zbuffer as zb  # noqa: E402

COLORS = [(0.55, 0.55, 0.58), (0.95, 0.95, 0.92), (0.92, 0.88, 0.78), (0.75, 0.25, 0.2)]


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def meshes(gen, angle_deg):
    out = []
    hinge = np.array(gen['nodes'][2]['pos'])
    for k, m in enumerate(gen['meshes']):
        V = np.array(m['vertices']).reshape(-1, 3)
        if k == 1:
            V = (rot_z(math.radians(angle_deg)) @ V.T).T + hinge
        idx = m['indices']
        starts = m['subMeshStarts'] + [len(idx)]
        for s in range(len(m['subMeshStarts'])):
            F = np.array(idx[starts[s]:starts[s + 1]]).reshape(-1, 3)
            out.append({'V': V, 'F': F, 'color': COLORS[min(s, 3)]})
    return out


def main():
    rows = []
    for level in ('Level1', 'Level2'):
        gen = json.load(open(os.path.join(ROOT, 'Assets', 'Generated', 'IntroBooks', f'IntroBook_{level}.kgen')))
        allv = np.vstack([np.array(m['vertices']).reshape(-1, 3) for m in gen['meshes']])
        center = np.array([0.0, 0.0, allv[:, 2].mean()])
        size = allv[:, 0].max() - allv[:, 0].min()
        eye = center + np.array([0.15 * size, 0.9 * size, -0.9 * size])
        row = [zb.render(meshes(gen, a), zb.look_at(eye, center), size=(420, 300)) for a in (-180, -135, -90, -45, 0)]
        rows.append(np.hstack(row))
    out = os.path.join(ROOT, 'specs', '025-paper-book', 'previews', 'intro-books.png')
    Image.fromarray((np.vstack(rows) * 255).astype(np.uint8)).save(out)
    print(f'[preview-intro-books] {out}')


if __name__ == '__main__':
    main()
