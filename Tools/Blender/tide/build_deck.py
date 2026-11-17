# L1 deck props (design/L01_v2_spec.md §3, positions identical to TideWatchtowerEnv.Props): a big brass ship lantern with glowing
# panes on the rail post, a staved orange barrel with steel hoops, a plank crate of cannonballs, a rope barrel with a twisted
# 3-strand rope coil, a hanging loop and a ball. Modelled in Blender with bevels and UVs for the CC0 PBR sets.
# Output: Resources/Kingdom/Meshes/Tide_Deck.bytes, objects <Part>_<Material>; Material in Stave Crate Hoop Ball Brass Glow Rope.
# Coordinates are the environment's (UNITY world metres).
import bpy, bmesh, math, sys, os, random
sys.path.insert(0, os.path.dirname(__file__))
from tw_lib import *
OUT = sys.argv[sys.argv.index('--') + 1]
clear(); random.seed(11)
groups = {}
def add(mat, o): groups.setdefault(mat, []).append(o)

def ring_pts(cx, y, cz, r, n, fn=None):
    return [(cx + math.cos(2*math.pi*i/n) * r * (fn(i) if fn else 1), y, cz + math.sin(2*math.pi*i/n) * r * (fn(i) if fn else 1)) for i in range(n)]

def loft(bm, rings, close_bottom=False, close_top=False):
    vs = [[bm.verts.new(U(*p)) for p in r] for r in rings]; n = len(rings[0])
    for k in range(len(vs) - 1):
        for i in range(n):
            j = (i + 1) % n; bm.faces.new((vs[k][i], vs[k][j], vs[k+1][j], vs[k+1][i]))
    if close_bottom: bm.faces.new(list(reversed(vs[0])))
    if close_top: bm.faces.new(vs[-1])
    return vs

def barrel(cx, cz, h, r, stave_mat, hoop_mat, lid=True):
    """Staved barrel standing on the deck (y 0..h), bulging to r at mid height, 18 staves with grooves, 3 hoops."""
    N = 18 * 4; staves = 18; rows = 14
    def groove(i): return 1.0 - (0.022 if i % 4 == 0 else 0.0)
    bm = bmesh.new(); rings = []
    for k in range(rows + 1):
        f = k / rows; y = 0.03 + (h - 0.06) * f; rr = r * (0.84 + 0.16 * math.sin(math.pi * f))
        rings.append(ring_pts(cx, y, cz, rr, N, groove))
    # chime: staves stand proud of the head by 4 cm
    top_in = ring_pts(cx, h - 0.03, cz, r * 0.84 * 0.93, N); top_head = ring_pts(cx, h - 0.07, cz, r * 0.84 * 0.93, N)
    rings += [ring_pts(cx, h - 0.0, cz, r * 0.84 * 0.97, N)] + [top_in, top_head]
    bot = [ring_pts(cx, 0.0, cz, r * 0.84 * 0.97, N)]
    loft(bm, bot + rings, close_bottom=True, close_top=lid)
    o = obj('Barrel', bm); fix_normals(o)
    me = o.data; me.uv_layers.new(name='UV'); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co; a = math.atan2(co.y - cz, co.x - cx)
            uv[li].uv = (a * r / 0.9, co.z / 0.9) if abs(p.normal.z) < 0.7 else ((co.x - cx) / 0.9, (co.y - cz) / 0.9)
    add(stave_mat, o)
    for fy in (0.14, 0.5, 0.86) if h > 1.0 else (0.16, 0.84):
        y = h * fy; rr = r * (0.84 + 0.16 * math.sin(math.pi * fy)) + 0.012
        bm = bmesh.new(); prof = []
        for (dy, dr) in ((-0.05, -0.012), (-0.05, 0.0), (-0.04, 0.012), (0.04, 0.012), (0.05, 0.0), (0.05, -0.012)):
            prof.append(ring_pts(cx, y + dy, cz, rr + dr, 48))
        loft(bm, prof); o = obj('Hoop', bm); fix_normals(o); box_uv(o, 0.6); add(hoop_mat, o)
        # rivets
        bm = bmesh.new()
        for i in range(8):
            a = 2 * math.pi * i / 8 + 0.2; p = Vector((cx + math.cos(a) * (rr + 0.012), y, cz + math.sin(a) * (rr + 0.012)))
            bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=0.016, matrix=__import__('mathutils').Matrix.Translation(U(*p)))
        o = obj('Rivet', bm); add(hoop_mat, o)

def ball(c, r=0.17):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=r, matrix=__import__('mathutils').Matrix.Translation(U(*c)))
    o = obj('Ball', bm); box_uv(o, 0.5); add('Ball', o)

