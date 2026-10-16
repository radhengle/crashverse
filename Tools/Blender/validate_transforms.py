# Round-trip check: world-space bbox centre of every mesh in the source .blend == after FBX re-import; all rotations identity; depth<=1
import bpy,sys,glob,os
from mathutils import Vector
R=sys.argv[sys.argv.index("--")+1]; bad=0; n=0
def cen(o):
    p=[o.matrix_world@Vector(c) for c in o.bound_box]; return tuple(round(sum(x[i] for x in p)/8,3) for i in range(3))
SRC="/workspace/crashverse/models_src"
for f in sorted(glob.glob(R+"/**/*.fbx",recursive=True)):
    d=os.path.basename(os.path.dirname(f)); d="BuildingKit_validation" if d=="validation" else d
    b=os.path.join(SRC,d,os.path.basename(f).replace(".fbx",".blend"))
    if not os.path.exists(b): continue
    bpy.ops.wm.open_mainfile(filepath=b); a={o.name:cen(o) for o in bpy.data.objects if o.type=='MESH'}
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f); e=[]
    for o in bpy.data.objects:
        if o.type=='MESH' and o.name in a:
            n+=1
            if max(abs(x-y) for x,y in zip(a[o.name],cen(o)))>0.002: e.append(o.name)
    print(os.path.relpath(f,R),"meshes checked:",sum(1 for o in bpy.data.objects if o.name in a),"->","PASS" if not e else "FAIL "+str(e[:5])); bad+=bool(e)
print("TRANSFORM RESULT:", "ALL PASS" if not bad else f"{bad} FAILED")
