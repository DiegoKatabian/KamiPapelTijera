"""Fits the procedural page curl (tools/paper_core.py, spec 025 D23) to the old page-turn clip (task 0.C).

The old sheet (HojaMaster, clip "Take 001") is a 31-joint chain; in its own rig space it is inextensible, so its shape
over time is fully described by the angle of each of its 30 segments. This samples those angles over the clip (Unity's
Hermite curves), mirrors them so the start side is +x, and fits CurlParams (lead, ease) on flat profiles by least
squares. Writes tools/data/paper-curl.json and a side-view contact sheet
specs/025-paper-book/previews/curl-fit.png (old in grey, new in red, per level with the real book profiles).

    python tools/fit-paper-curl.py
"""
import importlib.util
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import paper_core as pc  # noqa: E402

_spec = importlib.util.spec_from_file_location('mbp', os.path.join(ROOT, 'tools', 'measure-book-pages.py'))
mbp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mbp)

PREVIEWS = os.path.join(ROOT, 'specs', '025-paper-book', 'previews')


def hermite(keys, t):
    """Unity's curve evaluation for one float channel: keys = [(time, value, inSlope, outSlope)]."""
    if t <= keys[0][0]:
        return keys[0][1]
    if t >= keys[-1][0]:
        return keys[-1][1]
    for (t0, v0, _, o0), (t1, v1, i1, _) in zip(keys[:-1], keys[1:]):
        if t0 <= t <= t1:
            dt = t1 - t0
            if not math.isfinite(o0) or not math.isfinite(i1):
                return v0
            s = (t - t0) / dt
            h00 = 2 * s ** 3 - 3 * s ** 2 + 1
            h10 = s ** 3 - 2 * s ** 2 + s
            h01 = -2 * s ** 3 + 3 * s ** 2
            h11 = s ** 3 - s ** 2
            return h00 * v0 + h10 * dt * o0 + h01 * v1 + h11 * dt * i1
    return keys[-1][1]


def load_clip():
    prefab = mbp.SHEETS['forward']
    docs = mbp.unity_yaml(prefab)
    inst = [v for k, (t, v) in docs.items() if t == 1001 and 'GlitterHoja' not in mbp.asset_of_guid(v['m_SourcePrefab']['guid'])][0]
    rig = mbp.Rig(mbp.asset_of_guid(inst['m_SourcePrefab']['guid']))
    clip = None
    for art in mbp.find_artifacts([b'PN000pageJoint30', b'Take 001']):
        for cid, obj in mbp.artifact_objects(art):
            if cid == 74 and obj.get('m_Name') == 'Take 001':
                clip = obj
    if clip is None:
        raise RuntimeError('Take 001 not found in Library/Artifacts')
    curves = {}
    for c in clip['m_RotationCurves']:
        ks = c['curve']['m_Curve']
        curves[c['path']] = [[(k['time'], k['value'][a], k['inSlope'][a], k['outSlope'][a]) for k in ks] for a in 'xyzw']
    pcurves = {}
    for c in clip.get('m_PositionCurves') or []:
        ks = c['curve']['m_Curve']
        pcurves[c['path']] = [[(k['time'], k['value'][a], k['inSlope'][a], k['outSlope'][a]) for k in ks] for a in 'xyz']
    length = max(k['time'] for c in clip['m_RotationCurves'] for k in c['curve']['m_Curve'])
    joints = sorted([tid for tid in rig.tf if rig.name(tid).startswith('PN000pageJoint')], key=lambda t: rig.name(t))
    return rig, curves, pcurves, length, joints


def chain_angles(rig, curves, pcurves, joints, t):
    pose = {}
    for path, ch in curves.items():
        q = np.array([hermite(c, t) for c in ch])
        q /= np.linalg.norm(q)
        pose.setdefault(path, {})['rot'] = list(q)
    for path, ch in pcurves.items():
        pose.setdefault(path, {})['pos'] = [hermite(c, t) for c in ch]
    m = rig.bone_matrices(np.eye(4), pose)
    pts = np.array([m[j][:3, 3] for j in joints])[:, :2]
    pts[:, 0] *= -1  # the rig's start side is -x
    d = np.diff(pts, axis=0)
    return np.unwrap(np.arctan2(d[:, 1], d[:, 0])), np.hypot(d[:, 0], d[:, 1])


