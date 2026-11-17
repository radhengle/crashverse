# L1 Tide Watchtower pagoda roofs (PM b24): the main hip roof with swept, upturned corners, rolled tile ribs, gold hip caps,
# a gold eave band and curled gold horns, split at z -0.75 into the two destructible halves (roofL / roofR), and the turret's
# two-tier roof (lower pagoda + a small top roof on posts, gold finial). Spec coordinates (TideWatchtower.cs, UNITY axes).
# Output Resources/Kingdom/Meshes/Tide_Roof.bytes: RoofL_Tile, RoofL_Gold, RoofR_Tile, RoofR_Gold, Turret_Tile, Turret_Gold, Turret_Timber.
import bpy, bmesh, math, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from tw_lib import *
OUT = sys.argv[sys.argv.index('--') + 1]
clear()

def pagoda(E, yE, T, yT, lift, horn, ribGap, ribR, goldR, name, rows=14, cols=28, thick=0.07, flat_top=True):
    """E, T = (x0, x1, z0, z1). Returns dict of material -> [objects]."""
    out = {'Tile': [], 'Gold': []}
    def side_pts(s, t, f):
        # side s: 0 = front (x0, z1->z0), 1 = right (z0, x0->x1), 2 = back (x1, z0->z1), 3 = left (z1, x1->x0)
        def P(R):
            x0, x1, z0, z1 = R
            return [((x0, z1), (x0, z0)), ((x0, z0), (x1, z0)), ((x1, z0), (x1, z1)), ((x1, z1), (x0, z1))][s]
        (ea, eb), (ta, tb) = P(E), P(T)
        ex, ez = ea[0] + (eb[0] - ea[0]) * t, ea[1] + (eb[1] - ea[1]) * t
        tx, tz = ta[0] + (tb[0] - ta[0]) * t, ta[1] + (tb[1] - ta[1]) * t
        x, z = ex + (tx - ex) * f, ez + (tz - ez) * f
        corner = max(0.0, 1.0 - min(t, 1.0 - t) / 0.3) ** 2
        y = yE + (yT - yE) * f ** 1.7 + lift * corner * (1.0 - f) ** 2.2
        return (x, y, z)
    bm = bmesh.new()
    for s in range(4):
        grid = [[bm.verts.new(U(*side_pts(s, i / cols, j / rows))) for j in range(rows + 1)] for i in range(cols + 1)]
        for i in range(cols):
            for j in range(rows): bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    if flat_top:
        x0, x1, z0, z1 = T; bm.faces.new([bm.verts.new(U(x, yT, z)) for x, z in ((x0, z1), (x1, z1), (x1, z0), (x0, z0))])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    o = obj(name + 'Surf', bm); m = o.modifiers.new('sol', 'SOLIDIFY'); m.thickness = thick; m.offset = -1.0
    fix_normals(o); out['Tile'].append(o)
    # rolled tile ribs running down each slope
    bm = bmesh.new()
    for s in range(4):
        x0, x1, z0, z1 = E; L = abs(z1 - z0) if s % 2 == 0 else abs(x1 - x0)
        n = max(3, int(L / ribGap))
        for i in range(1, n):
            t = i / n
            if min(t, 1 - t) < 0.06: continue
            pts = [Vector(side_pts(s, t, j / 8 * 0.97)) + Vector((0, ribR * 0.55, 0)) for j in range(9)]
            for a, b in zip(pts[:-1], pts[1:]): cyl(bm, a, b, ribR, 6, caps=False)
            cyl(bm, pts[0] - (pts[1] - pts[0]).normalized() * 0.02, pts[0], ribR * 1.05, 6, caps=True)  # round tile end
    o = obj(name + 'Ribs', bm); fix_normals(o); out['Tile'].append(o)
    # gold: hip caps, eave band, horns
    bm = bmesh.new()
    for s in range(4):
        hip = [Vector(side_pts(s, 0.0, j / 10)) + Vector((0, goldR * 0.8, 0)) for j in range(11)]
        for a, b in zip(hip[:-1], hip[1:]): cyl(bm, a, b, goldR, 8, caps=False)
        band = [Vector(side_pts(s, i / 24, 0.0)) + Vector((0, -0.02, 0)) for i in range(25)]
        for a, b in zip(band[:-1], band[1:]): cyl(bm, a, b, goldR * 0.75, 6, caps=False)
        # horn at the corner (t = 0): out along the diagonal, curling up
        c = Vector(side_pts(s, 0.0, 0.0)); ctr = Vector(((E[0] + E[1]) / 2, yE, (E[2] + E[3]) / 2))
        d = c - ctr; d.y = 0; d.normalize()
        hp = [c, c + d * horn * 0.35 + Vector((0, horn * 0.08, 0)), c + d * horn * 0.65 + Vector((0, horn * 0.3, 0)), c + d * horn * 0.8 + Vector((0, horn * 0.62, 0)), c + d * horn * 0.72 + Vector((0, horn * 0.85, 0))]
        hr = [goldR * 1.6, goldR * 1.3, goldR * 0.95, goldR * 0.6, goldR * 0.15]
        for k in range(4):
            seg = bmesh.new() if False else None
            cyl(bm, hp[k], hp[k + 1], (hr[k] + hr[k + 1]) / 2, 8, caps=(k == 3))
        bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=goldR * 1.7, matrix=__import__('mathutils').Matrix.Translation(U(*c)))
    o = obj(name + 'Gold', bm); fix_normals(o); out['Gold'].append(o)
    return out

