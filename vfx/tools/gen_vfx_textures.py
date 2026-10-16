"""CRASHVERSE original VFX textures (procedural, numpy + Pillow). Run from repo root: python3 vfx/tools/gen_vfx_textures.py
Deterministic (seeded). Toon look per Art: 2-3 flat value steps, clean silhouettes, no photo noise."""
import numpy as np
from PIL import Image, ImageDraw
OUT = "Assets/_Project/VFX/Textures/"
rng = np.random.default_rng(1337)
SS = 4  # supersample for clean antialiased silhouettes

def save(a, name, mode):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), mode).save(OUT + name)

def down(a, f=SS):
    h, w = a.shape[:2]
    return a.reshape(h // f, f, w // f, f, *a.shape[2:]).mean(axis=(1, 3))

def steps(v, levels=(0.5, 0.78, 1.0), cuts=(0.35, 0.68)):
    return np.where(v < cuts[0], levels[0], np.where(v < cuts[1], levels[1], levels[2]))

# 1) Toon puff flipbook 4x4 (16 frames, 128 px cells): lumpy blob grows, then erodes. Gray = value step, A = silhouette.
def puff_frame(t, seed, n=128):
    r = np.random.default_rng(seed); N = n * SS
    y, x = (np.mgrid[0:N, 0:N] + 0.5) / N * 2 - 1
    lobes = [(0, 0, 0.42)] + [(np.cos(a) * 0.32, np.sin(a) * 0.30, r.uniform(0.2, 0.3)) for a in np.linspace(0, 2 * np.pi, 7, endpoint=False) + r.uniform(0, 0.6)]
    grow = 0.55 + 0.45 * min(1, t / 0.35) ** 0.5
    sdf = np.full((N, N), 9.0); nx = np.zeros((N, N)); ny = np.zeros((N, N))
    for cx, cy, rad in lobes:
        dx, dy = x - cx * grow, y - cy * grow; d = np.sqrt(dx * dx + dy * dy); s = d - rad * grow
        m = s < sdf; sdf = np.where(m, s, sdf); nx = np.where(m, dx / (d + 1e-6), nx); ny = np.where(m, dy / (d + 1e-6), ny)
    # erosion after 45%: holes eat in from bottom-right (dissipating), silhouette stays clean
    e = max(0, (t - 0.45) / 0.55)
    sdf = sdf + e * (0.55 + 0.35 * (x + y)) * 0.9
    alpha = (sdf < 0).astype(float)
    lit = np.clip(0.5 + 0.5 * (-0.6 * nx - 0.7 * ny) * np.clip(1 + sdf * 2.2, 0, 1) + 0.35 * np.clip(-sdf * 2, 0, 1), 0, 1)
    val = steps(lit)
    return down(val), down(alpha)

cells = []
for i in range(16):
    v, a = puff_frame(i / 15, 100 + i)
    cells.append(np.dstack([v, v, v, a]))
rows = [np.concatenate(cells[r * 4:(r + 1) * 4], axis=1) for r in range(4)]
save(np.concatenate(rows, axis=0), "T_VFX_PuffFlipbook_4x4.png", "RGBA")

# 2) Spark streak (64x256): tapered capsule, white-hot core step, soft-free stepped edge. Tinted by particle colour (additive).
N = 256 * SS; W = 64 * SS
y, x = (np.mgrid[0:N, 0:W] + 0.5); y = y / N; x = (x / W) * 2 - 1
width = 0.15 + 0.85 * np.sin(np.clip(y, 0, 1) * np.pi) ** 0.6 * (1 - 0.6 * y)
d = np.abs(x) / np.maximum(width, 1e-3)
spark = np.where(d < 0.35, 1.0, np.where(d < 0.7, 0.7, np.where(d < 1.0, 0.35, 0)))
save(down(np.dstack([spark, spark, spark, (d < 1.0).astype(float)])), "T_VFX_SparkStreak.png", "RGBA")

# 3) Ring (256): stepped toon ring for explosion shockwave / joint ring. Bright inner edge, two value steps.
N = 256 * SS; y, x = (np.mgrid[0:N, 0:N] + 0.5) / N * 2 - 1; rr = np.sqrt(x * x + y * y)
band = (rr > 0.72) & (rr < 0.95)
val = np.where(rr < 0.8, 1.0, 0.7)
save(down(np.dstack([val * band, val * band, val * band, band.astype(float)])), "T_VFX_Ring.png", "RGBA")

# 4) Debris atlas 4x2 (128 px cells). Row 0: chunks/splinters (gray, 3 facet values, tinted per material).
#    Row 1: glass shards (glossy: cool facets + white glint edge, alpha 0.85 body).
def poly_cell(points_list, facet_vals, glint=False, body_alpha=1.0, n=128):
    N = n * SS; img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); dr = ImageDraw.Draw(img)
    for pts, v in zip(points_list, facet_vals):
        g = int(v * 255); dr.polygon([(px * N, py * N) for px, py in pts], fill=(g, g, g, int(body_alpha * 255)))
    if glint:
        p = points_list[0]; dr.line([(p[0][0] * N, p[0][1] * N), (p[1][0] * N, p[1][1] * N)], fill=(255, 255, 255, 255), width=int(0.05 * N))
    return down(np.asarray(img).astype(float) / 255)

