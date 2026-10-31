import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix, noise
sys.path.insert(0, os.path.dirname(__file__))
import cvm_export
OUT = sys.argv[sys.argv.index('--')+1]
random.seed(7)
def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
def new_obj(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); return o
def lathe(bm, prof, segs=40, axis='X', at=(0,0,0), caps=True, jitter=0.0, seed=7):
    """prof = [(t, r)]; revolve around axis through 'at'. Smooth sides; caps get their own verts (hard edge)."""
    ax = Vector(at)
    def P(t, r, a):
        c, s = math.cos(a)*r, math.sin(a)*r
        if axis == 'X': return ax + Vector((t, c, s))
        if axis == 'Y': return ax + Vector((c, t, s))
        return ax + Vector((c, s, t))
    rings = []; rj = random.Random(seed)
    for (t, r) in prof:
        # jitter: hand-cast iron is never perfectly round (pitted, lumpy surface), applied per vertex
        rings.append([bm.verts.new(P(t, r * (1.0 + (rj.uniform(-jitter, jitter) if jitter else 0.0)), 2*math.pi*i/segs)) for i in range(segs)])
    for k in range(len(rings)-1):
        a, b = rings[k], rings[k+1]
        for i in range(segs):
            j = (i+1) % segs
            try: bm.faces.new((a[i], a[j], b[j], b[i]))
            except ValueError: pass
    if caps:
        for k, sign in ((0, -1), (len(prof)-1, 1)):
            t, r = prof[k]
            if r < 1e-4: continue
            vs = [bm.verts.new(P(t, r, 2*math.pi*i/segs)) for i in range(segs)]
            f = bm.faces.new(vs if sign > 0 else list(reversed(vs)))
            f.smooth = False
    return bm
def fix_normals(bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
def box(bm, c, s, rot=None):
    m = Matrix.Translation(Vector(c)) @ (rot.to_4x4() if rot else Matrix.Identity(4)) @ Matrix.Diagonal((s[0], s[1], s[2], 1))
    r = bmesh.ops.create_cube(bm, size=1.0, matrix=m)
    for f in {f for v in r['verts'] for f in v.link_faces}: f.smooth = False
def cyl(bm, c, r, h, axis='Y', segs=16, flat=False):
    rot = {'X': Matrix.Rotation(math.pi/2, 4, 'Y'), 'Y': Matrix.Rotation(math.pi/2, 4, 'X'), 'Z': Matrix.Identity(4)}[axis]
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=h, matrix=Matrix.Translation(Vector(c)) @ rot)
    if flat:
        for f in {f for v in res['verts'] for f in v.link_faces}: f.smooth = False
def sharp_caps(o):
    # mark faces whose normal is axis-aligned with the lathe axis flat -> handled via split by angle
    mod = o.modifiers.new('ea', 'EDGE_SPLIT'); mod.split_angle = math.radians(40)

