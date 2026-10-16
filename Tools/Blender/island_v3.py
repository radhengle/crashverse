# Island_Sunnyville v3 (<=10k LOD0, per-file LOD0/1/2 per PR #28). Blender -Y = forward (-> Unity +Z), +Z up, +X = left, 1u = 1 m
# Hierarchy depth 1 (exporter bake bug); runtime parent in custom prop attach_to.
import subprocess,sys,os,bpy,bmesh,math,random; sys.path.insert(0,os.path.dirname(__file__)); from cv_lib import *
import bake as BK
from mathutils import Vector, Matrix
M_ROOT="/workspace/wt/3d-modeler-v3/models"; A_ROOT="/workspace/wt/3d-modeler-v3/Assets/_Project/Models"
ROT={'Y':Matrix.Rotation(math.radians(90),4,'X'),'X':Matrix.Rotation(math.radians(90),4,'Y'),'Z':Matrix()}
def merge(dst,src):
    me=bpy.data.meshes.new("t"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)
def bm_cyl(r1,r2,depth,seg,cell,axis='Y',center=(0,0,0),bev=0.0):
    bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=True,segments=seg,radius1=r1,radius2=r2,depth=depth)
    paint(bm,lambda f:cell)
    if bev: bevel_hard(bm,bev,1,40)
    bmesh.ops.transform(bm,matrix=Matrix.Translation(center)@ROT[axis],verts=bm.verts); return bm
def bm_box(c,s,cell,bev=0.0,seg=1):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector((v.co.x*s[0],v.co.y*s[1],v.co.z*s[2]))
    paint(bm,lambda f:cell)
    if bev: bevel_hard(bm,bev,seg)
    bmesh.ops.translate(bm,verts=bm.verts,vec=c); return bm
def bm_lathe(profile,seg,cell,axis='Y',center=(0,0,0)):
    """profile: list of (r, h) along local Z; returns surface of revolution"""
    bm=bmesh.new(); rings=[]
    for r,h in profile:
        rings.append([bm.verts.new((r*math.cos(2*math.pi*i/seg),r*math.sin(2*math.pi*i/seg),h)) for i in range(seg)])
    for a,b in zip(rings,rings[1:]):
        for i in range(seg): bm.faces.new([a[i],a[(i+1)%seg],b[(i+1)%seg],b[i]])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces); paint(bm,lambda f:cell)
    bmesh.ops.transform(bm,matrix=Matrix.Translation(center)@ROT[axis],verts=bm.verts); return bm
def bm_sphere(r,u,v,cell,c=(0,0,0)):
    bm=bmesh.new(); bmesh.ops.create_uvsphere(bm,u_segments=u,v_segments=v,radius=r); paint(bm,lambda f:cell)
    bmesh.ops.translate(bm,verts=bm.verts,vec=c); return bm
def setup(asset):
    d=os.path.join(A_ROOT,asset); t=os.path.join(d,"textures"); os.makedirs(t,exist_ok=True)
    p="/tmp/cv_palette_256.png"; write_palette(p); return d,p   # palette only drives the bake, not shipped
def A(a): return -a
TR={}
def report():
    for o in bpy.data.objects:
        if o.type=='MESH' and not o.name.startswith("UCX"): TR[o.name]=tris(o); print("TRIS",o.name,tris(o))

# ======================= Island_Sunnyville =======================
TR.clear(); reset(); d,pal=setup("Island_Sunnyville"); M=material("M_Palette",pal,0.5,0.0); Rn=random.Random(3)
root=empty("Island_Sunnyville")
def island(rx,ry,depth,seg,cap=0.6,rings=7,terrace=True):
    bm=bmesh.new(); J=[1+Rn.uniform(-0.03,0.03) for i in range(seg)]
    def ring(k,z,cell=None):
        return [bm.verts.new((rx*k*J[i]*math.cos(2*math.pi*i/seg),ry*k*J[i]*math.sin(2*math.pi*i/seg),z)) for i in range(seg)]
    def band(a,b):
        for i in range(seg): bm.faces.new([a[(i+1)%seg],a[i],b[i],b[(i+1)%seg]])
    inner=ring(0.94,0); top=ring(1.0,-0.12)                       # soft rounded rim
    c=bm.verts.new((0,0,0)); 
    for i in range(seg): bm.faces.new([inner[i],inner[(i+1)%seg],c])
    band(top,inner) if False else None
    for i in range(seg): bm.faces.new([inner[(i+1)%seg],inner[i],top[i],top[(i+1)%seg]])
    over=ring(1.03,-0.3); band(top,over); lip=ring(1.0,-cap); band(over,lip); prev=lip   # grass edge overhang
    for r in range(1,rings+1):                                     # rounded (sin) profile, strata bands
        t=r/(rings+1); k=math.cos(t*math.pi/2)**0.8; z=-cap-depth*t
        cur=[bm.verts.new((v.co.x*k*Rn.uniform(0.95,1.04),v.co.y*k*Rn.uniform(0.95,1.04),z)) for v in lip]; band(prev,cur); prev=cur
    tip=bm.verts.new((0,0,-cap-depth*1.04)); 
    for i in range(seg): bm.faces.new([prev[(i+1)%seg],prev[i],tip])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    def col(f):
        z=f.calc_center_median().z
        if z>-0.13: return "grass"
        if z>-cap: return "grass_shade"
        t=(-z-cap)/depth; b=int(t*(rings+1)); h=hash((round(f.calc_center_median().x,1),round(f.calc_center_median().y,1)))%3
        if t>0.55: return ["cliff_deep","cliff_deep2"][b%2]
        return ["cliff","cliff_dk","cliff_lt","cliff","cliff_deep"][b%5]
    paint(bm,col); return bm
