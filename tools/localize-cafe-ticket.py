"""Builds the English and Portuguese cafe tickets from Valentino's Spanish art.

    python tools/localize-cafe-ticket.py

Writes, next to the Spanish originals:
  Assets/2D/OrigamiProps/OrigamiSheet CAFE TICKET EN.png / PT.png   (the 6-frame fold sheet)
  Assets/2D/UI/InventorySprites/Cafe Ticket Item EN.png / PT.png    (the inventory icon)
Their .meta files are not touched, so re-running keeps every reference.

Each printed Spanish word is erased (paper mirrored in from just above and below it, then its
low-frequency shading matched to the paper seen between the old letters) and the new word drawn
in Century Gothic at the old word's cap height and squash, its ink multiplied onto the paper so
fold shadows carry over. The word boxes below were measured on the 2026-10-20 art: if Valentino
redraws the ticket, re-measure them (each is the old word's ink bounding box, in pixels).
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

REG = r"C:\Windows\Fonts\GOTHIC.TTF"
BOLD = r"C:\Windows\Fonts\GOTHICB.TTF"
MED = "MED"  # Regular thickened with a stroke: the receipt print sits between Century Gothic Regular and Bold
MED_STROKE = 0.022


def load(font_path, size):
    if font_path == MED:
        return ImageFont.truetype(REG, size), max(1, round(size * MED_STROKE))
    return ImageFont.truetype(font_path, size), 0

WORDS = {
    "en": {"DESCRIPCION": "DESCRIPTION", "IMPORTE": "AMOUNT", "CAFE": "COFFEE", "MEDIALUNA": "CROISSANT",
           "FECHA:": "DATE:", "HORA:": "TIME:", "GRACIAS POR SU COMPRA C:": "THANKS FOR YOUR PURCHASE C:"},
    "pt": {"DESCRIPCION": "DESCRIÇÃO", "IMPORTE": "VALOR", "CAFE": "CAFÉ", "MEDIALUNA": "CROISSANT",
           "FECHA:": "DATA:", "HORA:": "HORA:", "GRACIAS POR SU COMPRA C:": "OBRIGADO PELA COMPRA C:"},
}


def cap_height(font):
    l, t, r, b = font.getbbox("H", anchor="ls")
    return -t


def natural_width(text, font, stroke=0):
    l, t, r, b = font.getbbox(text, anchor="ls", stroke_width=stroke)
    return r - l


def clean_rows(arr, x0, x1, y, step, limit=40):
    """How many rows from y (going by step) have no ink in columns x0..x1."""
    n = 0
    while n < limit:
        yy = y + step * n
        if yy < 0 or yy >= arr.shape[0]:
            break
        row = arr[yy, x0:x1 + 1].astype(float)
        lum = row[:, :3].mean(-1)
        opaque = row[:, 3] > 200
        if not opaque.any():
            break
        if ((lum < np.percentile(lum[opaque], 90) * 0.86) & opaque).any():
            break
        n += 1
    return max(1, n)


def box_blur(a, r):
    """Mean over a (2r+1)^2 window, edges clamped. a: 2D float."""
    p = np.pad(a, r, mode="edge")
    c = p.cumsum(0).cumsum(1)
    c = np.pad(c, ((1, 0), (1, 0)))
    k = 2 * r + 1
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def match_shading(orig, fill, r=7):
    """Scales the fill so its low-frequency brightness matches the paper seen between the old letters
    (a crease running through the text row would otherwise come out as a light rectangle)."""
    o = orig[..., :3].astype(float); f = fill[..., :3].astype(float)
    lum_o = o.mean(-1); lum_f = f.mean(-1)
    paper = np.percentile(lum_o, 90)
    m = (lum_o > paper * 0.82).astype(float)
    target = box_blur(lum_o * m, r) / np.maximum(box_blur(m, r), 1e-3)
    wide = box_blur(lum_o * m, r * 3) / np.maximum(box_blur(m, r * 3), 1e-3)
    have = box_blur(m, r)
    target = np.where(have > 0.15, target, wide)
    gain = target / np.maximum(box_blur(lum_f, r), 1)
    gain = np.clip(gain, 0.8, 1.2)
    out = fill.astype(float)
    out[..., :3] = f * gain[..., None]
    return out.clip(0, 255)


def pingpong(i, n):
    period = 2 * n
    k = i % period
    return k if k < n else period - 1 - k


def erase(arr, box, pad=None):
    """Fills box with clean paper from just above (top part) and just below (bottom part), ping-pong mirrored inside the clean gap."""
    x0, y0, x1, y1 = box
    if pad is None:
        pad = min(3, max(1, (y1 - y0 + 1) // 5))
    x0 -= pad + 3; x1 += pad + 3; y0 -= pad; y1 += pad
    h = y1 - y0 + 1
    out = arr.copy()
    src = arr.astype(float)
    ga = clean_rows(arr, x0, x1, y0 - 1, -1)
    gb = clean_rows(arr, x0, x1, y1 + 1, 1)
    for i in range(h):
        y = y0 + i
        above = src[y0 - 1 - pingpong(i, ga), x0:x1 + 1]
        below = src[y1 + 1 + pingpong(h - 1 - i, gb), x0:x1 + 1]
        t = i / max(1, h - 1)
        w = np.clip((t - 0.3) / 0.4, 0, 1)
        row = above * (1 - w) + below * w
        row[:, 3] = src[y, x0:x1 + 1, 3]
        out[y, x0:x1 + 1] = row
    out[y0:y1 + 1, x0:x1 + 1] = match_shading(arr[y0:y1 + 1, x0:x1 + 1], out[y0:y1 + 1, x0:x1 + 1])
    for k, a in ((0, 0.33), (1, 0.66)):
        for x in (x0 + k, x1 - k):
            out[y0:y1 + 1, x] = (arr[y0:y1 + 1, x] * (1 - a) + out[y0:y1 + 1, x] * a)
    return out


def ink_ratio(arr, box):
    x0, y0, x1, y1 = box
    c = arr[y0:y1 + 1, x0:x1 + 1, :3].astype(float)
    lum = c.mean(-1)
    paper = np.percentile(c.reshape(-1, 3), 90, axis=0)
    ink = c[lum <= np.percentile(lum, 6)].mean(0)
    return np.clip(ink / np.maximum(paper, 1), 0, 1)


def text_mask(text, font_path, cap_px, sx, ss=4):
    """Alpha mask of text whose cap height is cap_px and whose width is squashed by sx. Returns mask, baseline row, ink left/right."""
    size = 200
    font, stroke = load(font_path, size * ss // 4)
    ch = cap_height(font) + stroke
    w = natural_width(text, font, stroke)
    canvas = Image.new("L", (w + 40 * ss, int(ch * 2.2) + 20 * ss), 0)
    base = int(ch * 1.5) + 10 * ss
    ImageDraw.Draw(canvas).text((20 * ss, base), text, font=font, fill=255, anchor="ls", stroke_width=stroke, stroke_fill=255)
    scale_y = cap_px / ch
    scale_x = scale_y * sx
    nw = max(1, round(canvas.width * scale_x)); nh = max(1, round(canvas.height * scale_y))
    m = canvas.resize((nw, nh), Image.LANCZOS)
    m = np.array(m).astype(float) / 255.0
    cols = np.where(m.max(0) > 0.08)[0]
    return m, base * scale_y, cols[0], cols[-1]


def calib_sx(old_text, font_path, box):
    font, stroke = load(font_path, 200)
    ch = cap_height(font) + stroke
    bw = box[2] - box[0] + 1; bh = box[3] - box[1] + 1
    return bw / (natural_width(old_text, font, stroke) * bh / ch)


def stamp(arr, mask, baseline, x_anchor, y_base, ratio):
    """Multiplies the paper under mask by the ink ratio. x_anchor = image x of mask column 0."""
    h, w = mask.shape
    top = int(round(y_base - baseline))
    x0 = int(round(x_anchor))
    out = arr.astype(float)
    ys = slice(max(0, top), min(arr.shape[0], top + h))
    xs = slice(max(0, x0), min(arr.shape[1], x0 + w))
    m = mask[ys.start - top:ys.stop - top, xs.start - x0:xs.stop - x0][..., None]
    region = out[ys, xs, :3]
    out[ys, xs, :3] = region * (1 - m * (1 - ratio))
    return out.clip(0, 255).astype(np.uint8)


def replace(arr, box, old, new, font_path=REG, align="left", max_right=None):
    if old == new:
        return arr
    ratio = ink_ratio(arr, box)
    sx = calib_sx(old, font_path, box)
    cap = box[3] - box[1] + 1
    arr = erase(arr, box)
    mask, base, il, ir = text_mask(new, font_path, cap, sx)
    if max_right is not None and align == "left":
        width = ir - il
        if box[0] + width > max_right:
            f = (max_right - box[0]) / width
            mask, base, il, ir = text_mask(new, font_path, cap * f, sx)
    if align == "left":
        xa = box[0] - il
    else:
        xa = box[2] - ir
    return stamp(arr, mask, base, xa, box[3] + 1, ratio), (xa + il, xa + ir)


def move(arr, box, dx):
    """Moves the ink inside box dx pixels sideways (the date/time value after its label changed width)."""
    dx = int(round(dx))
    if dx == 0:
        return arr
    x0, y0, x1, y1 = box
    pad = min(3, max(1, (y1 - y0 + 1) // 5))
    before = arr.astype(float)
    clean = erase(arr, box).astype(float)
    ratio = np.clip(before[y0 - pad:y1 + pad + 1, x0 - pad:x1 + pad + 1, :3] / np.maximum(clean[y0 - pad:y1 + pad + 1, x0 - pad:x1 + pad + 1, :3], 1), 0, 1)
    out = clean.copy()
    out[y0 - pad:y1 + pad + 1, x0 - pad + dx:x1 + pad + 1 + dx, :3] *= ratio
    return out.clip(0, 255).astype(np.uint8)


def relabel_lines(arr, lines, lang, x_off=0, y_off=0):
    """lines: list of (kind, args). Coordinates are relative to (x_off, y_off)."""
    W = WORDS[lang]
    for item in lines:
        kind = item[0]
        def B(b):
            return (b[0] + x_off, b[1] + y_off, b[2] + x_off, b[3] + y_off)
        if kind == "word":
            _, old, box, font, align = item[:5]
            max_right = item[5] + x_off if len(item) > 5 and item[5] is not None else None
            res = replace(arr, B(box), old, W[old], font, align, max_right)
            if isinstance(res, tuple):
                arr = res[0]
        elif kind == "label":
            # label + value pair: label stays left-aligned, value keeps its gap to the label
            _, old, lbox, vbox = item
            if W[old] == old:
                continue
            lb = B(lbox); vb = B(vbox)
            gap = vb[0] - lb[2]
            arr, (l, r) = replace(arr, lb, old, W[old], MED, "left")
            arr = move(arr, vb, (r + gap) - vb[0])
    return arr


# ---------- the fold sheet (2100x1400, 3x2 frames of 700x700) ----------
SHEET_F0 = [
    ("word", "DESCRIPCION", (221, 253, 336, 268), MED, "left"),
    ("word", "IMPORTE", (431, 253, 508, 268), MED, "right"),
    ("word", "CAFE", (225, 310, 301, 334), MED, "left"),
    ("word", "MEDIALUNA", (226, 354, 408, 378), MED, "left", 420),
    ("label", "FECHA:", (196, 515, 251, 528), (257, 513, 350, 529)),
    ("label", "HORA:", (394, 515, 445, 528), (453, 513, 504, 529)),
    ("word", "GRACIAS POR SU COMPRA C:", (195, 551, 478, 566), BOLD, "left", 505),
]
SHEET_F1 = [
    ("word", "DESCRIPCION", (908, 286, 1025, 296), MED, "left"),
    ("word", "IMPORTE", (1119, 286, 1197, 296), MED, "right"),
    ("word", "CAFE", (921, 324, 999, 340), MED, "left"),
    ("word", "MEDIALUNA", (926, 354, 1108, 378), MED, "left", 1120),
    ("label", "FECHA:", (896, 515, 951, 528), (957, 513, 1050, 529)),
    ("label", "HORA:", (1094, 515, 1145, 528), (1153, 513, 1204, 529)),
    ("word", "GRACIAS POR SU COMPRA C:", (895, 551, 1178, 566), BOLD, "left", 1205),
]
SHEET_F2 = [
    ("word", "DESCRIPCION", (1590, 329, 1707, 333), MED, "left"),
    ("word", "IMPORTE", (1800, 329, 1877, 333), MED, "right"),
    ("word", "CAFE", (1614, 341, 1695, 347), MED, "left"),
    ("word", "MEDIALUNA", (1626, 354, 1808, 379), MED, "left", 1820),
    ("label", "FECHA:", (1596, 515, 1651, 528), (1657, 513, 1750, 529)),
    ("label", "HORA:", (1794, 515, 1845, 528), (1853, 513, 1904, 529)),
    ("word", "GRACIAS POR SU COMPRA C:", (1595, 551, 1878, 566), BOLD, "left", 1905),
]
SHEET_F3 = [
    ("label", "FECHA:", (196, 1215, 251, 1228), (257, 1213, 350, 1229)),
    ("label", "HORA:", (394, 1215, 445, 1228), (453, 1213, 504, 1229)),
    ("word", "GRACIAS POR SU COMPRA C:", (195, 1251, 478, 1266), BOLD, "left", 505),
]
SHEET_F4 = [
    ("word", "GRACIAS POR SU COMPRA C:", (895, 1251, 1178, 1267), BOLD, "left", 1205),
]

# ---------- the inventory icon (1000x1000, its print tilted) ----------
ANGLE = 2.8  # the icon's print is tilted: work on a straightened copy, paste only the edited areas back
ICON_OPS = [
    ("word", "DESCRIPCION", (344, 360, 488, 375), MED, "left"),
    ("word", "IMPORTE", (608, 360, 704, 375), MED, "right"),
    ("word", "CAFE", (350, 422, 445, 448), MED, "left"),
    ("word", "MEDIALUNA", (348, 471, 576, 501), MED, "left", 598),
    ("label", "FECHA:", (293, 679, 362, 694), (369, 676, 487, 696)),
    ("label", "HORA:", (540, 679, 605, 695), (615, 678, 679, 695)),
    ("word", "GRACIAS POR SU COMPRA C:", (287, 724, 643, 745), BOLD, "left", 705),
]


def localize_sheet(src, lang, dst):
    arr = np.array(Image.open(src).convert("RGBA"))
    for frame in (SHEET_F0, SHEET_F1, SHEET_F2, SHEET_F3, SHEET_F4):
        arr = relabel_lines(arr, frame, lang)
    Image.fromarray(arr).save(dst)


def localize_icon(src, lang, dst):
    orig = Image.open(src).convert("RGBA")
    rot = np.array(orig.rotate(ANGLE, resample=Image.BICUBIC))
    edited = relabel_lines(rot.copy(), ICON_OPS, lang)
    changed = (np.abs(edited.astype(int) - rot.astype(int)).sum(-1) > 0).astype(np.uint8) * 255
    mask = Image.fromarray(changed).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(1.5))
    back = Image.fromarray(edited).rotate(-ANGLE, resample=Image.BICUBIC)
    out = Image.composite(back, orig, mask.rotate(-ANGLE, resample=Image.BICUBIC))
    out.putalpha(orig.getchannel("A"))
    out.save(dst)


if __name__ == "__main__":
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    sheet = os.path.join(repo, "Assets", "2D", "OrigamiProps", "OrigamiSheet CAFE TICKET")
    icon = os.path.join(repo, "Assets", "2D", "UI", "InventorySprites", "Cafe Ticket Item")
    for lang in ("en", "pt"):
        localize_sheet(sheet + ".png", lang, f"{sheet} {lang.upper()}.png")
        localize_icon(icon + ".png", lang, f"{icon} {lang.upper()}.png")
        print(f"[localize-cafe-ticket] wrote the {lang} sheet and icon")
