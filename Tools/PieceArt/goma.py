import math, sys
import numpy as np
from scipy import ndimage
sys.path.insert(0, '/home/claude/gen')
import piecekit as K

K.set_out(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/gen/out')

# Fits the prefab's BoxCollider2D: centre (1.535, 0.005), size 1.36 x 2.38 (local units). Vertical:
# pink rubber on top, crimped metal ferrule at the bottom (the end that held the pencil).
CX, CY, BW, BH = 1.535, 0.005, 1.36, 2.38
x0, x1 = CX - BW / 2, CX + BW / 2
y0, y1 = CY - BH / 2, CY + BH / 2
FER_H = 0.95
rub = K.rounded_rect(x0 + 0.04, y0 + FER_H - 0.1, x1 - 0.04, y1, 0.34)
fer = K.rounded_rect(x0, y0, x1, y0 + FER_H, 0.08)

m = 0.7
cv = K.Canvas(x0 - m, y0 - m, x1 + m, y1 + m)
mr = cv.poly_mask([rub]); mf = cv.poly_mask([fer])
mr = np.clip(mr - mf, 0, 1)
full = np.clip(mr + mf, 0, 1)

L = K.Layer(cv)
noise = K.smooth_noise(cv.H, cv.W, 14, 5)
X = (np.arange(cv.W) / K.SPU + cv.x0)[None, :].repeat(cv.H, 0)
Y = (cv.y1 - np.arange(cv.H) / K.SPU)[:, None].repeat(cv.W, 1)
u = (X - x0) / BW                       # 0 left .. 1 right

# rubber: flat pink, cel shadow on the right with a wobbly edge, a softer core shade, shines on the left
PINK, PINK_D, PINK_DD = (244, 146, 164), (214, 100, 128), (186, 76, 106)
L.over(PINK, mr)
L.over(PINK_D, mr * (u + noise * 0.025 > 0.66))
L.over(PINK_DD, mr * (u + noise * 0.02 > 0.88) * 0.8)
top_shade = np.clip((Y - (y1 - 0.16)) / 0.12, 0, 1)
L.over((255, 196, 206), mr * top_shade * (u < 0.66) * 0.7)
def stroke(pts, w):
    return cv.poly_mask([pts])
L.over((255, 255, 255), mr * cv.poly_mask([[(x0 + 0.20, y1 - 0.30), (x0 + 0.30, y1 - 0.22), (x0 + 0.30, y0 + FER_H + 0.30), (x0 + 0.20, y0 + FER_H + 0.42)]]) * 0.9)
L.over((255, 255, 255), mr * cv.poly_mask([[(x0 + 0.40, y1 - 0.20), (x0 + 0.58, y1 - 0.13), (x0 + 0.55, y1 - 0.20), (x0 + 0.40, y1 - 0.26)]]) * 0.9)

# ferrule: brushed metal, vertical light strip on the left, dark on the right, crimp grooves
MET, MET_L, MET_D = (190, 196, 206), (236, 240, 246), (118, 126, 140)
L.over(MET, mf)
L.over(MET_L, mf * np.clip(1 - np.abs(u - 0.24) / 0.09, 0, 1))
L.over(MET_D, mf * (u + noise * 0.02 > 0.74))
for gy in (y0 + 0.22, y0 + 0.40, y0 + FER_H - 0.28):
    L.over((92, 98, 112), mf * K.band(Y, gy - 0.03, gy + 0.03))
    L.over((246, 248, 252), mf * K.band(Y, gy + 0.035, gy + 0.06) * 0.8)
# rubber tucked into the ferrule: dark shadow line just above the ferrule
L.over((120, 50, 70), mr * K.band(Y, y0 + FER_H - 0.1, y0 + FER_H + 0.05) * 0.6)

# ink outline (wobbly), plus the seam between rubber and ferrule
w = 0.065 * (1 + 0.35 * K.smooth_noise(cv.H, cv.W, 30, 9))
di = K.inner_dist(full); do = K.outer_dist(full)
ink = np.where(full > 0.5, np.clip((w * 0.65 - di) * K.SPU / 1.2 + 0.5, 0, 1), np.clip((w * 0.35 - do) * K.SPU / 1.2 + 0.5, 0, 1))
seam = K.band(Y, y0 + FER_H - 0.03, y0 + FER_H + 0.015) * full
INK = (52, 32, 34)
L.over(INK, np.maximum(ink, seam))

piv = cv.pivot()
L.save('goma_body', dict(pivot=piv))
K.shadow_layers(cv, full, 'goma', tint=(30, 10, 14)); K.save_meta('goma_shadow', pivot=piv); K.save_meta('goma_shadowSoft', pivot=piv)
K.outline_ring(cv, full, 'goma'); K.save_meta('goma_outline', pivot=piv)
K.write_meta_json()
print('goma', piv)