bm=island(30,12.5,13,56,rings=14)
# terraces: two soft raised grass tiers at the back rim (out of the play area)
for (x,y,rx,ry,h) in ((0,-9.5,22,3.2,0.7),(-6,-10.8,12,1.8,1.4)):
    t=bm_cyl(1,0.9,h,40,"grass_dk",'Z',(0,0,h/2),bev=0.15)
    for v in t.verts: v.co.x=v.co.x*rx+x; v.co.y=v.co.y*ry+y
    uv=t.loops.layers.uv.active
    for f in t.faces:
        if f.normal.z>0.7:
            c=uv_cell(None,"grass")
            for l in f.loops: l[uv].uv=c
    merge(bm,t)
merge(bm,bm_cyl(3.2,3.3,0.1,40,"sand",'Z',(0,18,0.05),bev=0.03))            # Wrecker sand pad
# sand path from pad to arena centre with stepping stones
for k in range(7):
    y=15-k*2.2; merge(bm,bm_box((math.sin(k*0.7)*0.8,y,0.03),(1.6,2.1,0.06),"path",0.02,1))
    for s in (-1,1):
        merge(bm,bm_box((math.sin(k*0.7)*0.8+s*0.45,y+Rn.uniform(-0.4,0.4),0.08),(0.5,0.5,0.12),"stone",0.04,2)) if (k+s)%2 else None
# trees + bushes
def tree(x,y,s):
    t=bm_cyl(0.18*s,0.13*s,1.2*s,10,"trunk",'Z',(x,y,0.6*s))
    blobs=((0,0,1.75,0.8,"leaf"),(0.45,0.2,1.5,0.6,"leaf_dk"),(-0.4,-0.25,1.95,0.58,"grass_lt")) if s>=1.0 else ((0,0,1.7,0.85,"leaf"),(0.35,-0.2,1.95,0.55,"grass_lt"))
    for bx,by,bz,br,cl in blobs:
        c=bmesh.new(); bmesh.ops.create_icosphere(c,subdivisions=1,radius=br*s); paint(c,lambda f,cl=cl:cl if f.calc_center_median().z>-0.25*br*s else "leaf_dk")
        for v in c.verts: v.co.z*=0.85
        bmesh.ops.translate(c,verts=c.verts,vec=(x+bx*s,y+by*s,bz*s)); merge(t,c)
    return t
for x,y,s in ((-25,-3,1.3),(-21,-8,1.5),(23,4,1.2),(21,-6,1.4),(-10,-9.5,1.1),(12,-9.5,1.0),(5,9,0.8)):
    merge(bm,tree(x,y,s))
for x,y,s in ((-17,3,0.6),(-14,-6,0.5),(16,6,0.55),(18,-3,0.6),(3,10,0.5),(9,9,0.5)):
    b=bmesh.new(); bmesh.ops.create_uvsphere(b,u_segments=12,v_segments=8,radius=s); paint(b,lambda f:"leaf")
    for v in b.verts: v.co.z*=0.7
    bmesh.ops.translate(b,verts=b.verts,vec=(x,y,s*0.35)); merge(bm,b)
