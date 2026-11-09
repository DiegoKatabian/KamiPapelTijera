"""The Narrator's hand (spec 025 Part B, D2-D8): a white cotton glove with a navy wool sleeve, built as a rigid-piece
puppet (one mesh per piece under its joint), UV-unwrapped into one 1024 atlas, with its base texture and materials.

Writes:
  Assets/Generated/NarratorHand/NarratorHand.kgen         hierarchy + meshes + the test-pose clip (KamiGenImporter)
  Assets/Generated/NarratorHand/NarratorHand_Atlas.png    base texture (Valentino can paint over the UV layout)
  Assets/Generated/NarratorHand/NarratorHand_UVLayout.png the islands, for painting
  Assets/Generated/NarratorHand/Glove.mat, Sleeve.mat     URP Lit, matte (no specular highlights: Level 2 blooms)
  tools/data/narrator-hand.json                            the rig (joints, rest poses, limits, sockets) for the clips
  specs/025-paper-book/previews/hand-*.png                 turntable, checker, UV layout, poses

Every size is in L = the hand's length from the wrist to the middle fingertip (D4: one hand for every level).

    python tools/make-narrator-hand.py
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402
import zbuffer as zb  # noqa: E402

OUT_DIR = 'Assets/Generated/NarratorHand'
PREVIEWS = os.path.join(ROOT, 'specs', '025-paper-book', 'previews')
L = 45.0           # wrist to middle fingertip, world units (D4)
ATLAS = 1024
PAD = 8            # px between islands

# colours (sRGB 0..255). The glove is a cream a little darker than paper white: Level 2's Film profile blooms above
# about 0.67 linear with +0.55 exposure (spec 025 review), so the glove's albedo stays near 0.5 linear.
GLOVE = (206, 199, 186)
STITCH = (165, 156, 142)
CUFF = (214, 208, 196)
SLEEVE = (38, 46, 80)

# ---------------------------------------------------------------------------------------------
# math
# ---------------------------------------------------------------------------------------------


def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def euler(deg):
    """Unity's Euler order: Z, then X, then Y (R = Ry Rx Rz)."""
    x, y, z = (math.radians(v) for v in deg)
    return rot_y(y) @ rot_x(x) @ rot_z(z)


def quat(m):
    """Rotation matrix -> (x, y, z, w), the same convention as UnityEngine.Quaternion."""
    t = np.trace(m)
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        w = 0.25 * s
        x = (m[2, 1] - m[1, 2]) / s
        y = (m[0, 2] - m[2, 0]) / s
        z = (m[1, 0] - m[0, 1]) / s
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        w = (m[2, 1] - m[1, 2]) / s
        x = 0.25 * s
        y = (m[0, 1] + m[1, 0]) / s
        z = (m[0, 2] + m[2, 0]) / s
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        w = (m[0, 2] - m[2, 0]) / s
        x = (m[0, 1] + m[1, 0]) / s
        y = 0.25 * s
        z = (m[1, 2] + m[2, 1]) / s
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        w = (m[1, 0] - m[0, 1]) / s
        x = (m[0, 2] + m[2, 0]) / s
        y = (m[1, 2] + m[2, 1]) / s
        z = 0.25 * s
    q = np.array([x, y, z, w])
    return q / np.linalg.norm(q)


# ---------------------------------------------------------------------------------------------
# geometry
# ---------------------------------------------------------------------------------------------


class Piece:
    """One mesh: triangle corners carry their own UV island coordinates (world units within the island)."""

    def __init__(self, name):
        self.name = name
        self.V = []   # positions (local to its joint)
        self.F = []   # triangles
        self.UVL = []  # per-vertex island-local uv (world units)
        self.island = []  # per-vertex island index (into Piece.islands)
        self.islands = []  # [(width, height)] in world units
        self.material = 'glove'

    def add(self, V, F, UVL, island_size):
        base = len(self.V)
        idx = len(self.islands)
        self.islands.append(island_size)
        self.V += [tuple(v) for v in V]
        self.UVL += [tuple(u) for u in UVL]
        self.island += [idx] * len(V)
        self.F += [tuple(int(i) + base for i in f) for f in F]


