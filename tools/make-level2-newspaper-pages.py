"""Renders Level 2's newspaper: ten pages of one issue of "The Daily Ink", two per page of the level
(the open book's left and right halves), and the faces of the sheets that turn between them.

The book is a real book: turning forward from level page N, the sheet carries page N's right half on the
face that leaves and page N+1's left half on the face that lands. Every half is a different newspaper page;
the right halves carry the level page number in their lower corner.

Style: Valentino's vintage newspaper reference. Only game art is printed (the dialogue portraits, the
Pelusa, Valentino's posters), halftoned. All the text lives in the page functions below.

Fit: the pages have the exact proportions of the book's halves, and the sheet faces are warped triangle by
triangle onto the real turning mesh, using tools/data/level2-book-pages.json (tools/measure-book-pages.py).

Writes into Assets/3D/Libros/Diario Kami/Texturas/Newspaper Textures/Level2 Pages/:
  Level2_News_P<n>_Left.jpg / _Right.jpg       the book's halves on level page n
  Sheets/Level2_Sheet_P<a>toP<b>_Leaving.jpg   the face lying on the book when the turn from a to b starts
  Sheets/Level2_Sheet_P<a>toP<b>_Arriving.jpg  the face that lands when it ends
  Level2_BookPages.asset                       the BookPageSet that lists them all (BookPageSpread reads it)
plus a .meta for each new image (an existing .meta is kept, so its GUID and references survive a re-run).

    python tools/make-level2-newspaper-pages.py [--preview <png>]
"""
import json
import math
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEOMETRY = os.path.join(ROOT, 'tools', 'data', 'level2-book-pages.json')
OUT_DIR = os.path.join(ROOT, 'Assets', '3D', 'Libros', 'Diario Kami', 'Texturas', 'Newspaper Textures', 'Level2 Pages')
SHEET_DIR = os.path.join(OUT_DIR, 'Sheets')
PORTRAITS = os.path.join(ROOT, 'Assets', '2D', 'NPC', 'Nuevos Dialogos')
ITEMS = os.path.join(ROOT, 'Assets', '2D', 'UI', 'InventorySprites')
POSTERS = os.path.join(ROOT, 'Assets', '2D', 'Props', 'Poster')
SET_SCRIPT_META = os.path.join(ROOT, 'Assets', 'Scripts', 'Book', 'BookPageSet.cs.meta')
TEMPLATE_META = os.path.join(ROOT, 'Assets', '3D', 'Libros', 'Diario Kami', 'Texturas', 'Newspaper Textures',
                             '19951113Big.jpg.meta')

GEO = json.load(open(GEOMETRY))


def _half_aspect():
    """Width / height of a book half as the camera sees it: UV span times world units per UV on the flat part."""
    ratios = []
    for half in GEO['book'].values():
        pts = np.array([p for tri in half['tris'] for p in tri])
        A = np.hstack([pts[:, 2:4], np.ones((len(pts), 1))])
        coef, *_ = np.linalg.lstsq(A, pts[:, 0:2], rcond=None)
        u0, v0, u1, v1 = half['uv_rect']
        ratios.append((u1 - u0) * abs(coef[0, 0]) / ((v1 - v0) * abs(coef[1, 1])))
    return float(np.mean(ratios))


W = 2048
H = int(round(W / _half_aspect() / 4)) * 4   # multiples of 4 keep the DXT compression happy
BLEED = 220                                   # paper beyond the page, for the turning sheet's overhang
SHEET_W = 1024
MARGIN = 72
COLS = 6
GUTTER = 30
COL_W = (W - 2 * MARGIN - (COLS - 1) * GUTTER) / COLS
FOOTER = 150                                  # bottom band of a right page, where its number sits

INK = np.array([36, 30, 25], dtype=np.float32)
PAPER = np.array([226, 215, 192], dtype=np.float32)

FONT_DIR = r'C:\Windows\Fonts'
BODY, BODY_B, BODY_I = 'GARA.TTF', 'GARABD.TTF', 'GARAIT.TTF'
HEAD_BLACK, HEAD_COND, HEAD_I = 'BOD_BLAR.TTF', 'BOD_CB.TTF', 'BOD_BI.TTF'
MAST, CAPS, POSTER = 'OLDENGL.TTF', 'COPRGTB.TTF', 'PLAYBILL.TTF'
SLAB, CASTELLAR = 'ROCKEB.TTF', 'CASTELAR.TTF'

BODY_SIZE = 30
LEAD = 35
AD_ROW = 470                                  # height of the row of display advertisements at a page's foot

DATE = 'TUESDAY, OCTOBER 20'

_fonts = {}


def F(name, size):
    key = (name, int(size))
    if key not in _fonts:
        _fonts[key] = ImageFont.truetype(os.path.join(FONT_DIR, name), int(size))
    return _fonts[key]


def cx(c):
    return MARGIN + c * (COL_W + GUTTER)


def span(c0, c1):
    return cx(c0), cx(c1) + COL_W


# ---------------------------------------------------------------------------------------------
# The ink layer (L image, 255 = ink); paper and ageing come at the end
# ---------------------------------------------------------------------------------------------

class Sheet:
    def __init__(self, number=None, ads=None):
        self.ink = Image.new('L', (W, H), 0)
        self.d = ImageDraw.Draw(self.ink)
        self.briefs = BRIEF_STREAM  # one stream for the whole paper: no item is printed twice
        self.number = number
        self.page_bottom = H - MARGIN - (FOOTER if number else 60)
        self.bottom = self.page_bottom
        self.ad_row = None
        self.ads = ads
        if ads:
            self.reserve_ad_row()

    def reserve_ad_row(self):
        """Keeps the foot of the page for a row of advertisements (drawn by ad_row): the articles end above it."""
        self.ad_row = (self.bottom - AD_ROW, self.bottom)
        self.bottom = self.ad_row[0] - 34

    def text(self, xy, s, font, anchor='la', fill=255):
        self.d.text(xy, s, font=font, fill=fill, anchor=anchor)

    def hline(self, x0, x1, y, w=2):
        self.d.rectangle([x0, y, x1, y + w - 1], fill=255)

    def vline(self, x, y0, y1, w=2):
        self.d.rectangle([x, y0, x + w - 1, y1], fill=255)

    def box(self, x0, y0, x1, y1, w=3):
        self.d.rectangle([x0, y0, x1, y1], outline=255, width=w)

    def double_box(self, x0, y0, x1, y1, gap=9):
        self.box(x0, y0, x1, y1, 5)
        self.box(x0 + gap, y0 + gap, x1 - gap, y1 - gap, 2)

    def paste_ink(self, img, x, y):
        region = self.ink.crop((int(x), int(y), int(x) + img.width, int(y) + img.height))
        self.ink.paste(ImageChops.lighter(region, img), (int(x), int(y)))


def wrap(words, font, width):
    lines, cur = [], []
    for w in words:
        if cur and font.getlength(' '.join(cur + [w])) > width:
            lines.append(cur)
            cur = [w]
        else:
            cur = cur + [w]
    if cur:
        lines.append(cur)
    return lines


def draw_justified(sh, x, y, words, font, width, justify, indent=0):
    if not justify or len(words) == 1:
        sh.text((x + indent, y), ' '.join(words), font)
        return
    total = sum(font.getlength(w) for w in words)
    gap = (width - indent - total) / (len(words) - 1)
    if gap > font.size * 1.1:
        sh.text((x + indent, y), ' '.join(words), font)
        return
    xx = x + indent
    for w in words:
        sh.text((xx, y), w, font)
        xx += font.getlength(w) + gap


def centred(sh, x0, x1, y, text, font, size, leading=1.18, max_width=None):
    f = F(font, size)
    lines = wrap(text.split(), f, max_width or (x1 - x0))
    for i, l in enumerate(lines):
        sh.text(((x0 + x1) / 2, y + i * size * leading), ' '.join(l), f, anchor='ma')
    return y + len(lines) * size * leading


def fitted(sh, x0, x1, y, text, font, size, max_lines=2, leading=1.0, min_size=26):
    """Centred, shrunk until it fits in max_lines."""
    s = size
    while True:
        f = F(font, s)
        lines = wrap(text.split(), f, x1 - x0)
        if (len(lines) <= max_lines and all(f.getlength(' '.join(l)) <= x1 - x0 for l in lines)) or s <= min_size:
            break
        s -= 2
    for i, l in enumerate(lines):
        sh.text(((x0 + x1) / 2, y + i * s * leading), ' '.join(l), f, anchor='ma')
    return y + len(lines) * s * leading + s * 0.1


def short_rule(sh, x0, x1, y):
    w = (x1 - x0) * 0.22
    sh.hline((x0 + x1) / 2 - w / 2, (x0 + x1) / 2 + w / 2, y, 2)


def decks(sh, x0, x1, y, parts):
    """A 1940s headline: stacked decks of different weights with short rules between them.
    parts: (text, kind) with kind 'banner' | 'top' | 'deck' | 'caps' | 'sub'."""
    style = {'banner': (HEAD_BLACK, 130, 1), 'top': (HEAD_BLACK, 76, 3), 'deck': (HEAD_COND, 46, 3),
             'caps': (HEAD_COND, 38, 2), 'sub': (HEAD_I, 30, 3)}
    for i, (text, kind) in enumerate(parts):
        font, size, lines = style[kind]
        if i:
            short_rule(sh, x0, x1, y + 6)
            y += 20
        if kind == 'sub':
            y = centred(sh, x0, x1, y, text, font, size, 1.12, (x1 - x0) * 0.92) + 6
        else:
            y = fitted(sh, x0, x1, y, text, font, size, lines)
    return y + 10


def rule_ornament(sh, x0, x1, y):
    mid = (x0 + x1) / 2
    sh.hline(x0 + 30, mid - 26, y, 2)
    sh.hline(mid + 26, x1 - 30, y, 2)
    sh.d.polygon([(mid, y - 9), (mid + 10, y + 1), (mid, y + 11), (mid - 10, y + 1)], fill=255)


# ---------------------------------------------------------------------------------------------
# Body text that flows down columns, topped up with short news items
# ---------------------------------------------------------------------------------------------

class Line:
    def __init__(self, h, draw, keep=0):
        self.h, self.draw, self.keep = h, draw, keep


def paragraph_lines(text, width, font_name=BODY, indent=True, justify=True):
    f = F(font_name, BODY_SIZE)
    ind = BODY_SIZE * 1.3 if indent else 0
    lines, cur, first = [], [], True
    for w in text.split():
        avail = width - (ind if first else 0)
        if cur and f.getlength(' '.join(cur + [w])) > avail:
            lines.append((cur, first))
            cur, first = [w], False
        else:
            cur = cur + [w]
    if cur:
        lines.append((cur, first))
    return [Line(LEAD, lambda sh, x, y, ws=ws, fi=fi, last=(i == len(lines) - 1):
                 draw_justified(sh, x, y, ws, f, width, justify and not last, ind if fi else 0))
            for i, (ws, fi) in enumerate(lines)]


def head_lines(head, width, font=HEAD_COND, size=29, gap=34):
    f = F(font, size)
    heads = wrap(head.split(), f, width)
    out = [Line(22, lambda sh, x, y: sh.hline(x + width * 0.32, x + width * 0.68, y + 6, 2))]
    for i, l in enumerate(heads):
        out.append(Line(gap, lambda sh, x, y, l=l: sh.text((x + width / 2, y), ' '.join(l), f, anchor='ma'),
                        keep=len(heads) - i + 1))
    out.append(Line(6, lambda sh, x, y: None))
    return out


def item_lines(head, text, width):
    return head_lines(head, width) + paragraph_lines(text, width, indent=False)


def flow(sh, rects, paragraphs, fill=True, items=None):
    """Pours paragraphs down rects (all the same width). items: more (head, text) pieces to pour after the
    article, before the page's pool of short news; fill=False leaves the rest of the space empty."""
    width = rects[0][2] - rects[0][0]
    queue = []
    for p in paragraphs:
        queue += paragraph_lines(p, width)
    article = len(queue)       # lines still owed to the article itself (never left half-printed silently)
    extra = iter(items or [])
    y = rects[0][1]
    for ri, (x0, y0, x1, y1) in enumerate(rects):
        y = y0
        # room left from here to the end of the last column (a column break costs a line or two, hence the margin)
        room = lambda: (y1 - y) + sum(r[3] - r[1] for r in rects[ri + 1:]) - LEAD * 2 * (len(rects) - 1 - ri)
        while True:
            if not queue:
                piece, from_pool = next(extra, None), False
                if piece is None and fill:
                    piece, from_pool = sh.briefs.take(), True
                if piece is None:
                    return y
                lines = item_lines(*piece, width)
                # a short item may run on into the next column, but never stops half-way at the very end
                if sum(l.h for l in lines) > room():
                    if not from_pool:
                        print(f'[make-level2-newspaper-pages] WARNING: "{piece[0]}" did not fit and was left out')
                        return y
                    sh.briefs.give_back(piece)
                    space = room()
                    piece = sh.briefs.take_where(lambda p: sum(l.h for l in item_lines(*p, width)) <= space)
                    if piece is None:
                        return y
                    lines = item_lines(*piece, width)
                queue = lines
            ln = queue[0]
            if y == y0 and ln.h < LEAD and ln.keep == 0 and len(queue) > 1 and queue[1].keep:
                queue.pop(0)
                continue
            if y + sum(l.h for l in queue[:1 + ln.keep]) > y1:
                break
            ln.draw(sh, x0, y)
            y += ln.h
            queue.pop(0)
            article -= 1
    if article > 0:
        print(f'[make-level2-newspaper-pages] WARNING: an article was cut short ({article} lines did not fit)')
    elif queue:
        print('[make-level2-newspaper-pages] WARNING: a short item was cut at the end of its columns')
    return y


