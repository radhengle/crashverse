# Wrecker_v2 + Island_Sunnyville. Convention: Blender -Y = forward (-> Unity +Z with FBX fwd -Z/up Y), +Z up, 1u=1m
import sys,os,bpy,bmesh,math,random; sys.path.insert(0,os.path.dirname(__file__)); from cv_lib import *
from mathutils import Vector, Matrix
M_ROOT="/workspace/wt/3d-modeler/models"
def bm_cyl(r1,r2,depth,seg,cellname,axis='Y',center=(0,0,0)):
    bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=True,segments=seg,radius1=r1,radius2=r2,depth=depth)
    R={'Y':Matrix.Rotation(math.radians(90),4,'X'),'X':Matrix.Rotation(math.radians(90),4,'Y'),'Z':Matrix()}[axis]
    bmesh.ops.transform(bm,matrix=Matrix.Translation(center)@R,verts=bm.verts); paint(bm,lambda f:cellname); return bm
def merge(dst,src):
    me=bpy.data.meshes.new("t"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)
def bm_box(c,s,cellname):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector((v.co.x*s[0]+c[0],v.co.y*s[1]+c[1],v.co.z*s[2]+c[2]))
    paint(bm,lambda f:cellname); return bm
def setup(asset):
    d=os.path.join(M_ROOT,asset); t=os.path.join(d,"textures"); os.makedirs(t,exist_ok=True)
    p=os.path.join(t,"T_Palette_256.png"); write_palette(p); return d,p
def ucx(name,bm,parent,loc=(0,0,0)):
    o=mesh_from_bm(name,bm,[],root,loc); o["attach_to"]=parent.name; o.display_type='WIRE'; return o
def A(a): return -a   # "along forward" distance -> Blender Y

# ---------------- Wrecker_v2 ----------------
reset(); d,pal=setup("Wrecker_v2"); M=material("M_Palette",pal,0.3,0.0)
root=empty("Wrecker_v2")
# carriage: wooden wedge, rear low (0.55) -> front high (1.35), x along -1.5..1.0
bm=bmesh.new(); W=0.8
prof=[(-1.5,0.25),(1.0,0.25),(1.0,1.35),(0.2,1.35),(-1.5,0.55)]
for sx in (-W,W):
    for a,z in prof: bm.verts.new((sx,A(a),z))
bm.verts.ensure_lookup_table(); n=len(prof)
bm.faces.new([bm.verts[i] for i in range(n)]); bm.faces.new([bm.verts[n+i] for i in reversed(range(n))])
for i in range(n):
    j=(i+1)%n; bm.faces.new([bm.verts[j],bm.verts[i],bm.verts[n+i],bm.verts[n+j]])
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bmesh.ops.bevel(bm,geom=bm.edges[:],offset=0.04,segments=1,affect='EDGES'); paint(bm,lambda f:"wood")
for sx in (-W-0.01,W+0.01):                      # gold rivets (low-poly studs)
    for a,z in ((-1.1,0.45),(-0.4,0.45),(0.6,0.45),(0.6,1.1)): merge(bm,bm_cyl(0.06,0.06,0.04,6,"gold",'X',(sx,A(a),z)))
merge(bm,bm_box((0,A(0.15),0.6),(1.5,0.3,0.3),"steel_dk"))  # axle housing
car=mesh_from_bm("Carriage",bm,[M],root); ucx("UCX_Carriage_01",bm_box((0,A(-0.25),0.8),(1.6,2.5,1.1),"wood"),car)
def wheel(name,x,a,r,big):
    bm=bm_cyl(r,r,0.24,16,"wood" if big else "outline",'X')
    if big:
        merge(bm,bm_cyl(r+0.04,r+0.04,0.18,16,"steel",'X'))   # steel rim
        merge(bm,bm_cyl(0.16,0.16,0.34,8,"gold",'X'))          # gold hub
        for k in range(4):                                      # raised spokes
            s=bm_box((0,0,0),(0.28,0.08,2*r*0.85),"wood_dk"); bmesh.ops.rotate(s,verts=s.verts,cent=(0,0,0),matrix=Matrix.Rotation(k*math.pi/4,3,'X')); merge(bm,s)
    else: merge(bm,bm_cyl(0.1,0.1,0.3,8,"steel",'X'))
    o=mesh_from_bm(name,bm,[M],root,(x,A(a),r)); return o
