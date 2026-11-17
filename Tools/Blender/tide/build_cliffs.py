# L1 far background cliffs (design/L01_v2_spec.md §6; same footprint as TideWatchtowerEnv.Background's procedural masses):
# the right cliff (x 380-480, z -60..-170, 45-98 m, sheer face at x ~383 for the waterfall at z -116 and a plateau at ~86 for
# the castle), the left headland (z 84..175, 35-66 m) and the hill behind it. Heightfields with ridged noise and strata,
# split into Rock (steep faces) and Grass (tops, normal.y > 0.72). Output Resources/Kingdom/Meshes/Tide_Cliffs.bytes:
# Cliff_Rock, Cliff_Grass (box UVs, 9 m / 6 m tiles).
import bpy, bmesh, math, sys, os
from mathutils import Vector, noise
sys.path.insert(0, os.path.dirname(__file__))
from tw_lib import *
OUT = sys.argv[sys.argv.index('--') + 1]
clear()
def interp(pts, z):
    pts = sorted(pts)
    if z <= pts[0][0]: return pts[0][1]
    for (z0, h0), (z1, h1) in zip(pts, pts[1:]):
        if z <= z1: f = (z - z0) / (z1 - z0); f = f * f * (3 - 2 * f); return h0 + (h1 - h0) * f
    return pts[-1][1]
def sstep(a, b, x): t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

def field(name, x0, x1, z0, z1, step, hfun):
    nx, nz = int((x1 - x0) / step) + 1, int((z1 - z0) / step) + 1
    bm = bmesh.new(); grid = []
    for i in range(nx):
        row = []
        for j in range(nz):
            x = x0 + i * step; z = z0 + j * step; y = hfun(x, z)
            row.append(bm.verts.new(U(x, y, z)))
        grid.append(row)
    for i in range(nx - 1):
        for j in range(nz - 1):
            bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
    o = obj(name, bm); fix_normals(o)
    # all normals up / toward the camera (-x): flip if most face away
    up = sum(p.normal.z for p in o.data.polygons)
    if up < 0:
        bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(o.data); bm.free()
    return o

def ridged(x, z, s, oct=4):
    v = 0; a = 1; f = 1
    for _ in range(oct):
        v += a * (1 - abs(noise.noise(Vector((x * s * f, z * s * f, 3.7 * f))))); a *= 0.5; f *= 2.03
    return v / 1.875

def H(a, b): return (math.sin(a * 12.9898 + b * 78.233) * 43758.5453) % 1.0
def masses(seed, a, b, n, w0, w1, h0, h1):
    out = []
    for i in range(n):
        f = 0.5 if n == 1 else i / (n - 1)
        px = a[0] + (b[0] - a[0]) * f; pz = a[1] + (b[1] - a[1]) * f
        h = (h0 + (h1 - h0) * f) * (0.85 + 0.3 * H(seed, i)); w = w0 + (w1 - w0) * H(seed + 1, i)
        jz = (H(seed + 4, i) - 0.5) * w * 0.5
        out.append((px, pz, w * 0.62, w * 0.66, h))                                    # main mass
        out.append((px - w * 0.42, pz + jz, w * 0.40, w * 0.42, h * 0.52))              # front shoulders
        out.append((px - w * 0.30, pz - jz * 1.4, w * 0.34, w * 0.34, h * 0.66))
    return out
def mfield(ms, crag, seed):
    def f(x, z):
        best = -3.0
        for (cx, cz, rx, rz, h) in ms:
            r = math.hypot((x - cx) / rx, (z - cz) / rz)
            if r >= 1: continue
            y = h * (1 - r ** 3.2) ** 0.4
            best = max(best, y)
        if best > 0:
            best += crag * (ridged(x + seed, z, 0.045) - 0.5) * min(1.0, best / 12)
            strata = math.floor(best / 7.0) * 7.0; best = best * 0.55 + strata * 0.45
        return best
    return f
_right = mfield(masses(31, (400, -72), (440, -158), 6, 26, 46, 62, 98) + [(399, -116, 18, 11, 84)], 12, 0)  # + the waterfall buttress (face x ~381)
def right(x, z):
    h = _right(x, z)
    m = max(0.0, 1 - math.hypot((x - 425) / 17, (z - 112) / 1e9 if False else (z + 112) / 30))   # castle terrace at y 86
    m = min(1.0, m * 2.5)
    return h + (86.0 - h) * m if h > 40 or m > 0.6 else h
left = mfield(masses(41, (410, 160), (400, 84), 5, 24, 36, 40, 66), 10, 50)
hill = mfield(masses(47, (470, 140), (470, 112), 2, 22, 26, 48, 52), 5, 90)

parts = [field('R', 360, 480, -190, -40, 2.0, right), field('L', 370, 440, 60, 190, 2.0, left), field('H', 440, 500, 90, 165, 2.5, hill)]
for o in parts:
    for m in list(o.modifiers): pass
whole = join(parts, 'Cliffs')
# split steep vs flat (and drop the seabed faces)
bm = bmesh.new(); bm.from_mesh(whole.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if max(v.co.z for v in f.verts) < -1.5], context='FACES')
bm.normal_update()
grass = [f for f in bm.faces if f.normal.z > 0.32 and f.calc_center_median().z > 4.0]
gbm = bmesh.new(); vmap = {}
for f in grass:
    vs = []
    for v in f.verts:
        if v not in vmap: vmap[v] = gbm.verts.new(v.co)
        vs.append(vmap[v])
    gbm.faces.new(vs)
bmesh.ops.delete(bm, geom=grass, context='FACES_ONLY')
bm.to_mesh(whole.data); bm.free()
whole.name = 'Cliff_Rock'; whole.data.name = 'Cliff_Rock'
g = obj('Cliff_Grass', gbm)
for o in (whole, g):
    for p in o.data.polygons: p.use_smooth = True
box_uv(whole, 9.0, 'z'); box_uv(g, 6.0, 'z')
export(OUT, [whole, g])
if '--blend' in sys.argv: bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.bytes', '.blend'))
