"""Piece sprites from the hand-drawn art (Tools/PieceArt/src): the drawing is mapped onto the piece's exact
geometry (same canvas, pivot and size as before, so the prefabs and their colliders stay valid) and the
shadows / hover outline / plastic thickness are rebuilt from its silhouette."""
import math, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import piecekit as K

SRC = sys.argv[1]
K.set_out(sys.argv[2])


def premul(path):
    a = np.asarray(Image.open(path).convert('RGBA'), np.float32) / 255.0
    a[..., :3] *= a[..., 3:4]
    return a


def warp(src, M_local_to_src, cv):
    """Samples the premultiplied source at every supersampled canvas pixel. M maps local units -> source px (2x3)."""
    j, i = np.mgrid[0:cv.H, 0:cv.W].astype(np.float32)
    lx = cv.x0 + (i + 0.5) / K.SPU
    ly = cv.y1 - (j + 0.5) / K.SPU
    sx = M_local_to_src[0, 0] * lx + M_local_to_src[0, 1] * ly + M_local_to_src[0, 2]
    sy = M_local_to_src[1, 0] * lx + M_local_to_src[1, 1] * ly + M_local_to_src[1, 2]
    out = np.zeros((cv.H, cv.W, 4), np.float32)
    for c in range(4):
        out[..., c] = ndimage.map_coordinates(src[..., c], [sy - 0.5, sx - 0.5], order=1, mode='constant', cval=0)
    return out


def affine_from_points(local_pts, src_pts):
    """2x3 matrix M with M @ (x, y, 1) = src for the three point pairs."""
    A = np.array([[x, y, 1] for x, y in local_pts], np.float64)
    S = np.array(src_pts, np.float64)
    return np.linalg.solve(A, S).T


def silhouette(pm):
    """Solid silhouette of the drawing (its fill is translucent plastic, the shape is not)."""
    m = ndimage.binary_closing(pm[..., 3] > 0.06, iterations=2)
    lab, n = ndimage.label(m)
    if n > 1:   # keep the piece, drop stray specks of the drawing
        sizes = ndimage.sum(m, lab, range(1, n + 1))
        m = lab == (1 + int(np.argmax(sizes)))
    return np.clip(ndimage.gaussian_filter(m.astype(np.float32), 0.6), 0, 1)


def to_layer(cv, pm):
    L = K.Layer(cv)
    a = pm[..., 3]
    L.rgb = np.where(a[..., None] > 1e-6, pm[..., :3] / np.maximum(a, 1e-6)[..., None], 0)
    L.a = a
    return L


def under(L, color, alpha):
    """Paints color BELOW what the layer already has."""
    c = np.array(color, np.float32) / 255.0
    alpha = np.clip(alpha, 0, 1) * (1 - L.a)
    out_a = L.a + alpha
    safe = np.maximum(out_a, 1e-6)[..., None]
    L.rgb = (L.rgb * L.a[..., None] + c * alpha[..., None]) / safe
    L.a = out_a


