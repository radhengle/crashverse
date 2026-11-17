# Quick Eevee preview of Tide_Cannon.blend from the L1 gameplay camera (behind and above the breech). Debug only.
import bpy, math, sys
from mathutils import Vector, Euler
out = sys.argv[sys.argv.index('--') + 1]; elev = float(sys.argv[sys.argv.index('--') + 2]) if len(sys.argv) > sys.argv.index('--') + 2 else 10
COL = {'Navy': ((0.11, 0.12, 0.25), 0.25, 0.62), 'NavyDark': ((0.235, 0.223, 0.305), 0.3, 0.6), 'Gold': ((0.80, 0.55, 0.30), 0.6, 0.4),
       'Sun': ((0.97, 0.89, 0.45), 0.4, 0.3), 'SunDeep': ((0.93, 0.57, 0.08), 0.4, 0.35), 'Bore': ((0.03, 0.03, 0.05), 0, 1),
       'Wood': ((0.36, 0.20, 0.12), 0, 0.85), 'Iron': ((0.12, 0.12, 0.14), 0.6, 0.45)}
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type != 'MESH': continue
    key = o.name.split('_')[-1]; c, met, rough = COL.get(key, ((1, 0, 1), 0, 0.5))
    m = bpy.data.materials.new(key); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*c, 1); b.inputs['Metallic'].default_value = met; b.inputs['Roughness'].default_value = rough
    o.data.materials.clear(); o.data.materials.append(m)
    if o.name.startswith('Barrel'):
        o.rotation_euler = Euler((0, -math.radians(elev), 0)); o.location = Vector((0, 0, 1.85))
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.5, 0.6, 0.75, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.8
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 4; sun.rotation_euler = Euler((math.radians(50), math.radians(10), math.radians(-60)))
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.location = Vector((-6.0, 0, 6.25)); cam.data.lens_unit = "FOV"; cam.data.angle = math.radians(50)
cam.rotation_euler = (Vector((0.0, 0, 6.25 - 6.0 * 0.3249)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.render.engine = 'BLENDER_EEVEE_NEXT'; sc.render.resolution_x = 600; sc.render.resolution_y = 800; sc.render.filepath = out
bpy.ops.render.render(write_still=True)
