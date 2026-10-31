import bpy, struct, sys, math
from mathutils import Vector
args = sys.argv[sys.argv.index('--')+1:]
out = args[0]; files = args[1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
cols = {'Navy': (0.05,0.09,0.22,1), 'Gold': (0.95,0.65,0.18,1), 'Bore': (0.02,0.02,0.02,1), 'Wood': (0.35,0.18,0.08,1), 'Iron': (0.12,0.12,0.13,1),
        'Rock': (0.4,0.37,0.33,1), 'Trunk': (0.45,0.3,0.18,1), 'Leaf': (0.15,0.5,0.1,1), 'Stone': (0.6,0.58,0.52,1), 'Roof': (0.6,0.1,0.06,1), 'Flag': (0.7,0.05,0.05,1), 'Grass': (0.2,0.5,0.12,1)}
def mat(tag):
    m = bpy.data.materials.new(tag); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = cols.get(tag, (0.8,0.8,0.8,1))
    b.inputs['Metallic'].default_value = 1.0 if tag in ('Gold','Navy','Iron') else 0.0
    b.inputs['Roughness'].default_value = 0.3 if tag in ('Gold','Navy') else 0.7
    return m
def load(path, offset):
    d = open(path,'rb').read(); assert d[:4] == b'CVM1'; o = 4
    n, = struct.unpack_from('<i', d, o); o += 4
    for _ in range(n):
        ln, = struct.unpack_from('<i', d, o); o += 4; name = d[o:o+ln].decode(); o += ln
        nv, ni = struct.unpack_from('<ii', d, o); o += 8
        P = struct.unpack_from('<%df' % (3*nv), d, o); o += 12*nv
        o += 12*nv + 8*nv + 4*nv
        I = struct.unpack_from('<%di' % ni, d, o); o += 4*ni
        verts = [(P[3*i]+offset[0], P[3*i+2]+offset[1], P[3*i+1]+offset[2]) for i in range(nv)]
        faces = [(I[k], I[k+2], I[k+1]) for k in range(0, ni, 3)]
        me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
        for p in me.polygons: p.use_smooth = True
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
        ob.data.materials.append(mat(name.split('_')[-1]))
        yield ob
objs = []
for f in [x for x in files if not x.startswith("cam:")]:
    p, _, off = f.partition('@'); off = tuple(float(x) for x in off.split(',')) if off else (0,0,0)
    objs += list(load(p, off))
for ob in objs:
    if ob.name.startswith('Barrel'):
        ob.rotation_euler = (0, math.radians(-25), 0); ob.location = (0, 0, 1.5)
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'
sc.render.resolution_x, sc.render.resolution_y = 540, 960
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.5,0.7,1,1); w.node_tree.nodes['Background'].inputs[1].default_value = 1.0
sun = bpy.data.lights.new('s', 'SUN'); sun.energy = 4; so = bpy.data.objects.new('s', sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co); sc.camera = co
cp = [float(x) for x in args[-1].split(':')[1].split(',')] if args[-1].startswith('cam:') else None
co.location = Vector(cp[:3]) if cp else Vector((-7, 0, 3.4)); look = Vector(cp[3:6]) if cp else Vector((4, 0, 1.0)); cam.lens = cp[6] if cp and len(cp) > 6 else 35
co.rotation_euler = (look - co.location).to_track_quat('-Z', 'Y').to_euler()
sc.render.filepath = out; bpy.ops.render.render(write_still=True)
