# quick look-dev check: blender -b --python render_check.py -- <out.png> <blend[:dx]> ...
import sys,os,bpy,glob; sys.path.insert(0,"/workspace/wt/3d-modeler-v3/Tools/Blender/v3"); import rig
from mathutils import Vector,Matrix
A=sys.argv[sys.argv.index("--")+1:]; out=A[0]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
sc=rig.setup(64,res=(1280,720)); objs=[]
for i,spec in enumerate(A[1:]):
    p,dx=(spec.split(":")+["0"])[:2]
    with bpy.data.libraries.load(p) as (s,d): d.objects=[n for n in s.objects if not n.startswith(("UCX","JNT","ANCHOR")) and not n.endswith(("_LOD1","_LOD2"))]
    for im in bpy.data.images:
        if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
            h=glob.glob(f"/workspace/wt/3d-modeler-v3/Assets/_Project/Models/**/{os.path.basename(im.filepath)}",recursive=True)
            if h: im.filepath=h[0]; im.reload()
    for o in d.objects:
        if not o: continue
        sc.collection.objects.link(o)
    bpy.context.view_layer.update()
    for o in d.objects:
        if o and o.type=='MESH':
            W=o.matrix_world.copy(); o.parent=None; o.matrix_world=Matrix.Translation((float(dx),0,0))@W
            if "_frag_" in o.name:
                c=o.matrix_world.translation; o.location=c+Vector((c.x-float(dx),c.y,c.z-1.0))*0.18
            objs.append(o)
rig.ground(12)
rig.art_sky(sc)
rig.shoot(out,objs,(0.45,-1.0,0.42),lens=45,fill=1.25)