def drop_degenerate(V, F):
    V = np.asarray(V)
    out = []
    for f in F:
        a, b, c = V[f[0]], V[f[1]], V[f[2]]
        if np.linalg.norm(np.cross(b - a, c - a)) > 1e-9:
            out.append(f)
    return out


def orient_outward(V, F):
    """Flips every triangle if the shell's signed volume is negative (cross(b-a, c-a) must point out)."""
    V = np.asarray(V)
    vol = sum(np.dot(V[a], np.cross(V[b], V[c])) for a, b, c in F) / 6
    return [(a, c, b) for a, b, c in F] if vol < 0 else F


def capsule(z0, z1, r0, r1, flat=0.8, around=12, cap=3, body=3, offset=(0, 0, 0), axis_rot=None):
    """A tapered capsule along +z from z0 to z1 (caps extend r beyond), elliptic section (y squashed by `flat`).
    The UV seam runs along +x. Returns (V, F, UVL, island size)."""
    prof = []
    for i in range(cap + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / cap
        prof.append((z0 + r0 * math.sin(a), r0 * math.cos(a)))
    for i in range(1, body):
        t = i / body
        prof.append((z0 + (z1 - z0) * t, r0 + (r1 - r0) * t))
    for i in range(cap + 1):
        a = (math.pi / 2) * i / cap
        prof.append((z1 + r1 * math.sin(a), r1 * math.cos(a)))
    arc = [0.0]
    for (za, ra), (zb_, rb) in zip(prof[:-1], prof[1:]):
        arc.append(arc[-1] + math.hypot(zb_ - za, rb - ra))
    rmax = max(r0, r1)
    circ = 2 * math.pi * rmax * (1 + flat) / 2
    V, UVL = [], []
    for k, (z, r) in enumerate(prof):
        for j in range(around + 1):
            phi = 2 * math.pi * j / around
            V.append((r * math.cos(phi), r * flat * math.sin(phi), z))
            UVL.append((circ * j / around, arc[k]))
    F = []
    w = around + 1
    for k in range(len(prof) - 1):
        for j in range(around):
            a, b, c, d = k * w + j, k * w + j + 1, (k + 1) * w + j, (k + 1) * w + j + 1
            F += [(a, c, b), (b, c, d)]
    V = np.array(V)
    if axis_rot is not None:
        V = V @ axis_rot.T
    V = V + np.array(offset)
    F = drop_degenerate(V, F)
    F = orient_outward(V, F)
    return V, F, UVL, (circ, arc[-1])


def palm(width_knuckle, width_wrist, thick, length, cup=0.25, n=22):
    """A rounded, slightly tapered and cupped palm from z 0 (wrist) to z length (knuckles). Two planar UV islands
    (back and palm side), split by each triangle's normal; vertices are duplicated along the split."""
    ex = 0.5  # superellipse exponent: rounded corners
    V, idx = [], {}
    rows = n
    cols = 2 * n
    for i in range(rows + 1):
        th = -math.pi / 2 + math.pi * i / rows
        for j in range(cols + 1):
            ph = -math.pi + 2 * math.pi * j / cols
            ct, st = math.cos(th), math.sin(th)
            if i in (0, rows):
                ct = 0.0  # exact poles: |cos|^ex would turn cos(pi/2)'s 6e-17 into a sliver triangle
            cp, sp = math.cos(ph), math.sin(ph)
            sx = math.copysign(abs(ct) ** ex * abs(cp) ** ex, ct * cp)
            sy = math.copysign(abs(ct) ** ex * abs(sp) ** ex, ct * sp)
            sz = math.copysign(abs(st) ** ex, st)
            z = (sz + 1) / 2 * length
            half = (width_wrist + (width_knuckle - width_wrist) * (z / length)) / 2
            x = sx * half
            y = sy * thick / 2
            if y < -1e-6:  # the seam column sits at sin(pi) ~ 1e-16 on both sides: cup neither
                y += cup * thick * (1 - (x / max(half, 1e-6)) ** 2) * math.sin(math.pi * z / length) * 0.5
            V.append((x, y, z))
    F = []
    w = cols + 1
    for i in range(rows):
        for j in range(cols):
            a, b, c, d = i * w + j, i * w + j + 1, (i + 1) * w + j, (i + 1) * w + j + 1
            F += [(a, b, c), (b, d, c)]
    V = np.array(V)
    F = drop_degenerate(V, F)
    F = orient_outward(V, F)
    # split into the two planar islands by triangle normal, duplicating corners
    out_V, out_F, out_UV, out_isl = [], [], [], []
    for tri in F:
        p = V[list(tri)]
        nrm = np.cross(p[1] - p[0], p[2] - p[0])
        top = nrm[1] >= 0
        for q in p:
            out_V.append(tuple(q))
            # back island: x across, z along; palm island mirrored in x so it reads as seen from below
            out_UV.append((q[0] + width_knuckle / 2, q[2]) if top else (width_knuckle / 2 - q[0], q[2]))
            out_isl.append(0 if top else 1)
        k = len(out_V)
        out_F.append((k - 3, k - 2, k - 1))
    return np.array(out_V), out_F, out_UV, out_isl, (width_knuckle, length)


# ---------------------------------------------------------------------------------------------
# the rig
# ---------------------------------------------------------------------------------------------

# fingers: name, knuckle position on the palm (in L), segment lengths, radii (base, tip), rest splay (deg about y)
FINGERS = [
    ('Index', (-0.150, 0.010, 0.43), (0.22, 0.14, 0.11), (0.064, 0.056), -5),
    ('Middle', (-0.050, 0.015, 0.445), (0.24, 0.16, 0.12), (0.066, 0.057), -1),
    ('Ring', (0.050, 0.010, 0.435), (0.23, 0.15, 0.11), (0.063, 0.054), 3),
    ('Pinky', (0.145, 0.000, 0.40), (0.18, 0.12, 0.09), (0.055, 0.048), 8),
]
THUMB = ('Thumb', (-0.200, -0.045, 0.17), (0.16, 0.13, 0.10), (0.072, 0.060), (12, -38, 52))
SLEEVE_LEN = 3.0  # runs out of frame: the elbow end must never show
LIMITS = {'curl': (-12, 100), 'thumb_curl': (-20, 75), 'thumb_yaw': (-45, 35)}


def build_rig():
    """Joints in order (parents before children): name, parent, local pos (world units), rest rotation (3x3), piece."""
    joints = []

    def add(name, parent, pos, rest, piece=None):
        joints.append({'name': name, 'parent': parent, 'pos': np.array(pos, float) * L, 'rest': rest, 'piece': piece})
        return len(joints) - 1

    root = add('Root', -1, (0, 0, 0), np.eye(3))
    sleeve = Piece('Sleeve')
    sleeve.material = 'sleeve'
    sleeve.add(*capsule(0.0, SLEEVE_LEN * L, 0.24 * L, 0.20 * L, flat=0.82, around=16, cap=2, body=4))
    forearm = add('Forearm', root, (0, 0, -SLEEVE_LEN), np.eye(3), sleeve)
    cuff = Piece('Cuff')
    cuff.add(*capsule(-0.10 * L, 0.02 * L, 0.215 * L, 0.205 * L, flat=0.74, around=16, cap=2, body=2))
    wrist = add('Wrist', forearm, (0, 0, SLEEVE_LEN), np.eye(3), cuff)
    pal = Piece('Palm')
    Vp, Fp, UVp, islp, size = palm(0.44 * L, 0.33 * L, 0.17 * L, 0.46 * L)
    base = 0
    pal.V = [tuple(v) for v in Vp]
    pal.F = [tuple(f) for f in Fp]
    pal.UVL = [tuple(u) for u in UVp]
    pal.island = list(islp)
    pal.islands = [size, size]
    # the thumb's base: a fat capsule merged into the palm, running to the thumb's first joint
    tb = np.array(THUMB[1]) * L
    start = np.array((-0.06, -0.04, 0.06)) * L
    d = tb - start
    length = np.linalg.norm(d)
    yaw = math.atan2(d[0], d[2])
    pitch = -math.asin(d[1] / length)
    pal.add(*capsule(0.0, length, 0.095 * L, 0.075 * L, flat=0.75, around=12, cap=3, body=2,
                     offset=start, axis_rot=rot_y(yaw) @ rot_x(pitch)))
    palm_j = add('Palm', wrist, (0, 0, 0), np.eye(3), pal)

    for name, knuckle, segs, radii, splay in FINGERS:
        parent = palm_j
        pos = knuckle
        for k, seg in enumerate(segs):
            r0 = radii[0] + (radii[1] - radii[0]) * k / 3
            r1 = radii[0] + (radii[1] - radii[0]) * (k + 1) / 3
            pc_ = Piece(f'{name}{k + 1}')
            pc_.add(*capsule(0.0, seg * L, r0 * L, r1 * L, flat=0.88))
            rest = rot_y(math.radians(splay)) if k == 0 else np.eye(3)
            parent = add(f'{name}{k + 1}', parent, pos, rest, pc_)
            pos = (0, 0, seg)
    name, knuckle, segs, radii, rest_euler = THUMB
    parent = palm_j
    pos = knuckle
    for k, seg in enumerate(segs):
        r0 = radii[0] + (radii[1] - radii[0]) * k / 3
        r1 = radii[0] + (radii[1] - radii[0]) * (k + 1) / 3
        pc_ = Piece(f'{name}{k + 1}')
        pc_.add(*capsule(0.0, seg * L, r0 * L, r1 * L, flat=0.85))
        rest = euler(rest_euler) if k == 0 else np.eye(3)
        parent = add(f'{name}{k + 1}', parent, pos, rest, pc_)
        pos = (0, 0, seg)
    return joints


def fk(joints, pose):
    """World (root-space) matrices of every joint. pose: name -> euler degrees applied after the rest rotation."""
    out = []
    for j in joints:
        r = j['rest'] @ euler(pose.get(j['name'], (0, 0, 0)))
        m = np.eye(4)
        m[:3, :3] = r
        m[:3, 3] = j['pos']
        out.append(m if j['parent'] < 0 else out[j['parent']] @ m)
    return out


def tip(joints, mats, name):
    i = [k for k, j in enumerate(joints) if j['name'] == name][0]
    seg_len = {f'{n}3': s[2] for n, _, s, _, _ in FINGERS}
    seg_len['Thumb3'] = THUMB[2][2]
    rad = {'Thumb3': THUMB[3][1], 'Index3': FINGERS[0][3][1]}
    if name == 'Thumb3':
        seg_len[name] = THUMB[2][2]
    return (mats[i] @ np.array([0, 0, (seg_len[name] + 0.6 * rad.get(name, 0.04)) * L, 1]))[:3]


def curl_pose(curls, thumb=(0, 0, 0, 0)):
    """curls: finger -> (c1, c2, c3) degrees; thumb: (yaw, c1, c2, c3)."""
    pose = {}
    for name, c in curls.items():
        for k in range(3):
            pose[f'{name}{k + 1}'] = (c[k], 0, 0)
    yaw, t1, t2, t3 = thumb
    pose['Thumb1'] = (t1, yaw, 0)
    pose['Thumb2'] = (t2, 0, 0)
    pose['Thumb3'] = (t3, 0, 0)
    return pose


def pinch_pose(joints):
    """Index slightly curled; the thumb solved so its tip meets the index tip (grid search)."""
    others = {'Middle': (42, 55, 35), 'Ring': (50, 62, 40), 'Pinky': (58, 68, 45)}
    best = None
    for i1 in (25, 35, 45, 55):
        index = (i1, 40, 22)
        for yaw in range(-45, 36, 5):
            for t1 in range(-20, 76, 5):
                for t23 in range(-10, 76, 5):
                    pose = curl_pose({'Index': index, **others}, (yaw, t1, t23, t23))
                    m = fk(joints, pose)
                    dist = np.linalg.norm(tip(joints, m, 'Thumb3') - tip(joints, m, 'Index3'))
                    if best is None or dist < best[0]:
                        best = (dist, pose)
    return best[1], best[0]


POSES = {
    'open': curl_pose({'Index': (6, 6, 4), 'Middle': (8, 8, 5), 'Ring': (10, 9, 6), 'Pinky': (12, 10, 7)}, (0, 6, 5, 5)),
    'fist': curl_pose({'Index': (80, 95, 60), 'Middle': (82, 95, 60), 'Ring': (84, 95, 60), 'Pinky': (86, 95, 60)}, (-20, 30, 40, 30)),
    'point': curl_pose({'Index': (0, 0, 0), 'Middle': (82, 95, 60), 'Ring': (84, 95, 60), 'Pinky': (86, 95, 60)}, (-20, 30, 40, 30)),
}


def check_limits(pose):
    bad = []
    for name, (x, y, z) in pose.items():
        lo, hi = LIMITS['thumb_curl'] if name.startswith('Thumb') else LIMITS['curl']
        if not lo <= x <= hi:
            bad.append(f'{name} curl {x}')
        if name == 'Thumb1' and not LIMITS['thumb_yaw'][0] <= y <= LIMITS['thumb_yaw'][1]:
            bad.append(f'{name} yaw {y}')
    return bad


# ---------------------------------------------------------------------------------------------
# UV atlas
# ---------------------------------------------------------------------------------------------


def pack(islands):
    """Shelf packing of (width, height) world-unit rectangles into ATLAS px with PAD px gaps, at the largest even texel
    density that fits. Returns (density px/unit, [(x, y) px of each island's corner])."""
    order = sorted(range(len(islands)), key=lambda i: -islands[i][1])
    lo, hi = 0.1, 200.0
    best = None
    for _ in range(40):
        d = 0.5 * (lo + hi)
        x = y = shelf = PAD
        pos = {}
        ok = True
        for i in order:
            w, h = islands[i][0] * d, islands[i][1] * d
            if x + w + PAD > ATLAS:
                x = PAD
                y += shelf + PAD
                shelf = 0
            if y + h + PAD > ATLAS or w + 2 * PAD > ATLAS:
                ok = False
                break
            pos[i] = (x, y)
            x += w + PAD
            shelf = max(shelf, h)
        if ok:
            best = (d, [pos[i] for i in range(len(islands))])
            lo = d
        else:
            hi = d
    return best


def build():
    joints = build_rig()
    pinch, pinch_err = pinch_pose(joints)
    POSES['pinch'] = pinch
    pieces = [j['piece'] for j in joints if j['piece'] is not None]
    islands = []
    for p in pieces:
        p.atlas_index = []
        for isl in p.islands:
            p.atlas_index.append(len(islands))
            islands.append(isl)
    density, corners = pack(islands)
    # uvs
    for p in pieces:
        uv = []
        for (lu, lv), isl in zip(p.UVL, p.island):
            cx, cy = corners[p.atlas_index[isl]]
            uv.append(((cx + lu * density) / ATLAS, 1 - (cy + lv * density) / ATLAS))
        p.UV = uv
    return joints, pieces, islands, density, corners, pinch_err


def paint(pieces, islands, density, corners):
    img = Image.new('RGB', (ATLAS, ATLAS), (128, 128, 128))
    draw = ImageDraw.Draw(img)
    layout = Image.new('RGB', (ATLAS, ATLAS), (255, 255, 255))
    ldraw = ImageDraw.Draw(layout)
    rng = np.random.default_rng(25)
    for p in pieces:
        color = SLEEVE if p.material == 'sleeve' else (CUFF if p.name == 'Cuff' else GLOVE)
        for k, ai in enumerate(p.atlas_index):
            w, h = islands[ai][0] * density, islands[ai][1] * density
            x, y = corners[ai]
            draw.rectangle([x - PAD / 2, y - PAD / 2, x + w + PAD / 2, y + h + PAD / 2], fill=color)
            ldraw.rectangle([x, y, x + w, y + h], outline=(40, 40, 40))
            ldraw.text((x + 3, y + 3), f'{p.name}{"" if len(p.atlas_index) == 1 else ("/back" if k == 0 else "/palm")}', fill=(200, 0, 0))
            if p.name == 'Palm' and k == 0:
                # three stitched lines on the back of the hand, wrist toward the knuckles
                for fx in (-0.075, 0.015, 0.105):
                    px = x + (fx * L + 0.20 * L) * density
                    for s in range(10, int(h * 0.62), 9):
                        draw.line([px, y + s, px, y + s + 5], fill=STITCH, width=2)
            if p.name == 'Cuff':
                for s in range(0, int(w), 7):
                    draw.line([x + s, y, x + s, y + h], fill=(198, 192, 180), width=2)
    arr = np.asarray(img).astype(float)
    noise = rng.normal(0, 3.0, arr.shape[:2])[..., None]
    knit = (np.sin(np.arange(ATLAS)[None, :] * 0.9) * np.sin(np.arange(ATLAS)[:, None] * 1.3))[..., None] * 3
    arr = np.clip(arr + noise + knit, 0, 255).astype(np.uint8)
    img = Image.fromarray(arr).filter(ImageFilter.SMOOTH)
    # draw the triangles' outlines onto the layout so Valentino sees the wireframe
    for p in pieces:
        for f in p.F:
            pts = [(p.UV[i][0] * ATLAS, (1 - p.UV[i][1]) * ATLAS) for i in f]
            ldraw.polygon(pts, outline=(150, 150, 220))
    return img, layout


def smooth_normals(V, F):
    V = np.asarray(V, float)
    key = [tuple(np.round(v, 4)) for v in V]
    acc = {}
    for f in F:
        a, b, c = V[list(f)]
        n = np.cross(b - a, c - a)
        for i in f:
            acc[key[i]] = acc.get(key[i], np.zeros(3)) + n
    N = []
    for k in key:
        n = acc[k]
        N.append(n / max(np.linalg.norm(n), 1e-9))
    return np.array(N)


def kgen(joints, pieces, mats):
    nodes, meshes = [], []
    mesh_index = {}
    for p in pieces:
        mesh_index[p.name] = len(meshes)
        V = np.asarray(p.V, float)
        N = smooth_normals(V, p.F)
        meshes.append({'name': p.name, 'vertices': V.reshape(-1).round(5).tolist(), 'normals': N.reshape(-1).round(5).tolist(),
                       'uv': np.asarray(p.UV).reshape(-1).round(6).tolist(), 'subMeshStarts': [0],
                       'indices': [int(i) for f in p.F for i in f]})
    for j in joints:
        p = j['piece']
        q = quat(j['rest'])
        nodes.append({'name': j['name'], 'parent': j['parent'] + 1 if j['parent'] >= 0 else 0,
                      'pos': [round(float(v), 5) for v in j['pos']], 'rot': [round(float(v), 7) for v in q],
                      'scale': [1, 1, 1], 'mesh': mesh_index[p.name] if p else -1,
                      'materials': [mats['sleeve' if (p and p.material == 'sleeve') else 'glove']], 'castShadows': True})
    return nodes, meshes


def sockets(joints, pinch):
    """Grip and Carry: the pinch point (between thumb and index tips) in the Palm's space."""
    m = fk(joints, pinch)
    point = 0.5 * (tip(joints, m, 'Thumb3') + tip(joints, m, 'Index3'))
    palm_i = [k for k, j in enumerate(joints) if j['name'] == 'Palm'][0]
    local = np.linalg.inv(m[palm_i]) @ np.append(point, 1)
    return local[:3]


def write_previews(joints, pieces, tex_img, layout, checker=True):
    os.makedirs(PREVIEWS, exist_ok=True)
    tex = np.asarray(tex_img).astype(float) / 255
    iy, ix = np.indices((ATLAS, ATLAS))
    chk = (iy // 32 + ix // 32) % 2
    chk = np.stack([0.3 + 0.6 * chk] * 3, -1)

    def meshes_for(pose, texture, kami=True, offset=(0, 0, 0)):
        m = fk(joints, pose)
        out = []
        for k, j in enumerate(joints):
            p = j['piece']
            if p is None:
                continue
            V = (m[k] @ np.hstack([np.asarray(p.V), np.ones((len(p.V), 1))]).T).T[:, :3] + np.array(offset)
            out.append({'V': V, 'F': p.F, 'uv': np.asarray(p.UV), 'tex': texture})
        if kami:
            # an 8-unit Kami silhouette next to the hand, for scale
            kx, kz = 0.35 * L, 0.6 * L
            Vk = np.array([[kx, -0.2 * L, kz], [kx + 3, -0.2 * L, kz], [kx + 3, -0.2 * L + 8, kz], [kx, -0.2 * L + 8, kz]]) + np.array(offset)
            out.append({'V': Vk, 'F': [(0, 1, 2), (0, 2, 3), (0, 2, 1), (0, 3, 2)], 'color': (0.85, 0.35, 0.3)})
        return out

    center = np.array([0, 0, 0.35 * L])
    frames = []
    for ang in range(0, 360, 60):
        a = math.radians(ang)
        eye = center + np.array([math.sin(a) * 2.6 * L, 0.9 * L, -math.cos(a) * 2.6 * L])
        frames.append(zb.render(meshes_for(POSES['open'], tex), zb.look_at(eye, center), size=(360, 300)))
    sheet = np.vstack([np.hstack(frames[:3]), np.hstack(frames[3:])])
    Image.fromarray((sheet * 255).astype(np.uint8)).save(os.path.join(PREVIEWS, 'hand-turntable.png'))
    if checker:
        eye = center + np.array([1.6 * L, 1.4 * L, -1.8 * L])
        img = zb.render(meshes_for(POSES['open'], chk, kami=False), zb.look_at(eye, center), size=(640, 520))
        Image.fromarray((img * 255).astype(np.uint8)).save(os.path.join(PREVIEWS, 'hand-checker.png'))
    layout.save(os.path.join(PREVIEWS, 'hand-uv-layout.png'))
    poses = []
    for name in ('open', 'pinch', 'fist', 'point'):
        eye = center + np.array([1.9 * L, 0.6 * L, -1.7 * L])
        poses.append(zb.render(meshes_for(POSES[name], tex, kami=False), zb.look_at(eye, center), size=(320, 280)))
    Image.fromarray((np.hstack(poses) * 255).astype(np.uint8)).save(os.path.join(PREVIEWS, 'hand-poses.png'))


def pose_clip(joints, keys, length, name, loop=False):
    """keys: [(time, pose name)] -> a clip of the finger/thumb joints (rest rotation x pose)."""
    curves = []
    paths = {}
    for k, j in enumerate(joints):
        parts = []
        i = k
        while i >= 0:
            parts.append(joints[i]['name'])
            i = joints[i]['parent']
        paths[j['name']] = '/'.join(reversed(parts))
    animated = sorted({n for _, p in keys for n in POSES[p]})
    for n in animated:
        j = [x for x in joints if x['name'] == n][0]
        times, values = [], []
        for t, p in keys:
            q = quat(j['rest'] @ euler(POSES[p].get(n, (0, 0, 0))))
            times.append(t)
            values += [round(float(v), 7) for v in q]
        curves.append({'path': paths[n], 'property': 'localRotation', 'times': times, 'values': values})
    return {'name': name, 'length': length, 'loop': loop, 'curves': curves}


def main():
    joints, pieces, islands, density, corners, pinch_err = build()
    bad = {name: check_limits(p) for name, p in POSES.items()}
    tri_count = sum(len(p.F) for p in pieces)
    ua.ensure_folder(OUT_DIR)
    tex_img, layout = paint(pieces, islands, density, corners)
    tex_rel = f'{OUT_DIR}/NarratorHand_Atlas.png'
    tex_img.save(ua.path(tex_rel))
    tex_guid = ua.ensure_meta(tex_rel, ua.TEXTURE, srgb=1, wrap=1)
    layout.save(ua.path(f'{OUT_DIR}/NarratorHand_UVLayout.png'))
    ua.ensure_meta(f'{OUT_DIR}/NarratorHand_UVLayout.png', ua.TEXTURE, srgb=1, wrap=1)
    mats = {}
    for key, name, color in (('glove', 'Glove', (1, 1, 1, 1)), ('sleeve', 'Sleeve', (1, 1, 1, 1))):
        rel = f'{OUT_DIR}/{name}.mat'
        ua.write(rel, ua.urp_lit(name, tex_guid, color, smoothness=0.12 if key == 'glove' else 0.05))
        ua.ensure_meta(rel, ua.NATIVE, main=2100000)
        mats[key] = rel
    nodes, meshes = kgen(joints, pieces, mats)
    grip = sockets(joints, POSES['pinch'])
    palm_index = [k for k, j in enumerate(joints) if j['name'] == 'Palm'][0] + 1
    for s in ('Grip', 'Carry'):
        nodes.append({'name': s, 'parent': palm_index, 'pos': [round(float(v), 5) for v in grip], 'rot': [0, 0, 0, 1],
                      'scale': [1, 1, 1], 'mesh': -1, 'materials': [], 'castShadows': False})
    # the importer's root is the asset itself; node 0 here is "Root" under it, so parents shift by one
    nodes = [{'name': 'NarratorHand', 'parent': -1, 'pos': [0, 0, 0], 'rot': [0, 0, 0, 1], 'scale': [1, 1, 1], 'mesh': -1,
              'materials': [], 'castShadows': False}] + nodes
    for n in nodes[1:]:
        if n['parent'] == 0 and n['name'] == 'Root':
            pass
    clip = pose_clip(joints, [(0, 'open'), (1, 'pinch'), (2, 'fist'), (3, 'point'), (4, 'open')], 4.0, 'Hand_TestPoses')
    for c in clip['curves']:
        c['path'] = 'NarratorHand/' + c['path']
    gen = {'name': 'NarratorHand', 'skinned': False, 'nodes': nodes, 'meshes': meshes, 'clips': [clip]}
    rel = f'{OUT_DIR}/NarratorHand.kgen'
    ua.write(rel, json.dumps(gen, separators=(',', ':')))
    importer = ua.guid_of('Assets/Editor/Generated/KamiGenImporter.cs')
    ua.ensure_meta(rel, ua.SCRIPTED, importer=importer)
    rig = {'L': L, 'joints': [{'name': j['name'], 'parent': j['parent'], 'pos': j['pos'].tolist(),
                               'rest': j['rest'].tolist()} for j in joints],
           'grip_in_palm': grip.tolist(), 'poses': POSES, 'limits': LIMITS, 'pinch_error': pinch_err}
    json.dump(rig, open(os.path.join(ROOT, 'tools', 'data', 'narrator-hand.json'), 'w'), indent=1)
    write_previews(joints, pieces, tex_img, layout)
    print(f'[make-narrator-hand] {len(pieces)} pieces, {tri_count} triangles, {len(islands)} UV islands at '
          f'{density:.2f} px/unit, pinch gap {pinch_err:.2f} units, limit problems: {sum(len(b) for b in bad.values())}')
    for name, b in bad.items():
        for x in b:
            print(f'  pose {name}: {x} outside its limits')


if __name__ == '__main__':
    main()