def split(o, zc, keep_above):
    c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c)
    bm = bmesh.new(); bm.from_mesh(c.data)
    # unity z is blender y
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, zc, 0), plane_no=(0, 1, 0), clear_inner=keep_above, clear_outer=not keep_above)
    bm.to_mesh(c.data); bm.free(); return c

def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

objs = []
main = pagoda((-5.25, -1.15, -3.45, 1.95), 7.62, (-3.8, -2.6, -1.62, -0.38), 9.4, 0.55, 0.75, 0.21, 0.045, 0.06, 'Main')
for k, lst in main.items():
    for o in lst: apply_all(o)
    j = join(lst, 'Main' + k)
    a = split(j, -0.75, True); b = split(j, -0.75, False)
    objs.append(join([a], 'RoofL_' + k)); objs.append(join([b], 'RoofR_' + k))
    bpy.data.objects.remove(j)
tur = pagoda((-4.05, -2.35, -1.87, -0.11), 10.08, (-3.32, -3.08, -1.11, -0.87), 10.6, 0.3, 0.38, 0.17, 0.03, 0.04, 'Tur', rows=10, cols=18)
top = pagoda((-3.55, -2.85, -1.34, -0.64), 10.86, (-3.21, -3.19, -1.0, -0.98), 11.22, 0.14, 0.2, 0.15, 0.022, 0.03, 'Top', rows=8, cols=12, flat_top=False)
bm = bmesh.new()
for x in (-3.4, -3.0):
    for z in (-1.2, -0.78): box(bm, (x - 0.04, 10.55, z - 0.04), (x + 0.04, 10.9, z + 0.04))
box(bm, (-3.42, 10.55, -1.22), (-2.98, 10.62, -0.76))
tim = obj('TurTimber', bm, smooth=False); fix_normals(tim)
bm = bmesh.new(); cyl(bm, (-3.2, 11.15, -0.99), (-3.2, 11.62, -0.99), 0.035, 8)
bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.08, matrix=__import__('mathutils').Matrix.Translation(U(-3.2, 11.3, -0.99)))
bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=0.05, matrix=__import__('mathutils').Matrix.Translation(U(-3.2, 11.48, -0.99)))
fin = obj('Finial', bm); fix_normals(fin)
for k in ('Tile', 'Gold'):
    lst = tur[k] + top[k] + ([fin] if k == 'Gold' else [])
    for o in lst: apply_all(o)
    objs.append(join(lst, 'Turret_' + k))
objs.append(join([tim], 'Turret_Timber'))
for o in objs: box_uv(o, 1.2)
export(OUT, objs)
for o in objs: print(o.name, len(o.data.vertices))
if '--blend' in sys.argv: bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.bytes', '.blend'))