def fill_under(sh, x0, x1, y0, y1):
    """Pours short news into the room left under a box that closed early (x0..x1 on the column grid)."""
    if y1 - y0 < LEAD * 4:
        return
    c0 = [c for c in range(COLS) if abs(cx(c) - x0) < 2]
    c1 = [c for c in range(COLS) if abs(cx(c) + COL_W - x1) < 2]
    if not c0 or not c1:
        return
    column_rules(sh, c0[0], c1[0], y0, y1)
    flow(sh, col_rects(c0[0], c1[0], y0, y1), [])


def col_rects(c0, c1, y0, y1):
    return [(cx(c), y0, cx(c) + COL_W, y1) for c in range(c0, c1 + 1)]


def column_rules(sh, c0, c1, y0, y1):
    for c in range(c0, c1):
        sh.vline(cx(c) + COL_W + GUTTER / 2 - 1, y0, y1, 2)


def row_rule(sh, y):
    sh.hline(MARGIN, W - MARGIN, y - 17, 3)


def entry_lines(lead, text, width):
    fb, f = F(BODY_B, BODY_SIZE), F(BODY, BODY_SIZE)
    lead_w = fb.getlength(lead + ' ') if lead else 0
    words = text.split()
    first = wrap(words, f, width - lead_w)[0] if words else []
    if first and f.getlength(' '.join(first)) > width - lead_w:
        first = []  # a lead too long to share its line with the text
    rest = words[len(first):]

    def draw_first(sh, x, y):
        if lead:
            sh.text((x, y), lead, fb)
        draw_justified(sh, x + lead_w, y, first, f, width - lead_w, bool(rest))

    out = [Line(LEAD, draw_first)]
    lines = wrap(rest, f, width)
    out += [Line(LEAD, lambda sh, x, y, l=l, last=(i == len(lines) - 1): draw_justified(sh, x, y, l, f, width, not last))
            for i, l in enumerate(lines)]
    out.append(Line(10, lambda sh, x, y: None))
    return out


def notices(sh, x0, y0, x1, y1, title, sections, cols=2, title_size=56, fill=True):
    """A boxed list (classifieds, programmes, blotter): sections of (heading or None, [(lead, text)]), in balanced
    columns. The box closes under its last line (y1 is the most it may take); returns the box's bottom."""
    top = y0 + 30 + title_size
    cw = (x1 - x0 - 40 - (cols - 1) * 24) / cols
    rects = [(x0 + 20 + c * (cw + 24), top + 16, x0 + 20 + c * (cw + 24) + cw, y1 - 16) for c in range(cols)]
    queue = []
    for heading, entries in sections:
        if heading:
            queue += head_lines(heading, cw, CAPS, 22, 30)
        for lead, text in entries:
            queue += entry_lines(lead, text, cw)
    share = sum(l.h for l in queue) / cols + LEAD
    placed, lowest = [], top + 16
    for i, (rx0, ry0, rx1, ry1) in enumerate(rects):
        y = ry0
        bottom = ry1 if i == len(rects) - 1 else min(ry1, ry0 + share)
        while queue:
            ln = queue[0]
            if y == ry0 and ln.h < LEAD and ln.keep == 0 and len(queue) > 1 and queue[1].keep:
                queue.pop(0)
                continue
            if y + sum(l.h for l in queue[:1 + ln.keep]) > bottom:
                break
            placed.append((ln, rx0, y))
            y += ln.h
            queue.pop(0)
        lowest = max(lowest, y)
    if queue:
        print(f'[make-level2-newspaper-pages] WARNING: the "{title}" box is too small, {len(queue)} lines left out')
    box_bottom = min(y1, lowest + 18)
    sh.box(x0, y0, x1, box_bottom, 4)
    sh.text(((x0 + x1) / 2, y0 + 16), title, F(HEAD_BLACK, title_size), anchor='ma')
    sh.hline(x0 + 20, x1 - 20, top, 3)
    for c in range(cols - 1):
        sh.vline(rects[c][2] + 11, top + 16, box_bottom - 20, 2)
    for ln, x, y in placed:
        ln.draw(sh, x, y)
    if fill:
        fill_under(sh, x0, x1, box_bottom + 30, y1)
    return box_bottom


def ad_row(sh, ads):
    """The row of display advertisements kept by reserve_ad_row: ads = [((c0, c1), lines)]."""
    y0, y1 = sh.ad_row
    row_rule(sh, y0)
    for (c0, c1), lines in ads:
        x0, x1 = span(c0, c1)
        ad(sh, x0, y0, x1, y1, lines, shrink=False)


def ad(sh, x0, y0, x1, y1, lines, picture=None, shrink=True):
    """A boxed display advertisement: centred lines of (text, font, size), optionally under a picture (path,
    height), printed at its own proportions. The box closes under its content (y1 is the most it may take);
    returns the box's bottom."""
    ix0, iy0, ix1 = x0 + 26, y0 + 26, x1 - 26
    gap = 22
    blocks = []
    for text, font, size in lines:
        if text == '---':
            blocks.append(('rule', 26))
        else:
            f = F(font, size)
            blocks.append(('text', (wrap(text.split(), f, ix1 - ix0), f, size)))
    natural = sum(b if k == 'rule' else len(b[0]) * b[2] * 1.15 for k, b in blocks) + gap * (len(blocks) + 1)
    y = iy0
    if picture:
        p, ph = picture
        im = Image.open(p)
        pw = min(int(ix1 - ix0), int(ph * im.width / im.height))
        ph = int(pw * im.height / im.width)
        sh.paste_ink(halftone(p, pw, ph, (0, 0, 1, 1), period=6.0, contrast=1.25), (ix0 + ix1 - pw) / 2, y)
        y += ph + 10
    if y + natural + 26 > y1:
        print(f'[make-level2-newspaper-pages] WARNING: an advertisement is crowded ({lines[0][0]})')
    if shrink:
        bottom = min(y1, y + natural + 26)
    else:
        bottom = y1
        y += max(0, (y1 - 26 - y - natural) / 2)  # a fixed-size ad keeps its copy in the middle
    sh.double_box(x0, y0, x1, bottom)
    y += gap
    for kind, b in blocks:
        if kind == 'rule':
            rule_ornament(sh, ix0 + 30, ix1 - 30, y + 10)
            y += b + gap
            continue
        wrapped, f, size = b
        for l in wrapped:
            sh.text(((ix0 + ix1) / 2, y), ' '.join(l), f, anchor='ma')
            y += size * 1.15
        y += gap
    if shrink:
        fill_under(sh, x0, x1, bottom + 30, y1)
    return bottom


# ---------------------------------------------------------------------------------------------
# Pictures: game art printed as halftone
# ---------------------------------------------------------------------------------------------