wheel("Wheel_FL",0.95,0.15,0.6,True); wheel("Wheel_FR",-0.95,0.15,0.6,True)   # left = Blender +X when facing -Y
wheel("Wheel_RL",0.6,-1.35,0.35,False); wheel("Wheel_RR",-0.6,-1.35,0.35,False)
yaw=root; PV=(0,0,1.5)  # barrel pivot; Barrel does yaw(+/-60) + pitch(5-70)
bm=bm_cyl(0.42,0.34,2.3,16,"navy",'Y',(0,A(0.45),0))                            # tapered barrel -0.7..+1.6
for a in (0.25,1.15): merge(bm,bm_cyl(0.45,0.45,0.12,16,"gold",'Y',(0,A(a),0)))
merge(bm,bm_cyl(0.2,0.2,0.02,12,"outline",'Y',(0,A(1.605),0)))                  # dark bore cap
bar=mesh_from_bm("Barrel",bm,[M],root,PV); ucx("UCX_Barrel_01",bm_cyl(0.45,0.36,2.3,8,"navy",'Y',(0,A(0.45),0)),bar,PV)
def slot(n,bm): o=mesh_from_bm(n,bm,[M],root,PV); o["attach_to"]="Barrel"; return o
bm=bmesh.new(); bmesh.ops.create_uvsphere(bm,u_segments=16,v_segments=8,radius=0.45); paint(bm,lambda f:"cyan")
merge(bm,bm_cyl(0.47,0.47,0.06,16,"gold",'Z')); slot("Slot_Core",bm)
slot("Slot_Module",bm_box((0,A(0.65),0.62),(0.4,0.64,0.44),"violet"))
bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=False,segments=16,radius1=0.45,radius2=0.45,depth=0.18)
bmesh.ops.solidify(bm,geom=bm.faces[:],thickness=0.1); paint(bm,lambda f:"orange")
bmesh.ops.transform(bm,matrix=Matrix.Translation((0,A(1.75),0))@Matrix.Rotation(math.radians(90),4,'X'),verts=bm.verts)
slot("Slot_Booster",bm)
empty("Muzzle_Spawn",(0,A(1.95),1.5),root,0.15)["attach_to"]="Barrel"
for o in bpy.data.objects:
    if o.type=='MESH' and not o.name.startswith("UCX"): print("TRIS",o.name,tris(o))
export(os.path.join(d,"Wrecker_v2.fbx"),os.path.join(d,"source","Wrecker_v2.glb"))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(d,"source","Wrecker_v2.blend"))

# ---------------- Island_Sunnyville ----------------
reset(); d,pal=setup("Island_Sunnyville"); M=material("M_Palette",pal,0.5,0.0); R=random.Random(3)
root=empty("Island_Sunnyville")
def island(name,rx,ry,depth,seg,parent,loc,rings=3,cap=0.6):
    bm=bmesh.new(); top=[];
    for i in range(seg):
        t=2*math.pi*i/seg; j=1+R.uniform(-0.04,0.04); top.append(bm.verts.new((rx*j*math.cos(t),ry*j*math.sin(t),0)))
    lip=[bm.verts.new((v.co.x*1.01,v.co.y*1.01,-cap)) for v in top]
    bm.faces.new(top)
    for i in range(seg): bm.faces.new([top[(i+1)%seg],top[i],lip[i],lip[(i+1)%seg]])
    prev=lip
    for r in range(1,rings+1):
        k=1-r/(rings+1); z=-cap-depth*r/(rings+1)
        cur=[bm.verts.new((v.co.x*k*R.uniform(0.85,1.1)/1.01,v.co.y*k*R.uniform(0.85,1.1)/1.01,z+R.uniform(-0.1,0.1)*depth/rings)) for v in lip]
        for i in range(seg): bm.faces.new([prev[(i+1)%seg],prev[i],cur[i],cur[(i+1)%seg]])
        prev=cur
    tip=bm.verts.new((0,0,-cap-depth))
    for i in range(seg): bm.faces.new([prev[(i+1)%seg],prev[i],tip])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    paint(bm,lambda f:"grass" if f.calc_center_median().z>-cap*0.99 else "cliff")
    return bm
bm=island("Island_Sunnyville_Mesh",30,12.5,14,32,root,(0,0,0))
merge(bm,bm_cyl(3.2,3.2,0.08,16,"sand",'Z',(0,18,0.04)))           # Wrecker sand pad (behind, Unity -Z)
for x,y,s in ((-24,4,1.2),(22,-6,0.9),(10,9,0.7),(-12,-9,0.8)):      # rocks
    r=bmesh.new(); bmesh.ops.create_icosphere(r,subdivisions=1,radius=s); paint(r,lambda f:"steel_dk")
    bmesh.ops.translate(r,verts=r.verts,vec=(x,y,s*0.4)); merge(bm,r)
wf=bmesh.new(); bmesh.ops.create_grid(wf,x_segments=1,y_segments=3,size=1)            # waterfall strip
for v in wf.verts: v.co=Vector((29.6+ (v.co.y+1)*0.6, 0+v.co.x*1.5, -(v.co.y+1)*4.5))
paint(wf,lambda f:"cyan"); merge(bm,wf)
isl=mesh_from_bm("Island_Sunnyville_Mesh",bm,[M],root)
u=bm_cyl(30,30,1.0,16,"grass",'Z',(0,0,-0.5)); 
for v in u.verts: v.co.y*=12.5/30
ucx("UCX_Island_Sunnyville_Mesh_01",u,isl)
for i,(x,y,z,s) in enumerate(((-36,-10,-6,4),(37,8,-3,3))):
    sb=island("s",s*1.4,s,s*2.2,12,root,(0,0,0)); mesh_from_bm(f"Island_Sunnyville_Satellite_{i+1:02d}",sb,[M],root,(x,y,z))
empty("Spawn_Wrecker",(0,18,0),root,0.5); empty("KillPlane_Hint",(0,0,-25),root,1.0)["note"]="place kill plane at y=-25"
for o in bpy.data.objects:
    if o.type=='MESH' and not o.name.startswith("UCX"): print("TRIS",o.name,tris(o))
export(os.path.join(d,"Island_Sunnyville.fbx"),os.path.join(d,"source","Island_Sunnyville.glb"))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(d,"source","Island_Sunnyville.blend"))
