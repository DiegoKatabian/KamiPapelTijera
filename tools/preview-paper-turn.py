"""Side-view contact sheet of a paper page turn (spec 025 V12): the sheet and a set of pieces (houses, cards, a wide
volume) on each leaf, folding through the turn, using the Python reference (tools/paper_core.py), on Level 2's book.
Writes specs/025-paper-book/previews/paper-turn-level2.png. Pieces of the page being left are red, of the page
arriving blue; the sheet is black.

    python tools/preview-paper-turn.py
"""
import json
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import paper_core as pc  # noqa: E402

PREVIEWS = os.path.join(ROOT, 'specs', '025-paper-book', 'previews')


def box(x0, x1, y0, y1, z0, z1):
    return [(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]


def main(level='level2'):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    d = json.load(open(os.path.join(ROOT, 'tools', 'data', f'paper-book-{level}.json')))
    fit = json.load(open(os.path.join(ROOT, 'tools', 'data', 'paper-curl.json')))
    curl = pc.Curl(pc.Profile(d['profiles']['right']), pc.Profile(d['profiles']['left']),
                   pc.CurlParams(fit['lead'], fit['ease'], fit['duration'], fit['shape']))
    model = pc.TurnModel(curl, 1)
    top = 1.0
    specs = [
        (pc.VOLUME, pc.SHEET_FRONT, box(8, 16, top, top + 12, -3, 3), 'r'),
        (pc.CARD, pc.SHEET_FRONT, box(26, 30, top, top + 9, -0.1, 0.1), 'r'),
        (pc.VOLUME, pc.SHEET_FRONT, box(33, 44, top, top + 4, -6, 6), 'r'),
        (pc.VOLUME, pc.END_STATIC, box(-14, -6, top, top + 14, -3, 3), 'r'),
        (pc.CARD, pc.END_STATIC, box(-30, -25, top, top + 8, -0.1, 0.1), 'r'),
        (pc.VOLUME, pc.START_STATIC, box(5, 12, top, top + 10, -3, 3), 'b'),
        (pc.CARD, pc.START_STATIC, box(20, 24, top, top + 7, -0.1, 0.1), 'b'),
        (pc.VOLUME, pc.SHEET_BACK, box(-20, -12, top, top + 11, -3, 3), 'b'),
        (pc.CARD, pc.SHEET_BACK, box(-36, -32, top, top + 6, -0.1, 0.1), 'b'),
    ]
    pieces = []
    for kind, leaf, corners, color in specs:
        p = pc.Piece(kind, leaf, corners)
        model.prepare(p)
        pieces.append((p, color))
        # as PaperTurn.SetLeafThickness: half the tallest piece on each leaf
        height = max(c[1] for c in corners) - top
        model.thickness[leaf] = max(model.thickness[leaf], 0.5 * height)
    times = np.linspace(0, 1, 13)
    fig, axes = plt.subplots(len(times), 1, figsize=(10, 3.2 * len(times)))
    for ax, t in zip(axes, times):
        model.set_time(t)
        pts, _ = model.sheet
        ax.plot(pts[:, 0], pts[:, 1], 'k', lw=2)
        S, E = curl.S, curl.E
        ax.plot(S.xo, S.y, color='0.7')
        ax.plot(-E.xo, E.y, color='0.7')
        for p, color in pieces:
            m = model.solve(p)
            c = np.hstack([p.corners, np.ones((8, 1))]) @ m.T
            # side view: the box's outline in x/y (corners 0..7 in x,y,z order)
            for a, b in ((0, 2), (2, 6), (6, 4), (4, 0), (1, 3), (3, 7), (7, 5), (5, 1), (0, 1), (2, 3), (4, 5), (6, 7)):
                ax.plot([c[a, 0], c[b, 0]], [c[a, 1], c[b, 1]], color=color, lw=1)
        ax.set_xlim(-50, 50)
        ax.set_ylim(-2, 50)
        ax.set_aspect('equal')
        ax.set_title(f't = {t:.2f}', fontsize=9)
    fig.tight_layout()
    os.makedirs(PREVIEWS, exist_ok=True)
    out = os.path.join(PREVIEWS, f'paper-turn-{level}.png')
    fig.savefig(out, dpi=60)
    print(f'[preview-paper-turn] {out}')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'level2')
