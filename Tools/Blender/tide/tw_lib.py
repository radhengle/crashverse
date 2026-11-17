# Shared helpers for the L1 Tide Watchtower Blender builds (Blender 4.2 LTS, run headless: blender -b -P <script> -- <out>).
# Coordinates: write everything in UNITY axes through U(x, y, z); Blender stores (x, z, y). cvm_export swaps back.
import bpy, bmesh, math, random, os, sys
from mathutils import Vector
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'kingdom'))
import cvm_export

def U(x, y, z): return Vector((x, z, y))

def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def obj(name, bm, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); return o

def bevel(o, width, segs=2, angle=35.0, harden=True):
    m = o.modifiers.new('bevel', 'BEVEL'); m.width = width; m.segments = segs
    m.limit_method = 'ANGLE'; m.angle_limit = math.radians(angle); m.harden_normals = harden
    m.profile = 0.6
    return o

def weld(o, dist=1e-4):
    m = o.modifiers.new('weld', 'WELD'); m.merge_threshold = dist; return o

def box(bm, c0, c1):
    """axis-aligned box between two UNITY-space corners."""
    (x0, y0, z0), (x1, y1, z1) = c0, c1
    vs = [bm.verts.new(U(x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    def v(i, j, k): return vs[i * 4 + j * 2 + k]
    F = [(v(0,0,0), v(0,0,1), v(0,1,1), v(0,1,0)), (v(1,0,0), v(1,1,0), v(1,1,1), v(1,0,1)),
         (v(0,0,0), v(1,0,0), v(1,0,1), v(0,0,1)), (v(0,1,0), v(0,1,1), v(1,1,1), v(1,1,0)),
         (v(0,0,0), v(0,1,0), v(1,1,0), v(1,0,0)), (v(0,0,1), v(1,0,1), v(1,1,1), v(0,1,1))]
    out = [bm.faces.new(f) for f in F]
    return out

def extrude_profile(bm, pts_xy, z0, z1):
    """Prism: a polygon in the UNITY x/y plane extruded from z0 to z1 (convex or simple concave outline, CCW)."""
    a = [bm.verts.new(U(x, y, z0)) for x, y in pts_xy]; b = [bm.verts.new(U(x, y, z1)) for x, y in pts_xy]
    n = len(pts_xy)
    bm.faces.new(a); bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n; bm.faces.new((a[i], b[i], b[j], a[j]))

def lathe(bm, prof, segs=64, axis_x=True, caps=False):
    """Revolve [(x, r)] around the UNITY x axis."""
    rings = []
    for (x, r) in prof:
        rings.append([bm.verts.new(U(x, math.cos(2 * math.pi * i / segs) * r, math.sin(2 * math.pi * i / segs) * r)) for i in range(segs)])
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        for i in range(segs):
            j = (i + 1) % segs
            try: bm.faces.new((a[i], a[j], b[j], b[i]))
            except ValueError: pass
    if caps:
        if prof[0][1] > 1e-4: bm.faces.new(list(reversed(rings[0])))
        if prof[-1][1] > 1e-4: bm.faces.new(rings[-1])
    return rings

def cyl(bm, p0, p1, r, segs=12, caps=True):
    """Cylinder between two UNITY points."""
    a, b = Vector(p0), Vector(p1); d = (b - a).normalized()
    t = d.cross(Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))).normalized(); s = d.cross(t)
    ra = [bm.verts.new(U(*(a + (t * math.cos(2*math.pi*i/segs) + s * math.sin(2*math.pi*i/segs)) * r))) for i in range(segs)]
    rb = [bm.verts.new(U(*(b + (t * math.cos(2*math.pi*i/segs) + s * math.sin(2*math.pi*i/segs)) * r))) for i in range(segs)]
    for i in range(segs):
        j = (i + 1) % segs; bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    if caps: bm.faces.new(list(reversed(ra))); bm.faces.new(rb)

def fix_normals(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.to_mesh(o.data); bm.free()

def box_uv(o, tile=1.0, grain='x'):
    """Box-projected UVs in metres/tile, in UNITY axes; wood grain (texture v) runs along `grain`."""
    me = o.data
    if not me.uv_layers: me.uv_layers.new(name='UV')
    uv = me.uv_layers.active.data
    for p in me.polygons:
        n = p.normal; nx, ny, nz = abs(n.x), abs(n.z), abs(n.y)   # unity x, y, z magnitudes
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co; x, y, z = co.x, co.z, co.y
            if nx >= ny and nx >= nz: u, v = z, y
            elif ny >= nz: u, v = (z, x) if grain == 'x' else (x, z)
            else: u, v = (y, x) if grain == 'x' else (x, y)
            uv[li].uv = (u / tile, v / tile)

def lathe_uv(o, tile=1.0):
    """Cylindrical UVs around the UNITY x axis (u = arc length, v = x)."""
    me = o.data
    if not me.uv_layers: me.uv_layers.new(name='UV')
    uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            a = math.atan2(co.y, co.z); r = math.hypot(co.y, co.z)
            uv[li].uv = (a * max(r, 0.05) / tile, co.x / tile)

def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    for o in objs:
        for m in list(o.modifiers):
            bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join(); objs[0].name = name; objs[0].data.name = name
    return objs[0]

def export(path, objs):
    cvm_export.export(path, objs)
