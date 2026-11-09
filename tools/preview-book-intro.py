"""Contact sheet of the level intro (spec 025 V5): the intro book and the hand, every 0.75 s, evaluating the generated
clips (linear between keys), with Kami as a red block on the hand's Carry socket while carried. Writes
specs/025-paper-book/previews/book-intro-LevelN.png.

    python tools/preview-book-intro.py [Level2]
"""
import importlib.util
import json
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import zbuffer as zb  # noqa: E402

_spec = importlib.util.spec_from_file_location('hand', os.path.join(ROOT, 'tools', 'make-narrator-hand.py'))
hand = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hand)
_spec2 = importlib.util.spec_from_file_location('clips', os.path.join(ROOT, 'tools', 'make-book-intro-clips.py'))
clips = importlib.util.module_from_spec(_spec2)
_spec2.loader.exec_module(clips)


def sample(curve, t):
    times = curve['times']
    stride = 4 if curve['property'] == 'localRotation' else 3
    vals = np.array(curve['values']).reshape(-1, stride)
    if t <= times[0]:
        return vals[0]
    if t >= times[-1]:
        return vals[-1]
    i = int(np.searchsorted(times, t) - 1)
    u = (t - times[i]) / (times[i + 1] - times[i])
    v = vals[i] + (vals[i + 1] - vals[i]) * u
    return v / np.linalg.norm(v) if stride == 4 else v


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def main(level='Level2'):
    gen = json.load(open(os.path.join(ROOT, 'Assets', 'Generated', 'BookIntro', f'BookIntroClips_{level}.kgen')))
    hand_clip = [c for c in gen['clips'] if c['name'].startswith('BookIntro_Hand')][0]
    book_clip = [c for c in gen['clips'] if c['name'].startswith('BookIntro_Book')][0]
    book_gen = json.load(open(os.path.join(ROOT, 'Assets', 'Generated', 'IntroBooks', f'IntroBook_{level}.kgen')))
    joints, pieces, *_ = hand.build()
    by_path = {c['path'].split('/')[-1] + '|' + c['property']: c for c in hand_clip['curves']}
    hinge = np.array(book_gen['nodes'][2]['pos'])
    allv = np.vstack([np.array(m['vertices']).reshape(-1, 3) for m in book_gen['meshes']])
    center = np.array([0.0, 4.0, allv[:, 2].mean()])
    size = allv[:, 0].max() - allv[:, 0].min()
    eye = center + np.array([0.05 * size, 0.95 * size, -1.15 * size])
    view = zb.look_at(eye, center)
    frames = []
    for t in np.arange(0, 12.01, 0.75):
        out = []
        q = sample(book_clip['curves'][0], t)
        for k, m in enumerate(book_gen['meshes']):
            V = np.array(m['vertices']).reshape(-1, 3)
            if k == 1:
                V = (qmat(q) @ V.T).T + hinge
            out.append({'V': V, 'F': np.array(m['indices']).reshape(-1, 3), 'color': (0.85, 0.83, 0.78) if k == 0 else (0.8, 0.5, 0.45)})
        # hand: root from the clip, joints from their own curves
        mats = []
        for j in joints:
            pc = by_path.get(j['name'] + '|localPosition')
            rc = by_path.get(j['name'] + '|localRotation')
            m4 = np.eye(4)
            m4[:3, :3] = qmat(sample(rc, t)) if rc else j['rest']
            m4[:3, 3] = sample(pc, t) if pc else j['pos']
            mats.append(m4 if j['parent'] < 0 else mats[j['parent']] @ m4)
        for k, j in enumerate(joints):
            p = j['piece']
            if p is None:
                continue
            V = (mats[k] @ np.hstack([np.asarray(p.V), np.ones((len(p.V), 1))]).T).T[:, :3]
            out.append({'V': V, 'F': p.F, 'color': (0.15, 0.18, 0.32) if p.material == 'sleeve' else (0.82, 0.8, 0.76)})
        palm = [k for k, j in enumerate(joints) if j['name'] == 'Palm'][0]
        sock = (mats[palm] @ np.append(clips.GRIP, 1))[:3]
        if clips.BEATS['showKami'] <= t < clips.T_LET_GO:
            base = sock + np.array([0, -clips.HANG, 0])
        elif t >= clips.T_LET_GO:
            base = np.array(clips.KAMI[level])
        else:
            base = None
        if base is not None:
            b = np.array([[-1.5, 0, -0.5], [1.5, 0, -0.5], [1.5, 8, -0.5], [-1.5, 8, -0.5]]) + base
            out.append({'V': b, 'F': [(0, 2, 1), (0, 3, 2), (0, 1, 2), (0, 2, 3)], 'color': (0.9, 0.2, 0.2), 'cull': False})
        img = zb.render(out, view, size=(400, 260))
        frames.append(img)
    rows = [np.hstack(frames[i:i + 4]) for i in range(0, len(frames) - len(frames) % 4, 4)]
    path = os.path.join(ROOT, 'specs', '025-paper-book', 'previews', f'book-intro-{level}.png')
    Image.fromarray((np.vstack(rows) * 255).astype(np.uint8)).save(path)
    print(f'[preview-book-intro] {path}')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'Level2')