def halftone(path, w, h, crop, period=7.0, contrast=1.35, lift=0.0, backdrop=(214, 236)):
    im = Image.open(path).convert('RGBA')
    iw, ih = im.size
    grad = np.linspace(backdrop[0], backdrop[1], ih, dtype=np.float32)[:, None].repeat(iw, 1)
    bg = Image.fromarray(np.dstack([grad, grad, grad, np.full_like(grad, 255)]).astype(np.uint8), 'RGBA')
    bg.alpha_composite(im)
    pad = max(iw, ih) // 2
    g = Image.new('L', (iw + 2 * pad, ih + 2 * pad), backdrop[0])
    g.paste(bg.convert('L'), (pad, pad))
    x0, y0, x1, y1 = crop[0] * iw, crop[1] * ih, crop[2] * iw, crop[3] * ih
    mx, my, cw, ch = (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0
    if cw / ch < w / h:
        cw = ch * w / h
    else:
        ch = cw * h / w
    g = g.crop((int(mx - cw / 2) + pad, int(my - ch / 2) + pad, int(mx + cw / 2) + pad, int(my + ch / 2) + pad))
    s = 2
    a = np.asarray(g.resize((w * s, h * s), Image.LANCZOS), dtype=np.float32) / 255.0
    dark = np.clip((1.0 - a - 0.5) * contrast + 0.5 + lift, 0, 1)
    yy, xx = np.mgrid[0:h * s, 0:w * s].astype(np.float32)
    th, p = math.radians(45), period * s
    u = (xx * math.cos(th) + yy * math.sin(th)) / p
    v = (-xx * math.sin(th) + yy * math.cos(th)) / p
    screen = (2.0 - (np.cos(2 * np.pi * u) + np.cos(2 * np.pi * v))) / 4.0
    return Image.fromarray(((dark > screen) * 255).astype(np.uint8), 'L').resize((w, h), Image.LANCZOS)


def photo(sh, x0, y0, x1, y1, path, crop, caption=None, **kw):
    w, h = int(x1 - x0), int(y1 - y0)
    sh.paste_ink(halftone(path, w, h, crop, **kw), x0, y0)
    sh.box(x0, y0, x0 + w, y0 + h, 3)
    y = y0 + h + 10
    if caption:
        f = F(BODY_I, 23)
        for l in wrap(caption.split(), f, w):
            sh.text((x0, y), ' '.join(l), f)
            y += 26
    return y + 12


def portrait(name):
    return os.path.join(PORTRAITS, name)


def poster(n):
    return os.path.join(POSTERS, f'Poster {n}.png')


# ---------------------------------------------------------------------------------------------
# Page furniture
# ---------------------------------------------------------------------------------------------

def ear(sh, x0, x1, y0, y1, title, text):
    sh.box(x0, y0, x1, y1, 3)
    sh.text(((x0 + x1) / 2, y0 + 16), title, F(CAPS, 24), anchor='ma')
    sh.hline(x0 + 40, x1 - 40, y0 + 52, 2)
    centred(sh, x0 + 16, x1 - 16, y0 + 66, text, BODY_I, 25, 1.12)


def front_masthead(sh, left, right):
    top = MARGIN
    ear_w = 330
    ear(sh, MARGIN, MARGIN + ear_w, top + 20, top + 220, *left)
    ear(sh, W - MARGIN - ear_w, W - MARGIN, top + 20, top + 220, *right)
    title = 'The Daily Ink'
    room = W - 2 * MARGIN - 2 * ear_w - 70
    s = 300
    while F(MAST, s).getlength(title) > room:
        s -= 4
    sh.text((W / 2, top + 135), title, F(MAST, s), anchor='mm')
    y = top + 262
    sh.hline(MARGIN, W - MARGIN, y, 6)
    sh.hline(MARGIN, W - MARGIN, y + 11, 2)
    f = F(CAPS, 25)
    sh.text((MARGIN + 6, y + 24), 'VOL. XII.  No. 47', f)
    sh.text((W / 2, y + 24), DATE, f, anchor='ma')
    sh.text((W - MARGIN - 6, y + 24), 'CITY EDITION', f, anchor='ra')
    y += 62
    sh.hline(MARGIN, W - MARGIN, y, 2)
    sh.hline(MARGIN, W - MARGIN, y + 7, 5)
    return y + 30


def inner_header(sh, section):
    """The running head of an inner page: the paper's name, the date and the section."""
    top = MARGIN
    f = F(CAPS, 24)
    sh.text((MARGIN + 4, top + 34), DATE, f)
    sh.text((W - MARGIN - 4, top + 34), 'CITY EDITION', f, anchor='ra')
    sh.text((W / 2, top + 4), 'The Daily Ink', F(MAST, 88), anchor='ma')
    y = top + 108
    sh.hline(MARGIN, W - MARGIN, y, 5)
    sh.hline(MARGIN, W - MARGIN, y + 10, 2)
    sh.text((W / 2, y + 26), section, F(CASTELLAR, 52), anchor='ma')
    y += 96
    sh.hline(MARGIN, W - MARGIN, y, 2)
    sh.hline(MARGIN, W - MARGIN, y + 7, 4)
    return y + 30


def footer(sh):
    """The row of advertisements if the page has one, a closing rule, and on a right half the level page number
    in the lower outer corner."""
    if sh.ad_row:
        ad_row(sh, sh.ads)
    y = sh.page_bottom + 22
    sh.hline(MARGIN, W - MARGIN, y, 3)
    if sh.number:
        f = F(HEAD_BLACK, 104)
        label = str(sh.number)
        bx1, by1 = W - MARGIN, H - MARGIN
        bx0, by0 = bx1 - f.getlength(label) - 64, y + 18
        sh.box(bx0, by0, bx1, by1, 4)
        sh.text(((bx0 + bx1) / 2, (by0 + by1) / 2 + 4), label, f, anchor='mm')
        sh.text((MARGIN, y + 22), f'THE DAILY INK  \u2022  {DATE}', F(CAPS, 22))


# ---------------------------------------------------------------------------------------------
# Short news, poured into whatever column space an article leaves. One pool for the whole paper; each page
# starts at a different item and never repeats one.
# ---------------------------------------------------------------------------------------------

BRIEFS = [
    ('PAPER COLLECTION THIS WEEK', 'The municipal paper collection will be made in all districts on Thursday. '
     'Householders are asked to tie their old newspapers in bundles and to leave them at the kerb before eight '
     'o\'clock in the morning. Every scrap is wanted.'),
    ('PRICE OF COFFEE ADVANCES', 'The price of a cup of coffee in the cafes of the city advanced again this week, '
     'the third increase since the spring. Several patrons, among them an elderly lady of the old quarter, '
     'expressed their opinion of the change in no uncertain terms.'),
    ('NEW REFUSE BINS', 'Fifteen refuse bins with hinged lids have been placed along the principal streets by the '
     'sanitation department. Citizens are asked to use them for refuse only.'),
    ('EVENING BULLETINS EXTENDED', 'City Radio announces that its evening bulletins will be extended this week, '
     'with special reports upon the museum thefts as further information is received.'),
    ('CAT RECOVERED', 'A tabby cat belonging to Mrs. Ferreyra of Elm Street, missing for a week, was found yesterday '
     'asleep in a refuse bin two doors from its home. It was returned to its owner none the worse.'),
    ('TRAMWAY SCHEDULE ALTERED', 'The Number 7 tram will henceforward stop at the museum, the cafes of the avenue and '
     'the old quarter. The company regrets that, owing to the present traffic, it cannot keep to the times '
     'printed in its schedule.'),
    ('CHESS CLUB MEETS', 'The Tuesday Chess Club will meet at six o\'clock this evening at the cafe on the avenue. '
     'New members will be made welcome.'),
    ('DESK LAMPS SCARCE', 'Stationers report an unusual demand for blue desk lamps, which are at present not to be had '
     'anywhere in the city. No explanation of the demand has been offered.'),
    ('KEYS FOUND', 'A ring of large brass keys was found yesterday in a flower box near the museum gates. It has been '
     'handed in at the Central Station, where it awaits its owner.'),
    ('UMBRELLAS MISSING', 'Three umbrellas have disappeared from the stand of a cafe on the avenue during the past '
     'week. The proprietor does not believe the matter to be connected with the museum.'),
    ('PIGEONS IN MUSEUM YARD', 'Complaint has been made that the pigeons of the museum yard have learned to lift the '
     'lids of the new refuse bins. The matter is under consideration by the council.'),
    ('ADVERTISING BILLS', 'The council has received a petition from residents of the old quarter protesting against '
     'the number of advertising bills now posted upon the walls of their streets.'),
    ('STREET LAMPS OUT', 'Residents of Elm Street report that half of the street lamps of that thoroughfare have been '
     'out of order for a fortnight. The lamp department promises early attention.'),
    ('OPERA POSTPONED', 'This evening\'s performance at the Opera House has been postponed owing to the indisposition '
     'of the principal tenor. Tickets will be honoured on Friday.'),
    ('MARKET PRICES', 'Lemons were plentiful and cheap at the central market yesterday. Cocoa was dearer, and paper of '
     'every description remained very scarce.'),
    ('AN OVERDUE BOOK', 'The Public Library reminds readers that books must be returned within fourteen days. One '
     'volume, borrowed in the year the library was opened, has still not been returned.'),
    ('NEW BAKERY OPENS', 'A bakery has opened its doors opposite the Central Station. Its medialunas are already well '
     'spoken of by the officers of the night watch.'),
    ('RADIO QUIZ', 'Last evening\'s radio quiz was won by an elderly lady who answered every question correctly. She '
     'has since written to the station to say that the prize was inadequate.'),
    ('SPELLING CONTEST', 'The schools\' spelling contest was won yesterday by a girl of the third form, who spelled '
     '"investigation" without hesitation and announced her intention of becoming a detective.'),
    ('BRIDGE REPAINTED', 'The repainting of the iron bridge has been completed. The bridge has been painted grey, '
     'as before.'),
    ('STRAYED CHILD FOUND', 'A small boy who strayed from his mother in the central market yesterday was found an hour '
     'later by an officer, seated upon the steps of the museum and reading a newspaper.'),
    ('SCISSORS GRINDER RETURNS', 'The scissors grinder of the old quarter has resumed his rounds after an illness, '
     'and may be heard every morning upon the avenue.'),
    ('NEW POSTING BOX', 'A posting box has been placed at the corner of the avenue and Elm Street. Letters are '
     'collected at nine, at noon and at five.'),
    ('FIRE BRIGADE CALLED', 'The fire brigade was called to a cafe on the avenue yesterday morning, where a coffee '
     'urn had boiled over. No damage was done.'),
    ('MUSEUM GUIDES WANTED', 'The City Museum invites persons of good education to assist in guiding visitors '
     'through the galleries on Saturdays. Applications should be addressed to the director.'),
    ('KITE FLYING FORBIDDEN', 'Boys flying paper kites from the roof of the old mill were warned by an officer '
     'yesterday that the practice is dangerous and must cease.'),
    ('LAMPLIGHTERS\' PAY', 'The council has agreed to an increase in the wages of the city lamplighters, who have '
     'complained that the new electric lamps make their work no easier.'),
    ('A LATE FROST', 'Gardeners are warned that a late frost may be expected towards the end of the week. Tender '
     'plants should be covered at night.'),
    ('MUSEUM LECTURE', 'A lecture on "The Modern School of Painting" will be given in the lecture room of the City '
     'Museum on Thursday at six o\'clock. Admission is free.'),
    ('NEW TELEPHONE NUMBERS', 'Subscribers in the old quarter are reminded that their telephone numbers will be '
     'altered on the first of next month. New directories may be had at the exchange.'),
    ('NEW SCHOOL OPENED', 'The new elementary school of the northern district was opened by the Mayor on Monday. It '
     'has eight classrooms, a library and a yard for games.'),
    ('HORSE BOLTS ON AVENUE', 'A milk-cart horse bolted upon the avenue early yesterday morning and was stopped by a '
     'passing officer outside the museum. Several bottles were broken.'),
    ('BAND IN THE PLAZA', 'The City Band will give a concert in the plaza on Saturday evening at seven o\'clock, '
     'weather permitting. The programme includes several marches and a tango.'),
    ('GARDEN PRIZE AWARDED', 'The prize for the best kept front garden of the old quarter has been awarded to Mrs. '
     'Ortega, whose roses were much admired by the judges.'),
    ('RIVER RISING', 'The river rose some two feet during the night following the rains in the hills. The harbour '
     'master does not anticipate any danger.'),
    ('LECTURE ON THE STARS', 'A lecture upon the stars to be seen in the autumn sky will be given at the Public '
     'Library on Friday evening. Those attending are advised to bring warm coats.'),
    ('IMPROVED GAS LAMPS', 'The gas company is replacing the lamps of the lower avenue with lamps of an improved '
     'pattern, which are said to give twice the light.'),
    ('BOOKBINDERS MEET', 'The Guild of Bookbinders held its annual meeting on Monday and elected officers for the '
     'coming year. The shortage of good paper was the principal subject of discussion.'),
    ('HOSPITAL CONCERT', 'A concert in aid of the children\'s hospital will be given at the Royal Theatre on Sunday '
     'afternoon. Tickets may be had at the box office.'),
    ('THE TOWN HALL CLOCK', 'The clock of the town hall, which had lately been running eleven minutes slow, has been '
     'put right. Citizens who set their watches by it are asked to do so again.'),
    ('PARROT AT LIBERTY', 'A grey parrot which escaped on Sunday from a house in Elm Street has been seen in the trees '
     'of the plaza. Its owner offers a small reward for its return.'),
    ('FERRY SERVICE', 'A ferry service across the river will begin on the first of the month, with crossings every '
     'half hour from seven o\'clock in the morning.'),
    ('BAKERS TO OPEN EARLIER', 'The bakers of the city have agreed to open their shops at six o\'clock in the morning '
     'instead of seven, for the convenience of workers.'),
    ('TRAM LEAVES RAILS', 'A tram of the Number 4 line left the rails at the corner of the market yesterday afternoon. '
     'No person was hurt, and the service was restored within the hour.'),
    ('PRICE OF INK', 'Printers of the city have expressed concern at the rising price of printing ink. This newspaper '
     'assures its readers that it will continue to appear as usual.'),
    ('NEW DOCTOR', 'Dr. Molina has taken over the practice of the late Dr. Ruiz in the old quarter, and will see '
     'patients daily from nine o\'clock until noon.'),
    ('STREET CLEANING', 'The streets of the old quarter will be swept on Wednesday morning. Residents are asked to move '
     'their motor cars before eight o\'clock.'),
    ('SCHOOL CHOIR', 'The choir of the girls\' school sang at the cathedral on Sunday and was warmly praised. A '
     'collection was taken for new hymn books.'),
    ('BICYCLE STOLEN', 'A bicycle left outside a cafe on the avenue on Monday evening was taken while its owner was '
     'inside. The police do not connect the matter with the museum.'),
    ('IN THE HARBOUR', 'Three steamers entered the harbour yesterday with cargoes of timber, coffee beans and paper '
     'pulp.'),
    ('PUBLIC BATHS', 'The public baths will close for the winter at the end of the month, and will reopen in the '
     'spring.'),
    ('DOG SHOW', 'The annual dog show will be held in the market hall on Saturday. There will be prizes in eleven '
     'classes, among them one for the best-behaved dog.'),
    ('NEW BOOKSHOP', 'A bookshop has opened in the arcade off the avenue. The proprietor states that he will buy old '
     'books as well as sell new ones.'),
    ('PIANO TUNER RETURNS', 'The piano tuner of the old quarter announces that he has returned from the provinces and '
     'is ready to receive orders.'),
    ('AUTUMN FAIR', 'The autumn fair will be held upon the common next week. There will be roundabouts, a brass band '
     'and a competition for the largest pumpkin.'),
    ('POLICE BAND', 'The band of the Central Police Station will play in the plaza on Sunday afternoon.'),
    ('WRITING PAPER RATIONED', 'The stationers of the avenue regret that, owing to the shortage, writing paper can be '
     'sold to each customer in small quantities only.'),
    ('SNOW IN THE HILLS', 'Snow has fallen in the hills to the north, a fortnight earlier than usual. The weather office '
     'expects a hard winter.'),
    ('STREET NAMES', 'Several streets of the new district are to be named after the founders of the City Museum, the '
     'council decided on Monday.'),
    ('THE OLD MILL', 'The old mill by the river, empty for many years, has been bought by a firm of paper makers, who '
     'intend to set it working again.'),
    ('NEW BENCHES IN THE PLAZA', 'Six new benches have been placed in the plaza by the parks department, two of them '
     'beneath the plane trees on the north side.'),
    ('TRAMWAYMEN\'S DANCE', 'The annual dance of the tramwaymen and their families will be held in the depot hall on '
     'Saturday evening.'),
    ('BOAT RECOVERED', 'A rowing boat which broke from its moorings during the night was recovered yesterday a mile '
     'below the iron bridge.'),
    ('CANDLES SCARCE', 'Grocers of the old quarter report that candles are in short supply, many households having '
     'laid in a store against the failure of the street lamps.'),
    ('WEDDING AT THE CATHEDRAL', 'The marriage took place at the cathedral on Saturday of Miss Elena Rios and Mr. Pablo '
     'Duarte. The bride wore her mother\'s veil.'),
    ('FOG UPON THE RIVER', 'A thick fog lay upon the river yesterday morning, and the first ferry was delayed for an '
     'hour.'),
    ('MARKET HALL REPAIRS', 'The roof of the market hall is to be repaired before the winter. The stallholders will be '
     'moved to the yard while the work is in progress.'),
    ('POETRY RECITAL', 'A recital of his own verses was given by a young poet of the old quarter at the Public Library '
     'on Monday evening, before an attentive audience.'),
    ('NEW TRAFFIC SIGNS', 'Signs bearing the word STOP have been placed at four crossings of the avenue. It is hoped '
     'that motorists will observe them.'),
    ('SATCHEL LOST', 'A brown leather school satchel containing exercise books was lost upon the Number 7 tram on Monday. '
     'The finder is asked to return it to this office.'),
    ('PHOTOGRAPHIC SOCIETY', 'The Photographic Society will hold its autumn exhibition in the lecture room of the museum '
     'from Monday next. Members are invited to send in their work.'),
    ('SCHOOLBOYS\' FOOTBALL', 'The boys of the northern school beat the boys of the old quarter by three goals to nil '
     'on Saturday morning, in spite of the rain.'),
    ('NEW CAFE OPENED', 'A new cafe of the Big C company was opened in Elm Street yesterday. It is the eleventh of the '
     'company\'s establishments in the city.'),
    ('PIGEON RACE', 'The pigeon race from the coast was won by a bird belonging to a member of the harbour club, which '
     'completed the flight in a little under three hours.'),
    ('ORGAN RECITAL', 'An organ recital will be given at the cathedral on Wednesday evening at seven. The programme is '
     'of music for the autumn.'),
    ('CHIMNEY FIRE', 'A chimney caught fire at a house in the old quarter on Monday night. It was put out by the '
     'occupants before the brigade arrived.'),
    ('EVENING CLASSES', 'Evening classes in shorthand, typewriting and book-keeping will begin at the commercial school '
     'on Monday. Enrolment may be made at the school.'),
    ('A SECOND BRIDGE', 'The council has received plans for a second bridge across the river, to be built of stone. '
     'No decision has yet been taken.'),
    ('GIFT TO THE LIBRARY', 'An anonymous gift of one hundred books has been received by the Public Library. The books '
     'were left upon the library steps in a wooden crate.'),
    ('CITY ACCOUNTS', 'The city treasurer reports that the accounts of the past year show a small surplus, the first '
     'for some years.'),
    ('HARVEST FESTIVAL', 'The harvest festival will be kept at the parish church on Sunday. The gifts of fruit and '
     'vegetables will afterwards be sent to the hospital.'),
    ('TELEGRAPH WIRES DOWN', 'High winds on Sunday brought down the telegraph wires along the coast road. Messages are '
     'being sent by way of the hills until repairs are made.'),
    ('MOTOR CAR IN FOUNTAIN', 'A motor car whose brakes had failed came to rest in the fountain of the plaza yesterday '
     'afternoon. The driver was unhurt.'),
    ('SCOUTS COLLECT PAPER', 'The Boy Scouts of the old quarter collected more than two tons of old paper on Saturday, '
     'which will be sold in aid of their summer camp.'),
    ('LAMPS FOR THE RIVER WALK', 'Electric lamps are to be placed along the river walk before the end of the year, the '
     'council has decided.'),
    ('THE MAILS', 'The mail steamer from the south is expected on Thursday. Letters for the south must be posted by '
     'noon on Wednesday.'),
    ('CLOCKMAKERS\' NOTICE', 'The clockmakers of the avenue announce that, owing to the press of work, repairs cannot be '
     'undertaken in less than a fortnight.'),
    ('SKATING RINK', 'The skating rink at the Avenue Hall will open for the season on Saturday next. Skates may be '
     'hired at the door.'),
    ('A DOLL FOUND', 'A small doll made of paper, with blue hair, was found upon the steps of the cathedral on Monday. '
     'It may be claimed at the Central Station.'),
    ('MOUNTAIN ROAD CLOSED', 'Motorists travelling to the hills are warned that the mountain road is closed above the '
     'second bridge owing to a fall of rock.'),
    ('ORPHANS\' OUTING', 'The children of the city orphanage were taken for an outing to the coast on Sunday by the '
     'members of the Rotary Club.'),
    ('MOTOR OMNIBUSES', 'The tramway company is to try two motor omnibuses upon the route to the northern district. If '
     'they prove satisfactory, more will be bought.'),
    ('POLICE PENSIONS', 'The council has approved an increase in the pensions of retired police officers, to take '
     'effect from the first of next month.'),
    ('TENORS WANTED', 'The City Choir is in need of tenors. Gentlemen who can sing are invited to attend a rehearsal on '
     'Thursday evening at the town hall.'),
    ('MUSIC HALL LICENCE', 'The licence of The Low Note music hall was renewed by the magistrates on Monday, upon the '
     'understanding that the music will cease at two o\'clock.'),
    ('BEES ON THE AVENUE', 'A swarm of bees settled upon a lamp post on the avenue yesterday, and was removed by a '
     'beekeeper from the northern district.'),
    ('FLOWER SHOW', 'The autumn flower show will open in the market hall on Friday. The chrysanthemums are expected to '
     'be the feature of the show.'),
    ('THE OBSERVATORY', 'The observatory on the hill will be open to visitors on Saturday evening, should the sky be '
     'clear.'),
    ('LIBRARY HOURS', 'The Public Library will remain open until nine o\'clock in the evening during the winter months.'),
    ('PAPER BOATS', 'Children sailing paper boats upon the fountain of the plaza have been asked by the park keepers to '
     'collect them when they have finished.'),
    ('CARRIAGE OVERTURNED', 'A horse-drawn carriage overturned on the hill road on Sunday. The passengers were shaken '
     'but not injured.'),
    ('FIRE BRIGADE DISPLAY', 'The fire brigade will hold its annual display in the plaza on Saturday afternoon, with a '
     'demonstration of the new ladder.'),
    ('COFFEE LANDED', 'A shipment of coffee beans for the Big C company was landed at the harbour yesterday, the '
     'largest ever received at the port.'),
    ('NIGHT SCHOOL', 'The night school for working men will reopen on Monday, with classes in reading, writing and '
     'arithmetic.'),
    ('COAL DELIVERIES', 'The coal merchants announce that winter deliveries will begin on Monday. Orders should be '
     'placed early.'),
    ('NEW SIGNAL BOX', 'A new signal box has been built at the junction north of the city, and the trains of the coast '
     'line are expected to run more punctually in consequence.'),
    ('THE MUNICIPAL GARDENS', 'A pair of llamas has arrived at the municipal gardens from the hills, and may be seen by '
     'the public from Saturday.'),
    ('CYCLISTS WARNED', 'Cyclists are reminded that it is an offence to ride upon the pavements of the avenue. Several '
     'riders were cautioned yesterday.'),
    ('HOSPITAL WING', 'The new children\'s wing of the city hospital will be opened next month. It has forty beds and a '
     'garden.'),
    ('AUCTION OF FURNITURE', 'The furniture of the late Mr. Ruiz will be sold by auction at his house in the old quarter '
     'on Thursday, at eleven o\'clock.'),
    ('ESSAY PRIZE', 'The prize for the best essay upon "The History of Our City" has been awarded to a pupil of the '
     'girls\' school.'),
    ('BREAD PRICES STEADY', 'The bakers\' association states that the price of bread will remain unchanged for the '
     'present, in spite of the rising cost of flour.'),
    ('NEW DRINKING FOUNTAIN', 'A drinking fountain has been placed at the entrance to the museum yard, the gift of an '
     'anonymous benefactor.'),
    ('A LOST PURSE', 'A lady\'s purse, black, containing a small sum and a tram ticket, was lost upon the avenue on '
     'Monday. Reward on return to this office.'),
    ('THE LIGHTHOUSE', 'The light of the harbour lighthouse will be extinguished for two nights next week while the lamp '
     'is cleaned.'),
    ('TUG OF WAR', 'The police team won the tug of war against the fire brigade at the sports meeting on Saturday, after '
     'a contest lasting four minutes.'),
    ('TYPEWRITING CONTEST', 'The typewriting contest held at the commercial school on Friday was won by a young lady who '
     'typed seventy words in the minute without an error.'),
    ('WINTER COATS', 'The drapers of the avenue report a brisk trade in winter coats, the cold having come earlier than '
     'usual.'),
    ('THE PLANE TREES', 'The plane trees of the plaza are to be pruned next week. The work will be done in the early '
     'morning, so as not to inconvenience the public.'),
]

class BriefStream:
    """The short news in order, each handed out once; an item that did not fit goes back for the next space."""

    def __init__(self, items):
        self.items = list(items)
        self.used = 0
        self.ran_out = 0

    def take(self):
        if not self.items:
            self.ran_out += 1
            return None
        self.used += 1
        return self.items.pop(0)

    def give_back(self, item):
        self.used -= 1
        self.items.insert(0, item)

    def take_where(self, fits, look=10):
        """The first of the next few items that fits (a shorter one for the last lines of a column)."""
        for i, item in enumerate(self.items[:look]):
            if fits(item):
                self.used += 1
                return self.items.pop(i)
        return None


BRIEF_STREAM = BriefStream(BRIEFS)


# The row of display advertisements at the foot of each page: ((first column, last column), lines).
AD_ROWS = {
    'p1l': [
        ((0, 1), [('ROSA\'S', POSTER, 84), ('MEDIALUNAS', CAPS, 24), ('Baked fresh every morning', HEAD_I, 30),
                  ('Corner of the Avenue and Elm Street', BODY, 26)]),
        ((2, 3), [('VILLAR & SON', SLAB, 52), ('LOCKSMITHS', CAPS, 24), ('---', None, 0),
                  ('Keys cut while you wait. Locks of every pattern fitted and repaired.', BODY, 26)]),
        ((4, 5), [('THE GRAND HOTEL', HEAD_BLACK, 46), ('---', None, 0), ('Rooms by the day or by the week', HEAD_I, 30),
                  ('Tea dances on Saturday afternoons', BODY, 26)]),
    ],
    'p2l': [
        ((0, 1), [('THE OLD MILL', SLAB, 50), ('PAPER MAKERS', CAPS, 24), ('---', None, 0),
                  ('Paper of every description: writing, wrapping and folding. Wholesale and retail.', BODY, 26)]),
        ((2, 3), [('PEREYRA\'S', POSTER, 84), ('STATIONERY', CAPS, 24),
                  ('Ink, pens, paste and scissors. Everything for the office and the schoolroom.', HEAD_I, 28)]),
        ((4, 5), [('CINEMA PALACE', HEAD_BLACK, 44), ('---', None, 0), ('"Shadows Over the City"', HEAD_I, 32),
                  ('Continuous performance from two o\'clock', BODY, 26)]),
    ],
    'p2r': [
        ((0, 1), [('THE AVENUE HATTER', HEAD_BLACK, 40), ('---', None, 0), ('Felt hats for gentlemen', HEAD_I, 30),
                  ('Hats cleaned and re-blocked while you wait', BODY, 26)]),
        ((2, 3), [('ORTEGA & DAUGHTERS', SLAB, 40), ('OPTICIANS', CAPS, 24), ('---', None, 0),
                  ('Eyes tested without charge. Spectacles made to order.', BODY, 26)]),
        ((4, 5), [('TAXICABS', POSTER, 90), ('DAY AND NIGHT', CAPS, 24), ('Telephone 2-1717', HEAD_COND, 38)]),
    ],
    'p3l': [
        ((0, 2), [('OLD QUARTER DANCING SOCIETY', HEAD_BLACK, 44), ('---', None, 0),
                  ('The tango taught by experienced teachers', HEAD_I, 32),
                  ('Sunday mornings in the plaza. Private lessons by arrangement.', BODY, 28)]),
        ((3, 5), [('HARBOUR STEAM LAUNDRY', SLAB, 44), ('---', None, 0),
                  ('Collars and cuffs a speciality', HEAD_I, 32),
                  ('Called for and delivered in all districts', BODY, 28)]),
    ],
    'p3r': [
        ((0, 1), [('THE ARCADE', HEAD_BLACK, 46), ('BOOKSHOP', CAPS, 24), ('---', None, 0),
                  ('Books bought and sold. Old volumes a speciality.', BODY, 26)]),
        ((2, 3), [('SEWING MACHINES', SLAB, 42), ('---', None, 0), ('For the home and the workroom', HEAD_I, 30),
                  ('Easy terms. Lessons given free.', BODY, 26)]),
        ((4, 5), [('UMBRELLAS', POSTER, 84), ('THE AVENUE UMBRELLA SHOP', CAPS, 20),
                  ('Established 1898', HEAD_I, 30)]),
    ],
    'p4l': [
        ((0, 2), [('GARAGE OF THE AVENUE', HEAD_BLACK, 46), ('---', None, 0), ('Motor cars, new and used', HEAD_I, 32),
                  ('Repairs undertaken by skilled mechanics. Open day and night.', BODY, 28)]),
        ((3, 5), [('DR. MOLINA\'S TONIC', SLAB, 44), ('---', None, 0), ('For tired nerves and late nights', HEAD_I, 32),
                  ('Sold by all chemists', BODY, 28)]),
    ],
    'p4r': [
        ((0, 2), [('CITY RADIO', POSTER, 96), ('THE EVENING BULLETIN', CAPS, 24),
                  ('Every night at half past eight', HEAD_I, 32)]),
        ((3, 5), [('THE BLUE LAMP COMPANY', HEAD_BLACK, 42), ('---', None, 0), ('Desk lamps of every kind', HEAD_I, 32),
                  ('Orders now taken for delivery next month', BODY, 28)]),
    ],
    'p5l': [
        ((0, 2), [('THE ROYAL THEATRE', HEAD_BLACK, 46), ('---', None, 0), ('"The Paper Moon"', HEAD_I, 34),
                  ('A comedy in three acts. Nightly at nine.', BODY, 28)]),
        ((3, 5), [('HARBOUR STEAMERS', SLAB, 44), ('---', None, 0), ('Daily sailings to the coast', HEAD_I, 32),
                  ('Tickets at the harbour office', BODY, 28)]),
    ],
    'p5r': [
        ((0, 1), [('AVENUE SPORTS', SLAB, 42), ('---', None, 0), ('Footballs, boots and jerseys', HEAD_I, 30),
                  ('Club colours supplied', BODY, 26)]),
        ((2, 3), [('CITY UNITED', HEAD_BLACK, 44), ('v. HARBOUR WANDERERS', CAPS, 22),
                  ('Sunday next at three o\'clock', HEAD_I, 30), ('Special trams from the plaza', BODY, 26)]),
        ((4, 5), [('BIG C', SLAB, 76), ('THE CUP THAT CHEERS', CAPS, 22), ('At every cafe of the avenue', HEAD_I, 30)]),
    ],
}


# ---------------------------------------------------------------------------------------------
# Level page 1 (the cafe, the city): the back page on the left, the front page on the right
# ---------------------------------------------------------------------------------------------

def page1_left():
    sh = Sheet(ads=AD_ROWS['p1l'])
    top = inner_header(sh, 'CLASSIFIED ADVERTISEMENTS')
    r1 = 1500
    x0, x1 = span(0, 2)
    ad(sh, x0, top, x1, r1, [
        ('BIG C', SLAB, 92),
        ('THE CITY\'S FAVOURITE CUP', CAPS, 26),
        ('Fresh-roasted every morning and served hot at every Big C cafe from seven in the morning until '
         'midnight.', HEAD_I, 30),
        ('ASK FOR IT BY NAME', HEAD_COND, 40),
    ], picture=(poster(1), 640))
    x0, x1 = span(3, 5)
    radio_bottom = notices(sh, x0, top, x1, r1 - 400, 'CITY RADIO', [
        ('TODAY\'S PROGRAMMES', [
            ('7.00', 'Morning News and Weather.'), ('7.30', 'Music for the Breakfast Table.'),
            ('9.00', 'Talk for the Household: "Making Do With Paper."'), ('12.00', 'Midday News.'),
            ('1.15', 'The Avenue Dance Orchestra.'), ('4.00', 'Children\'s Hour: "The Little Paper Girl."'),
            ('6.00', 'Market Prices.'), ('7.00', 'Evening News.'), ('7.30', 'The Tango Hour.'),
            ('8.30', 'Evening Bulletin.'), ('9.00', 'Play: "The Shadow Under the Lamp."'),
            ('10.30', 'Late News. Close Down.'),
        ]),
    ], fill=False)
    notices(sh, x0, radio_bottom + 30, x1, r1, 'THE WEATHER', [
        (None, [('TODAY:', 'Fair and cool; light winds from the south.'),
                ('TOMORROW:', 'Cloudy, with rain likely towards evening.'),
                ('SUN:', 'Rises 6.41 a.m.; sets 7.12 p.m.'),
                ('YESTERDAY:', 'Highest 19 degrees, lowest 11.')]),
    ], cols=1, title_size=48)
    row_rule(sh, r1 + 34)
    notices(sh, MARGIN, r1 + 34, W - MARGIN, sh.bottom, 'CLASSIFIED', [
        ('LOST AND FOUND', [
            ('LOST.', 'Gentleman\'s brown leather glove, right hand, in the vicinity of the City Museum, Monday '
             'evening. Liberal reward; no questions asked. Box 41, this office.'),
            ('LOST.', 'Book, blue cloth binding, much worn, last seen upon a writing desk beneath a blue lamp. Of '
             'great sentimental value. Reward.'),
            ('LOST.', 'The way home. Will the finder kindly return it to the third book on the left.'),
            ('FOUND.', 'Small silver thimble, upon the steps of the museum. Owner may have same on describing it. '
             'Box 17.'),
            ('FOUND.', 'Black umbrella, gentleman\'s, at the cafe on the avenue. It has been there some years. '
             'Apply within.'),
        ]),
        ('HELP WANTED', [
            ('PIANIST', 'wanted for new music hall, The Low Note. Must read music and know the tango. Apply after '
             'nine in the evening.'),
            ('NIGHT WATCHMAN', 'wanted, sober and reliable, for warehouse near the river. References required. '
             'Box 23.'),
            ('BOY', 'wanted to deliver newspapers, early mornings. Must have own bicycle. Apply at this office.'),
            ('WAITRESSES', 'wanted for the Big C cafes, all districts. Good wages and meals. Apply at any Big C cafe.'),
        ]),
        ('FOR SALE', [
            ('BICYCLE,', 'gentleman\'s, good condition, one tyre flat. Cheap for cash.'),
            ('TYPEWRITER,', 'Clack & Co. Model 7, little used. Box 52.'),
            ('COFFEE GRINDER,', 'cast iron, hand-operated. Owner now takes his coffee at the cafe.'),
            ('ACCORDION,', 'with case, nearly new. Apply evenings.'),
        ]),
        ('SITUATIONS WANTED', [
            ('YOUNG WOMAN,', 'quick and observant, seeks position as assistant in an office of inquiry. Box 9.'),
            ('GRANDMOTHER,', 'excellent cook, accustomed to travel, seeks position as housekeeper. Will go anywhere '
             'by any means. Box 14.'),
        ]),
        ('SERVICES', [
            ('SCISSORS', 'and knives sharpened. Apply to the grinder, mornings, on the avenue.'),
            ('PAPER BOUGHT.', 'Highest prices paid for old newspapers and wrappings.'),
            ('UMBRELLAS', 'recovered and repaired while you wait.'),
            ('THE TANGO', 'taught. Sunday mornings in the plaza; private lessons by arrangement.'),
        ]),
    ], cols=4, title_size=60)
    footer(sh)
    return sh


def page1_right():
    sh = Sheet(number=1)
    top = front_masthead(sh, ('CITY EDITION', 'All the news that is fit to print.'),
                         ('THE WEATHER', 'Fair and cool today. Rain likely tomorrow.'))
    r1 = 1680
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('THIRD THEFT AT CITY MUSEUM', 'banner'),
        ('Silver Statuette Taken From North Gallery During the Night', 'deck'),
        ('WATCHMEN HEARD NOTHING', 'caps'),
        ('Director Declares Galleries Will Remain Open; Police Double the Night Patrols', 'sub'),
    ])
    px0, px1 = span(2, 3)
    py = photo(sh, px0, y + 4, px1, y + 500, portrait('Grace_TALK.png'), (0.25, 0.08, 0.75, 0.55),
               'Director Grace, of the City Museum, photographed yesterday upon the museum steps.')
    column_rules(sh, 0, 1, y, r1)
    column_rules(sh, 2, 3, py, r1)
    flow(sh, col_rects(0, 1, y, r1) + col_rects(2, 3, py, r1), [
        'Thieves entered the City Museum some time during Monday night and carried away a silver statuette from '
        'the North Gallery, it was disclosed yesterday by the museum authorities. The theft, the third of its '
        'kind since the first of the month, was discovered by a night watchman upon his eleven o\'clock round.',
        'The statuette, a figure of a dancer some eighteen inches in height, had stood in the same corner of the '
        'gallery for upwards of fifty years. It is the property of the city and is described by the director as '
        'beyond price. Earlier in the month a blue porcelain vase and a case of antique pocket watches were '
        'removed from the building in the same mysterious fashion.',
        'No window was broken and no lock was forced. The watchmen, who make their rounds every hour, state that '
        'they observed nothing out of the ordinary and heard no sound. "Whoever is responsible knows this building '
        'as well as any of us," Director Grace told a representative of this newspaper. "They come in after the '
        'doors are closed, and they leave the same way."',
        'The Inspector in charge of the case declined to say whether the three thefts are believed to be the work '
        'of one person, but stated that the department is following several lines of inquiry and expects '
        'developments shortly. The night patrols in the vicinity of the museum have been doubled.',
        'The museum will remain open to the public as usual. Its most valuable possession, the original of "Sin '
        'Titulo" by the artist known as Pelusa, has been placed on view in the East Hall under special guard. '
        'Particulars will be found elsewhere in this issue.',
    ])

    x0, x1 = span(4, 5)
    ix0, ix1 = x0 + 28, x1 - 28
    y = decks(sh, ix0, ix1, top + 26, [
        ('POLICE SEEK MISSING GIRL', 'top'),
        ('Cami Has Not Been Seen at Her Home for Several Days', 'deck'),
        ('PUBLIC\'S AID ASKED', 'caps'),
    ])
    end = flow(sh, [(ix0, y, ix1, r1 - 30)], [
        'The police yesterday asked the assistance of the public in the search for a young girl named Cami, who '
        'has not been seen at her home for several days. Her family state that she is of a quiet and kindly '
        'disposition, fond of books and of stories, and that she left no word of where she was going.',
        'No photograph of the girl is available. Anyone who may have seen her, or who has knowledge of her '
        'whereabouts, is requested to communicate at once with the Central Police Station or with any officer '
        'upon patrol. A friend of the family is also making private inquiries.',
        'Her family have asked this newspaper to say that she is greatly missed, and that she has only to come '
        'home.',
    ], fill=False)
    sh.double_box(x0, top, x1, end + 24)
    fill_under(sh, x0, x1, end + 54, r1)

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 1)
    y = decks(sh, x0, x1, r2a, [('TRAFFIC WORST IN YEARS', 'top'), ('Delays of an Hour Reported Near the Old Quarter', 'sub')])
    column_rules(sh, 0, 1, y, sh.bottom)
    flow(sh, col_rects(0, 1, y, sh.bottom), [
        'Motorists and pedestrians alike complained yesterday of the congestion upon the principal avenues, which '
        'officials of the traffic department describe as the worst in their experience. At several crossings near '
        'the old quarter delays of more than an hour were reported during the afternoon.',
        'The increase is attributed in part to the delivery vans of the Big C company, which make frequent stops '
        'to supply the company\'s cafes and to post its advertising bills, and in part to the number of patrol '
        'cars now upon the streets.',
        'Pedestrians are cautioned to cross only at the corners, and to keep a careful watch for motor traffic.',
    ])
    x0, x1 = span(2, 3)
    y = decks(sh, x0, x1, r2a, [('WHO MAY ENTER MUSEUM AT NIGHT?', 'top'), ('Question Raised by Third Theft', 'sub')])
    column_rules(sh, 2, 3, y, sh.bottom)
    flow(sh, col_rects(2, 3, y, sh.bottom), [
        'Only a small number of persons are permitted to enter the City Museum after its doors have been closed '
        'for the night. They are the director, the members of the night watch, and the family of the museum\'s '
        'foremost patron, the founder of the Big C coffee concern, who has long enjoyed the privilege.',
        'The watchmen each carry a single key, which is surrendered at dawn. The director keeps her own key about '
        'her at all times. The patron\'s son, Ariel, is known to be a frequent visitor to the galleries in the '
        'evening hours, and is said to take a lively interest in the collection.',
        'The police have made it plain that no suspicion attaches to any person at this time.',
    ])
    x0, x1 = span(4, 5)
    notices(sh, x0, r2a, x1, sh.bottom, 'IN THIS ISSUE', [
        (None, [('ARTS.', 'The Pelusa original placed on view in the East Hall.'),
                ('SOCIETY.', 'Big C founder endows a new wing of the museum.'),
                ('CITY LIFE.', 'A new music hall opens in the old quarter.'),
                ('PERSONAL.', 'Notices, letters and inquiries.'),
                ('POLICE.', 'Patrols doubled; new cells completed.'),
                ('SPORT.', 'City United win the derby.')]),
    ], cols=1, title_size=46)
    footer(sh)
    return sh