# tiny houses (terraces), barrel, crate, fence
def house(x,y,z,w,dp,h,wall):
    b=bm_box((x,y,z+h/2),(w,dp,h),wall,0.04)
    r=bmesh.new(); vs=[r.verts.new(p) for p in ((x-w/2-0.12,y-dp/2-0.12,z+h),(x+w/2+0.12,y-dp/2-0.12,z+h),(x+w/2+0.12,y+dp/2+0.12,z+h),(x-w/2-0.12,y+dp/2+0.12,z+h),(x-w/2-0.12,y,z+h+0.75*w),(x+w/2+0.12,y,z+h+0.75*w))]
    for f in ((0,1,5,4),(2,3,4,5),(0,4,3),(1,2,5),(0,3,2,1)): r.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(r,faces=r.faces); paint(r,lambda f:"roof"); bevel_hard(r,0.03,1,40); merge(b,r)
    merge(b,bm_box((x,y+dp/2+0.015,z+0.36),(0.42,0.03,0.72),"white")); merge(b,bm_box((x,y+dp/2+0.03,z+0.34),(0.3,0.03,0.62),"wood_dk"))   # door + frame (faces +Y, camera side)
    for wx in (-w*0.3,w*0.3):
        merge(b,bm_box((x+wx,y+dp/2+0.015,z+h*0.62),(0.34,0.03,0.3),"white")); merge(b,bm_box((x+wx,y+dp/2+0.03,z+h*0.62),(0.24,0.03,0.2),"window"))
    return b
merge(bm,house(-16,-9.4,0.7,1.6,1.3,1.1,"wood")); merge(bm,house(-8,-10.8,1.4,1.4,1.1,1.0,"white")); merge(bm,house(15,-9.3,0.7,1.5,1.2,1.0,"brick"))
merge(bm,bm_cyl(0.32,0.32,0.8,16,"brick",'Z',(4.2,16.2,0.4),bev=0.03))       # barrel (indestructible deco, not the explosive gameplay barrel)
merge(bm,bm_box((-4.0,16.6,0.35),(0.7,0.7,0.7),"wood",0.04))                # crate
for k in range(5): merge(bm,bm_box((2.0+k*0.9,13.8,0.4),(0.1,0.1,0.8),"wood_dk"))   # fence posts + rails
for z in (0.3,0.6): merge(bm,bm_box((3.8,13.8,z),(3.8,0.06,0.08),"wood"))
# v3 extra detail: more trees/bushes, rocks along the rim, flowers, lamp posts
for x,y,s in ((-27,4,1.0),(26,-1,1.1),(-18,-10.5,0.9),(18,-8.5,1.0),(8,-10.6,0.9),(-4,-10,0.8),(-12,8,0.7),(14,9.5,0.75)):
    merge(bm,tree(x,y,s))
for x,y,s in ((-23,6,0.5),(-6,-7,0.45),(6,-7.5,0.5),(24,7,0.5),(-8,10.5,0.4),(11,-6,0.45),(-26,-6,0.5),(27,3,0.55)):
    b=bmesh.new(); bmesh.ops.create_uvsphere(b,u_segments=12,v_segments=8,radius=s); paint(b,lambda f:"leaf")
    for v in b.verts: v.co.z*=0.7
    bmesh.ops.translate(b,verts=b.verts,vec=(x,y,s*0.35)); merge(bm,b)
for k in range(14):
    a=2*math.pi*k/14+Rn.uniform(-0.1,0.1); r=Rn.uniform(0.86,0.93); s=Rn.uniform(0.35,0.8)
    b=bmesh.new(); bmesh.ops.create_icosphere(b,subdivisions=1,radius=s); paint(b,lambda f:"stone")
    for v in b.verts: v.co=Vector((v.co.x*Rn.uniform(0.8,1.3),v.co.y*Rn.uniform(0.8,1.3),v.co.z*0.6))
    bmesh.ops.translate(b,verts=b.verts,vec=(30*r*math.cos(a),12.5*r*math.sin(a),0.1)); merge(bm,b)
for k in range(44):   # grass tufts (3-sided, 4 tris)
    x=Rn.uniform(-26,26); y=Rn.uniform(-9,10)
    if (x/30)**2+(y/12.5)**2>0.8 or (abs(x)<2.5 and y>-2): continue
    merge(bm,bm_cyl(0.18,0.0,0.32,3,"grass_lt",'Z',(x,y,0.15)))
for x in (-2.2,2.2):
    merge(bm,bm_cyl(0.06,0.06,1.6,8,"wood_dk",'Z',(x,13.0,0.8))); merge(bm,bm_sphere(0.16,10,6,"white",(x,13.0,1.7)))
isl=mesh_from_bm("Island_Sunnyville_Mesh",bm,[M],root)
u=bm_cyl(30,30,1.0,16,"grass",'Z',(0,0,-0.5))
for v in u.verts: v.co.y*=12.5/30
uo=mesh_from_bm("UCX_Island_Sunnyville_Mesh_01",u,[],root); uo.display_type='WIRE'; uo["attach_to"]="Island_Sunnyville_Mesh"
for i,(x,y,z,s) in enumerate(((-37,-9,-6,4),(38,8,-3,3))):
    sb=island(s*1.4,s,s*2.0,24,0.3,6); merge(sb,tree(0,0,0.7)); mesh_from_bm(f"Island_Sunnyville_Satellite_{i+1:02d}",sb,[M],root,(x,y,z))
