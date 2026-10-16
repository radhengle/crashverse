import sys,os,bpy; sys.path.insert(0,os.path.dirname(__file__)); import rig
OUT="/workspace/wt/3d-modeler-v3/models/previews/v3"; os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="/workspace/crashverse/models_src/Island_Sunnyville/Island_Sunnyville.blend")
for o in bpy.data.objects:
    if o.name.startswith("UCX_"): o.hide_render=True
for l in list(bpy.data.lights): bpy.data.lights.remove(l)
objs=[o for o in bpy.data.objects if o.type=='MESH' and not o.hide_render]
main=[o for o in objs if o.name=="Island_Sunnyville_Mesh"]
rig.setup(96)
rig.shoot(f"{OUT}/Island_Sunnyville_gameplay.png",objs,(0.0,1.0,0.55),lens=32,fill=0.95)
rig.shoot(f"{OUT}/Island_Sunnyville_beauty.png",objs,(0.9,-0.6,0.22),lens=50,dof=True,fill=1.0,fstop=4.0)
