"""A small z-buffer rasteriser for previews (spec 025 D8): matplotlib's 3D view sorts whole polygons, so overlapping
pieces draw in the wrong order. Renders triangles with Lambert shading, either a flat colour per triangle or a texture
through per-vertex UVs. numba makes it fast; without it, it still works, slowly.
"""
import math

import numpy as np

try:
    from numba import njit
except ImportError:  # pragma: no cover
    def njit(*a, **k):
        def wrap(f):
            return f
        return wrap if not (a and callable(a[0])) else a[0]


def look_at(eye, target, up=(0, 1, 0)):
    """World -> camera matrix (camera looks along +z in its own space, Unity-like)."""
    eye = np.array(eye, float)
    f = np.array(target, float) - eye
    f /= np.linalg.norm(f)
    r = np.cross(np.array(up, float), f)
    r /= np.linalg.norm(r)
    u = np.cross(f, r)
    m = np.eye(4)
    m[0, :3], m[1, :3], m[2, :3] = r, u, f
    m[:3, 3] = -m[:3, :3] @ eye
    return m


@njit(cache=True)
def _raster(px, py, pz, uv, tris, tri_shade, tri_color, tex, use_tex, img, depth):
    h, w = depth.shape
    th = tex.shape[0]
    tw = tex.shape[1]
    for t in range(tris.shape[0]):
        a, b, c = tris[t, 0], tris[t, 1], tris[t, 2]
        x0, y0, z0 = px[a], py[a], pz[a]
        x1, y1, z1 = px[b], py[b], pz[b]
        x2, y2, z2 = px[c], py[c], pz[c]
        if z0 <= 0 or z1 <= 0 or z2 <= 0:
            continue
        minx = max(int(math.floor(min(x0, min(x1, x2)))), 0)
        maxx = min(int(math.ceil(max(x0, max(x1, x2)))), w - 1)
        miny = max(int(math.floor(min(y0, min(y1, y2)))), 0)
        maxy = min(int(math.ceil(max(y0, max(y1, y2)))), h - 1)
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-9:
            continue
        for yy in range(miny, maxy + 1):
            for xx in range(minx, maxx + 1):
                sx = xx + 0.5
                sy = yy + 0.5
                w0 = ((x1 - sx) * (y2 - sy) - (x2 - sx) * (y1 - sy)) / area
                w1 = ((x2 - sx) * (y0 - sy) - (x0 - sx) * (y2 - sy)) / area
                w2 = 1.0 - w0 - w1
                if w0 < 0 or w1 < 0 or w2 < 0:
                    continue
                z = w0 * z0 + w1 * z1 + w2 * z2
                if z >= depth[yy, xx]:
                    continue
                depth[yy, xx] = z
                s = tri_shade[t]
                if use_tex:
                    u = w0 * uv[a, 0] + w1 * uv[b, 0] + w2 * uv[c, 0]
                    v = w0 * uv[a, 1] + w1 * uv[b, 1] + w2 * uv[c, 1]
                    ix = min(max(int(u * tw), 0), tw - 1)
                    iy = min(max(int((1.0 - v) * th), 0), th - 1)
                    for k in range(3):
                        img[yy, xx, k] = tex[iy, ix, k] * s
                else:
                    for k in range(3):
                        img[yy, xx, k] = tri_color[t, k] * s


def render(meshes, view, size=(480, 480), fov=35.0, ortho=None, light=(0.4, 0.8, -0.45), background=(0.93, 0.93, 0.95)):
    """meshes: list of dicts {V (n,3) world, F (m,3), and color (3,) or uv (n,2) + tex (h,w,3) floats 0..1}.
    view: world -> camera 4x4 (look_at). ortho: half-height in world units for an orthographic camera."""
    w, h = size
    img = np.empty((h, w, 3))
    img[:] = background
    depth = np.full((h, w), np.inf)
    L = np.array(light, float)
    L /= np.linalg.norm(L)
    f = (h / 2) / math.tan(math.radians(fov) / 2)
    for m in meshes:
        V = np.asarray(m['V'], float)
        F = np.asarray(m['F'], np.int64)
        if len(F) == 0:
            continue
        cam = (view @ np.hstack([V, np.ones((len(V), 1))]).T).T[:, :3]
        if ortho:
            s = (h / 2) / ortho
            px = w / 2 + cam[:, 0] * s
            py = h / 2 - cam[:, 1] * s
            pz = cam[:, 2] + 1000.0
        else:
            z = np.maximum(cam[:, 2], 1e-6)
            px = w / 2 + cam[:, 0] / z * f
            py = h / 2 - cam[:, 1] / z * f
            pz = cam[:, 2]
        if m.get('cull', True):
            # Unity culls back faces: keep triangles whose normal faces the camera
            ctri = cam[F].mean(1)
            nc = np.cross(cam[F[:, 1]] - cam[F[:, 0]], cam[F[:, 2]] - cam[F[:, 0]])
            facing = np.einsum('ij,ij->i', nc, (np.zeros(3) if not ortho else np.array([0, 0, -1.0])) - (ctri if not ortho else 0)) > 0
            F = F[facing]
            if len(F) == 0:
                continue
        n = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
        ln = np.linalg.norm(n, axis=1)
        ln[ln == 0] = 1
        n /= ln[:, None]
        shade = 0.35 + 0.65 * np.clip(np.abs(n @ L), 0, 1)
        use_tex = 'tex' in m and 'uv' in m
        uv = np.asarray(m['uv'], float) if use_tex else np.zeros((len(V), 2))
        tex = np.asarray(m['tex'], float) if use_tex else np.zeros((1, 1, 3))
        color = np.tile(np.asarray(m.get('color', (0.8, 0.8, 0.8)), float), (len(F), 1))
        _raster(px, py, pz, uv, F, shade, color, tex, use_tex, img, depth)
    return np.clip(img, 0, 1)