# ---------------- CANNON ----------------
def cannon():
    clear(); objs = []
    navy = bmesh.new(); gold = bmesh.new(); dark = bmesh.new()
    # Concept (design/level_concepts_v1/L01): stubby, thick, barely tapered iron barrel; four heavy brass bands; a big flared muzzle bell.
    # Low-poly (28 segments). Overall extents unchanged (r <= 0.57, x -1.86..2.07) so the PlayMode framing tests still hold.
    S = 28
    # cast-iron reinforce / chase sections: many rings with per-vertex jitter (not machined-smooth), slight swells between bands
    def iron(t0, t1, r0, r1, n, sd):
        lathe(navy, [(t0 + (t1 - t0) * k / n, r0 + (r1 - r0) * k / n + 0.006 * math.sin(k * 2.3)) for k in range(n + 1)], segs=S, jitter=0.012, seed=sd)
    iron(-1.20, -0.30, 0.52, 0.52, 8, 11)
    iron(0.0, 0.88, 0.51, 0.49, 8, 12)
    iron(1.06, 1.48, 0.48, 0.47, 4, 13)
    iron(1.58, 1.62, 0.47, 0.465, 1, 14)
    lathe(gold, [(-1.40, 0.12), (-1.38, 0.44), (-1.34, 0.57), (-1.22, 0.57), (-1.18, 0.52)], segs=S)           # breech ring
    lathe(gold, [(-0.33, 0.52), (-0.29, 0.565), (-0.03, 0.565), (0.01, 0.51)], segs=S)                           # reinforce ring
    lathe(gold, [(0.86, 0.49), (0.90, 0.54), (1.04, 0.54), (1.08, 0.48)], segs=S)                                # chase band
    lathe(gold, [(1.46, 0.47), (1.49, 0.505), (1.57, 0.505), (1.60, 0.47)], segs=S)                              # astragal
    lathe(gold, [(1.62, 0.465), (1.72, 0.49), (1.84, 0.53), (1.93, 0.56), (1.96, 0.56), (1.965, 0.60), (2.065, 0.60), (2.07, 0.53)], segs=S, caps=False)  # muzzle bell + raised muzzle ring (PM QA)
    lathe(gold, [(2.07, 0.53), (2.07, 0.31)], segs=S, caps=False)
    lathe(dark, [(2.07, 0.31), (1.5, 0.31), (1.5, 0.0)], segs=S, caps=False)
    prof = [(-1.40, 0.11), (-1.50, 0.09), (-1.55, 0.15), (-1.64, 0.20), (-1.76, 0.19), (-1.83, 0.12), (-1.86, 0.0)]
    lathe(gold, prof, segs=20, caps=False)  # cascabel knob
    # trunnions + caps
    cyl(navy, (0, 0, 0), 0.13, 1.30, 'Y', 20)
    for y in (-0.66, 0.66): cyl(gold, (0, y, 0), 0.16, 0.06, 'Y', 20)
    # vent + top sight ridge
    cyl(gold, (-1.05, 0, 0.53), 0.05, 0.06, 'Z', 12)
    for b, n in ((navy, 'Barrel_Navy'), (gold, 'Barrel_Gold'), (dark, 'Barrel_Bore')):
        fix_normals(b); o = new_obj(n, b); sharp_caps(o); objs.append(o)
    cvm_export.export(OUT + '/Cannon_Barrel.cvm', objs)
    keep = list(objs)
    # carriage (root at ground centre; barrel pivot at z=1.5, x=0)
    wood = bmesh.new(); gld = bmesh.new(); iron = bmesh.new()
    for y in (-0.72, 0.72):
        # stepped cheek: front block up to the trunnion, then 2 steps down to the rear
        box(wood, (0.15, y, 0.95), (1.30, 0.28, 0.95))   # x -0.5..0.8, z 0.475..1.425 (heavy cheek)
        box(wood, (-0.75, y, 0.80), (0.55, 0.28, 0.65))
        box(wood, (-1.25, y, 0.65), (0.50, 0.28, 0.38))
        box(gld, (0.78, y, 0.95), (0.06, 0.31, 0.95))   # brass front edge plate
        # gold top trims + bolts
        box(gld, (0.15, y, 1.44), (1.30, 0.31, 0.05))
        box(gld, (-0.75, y, 1.14), (0.55, 0.31, 0.05))
        box(gld, (-1.25, y, 0.85), (0.50, 0.31, 0.05))
        for (bx, bz) in ((0.6, 1.2), (-0.3, 1.2), (0.6, 0.65), (-0.3, 0.65), (-0.75, 0.8), (-1.25, 0.6)):
            cyl(gld, (bx, y + (0.155 if y > 0 else -0.155), bz), 0.06, 0.04, 'Y', 8)
        # trunnion cap-square (iron strap)
        box(iron, (0.0, y, 1.47), (0.36, 0.26, 0.06))
    box(wood, (-0.35, 0, 0.58), (2.1, 1.22, 0.16))   # bed / transom
    box(wood, (0.72, 0, 0.95), (0.16, 1.22, 0.55))   # front transom
    box(wood, (-0.55, 0, 1.05), (0.7, 0.5, 0.18)); box(wood, (-0.70, 0, 1.18), (0.5, 0.45, 0.12))  # quoin under the breech
    # axles
    cyl(iron, (0.30, 0, 0.60), 0.08, 2.2, 'Y', 12); cyl(iron, (-1.30, 0, 0.38), 0.07, 2.0, 'Y', 12)
    # wheels: front r .6, rear r .38, spoked
    def wheel(cx, cz, r, y):
        # solid timber truck (concept): two faces + tread, iron tyre, brass hub and cap; 24 segments
        W = 24
        lathe(wood, [(0.10, r*0.22), (0.10, r*0.96)], segs=W, axis='Y', at=(cx, y, cz), caps=False)
        lathe(wood, [(-0.10, r*0.96), (-0.10, r*0.22)], segs=W, axis='Y', at=(cx, y, cz), caps=False)
        lathe(iron, [(-0.12, r*0.96), (-0.12, r*1.04), (0.12, r*1.04), (0.12, r*0.96)], segs=W, axis='Y', at=(cx, y, cz), caps=False)
        for k in range(4):  # plank seams across the disc (raised battens)
            rm = Matrix.Rotation(-k * math.pi / 4, 3, 'Y')
            box(wood, Vector((cx, y + (0.11 if y > 0 else -0.11), cz)), (r*1.8, 0.03, 0.05), rm)
        cyl(gld, (cx, y, cz), r*0.26, 0.28, 'Y', 16)
        cyl(gld, (cx, y + (0.16 if y > 0 else -0.16), cz), r*0.13, 0.06, 'Y', 12)
    for y in (-0.98, 0.98): wheel(0.30, 0.60, 0.60, y); wheel(-1.30, 0.38, 0.38, y)
    cobjs = []
    for b, n in ((wood, 'Carriage_Wood'), (gld, 'Carriage_Gold'), (iron, 'Carriage_Iron')):
        fix_normals(b); o = new_obj(n, b); sharp_caps(o); cobjs.append(o)
    cvm_export.export(OUT + '/Cannon_Carriage.cvm', cobjs)
    # preview: barrel at pivot (0,0,1.5) with 20 deg elevation
    for o in keep: o.matrix_world = Matrix.Translation((0, 0, 1.5)) @ Matrix.Rotation(math.radians(-20), 4, 'Y')
    return keep + cobjs

