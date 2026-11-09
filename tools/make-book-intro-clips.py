"""The level intro's animation clips (spec 025 D10, D16, task 6.A) and the hand's Level 3 set (task 7.A).

Per level, Assets/Generated/BookIntro/BookIntroClips_LevelN.kgen holds two clips:
  BookIntro_Hand_LevelN   on the hand (paths NarratorHand/Root/...): the hand comes in from the right, pinches the
                          cover's edge, lifts it to 120 degrees, lets go, leaves, comes back carrying Kami, lowers her
                          over her spawn, lets go, leaves.
  BookIntro_Book_LevelN   on the intro book (path IntroBook_LevelN/Hinge): closed, lifted with the hand, falls open,
                          bounces.
Both are computed from the same numbers: while the hand holds the cover its Grip socket is placed on the cover's edge
on every key (the generator measures the error between keys and fails above 0.05 units, V5).

Assets/Generated/NarratorHand/NarratorHand_Level3.kgen holds the Level 3 clips (spec's table, plus Grab / Carry /
Place), in the hand's own space (Root at the origin): the Animator Controller is built from them by
Kami > Book Intro > Build.

Beat times go to tools/data/book-intro-clips.json; BookIntro's defaults match them.

    python tools/make-book-intro-clips.py
"""
import importlib.util
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402

_spec = importlib.util.spec_from_file_location('hand', os.path.join(ROOT, 'tools', 'make-narrator-hand.py'))
hand = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hand)

RIG = json.load(open(os.path.join(ROOT, 'tools', 'data', 'narrator-hand.json')))
BOOKS = json.load(open(os.path.join(ROOT, 'tools', 'data', 'intro-books.json')))
L = RIG['L']
GRIP = np.array(RIG['grip_in_palm'])  # Palm == Wrist == Root at rest, so this is the grip in Root space
POSES = RIG['poses']

# Kami's position in each level scene (her spawn and respawn point), read from the scenes' Kami.prefab instances
KAMI = {'Level1': (-33.0625, 0.0075, -14.607229), 'Level2': (-40.25, 6.32, -31.5)}

# the sequence (seconds), spec 025 D16
T_ENTER, T_GRIP, T_RELEASE_COVER = 2.0, 3.0, 5.0
T_FALL_END, T_BOUNCE_END = 5.45, 5.85
T_HAND_OUT = 6.0
T_RETURN, T_OVER_SPAWN, T_LOWERED, T_LET_GO = 7.0, 9.4, 10.0, 10.1
T_GONE, LENGTH = 11.8, 12.0
LIFT_TO = -60.0          # hinge angle when the hand lets go (cover 120 degrees open)
HAND_ROLL = 0.6          # how much of the cover's turn the hand's own roll follows while gripping
RELEASE_HEIGHT = 4.0     # Kami falls this far after being let go
HANG = 7.0               # her root below the pinch while carried (BookIntro._carryOffset matches)
BEATS = {'coverOpened': T_FALL_END, 'showKami': 6.6, 'releaseKami': T_LET_GO, 'end': LENGTH}


def smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


R0 = hand.rot_y(math.radians(-90))  # fingers toward -x (the book), palm down, sleeve out to the right


def hinge_angle(t):
    """Degrees: -180 closed, 0 open."""
    if t <= T_GRIP:
        return -180.0
    if t <= T_RELEASE_COVER:
        return -180.0 + (LIFT_TO + 180.0) * smooth((t - T_GRIP) / (T_RELEASE_COVER - T_GRIP))
    if t <= T_FALL_END:
        u = (t - T_RELEASE_COVER) / (T_FALL_END - T_RELEASE_COVER)
        return LIFT_TO * (1 - u * u)  # falls, accelerating
    if t <= T_BOUNCE_END:
        u = (t - T_FALL_END) / (T_BOUNCE_END - T_FALL_END)
        return -7.0 * math.sin(math.pi * u)
    return 0.0