empty("Spawn_Wrecker",(0,18,0),root,0.5); empty("KillPlane_Hint",(0,0,-25),root,1.0)["note"]="place kill plane at y=-25"
# VFX placeholder waterfall: 1.5 m ribbon + splash puff (VFX replaces it; own flat material)
wf=bmesh.new(); pts=[(28.6,0.02),(29.8,0.02),(30.4,-1.5),(30.8,-5),(31.0,-9),(31.0,-12)]
for i,(x,z) in enumerate(pts):
    for s in (-1,1): wf.verts.new((x,s*0.75,z+0.03))
wf.verts.ensure_lookup_table()
for i in range(len(pts)-1): wf.faces.new([wf.verts[2*i],wf.verts[2*i+1],wf.verts[2*i+3],wf.verts[2*i+2]])
sp=bmesh.new(); bmesh.ops.create_icosphere(sp,subdivisions=1,radius=1.1)
for v in sp.verts: v.co.z*=0.55
bmesh.ops.translate(sp,verts=sp.verts,vec=(31,0,-12)); merge(wf,sp)
ft=bmesh.new(); bmesh.ops.create_icosphere(ft,subdivisions=1,radius=0.8)
for v in ft.verts: v.co.z*=0.4; v.co.y*=1.3
bmesh.ops.translate(ft,verts=ft.verts,vec=(29.4,0,0.05)); merge(wf,ft)
MV=bpy.data.materials.new("M_VFX_Placeholder"); MV.use_nodes=True; _p=MV.node_tree.nodes["Principled BSDF"]; _p.inputs["Base Color"].default_value=(0.85,0.95,1,1); _p.inputs["Alpha"].default_value=0.7; _p.inputs["Roughness"].default_value=0.15; _p.inputs["Emission Color"].default_value=(1,1,1,1); _p.inputs["Emission Strength"].default_value=0.15; MV.blend_method='BLEND' 
wfo=mesh_from_bm("VFX_Waterfall_Placeholder",wf,[MV],root); wfo["vfx_replace"]="waterfall"
uvl=wfo.data.uv_layers.new(name="UV0")
for poly in wfo.data.polygons:
    for li in poly.loop_indices:
        co=wfo.data.vertices[wfo.data.loops[li].vertex_index].co; uvl.data[li].uv=(co.y/1.5+0.5, 1+co.z/12.5)   # V runs down the fall (scroll for VFX)
report(); print("TOTAL_ISLAND",sum(TR.values()))
obs=[o for o in bpy.data.objects if o.type=='MESH' and not o.name.startswith(("UCX","VFX_"))]
cells={o.name:BK.face_cells(o) for o in obs}
tx=os.path.join(d,"textures"); paths,td,area=BK.bake(obs,2048,"Island",tx)
gen_tex("detail",tx); subprocess.check_call(["/usr/bin/python3",os.path.join(os.path.dirname(__file__),"island_post.py"),tx]); [im.reload() for im in bpy.data.images if im.filepath]
MI_=material_pbr("M_Island",paths["Albedo"],paths["Normal"],paths["ORM"])
for o in obs:
    o.data.materials.append(MI_); cl=cells[o.name]
    add_vcol(o,wind_fn=lambda ob,p:1.0 if cl[p.index] in ("leaf","leaf_dk") else 0.0); o["static"]=True
add_vcol(wfo)
add_lightmap_uv(obs)
print("TEXEL Island %.1f px/m area %.0f m2"%(td,area))
save_src("Island_Sunnyville","Island_Sunnyville")
export(os.path.join(d,"Island_Sunnyville_LOD0.fbx"),os.path.join(M_ROOT,"Island_Sunnyville","source","Island_Sunnyville.glb"))
print("TOTAL LOD0",sum(tris(o) for o in obs))
for lvl,ratio in ((1,0.5),(2,0.22)):
    for o in obs:
        m=o.modifiers.new("dec",'DECIMATE'); m.ratio=ratio/(ratio**(lvl-1)) if False else (0.5 if lvl==1 else 0.44); m.use_collapse_triangulate=True
        bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier="dec")
    print("TOTAL LOD%d"%lvl,sum(tris(o) for o in obs))
    export(os.path.join(d,"Island_Sunnyville_LOD%d.fbx"%lvl))
