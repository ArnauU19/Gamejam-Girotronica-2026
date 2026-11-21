"""Comic-style plastic / rubber piece sprites for Girotronica.
Everything is drawn from geometry in local units, supersampled, then downsampled.
"""
import json, math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage

PPU = 128          # final pixels per unit
SS = 3             # supersampling
SPU = PPU * SS     # supersampled pixels per unit

OUT = None
META = {}


def set_out(path):
    global OUT
    OUT = path
    os.makedirs(path, exist_ok=True)
    p = os.path.join(path, 'pieces_import.json')
    if os.path.exists(p):
        for s in json.load(open(p))['sprites']:
            META[s.pop('name')] = s


# ---------------------------------------------------------------- canvas
class Canvas:
    """Supersampled canvas covering [x0,x1]x[y0,y1] local units (y up)."""
    def __init__(self, x0, y0, x1, y1):
        self.x0, self.y0, self.x1, self.y1 = x0, y0, x1, y1
        self.W = int(round((x1 - x0) * PPU)) * SS
        self.H = int(round((y1 - y0) * PPU)) * SS

    def px(self, p):
        return ((p[0] - self.x0) * SPU, (self.y1 - p[1]) * SPU)

    def poly_mask(self, polys_add, polys_sub=()):
        im = Image.new('L', (self.W, self.H), 0)
        d = ImageDraw.Draw(im)
        for p in polys_add:
            d.polygon([self.px(q) for q in p], fill=255)
        for p in polys_sub:
            d.polygon([self.px(q) for q in p], fill=0)
        return np.asarray(im, dtype=np.float32) / 255.0

    def pivot(self):
        return ((0 - self.x0) / (self.x1 - self.x0), (0 - self.y0) / (self.y1 - self.y0))


