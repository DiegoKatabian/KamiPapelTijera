"""Builds the two A4 Level 2 sticker sheets (Kami detective, Natalia, Ariel, and the Abuela or Grace).

Every character image is Valentino's own art: full-body poses rendered from the game's Spine
exports (docs/marketing/stickers-level2/renders/, made with the official spine-webgl runtime)
and his dialogue portraits. The script only lays out backgrounds, frames and text around them.

Sheet = A4 landscape at 300 dpi, rectangles sharing straight cut lines (guillotine friendly).
Section 1: 4 big cards (62x100 mm). Section 2: 4 tall cards (40.75x100 mm). Section 3: 8 small
cards (40.75x50 mm). The sheets differ in section 1 (newspaper front page vs police lineup), and
sheet 1 has the Abuela where sheet 2 has Grace in sections 2 and 3.

    python tools/stickers/make_level2_sheets.py
"""
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "docs", "marketing", "stickers-level2")
RENDERS = os.path.join(OUT, "renders")
LOGOS = os.path.join(ROOT, "Assets", "2D", "UI", "Logo")
PORTRAITS = os.path.join(ROOT, "Assets", "2D", "NPC", "Nuevos Dialogos")
NEWSPRINT = os.path.join(ROOT, "Assets", "3D", "Libros", "Diario Kami", "Texturas", "Newspaper Textures")
FONTS = "C:/Windows/Fonts"

# 600 dpi: the Spine renders are 2400 px tall, enough to print the largest figure at this resolution.
DPI = 600
SHEET_W_MM, SHEET_H_MM = 297, 210
MARGIN_MM = 5
BIG_W_MM = 62

HANDLE = "@KIMMIARTS"


def mm(v):
    return int(round(v * DPI / 25.4))


def px(v):
    """A hairline width tuned at 300 dpi, scaled to the sheet's resolution."""
    return max(1, int(round(v * DPI / 300)))


# ================================================================ what goes on each card

# Section 1, sheet 1: newspaper front page. (pose, headline, line under it)
FRONT_PAGE = [
    ("kami_idle", "¡LA DETECTIVE!", "Kami resuelve el caso"),
    ("natalia_idle", "NATALIA INVESTIGA", "La reportera del barrio"),
    ("abuela_idle", "ABUELA", "La mismísima"),
    ("ariel_point", "¿SOSPECHOSO?", "Ariel niega todo"),
]
# Section 1, sheet 2: police lineup. (pose, name on the letter board)
LINEUP = [("kami_idle", "KAMI"), ("natalia_idle", "NATALIA"), ("abuela_idle", "ABUELA"), ("ariel_angry", "ARIEL")]

# Section 2: case-file folders. (pose, name, bust height as a fraction of the render, head x or None)
FOLDERS_ABUELA = [("kami_jump", "KAMI", 0.62, None), ("natalia_angry", "NATALIA", 0.62, None),
                  ("abuela_idle", "ABUELA", 0.75, 0.45), ("ariel_idle", "ARIEL", 0.62, None)]
FOLDERS_GRACE = FOLDERS_ABUELA[:2] + [("grace_saludo", "GRACE", 0.62, None)] + FOLDERS_ABUELA[3:]

# Section 3: newspaper photos. "r:" = Spine render, "p:" = dialogue portrait.
# (source, bust height fraction, caption, head x or None, top of the crop as a fraction)
PHOTOS_GRACE = [
    ("r:kami_idle", 0.40, "Kami", None, 0.0), ("p:Natalia_TALK", 0.50, "Natalia", None, 0.0),
    ("r:grace_idle", 0.36, "Grace", None, 0.0), ("p:Ariel_SERIOUS", 0.50, "Ariel", None, 0.0),
    ("r:kami_reward", 0.40, "Kami", None, 0.0), ("p:Natalia_THINKING", 0.50, "Natalia", None, 0.0),
    ("r:grace_saludo", 0.36, "Grace", None, 0.0), ("p:Ariel_SHOCKED_SMUG", 0.50, "Ariel", None, 0.0),
]
# The Abuela's staff and raised arm fool the automatic head finder, so her crops say where her head is.
PHOTOS_ABUELA = list(PHOTOS_GRACE)
PHOTOS_ABUELA[2] = ("r:abuela_idle", 0.45, "Abuela", 0.45, 0.0)
PHOTOS_ABUELA[6] = ("r:abuela_celebrate", 0.37, "Abuela", 0.38, 0.27)