def plastic(name, src_file, src_tri, tri, seed):
    """src_tri / tri: (A bottom-left, B right angle, C top) in source px / local units."""
    xs = [p[0] for p in tri]; ys = [p[1] for p in tri]
    m = 0.7
    cv = K.Canvas(min(xs) - m, min(ys) - m, max(xs) + m, max(ys) + m)
    M = affine_from_points(tri, src_tri)
    pm = warp(premul(os.path.join(SRC, src_file)), M, cv)
    mask = silhouette(pm)
    pm = pm * np.clip(ndimage.binary_dilation(mask > 0.02, iterations=3), 0, 1)[..., None]
    L = to_layer(cv, pm)

    # thickness of the plastic slab (as before): the silhouette pushed down-right, darker, inked
    depth = (0.09, -0.12)
    sh = ndimage.shift(mask, (-depth[1] * K.SPU, depth[0] * K.SPU), order=1)
    side = np.clip(sh - mask, 0, 1)
    side_col = (38, 150, 82)     # same plastic edge colour as before
    ink = (40, 28, 28)
    do = K.outer_dist(mask)
    side_ink = side * np.clip((0.035 - K.outer_dist(np.clip(np.maximum(mask, sh), 0, 1))) * K.SPU + 0.5, 0, 1)
    under(L, side_col, side * 0.92)
    L.over(ink, side_ink * (mask < 0.5))
    full = np.clip(np.maximum(mask, side), 0, 1)

    piv = cv.pivot()
    L.save(name + '_body', dict(pivot=piv))
    K.shadow_layers(cv, full, name, tint=(8, 52, 26)); K.save_meta(name + '_shadow', pivot=piv); K.save_meta(name + '_shadowSoft', pivot=piv)
    K.outline_ring(cv, full, name); K.save_meta(name + '_outline', pivot=piv)
    print(name, cv.W // K.SS, cv.H // K.SS, piv)


def goma(src_file, box, height):
    """Goma: the drawing keeps its proportions, 'height' units tall, centred on the old box."""
    CX, CY, BW, BH = box
    m = 0.7
    x0, x1 = CX - BW / 2, CX + BW / 2
    y0, y1 = CY - BH / 2, CY + BH / 2
    cv = K.Canvas(x0 - m, y0 - m, x1 + m, y1 + m)          # same canvas as before
    src = premul(os.path.join(SRC, src_file))
    al = src[..., 3]
    ys, xs = np.where(al > 0.5)
    sx0, sx1, sy0, sy1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    k = height / (sy1 - sy0)                                # units per source px
    w = (sx1 - sx0) * k
    # local -> src: x_src = sx0 + (x - (CX - w/2)) / k ; y_src = sy0 + ((CY + height/2) - y) / k
    M = np.array([[1 / k, 0, sx0 - (CX - w / 2) / k], [0, -1 / k, sy0 + (CY + height / 2) / k]])
    pm = warp(src, M, cv)
    mask = silhouette(pm)
    pm = pm * np.clip(ndimage.binary_dilation(mask > 0.02, iterations=3), 0, 1)[..., None]
    L = to_layer(cv, pm)
    piv = cv.pivot()
    L.save('goma_body', dict(pivot=piv))
    K.shadow_layers(cv, mask, 'goma', tint=(30, 10, 14)); K.save_meta('goma_shadow', pivot=piv); K.save_meta('goma_shadowSoft', pivot=piv)
    K.outline_ring(cv, mask, 'goma'); K.save_meta('goma_outline', pivot=piv)
    # where the ferrule starts (first grey row along the centre column)
    col = np.asarray(Image.open(os.path.join(SRC, src_file)).convert('RGBA'))[:, (sx0 + sx1) // 2]
    fy = next(y for y in range(sy0, sy1) if col[y, 0] < 160 and abs(int(col[y, 0]) - int(col[y, 2])) < 25 and col[y, 3] > 200)
    rub_h = (fy - sy0) * k
    print('goma', cv.W // K.SS, cv.H // K.SS, piv, 'width', round(w, 4), 'height', height, 'rubber', round(rub_h, 4), 'ferrule', round(height - rub_h, 4))


def ruler(src_file):
    """Ruler body, used 9-sliced by RulerBuilder: the drawing is 1 unit wide (its long side horizontal),
    the borders (ends, hole included) keep their size and the middle stretches to any length."""
    src = premul(os.path.join(SRC, src_file))
    ys, xs = np.where(src[..., 3] > 0.3)
    sx0, sx1, sy0, sy1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    k = 1.0 / (sy1 - sy0)                       # units per source px (ruler width = 1)
    LEN = (sx1 - sx0) * k
    M = np.array([[1 / k, 0, sx0], [0, -1 / k, sy1]])   # local (x right, y up from the bottom edge) -> src px

    def render(margin):
        cv = K.Canvas(-margin, -margin, LEN + margin, 1 + margin)
        pm = warp(src, M, cv)
        mask = silhouette(pm)
        return cv, pm * np.clip(ndimage.binary_dilation(mask > 0.02, iterations=3), 0, 1)[..., None], mask

    m = 0.2
    cv, pm, mask = render(m)
    L = to_layer(cv, pm)
    depth = (0.06, -0.09)                        # plastic thickness, as before
    sh = ndimage.shift(mask, (-depth[1] * K.SPU, depth[0] * K.SPU), order=1)
    side = np.clip(sh - mask, 0, 1)
    under(L, (52, 118, 206), side * 0.92)
    side_ink = side * np.clip((0.035 - K.outer_dist(np.clip(np.maximum(mask, sh), 0, 1))) * K.SPU + 0.5, 0, 1)
    L.over((22, 36, 74), side_ink * (mask < 0.5))
    bpx = int((m + 0.42) * K.PPU)
    L.save('ruler_body', dict(pivot=[0.5, 0.5], border=[bpx, bpx, bpx, bpx], margin=m))

    ms = 0.7
    cv2, pm2, mask2 = render(ms)
    sh2 = ndimage.shift(mask2, (-depth[1] * K.SPU, depth[0] * K.SPU), order=1)
    full = np.clip(np.maximum(mask2, sh2), 0, 1)
    K.shadow_layers(cv2, full, 'ruler', tint=(10, 28, 70))
    K.outline_ring(cv2, full, 'ruler')
    spx = int((ms + 0.45) * K.PPU)
    for n in ('ruler_shadow', 'ruler_shadowSoft', 'ruler_outline'):
        K.save_meta(n, pivot=[0.5, 0.5], border=[spx, spx, spx, spx], margin=ms)
    print('ruler', cv.W // K.SS, cv.H // K.SS, 'length', round(LEN, 3), 'borders', bpx, spx)


which = sys.argv[3:] or ['cartabo', 'escuadra', 'goma', 'ruler']
if 'cartabo' in which:
    AX, AY, BX = -5.0966, -3.7706, 3.7766
    H = (BX - AX) * math.tan(math.radians(30))
    plastic('cartabo', 'cartabo.png', [(300, 953), (1510, 957), (1534, 17)], [(AX, AY), (BX, AY), (BX, AY + H)], 7)
if 'escuadra' in which:
    AX, AY, BX = -4.4, -3.6, 2.8
    plastic('escuadra', 'escaire.png', [(585, 952), (1522, 954), (1522, 19)], [(AX, AY), (BX, AY), (BX, AY + BX - AX)], 17)
if 'goma' in which:
    goma('goma.png', (1.535, 0.005, 1.36, 2.38), 2.38)
if 'ruler' in which:
    ruler('regle.png')
K.write_meta_json()