def edge_point(book, angle_deg):
    """World position of the cover edge the hand pinches (outer edge, cover thickness middle, toward the camera)."""
    hinge = np.array(book['hinge'])
    e = np.array([book['x_outer'] - book['spine_x'], -(book['top'] - book['bottom']) / 2,
                  book['z0'] + 0.38 * (book['z1'] - book['z0'])])
    p = rot_z(math.radians(angle_deg)) @ np.array([e[0], e[1], 0.0])
    return hinge + np.array([p[0], p[1], e[2]])


def hand_rot(angle_deg):
    return rot_z(math.radians((angle_deg + 180.0) * HAND_ROLL)) @ R0


def root_for_grip(point, R):
    return point - R @ GRIP


def lerp(a, b, t):
    return np.asarray(a) + (np.asarray(b) - np.asarray(a)) * t


def hand_clip(level, book):
    """Root position/rotation keys + finger pose keys."""
    kami = np.array(KAMI[level])
    closed_edge = edge_point(book, -180.0)
    R_grip = hand_rot(-180.0)
    p_grip = root_for_grip(closed_edge, R_grip)
    p_off = p_grip + np.array([1.6 * L, 1.4 * L, -0.2 * L])        # out of frame, up and to the right
    keys_p, keys_r = [], []

    def key(t, p, R):
        keys_p.append((t, p))
        keys_r.append((t, hand.quat(R)))

    key(0.0, p_off, R_grip)
    key(T_ENTER, p_off, R_grip)
    for k in range(1, 11):
        u = k / 10
        key(T_ENTER + (T_GRIP - T_ENTER) * u, lerp(p_off, p_grip, smooth(u)) + np.array([0, 4 * math.sin(math.pi * u), 0]), R_grip)
    # holding the cover: dense keys, the grip on the edge
    n = 60  # 1/30 s: the grip error between keys falls with the square of the key spacing
    for k in range(1, n + 1):
        t = T_GRIP + (T_RELEASE_COVER - T_GRIP) * k / n
        a = hinge_angle(t)
        R = hand_rot(a)
        key(t, root_for_grip(edge_point(book, a), R), R)
    # let go and leave, up and to the right
    R_rel = hand_rot(LIFT_TO)
    p_rel = root_for_grip(edge_point(book, LIFT_TO), R_rel)
    key(T_RELEASE_COVER + 0.25, p_rel + np.array([0.1 * L, 0.15 * L, 0]), R_rel)
    key(T_HAND_OUT, p_rel + np.array([1.5 * L, 1.2 * L, 0]), R_rel)
    # come back with Kami: grip socket = Kami's pinch point
    R_carry = hand.rot_y(math.radians(-90)) @ hand.rot_x(math.radians(-10))
    socket_release = kami + np.array([0, RELEASE_HEIGHT + HANG, 0])
    over = socket_release + np.array([0, 0.25 * L, 0])
    enter = over + np.array([1.4 * L, 0.9 * L, 0])
    key(T_RETURN, root_for_grip(enter, R_carry), R_carry)
    for k in range(1, 9):
        u = k / 8
        key(T_RETURN + (T_OVER_SPAWN - T_RETURN) * u, root_for_grip(lerp(enter, over, smooth(u)), R_carry), R_carry)
    key(T_LOWERED, root_for_grip(socket_release, R_carry), R_carry)
    key(T_LET_GO, root_for_grip(socket_release, R_carry), R_carry)
    key(T_LET_GO + 0.3, root_for_grip(socket_release + np.array([0.05 * L, 0.12 * L, 0]), R_carry), R_carry)
    key(T_GONE, root_for_grip(socket_release + np.array([1.3 * L, 1.3 * L, 0]), R_carry), R_carry)
    key(LENGTH, root_for_grip(socket_release + np.array([1.3 * L, 1.3 * L, 0]), R_carry), R_carry)
    # fingers
    finger_keys = [(0.0, 'open'), (T_GRIP - 0.25, 'open'), (T_GRIP, 'pinch'), (T_RELEASE_COVER, 'pinch'),
                   (T_RELEASE_COVER + 0.2, 'open'), (T_RETURN - 0.3, 'pinch'), (T_LET_GO, 'pinch'),
                   (T_LET_GO + 0.2, 'open'), (LENGTH, 'open')]
    return keys_p, keys_r, finger_keys, socket_release


