import math, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
sys.path.insert(0, '/home/claude/gen')
import piecekit as K

K.set_out(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/gen/out')
FONT = '/mnt/user-data/uploads/Gamejam-Girotronica-2026/Assets/Art/UI/Fonts/PatrickHand-Regular.ttf'

# Reference ruler drawn horizontally: LREF long, W wide (local units). It is used 9-sliced, so
# only the borders matter; the centre is uniform and stretches to any length / width.
LREF, W = 4.0, 1.0
CM = 0.75                     # reference units per cm (the builder scales the cm cell if this changes)
BLUE = (88, 160, 240)
RIM = (30, 88, 176)
SIDE = (52, 118, 206)
BEVEL = (206, 234, 255)
INK = (22, 36, 74)

R = 0.12
shape = K.rounded_rect(0, 0, LREF, W, R)

# ---- body (sliced)
m = 0.2
cv = K.Canvas(-m, -m, LREF + m, W + m)
mask = cv.poly_mask([shape])
body = K.plastic_body(cv, mask, BLUE, RIM, BEVEL, INK, [], seed=3, fill_alpha=0.34, rim_w=0.13,
                      ink_w=0.06, depth=(0.06, -0.09), side_col=SIDE)
bpx = int((m + 0.42) * K.PPU)
body.save('ruler_body', dict(pivot=[0.5, 0.5], border=[bpx, bpx, bpx, bpx], margin=m))

# ---- shadows and outline (sliced, bigger margin for blur)
ms = 0.7
cv2 = K.Canvas(-ms, -ms, LREF + ms, W + ms)
mask2 = cv2.poly_mask([shape])
K.shadow_layers(cv2, mask2, 'ruler', tint=(10, 28, 70))
K.outline_ring(cv2, mask2, 'ruler')
spx = int((ms + 0.45) * K.PPU)
for n in ('ruler_shadow', 'ruler_shadowSoft', 'ruler_outline'):
    K.save_meta(n, pivot=[0.5, 0.5], border=[spx, spx, spx, spx], margin=ms)

# ---- one cm cell: ticks hanging from the top edge, cell origin = the cm tick (left edge)
def draw_cell(name, n_mm, seed):
    cw = CM * n_mm / 10
    cvc = K.Canvas(0, 0, max(cw, 0.06), W)
    L = K.Layer(cvc)
    im = Image.new('L', (cvc.W, cvc.H), 0)
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(seed)
    top = W - 0.075
    for i in range(n_mm):
        x = i * CM / 10 + 0.012
        ln = 0.30 if i == 0 else (0.21 if i == 5 else 0.13)
        ln *= 1 + rng.uniform(-0.06, 0.06)
        wd = 0.024 if i == 0 else 0.017
        x0, y0 = cvc.px((x - wd / 2, top)); x1, y1 = cvc.px((x + wd / 2 + rng.uniform(-0.004, 0.004), top - ln))
        d.rounded_rectangle([x0, y0, x1, y1], radius=wd * K.SPU / 2, fill=255)
    L.over(INK, np.asarray(im, np.float32) / 255 * 0.9)
    L.save(name, dict(pivot=[0.0, 0.0]))

draw_cell('ruler_cm', 10, 11)
draw_cell('ruler_tick', 1, 12)      # lone cm tick for the last mark

# ---- numbers
font = ImageFont.truetype(FONT, int(0.36 * K.SPU))
for n in range(0, 51):
    txt = str(n)
    bbox = font.getbbox(txt)
    w = bbox[2] - bbox[0] + 8 * K.SS; h = int(0.40 * K.SPU)
    w = int(math.ceil(w / K.SS)) * K.SS
    im = Image.new('L', (w, h), 0)
    ImageDraw.Draw(im).text((4 * K.SS - bbox[0], (h - (bbox[3] + bbox[1])) // 2), txt, font=font, fill=255, stroke_width=int(0.011 * K.SPU), stroke_fill=255)
    cvn = K.Canvas(0, 0, w / K.SPU, h / K.SPU)
    cvn.W, cvn.H = w, h
    L = K.Layer(cvn)
    L.over(INK, np.asarray(im, np.float32) / 255 * 0.9)
    L.save('ruler_n%d' % n, dict(pivot=[0.5, 0.5]))

# ---- glints (white shines laid on top, placed by the builder)
def glint(name, strokes):
    cvg = K.Canvas(-0.05, -0.05, 0.75, 0.75)
    msk = cvg.poly_mask(strokes)
    L = K.Layer(cvg)
    L.over((255, 255, 255), msk * 0.85)
    L.save(name, dict(pivot=[0.5, 0.5]))

def para(cx, cy, ln, w, ang):
    dx, dy = math.cos(ang), math.sin(ang); nx, ny = -dy, dx
    return [(cx - dx * ln / 2, cy - dy * ln / 2), (cx + dx * ln / 2, cy + dy * ln / 2),
            (cx + dx * ln / 2 + nx * w * 0.4, cy + dy * ln / 2 + ny * w * 0.4), (cx - dx * ln / 2 + nx * w, cy - dy * ln / 2 + ny * w)]

a = math.radians(62)
glint('ruler_glint', [para(0.25, 0.35, 0.62, 0.12, a), para(0.47, 0.37, 0.36, 0.07, a)])

K.write_meta_json()
print('ruler ok')