# ---------------------------------------------------------------------------------------------
# Level page 2 (the museum, looking for clues): arts on the left, society on the right
# ---------------------------------------------------------------------------------------------

def page2_left():
    sh = Sheet(ads=AD_ROWS['p2l'])
    top = inner_header(sh, 'ARTS')
    r1 = 1760
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('PELUSA ORIGINAL ON VIEW', 'banner'),
        ('"Sin Titulo," Valued at Millions, Hung in the East Hall', 'deck'),
        ('UNDER GUARD DAY AND NIGHT', 'caps'),
        ('Public May View the Masterpiece Until Half Past Eight Each Evening', 'sub'),
    ])
    fx0, fx1 = span(0, 1)
    fy0, fy1 = y + 6, y + 610
    sh.double_box(fx0, fy0, fx1, fy1, 14)
    sh.box(fx0 + 26, fy0 + 26, fx1 - 26, fy1 - 26, 3)
    art = halftone(os.path.join(ITEMS, 'Pelusa Icon.png'), int(fx1 - fx0 - 70), int(fy1 - fy0 - 70),
                   (0.06, 0.06, 0.94, 0.94), period=6.0, contrast=1.6, lift=0.08, backdrop=(242, 242))
    sh.paste_ink(art, fx0 + 35, fy0 + 35)
    cy = fy1 + 10
    f = F(BODY_I, 23)
    for l in wrap('"Sin Titulo," by Pelusa, as it now hangs in the East Hall of the City Museum.'.split(), f, fx1 - fx0):
        sh.text((fx0, cy), ' '.join(l), f)
        cy += 26
    cy += 12
    column_rules(sh, 0, 1, cy, r1)
    column_rules(sh, 1, 3, y, r1)
    flow(sh, col_rects(0, 1, cy, r1) + col_rects(2, 3, y, r1), [
        'The original of "Sin Titulo," the celebrated work of the artist known only as Pelusa, was placed on view '
        'in the East Hall of the City Museum yesterday afternoon. It is the first occasion upon which the painting '
        'has been shown to the public, having remained in private hands since it was painted.',
        'The work arrived at the museum in a wooden case under the escort of four officers of police. Six men were '
        'required to hang it, and some time was spent in determining which way up it should hang.',
        'Pelusa, who is regarded by the critics as the foremost of the post-modern school, has never permitted '
        'himself to be photographed, and is understood to have left the city some years ago. Only three of his '
        'works are known to survive, and the value of the present canvas is put at several millions.',
        'In view of the thefts which have lately occurred at the museum, new locks have been fitted to the doors of '
        'the East Hall, and the painting will be inspected by the night watch every hour. The hall closes '
        'promptly at half past eight in the evening, and visitors are asked to leave the building by that hour.',
    ])
    x0, x1 = span(4, 5)
    ad(sh, x0, top, x1, top + 560, [
        ('CITY MUSEUM', HEAD_BLACK, 50), ('---', None, 0),
        ('HOURS OF ADMISSION', CAPS, 22),
        ('Tuesday to Sunday', HEAD_I, 32),
        ('10 a.m. to 8.30 p.m.', POSTER, 76),
        ('Closed on Mondays.  Admission free on Thursdays.', BODY, 26),
        ('Visitors are requested not to touch the exhibits.', BODY_I, 24),
    ])
    y = decks(sh, x0, x1, top + 590, [('THE ART WORLD', 'top'), ('By Our Art Critic', 'sub')])
    column_rules(sh, 4, 5, y, r1)
    flow(sh, col_rects(4, 5, y, r1), [
        'It is not easy to say what "Sin Titulo" represents, and it may be that it represents nothing at all. The '
        'present writer, having stood before it for the better part of an hour, came away persuaded that he had '
        'seen something remarkable, without being able to say what.',
        'Some visitors profess to see in it a tangle of string; others a map of the city; others again a portrait '
        'of the artist\'s state of mind. The museum, wisely, offers no opinion. It is enough that we have it, and '
        'that it is ours to look at.',
        'It is to be hoped that the authorities will take every care of it. The city has lost enough this month.',
    ])

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 2)
    notices(sh, x0, r2a, x1, sh.bottom, 'CALENDAR', [
        ('EVENTS OF THE WEEK', [
            ('TUESDAY.', 'The Low Note, jazz from nine o\'clock.'),
            ('WEDNESDAY.', 'Paper folding for beginners, museum yard, 4 p.m. Pupils to bring their own paper.'),
            ('THURSDAY.', 'Admission to the museum free. Lecture on the modern school, 6 p.m.'),
            ('FRIDAY.', 'Tango night at The Low Note.'),
            ('SATURDAY.', 'Chess at the cafe on the avenue, 6 p.m.'),
            ('SUNDAY.', 'Tango exhibition in the plaza, 11 a.m. Demonstration of the restored catapult, museum '
             'yard, 3 p.m.'),
        ]),
    ], cols=2, title_size=50)
    x0, x1 = span(3, 5)
    notices(sh, x0, r2a, x1, sh.bottom, 'AT THE THEATRES', [
        (None, [
            ('OPERA HOUSE.', 'Tonight\'s performance postponed; tickets honoured on Friday.'),
            ('THE ROYAL.', '"The Paper Moon," a comedy in three acts. Nightly at nine.'),
            ('CINEMA PALACE.', '"Shadows Over the City," with full supporting programme. Continuous from two.'),
            ('THE LOW NOTE.', 'The Low Note Trio, nightly. Tango after midnight.'),
            ('CHILDREN\'S MATINEE.', 'Saturday at two: "The Little Paper Girl," a play for young people.'),
        ]),
    ], cols=2, title_size=50)
    footer(sh)
    return sh