# Where a pose's body stands, as a fraction of the render's width, for poses whose bounding box is
# not centred on the character (Kami's idle holds the scissors out to one side).
BODY_X = {"kami_idle": 0.66}
# How far a wide pose may bleed past the card's sides instead of shrinking (Kami's scissor tips).
BLEED = {"kami": 1.7}


# ================================================================ loading

_cache = {}


def load(path):
    if path not in _cache:
        im = Image.open(path).convert("RGBA")
        bbox = im.getchannel("A").getbbox()
        _cache[path] = im.crop(bbox) if bbox else im
    return _cache[path]


def render(name):
    return load(os.path.join(RENDERS, name + ".png"))


def portrait(name):
    return load(os.path.join(PORTRAITS, name + ".png"))


def small_source(spec):
    kind, name = spec.split(":")
    return render(name) if kind == "r" else portrait(name)


def logo(name):
    return load(os.path.join(LOGOS, name))


def texture(name):
    key = "tex:" + name
    if key not in _cache:
        _cache[key] = Image.open(os.path.join(NEWSPRINT, name)).convert("RGB")
    return _cache[key]


def font(file, size_mm):
    return ImageFont.truetype(os.path.join(FONTS, file), max(8, mm(size_mm)))


# ================================================================ image helpers


def scaled(im, w=None, h=None):
    if w is None:
        w = im.width * h / im.height
    if h is None:
        h = im.height * w / im.width
    return im.resize((max(1, int(w)), max(1, int(h))), Image.LANCZOS)


def fit(im, box_w, box_h):
    s = min(box_w / im.width, box_h / im.height)
    return scaled(im, im.width * s, im.height * s)


def head_x(im):
    head = im.getchannel("A").crop((0, 0, im.width, max(1, int(im.height * 0.08)))).getbbox()
    return (head[0] + head[2]) / 2 if head else im.width / 2


def bust(im, frac, aspect, cx_frac=None, top_frac=0.0):
    """`frac` of a full-body image's height from `top_frac` down, cropped to w/h `aspect` around
    the head (`cx_frac` of the width when the top of the image is not the head)."""
    top = int(im.height * top_frac)
    h = int(im.height * frac)
    cx = im.width * cx_frac if cx_frac is not None else head_x(im)
    w = int(h * aspect)
    left = int(cx - w / 2)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste(im.crop((left, top, left + w, top + h)), (0, 0))
    return out


def shadow_of(im, blur_mm=1.2, opacity=0.45):
    a = im.getchannel("A").point(lambda v: int(v * opacity))
    a = a.filter(ImageFilter.GaussianBlur(mm(blur_mm)))
    sh = Image.new("RGBA", im.size, (0, 0, 0, 255))
    sh.putalpha(a)
    return sh


def figure(name, box_w, box_h):
    """Fits a render in the box; returns it with the x (fraction of its width) to centre on."""
    bleed = BLEED.get(name.split("_")[0], 1.0)
    return fit(render(name), box_w * bleed, box_h), BODY_X.get(name)


def paste_figure(card, fig, cx, bottom, opacity=0.45):
    im, body_x = fig
    if body_x is not None:
        cx -= im.width * body_x - im.width / 2
    elif im.width > card.width:
        cx -= head_x(im) - im.width / 2
    x, y = int(cx - im.width / 2), int(bottom - im.height)
    _paste_clipped(card, shadow_of(im, opacity=opacity), x + mm(0.8), y + mm(0.8))
    _paste_clipped(card, im, x, y)


def _paste_clipped(card, im, x, y):
    layer = Image.new("RGBA", card.size, (0, 0, 0, 0))
    layer.paste(im, (x, y), im)
    card.alpha_composite(layer)