def paths():
    joints = RIG['joints']
    out = {}
    for k, j in enumerate(joints):
        parts = []
        i = k
        while i >= 0:
            parts.append(joints[i]['name'])
            i = joints[i]['parent']
        out[j['name']] = 'NarratorHand/' + '/'.join(reversed(parts))
    return out


def finger_curves(finger_keys):
    P = paths()
    joints = {j['name']: j for j in RIG['joints']}
    curves = []
    names = sorted({n for _, p in finger_keys for n in POSES[p]})
    for n in names:
        rest = np.array(joints[n]['rest'])
        times, values = [], []
        for t, p in finger_keys:
            q = hand.quat(rest @ hand.euler(POSES[p].get(n, (0, 0, 0))))
            times.append(round(t, 4))
            values += [round(float(v), 7) for v in q]
        curves.append({'path': P[n], 'property': 'localRotation', 'times': times, 'values': values})
    return curves


def root_curves(keys_p, keys_r):
    P = paths()
    return [
        {'path': P['Root'], 'property': 'localPosition', 'times': [round(t, 4) for t, _ in keys_p],
         'values': [round(float(v), 5) for _, p in keys_p for v in p]},
        {'path': P['Root'], 'property': 'localRotation', 'times': [round(t, 4) for t, _ in keys_r],
         'values': [round(float(v), 7) for _, q in keys_r for v in continuity(keys_r, q)]},
    ]


def continuity(keys, q):
    return q


def fix_quat_signs(keys_r):
    out = []
    prev = None
    for t, q in keys_r:
        q = np.array(q)
        if prev is not None and np.dot(prev, q) < 0:
            q = -q
        out.append((t, q))
        prev = q
    return out