def page2_right():
    sh = Sheet(number=2, ads=AD_ROWS['p2r'])
    top = inner_header(sh, 'SOCIETY')
    r1 = 1700
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('BIG C FOUNDER ENDOWS NEW MUSEUM WING', 'top'),
        ('Coffee Magnate, the Institution\'s Foremost Patron, Pledges a Generous Sum', 'deck'),
        ('SON A FREQUENT VISITOR', 'caps'),
        ('New Wing to House the Family\'s Private Collection, Never Before Exhibited', 'sub'),
    ])
    px0, px1 = span(2, 3)
    py = photo(sh, px0, y + 4, px1, y + 540, portrait('Ariel_SHOCKED_SMUG.png'), (0.22, 0.06, 0.8, 0.5),
               'Ariel, son of the founder of the Big C concern, at Saturday\'s reception.')
    column_rules(sh, 0, 1, y, r1)
    column_rules(sh, 2, 3, py, r1)
    flow(sh, col_rects(0, 1, y, r1) + col_rects(2, 3, py, r1), [
        'The founder of the Big C coffee concern, for many years the most generous patron of the City Museum, has '
        'undertaken to bear the entire cost of a new wing, to be opened next spring, it was announced at a '
        'reception held in the museum on Saturday evening.',
        'The new wing will house the family\'s private collection of paintings and objects of art, which has never '
        'before been seen by the public. It will be known as the Big C Wing, and will contain a cafe of its own.',
        'The family\'s connection with the museum is of long standing. Their name is already to be found upon three '
        'of its halls, upon two of the benches of the sculpture court and upon the coffee urn of the entrance '
        'lobby, and members of the family have for many years enjoyed the privilege of free access to the '
        'building at all hours.',
        'The founder\'s son, Ariel, is a familiar figure in the galleries, particularly in the evening. "My father '
        'wishes everyone to enjoy art," he said on Saturday. "I do my best to set the example."',
        'Director Grace thanked the family for their generosity in a short address.',
    ])
    x0, x1 = span(4, 5)
    y = decks(sh, x0, x1, top, [
        ('ANCIENT CATAPULT RESTORED', 'top'),
        ('Engine Once Used in the City\'s Defence Again in Working Order', 'deck'),
    ])
    column_rules(sh, 4, 5, y, r1)
    flow(sh, col_rects(4, 5, y, r1), [
        'The great wooden catapult which has stood in the museum yard for more than a century has been restored to '
        'working order by the museum\'s carpenters, and will be demonstrated to the public on Sunday afternoon, '
        'weather permitting.',
        'Built some three hundred years ago for the defence of the city walls, the engine was afterwards employed '
        'for the discharge of fireworks upon feast days, and has not been put to any use within living memory.',
        'The carpenter in charge of the work states that it will throw a sack of flour clear across the river, or '
        'any object of about the weight of a young girl. Visitors are earnestly requested not to climb into the '
        'bucket.',
        '"It still works," said Director Grace. "We have not yet decided what we shall do with it."',
    ])

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, r2a, [('A LETTER FROM THE DIRECTOR', 'top'), ('To the Citizens of This City', 'sub')])
    column_rules(sh, 0, 3, y, sh.bottom)
    flow(sh, col_rects(0, 3, y, sh.bottom), [
        'Sir,--I write in the hope that, through your columns, I may reach those who love our museum as I do.',
        'Three times within this month, persons unknown have entered the museum at night and carried away objects '
        'which belong to all of us. I know that many of you are troubled by this, and I wish you to know that I '
        'share your distress.',
        'I give you my word that every effort is being made to bring those responsible to account. The museum '
        'belongs to the whole city, and no person, however highly placed, stands above its rules.',
        'In the meantime our doors remain open. I invite you to visit us, to look at the pictures, and to keep your '
        'eyes open. Someone, somewhere, has seen something.',
        'I am, Sir, yours faithfully, GRACE, Director, City Museum.',
    ], items=[
        ('SOCIETY NOTES', 'A reception was held at the City Museum on Saturday evening to mark the endowment of the '
         'new wing. Among those present were the founder of the Big C concern and his son, the director, and the '
         'members of the museum committee.'),
    ])
    x0, x1 = span(4, 5)
    ad(sh, x0, r2a, x1, sh.bottom, [
        ('BIG C', SLAB, 96),
        ('NOW SERVED IN THE MUSEUM LOBBY', CAPS, 24), ('---', None, 0),
        ('Refresh yourself between the galleries with a cup of Big C and a medialuna.', HEAD_I, 34),
        ('OPEN DAILY DURING MUSEUM HOURS', HEAD_COND, 38),
    ])
    footer(sh)
    return sh


