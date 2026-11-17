# L1 masonry + rock kit: unit-box (-0.5..0.5) meshes that TwMesh.Kit scales into every dressed stone and boulder of the Tide
# Watchtower, so each piece keeps its destructible split but gets Blender bevels / chipped edges / faceted rock forms.
# Stone_0..5: bevelled ashlar blocks with chipped arrises and a slightly pillowed face. Rock_0..5: chunky faceted sea rocks.
# Output: Resources/Kingdom/Meshes/Tide_Kit.bytes (CVM1). UVs are re-projected at runtime (box UVs in metres).
import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix, noise
sys.path.insert(0, os.path.dirname(__file__))
from tw_lib import *
OUT = sys.argv[sys.argv.index('--') + 1]
clear()

def normalise(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    vs = o.data.vertices; lo = Vector((min(v.co[i] for v in vs) for i in range(3))); hi = Vector((max(v.co[i] for v in vs) for i in range(3)))
    c = (lo + hi) / 2; s = hi - lo
    for v in vs: v.co = Vector(((v.co.x - c.x) / s.x, (v.co.y - c.y) / s.y, (v.co.z - c.z) / s.z))

objs = []
for k in range(6):
    rnd = random.Random(100 + k)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    o = obj('Stone_%d' % k, bm)
    bv = o.modifiers.new('b', 'BEVEL'); bv.width = 0.09 + 0.03 * rnd.random(); bv.segments = 2; bv.profile = 0.6; bv.limit_method = 'NONE'
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    off = Vector((rnd.random() * 10, rnd.random() * 10, rnd.random() * 10))
    for v in o.data.vertices:
        p = v.co; edge = max(0.0, max(abs(p.x), abs(p.y), abs(p.z)) - 0.38) / 0.12   # 0 on faces, 1 at arrises
        n = noise.noise(p * 3.1 + off); chip = noise.noise(p * 7.3 + off)
        d = 0.025 * n + 0.05 * edge * max(0.0, chip)       # chipped arrises, gently uneven faces
        v.co = p - p.normalized() * d
    for p in o.data.polygons: p.use_smooth = True
    normalise(o); objs.append(o)

for k in range(6):
    rnd = random.Random(200 + k)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=0.5)
    # chunky facets: planar cuts at random orientations
    for c in range(9 + k % 3):
        nrm = Vector((rnd.uniform(-1, 1), rnd.uniform(-0.6, 1.0), rnd.uniform(-1, 1))).normalized()
        if nrm.length < 1e-3: continue
        d = rnd.uniform(0.28, 0.42)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=nrm * d, plane_no=nrm, clear_outer=True)
        edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            try: bmesh.ops.holes_fill(bm, edges=edges, sides=0)
            except Exception: pass
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    o = obj('Rock_%d' % k, bm, smooth=False)
    sd = o.modifiers.new('s', 'SUBSURF'); sd.levels = 1; sd.subdivision_type = 'SIMPLE'
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier='s')
    off = Vector((rnd.random() * 10, rnd.random() * 10, rnd.random() * 10))
    for v in o.data.vertices: v.co += v.co.normalized() * 0.035 * noise.noise(v.co * 4.0 + off)
    bv = o.modifiers.new('b', 'BEVEL'); bv.width = 0.02; bv.segments = 1; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(30); bv.harden_normals = False
    for p in o.data.polygons: p.use_smooth = True
    # keep facets crisp: mark edges sharper than 35 deg
    for e in o.data.edges: pass
    normalise(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(38))
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    objs.append(o)
export(OUT, objs)
if '--blend' in sys.argv:
    for i, o in enumerate(objs): o.location.x = (i % 6) * 1.4; o.location.y = (i // 6) * 1.6
    bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.bytes', '.blend'))
