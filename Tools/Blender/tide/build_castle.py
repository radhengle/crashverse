# L1 far castle on the right cliff (design/L01_v2_spec.md §6; same footprint as TideWatchtowerEnv.Background: x 425, base y 52,
# z -129..-95): white round towers with crenellated parapets and flared blue conical spires with gold finials, a curtain wall
# with merlons, a keep, arrow-slit windows. Output Resources/Kingdom/Meshes/Tide_Castle.bytes: Castle_Wall, Castle_Shade,
# Castle_Spire, Castle_Window, Castle_Gold.
import bpy, bmesh, math, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from tw_lib import *
OUT = sys.argv[sys.argv.index('--') + 1]
clear()
G = {}
def add(k, o): G.setdefault(k, []).append(o)
def tower(cx, by, cz, r, h, spire=3.2):
    bm = bmesh.new(); 
    rings = lathe(bm, [(by, 0.0), (by, r * 1.05), (by + 1.0, r), (by + h, r), (by + h, r * 1.18), (by + h + 1.2, r * 1.18), (by + h + 1.2, r * 1.0), (by + h + 0.6, r * 1.0)], 20)
    o = obj('T', bm)
    # the lathe runs along unity x: rotate so it stands up (x -> y) and move to (cx, cz)
    for v in o.data.vertices:
        ux, uy, uz = v.co.x, v.co.z, v.co.y          # unity coords of the vertex (lathe axis = unity x)
        nx, ny, nz = cx + uy, ux, cz + uz            # stand it up
        v.co.x, v.co.y, v.co.z = nx, nz, ny
    fix_normals(o); add('Wall', o)
    # merlons
    bm = bmesh.new()
    for i in range(10):
        a = 2 * math.pi * i / 10; px, pz = cx + math.cos(a) * r * 1.12, cz + math.sin(a) * r * 1.12
        box(bm, (px - 0.35, by + h + 1.2, pz - 0.35), (px + 0.35, by + h + 2.0, pz + 0.35))
    o = obj('M', bm, smooth=False); fix_normals(o); add('Wall', o)
    # spire: flared cone
    bm = bmesh.new(); prof = [(0, r * 1.25), (0.15, r * 1.05)] + [(0.15 + (spire * r) * f, r * 1.0 * (1 - f) ** 1.3) for f in (0.2, 0.4, 0.6, 0.8, 1.0)]
    lathe(bm, prof, 20); o = obj('S', bm)
    for v in o.data.vertices:
        ux, uy, uz = v.co.x, v.co.z, v.co.y; v.co.x, v.co.y, v.co.z = cx + uy, cz + uz, by + h + 1.0 + ux
    fix_normals(o); add('Spire', o)
    bm = bmesh.new(); cyl(bm, (cx, by + h + 1.0 + spire * r, cz), (cx, by + h + 1.0 + spire * r + 1.6, cz), 0.08, 6); o = obj('F', bm); add('Gold', o)
    bm = bmesh.new(); cyl(bm, (cx - 0.1, by + h + 1.0 + spire * r + 1.3, cz), (cx - 0.1, by + h + 1.0 + spire * r + 1.3, cz + 1.2), 0.02, 4)
    # windows: dark slits facing the camera (-x)
    bm2 = bmesh.new()
    for k in range(3):
        y = by + h * (0.35 + 0.22 * k)
        box(bm2, (cx - r - 0.06, y, cz - 0.25), (cx - r + 0.1, y + 1.3, cz + 0.25))
    o = obj('W', bm2, smooth=False); fix_normals(o); add('Window', o)

cx, by = 425.0, 52.0   # PM b23: background lowered x0.6 (terrace at 52)
# curtain wall + keep (shaded face is the camera side, -x)
bm = bmesh.new(); box(bm, (cx - 8, by, -127), (cx + 8, by + 9, -97)); o = obj('Keepblock', bm, smooth=False); fix_normals(o); add('Shade', o)
bm = bmesh.new(); box(bm, (cx - 9, by, -126), (cx - 7.5, by + 8, -98)); o = obj('Wallfront', bm, smooth=False); fix_normals(o); add('Wall', o)
bm = bmesh.new()
for i in range(15):
    z = -125.5 + i * 1.9; box(bm, (cx - 9.1, by + 8, z), (cx - 7.6, by + 9.0, z + 1.0))
o = obj('Merlons', bm, smooth=False); fix_normals(o); add('Wall', o)
bm = bmesh.new(); box(bm, (cx - 2, by + 9, -118), (cx + 6, by + 22, -106)); o = obj('Keep', bm, smooth=False); fix_normals(o); add('Wall', o)
bm = bmesh.new()
for i in range(6): z = -117.6 + i * 2.1; box(bm, (cx - 2.1, by + 22, z), (cx - 1.2, by + 23.2, z + 1.1))
o = obj('KeepMerlons', bm, smooth=False); fix_normals(o); add('Wall', o)
bm2 = bmesh.new()
for (z, y) in ((-115, 14), (-111, 14), (-115, 18), (-111, 18), (-108, 16)):
    box(bm2, (cx - 2.1, by + y, z), (cx - 1.9, by + y + 1.6, z + 0.6))
o = obj('KeepWin', bm2, smooth=False); fix_normals(o); add('Window', o)
tz = [-129, -121, -112, -104, -95]; th = [14, 20, 27, 18, 12]
for z, h in zip(tz, th): tower(cx - 6, by + 2, z, 2.6 if h == 27 else 2.0, h)
tower(cx + 4, by + 9, -112, 1.6, 24, 3.6)
objs = [join(v, 'Castle_' + k) for k, v in G.items()]
for o in objs: box_uv(o, 4.0)
export(OUT, objs)
if '--blend' in sys.argv: bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.bytes', '.blend'))