# ---------------------------------------------------------------------------------------------
# Level page 3 (Natalia's house, the jazz bar, the jam): city life on the left, personal on the right
# ---------------------------------------------------------------------------------------------

def page3_left():
    sh = Sheet(ads=AD_ROWS['p3l'])
    top = inner_header(sh, 'CITY LIFE')
    r1 = 1760
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('NEW MUSIC HALL IN OLD QUARTER', 'banner'),
        ('The Low Note Offers Jazz and the Tango Nightly', 'deck'),
        ('A QUEUE UPON THE STAIRS', 'caps'),
        ('Quiet Street Divided in Its Opinion of the New Neighbour', 'sub'),
    ])
    column_rules(sh, 0, 3, y, r1)
    flow(sh, col_rects(0, 3, y, r1), [
        'For many years nothing of note has occurred in the quiet streets at the edge of the old quarter. A week '
        'ago, however, a saxophone was heard in the basement of an old house upon the corner, and it has been '
        'heard every night since.',
        'The Low Note, as the establishment is called, is a small room, very full of smoke and, most evenings, very '
        'full of people. A trio of saxophone, double bass and piano plays slow jazz until midnight and the tango '
        'thereafter. The proprietors are seeking a permanent pianist.',
        'On the first night the band played to an empty room. On the second, six persons attended. By Saturday a '
        'queue had formed outside the door and up the stairs to the top floor of the house.',
        'The neighbours are divided in their opinion. Some have complained to the police; others have been seen in '
        'the queue. A young lady who lives upon the upper floor states that she does not object to the music. "It '
        'helps me to think," she said. "I do my best thinking at night."',
    ], items=[
        ('ABOUT THE TOWN', 'The cafes of the avenue report that business has never been better, though the price of '
         'a cup continues to give rise to complaint among their older patrons.'),
    ])
    x0, x1 = span(4, 5)
    ad(sh, x0, top, x1, r1, [
        ('THE LOW NOTE', POSTER, 120),
        ('MUSIC HALL', CAPS, 26), ('---', None, 0),
        ('JAZZ AND THE TANGO', HEAD_COND, 44),
        ('Nightly from 9 p.m.', HEAD_I, 34),
        ('The Low Note Trio', BODY, 32),
        ('Under the old house on the corner', BODY_I, 28),
        ('Coffee  \u2022  Cocoa  \u2022  Medialunas', BODY, 28),
    ], picture=(poster(3), 600))

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 2)
    y = decks(sh, x0, x1, r2a, [('MOTOR CARS HELD FAST ON AVENUE', 'top'),
                                ('Four Vehicles Unable to Move for Six Hours', 'sub')])
    column_rules(sh, 0, 2, y, sh.bottom)
    flow(sh, col_rects(0, 2, y, sh.bottom), [
        'Four motor cars were held fast upon the avenue outside the old house on the corner for upwards of six hours '
        'yesterday, being unable either to advance or to retire, and their drivers made their feelings known by '
        'sounding their horns at intervals throughout the day.',
        'The obstruction is believed to have begun when a delivery van stopped to unload a crate of advertising '
        'bills. A taxicab stopped behind it, and two private cars behind the taxicab.',
        'An elderly lady who passed at noon inquired of the drivers why they did not get out and push. The question '
        'remained unanswered at the time of going to press. Officers of the traffic department attended in the '
        'afternoon and expressed the opinion that the obstruction would clear in due course.',
    ])
    x0, x1 = span(3, 5)
    y = decks(sh, x0, x1, r2a, [('TANGO EXHIBITION IN THE PLAZA', 'top'),
                                ('Sunday Mornings Throughout the Autumn', 'sub')])
    column_rules(sh, 3, 5, y, sh.bottom)
    flow(sh, col_rects(3, 5, y, sh.bottom), [
        'The exhibitions of the tango given upon Sunday mornings in the plaza by members of the Old Quarter Dancing '
        'Society will be continued throughout the autumn, the society announced yesterday.',
        'Last Sunday\'s exhibition drew a large crowd, and the dancers were warmly applauded. Lessons will be given '
        'after each exhibition, without charge, to any who wish to learn.',
    ])
    footer(sh)
    return sh


def page3_right():
    sh = Sheet(number=3, ads=AD_ROWS['p3r'])
    top = inner_header(sh, 'PERSONAL')
    r1 = 1450
    x0, x1 = span(0, 2)
    sh.double_box(x0, top, x1, r1)
    ix0, ix1 = x0 + 28, x1 - 28
    pw = int((ix1 - ix0) * 0.42)
    photo(sh, ix0, top + 28, ix0 + pw, r1 - 28, portrait('Natalia_THINKING.png'), (0.3, 0.07, 0.72, 0.42))
    tx0, tx1 = ix0 + pw + 22, ix1
    y = top + 40
    y = centred(sh, tx0, tx1, y, 'PRIVATE INQUIRIES', SLAB, 46, 1.05) + 10
    rule_ornament(sh, tx0, tx1, y)
    y += 24
    sh.text(((tx0 + tx1) / 2, y), 'NATALIA', F(POSTER, 132), anchor='ma')
    y += 140
    for text, font, size in [('Confidential investigations undertaken.', HEAD_I, 32),
                             ('Lost articles traced. Missing persons found. Strange cases a speciality.', BODY, 30),
                             ('Moderate terms. Strict discretion.', BODY_I, 30)]:
        y = centred(sh, tx0, tx1, y, text, font, size, 1.15) + 16
    rule_ornament(sh, tx0, tx1, y + 4)
    centred(sh, tx0, tx1, y + 28, 'Apply upstairs, The Low Note, in the old quarter.', HEAD_COND, 34, 1.1)

    x0, x1 = span(3, 5)
    notices(sh, x0, top, x1, r1, 'PERSONAL', [
        (None, [
            ('N.--', 'A small token of my esteem will be left at your door. Pray accept it, and think kindly of one '
             'who has long admired you.--AN ADMIRER.'),
            ('GENTLEMAN,', 'quiet, of good family, fond of art and of coffee, wishes to meet a lady of similar tastes. '
             'Has the run of the museum. Box 30.'),
            ('WIDOW,', 'excellent cook, of adventurous disposition and accustomed to travel by air, would be glad to '
             'hear from persons of like mind. Box 14.'),
            ('SAXOPHONE', 'wishes to meet double bass, with a view to slow numbers and late evenings. Apply The Low '
             'Note.'),
            ('CAMI.--', 'Come home. Your room is just as you left it, and the lamp is always lit.--YOUR FATHER.'),
            ('K.--', 'Do not be afraid. Someone will help you to find your way back.--A FRIEND.'),
        ]),
    ], cols=2)

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, r2a, [('LETTERS TO THE EDITOR', 'top')])
    column_rules(sh, 0, 3, y, sh.bottom)
    flow(sh, col_rects(0, 3, y, sh.bottom), [], items=[
        ('THE NOISE IN THE OLD QUARTER', 'Sir,--May I, through your columns, protest against the saxophone which is '
         'played every night until a late hour in the basement of the house opposite mine? I am not opposed to '
         'music, but I am opposed to the tango at two o\'clock in the morning.--A RESIDENT OF THE OLD QUARTER.'),
        ('THE PRICE OF COFFEE', 'Sir,--When I was a girl, a cup of coffee could be had for a few coins. Today the Big C '
         'cafe on the avenue asks a sum for a coffee and a medialuna that I would once have paid for a week\'s '
         'groceries. It is too dear, Sir, and somebody ought to say so.--A GRANDMOTHER.'),
        ('THE MUSEUM THEFTS', 'Sir,--Is it not time that the police inquired who it is that holds a key to the museum, '
         'and why?--ONE WHO WONDERS.'),
        ('THE TRAFFIC', 'Sir,--I waited at the corner of the avenue for twenty minutes yesterday for an opportunity to '
         'cross. I am eighty-one years of age, and I have not twenty minutes to spare.--A PEDESTRIAN.'),
        ('A WORD OF THANKS', 'Sir,--I should like to thank the young lady with the scissors who freed my kite from the '
         'telegraph wires on Sunday. She did not give her name.--A GRATEFUL BOY.'),
    ])
    x0, x1 = span(4, 5)
    ad(sh, x0, r2a, x1, sh.bottom, [
        ('DRESSMAKING', HEAD_BLACK, 52),
        ('AND ALTERATIONS', CAPS, 26), ('---', None, 0),
        ('Costumes made to measure.', HEAD_I, 32),
        ('Repairs while you wait.', HEAD_I, 32),
        ('Small scissors, careful work.', BODY, 30),
        ('Apply upstairs, The Low Note', HEAD_COND, 36),
    ])
    footer(sh)
    return sh


# ---------------------------------------------------------------------------------------------
# Level page 4 (the police station): police on the left, public notices on the right
# ---------------------------------------------------------------------------------------------

