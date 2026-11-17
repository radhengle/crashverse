# L1 goal cannon (design/L01_v2_spec.md §2; dimensions identical to CannonDresser.DressGoal's procedural version):
# navy cast barrel with rounded gold rings, a raised 16-ray sun emblem, cascabel and trunnions; stepped bevelled timber
# cheeks with gold caps, iron straps and bolts. Output: Resources/Kingdom/Meshes/Tide_Cannon.bytes (CVM1), objects named
# <Part>_<Material> where Material is one of Navy NavyDark Gold Sun SunDeep Bore Wood Iron (CannonDresser maps them).
# Barrel objects are in barrel space (axis +x, origin on the bore axis); carriage objects in carriage space (ground y 0).
import bpy, bmesh, math, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from tw_lib import *
OUT = sys.argv[sys.argv.index('--') + 1]
clear()
S = 72  # barrel segments

def rounded_band(x0, x1, rIn, rOut, lip, n=5):
    prof = [(x0, rIn)]
    for i in range(n + 1):   # quarter round up the front edge
        a = math.pi / 2 * i / n; prof.append((x0 + lip - math.cos(a) * lip, rOut - lip + math.sin(a) * lip))
    for i in range(n + 1):
        a = math.pi / 2 * i / n; prof.append((x1 - lip + math.sin(a) * lip, rOut - lip + math.cos(a) * lip))
    prof.append((x1, rIn)); return prof

# ---- navy body: breech dome, breech, tapering chase to the muzzle
bm = bmesh.new()
prof = []
for i in range(13):  # PM b23: a deep closed rounded breech dome (axis at -2.52) behind the breech ring, so the near end reads as the breech
    a = math.pi / 2 * i / 12; prof.append((-1.98 - 0.54 * math.cos(a), 0.6 * math.sin(a) ** 0.8))
prof += [(-1.78, 0.62), (-1.2, 0.612), (-0.68, 0.60), (-0.38, 0.58), (0.0, 0.567), (0.55, 0.55), (0.78, 0.535), (1.3, 0.527), (1.80, 0.52), (1.99, 0.52)]
prof[0] = (prof[0][0], 0.0)
lathe(bm, prof, S)
navy = obj('Barrel_Navy', bm); lathe_uv(navy, 0.8)

# ---- gold rings (thick, raised, rounded lips) + muzzle swell with face and bore
objs = []
for (x0, x1, ri, ro) in ((-2.00, -1.66, 0.58, 0.72), (-0.72, -0.34, 0.56, 0.70), (0.50, 0.82, 0.515, 0.63), (1.76, 2.04, 0.50, 0.62)):
    bm = bmesh.new(); lathe(bm, rounded_band(x0, x1, ri, ro, 0.045), S); objs.append(obj('Band', bm))
bm = bmesh.new(); lathe(bm, [(2.04, 0.58), (2.06, 0.56), (2.06, 0.38), (2.03, 0.36)], S); objs.append(obj('Face', bm))
# fillet beads either side of each ring
for x in (-1.62, -0.76, -0.30, 0.46, 0.86, 1.72):
    r = 0.62 - (x + 1.78) * 0.027 if x < 0 else 0.567 - x * 0.03
    bm = bmesh.new(); lathe(bm, [(x - 0.02, r - 0.005)] + [(x - 0.02 * math.cos(math.pi * i / 6), r + 0.022 * math.sin(math.pi * i / 6)) for i in range(7)] + [(x + 0.02, r - 0.005)], S); objs.append(obj('Bead', bm))
# cascabel neck + knob
bm = bmesh.new(); lathe(bm, [(-2.47, 0.19), (-2.53, 0.17), (-2.60, 0.10), (-2.63, 0.10)] + [(-2.77 - 0.15 * math.cos(math.pi * i / 12), 0.15 * math.sin(math.pi * i / 12)) for i in range(13)], 32)
objs.append(obj('Knob', bm))
gold = join(objs, 'Barrel_Gold'); lathe_uv(gold, 0.8)

bm = bmesh.new(); lathe(bm, [(2.035, 0.36), (1.4, 0.36), (1.4, 0.0)], 32); bore = obj('Barrel_Bore', bm)
# breech ring groove (dark)
bm = bmesh.new(); lathe(bm, [(-1.855, 0.718), (-1.85, 0.724), (-1.81, 0.724), (-1.805, 0.718)], S); groove = obj('Barrel_NavyDark', bm)
# trunnions (navy) out to the cheeks
bm = bmesh.new()
for sz in (-1, 1): cyl(bm, (0, -0.2, sz * 0.5), (0, -0.2, sz * 0.74), 0.13, 20)
trun = obj('Trunnion', bm); bevel(trun, 0.02, 2)
navyAll = join([navy, trun], 'Barrel_Navy')