def rounded_rect(x0, y0, x1, y1, r, n=10):
    pts = []
    for cx, cy, a0 in ((x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180), (x1 - r, y0 + r, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def rounded_poly(pts, r, n=8):
    """Rounds the corners of a convex polygon (CCW or CW) with radius r."""
    out = []
    m = len(pts)
    for i in range(m):
        p0, p1, p2 = np.array(pts[i - 1]), np.array(pts[i]), np.array(pts[(i + 1) % m])
        a = p0 - p1; b = p2 - p1
        a /= np.linalg.norm(a); b /= np.linalg.norm(b)
        ang = math.acos(np.clip(np.dot(a, b), -1, 1))
        dist = r / math.tan(ang / 2)
        bis = (a + b); bis /= np.linalg.norm(bis)
        c = p1 + bis * (r / math.sin(ang / 2))
        s = p1 + a * dist; e = p1 + b * dist
        a0 = math.atan2(s[1] - c[1], s[0] - c[0]); a1 = math.atan2(e[1] - c[1], e[0] - c[0])
        da = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
        for k in range(n + 1):
            t = a0 + da * k / n
            out.append((c[0] + r * math.cos(t), c[1] + r * math.sin(t)))
    return out


# ---------------------------------------------------------------- fields
def inner_dist(mask):
    """Distance (in units) from each inside pixel to the shape boundary."""
    return ndimage.distance_transform_edt(mask > 0.5) / SPU


def outer_dist(mask):
    return ndimage.distance_transform_edt(mask <= 0.5) / SPU


def smooth_noise(h, w, cells, seed):
    rng = np.random.default_rng(seed)
    small = rng.random((max(2, int(h / w * cells)) + 2, cells + 2)).astype(np.float32)
    im = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255.0 * 2 - 1


def band(d, a, b, soft=0.6 / PPU):
    """1 where a <= d <= b, with soft edges."""
    return np.clip((d - a) / soft + 0.5, 0, 1) * np.clip((b - d) / soft + 0.5, 0, 1)


# ---------------------------------------------------------------- compositing
class Layer:
    def __init__(self, cv):
        self.rgb = np.zeros((cv.H, cv.W, 3), np.float32)
        self.a = np.zeros((cv.H, cv.W), np.float32)

    def over(self, color, alpha):
        """Paints color with per-pixel alpha (0..1) over the layer."""
        c = np.array(color[:3], np.float32) / 255.0
        alpha = np.clip(alpha, 0, 1)
        out_a = alpha + self.a * (1 - alpha)
        safe = np.maximum(out_a, 1e-6)[..., None]
        self.rgb = (c * alpha[..., None] + self.rgb * (self.a * (1 - alpha))[..., None]) / safe
        self.a = out_a

    def save(self, name, extra_meta=None):
        img = np.dstack([self.rgb, self.a])
        img = (np.clip(img, 0, 1) * 255).astype(np.uint8)
        im = Image.fromarray(img, 'RGBA')
        if SS > 1:
            # premultiply for clean downsampling
            arr = np.asarray(im, np.float32) / 255
            arr[..., :3] *= arr[..., 3:4]
            pm = Image.fromarray((arr * 255).astype(np.uint8), 'RGBA')
            pm = pm.resize((im.width // SS, im.height // SS), Image.LANCZOS)
            arr = np.asarray(pm, np.float32) / 255
            a = np.maximum(arr[..., 3:4], 1e-6)
            arr[..., :3] = np.where(arr[..., 3:4] > 0, arr[..., :3] / a, 0)
            im = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), 'RGBA')
        im.save(os.path.join(OUT, name + '.png'))
        META[name] = extra_meta or {}
        return im


def save_meta(name, **kw):
    META.setdefault(name, {}).update(kw)


def write_meta_json():
    with open(os.path.join(OUT, 'pieces_import.json'), 'w') as f:
        json.dump({'ppu': PPU, 'sprites': [dict(name=k, **v) for k, v in META.items()]}, f, indent=1)


# ---------------------------------------------------------------- shared pieces of a look
def plastic_body(cv, mask, tint, rim, bevel, ink, highlights, depth=(0.07, -0.08), ink_w=0.055, seed=1,
                 fill_alpha=0.42, rim_w=0.16, side_col=None):
    """Translucent comic plastic: tinted body, darker thick rim, light bevel line,
    extruded side (thickness), wobbly ink, white shines."""
    L = Layer(cv)
    di = inner_dist(mask)
    inside = mask

    # extruded side: mask shifted by depth, minus the mask -> visible thickness of the plastic slab
    sh = ndimage.shift(mask, (-depth[1] * SPU, depth[0] * SPU), order=1)
    side = np.clip(sh - mask, 0, 1)
    L.over(side_col or rim, side * 0.92)

    # body
    L.over(tint, inside * fill_alpha)
    # rim: plastic looks denser near the edges (light travels through more material)
    rim_t = np.clip(1 - di / rim_w, 0, 1) ** 1.6
    L.over(rim, inside * rim_t * 0.55)
    # light coming through the plastic: brighter band along the edges facing the light (top-left)
    gx = ndimage.sobel(ndimage.gaussian_filter(mask, 0.05 * SPU), 1)
    gy = -ndimage.sobel(ndimage.gaussian_filter(mask, 0.05 * SPU), 0)
    nrm = np.hypot(gx, gy) + 1e-6
    facing = np.clip((-gx * -0.7 + -gy * 0.7) / nrm, 0, 1)   # edges whose outward normal points up-left
    facing = ndimage.gaussian_filter(facing, 0.12 * SPU)
    glow = np.clip(1 - di / (rim_w * 2.2), 0, 1) * np.clip(facing * 3, 0, 1)
    L.over(bevel, inside * glow * 0.45)
    # bevel line
    L.over(bevel, inside * band(di, rim_w * 0.95, rim_w * 0.95 + 0.035) * 0.9)

    # shines
    for poly in highlights:
        hm = cv.poly_mask([poly]) * (di > rim_w * 1.2)
        L.over((255, 255, 255), hm * 0.85)

    # ink: wobbly line around everything (outer + side + holes)
    full = np.clip(np.maximum(mask, side), 0, 1)
    noise = smooth_noise(cv.H, cv.W, 40, seed)
    w = ink_w * (1 + 0.35 * noise)
    dfi = inner_dist(full)
    dfo = outer_dist(full)
    ink_a = np.where(full > 0.5, np.clip((w * 0.65 - dfi) * SPU / 1.2 + 0.5, 0, 1),
                     np.clip((w * 0.35 - dfo) * SPU / 1.2 + 0.5, 0, 1))
    L.over(ink, ink_a)
    # thin ink where body meets the side
    edge = np.clip(np.abs(ndimage.sobel(mask)) + np.abs(ndimage.sobel(mask, 0)), 0, 1)
    edge = ndimage.gaussian_filter(edge * side.clip(0, 1) * 0 + edge * (sh > 0.05), 0.6 * SS)
    L.over(ink, np.clip(edge * 1.2, 0, 1) * 0.6)
    return L


def shadow_layers(cv, mask, name, tint=(0, 0, 0)):
    sharp = Layer(cv)
    sharp.over(tint, ndimage.gaussian_filter(mask, 0.03 * SPU))
    sharp.save(name + '_shadow')
    soft = Layer(cv)
    soft.over(tint, ndimage.gaussian_filter(mask, 0.16 * SPU))
    soft.save(name + '_shadowSoft')


def outline_ring(cv, mask, name, width=0.09):
    do = outer_dist(mask)
    ring = Layer(cv)
    a = np.clip((width - do) * SPU / 1.5 + 0.5, 0, 1) * (mask < 0.5)
    ring.over((255, 255, 255), a)
    ring.save(name + '_outline')