def tile(tex, w, h, tile_px, offset=(0, 0)):
    t = tex.resize((tile_px, tile_px), Image.LANCZOS)
    out = Image.new("RGB", (w, h))
    ox, oy = offset[0] % tile_px, offset[1] % tile_px
    for y in range(-oy, h, tile_px):
        for x in range(-ox, w, tile_px):
            out.paste(t, (x, y))
    return out


def solid(w, h, color):
    return Image.new("RGBA", (w, h), color)


def tint(im_rgb, dark, light):
    return ImageOps.colorize(ImageOps.grayscale(im_rgb), dark, light).convert("RGBA")


def fade(im_rgba, toward, amount):
    return Image.blend(im_rgba.convert("RGBA"), Image.new("RGBA", im_rgba.size, toward), amount)


def paper_noise(w, h, base, strength=10, seed=1):
    rnd = random.Random(seed)
    grain = px(6)
    small = Image.new("L", (max(1, w // grain), max(1, h // grain)))
    small.putdata([128 + rnd.randint(-strength, strength) for _ in range(small.width * small.height)])
    n = small.resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(px(1.5)))
    return ImageChops.overlay(Image.new("RGB", (w, h), base), Image.merge("RGB", (n, n, n))).convert("RGBA")


def vgradient(w, h, top, bottom):
    g = Image.new("RGBA", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        g.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    return g.resize((w, h))


def text(card, xy, s, f, fill, anchor="la"):
    ImageDraw.Draw(card).text(xy, s, font=f, fill=fill, anchor=anchor)


def spaced(card, xy, s, f, fill, tracking_mm):
    """Letter-spaced single line centred on xy[0]."""
    d = ImageDraw.Draw(card)
    widths = [d.textlength(c, font=f) for c in s]
    x = xy[0] - (sum(widths) + mm(tracking_mm) * (len(s) - 1)) / 2
    for c, w in zip(s, widths):
        d.text((x, xy[1]), c, font=f, fill=fill, anchor="ls")
        x += w + mm(tracking_mm)


def shrink_to(card, s, f, max_w):
    d = ImageDraw.Draw(card)
    while d.textlength(s, font=f) > max_w:
        f = ImageFont.truetype(f.path, f.size - 2)
    return f


def with_logo(card, which, cx, cy, width_mm):
    lg = scaled(logo(which), w=mm(width_mm))
    _paste_clipped(card, lg, int(cx - lg.width / 2), int(cy - lg.height / 2))


def stamp(card, cx, cy, s, size_mm, angle, color):
    f = font("BOD_PSTC.TTF", size_mm)
    tw = ImageDraw.Draw(card).textlength(s, font=f)
    pad = mm(1.2)
    t = Image.new("RGBA", (int(tw + 2 * pad), int(mm(size_mm) * 1.05 + 2 * pad)), (0, 0, 0, 0))
    td = ImageDraw.Draw(t)
    td.rectangle((2, 2, t.width - 3, t.height - 3), outline=color, width=mm(0.5))
    td.text((t.width / 2, t.height / 2), s, font=f, fill=color, anchor="mm")
    t = t.rotate(angle, expand=True, resample=Image.BICUBIC)
    _paste_clipped(card, t, int(cx - t.width / 2), int(cy - t.height / 2))


# ================================================================ newspaper (sheet 1 section 1, section 3)

INK = (24, 23, 22, 255)
PAPER = (238, 233, 222, 255)
RED = (176, 36, 40, 255)
PAPER_NAME = "Kami, Tinta y Condena"


def newsprint_bg(w, h, seed, amount=0.62):
    bg = tile(texture("Newspaper Seamless Texture 2.png"), w, h, mm(55), (seed * 377, seed * 211)).convert("RGBA")
    return fade(bg, PAPER, amount)


def rule(card, y, x0, x1, width_px):
    ImageDraw.Draw(card).line((x0, y, x1, y), fill=INK, width=width_px)


def masthead(card, w, top, big_mm, sub):
    f = shrink_to(card, PAPER_NAME, font("OLDENGL.TTF", big_mm), w - mm(5))
    text(card, (w // 2, top + mm(big_mm * 0.95)), PAPER_NAME, f, INK, anchor="ms")
    y = top + mm(big_mm * 1.25)
    rule(card, y, mm(3), w - mm(3), px(6))
    rule(card, y + mm(0.9), mm(3), w - mm(3), px(2))
    spaced(card, (w // 2, y + mm(big_mm * 0.56)), sub, font("BOD_R.TTF", big_mm * 0.34), INK, 0.6)
    y2 = y + mm(big_mm * 0.72)
    rule(card, y2, mm(3), w - mm(3), px(2))
    return y2


def front_page(i, w, h):
    pose, headline, kicker = FRONT_PAGE[i]
    card = newsprint_bg(w, h, i)
    card.alpha_composite(solid(w, mm(18), PAPER), (0, 0))
    y = masthead(card, w, mm(1.5), 8.0, "EDICIÓN EXTRA")
    band_h = mm(17.5)
    band_top = h - band_h
    card.alpha_composite(solid(w, band_h, PAPER), (0, band_top))
    rule(card, band_top, 0, w, px(5))
    fh = shrink_to(card, headline, font("BOD_PSTC.TTF", 11.0), w - mm(5))
    text(card, (w // 2, band_top + mm(10.4)), headline, fh, INK, anchor="ms")
    text(card, (w // 2, h - mm(2.2)), kicker, font("BOD_I.TTF", 2.9), INK, anchor="ms")
    # Ariel points to the right, so his stamp goes on the left.
    stamp_x = mm(10) if pose == "ariel_point" else w - mm(10)
    stamp(card, stamp_x, y + mm(8), "EXTRA", 4.0, 12 if pose == "ariel_point" else -12, RED)
    # The cut-out stands in front of the headline band.
    fig = figure(pose, w - mm(3), band_top + mm(3) - (y + mm(0.5)))
    paste_figure(card, fig, w // 2, band_top + mm(3))
    text(card, (w - mm(2.2), h - mm(2.2)), HANDLE, font("ariblk.ttf", 1.6), INK, anchor="rs")
    with_logo(card, "LOGO KPT Simple PNG.png", mm(6.6), h - mm(5.2), 9)
    return card


def news_photo(entry, i, w, h):
    spec, frac, name, cx_frac, top_frac = entry
    card = paper_noise(w, h, PAPER[:3], 8, seed=i)
    box = (mm(2.2), mm(2.2), w - mm(2.2), h - mm(10))
    bw, bh = box[2] - box[0], box[3] - box[1]
    photo = tint(tile(texture("Newspaper Seamless Texture 2.png"), bw, bh, mm(38), (i * 97, 0)), (70, 68, 66), (205, 200, 192))
    card.alpha_composite(photo, box[:2])
    b = scaled(bust(small_source(spec), frac, bw / bh * 1.05, cx_frac, top_frac), w=bw * 1.05)
    layer = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
    layer.alpha_composite(shadow_of(b, 0.8, 0.5), (int((bw - b.width) / 2) + mm(0.6), mm(1.6)))
    layer.alpha_composite(b, (int((bw - b.width) / 2), mm(1)))
    card.alpha_composite(layer, box[:2])
    ImageDraw.Draw(card).rectangle(box, outline=INK, width=mm(0.45))
    text(card, (w // 2, h - mm(5.2)), f"Fig. {i + 1}  —  {name}", font("BOD_I.TTF", 3.2), INK, anchor="ms")
    text(card, (w // 2, h - mm(1.6)), HANDLE, font("ariblk.ttf", 1.5), INK, anchor="ms")
    return card


# ================================================================ police file (sheet 2 section 1, section 2)

WALL = (92, 96, 101, 255)
KRAFT_DARK, KRAFT_LIGHT = (122, 92, 52), (226, 200, 150)
TYPE = (34, 32, 30, 255)


def lineup(i, w, h):
    pose, name = LINEUP[i]
    card = fade(tile(texture("Newspaper Seamless Texture 2 Black.png"), w, h, mm(60), (i * 300, 0)).convert("RGBA"), WALL, 0.82)
    d = ImageDraw.Draw(card)
    board_top = h - mm(18)
    # Lineup height chart: a line every 5 mm, labelled every 10.
    fl = font("bahnschrift.ttf", 2.6)
    for n, y in enumerate(range(board_top - mm(2), mm(14), -mm(5))):
        major = n % 2 == 0
        d.line((0, y, w, y), fill=(235, 235, 235, 150 if major else 80), width=mm(0.35 if major else 0.2))
        if major:
            label = str(100 + n * 5)
            d.text((mm(1.5), y - mm(0.6)), label, font=fl, fill=(240, 240, 240, 220), anchor="ls")
            d.text((w - mm(1.5), y - mm(0.6)), label, font=fl, fill=(240, 240, 240, 220), anchor="rs")
    with_logo(card, "LOGO KPT Simple PNG Blanco.png", w // 2, mm(6.8), 19)
    fig = figure(pose, w - mm(5), board_top + mm(1.5) - mm(13))
    paste_figure(card, fig, w // 2, board_top + mm(1.5), opacity=0.6)
    # Letter board, like the mugshot placards.
    bx0, bx1 = mm(4), w - mm(4)
    by0, by1 = board_top - mm(1), h - mm(2.5)
    d.rectangle((bx0, by0, bx1, by1), fill=(22, 22, 24, 255))
    for y in range(by0 + mm(1.5), by1, mm(1.15)):
        d.line((bx0 + mm(1), y, bx1 - mm(1), y), fill=(40, 40, 44, 255), width=px(2))
    d.rectangle((bx0, by0, bx1, by1), outline=(140, 140, 140, 255), width=mm(0.4))
    spaced(card, (w // 2, by0 + mm(9.6)), name, font("bahnschrift.ttf", 7.0), (245, 245, 240, 255), 1.0)
    spaced(card, (w // 2, by1 - mm(2.0)), HANDLE, font("bahnschrift.ttf", 2.0), (190, 190, 190, 255), 0.4)
    return card


def kraft(w, h, seed):
    tex = texture("Newspaper Seamless Texture 3 Handwritten.png")
    return tint(tile(tex, w, h, mm(45), (seed * 211, seed * 97)), KRAFT_DARK, KRAFT_LIGHT)


def paperclip(card, x, y, size_mm, angle=-8):
    s = mm(size_mm)
    t = Image.new("RGBA", (int(s * 0.45), s), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    lw = max(3, mm(0.5))
    c = (150, 152, 158, 255)
    tw = t.width
    d.rounded_rectangle((lw, lw, tw - lw, s - lw), radius=int(tw / 2 - lw), outline=c, width=lw)
    d.rounded_rectangle((tw * 0.28, s * 0.18, tw * 0.72, s * 0.72), radius=int(tw * 0.22), outline=c, width=lw)
    _paste_clipped(card, t.rotate(angle, expand=True, resample=Image.BICUBIC), x, y)


def polaroid(im, w_px, h_px, bg=(196, 198, 200)):
    frame = Image.new("RGBA", (w_px, h_px), (250, 249, 245, 255))
    pad = int(w_px * 0.07)
    inner_w, inner_h = w_px - 2 * pad, h_px - pad - int(h_px * 0.17)
    photo = vgradient(inner_w, inner_h, (120, 124, 130), bg)
    fig = scaled(im, w=inner_w)
    layer = Image.new("RGBA", (inner_w, inner_h), (0, 0, 0, 0))
    layer.alpha_composite(shadow_of(fig, 0.6, 0.5), (int((inner_w - fig.width) / 2) + mm(0.5), mm(1.5)))
    layer.alpha_composite(fig, (int((inner_w - fig.width) / 2), mm(1)))
    photo.alpha_composite(layer)
    frame.alpha_composite(photo, (pad, pad))
    return frame


def case_folder(entry, i, w, h):
    pose, name, frac, cx_frac = entry
    card = kraft(w, h, i)
    d = ImageDraw.Draw(card)
    # Folder tab and its fold.
    d.rectangle((0, 0, w, mm(9)), fill=(176, 140, 86, 255))
    d.line((0, mm(9), w, mm(9)), fill=(110, 82, 46, 255), width=mm(0.35))
    text(card, (mm(2.5), mm(6.2)), f"EXP. 0{i + 1}", font("courbd.ttf", 3.4), TYPE)
    with_logo(card, "LOGO KPT Simple PNG.png", w - mm(8), mm(4.6), 12)
    pw, ph = w - mm(5), mm(64)
    pad = int(pw * 0.07)
    src = bust(render(pose), frac, (pw - 2 * pad) / (ph - pad - int(ph * 0.17)), cx_frac)
    pol = polaroid(src, pw, ph).rotate(2.5 if i % 2 else -2.5, expand=True, resample=Image.BICUBIC)
    card.alpha_composite(shadow_of(pol, 1.0, 0.35), (int((w - pol.width) / 2) + mm(0.6), mm(14.6)))
    card.alpha_composite(pol, (int((w - pol.width) / 2), mm(13.5)))
    paperclip(card, mm(4), mm(10.5), 11)
    text(card, (w // 2, mm(13.5) + pol.height + mm(5)), f"SUJETO: {name}", font("courbd.ttf", 3.6), TYPE, anchor="ms")
    stamp(card, w // 2, h - mm(7.8), "CONFIDENCIAL", 4.6, 9, RED)
    text(card, (w // 2, h - mm(1.5)), HANDLE, font("courbd.ttf", 1.9), TYPE, anchor="ms")
    return card


# ================================================================ sheets


def layout():
    """(kind, index, x_mm, y_mm, w_mm, h_mm) for every card on the sheet."""
    cells = []
    m = MARGIN_MM
    for r in range(2):
        for c in range(2):
            cells.append(("big", r * 2 + c, m + c * BIG_W_MM, m + r * 100, BIG_W_MM, 100))
    right_x = m + 2 * BIG_W_MM
    col_w = (SHEET_W_MM - m - right_x) / 4
    for c in range(4):
        cells.append(("tall", c, right_x + c * col_w, m, col_w, 100))
    for r in range(2):
        for c in range(4):
            cells.append(("small", r * 4 + c, right_x + c * col_w, m + 100 + r * 50, col_w, 50))
    return cells


def build_sheet(sections, name):
    sheet = Image.new("RGBA", (mm(SHEET_W_MM), mm(SHEET_H_MM)), (255, 255, 255, 255))
    for kind, i, x, y, w, h in layout():
        sheet.alpha_composite(sections[kind](i, mm(w), mm(h)), (mm(x), mm(y)))
    # Hairline cut guides, the same light grey as Valen's Level 1 sheet.
    lines = ImageDraw.Draw(sheet)
    for kind, i, x, y, w, h in layout():
        lines.rectangle((mm(x), mm(y), mm(x + w), mm(y + h)), outline=(190, 190, 190, 255), width=px(2))
    rgb = sheet.convert("RGB")
    png = os.path.join(OUT, name + ".png")
    rgb.save(png, dpi=(DPI, DPI))
    print("[make_level2_sheets] wrote", png)


SHEETS = {
    "plancha_1_diario": {
        "big": front_page,
        "tall": lambda i, w, h: case_folder(FOLDERS_ABUELA[i], i, w, h),
        "small": lambda i, w, h: news_photo(PHOTOS_ABUELA[i], i, w, h),
    },
    "plancha_2_rueda": {
        "big": lineup,
        "tall": lambda i, w, h: case_folder(FOLDERS_GRACE[i], i, w, h),
        "small": lambda i, w, h: news_photo(PHOTOS_GRACE[i], i, w, h),
    },
}


if __name__ == "__main__":
    for sheet_name, sections in SHEETS.items():
        build_sheet(sections, sheet_name)
