"""Placeholder front covers for the intro books (spec 025 D12, task 2.B): the level's title on a plain cover, sized to
the cover face measured by tools/make-intro-books.py (its proportions come from tools/data/paper-book-*.json).
Valentino's covers replace these PNGs later, same size and file name, no other change (#365).

    python tools/make-book-covers.py
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402

OUT_DIR = 'Assets/Generated/IntroBooks'
COVERS = {
    'Level1': {'title': ['Kami', 'Papel y Tijera'], 'paper': (152, 52, 44), 'ink': (236, 220, 180)},
    'Level2': {'title': ['Kami', 'Tinta y Condena'], 'paper': (58, 58, 62), 'ink': (232, 228, 214)},
}
FONTS = [r'C:\Windows\Fonts\georgiab.ttf', r'C:\Windows\Fonts\georgia.ttf', r'C:\Windows\Fonts\timesbd.ttf']


def font(size):
    for f in FONTS:
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def cover_size(level):
    d = json.load(open(os.path.join(ROOT, 'tools', 'data', f'paper-book-{level.lower()}.json')))
    w = d['profiles']['left'][-1][0]
    h = d['page_z'][1] - d['page_z'][0]
    return w, h


def make(level, spec):
    w, h = cover_size(level)
    scale = 1024 / max(w, h)
    W, H = int(round(w * scale)), int(round(h * scale))
    img = Image.new('RGB', (W, H), spec['paper'])
    d = ImageDraw.Draw(img)
    m = int(0.06 * min(W, H))
    d.rectangle([m, m, W - m, H - m], outline=spec['ink'], width=max(3, W // 200))
    d.rectangle([m + 12, m + 12, W - m - 12, H - m - 12], outline=spec['ink'], width=max(1, W // 600))
    sizes = [int(min(W, H) * 0.16), int(min(W, H) * 0.09)]
    lines = [(t, font(s)) for t, s in zip(spec['title'], sizes)]
    heights = [d.textbbox((0, 0), t, font=f)[3] - d.textbbox((0, 0), t, font=f)[1] for t, f in lines]
    y = H / 2 - (sum(heights) + 0.3 * sizes[0]) / 2
    for (t, f), th in zip(lines, heights):
        bb = d.textbbox((0, 0), t, font=f)
        d.text(((W - (bb[2] - bb[0])) / 2 - bb[0], y - bb[1]), t, font=f, fill=spec['ink'])
        y += th + 0.3 * sizes[0]
    rel = f'{OUT_DIR}/Cover_{level}.png'
    ua.ensure_folder(OUT_DIR)
    img.save(ua.path(rel))
    g = ua.ensure_meta(rel, ua.TEXTURE, srgb=1, wrap=1)
    print(f'[make-book-covers] {rel} {W}x{H} ({g})')
    return g


def main():
    for level, spec in COVERS.items():
        make(level, spec)


if __name__ == '__main__':
    main()