# ---------------- ROCKS ----------------
def rock(name, seed, sx, sy, sz, sub=3, rough=0.35):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    off = Vector((seed*13.1, seed*7.3, seed*3.7))
    for v in bm.verts:
        p = v.co.copy(); d = 1 + rough*noise.noise(p*1.3 + off) + rough*0.5*noise.noise(p*3.1 + off)
        v.co = Vector((p.x*sx*d, p.y*sy*d, max(-0.15, p.z)*sz*d))
    fix_normals(bm); o = new_obj(name, bm); sharp_caps(o); return o
def rocks():
    clear(); objs = [rock('Rock_A', 1, 1.0, 0.8, 0.6), rock('Rock_B', 2, 1.2, 1.0, 0.9), rock('Rock_C', 3, 0.7, 0.6, 0.5)]
    cvm_export.export(OUT + '/Rocks.cvm', objs); return objs

# ---------------- PALM ----------------
def palm(name, h, lean, seed):
    rnd = random.Random(seed)
    trunk = bmesh.new(); leaf = bmesh.new()
    pts = [Vector((lean*(t**1.6), 0, h*t)) for t in [i/10 for i in range(11)]]
    for i in range(10):
        a, b = pts[i], pts[i+1]; r0 = 0.26 - 0.1*i/10; r1 = 0.26 - 0.1*(i+1)/10
        d = (b - a); L = d.length; rot = Vector((0,0,1)).rotation_difference(d.normalized()).to_matrix()
        prof = [(0, r0*1.08), (L*0.15, r0), (L*0.85, r1), (L, r1*1.1)]
        tmp = bmesh.new(); lathe(tmp, prof, segs=10, axis='Z', caps=False)
        for v in tmp.verts: v.co = a + rot @ v.co
        me = bpy.data.meshes.new('t'); tmp.to_mesh(me); tmp.free(); trunk.from_mesh(me); bpy.data.meshes.remove(me)
    top = pts[-1]
    for k in range(9):
        az = k * 2*math.pi/9 + rnd.uniform(-0.2, 0.2); ln = rnd.uniform(2.4, 3.2); droop = rnd.uniform(0.9, 1.5)
        segs = 8; prev = None
        dirv = Vector((math.cos(az), math.sin(az), 0))
        side = Vector((-math.sin(az), math.cos(az), 0))
        for s in range(segs+1):
            t = s/segs
            c = top + dirv*(ln*t) + Vector((0,0, 0.5*t - droop*t*t))
            w = 0.55*math.sin(math.pi*min(1, t*1.1+0.05)) + 0.02
            vl = leaf.verts.new(c + side*w + Vector((0,0,-0.12*w))); vc = leaf.verts.new(c + Vector((0,0,0.06))); vr = leaf.verts.new(c - side*w + Vector((0,0,-0.12*w)))
            if prev:
                leaf.faces.new((prev[0], prev[1], vc, vl)); leaf.faces.new((prev[1], prev[2], vr, vc))
            prev = (vl, vc, vr)
    fix_normals(trunk); ot = new_obj(name + '_Trunk', trunk)
    lo = new_obj(name + '_Leaf', leaf); sm = lo.modifiers.new('s', 'SOLIDIFY'); sm.thickness = 0.03
    return [ot, lo]
def palms():
    clear(); objs = palm('PalmA', 6.5, 1.2, 1) + palm('PalmB', 5.2, -0.8, 2)
    cvm_export.export(OUT + '/Palms.cvm', objs); return objs