def plank_box(x0, y0, z0, x1, y1, z1, mat, bev=0.012):
    bm = bmesh.new(); box(bm, (x0, y0, z0), (x1, y1, z1)); o = obj('Plank', bm); fix_normals(o); bevel(o, bev, 2); box_uv(o, 0.8, 'z' if (z1 - z0) > (x1 - x0) else 'x'); add(mat, o); return o

# ---- ship lantern on the rail post at (-17.55, 1.30, 3.0): 0.8 m square, 1.9 m to the ring
lx, ly, lz = -17.55, 1.30, 3.0
def bbox(a, b, mat, bev=0.015):
    bm = bmesh.new(); box(bm, a, b); o = obj('L', bm); fix_normals(o); bevel(o, bev, 2); box_uv(o, 0.5); add(mat, o)
bbox((lx - 0.42, ly, lz - 0.42), (lx + 0.42, ly + 0.10, lz + 0.42), 'Brass', 0.025)          # foot plate
bbox((lx - 0.38, ly + 0.10, lz - 0.38), (lx + 0.38, ly + 0.17, lz + 0.38), 'Brass', 0.02)
bbox((lx - 0.34, ly + 0.17, lz - 0.34), (lx + 0.34, ly + 1.12, lz + 0.34), 'Glow', 0.004)     # glowing panes
for sx in (-1, 1):
    for sz in (-1, 1):
        bbox((lx + sx * 0.37 - 0.045, ly + 0.15, lz + sz * 0.37 - 0.045), (lx + sx * 0.37 + 0.045, ly + 1.16, lz + sz * 0.37 + 0.045), 'Brass', 0.012)
for yy in (0.62,):   # mid glazing bars
    for (a, b) in (((lx - 0.36, ly + yy - 0.02, lz - 0.355), (lx + 0.36, ly + yy + 0.02, lz - 0.335)), ((lx - 0.36, ly + yy - 0.02, lz + 0.335), (lx + 0.36, ly + yy + 0.02, lz + 0.355)),
                   ((lx - 0.355, ly + yy - 0.02, lz - 0.36), (lx - 0.335, ly + yy + 0.02, lz + 0.36)), ((lx + 0.335, ly + yy - 0.02, lz - 0.36), (lx + 0.355, ly + yy + 0.02, lz + 0.36))):
        bbox(a, b, 'Brass', 0.006)
for i in range(3):   # vertical bars on each face
    for (sa, ax) in ((-1, 'x'), (1, 'x'), (-1, 'z'), (1, 'z')):
        o = -0.18 + i * 0.18
        if ax == 'x': bbox((lx + sa * 0.345 - 0.012, ly + 0.17, lz + o - 0.012), (lx + sa * 0.345 + 0.012, ly + 1.12, lz + o + 0.012), 'Brass', 0.004)
        else: bbox((lx + o - 0.012, ly + 0.17, lz + sa * 0.345 - 0.012), (lx + o + 0.012, ly + 1.12, lz + sa * 0.345 + 0.012), 'Brass', 0.004)
bbox((lx - 0.44, ly + 1.12, lz - 0.44), (lx + 0.44, ly + 1.22, lz + 0.44), 'Brass', 0.025)    # cornice
bm = bmesh.new(); loft(bm, [ring_pts(lx, ly + 1.22, lz, 0.6, 4), ring_pts(lx, ly + 1.58, lz, 0.16, 4)], close_bottom=True)
o = obj('Roof', bm); o.rotation_euler = (0, 0, 0); fix_normals(o); bevel(o, 0.02, 2); box_uv(o, 0.5); add('Brass', o)
# rotate the pyramid 45 deg so it sits square on the cornice
for v in o.data.vertices:
    p = Vector((v.co.x - lx, v.co.y - lz)); c, s = math.cos(math.pi / 4), math.sin(math.pi / 4)
    v.co.x = lx + p.x * c - p.y * s; v.co.y = lz + p.x * s + p.y * c
bm = bmesh.new(); loft(bm, [ring_pts(lx, ly + 1.55, lz, 0.13, 16), ring_pts(lx, ly + 1.72, lz, 0.11, 16), ring_pts(lx, ly + 1.76, lz, 0.15, 16), ring_pts(lx, ly + 1.80, lz, 0.15, 16), ring_pts(lx, ly + 1.84, lz, 0.05, 16)], close_top=True)
o = obj('Chimney', bm); fix_normals(o); box_uv(o, 0.5); add('Brass', o)
bm = bmesh.new()
for i in range(20):   # carrying ring
    a0, a1 = math.pi * i / 20, math.pi * (i + 1) / 20
    cyl(bm, (lx, ly + 1.84 + math.sin(a0) * 0.2, lz + math.cos(a0) * 0.2), (lx, ly + 1.84 + math.sin(a1) * 0.2, lz + math.cos(a1) * 0.2), 0.025, 8, caps=False)
