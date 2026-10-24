# Kit_Base_Indestructible (Art ruling #10): intact only, no fracture set, no crack map ever.
# Monolithic charcoal granite plinth, thick gold trim band + rivets on the top edge. LOD0/1/2 in-file, UCX hull.
import sys,os,bpy,bmesh,math; sys.path.insert(0,os.path.dirname(__file__)); from cv_lib import *
from mathutils import Vector
OUT="/workspace/wt/3d-modeler-v3/Assets/_Project/Models/BuildingKit/Kit_Base"; TX=OUT+"/textures"; os.makedirs(TX,exist_ok=True)
for k in ("Granite","Gold"): gen_tex("sheet",k,TX)
reset()
MG=material_pbr("M_Granite",f"{TX}/T_Granite_A.png",f"{TX}/T_Granite_N.png",f"{TX}/T_Granite_ORM.png")
MT=material_pbr("M_Gold",f"{TX}/T_Gold_A.png",f"{TX}/T_Gold_N.png",f"{TX}/T_Gold_ORM.png")
S=3.0; H=1.0; name="Kit_Base_Indestructible"; grp=empty(name,(0,0,0))
def uvbox(bm):
    uv=bm.loops.layers.uv.verify()
    for f in bm.faces:
        n=f.normal; ax=max(range(3),key=lambda a:abs(n[a]))
        for l in f.loops: p=l.vert.co; l[uv].uv=((p[1] if ax==0 else p[0])/2,(p[1] if ax==2 else p[2])/2)
def add(dst,b,mi):
    for f in b.faces: f.material_index=mi
    me=bpy.data.meshes.new("t"); b.to_mesh(me); b.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)
def lod(level):
    bm=bmesh.new()
    body=box_bm(S,S,H); 
    if level<2: bevel_hard(body,0.06 if level==0 else 0.05,3 if level==0 else 1)
    add(bm,body,0)
    band=box_bm(S+0.08,S+0.08,0.2); bmesh.ops.translate(band,verts=band.verts,vec=(0,0,H-0.26))   # band below the granite top edge
    if level<2: bevel_hard(band,0.03,2 if level==0 else 1)
    add(bm,band,1)
    if level==0:
        for side in range(4):
            for k in range(6):
                t=(k+0.5)/6*S-S/2; r=bmesh.new(); bmesh.ops.create_uvsphere(r,u_segments=8,v_segments=4,radius=0.045)
                for v in r.verts: v.co.y*=0.5
                rot=Matrix_rot(side); bmesh.ops.transform(r,matrix=rot,verts=r.verts)
                p=rot@Vector((t,-(S+0.08)/2-0.005,H-0.16)); bmesh.ops.translate(r,verts=r.verts,vec=p-rot@Vector((0,0,0)))
                add(bm,r,1)
    uvbox(bm); return bm
def Matrix_rot(side):
    from mathutils import Matrix; return Matrix.Rotation(side*math.pi/2,4,'Z')
for L in (0,1,2):
    o=mesh_from_bm(f"{name}_LOD{L}",lod(L),[MG,MT],grp); add_vcol(o); o["static"]=True; o["indestructible"]=True
    print("TRIS",o.name,tris(o))
u=mesh_from_bm(f"UCX_{name}_LOD0_01",box_bm(S+0.08,S+0.08,H),[],grp); u.display_type='WIRE'
add_lightmap_uv([o for o in bpy.data.objects if o.name.startswith(name+"_LOD")])
save_src("Kit_Base",name)
export(f"{OUT}/{name}.fbx","/workspace/wt/3d-modeler-v3/models/BuildingKit/Kit_Base/source/"+name+".glb")