def model_angles(lead, ease, t, n, shape=1.0):
    p = pc.CurlParams(lead, ease)
    e = p.eased(t)
    u = (np.arange(n) + 0.5) / n
    return np.array([pc.smoothstep(e * (1 + lead) - lead * ui ** shape) for ui in u]) * math.pi


def main():
    rig, curves, pcurves, length, joints = load_clip()
    ts = np.linspace(0, 1, 41)
    old = []
    for f in ts:
        a, seg = chain_angles(rig, curves, pcurves, joints, f * length)
        old.append(a)
    old = np.array(old)
    # The old sheet floated above the book: its first segments stand vertical over the gutter and its tip droops
    # below horizontal, neither of which a sheet lying on the paper can do. Fit only the shape between them.
    old = np.clip(old, 0.0, math.pi)
    FIT_FROM = 2
    seg_spread = float(seg.std() / seg.mean())
    best = None
    for shape in (0.5, 0.7, 1.0, 1.4, 1.8, 2.2, 2.8, 3.5):
        for lead in np.linspace(0.0, 3.0, 61):
            for ease in np.linspace(0.0, 1.0, 11):
                err = 0.0
                for f, a in zip(ts, old):
                    err += float(((model_angles(lead, ease, f, len(a), shape) - a)[FIT_FROM:] ** 2).mean())
                err = math.sqrt(err / len(ts))
                if best is None or err < best[0]:
                    best = (err, lead, ease, shape)
    err, lead, ease, shape = best
    print(f'[fit-paper-curl] clip {length:.3f} s, {len(joints)} joints (segment spread {seg_spread:.4f}); '
          f'best lead {lead:.3f}, ease {ease:.2f}, shape {shape:.2f}, rms angle error {math.degrees(err):.1f} deg')
    out = {'duration': round(float(length), 4), 'lead': round(float(lead), 4), 'ease': round(float(ease), 3),
           'shape': round(float(shape), 3), 'rms_error_deg': round(math.degrees(err), 2)}
    json.dump(out, open(os.path.join(ROOT, 'tools', 'data', 'paper-curl.json'), 'w'), indent=1)

    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    os.makedirs(PREVIEWS, exist_ok=True)
    fig, axes = plt.subplots(3, 1, figsize=(12, 13))
    # old vs new on flat profiles, normalised length 1
    ax = axes[0]
    for f, a in zip(ts[::5], old[::5]):
        d = 1.0 / len(a)
        po = np.vstack([[0, 0], np.cumsum(np.c_[np.cos(a), np.sin(a)] * d, 0)])
        mn = model_angles(lead, ease, f, 64, shape)
        pn = np.vstack([[0, 0], np.cumsum(np.c_[np.cos(mn), np.sin(mn)] / 64, 0)])
        ax.plot(po[:, 0], po[:, 1], color='0.6')
        ax.plot(pn[:, 0], pn[:, 1], color='r', lw=1)
    ax.set_aspect('equal')
    ax.set_title(f'Old clip (grey) vs procedural curl (red), every 0.125. lead {lead:.2f}, ease {ease:.2f}, shape {shape:.2f}, '
                 f'rms {math.degrees(err):.1f} deg')
    for ax, level in zip(axes[1:], ('level1', 'level2')):
        data = json.load(open(os.path.join(ROOT, 'tools', 'data', f'paper-book-{level}.json')))
        S = pc.Profile(data['profiles']['right'])
        E = pc.Profile(data['profiles']['left'])
        curl = pc.Curl(S, E, pc.CurlParams(lead, ease, length, shape))
        for f in np.linspace(0, 1, 9):
            pts, _ = curl.points(f)
            ax.plot(pts[:, 0], pts[:, 1], color='r' if 0 < f < 1 else 'b', lw=1)
        ax.plot(S.xo, S.y, color='k', lw=2)
        ax.plot(-E.xo, E.y, color='k', lw=2)
        ax.set_aspect('equal')
        ax.set_title(f'{level}: the sheet on the real book profile (black), every 1/8 of the turn')
    fig.tight_layout()
    fig.savefig(os.path.join(PREVIEWS, 'curl-fit.png'), dpi=80)
    print(f'[fit-paper-curl] wrote tools/data/paper-curl.json and previews/curl-fit.png')


if __name__ == '__main__':
    main()
