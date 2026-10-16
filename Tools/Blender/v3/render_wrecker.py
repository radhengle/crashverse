import sys,os,bpy,math; sys.path.insert(0,os.path.dirname(__file__)); import rig
OUT="/workspace/wt/3d-modeler-v3/models/previews/v3"; os.makedirs(OUT,exist_ok=True)
SAMP=int(sys.argv[sys.argv.index("--")+1]) if "--" in sys.argv else 128
def load():
    bpy.ops.wm.open_mainfile(filepath="/workspace/crashverse/models_src/Wrecker_v2/Wrecker_v2.blend")
    for o in list(bpy.data.objects):
        if o.name.startswith("UCX_") or (o.users_collection and o.users_collection[0].name=="HP_source"): o.hide_render=True
    for l in list(bpy.data.lights): bpy.data.lights.remove(l)
    return [o for o in bpy.data.objects if o.type=='MESH' and not o.hide_render]
objs=load(); sc0=rig.setup(SAMP); rig.art_sky(sc0); rig.ground(5.5)
rig.shoot(f"{OUT}/Wrecker_v2_gameplay.png",objs,(0.8,-1.0,0.5),lens=35,fill=1.0)          # full 3/4 front (muzzle = -Y)
rig.shoot(f"{OUT}/Wrecker_v2_beauty.png",objs,(1.0,-0.55,0.28),lens=70,dof=True,fill=1.0,fstop=2.0)
tt=[]
sc=bpy.context.scene; sc.render.resolution_x=sc.render.resolution_y=720; sc.cycles.samples=max(32,SAMP//2)
for k in range(8):
    a=2*math.pi*k/8; p=f"{OUT}/tt/Wrecker_v2_tt_{k}.png"; os.makedirs(f"{OUT}/tt",exist_ok=True)
    rig.shoot(p,objs,(math.sin(a),-math.cos(a),0.35),lens=50,fill=1.05); tt.append(p)
rig.strip(tt,f"{OUT}/Wrecker_v2_turntable.png",4,360,"Wrecker_v2 turntable (8 x 45 deg) - Blender Filmic preview, final look sign-off in Unity ACES")
