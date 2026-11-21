import math, sys
import numpy as np
sys.path.insert(0, '/home/claude/gen')
import piecekit as K

K.set_out(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/gen/out')

# Escuadra (45 / 45 / 90), local units of the prefab: right angle bottom-right, equal legs.
AX, AY = -4.4, -3.6
BX = 2.8
LEG = BX - AX
H = LEG * math.tan(math.radians(45))
A, B, C = (AX, AY), (BX, AY), (BX, AY + H)
TRI = [A, B, C]

# inner hole: triangle inset by t from every edge (same angles)
def inset(tri, t):
    p = np.array(tri)
    a = np.linalg.norm(p[1] - p[2]); b = np.linalg.norm(p[0] - p[2]); c = np.linalg.norm(p[0] - p[1])
    I = (a * p[0] + b * p[1] + c * p[2]) / (a + b + c)
    s = (a + b + c) / 2
    r = math.sqrt(s * (s - a) * (s - b) * (s - c)) / s
    return [tuple(I + (q - I) * (r - t) / r) for q in p]

HOLE_T = 1.15
outer = K.rounded_poly(TRI, 0.10)
hole = K.rounded_poly(inset(TRI, HOLE_T), 0.22)

m = 0.7
cv = K.Canvas(AX - m, AY - m, BX + m, AY + H + m)
mask = cv.poly_mask([outer], [hole])

# shines: two parallel diagonal strokes near the long edge + a small one near the right angle
def streak(cx, cy, length, width, ang):
    dx, dy = math.cos(ang), math.sin(ang)
    nx, ny = -dy, dx
    return [(cx - dx * length / 2 - nx * width / 2, cy - dy * length / 2 - ny * width / 2),
            (cx + dx * length / 2 - nx * width / 2 * 0.4, cy + dy * length / 2 - ny * width / 2 * 0.4),
            (cx + dx * length / 2 + nx * width / 2 * 0.4, cy + dy * length / 2 + ny * width / 2 * 0.4),
            (cx - dx * length / 2 + nx * width / 2, cy - dy * length / 2 + ny * width / 2)]

ang = math.radians(45) + math.pi / 2  # perpendicular to the hypotenuse
shines_old = [streak(-1.9, -2.55, 1.3, 0.16, ang), streak(-1.45, -2.45, 0.8, 0.08, ang),
          streak(3.25, -3.25, 0.28, 0.08, math.radians(45)),
          streak(3.3, 0.35, 0.55, 0.07, math.radians(90))]

def along(p, q, t0, t1, off, w):
    p, q = np.array(p), np.array(q); d = q - p; n = np.array([-d[1], d[0]]); n /= np.linalg.norm(n)
    a = p + d * t0 + n * off; b = p + d * t1 + n * off
    return [tuple(a), tuple(b), tuple(b + n * w * 0.35), tuple(a + n * w)]
# long shine following the hypotenuse (inside), a short one after a gap, two dashes at the right angle
shines = [along(A, C, 0.16, 0.50, -0.42, -0.13), along(A, C, 0.55, 0.62, -0.42, -0.13),
          along(B, C, 0.10, 0.22, 0.40, 0.09), along(B, C, 0.26, 0.30, 0.40, 0.09),
          along(A, B, 0.80, 0.90, 0.38, 0.08)]
GREEN = (72, 205, 122)
RIM = (22, 122, 66)
BEVEL = (190, 250, 196)
INK = (28, 46, 34)

body = K.plastic_body(cv, mask, GREEN, RIM, BEVEL, INK, shines, seed=17, fill_alpha=0.34, rim_w=0.22, ink_w=0.07, depth=(0.09, -0.12), side_col=(38, 150, 82))
piv = cv.pivot()
body.save('escuadra_body', dict(pivot=piv))
K.shadow_layers(cv, mask, 'escuadra', tint=(8, 52, 26)); K.save_meta('escuadra_shadow', pivot=piv); K.save_meta('escuadra_shadowSoft', pivot=piv)
K.outline_ring(cv, mask, 'escuadra'); K.save_meta('escuadra_outline', pivot=piv)
K.write_meta_json()
print('escuadra', cv.W // K.SS, cv.H // K.SS, piv, 'H', H)