def page4_left():
    sh = Sheet(ads=AD_ROWS['p4l'])
    top = inner_header(sh, 'POLICE AND THE COURTS')
    r1 = 1720
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('POLICE PATROLS DOUBLED', 'banner'),
        ('Every Available Officer on Duty After the Third Museum Theft', 'deck'),
        ('ARRESTS EXPECTED SHORTLY', 'caps'),
        ('Chief of Police Asks Citizens to Report Anything Unusual', 'sub'),
    ])
    px0, px1 = span(2, 3)
    py = photo(sh, px0, y + 4, px1, y + 560, portrait('Policia1_TALK.png'), (0.25, 0.05, 0.75, 0.5),
               'An officer of the Central Station upon his rounds.')
    column_rules(sh, 0, 1, y, r1)
    column_rules(sh, 2, 3, py, r1)
    flow(sh, col_rects(0, 1, y, r1) + col_rects(2, 3, py, r1), [
        'Every available officer of the Central Police Station has been placed upon patrol duty, by day and by '
        'night, following the third theft from the City Museum within the month, the Chief of Police announced '
        'yesterday.',
        '"We expect to make an arrest within the week," the Chief said. Asked whether the department had any '
        'particular person in view, he replied that it would not be in the public interest to say.',
        'Officers have been instructed to keep a close watch upon the museum, the cafes of the avenue and the streets '
        'of the old quarter, and to question any person found loitering in the vicinity of the museum after dark, '
        'particularly any person seen carrying a parcel of unusual shape.',
        'Additional officers have been sent from the provinces to assist the city force. Citizens are asked to '
        'report anything unusual to the nearest officer, and are reminded that the officers of the night patrol '
        'carry lanterns and are not easily deceived.',
    ])
    x0, x1 = span(4, 5)
    y = decks(sh, x0, x1, top, [('NEW CELLS READY AT CENTRAL STATION', 'top'),
                                ('Steel Fences and Patent Locks Installed', 'deck')])
    column_rules(sh, 4, 5, y, r1)
    flow(sh, col_rects(4, 5, y, r1), [
        'The new cells at the Central Police Station were completed yesterday and are now ready for occupation. Each '
        'is enclosed by a fence of steel bars and secured with a padlock of the latest pattern.',
        '"Nobody will get out of these," the desk sergeant said, conducting a representative of this newspaper over '
        'the building. "Not a mouse. Not a scrap of paper."',
        'The only window in the upper storey of the building is set so high, the sergeant added, that nothing could '
        'reach it which could not fly.',
    ], items=[
        ('IN THE POLICE COURTS', 'Two men charged with singing in the street at a late hour were bound over to keep '
         'the peace. They stated that they had been at The Low Note.'),
        ('OBSTRUCTION', 'The driver of a delivery van was cautioned for leaving his vehicle upon the avenue while he '
         'posted advertising bills.'),
        ('UNLICENSED VENDOR', 'A man selling paper flowers without a licence outside the museum was fined, and his '
         'stock was confiscated.'),
    ])

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 1)
    ad(sh, x0, r2a, x1, sh.bottom, [
        ('IRONCLAD', POSTER, 120),
        ('PATENT PADLOCKS', CAPS, 26), ('---', None, 0),
        ('Proof against file, saw and shears.', HEAD_I, 32),
        ('As supplied to the Central Police Station.', BODY, 28),
        ('Every lock guaranteed.', HEAD_COND, 38),
        ('Sold by all good ironmongers', BODY_I, 26),
    ])
    x0, x1 = span(2, 5)
    y = decks(sh, x0, x1, r2a, [('STRANGE NOISE IN SKY ALARMS RESIDENTS', 'top'),
                                ('Weather Bureau Attributes Disturbance to Thunder', 'sub')])
    column_rules(sh, 2, 5, y, sh.bottom)
    flow(sh, col_rects(2, 5, y, sh.bottom), [
        'Residents of the streets about the Central Police Station were alarmed last evening by a deep rumbling sound '
        'in the sky, resembling, as one of them put it, the engine of a motor car a long way off.',
        'Several persons state that they observed an object of the shape of a large paper aeroplane circling above '
        'the roofs. Mrs. Ortega, who lives opposite the station, is positive that she heard a voice overhead. "An '
        'elderly woman\'s voice," she said, "giving a recipe. There was a great deal of garlic in it."',
        'The Weather Bureau attributes the disturbance to thunder.',
    ])
    footer(sh)
    return sh


def page4_right():
    sh = Sheet(number=4, ads=AD_ROWS['p4r'])
    top = inner_header(sh, 'PUBLIC NOTICES')
    r1 = 1600
    x0, x1 = span(0, 1)
    ad(sh, x0, top, x1, r1, [
        ('REWARD', POSTER, 150), ('---', None, 0),
        ('INFORMATION WANTED', HEAD_COND, 42),
        ('The Chief of Police will pay a reward for information leading to the recovery of the objects stolen from '
         'the City Museum, and to the apprehension of the person or persons responsible.', BODY, 28),
        ('The thief is believed to wear a hat and gloves.', HEAD_I, 30),
        ('Apply in confidence, Central Police Station', CAPS, 20),
    ])
    x0, x1 = span(2, 5)
    y = decks(sh, x0, x1, top, [
        ('NEW TRAFFIC REGULATIONS', 'top'),
        ('Stopping on Avenue Forbidden Between Eight and Six', 'deck'),
        ('Rules Come Into Force on Monday Next', 'sub'),
    ])
    column_rules(sh, 2, 5, y, r1)
    flow(sh, col_rects(2, 5, y, r1), [
        'The council has approved new regulations for the control of traffic upon the principal avenues, which come '
        'into force on Monday next.',
        'Under the new rules no vehicle may stop upon the avenue between eight in the morning and six in the evening '
        'except to set down or take up passengers, and delivery vans must unload in the side streets. Bill posters '
        'are forbidden to work from vehicles standing in the roadway.',
        'Officers of the traffic department will be stationed at the principal crossings to assist pedestrians, '
        'and motorists who disregard the regulations will be summoned.',
    ], items=[
        ('MUNICIPAL NOTICE', 'The paper collection will be made in all districts on Thursday. Bundles to be left at '
         'the kerb before eight o\'clock.'),
        ('WATER SUPPLY', 'The water supply of the old quarter will be cut off between two and four on Wednesday '
         'afternoon for repairs to the mains.'),
    ])

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 2)
    notices(sh, x0, r2a, x1, sh.bottom, 'POLICE BLOTTER', [
        (None, [
            ('10.15 a.m.', 'A motor car standing upon the pavement outside the museum. Driver warned.'),
            ('11.30 a.m.', 'Four motor cars held fast upon the avenue. Traffic department informed.'),
            ('1.40 p.m.', 'A brown leather glove reported in a refuse bin near the museum. Owner unknown.'),
            ('3.20 p.m.', 'The lid of a refuse bin reported open. It was closed again.'),
            ('5.05 p.m.', 'Complaint of saxophone playing, old quarter. No action taken.'),
            ('7.50 p.m.', 'An elderly woman reported flying low over the city. Report not credited.'),
            ('10.45 p.m.', 'Lights seen in the museum after closing. The watchman states that it was himself.'),
            ('11.30 p.m.', 'A rumbling in the sky over the Central Station. Weather Bureau informed.'),
        ]),
    ], cols=2, title_size=54)
    x0, x1 = span(3, 4)
    notices(sh, x0, r2a, x1, sh.bottom, 'LOST PROPERTY', [
        (None, [('', 'The following articles have been handed in at the Central Station and may be claimed upon '
                 'proof of ownership:'),
                ('', 'A ring of brass keys.'), ('', 'A black umbrella.'), ('', 'A child\'s red scarf.'),
                ('', 'A lady\'s felt hat, with ribbon.'), ('', 'A pair of spectacles.'),
                ('', 'A volume of poetry, much read.')]),
    ], cols=1, title_size=46)
    x0, x1 = span(5, 5)
    ad(sh, x0, r2a, x1, sh.bottom, [
        ('CLACK & CO.', SLAB, 40), ('TYPEWRITERS', CAPS, 20), ('---', None, 0),
        ('Built for the busy office.', HEAD_I, 28),
        ('As used at the Central Police Station.', BODY, 26),
        ('Strong, simple, speedy.', HEAD_COND, 32),
        ('Easy terms arranged', BODY_I, 24),
    ])
    footer(sh)
    return sh


# ---------------------------------------------------------------------------------------------
# Level page 5 (the museum, the ending): the museum's history on the left, sport and a story on the right
# ---------------------------------------------------------------------------------------------

def page5_left():
    sh = Sheet(ads=AD_ROWS['p5l'])
    top = inner_header(sh, 'THE CITY MUSEUM')
    r1 = 1780
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('A HUNDRED YEARS OF THE CITY MUSEUM', 'top'),
        ('From a Single Room Over a Bookshop to the Pride of the City', 'deck'),
        ('A Short History, Compiled From the Museum\'s Own Records', 'sub'),
    ])
    column_rules(sh, 0, 3, y, r1)
    flow(sh, col_rects(0, 3, y, r1), [
        'The City Museum was founded a little more than a hundred years ago in a single room above a bookshop in the '
        'old quarter, where a society of gentlemen interested in antiquities exhibited their collections to the '
        'public upon Sunday afternoons.',
        'The collections grew, as such collections will, and within twenty years the society had removed to the '
        'present building, which was erected by public subscription upon the site of the old city granary. The great '
        'wooden catapult which stands in the yard is older than the museum itself, having been found upon the site '
        'when the foundations were dug.',
        'The museum passed into the care of the city some sixty years ago. Its collections now fill eleven galleries, '
        'and are visited each year by many thousands of persons. The North Gallery, from which the silver statuette '
        'was taken on Monday night, was opened in the year of the great flood and is among the oldest parts of the '
        'building.',
        'Of late years the museum has owed much to the generosity of the founder of the Big C concern, whose family '
        'have given money for three of its halls and are now to give a fourth wing. The present director was '
        'appointed six years ago, and has been at pains to bring the collections before a wider public.',
        'It is the hope of every citizen that the treasures lately lost will soon be returned to their places, and '
        'that the museum will enter upon its second century as it began its first: open to all.',
    ], items=[
        ('MUSEUM SEEKS WITNESSES', 'The museum authorities would be glad to hear from any person who was in the '
         'vicinity of the building upon the evenings of the recent thefts, and particularly at about the hour of '
         'closing.'),
    ])
    x0, x1 = span(4, 5)
    ad(sh, x0, top, x1, r1, [
        ('BIG C', SLAB, 100),
        ('THE CUP THAT CHEERS', CAPS, 24), ('---', None, 0),
        ('Served in the museum lobby, at every cafe of the avenue, and wherever good company meets.', HEAD_I, 32),
        ('ASK FOR IT BY NAME', HEAD_COND, 40),
    ], picture=(poster(5), 640))

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 5)
    y = decks(sh, x0, x1, r2a, [('CATAPULT TO BE DEMONSTRATED ON SUNDAY', 'top'),
                                ('Public Invited to the Museum Yard at Three O\'Clock', 'sub')])
    column_rules(sh, 0, 5, y, sh.bottom)
    flow(sh, col_rects(0, 5, y, sh.bottom), [
        'The restored catapult of the museum yard will be demonstrated to the public on Sunday afternoon at three '
        'o\'clock, the director announced yesterday. A sack of flour will be thrown across the river, and members '
        'of the public are invited to watch from the bridge.',
        'The carpenters responsible for the restoration will be present to explain the working of the engine. '
        'Children must be accompanied, and no person will on any account be permitted to sit in the bucket.',
    ])
    footer(sh)
    return sh


def page5_right():
    sh = Sheet(number=5, ads=AD_ROWS['p5r'])
    top = inner_header(sh, 'SPORT AND LEISURE')
    r1 = 1620
    x0, x1 = span(0, 3)
    y = decks(sh, x0, x1, top, [
        ('CITY UNITED WIN THE DERBY', 'banner'),
        ('Rovers Beaten by Two Goals to One Before a Record Crowd', 'deck'),
        ('LATE GOAL DECIDES THE MATCH', 'caps'),
    ])
    column_rules(sh, 0, 3, y, r1)
    flow(sh, col_rects(0, 3, y, r1), [
        'City United defeated the Rovers by two goals to one at the Municipal Ground on Sunday afternoon, before a '
        'crowd which the club officials estimate at upwards of twenty thousand, the largest ever seen at the ground.',
        'The Rovers took the lead after a quarter of an hour, when their centre-forward headed in from a corner, and '
        'held it until the interval. United came out a different side in the second half, and drew level shortly '
        'after the hour with a fine shot from the edge of the penalty area.',
        'With five minutes remaining and the crowd already making for the gates, United\'s outside-right ran half the '
        'length of the field and placed the ball beyond the reach of the Rovers\' goalkeeper. The scenes at the '
        'final whistle were such as the ground has seldom witnessed.',
        'United are now two points clear at the head of the table, and meet the Harbour Wanderers away next Sunday.',
    ], items=[
        ('BOXING', 'The middleweight contest at the Avenue Hall on Saturday evening was won upon points by the local '
         'man after ten hard rounds.'),
        ('CYCLING', 'The road race to the coast and back will be held on Sunday week. Entries close on Friday.'),
    ])
    x0, x1 = span(4, 5)
    notices(sh, x0, top, x1, r1, 'ALMANAC', [
        ('THE WEATHER', [('TODAY:', 'Fair and cool.'), ('TOMORROW:', 'Cloudy; rain by evening.')]),
        ('THE SUN', [('RISES:', '6.41 a.m.'), ('SETS:', '7.12 p.m.')]),
        ('THE MOON', [('', 'First quarter on Thursday.')]),
        ('LAMPS', [('', 'Street lamps will be lit at 7.30 p.m.')]),
        ('THE MUSEUM', [('', 'Open until 8.30 p.m.')]),
    ], cols=1, title_size=52)

    r2a = r1 + 34
    row_rule(sh, r2a)
    x0, x1 = span(0, 5)
    y = decks(sh, x0, x1, r2a, [('FOR YOUNG READERS', 'top'),
                                ('"The Little Paper Girl": A Story in Instalments. Chapter the Third', 'sub')])
    column_rules(sh, 0, 5, y, sh.bottom)
    flow(sh, col_rects(0, 5, y, sh.bottom), [
        'Now when the little paper girl had walked a long way through the pages of the big book, she came at last to '
        'a city where everyone drank coffee and nobody had time to stop. She did not know the way home, and she did '
        'not know how to ask, for she was not much of a talker.',
        'But she had a pair of scissors, and she had nimble fingers, and she found that there was very little in the '
        'world that could not be cut, or folded, into something better.',
        'In the city she met a young lady who wore a long coat and noticed everything. "I shall help you to find your '
        'way back," said the young lady, "if you will help me to find a thief." And so the two of them set out '
        'together, the paper girl and the detective, while far above them, on a desk beneath a blue lamp, somebody '
        'turned the page.',
        'What happened to them in the city, and whether the paper girl ever came home, will be told in our next '
        'chapter.',
    ])
    footer(sh)
    return sh


# ---------------------------------------------------------------------------------------------
# Paper, ageing, warping and output
# ---------------------------------------------------------------------------------------------