o = obj('Ring', bm); add('Brass', o)

# ---- orange barrel (-18.25, 2.9), cannonball crate, rope barrel (-2.95)
barrel(-18.25, 2.9, 1.1, 0.45, 'Stave', 'Hoop')
cx0, cx1, cz0, cz1 = -18.5, -17.9, 1.35, 2.4
for k in range(3):   # side boards, 3 per side with a 1 cm gap
    y0 = k * 0.15; y1 = y0 + 0.14
    plank_box(cx0, y0, cz0, cx0 + 0.04, y1, cz1, 'Crate'); plank_box(cx1 - 0.04, y0, cz0, cx1, y1, cz1, 'Crate')
    plank_box(cx0, y0, cz0, cx1, y1, cz0 + 0.04, 'Crate'); plank_box(cx0, y0, cz1 - 0.04, cx1, y1, cz1, 'Crate')
plank_box(cx0 + 0.04, 0.0, cz0 + 0.04, cx1 - 0.04, 0.30, cz1 - 0.04, 'Crate')           # inner floor raised under the balls
for (a, b) in (((cx0 - 0.02, 0, cz0 - 0.02), (cx0 + 0.06, 0.47, cz0 + 0.06)), ((cx1 - 0.06, 0, cz0 - 0.02), (cx1 + 0.02, 0.47, cz0 + 0.06)),
               ((cx0 - 0.02, 0, cz1 - 0.06), (cx0 + 0.06, 0.47, cz1 + 0.02)), ((cx1 - 0.06, 0, cz1 - 0.06), (cx1 + 0.02, 0.47, cz1 + 0.02))):
    plank_box(*a, *b, 'Crate', 0.01)
for (a, b) in (((cx0 - 0.025, 0.36, cz0 - 0.025), (cx1 + 0.025, 0.40, cz0 + 0.0)), ((cx0 - 0.025, 0.36, cz1), (cx1 + 0.025, 0.40, cz1 + 0.025)),
               ((cx0 - 0.025, 0.36, cz0), (cx0, 0.40, cz1)), ((cx1, 0.36, cz0), (cx1 + 0.025, 0.40, cz1))):
    bbox(a, b, 'Hoop', 0.005)
for i in range(3): ball((-18.2, 0.48, 1.55 + i * 0.33))
for i in range(2): ball((-18.2, 0.76, 1.71 + i * 0.33))
barrel(-18.25, -2.95, 0.95, 0.45, 'Crate', 'Hoop')

# twisted 3-strand rope: a tube along a path whose radius is modulated by a twist so the strands read
def rope(path, R=0.045, seg=12, twist=0.11):
    bm = bmesh.new(); rings = []; L = 0.0
    for k, p in enumerate(path):
        p = Vector(p)
        if k: L += (p - Vector(path[k - 1])).length
        t = (Vector(path[min(k + 1, len(path) - 1)]) - Vector(path[max(k - 1, 0)])).normalized()
        a = t.cross(Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))).normalized(); b = t.cross(a)
        ring = []
        for i in range(seg):
            th = 2 * math.pi * i / seg; rr = R * (1 + 0.16 * math.cos(3 * th - L / twist * 2 * math.pi))
            ring.append(bm.verts.new(U(*(p + (a * math.cos(th) + b * math.sin(th)) * rr))))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg; bm.faces.new((rings[k][i], rings[k][j], rings[k+1][j], rings[k+1][i]))
    o = obj('Rope', bm); fix_normals(o)
    me = o.data; me.uv_layers.new(name='UV'); uv = me.uv_layers.active.data
    n = seg
    for p in me.polygons:
        for li in p.loop_indices:
            vi = me.loops[li].vertex_index; uv[li].uv = ((vi % n) / n * 0.3, (vi // n) * 0.05)
    add('Rope', o)
coil = []
for turn in range(4):
    r = 0.37 - turn * 0.035
    for i in range(129 if turn == 3 else 128):
        a = 2 * math.pi * (turn + i / 128); coil.append((-18.25 + math.cos(a) * r, 1.0 + 0.04 + (turn + i / 128) * 0.085, -2.95 + math.sin(a) * r))
rope(coil)
loop = []
for i in range(65):
    f = i / 64; loop.append((-18.25 - 0.40 - 0.08 * math.sin(f * math.pi), 1.0 - 0.75 * math.sin(f * math.pi), -2.95 - 0.25 + f * 0.5))
rope(loop)
ball((-18.25, 1.45, -2.95))

objs = []
for mat, os_ in groups.items():
    objs.append(join(os_, 'Deck' + '_' + mat))
export(OUT, objs)
if '--blend' in sys.argv: bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.bytes', '.blend'))