# ---- sun emblem: raised domed disc and 16 bevelled rays conforming to the barrel top at x -1.25
def on_barrel(x, z, h):
    r = 0.612 + h; y = math.sqrt(max(0.0, r * r - z * z)); return U(x, y, z)
cx = -1.25
bm = bmesh.new()
rings = []
for k, (rr, h) in enumerate(((0.0, 0.05), (0.08, 0.046), (0.15, 0.036), (0.2, 0.022), (0.205, 0.0))):
    if rr == 0: rings.append([bm.verts.new(on_barrel(cx, 0, h))]); continue
    rings.append([bm.verts.new(on_barrel(cx + math.cos(2*math.pi*i/32) * rr, math.sin(2*math.pi*i/32) * rr, h)) for i in range(32)])
for i in range(32): bm.faces.new((rings[0][0], rings[1][(i+1) % 32], rings[1][i]))
for k in range(1, len(rings) - 1):
    a, b = rings[k], rings[k + 1]
    for i in range(32): bm.faces.new((a[i], a[(i+1) % 32], b[(i+1) % 32], b[i]))
sun = obj('Barrel_Sun', bm)
bmL = bmesh.new(); bmS = bmesh.new()
for i in range(16):
    a = 2 * math.pi * i / 16; d = (math.cos(a), math.sin(a)); s = (-d[1], d[0])
    ln = 0.42 if i % 2 == 0 else 0.30; w = 0.07 if i % 2 == 0 else 0.055
    tgt = bmL if i % 2 == 0 else bmS
    base = [(0.19 * d[0] - w * s[0], 0.19 * d[1] - w * s[1]), (ln * d[0], ln * d[1]), (0.19 * d[0] + w * s[0], 0.19 * d[1] + w * s[1])]
    top = [tgt.verts.new(on_barrel(cx + p[0], p[1], 0.022 if j != 1 else 0.008)) for j, p in enumerate(base)]
    bot = [tgt.verts.new(on_barrel(cx + p[0], p[1], -0.01)) for p in base]
    tgt.faces.new(top); tgt.faces.new(list(reversed(bot)))
    for j in range(3):
        k = (j + 1) % 3; tgt.faces.new((bot[j], bot[k], top[k], top[j]))
raysL = obj('RaysL', bmL, smooth=False); raysS = obj('Barrel_SunDeep', bmS, smooth=False)
sunAll = join([sun, raysL], 'Barrel_Sun')
for o in (navyAll, gold, bore, groove, sunAll, raysS): fix_normals(o)

# ---- carriage: stepped cheeks (one bevelled prism each), gold caps, iron straps + bolts, transoms, quoin
wood, cgold, ciron, cnavy = [], [], [], []
step = [(-1.55, 0.0), (1.0, 0.0), (1.0, 1.62), (-0.35, 1.62), (-0.35, 1.2), (-0.95, 1.2), (-0.95, 0.85), (-1.55, 0.85)]
for sz in (-1, 1):
    zl, zh = sorted((sz * 0.7, sz * 1.3))
    bm = bmesh.new(); extrude_profile(bm, step, zl, zh); o = obj('Cheek', bm); fix_normals(o); bevel(o, 0.035, 3); wood.append(o)
    for (x0, x1, y) in ((-0.35, 1.0, 1.62), (-0.95, -0.35, 1.2), (-1.55, -0.95, 0.85)):
        # PM b23: gold only as thin trim along the step edges (the cheeks themselves read as dark wood)
        for (za, zb) in ((zl - 0.02, zl + 0.05), (zh - 0.05, zh + 0.02)):
            bm = bmesh.new(); box(bm, (x0 - 0.02, y - 0.03, za), (x1 + 0.02, y + 0.035, zb)); o = obj('Cap', bm); fix_normals(o); bevel(o, 0.01, 2); cgold.append(o)
        bm = bmesh.new(); box(bm, (x0 - 0.03, y - 0.06, zl - 0.02), (x0 + 0.02, y + 0.035, zh + 0.02)); o = obj('Riser', bm); fix_normals(o); bevel(o, 0.008, 2); cgold.append(o)
    bm = bmesh.new(); box(bm, (-0.22, 1.58, zl - 0.035), (0.22, 1.72, zh + 0.035)); o = obj('Strap', bm); fix_normals(o); bevel(o, 0.015, 2); ciron.append(o)
    bm = bmesh.new(); box(bm, (-1.5, 0.30, zl - 0.02), (0.95, 0.38, zh + 0.02)); o = obj('Band', bm); fix_normals(o); bevel(o, 0.01, 2); ciron.append(o)
    bm = bmesh.new()
    for k in range(5):
        bx = -1.35 + k * 0.55
        for zz in (zl - 0.02, zh + 0.02):
            cyl(bm, (bx, 0.34, zz - 0.012 if zz < 0 else zz), (bx, 0.34, zz + 0.012 if zz > 0 else zz), 0.045, 10)
    for k in range(3):
        bx = -1.2 + k * 0.9
        for zz in (zl - 0.015, zh + 0.015):
            cyl(bm, (bx, 0.95 if bx < 0 else 1.3, zz - 0.012 if zz < 0 else zz), (bx, 0.95 if bx < 0 else 1.3, zz + 0.012 if zz > 0 else zz), 0.05, 10)
    o = obj('Bolts', bm); fix_normals(o); bevel(o, 0.008, 1); ciron.append(o)
    bm = bmesh.new(); box(bm, (0.6, 0.0, zl - 0.1), (1.08, 0.36, zh + 0.1)); o = obj('Block', bm); fix_normals(o); bevel(o, 0.03, 3); wood.append(o)
    bm = bmesh.new(); box(bm, (-1.72, 0.0, zl), (-1.3, 0.5, zh)); o = obj('Chock', bm); fix_normals(o); bevel(o, 0.03, 3); wood.append(o)