def value_noise(rng, cell, w, h):
    small = rng.random((h // cell + 2, w // cell + 2)).astype(np.float32)
    img = Image.fromarray((small * 255).astype(np.uint8), 'L').resize((w + cell, h + cell), Image.BICUBIC)
    return np.asarray(img.crop((cell // 2, cell // 2, cell // 2 + w, cell // 2 + h)), dtype=np.float32) / 255.0


def finish(sh, seed):
    """The page on aged paper, with BLEED pixels of paper all round for the turning sheet's overhang."""
    cw, ch = W + 2 * BLEED, H + 2 * BLEED
    rng = np.random.default_rng(seed)
    mott = value_noise(rng, 220, cw, ch) * 0.55 + value_noise(rng, 60, cw, ch) * 0.3 + value_noise(rng, 14, cw, ch) * 0.15
    grain = rng.normal(0, 1, (ch, cw)).astype(np.float32)
    paper = PAPER[None, None, :] * (1.0 - 0.07 * (mott - 0.5) * 2)[..., None] + grain[..., None] * 3.0

    # Darker and yellower towards the page's edges (measured on the page itself, so the bleed keeps darkening)
    yy, xx = np.mgrid[0:ch, 0:cw].astype(np.float32)
    r = np.maximum(np.abs((xx - BLEED) / W - 0.5), np.abs((yy - BLEED) / H - 0.5)) * 2
    edge = np.clip((r - 0.8) / 0.2, 0, 1.4) ** 2
    paper *= (1.0 - edge[..., None] * np.array([0.12, 0.15, 0.24], dtype=np.float32))

    stain = Image.new('L', (cw, ch), 0)
    sd = ImageDraw.Draw(stain)
    for _ in range(4):
        x, y = rng.uniform(0, cw), rng.uniform(0, ch)
        rr = rng.uniform(120, 380)
        sd.ellipse([x - rr, y - rr * rng.uniform(0.6, 1.0), x + rr, y + rr * rng.uniform(0.6, 1.0)],
                   fill=int(rng.uniform(25, 55)))
    stain = stain.filter(ImageFilter.GaussianBlur(70))
    ring = Image.new('L', (cw, ch), 0)
    rd = ImageDraw.Draw(ring)
    x, y, rr = rng.uniform(400, cw - 400), rng.uniform(700, ch - 400), rng.uniform(110, 160)
    rd.ellipse([x - rr, y - rr, x + rr, y + rr], outline=120, width=9)
    rd.ellipse([x - rr * 0.97, y - rr * 0.97, x + rr * 0.97, y + rr * 0.97], fill=22)
    st = np.asarray(ImageChops.lighter(stain, ring.filter(ImageFilter.GaussianBlur(3))), dtype=np.float32) / 255.0
    paper -= st[..., None] * np.array([45, 62, 95], dtype=np.float32)

    for coord, pos in ((yy, BLEED + H / 2), (xx, BLEED + W / 2)):
        dist = coord - pos
        crease = np.exp(-(dist / 3.0) ** 2) * 0.10 - np.exp(-((dist - 6) / 7.0) ** 2) * 0.05
        paper *= (1.0 - crease[..., None])

    ink = Image.new('L', (cw, ch), 0)
    ink.paste(sh.ink, (BLEED, BLEED))
    a = np.asarray(ink.filter(ImageFilter.GaussianBlur(0.7)), dtype=np.float32) / 255.0
    wear = np.clip(0.80 + 0.2 * value_noise(rng, 8, cw, ch) + 0.06 * (value_noise(rng, 90, cw, ch) - 0.5), 0, 1)
    dropout = (rng.random((ch, cw)) > 0.9994).astype(np.float32)
    a = a * wear * (1.0 - dropout * 0.5) * (1.0 - st * 0.25)
    out = paper * (1.0 - a[..., None]) + INK[None, None, :] * a[..., None]
    return np.clip(out, 0, 255).astype(np.float32)


def bilinear(img, x, y):
    h, w = img.shape[:2]
    x = np.clip(x, 0, w - 1.001)
    y = np.clip(y, 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) +
            img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)


def barycentric_fill(points, tris, values, out, filled):
    """For every point inside one of tris (2D corners), writes the barycentric blend of values into out."""
    for t, val in zip(tris, values):
        (ax, ay), (bx, by), (cx_, cy_) = t
        den = (by - cy_) * (ax - cx_) + (cx_ - bx) * (ay - cy_)
        if abs(den) < 1e-12:
            continue
        lo, hi = np.min(t, axis=0) - 1e-6, np.max(t, axis=0) + 1e-6
        sel = np.where(~filled & (points[:, 0] >= lo[0]) & (points[:, 0] <= hi[0]) &
                       (points[:, 1] >= lo[1]) & (points[:, 1] <= hi[1]))[0]
        if not len(sel):
            continue
        px, py = points[sel, 0], points[sel, 1]
        l1 = ((by - cy_) * (px - cx_) + (cx_ - bx) * (py - cy_)) / den
        l2 = ((cy_ - ay) * (px - cx_) + (ax - cx_) * (py - cy_)) / den
        l3 = 1 - l1 - l2
        inside = (l1 >= -1e-6) & (l2 >= -1e-6) & (l3 >= -1e-6)
        idx = sel[inside]
        out[idx] = l1[inside, None] * val[0] + l2[inside, None] * val[1] + l3[inside, None] * val[2]
        filled[idx] = True


def world_to_page(half, X, Z):
    """World (x, z) on a book half -> its picture's normalized (s, t), through the book's own triangles (the
    curl at the spine included); beyond the half, its overall linear mapping carries on into the bleed."""
    pts = np.stack([X, Z], 1)
    u0, v0, u1, v1 = half['uv_rect']
    tri = np.array(half['tris'])                      # (n, 3, [x, z, u, v])
    st = np.stack([(tri[..., 2] - u0) / (u1 - u0), (tri[..., 3] - v0) / (v1 - v0)], -1)
    flat = tri.reshape(-1, 4)
    A = np.hstack([flat[:, 0:2], np.ones((len(flat), 1))])
    coef, *_ = np.linalg.lstsq(A, st.reshape(-1, 2), rcond=None)
    out = np.hstack([pts, np.ones((len(pts), 1))]) @ coef
    filled = np.zeros(len(pts), bool)
    barycentric_fill(pts, tri[..., 0:2], st, out, filled)
    return out


def sheet_face(canvas, face):
    """A turning sheet's face: for each texel, where it lies on the book (through the skinned mesh at the moment
    it lies flat) and what the book's picture shows there."""
    su0, sv0, su1, sv1 = GEO['sheet_uv_rect']
    tri = np.array(face['tris'])                      # (n, 3, [u, v, x, z])
    ext = np.ptp(tri[..., 3]) / np.ptp(tri[..., 2])   # world depth / width of the sheet
    sw = SHEET_W
    shh = int(round(sw * ext / 4)) * 4
    jj, ii = np.mgrid[0:shh, 0:sw]
    u = su0 + (ii + 0.5) / sw * (su1 - su0)
    v = sv0 + (1.0 - (jj + 0.5) / shh) * (sv1 - sv0)  # texture row 0 is the top: v = 1
    pts = np.stack([u.ravel(), v.ravel()], 1)
    xz = np.zeros((len(pts), 2))
    filled = np.zeros(len(pts), bool)
    barycentric_fill(pts, tri[..., 0:2], tri[..., 2:4], xz, filled)
    if not filled.all():
        # texels a hair outside the mesh by rounding: take the nearest covered texel of the same row
        grid, mask = xz.reshape(shh, sw, 2), filled.reshape(shh, sw)
        for j in range(shh):
            row = np.where(mask[j])[0]
            if len(row) and len(row) < sw:
                miss = np.where(~mask[j])[0]
                grid[j, miss] = grid[j, row[np.abs(row[None, :] - miss[:, None]).argmin(1)]]
        xz = grid.reshape(-1, 2)
    st = world_to_page(GEO['book'][face['half']], xz[:, 0], xz[:, 1])
    rgb = bilinear(canvas, BLEED + st[:, 0] * W, BLEED + (1.0 - st[:, 1]) * H).reshape(shh, sw, 3)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), 'RGB')


_known_guids = None


def new_guid():
    global _known_guids
    if _known_guids is None:
        _known_guids = set()
        for base, _, files in os.walk(os.path.join(ROOT, 'Assets')):
            for f in files:
                if f.endswith('.meta'):
                    m = re.search(r'guid: (\w+)', open(os.path.join(base, f), encoding='utf-8', errors='ignore').read())
                    if m:
                        _known_guids.add(m.group(1))
    while True:
        g = uuid.uuid4().hex
        if g not in _known_guids:
            _known_guids.add(g)
            return g


def ensure_meta(image_path, max_size):
    """A texture .meta copied from the current page scan's, with clamped wrap, no power-of-two rescale and a
    larger max size. An existing .meta is left alone so its GUID (and every reference to it) survives."""
    meta = image_path + '.meta'
    if os.path.exists(meta):
        return re.search(r'guid: (\w+)', open(meta).read()).group(1)
    text = open(TEMPLATE_META, encoding='utf-8').read()
    guid = new_guid()
    text = re.sub(r'guid: \w+', 'guid: ' + guid, text, count=1)
    text = re.sub(r'maxTextureSize: \d+', f'maxTextureSize: {max_size}', text)
    text = re.sub(r'wrap([UVW]): \d', r'wrap\1: 1', text)
    text = text.replace('nPOTScale: 1', 'nPOTScale: 0')
    open(meta, 'w', encoding='utf-8', newline='\n').write(text)
    return guid


def ensure_folder_meta(folder):
    meta = folder + '.meta'
    if not os.path.exists(meta):
        open(meta, 'w', encoding='utf-8', newline='\n').write(
            f'fileFormatVersion: 2\nguid: {new_guid()}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def write_page_set(guids, n):
    """Level2_BookPages.asset: the BookPageSet listing every texture, in page order."""
    script_guid = re.search(r'guid: (\w+)', open(SET_SCRIPT_META).read()).group(1)

    def refs(keys):
        return ''.join(f'\n  - {{fileID: 2800000, guid: {guids[k]}, type: 3}}' if k else '\n  - {fileID: 0}' for k in keys)

    pages = range(1, n + 1)
    body = (
        '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        f'  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: Level2_BookPages\n'
        '  m_EditorClassIdentifier: \n'
        f'  _left:{refs([f"P{p}_Left" for p in pages])}\n'
        f'  _right:{refs([f"P{p}_Right" for p in pages])}\n'
        f'  _forwardLeaving:{refs([f"P{p}toP{p + 1}_Leaving" if p < n else None for p in pages])}\n'
        f'  _forwardArriving:{refs([f"P{p}toP{p + 1}_Arriving" if p < n else None for p in pages])}\n'
        f'  _backwardLeaving:{refs([f"P{p}toP{p - 1}_Leaving" if p > 1 else None for p in pages])}\n'
        f'  _backwardArriving:{refs([f"P{p}toP{p - 1}_Arriving" if p > 1 else None for p in pages])}\n')
    path = os.path.join(OUT_DIR, 'Level2_BookPages.asset')
    open(path, 'w', encoding='utf-8', newline='\n').write(body)
    if not os.path.exists(path + '.meta'):
        open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(
            f'fileFormatVersion: 2\nguid: {new_guid()}\nNativeFormatImporter:\n  externalObjects: {{}}\n'
            '  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    print(f'[make-level2-newspaper-pages] wrote {path}')


PAGES = [  # (left half, right half) of each level page
    (page1_left, page1_right), (page2_left, page2_right), (page3_left, page3_right),
    (page4_left, page4_right), (page5_left, page5_right),
]


def save_jpg(img, path):
    img.save(path, quality=92, subsampling=0, optimize=True)


def main():
    os.makedirs(SHEET_DIR, exist_ok=True)
    ensure_folder_meta(SHEET_DIR)
    print(f'[make-level2-newspaper-pages] page {W} x {H} (+{BLEED} bleed), sheet faces {SHEET_W} wide')
    canvases, guids, previews = {}, {}, []
    for p, (left, right) in enumerate(PAGES, start=1):
        for side, build in (('Left', left), ('Right', right)):
            used, short = BRIEF_STREAM.used, BRIEF_STREAM.ran_out
            sh = build()
            print(f'[make-level2-newspaper-pages] P{p} {side}: {BRIEF_STREAM.used - used} short items'
                  + (f', {BRIEF_STREAM.ran_out - short} spaces left EMPTY (write more BRIEFS)' if BRIEF_STREAM.ran_out > short else ''))
            canvas = finish(sh, seed=p * 10 + (1 if side == 'Left' else 2))
            canvases[(p, side)] = canvas
            page = Image.fromarray(canvas[BLEED:BLEED + H, BLEED:BLEED + W].astype(np.uint8), 'RGB')
            path = os.path.join(OUT_DIR, f'Level2_News_P{p}_{side}.jpg')
            save_jpg(page, path)
            guids[f'P{p}_{side}'] = ensure_meta(path, 4096)
            previews.append(page)
            print(f'[make-level2-newspaper-pages] wrote {path}')
    n = len(PAGES)
    for p in range(1, n + 1):
        turns = []
        if p < n:  # forward from p: its right half leaves, the next page's left half lands
            turns.append((p, p + 1, 'forward', (p, 'Right'), (p + 1, 'Left')))
        if p > 1:  # back from p: its left half leaves, the previous page's right half lands
            turns.append((p, p - 1, 'backward', (p, 'Left'), (p - 1, 'Right')))
        for a, b, direction, leaving, arriving in turns:
            for role, source, face in (('Leaving', leaving, f'{direction}_start'),
                                       ('Arriving', arriving, f'{direction}_end')):
                img = sheet_face(canvases[source], GEO['sheet'][face])
                path = os.path.join(SHEET_DIR, f'Level2_Sheet_P{a}toP{b}_{role}.jpg')
                save_jpg(img, path)
                guids[f'P{a}toP{b}_{role}'] = ensure_meta(path, 2048)
                print(f'[make-level2-newspaper-pages] wrote {path} ({img.width} x {img.height})')
    write_page_set(guids, n)

    if '--preview' in sys.argv:
        tw = 420
        th = int(H * tw / W)
        out = Image.new('RGB', (10 * tw + 11 * 20 + 4 * 30, th + 40), (60, 60, 60))
        for i, img in enumerate(previews):
            out.paste(img.resize((tw, th), Image.LANCZOS), (20 + i * (tw + 20) + (i // 2) * 30, 20))
        out.save(sys.argv[sys.argv.index('--preview') + 1])


if __name__ == '__main__':
    main()