# ---------------- DISTANT ISLAND + PALACE ----------------
def island_cliff(name, rx, ry, h, seed):
    bm = bmesh.new(); segs = 28; rings = 6; rnd = random.Random(seed)
    prof = [(-6.0, 1.15), (-2, 1.08), (0, 1.0), (h*0.45, 0.93), (h*0.85, 0.86), (h, 0.80)]
    rs = []
    for (z, f) in prof:
        ring = []
        for i in range(segs):
            a = 2*math.pi*i/segs; n = 1 + 0.18*noise.noise(Vector((math.cos(a)*2+seed, math.sin(a)*2, z*0.25)))
            ring.append(bm.verts.new(Vector((math.cos(a)*rx*f*n, math.sin(a)*ry*f*n, z + 0.6*noise.noise(Vector((a*3, seed, z)))))))
        rs.append(ring)
    for k in range(len(rs)-1):
        for i in range(segs):
            j = (i+1) % segs; bm.faces.new((rs[k][i], rs[k][j], rs[k+1][j], rs[k+1][i]))
    topco = [rs[-1][i].co.copy() for i in range(segs)]
    fix_normals(bm); o = new_obj(name + '_Rock', bm)
    g = bmesh.new(); ctr = g.verts.new(Vector((0, 0, h + 0.8)))
    ring = [g.verts.new(topco[i] + Vector((0, 0, 0.15))) for i in range(segs)]
    for i in range(segs): g.faces.new((ctr, ring[i], ring[(i+1) % segs]))
    fix_normals(g); og = new_obj(name + '_Grass', g)
    return [o, og]
def tower(stone, roof, gold, x, y, z0, r, h, rr=None):
    rr = rr or r*1.25
    lathe(stone, [(z0, r), (z0+h, r), (z0+h, r*1.12), (z0+h+0.9, r*1.12)], segs=20, axis='Z', at=(x, y, 0))
    for k in range(10):
        a = k*2*math.pi/10; box(stone, (x+math.cos(a)*r*1.12, y+math.sin(a)*r*1.12, z0+h+1.2), (0.6, 0.6, 0.6))
    lathe(roof, [(z0+h+0.9, rr), (z0+h+0.9+rr*2.2, 0.02)], segs=20, axis='Z', at=(x, y, 0))
    cyl(gold, (x, y, z0+h+0.9+rr*2.2+0.4), 0.08, 0.9, 'Z', 8)
def palace():
    clear(); stone = bmesh.new(); roof = bmesh.new(); gold = bmesh.new(); flag = bmesh.new()
    box(stone, (0, 0, 7), (14, 9, 14))                       # keep
    for x in range(-6, 7, 2):
        for y in (-4.5, 4.5): box(stone, (x, y, 14.4), (0.9, 0.6, 0.9))
    lathe(roof, [(14, 0.0)], segs=4) if False else None
    rb = bmesh.ops.create_cone(roof, segments=4, radius1=9.0, radius2=0.2, depth=7, matrix=Matrix.Translation((0, 0, 17.5)) @ Matrix.Rotation(math.pi/4, 4, 'Z') @ Matrix.Diagonal((1.05, 0.72, 1, 1)))
    for (x, y, r, h) in ((-9, -6, 2.4, 18), (9, -6, 2.4, 18), (-9, 6, 2.2, 15), (9, 6, 2.2, 15), (0, 0, 2.0, 24)):
        tower(stone, roof, gold, x, y, 0, r, h)
    for (x0, x1, y) in ((-9, 9, -7.5),):
        box(stone, (0, y, 4.5), (18, 1.6, 9))
        for x in range(-8, 9, 2): box(stone, (x, y, 9.4), (0.9, 1.6, 0.9))
    box(gold, (0, -8.35, 3.2), (3.4, 0.2, 5.2))  # gate
    for (x, y, z) in ((-9, -6, 18+0.9+3+0.9), (9, -6, 22), (0, 0, 24+0.9+2.5*2.2)):
        box(flag, (x+0.9, y, z+0.7), (1.6, 0.06, 1.0))
    objs = []
    for b, n in ((stone, 'Palace_Stone'), (roof, 'Palace_Roof'), (gold, 'Palace_Gold'), (flag, 'Palace_Flag')):
        fix_normals(b); o = new_obj(n, b); sharp_caps(o); objs.append(o)
    objs += island_cliff('Crown', 30, 22, 10, 3)
    for o in objs:
        if not o.name.startswith('Crown'): o.location = (0, 0, 10.6)
    cvm_export.export(OUT + '/Palace.cvm', objs)
    isl = island_cliff('IsleA', 22, 14, 6, 5) + island_cliff('IsleB', 14, 10, 9, 8)
    cvm_export.export(OUT + '/Isles.cvm', isl)
    return objs

what = sys.argv[sys.argv.index('--')+2] if len(sys.argv) > sys.argv.index('--')+2 else 'all'
if what in ('all','cannon'): cannon()
if what in ('all','rocks'): rocks()
if what in ('all','palms'): palms()
if what in ('all','palace'): palace()
