# Debug preview of a Tide_*.blend: colours by object-name suffix, sun + sky, camera from argv (unity coords).
# blender -b X.blend -P preview.py -- out.png camx camy camz tgtx tgty tgtz fov
import bpy, math, sys
from mathutils import Vector, Euler
a = sys.argv[sys.argv.index('--') + 1:]; out = a[0]; c = [float(v) for v in a[1:8]]
U = lambda x, y, z: Vector((x, z, y))
COL = {'Navy': ((0.11, 0.12, 0.25), 0.25, 0.62, 0), 'NavyDark': ((0.235, 0.223, 0.305), 0.3, 0.6, 0), 'Gold': ((0.80, 0.55, 0.30), 0.6, 0.4, 0),
       'Sun': ((0.97, 0.89, 0.45), 0.4, 0.3, 0), 'SunDeep': ((0.93, 0.57, 0.08), 0.4, 0.35, 0), 'Bore': ((0.03, 0.03, 0.05), 0, 1, 0),
       'Wood': ((0.36, 0.20, 0.12), 0, 0.85, 0), 'Iron': ((0.12, 0.12, 0.14), 0.6, 0.45, 0), 'Hoop': ((0.3, 0.3, 0.33), 0.6, 0.4, 0),
       'Stave': ((0.75, 0.36, 0.12), 0, 0.8, 0), 'Crate': ((0.45, 0.30, 0.18), 0, 0.85, 0), 'Ball': ((0.12, 0.09, 0.1), 0.3, 0.45, 0),
       'Brass': ((0.80, 0.56, 0.24), 0.5, 0.4, 0), 'Glow': ((1, 0.9, 0.3), 0, 1, 6), 'Rope': ((0.55, 0.36, 0.2), 0, 0.9, 0),
       'Stone': ((0.5, 0.48, 0.45), 0, 0.9, 0), 'Mortar': ((0.12, 0.09, 0.08), 0, 1, 0), 'Roof': ((0.75, 0.15, 0.09), 0, 0.6, 0), 'Rock': ((0.35, 0.42, 0.5), 0, 0.9, 0), 'Grass': ((0.3, 0.55, 0.15), 0, 0.9, 0), 'Wall': ((0.93, 0.92, 0.9), 0, 0.8, 0), 'Shade': ((0.72, 0.76, 0.85), 0, 0.85, 0), 'Spire': ((0.2, 0.42, 0.85), 0, 0.6, 0), 'Window': ((0.05, 0.03, 0.03), 0, 1, 0)}
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type != 'MESH': continue
    key = o.name.split('_')[-1]; col, met, rough, em = COL.get(key, ((1, 0, 1), 0, 0.5, 0))
    m = bpy.data.materials.new(key); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Metallic'].default_value = met; b.inputs['Roughness'].default_value = rough
    if em: b.inputs['Emission Color'].default_value = (*col, 1); b.inputs['Emission Strength'].default_value = em
    o.data.materials.clear(); o.data.materials.append(m)
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.5, 0.6, 0.75, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.7
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 4; sun.rotation_euler = Euler((math.radians(50), math.radians(10), math.radians(-60)))
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.location = U(*c[0:3]); cam.data.lens_unit = 'FOV'; cam.data.angle = math.radians(c[6])
cam.rotation_euler = (U(*c[3:6]) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.view_settings.view_transform = 'Standard'
sc.render.engine = 'BLENDER_EEVEE_NEXT'; sc.render.resolution_x = 800; sc.render.resolution_y = 600; sc.render.filepath = out
bpy.ops.render.render(write_still=True)