for (a, b) in (((-1.5, 0.15, -0.72), (-1.0, 0.5, 0.72)), ((0.3, 0.15, -0.72), (0.9, 0.6, 0.72))):
    bm = bmesh.new(); box(bm, a, b); o = obj('Transom', bm); fix_normals(o); bevel(o, 0.03, 3); wood.append(o)
bm = bmesh.new(); box(bm, (-0.9, 0.5, -0.7), (-0.5, 0.75, 0.7)); o = obj('Quoin', bm); fix_normals(o); bevel(o, 0.025, 2); cnavy.append(o)
for wx in (-1.15, 0.62):
    for sz in (-1, 1):
        zc = sz * 1.40; r = 0.34
        bm = bmesh.new(); cyl(bm, (wx, r, zc - 0.08), (wx, r, zc + 0.08), r, 28); o = obj('Wheel', bm); fix_normals(o); bevel(o, 0.03, 2); wood.append(o)
        bm = bmesh.new(); cyl(bm, (wx, r, zc - 0.085), (wx, r, zc + 0.085), r + 0.025, 28); cyl(bm, (wx, r, zc - 0.07), (wx, r, zc + 0.07), r + 0.026, 28)
        o = obj('Tyre', bm); fix_normals(o); ciron.append(o)
        bm = bmesh.new(); cyl(bm, (wx, r, zc - 0.13), (wx, r, zc + 0.13), 0.09, 16); o = obj('Hub', bm); fix_normals(o); bevel(o, 0.02, 2); cgold.append(o)
        bm = bmesh.new(); cyl(bm, (wx, r, -1.5), (wx, r, 1.5), 0.05, 10); o = obj('Axle', bm); fix_normals(o); ciron.append(o)
cw = join(wood, 'Carriage_Wood'); box_uv(cw, 0.9, 'x')
cg = join(cgold, 'Carriage_Gold'); box_uv(cg, 0.8)
ci = join(ciron, 'Carriage_Iron'); box_uv(ci, 0.8)
cn = cnavy[0]; bpy.context.view_layer.objects.active = cn; bpy.ops.object.modifier_apply(modifier='bevel'); cn.name = 'Carriage_NavyDark'; box_uv(cn, 0.8)
barrel = [navyAll, gold, bore, groove, sunAll, raysS]
for o in barrel:
    for m in list(o.modifiers): bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
# PM b25 QA (b26): the goal's barrel is FAT and short on screen (~30% of the width at the breech, thick gold rings): 1.25x radius,
# 0.92x length about the trunnion axis; PM b26 QA (b27): radius 0.85x of b26 (1.25 -> 1.0625); carriage 1.05x sideways.
from mathutils import Matrix
for o in barrel: o.data.transform(Matrix.Diagonal((0.92, 1.0625, 1.0625, 1.0)))
for o in (cw, cg, ci, cn): o.data.transform(Matrix.Diagonal((1.0, 1.05, 1.0, 1.0)))
export(OUT, barrel + [cw, cg, ci, cn])
if '--blend' in sys.argv: bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.bytes', '.blend'))
