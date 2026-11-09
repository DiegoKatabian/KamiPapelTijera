"""Python reference of the paper page core (spec 025, D23-D28, D38).

The C# core in Assets/Scripts/Book/Paper/Core/ implements the same math; tools/make-paper-goldens.py runs this module
on fixed inputs and writes the numbers the C# tests must reproduce (tools/run-paper-tests.py). Change both together.

Coordinates ("canonical book space", a 2D cross-section perpendicular to the spine, plus z along the spine):
  x: across the page, 0 at the spine, the turn's START side is +x (the half the sheet lifts from), the END side -x;
  y: up (world y);
  z: along the spine (world z), untouched by any of this.
A turn going the other way is the same math with x mirrored by the adapter.

Profiles are the book's top surface on one side, as (outward distance from the spine, y) points: outward = +x on the
start side, -x on the end side.
"""
import math

import numpy as np

SEGMENTS = 64


def smoothstep(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


class Profile:
    """A book half's top surface, parametrised by arc length from the spine."""

    def __init__(self, points):
        p = np.array(points, float)
        if len(p) < 2:
            raise ValueError('a profile needs at least two points')
        self.xo = p[:, 0]
        self.y = p[:, 1]
        seg = np.hypot(np.diff(self.xo), np.diff(self.y))
        self.s = np.concatenate([[0.0], np.cumsum(seg)])
        self.length = float(self.s[-1])
        self.ang = np.arctan2(np.diff(self.y), np.diff(self.xo))  # per segment, relative to outward

    def seg_at(self, u):
        i = int(np.searchsorted(self.s, u, side='right') - 1)
        return min(max(i, 0), len(self.ang) - 1)

    def angle(self, u):
        return float(self.ang[self.seg_at(u)])

    def point(self, u):
        i = self.seg_at(u)
        f = (u - self.s[i]) / max(self.s[i + 1] - self.s[i], 1e-12)
        return float(self.xo[i] + f * (self.xo[i + 1] - self.xo[i])), float(self.y[i] + f * (self.y[i + 1] - self.y[i]))

    def u_at(self, xo):
        """Arc length at outward distance xo (clamped to the profile)."""
        xo = min(max(xo, self.xo[0]), self.xo[-1])
        i = int(np.searchsorted(self.xo, xo, side='right') - 1)
        i = min(max(i, 0), len(self.xo) - 2)
        f = (xo - self.xo[i]) / max(self.xo[i + 1] - self.xo[i], 1e-12)
        return float(self.s[i] + f * (self.s[i + 1] - self.s[i]))

    def height(self, xo):
        xo = min(max(xo, self.xo[0]), self.xo[-1])
        return float(np.interp(xo, self.xo, self.y))


class CurlParams:
    """lead: how far the part at the spine runs ahead of the edge (0 = the sheet turns as a rigid board).
    shape: how the lag grows from the spine to the edge, lag ~ (u / L) ** shape (< 1 bends close to the spine).
    ease: 0 = linear time, 1 = smoothstep time."""

    def __init__(self, lead=0.6, ease=1.0, duration=3.3333, shape=1.0, height_scale=1.0):
        self.lead = lead
        self.ease = ease
        self.duration = duration
        self.shape = shape
        self.height_scale = height_scale

    def eased(self, t):
        t = min(max(t, 0.0), 1.0)
        return (1 - self.ease) * t + self.ease * smoothstep(t)


class Curl:
    """The turning sheet: an inextensible strip of SEGMENTS pieces, from the spine to the edge."""

    def __init__(self, start_profile, end_profile, params):
        self.S = start_profile
        self.E = end_profile
        self.p = params
        self.length = min(start_profile.length, end_profile.length)
        self.du = self.length / SEGMENTS
        # each segment follows the chord of the surface it rests on (a midpoint slope overshoots in the gutter)
        self.phi_s = np.array([self._chord(start_profile, i) for i in range(SEGMENTS)])           # outward = +x
        self.phi_e = np.array([math.pi - self._chord(end_profile, i) for i in range(SEGMENTS)])  # outward = -x
        self.origin = (0.0, 0.5 * (start_profile.y[0] + end_profile.y[0]))

    def _chord(self, profile, i):
        a = profile.point(i * self.du)
        b = profile.point((i + 1) * self.du)
        return math.atan2(b[1] - a[1], b[0] - a[0])

    def angles(self, t):
        e = self.p.eased(t)
        k = self.p.lead
        u = (np.arange(SEGMENTS) + 0.5) / SEGMENTS
        w = np.array([smoothstep(e * (1 + k) - k * ui ** self.p.shape) for ui in u])
        return self.phi_s + w * (self.phi_e - self.phi_s)

    def points(self, t):
        """SEGMENTS + 1 points (x, y) and SEGMENTS segment angles."""
        a = self.angles(t)
        pts = np.zeros((SEGMENTS + 1, 2))
        pts[0] = self.origin
        pts[1:, 0] = self.origin[0] + np.cumsum(np.cos(a)) * self.du
        pts[1:, 1] = self.origin[1] + np.cumsum(np.sin(a)) * self.du
        if self.p.height_scale != 1:
            pts[1:, 1] = self.origin[1] + (pts[1:, 1] - self.origin[1]) * self.p.height_scale
            d = np.diff(pts, axis=0)
            a = np.arctan2(d[:, 1], d[:, 0])
        return pts, a


def frame(pts, angs, du, u):
    """Position and tangent angle at arc length u on an evaluated curve (clamped)."""
    n = len(angs)
    f = min(max(u / du, 0.0), n - 1e-9)
    i = int(f)
    i = min(i, n - 1)
    r = f - i
    p = pts[i] + r * (pts[i + 1] - pts[i])
    return p, float(angs[i])


def rigid(p_from, a_from, p_to, a_to):
    """2D rigid map taking the frame (p_from, a_from) onto (p_to, a_to): returns (R, T) with x' = R @ x + T."""
    d = a_to - a_from
    c, s = math.cos(d), math.sin(d)
    R = np.array([[c, -s], [s, c]])
    T = np.array(p_to) - R @ np.array(p_from)
    return R, T


def height_table(polyline, sign, x0, x1, bins):
    """Lowest y of a polyline over each of `bins` outward-x bins between x0 and x1 (outward = sign * x).
    +inf where the polyline does not pass."""
    table = np.full(bins, np.inf)
    width = (x1 - x0) / bins
    for a, b in zip(polyline[:-1], polyline[1:]):
        xa, xb = sign * a[0], sign * b[0]
        lo, hi = min(xa, xb), max(xa, xb)
        i0 = int(math.floor((lo - x0) / width))
        i1 = int(math.floor((hi - x0) / width))
        for i in range(max(i0, 0), min(i1, bins - 1) + 1):
            # y of the segment over this bin: the lower of its values at the bin's two ends (conservative)
            bl, br = x0 + i * width, x0 + (i + 1) * width
            ys = []
            for bx in (max(bl, lo), min(br, hi)):
                if hi - lo < 1e-12:
                    ys.append(min(a[1], b[1]))
                else:
                    f = (bx - xa) / (xb - xa)
                    ys.append(a[1] + f * (b[1] - a[1]))
            table[i] = min(table[i], min(ys))
    return table


def table_lookup(table, x0, x1, xo):
    bins = len(table)
    i = int(math.floor((xo - x0) / (x1 - x0) * bins))
    if i < 0 or i >= bins:
        return math.inf
    return table[i]


CARD, VOLUME = 0, 1


def fold_corners(corners, kind, alpha, squash, pivot, fall_sign):
    """Corners (N x 3, in the piece's rest outward space: xo, y, z) after folding by alpha (radians) and squashing.
    Card: rotates about the line y = pivot[1], z = pivot[2] (parallel to xo), the top falling toward fall_sign * z.
    Volume: rotates about the line xo = pivot[0], y = pivot[1] (parallel to z), the top falling outward;
    then squashed by `squash` along y toward the pivot's y."""
    c = np.array(corners, float).copy()
    ca, sa = math.cos(alpha), math.sin(alpha)
    if kind == CARD:
        dy = c[:, 1] - pivot[1]
        dz = c[:, 2] - pivot[2]
        c[:, 1] = pivot[1] + dy * ca - fall_sign * dz * sa
        c[:, 2] = pivot[2] + fall_sign * dy * sa + dz * ca
    else:
        dx = c[:, 0] - pivot[0]
        dy = c[:, 1] - pivot[1]
        c[:, 0] = pivot[0] + dx * ca + dy * sa
        c[:, 1] = pivot[1] - dx * sa + dy * ca
    c[:, 1] = pivot[1] + (c[:, 1] - pivot[1]) * squash
    return c


def fits(corners, table, x0, x1, clearance):
    for xo, y, _ in corners:
        if y > table_lookup(table, x0, x1, xo) - clearance:
            return False
    return True


def max_squash(corners, kind, alpha, pivot, fall_sign, table, x0, x1, clearance, min_squash, steps):
    """Largest squash (<= 1) that fits at a fixed fold, or None if even min_squash doesn't."""
    if fits(fold_corners(corners, kind, alpha, 1.0, pivot, fall_sign), table, x0, x1, clearance):
        return 1.0
    if not fits(fold_corners(corners, kind, alpha, min_squash, pivot, fall_sign), table, x0, x1, clearance):
        return None
    lo, hi = min_squash, 1.0
    for _ in range(steps):
        mid = 0.5 * (lo + hi)
        if fits(fold_corners(corners, kind, alpha, mid, pivot, fall_sign), table, x0, x1, clearance):
            lo = mid
        else:
            hi = mid
    return lo


def solve_fold(corners, kind, pivot, fall_sign, table, x0, x1, clearance=0.15, max_alpha=math.pi / 2,
               min_squash=0.02, steps=8, samples=9):
    """The smallest fold that keeps every corner under the table; if no fold does, the fold that allows the largest
    squash (squashing only along y). Folding is scanned at `samples` angles, then refined by bisection, because a wide
    volume gets taller when it tips over. Returns (alpha, squash, fitted)."""
    angles = [max_alpha * i / (samples - 1) for i in range(samples)]
    prev = None
    for a in angles:
        if fits(fold_corners(corners, kind, a, 1.0, pivot, fall_sign), table, x0, x1, clearance):
            if prev is None:
                return a, 1.0, True
            lo, hi = prev, a
            for _ in range(steps):
                mid = 0.5 * (lo + hi)
                if fits(fold_corners(corners, kind, mid, 1.0, pivot, fall_sign), table, x0, x1, clearance):
                    hi = mid
                else:
                    lo = mid
            return hi, 1.0, True
        prev = a
    best = None
    for a in angles:
        sq = max_squash(corners, kind, a, pivot, fall_sign, table, x0, x1, clearance, min_squash, steps)
        if sq is not None and (best is None or sq > best[1] + 1e-9):
            best = (a, sq)
    if best is None:
        return max_alpha, min_squash, False
    return best[0], best[1], True


# ---------------------------------------------------------------------------------------------
# The turn model (mirrors PaperTurnModel.cs)
# ---------------------------------------------------------------------------------------------

START_STATIC, END_STATIC, SHEET_FRONT, SHEET_BACK = 0, 1, 2, 3
TABLE_BINS = 128
TABLE_MARGIN = 4.0
SAMPLE_SPACING = 1.5
MAX_EDGE_SEGMENTS = 16
OWN_HALF_FROM = 0.5


def _segments(length):
    return max(1, min(MAX_EDGE_SEGMENTS, int(math.ceil(length / SAMPLE_SPACING))))


def sample_outline(x0, x1, y0, y1, z0, z1):
    """The outline of a box's x-y face at both z ends (mirrors PaperTurnModel.SampleOutline, same order)."""
    nx, ny = _segments(x1 - x0), _segments(y1 - y0)
    pts = []
    for z in (z0, z1):
        for i in range(nx + 1):
            x = x0 + (x1 - x0) * i / nx
            pts.append((x, y0, z))
            pts.append((x, y1, z))
        for j in range(1, ny):
            y = y0 + (y1 - y0) * j / ny
            pts.append((x0, y, z))
            pts.append((x1, y, z))
    return np.array(pts, float)


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    m = np.eye(4)
    m[0, 0], m[0, 1], m[1, 0], m[1, 1] = c, -s, s, c
    return m


def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    m = np.eye(4)
    m[1, 1], m[1, 2], m[2, 1], m[2, 2] = c, -s, s, c
    return m


def translate(x, y, z):
    m = np.eye(4)
    m[:3, 3] = (x, y, z)
    return m


def scale(x, y, z):
    return np.diag([x, y, z, 1.0])


class Piece:
    def __init__(self, kind, leaf, corners, folds=True):
        self.kind = kind
        self.leaf = leaf
        self.corners = np.array(corners, float)
        self.folds = folds
        self.alpha = None
        self.squash = None
        self.last = None


class TurnModel:
    def __init__(self, curl, fall_sign):
        self.curl = curl
        self.fall = 1 if fall_sign >= 0 else -1
        self.rest_front = curl.points(0.0)
        self.rest_back = curl.points(1.0)
        self.start_surface = self._surface(curl.S, 1)
        self.end_surface = self._surface(curl.E, -1)
        self.x1 = max(curl.S.xo[-1], curl.E.xo[-1]) + TABLE_MARGIN
        self.clearance = 0.15
        self.thickness = [0.0, 0.0, 0.0, 0.0]  # per leaf kind, see PaperTurnModel.LeafThickness
        self.set_time(0.0)

    @staticmethod
    def _surface(p, sign):
        n = 65
        out = []
        for i in range(n):
            x, y = p.point(p.length * i / (n - 1))
            out.append((sign * x, y))
        return np.array(out)

    def set_time(self, t):
        self.t = min(max(t, 0.0), 1.0)
        self.sheet = self.curl.points(self.t)

    def prepare(self, piece):
        piece.sign = 1 if piece.leaf in (START_STATIC, SHEET_FRONT) else -1
        piece.closing = piece.leaf in (END_STATIC, SHEET_FRONT)
        o = piece.corners.copy()
        o[:, 0] *= piece.sign
        # the corners alone let the sheet pass through the middle of a wide piece: test its outline too
        # only the part on its own half: the sheet lands across the spine at the end of every turn
        x0, x1 = o[:, 0].min(), o[:, 0].max()
        test_x0 = max(x0, OWN_HALF_FROM) if x1 > OWN_HALF_FROM else x0
        piece.outward = sample_outline(test_x0, x1, o[:, 1].min(), o[:, 1].max(), o[:, 2].min(), o[:, 2].max())
        if piece.kind == CARD:
            piece.pivot = (o[:, 0].mean(), o[:, 1].min(), o[:, 2].mean())
        else:
            piece.pivot = (o[:, 0].max(), o[:, 1].min(), o[:, 2].mean())
        rest = self.curl.S if piece.sign > 0 else self.curl.E
        piece.hinge_u = rest.u_at(max(piece.pivot[0], 0.0))
        piece.alpha = None
        piece.squash = None

    def leaf_delta(self, piece):
        if piece.leaf in (START_STATIC, END_STATIC):
            return 0.0, np.zeros(2)
        rest_pts, rest_ang = self.rest_front if piece.leaf == SHEET_FRONT else self.rest_back
        rp, ra = frame(rest_pts, rest_ang, self.curl.du, piece.hinge_u)
        np_, na = frame(self.sheet[0], self.sheet[1], self.curl.du, piece.hinge_u)
        d = na - ra
        c, s = math.cos(d), math.sin(d)
        R = np.array([[c, -s], [s, c]])
        return d, np.array(np_) - R @ np.array(rp)

    def offset_sheet(self, distance):
        pts, ang = self.sheet
        a = np.concatenate([ang, [ang[-1]]])
        out = pts.copy()
        out[:, 0] = pts[:, 0] - np.sin(a) * distance
        out[:, 1] = pts[:, 1] + np.cos(a) * distance
        return out

    def other_table(self, piece, delta):
        if piece.leaf == START_STATIC:
            poly = self.offset_sheet(-self.thickness[SHEET_BACK])
        elif piece.leaf == END_STATIC:
            poly = self.offset_sheet(self.thickness[SHEET_FRONT])
        else:
            front = piece.leaf == SHEET_FRONT
            other = (self.end_surface if front else self.start_surface).copy()
            other[:, 1] += self.thickness[END_STATIC if front else START_STATIC]
            a, T = delta
            c, s = math.cos(-a), math.sin(-a)
            R = np.array([[c, -s], [s, c]])
            Tinv = -(R @ T)
            poly = (R @ other.T).T + Tinv
        return height_table(poly, piece.sign, -TABLE_MARGIN, self.x1, TABLE_BINS)

    def solve(self, piece):
        delta = self.leaf_delta(piece)
        alpha, sq = 0.0, 1.0
        if piece.folds:
            table = self.other_table(piece, delta)
            alpha, sq, fitted = solve_fold(piece.outward, piece.kind, piece.pivot, self.fall, table, -TABLE_MARGIN,
                                           self.x1, self.clearance)
            if piece.alpha is not None:
                if piece.closing:
                    alpha, sq = max(alpha, piece.alpha), min(sq, piece.squash)
                else:
                    alpha, sq = min(alpha, piece.alpha), max(sq, piece.squash)
            piece.alpha, piece.squash = alpha, sq
            piece.last = (alpha, sq, fitted)
        return self.compose(piece, delta, alpha, sq)

    def compose(self, piece, delta, alpha, sq):
        o = scale(piece.sign, 1, 1)
        px, py, pz = piece.pivot
        if piece.kind == CARD:
            rot = translate(0, py, pz) @ rot_x(self.fall * alpha) @ translate(0, -py, -pz)
        else:
            rot = translate(px, py, 0) @ rot_z(-alpha) @ translate(-px, -py, 0)
        s = translate(0, py, 0) @ scale(1, sq, 1) @ translate(0, -py, 0)
        d = rot_z(delta[0])
        d[0, 3], d[1, 3] = delta[1][0], delta[1][1]
        return d @ o @ s @ rot @ o