def chunk(seed):
    r = np.random.default_rng(seed); c = np.array([0.5, 0.5]); k = r.integers(5, 7)
    ang = np.sort(r.uniform(0, 2 * np.pi, k)); rad = r.uniform(0.28, 0.42, k)
    ring = [tuple(c + rr * np.array([np.cos(a), np.sin(a)])) for a, rr in zip(ang, rad)]
    inner = tuple(c + r.uniform(-0.06, 0.06, 2))
    facets = [[inner, ring[i], ring[(i + 1) % k]] for i in range(k)]
    vals = [1.0 if np.sin(ang[i]) < -0.2 else (0.78 if np.cos(ang[i]) < 0.3 else 0.55) for i in range(k)]
    return facets, vals

def splinter(seed):
    r = np.random.default_rng(seed); w = r.uniform(0.07, 0.11)
    return [[(0.18, 0.82), (0.5 - w, 0.45), (0.86, 0.14)], [(0.18, 0.82), (0.86, 0.14), (0.5 + w, 0.62)]], [1.0, 0.7]

def shard(seed):
    r = np.random.default_rng(seed)
    a = (r.uniform(0.15, 0.3), r.uniform(0.1, 0.25)); b = (r.uniform(0.7, 0.9), r.uniform(0.2, 0.4)); c = (r.uniform(0.35, 0.65), r.uniform(0.8, 0.92))
    m = ((a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3)
    return [[a, b, m], [b, c, m], [c, a, m]], [1.0, 0.82, 0.62]

row0 = [poly_cell(*chunk(1)), poly_cell(*chunk(2)), poly_cell(*splinter(3)), poly_cell(*splinter(4))]
row1 = [poly_cell(*shard(10 + i), glint=True, body_alpha=0.85) for i in range(4)]
save(np.concatenate([np.concatenate(row0, axis=1), np.concatenate(row1, axis=1)], axis=0), "T_VFX_DebrisAtlas_4x2.png", "RGBA")

# 5) Wrecker muzzle flash (256): Core cyan #22D3EE body, white-hot #E0FBFF centre, gold #E8B04B rim. 6-point star + disc, baked colour.
hexc = lambda h: np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float) / 255
N = 256 * SS; y, x = (np.mgrid[0:N, 0:N] + 0.5) / N * 2 - 1; rr = np.sqrt(x * x + y * y); th = np.arctan2(y, x)
star = 0.55 + 0.4 * np.abs(np.cos(3 * th)) ** 6  # 6 chunky points
s = rr / star
col = np.where((s < 0.38)[..., None], hexc("#E0FBFF"), np.where((s < 0.8)[..., None], hexc("#22D3EE"), hexc("#E8B04B")))
a = (s < 1.0).astype(float)
save(down(np.dstack([col * a[..., None], a])), "T_VFX_MuzzleFlash_Core.png", "RGBA")

# 6) Crack mask (512, tileable-free UV space): chunky wedge cracks. R = step 1 sparse edge cracks, G = step 2 dense break (includes R).
def wedge_crack(dr, N, start, ang, length, width, r, depth=0):
    p = np.array(start, float); pts_l, pts_r = [], []; segs = r.integers(3, 5)
    for i in range(segs + 1):
        t = i / segs; w = width * (1 - t) ** 1.2 + 0.002
        nrm = np.array([-np.sin(ang), np.cos(ang)])
        pts_l.append(tuple((p + nrm * w) * N)); pts_r.append(tuple((p - nrm * w) * N))
        if i < segs:
            ang += r.uniform(-0.6, 0.6); p = p + np.array([np.cos(ang), np.sin(ang)]) * length / segs
            if depth < 1 and r.random() < 0.45:
                wedge_crack(dr, N, p, ang + r.choice([-1, 1]) * r.uniform(0.6, 1.1), length * 0.45, w * 0.7, r, depth + 1)
    dr.polygon(pts_l + pts_r[::-1], fill=255)

def crack_layer(seeds_edges, n=512):
    N = n * SS; img = Image.new("L", (N, N), 0); dr = ImageDraw.Draw(img)
    for seed, (sx, sy, ang, L, W) in seeds_edges:
        wedge_crack(dr, N, (sx, sy), ang, L, W, np.random.default_rng(seed))
    return down(np.asarray(img).astype(float) / 255)

sparse = [(1, (0.0, 0.3, 0.3, 0.35, 0.03)), (2, (1.0, 0.7, np.pi + 0.2, 0.3, 0.028)), (3, (0.6, 0.0, 1.8, 0.28, 0.025))]
dense = sparse + [(4, (0.0, 0.8, -0.4, 0.45, 0.034)), (5, (0.3, 1.0, -1.3, 0.5, 0.036)), (6, (1.0, 0.2, 2.6, 0.5, 0.034)),
                  (7, (0.5, 0.5, 0.9, 0.35, 0.03)), (8, (0.5, 0.5, -2.3, 0.35, 0.03)), (9, (0.85, 1.0, -2.0, 0.4, 0.03))]
R = crack_layer(sparse); G = np.maximum(crack_layer(dense), R)
save(np.dstack([R, G, np.zeros_like(R)]), "T_CrackMask_Wedge.png", "RGB")
print("ok")