def grip_error(level, book, keys_p, keys_r):
    """Worst distance between the grip socket and the cover edge while holding, evaluated between keys (linear
    position, slerp rotation: what Unity's curves do near enough at this key density)."""
    worst = 0.0
    kp = [(t, np.array(p)) for t, p in keys_p]
    kr = [(t, np.array(q)) for t, q in keys_r]
    for k in range(len(kp) - 1):
        t0, p0 = kp[k]
        t1, p1 = kp[k + 1]
        if t0 < T_GRIP - 1e-6 or t1 > T_RELEASE_COVER + 1e-6:
            continue
        q0, q1 = kr[k][1], kr[k + 1][1]
        for u in (0.25, 0.5, 0.75):
            t = t0 + (t1 - t0) * u
            p = p0 + (p1 - p0) * u
            q = q0 + (q1 - q0) * u
            q /= np.linalg.norm(q)
            x, y, z, w = q
            R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                          [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                          [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
            g = p + R @ GRIP
            worst = max(worst, float(np.linalg.norm(g - edge_point(book, hinge_angle(t)))))
    return worst


def book_curve(level):
    times, values = [], []
    t = 0.0
    while t <= LENGTH + 1e-6:
        dense = T_GRIP - 0.1 <= t <= T_BOUNCE_END + 0.1
        times.append(round(t, 4))
        values += [round(float(v), 7) for v in hand.quat(rot_z(math.radians(hinge_angle(t))))]
        t += 1 / 15 if dense else 0.5
    # quaternion continuity: -180 and 180 are the same rotation; keep neighbours on the same hemisphere
    q = np.array(values).reshape(-1, 4)
    for i in range(1, len(q)):
        if np.dot(q[i - 1], q[i]) < 0:
            q[i] = -q[i]
    return {'path': f'IntroBook_{level}/Hinge', 'property': 'localRotation', 'times': times,
            'values': [round(float(v), 7) for v in q.reshape(-1)]}


def level3_clips():
    """Level 3's set (spec table), in the hand's own space: Root at the origin, fingers along +z."""
    P = paths()
    clips = []

    def clip(name, length, loop, root_keys, finger_keys):
        curves = finger_curves(finger_keys)
        if root_keys:
            kp = [(t, np.array(p)) for t, p, _ in root_keys]
            kr = fix_quat_signs([(t, hand.quat(R)) for t, _, R in root_keys])
            curves += [
                {'path': P['Root'], 'property': 'localPosition', 'times': [round(t, 4) for t, _ in kp],
                 'values': [round(float(v), 5) for _, p in kp for v in p]},
                {'path': P['Root'], 'property': 'localRotation', 'times': [round(t, 4) for t, _ in kr],
                 'values': [round(float(v), 7) for _, q in kr for v in q]},
            ]
        clips.append({'name': name, 'length': length, 'loop': loop, 'curves': curves})

    I = np.eye(3)
    up = hand.rot_z(math.pi)  # palm up
    clip('Hand_Idle_Hover', 3.0, True,
         [(t, (0, 0.6 * math.sin(2 * math.pi * t / 3.0), 0), hand.rot_x(math.radians(2 * math.sin(2 * math.pi * t / 3.0)))) for t in np.linspace(0, 3, 13)],
         [(0, 'open'), (3, 'open')])
    clip('Hand_Point_No', 2.0, True,
         [(t, (0, 0, 0), hand.rot_y(math.radians(12 * math.sin(2 * math.pi * t)))) for t in np.linspace(0, 2, 17)],
         [(0, 'point'), (2, 'point')])
    clip('Hand_Reach_Unfold_Fail', 3.0, False,
         [(0, (0, 0, -6), I), (0.9, (0, -3, 6), I), (1.3, (0, -3, 6), I), (1.9, (0, 1, -2), hand.rot_x(math.radians(-8))),
          (2.2, (0, 2, -4), I), (3.0, (0, 0, -6), I)],
         [(0, 'open'), (0.9, 'open'), (1.2, 'pinch'), (1.8, 'pinch'), (2.0, 'open'), (3.0, 'open')])
    clip('Hand_Plead', 4.0, True,
         [(t, (0, 0.5 * math.sin(math.pi * t / 2), 0), up @ hand.rot_x(math.radians(-10 + 4 * math.sin(math.pi * t / 2)))) for t in np.linspace(0, 4, 13)],
         [(0, 'open'), (4, 'open')])
    clip('Hand_Offer_Palm', 2.0, True, [(0, (0, 0, 0), up), (2, (0, 0, 0), up)], [(0, 'open'), (2, 'open')])
    clip('Hand_Grab', 1.2, False, [(0, (0, 6, 0), I), (0.8, (0, 0, 0), I), (1.2, (0, 1, 0), I)],
         [(0, 'open'), (0.7, 'open'), (1.0, 'pinch'), (1.2, 'pinch')])
    clip('Hand_Carry', 2.0, True,
         [(t, (0, 0.4 * math.sin(math.pi * t), 0), hand.rot_z(math.radians(3 * math.sin(math.pi * t)))) for t in np.linspace(0, 2, 9)],
         [(0, 'pinch'), (2, 'pinch')])
    clip('Hand_Place', 1.4, False, [(0, (0, 4, 0), I), (0.9, (0, 0, 0), I), (1.4, (0, 3, 0), I)],
         [(0, 'pinch'), (0.9, 'pinch'), (1.1, 'open'), (1.4, 'open')])
    clip('Hand_Wave', 1.6, True,
         [(t, (0, 0, 0), hand.rot_x(math.radians(-80)) @ hand.rot_z(math.radians(18 * math.sin(2 * math.pi * t / 0.8)))) for t in np.linspace(0, 1.6, 17)],
         [(0, 'open'), (1.6, 'open')])
    # tapping: each finger lifts in turn (curl negative = up)
    tap = []
    for k, f in enumerate(('Pinky', 'Ring', 'Middle', 'Index')):
        tap.append(f)
    clips_tap_keys = []
    joints = {j['name']: j for j in RIG['joints']}
    curves = []
    for k, f in enumerate(tap):
        n = f + '1'
        times, values = [], []
        for t in np.linspace(0, 1.2, 13):
            phase = (t / 1.2 - k * 0.18) % 1.0
            lift = -18 * math.sin(math.pi * min(phase / 0.2, 1)) if phase < 0.2 else 0.0
            q = hand.quat(np.array(joints[n]['rest']) @ hand.euler((10 + lift, 0, 0)))
            times.append(round(float(t), 4))
            values += [round(float(v), 7) for v in q]
        curves.append({'path': P[n], 'property': 'localRotation', 'times': times, 'values': values})
    clips.append({'name': 'Hand_Tap_Desk', 'length': 1.2, 'loop': True, 'curves': curves})
    return clips


def main():
    report = {'beats': BEATS, 'release_height': RELEASE_HEIGHT, 'hang': HANG, 'levels': {}}
    importer = ua.guid_of('Assets/Editor/Generated/KamiGenImporter.cs')
    ua.ensure_folder('Assets/Generated/BookIntro')
    worst_all = 0.0
    for level in ('Level1', 'Level2'):
        book = BOOKS[level]
        keys_p, keys_r, finger_keys, socket_release = hand_clip(level, book)
        keys_r = fix_quat_signs(keys_r)
        err = grip_error(level, book, keys_p, keys_r)
        worst_all = max(worst_all, err)
        hand_clip_json = {'name': f'BookIntro_Hand_{level}', 'length': LENGTH, 'loop': False,
                          'curves': root_curves(keys_p, keys_r) + finger_curves(finger_keys)}
        book_clip_json = {'name': f'BookIntro_Book_{level}', 'length': LENGTH, 'loop': False, 'curves': [book_curve(level)]}
        gen = {'name': f'BookIntroClips_{level}', 'nodes': [], 'meshes': [], 'clips': [hand_clip_json, book_clip_json]}
        rel = f'Assets/Generated/BookIntro/BookIntroClips_{level}.kgen'
        ua.write(rel, json.dumps(gen, separators=(',', ':')))
        ua.ensure_meta(rel, ua.SCRIPTED, importer=importer)
        report['levels'][level] = {'kami': KAMI[level], 'socket_release': socket_release.tolist(), 'grip_error': err}
        print(f'[make-book-intro-clips] {rel}: {len(keys_p)} root keys, grip error while holding {err:.4f} units')
    gen = {'name': 'NarratorHand_Level3', 'nodes': [], 'meshes': [], 'clips': level3_clips()}
    rel = 'Assets/Generated/NarratorHand/NarratorHand_Level3.kgen'
    ua.write(rel, json.dumps(gen, separators=(',', ':')))
    ua.ensure_meta(rel, ua.SCRIPTED, importer=importer)
    print(f'[make-book-intro-clips] {rel}: {len(gen["clips"])} clips')
    # every curve path must exist on the hand or the book
    hand_paths = set(paths().values())
    bad = []
    for f in ('Level1', 'Level2'):
        g = json.load(open(ua.path(f'Assets/Generated/BookIntro/BookIntroClips_{f}.kgen')))
        for c in g['clips']:
            for cv in c['curves']:
                if cv['path'] not in hand_paths and cv['path'] != f'IntroBook_{f}/Hinge':
                    bad.append(cv['path'])
    for c in gen['clips']:
        for cv in c['curves']:
            if cv['path'] not in hand_paths:
                bad.append(cv['path'])
    report['missing_paths'] = bad
    json.dump(report, open(os.path.join(ROOT, 'tools', 'data', 'book-intro-clips.json'), 'w'), indent=1)
    print(f'[make-book-intro-clips] worst grip error {worst_all:.4f} (limit 0.05), missing paths: {len(bad)}')
    if worst_all > 0.05 or bad:
        sys.exit(1)


if __name__ == '__main__':
    main()
