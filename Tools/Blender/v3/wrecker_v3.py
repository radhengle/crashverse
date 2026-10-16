# CRASHVERSE Wrecker_v2 – v3 hero build: LP (~11k) + HP (bevel x4, plank seams, nails, wear) -> baked 2048 A/N/ORM/E, LOD1/LOD2.
# Blender: -Y forward (Unity +Z), +Z up, +X = left. Root at ground centre. Dimensions per art/m1b CHANGES.md.
import sys,os,bpy,bmesh,math,random; sys.path.insert(0,os.path.dirname(__file__)); import hp
from mathutils import Vector, Matrix
W="/workspace/wt/3d-modeler-v3"; ASSET=f"{W}/Assets/_Project/Models/Wrecker_v2"; SRC="/workspace/crashverse/models_src/Wrecker_v2"
TEX=f"{ASSET}/textures"; os.makedirs(TEX,exist_ok=True); os.makedirs(SRC,exist_ok=True); os.makedirs(f"{W}/models/Wrecker_v2/source",exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True); R=random.Random(7)
def A(a): return -a
RX=Matrix.Rotation(math.radians(90),4,'Y'); RY=Matrix.Rotation(math.radians(90),4,'X')   # Z->X , Z->-Y
# ---------------- HP materials (bake sources) ----------------
def edge_mask(nt,radius=0.012,lo=0.92,noise=6.0,amount=1.0):
    bv=nt.nodes.new("ShaderNodeBevel"); bv.inputs["Radius"].default_value=radius; bv.samples=8
    g=nt.nodes.new("ShaderNodeNewGeometry"); d=nt.nodes.new("ShaderNodeVectorMath"); d.operation='DOT_PRODUCT'
    nt.links.new(bv.outputs[0],d.inputs[0]); nt.links.new(g.outputs["Normal"],d.inputs[1])
    mr=nt.nodes.new("ShaderNodeMapRange"); mr.inputs[1].default_value=1.0; mr.inputs[2].default_value=lo; nt.links.new(d.outputs["Value"],mr.inputs[0])
    n=nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value=noise; n.inputs["Detail"].default_value=6
    tc=nt.nodes.new("ShaderNodeTexCoord"); nt.links.new(tc.outputs["Object"],n.inputs["Vector"])
    n2=nt.nodes.new("ShaderNodeMapRange"); n2.inputs[1].default_value=0.42; n2.inputs[2].default_value=0.62; nt.links.new(n.outputs[0],n2.inputs[0])
    m=nt.nodes.new("ShaderNodeMath"); m.operation='MULTIPLY'; nt.links.new(mr.outputs[0],m.inputs[0]); nt.links.new(n2.outputs[0],m.inputs[1])
    c=nt.nodes.new("ShaderNodeMath"); c.operation='MULTIPLY'; c.use_clamp=True; c.inputs[1].default_value=amount*1.6; nt.links.new(m.outputs[0],c.inputs[0])
    return c.outputs[0]
def mixc(nt,fac,a,b):
    m=nt.nodes.new("ShaderNodeMix"); m.data_type='RGBA'; nt.links.new(fac,m.inputs[0])
    for i,v in ((6,a),(7,b)):
        if isinstance(v,tuple): m.inputs[i].default_value=v+(1,)
        else: nt.links.new(v,m.inputs[i])
    return m.outputs[2]
def mixf(nt,fac,a,b):
    m=nt.nodes.new("ShaderNodeMix"); m.data_type='FLOAT'; nt.links.new(fac,m.inputs[0]); m.inputs[2].default_value=a; m.inputs[3].default_value=b; return m.outputs[0]
def grain(nt,base,scale=14,amt=0.22):
    w=nt.nodes.new("ShaderNodeTexWave"); w.wave_type='BANDS'; w.bands_direction='Y'; w.inputs["Scale"].default_value=scale; w.inputs["Distortion"].default_value=8; w.inputs["Detail"].default_value=4
    tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(0.15,1,1); nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],w.inputs[0])
    m=nt.nodes.new("ShaderNodeMix"); m.data_type='RGBA'; m.blend_type='MULTIPLY'; m.inputs[0].default_value=amt; m.inputs[6].default_value=base+(1,); nt.links.new(w.outputs[0],m.inputs[7])
    b=nt.nodes.new("ShaderNodeBump"); b.inputs["Strength"].default_value=0.3; b.inputs["Distance"].default_value=0.002; nt.links.new(w.outputs[0],b.inputs["Height"])
    return m.outputs[2], b.outputs[0]
MAT={}
def mk(key,**kw): MAT[key]=hp.HPMat("HP_"+key,**kw); return MAT[key]
_wood=[None]
def wood_col(nt):
    c,b=grain(nt,hp.lin("#E3A766")); _wood[0]=b; return mixc(nt,edge_mask(nt,0.015,0.9,5),c,hp.lin("#F7CB92"))
mk("wood",color=wood_col,rough=lambda nt:mixf(nt,edge_mask(nt),0.62,0.55),metal=0.0,bump=lambda nt:_wood[0] or hp.noise_bump(nt,60,0.2,0.002))
mk("wood_dk",color=lambda nt:grain(nt,hp.lin("#B5793F"))[0],rough=0.66,metal=0.0,bump=lambda nt:hp.noise_bump(nt,50,0.25,0.002))
mk("navy",color=lambda nt:mixc(nt,edge_mask(nt,0.012,0.93,7),hp.lin("#3E4C6E"),hp.lin("#C9D0DA")),
   rough=lambda nt:mixf(nt,edge_mask(nt,0.012,0.93,7),0.32,0.3),metal=lambda nt:mixf(nt,edge_mask(nt,0.012,0.93,7),0.0,1.0),
   bump=lambda nt:hp.noise_bump(nt,180,0.06,0.0006))
mk("gold",color=lambda nt:mixc(nt,edge_mask(nt,0.008,0.9,9),hp.lin("#F2BE55"),hp.lin("#FFE08A")),rough=0.25,metal=1.0,bump=lambda nt:hp.noise_bump(nt,300,0.05,0.0004))
mk("iron",color=hp.lin("#8A93A0"),rough=lambda nt:mixf(nt,edge_mask(nt),0.42,0.3),metal=1.0,bump=lambda nt:hp.noise_bump(nt,220,0.12,0.0006))
mk("steel",color=hp.lin("#C8CFD8"),rough=0.3,metal=1.0,bump=lambda nt:hp.noise_bump(nt,400,0.05,0.0003))
mk("rubber",color=hp.lin("#2E323B"),rough=0.7,metal=0.0,bump=lambda nt:hp.noise_bump(nt,120,0.2,0.001))
mk("bore",color=hp.lin("#1B2130"),rough=0.6,metal=0.0)
mk("core",color=hp.lin("#22D3EE"),rough=0.08,metal=0.0,emis=hp.lin("#22D3EE"))
mk("core_hi",color=hp.lin("#C9FBFF"),rough=0.05,metal=0.0,emis=hp.lin("#BFF6FF"))
mk("violet",color=hp.lin("#8B5CF6"),rough=0.22,metal=0.0)
mk("violet_glow",color=hp.lin("#B79BFF"),rough=0.15,metal=0.0,emis=hp.lin("#8B5CF6"))
mk("orange",color=hp.lin("#FF7A1A"),rough=0.3,metal=0.0)
mk("orange_glow",color=hp.lin("#FFB070"),rough=0.2,metal=0.0,emis=hp.lin("#FF6A10"))
KEYS=list(MAT); GLOW={"core","core_hi","violet_glow","orange_glow"}
# ---------------- geometry helpers ----------------
class Part:
    def __init__(s): s.bm=bmesh.new()
    def add(s,bm,key,bev=0.0,seg=1,ang=30):
        if bev:
            bm.normal_update(); es=[e for e in bm.edges if len(e.link_faces)==2 and e.calc_face_angle(0)>math.radians(ang)]
            if es: bmesh.ops.bevel(bm,geom=es,offset=bev,segments=seg,affect='EDGES',clamp_overlap=True)
        for f in bm.faces: f.material_index=KEYS.index(key)
        me=bpy.data.meshes.new("t"); bm.to_mesh(me); bm.free(); s.bm.from_mesh(me); bpy.data.meshes.remove(me); return s
def box(c,sz,rot=None):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector((v.co.x*sz[0],v.co.y*sz[1],v.co.z*sz[2]))
    if rot: bmesh.ops.transform(bm,matrix=rot,verts=bm.verts)
    bmesh.ops.translate(bm,verts=bm.verts,vec=c); return bm
def lathe(prof,seg,M=Matrix(),closed=False):
    bm=bmesh.new(); rings=[]
    for r,h in prof: rings.append([bm.verts.new((r*math.cos(2*math.pi*i/seg),r*math.sin(2*math.pi*i/seg),h)) for i in range(seg)])
    for a,b in zip(rings,rings[1:]+([rings[0]] if closed else [])):
        for i in range(seg): bm.faces.new([a[i],a[(i+1)%seg],b[(i+1)%seg],b[i]])
    if not closed:
        if prof[0][0]>1e-4: bm.faces.new(list(reversed(rings[0])))
        if prof[-1][0]>1e-4: bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-6); bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    bmesh.ops.transform(bm,matrix=M,verts=bm.verts); return bm
def cyl(r,h,seg,M):
    return lathe([(r,-h/2),(r,h/2)],seg,M)
def sphere(r,u,v,c):
    bm=bmesh.new(); bmesh.ops.create_uvsphere(bm,u_segments=u,v_segments=v,radius=r); bmesh.ops.translate(bm,verts=bm.verts,vec=c); return bm
def dome(r,seg,M):  # rivet head
    return lathe([(r,0),(r*0.75,r*0.55),(0.0001,r*0.8)],seg,M)
def hexbolt(r,h,M):
    bm=lathe([(r*1.35,0),(r*1.35,h*0.25)],8,M); b2=lathe([(r,h*0.25),(r,h),(0.0001,h*1.08)],6,M)
    me=bpy.data.meshes.new("t"); b2.to_mesh(me); b2.free(); bm.from_mesh(me); bpy.data.meshes.remove(me); return bm
def toM(c,normal):  # matrix placing local +Z along normal at c
    n=Vector(normal).normalized(); q=Vector((0,0,1)).rotation_difference(n); return Matrix.Translation(c)@q.to_matrix().to_4x4()
def prism(profile,x0,x1):  # profile [(along,z)] extruded across X (x0<x1)
    bm=bmesh.new(); L=[bm.verts.new((x0,A(a),z)) for a,z in profile]; Rr=[bm.verts.new((x1,A(a),z)) for a,z in profile]; n=len(profile)
    bm.faces.new(L); bm.faces.new(list(reversed(Rr)))
    for i in range(n): j=(i+1)%n; bm.faces.new([L[j],L[i],Rr[i],Rr[j]])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces); return bm
HPX=[]  # HP-only extra geometry (seams, nails)
def hp_only(bm,key,bev=0.0,seg=3):
    p=Part().add(bm,key,bev,seg); HPX.append(p)
# ================= CARRIAGE (built full; symmetric in X) =================
car=Part()
arc=[(0.45+0.55*math.sin(t),0.6+0.85*math.cos(t)) for t in [math.pi/2*k/6 for k in range(0,7)]]
cheek=[(-1.5,0.25),(1.0,0.25),(1.0,0.6)]+list(reversed(arc))[1:]+[(0.25,1.45),(0.18,1.5),(-0.18,1.5),(-0.25,1.42),(-0.35,1.2),(-0.9,1.2),(-0.9,0.95),(-1.25,0.95),(-1.25,0.72),(-1.5,0.72)]
cheek=[p for i,p in enumerate(cheek) if i==0 or (abs(p[0]-cheek[i-1][0])+abs(p[1]-cheek[i-1][1]))>1e-4]
for sx in (1,-1):
    x0,x1=sorted((sx*0.62,sx*0.80)); car.add(prism(cheek,x0,x1),"wood",0.02,1)
    # HP plank seams: cheek split into 3 boards with 6 mm gaps + nails
    for z0,z1 in ((0.25,0.62),(0.62,0.99),(0.99,1.55)):
        b=prism(cheek,x0+0.003,x1-0.003); geom=b.verts[:]+b.edges[:]+b.faces[:]
        r1=bmesh.ops.bisect_plane(b,geom=geom,plane_co=(0,0,z0+0.003),plane_no=(0,0,-1),clear_outer=True)
        bmesh.ops.contextual_create(b,geom=[e for e in r1['geom_cut'] if isinstance(e,bmesh.types.BMEdge)])
        geom=b.verts[:]+b.edges[:]+b.faces[:]; r2=bmesh.ops.bisect_plane(b,geom=geom,plane_co=(0,0,z1-0.003),plane_no=(0,0,1),clear_outer=True)
        bmesh.ops.contextual_create(b,geom=[e for e in r2['geom_cut'] if isinstance(e,bmesh.types.BMEdge)])
        if b.verts: hp_only(b,"wood",0.006,3)
    for a in (-1.35,-1.0,-0.6,-0.1,0.5,0.85):
        for z in (0.44,0.81):
            hp_only(cyl(0.012,0.01,10,Matrix.Translation((sx*0.802,A(a),z))@RX),"iron",0.002,2)
# transoms, bed, axle beds
car.add(box((0,A(0.72),0.55),(1.24,0.22,0.5)),"wood_dk",0.02,2).add(box((0,A(-1.32),0.48),(1.24,0.2,0.4)),"wood_dk",0.02,2)
car.add(box((0,A(-0.25),0.32),(1.24,2.4,0.12)),"wood_dk",0.015,2)
car.add(box((0,A(0.15),0.6),(1.62,0.26,0.24)),"wood",0.02,2)                    # front axle bed
car.add(cyl(0.065,2.0,16,Matrix.Translation((0,A(0.15),0.6))@RX),"iron",0.008,1)  # front axle
car.add(cyl(0.05,1.4,12,Matrix.Translation((0,A(-1.35),0.35))@RX),"iron",0.006,1)  # rear axle
# iron straps with rivets wrapping each cheek
def cheek_top(a):
    best=0.25
    for (a0,z0),(a1,z1) in zip(cheek,cheek[1:]+cheek[:1]):
        if min(a0,a1)<=a<=max(a0,a1) and abs(a1-a0)>1e-6: best=max(best,z0+(z1-z0)*(a-a0)/(a1-a0))
    return best
for a in (-1.38,-0.62,0.62):
    top=cheek_top(a)
    for sx in (1,-1):
        car.add(box((sx*0.71,A(a),(0.25+top)/2),(0.205,0.075,top-0.25+0.02)),"iron",0.008,1)
        for z in (0.36,(0.25+top)/2,top-0.1):
            hp_only(dome(0.018,8,toM((sx*0.8125,A(a),z),(sx,0,0))),"iron")
# trunnion cap squares (gold) + hex bolts
for sx in (1,-1):
    car.add(box((sx*0.71,0,1.53),(0.22,0.34,0.07)),"gold",0.015,2)
    for a in (-0.12,0.12): car.add(hexbolt(0.022,0.03,toM((sx*0.71,A(a),1.565),(0,0,1))),"steel")
    # rear handle rings
    car.add(lathe([(0.012,0),(0.012,0.0)],3) if False else box((sx*0.71,A(-1.56),0.6),(0.05,0.1,0.05)),"iron",0.008,1)
    tor=bmesh.new(); bmesh.ops.create_circle(tor,segments=1,radius=0.01)
    t=lathe([(0.06+0.014*math.cos(2*math.pi*k/6),0.014*math.sin(2*math.pi*k/6)) for k in range(6)],14,toM((sx*0.71,A(-1.66),0.6),(1,0,0)),closed=False)
    car.add(t,"iron")
# ================= WHEELS =================
TYRE_RIVETS=[]
def big_wheel():
    w=Part(); M=RX
    w.add(lathe([(0.44,-0.075),(0.53,-0.075),(0.53,0.075),(0.44,0.075)],24,M,closed=True),"wood")             # felloe
    w.add(lathe([(0.525,-0.1),(0.585,-0.1),(0.6,-0.085),(0.6,0.085),(0.585,0.1),(0.525,0.1)],24,M,closed=True),"steel")                     # steel tyre
    w.add(lathe([(0.0001,-0.17),(0.15,-0.17),(0.17,-0.12),(0.17,0.12),(0.15,0.17),(0.0001,0.17)],12,M),"wood_dk")   # nave
    w.add(lathe([(0.0001,0.17),(0.13,0.17),(0.13,0.2),(0.1,0.24),(0.0001,0.25)],12,M),"gold")               # gold hub cap
    w.add(lathe([(0.0001,-0.17),(0.12,-0.17),(0.12,-0.2),(0.0001,-0.21)],12,M),"gold")
    for k in range(10):                                                                                               # tapered spokes
        t=2*math.pi*k/10; bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
        for v in bm.verts:
            rr=0.155+(v.co.z+0.5)*0.30; wdt=0.075-(v.co.z+0.5)*0.025
            v.co=Vector((v.co.x*0.07,v.co.y*wdt,rr))
        bmesh.ops.transform(bm,matrix=Matrix.Rotation(t,4,'X'),verts=bm.verts); w.add(bm,"wood")
    for k in range(6):
        t=2*math.pi*k/6; w.add(hexbolt(0.018,0.025,toM((0.17,0.1*math.cos(t),0.1*math.sin(t)),(1,0,0))),"steel")
    for k in range(14):
        t=2*math.pi*k/14; TYRE_RIVETS.append(toM((0.1,0.565*math.cos(t),0.565*math.sin(t)),(1,0,0)))
    return w
def roller():
    w=Part(); M=RX
    w.add(lathe([(0.0001,-0.13),(0.3,-0.13),(0.35,-0.08),(0.35,0.08),(0.3,0.13),(0.0001,0.13)],18,M),"rubber")
    w.add(lathe([(0.0001,0.13),(0.14,0.13),(0.14,0.16),(0.1,0.19),(0.0001,0.2)],10,M),"gold")
    for k in range(5):
        t=2*math.pi*k/5; w.add(hexbolt(0.014,0.02,toM((0.16,0.09*math.cos(t),0.09*math.sin(t)),(1,0,0))),"steel")
    return w
# ================= BARREL (local Z = muzzle in profile space, mapped to -Y) =================
def rb(a): return 0.42+(0.34-0.42)*(a+0.7)/2.3
bar=Part(); MB=RY
prof=[(0.0001,-1.02),(0.07,-1.015),(0.1,-0.99),(0.1,-0.95),(0.06,-0.93),(0.06,-0.86),(0.2,-0.84),(0.3,-0.8),(0.37,-0.76),(0.44,-0.72),(0.44,-0.66),(rb(-0.62),-0.62)]
prof+=[(rb(a),a) for a in (0.3,1.25)]+[(rb(1.38)+0.035,1.42),(rb(1.5)+0.05,1.5),(rb(1.6)+0.03,1.6),(0.27,1.6),(0.25,1.45)]
bar.add(lathe(prof,32,MB),"navy",0.0)
bar.add(lathe([(0.25,1.45),(0.0001,1.45)],24,MB),"bore")
for a in (-0.45,0.3,1.15):                                                                                           # gold bands with beads
    r=rb(a); bar.add(lathe([(r-0.005,a-0.075),(r+0.035,a-0.06),(r+0.05,a),(r+0.035,a+0.06),(r-0.005,a+0.075)],32,MB),"gold")
for sx in (1,-1):                                                                                                     # trunnions
    bar.add(lathe([(0.0001,0.38),(0.11,0.38),(0.11,0.62),(0.13,0.64),(0.13,0.7),(0.0001,0.7)],12,toM((0,0,0),(sx,0,0))),"navy",0.0)
    bar.add(hexbolt(0.03,0.04,toM((sx*0.7,0,0),(sx,0,0))),"gold")
# ================= SLOTS =================
cz=rb(0.0)+0.6*0.45-0.225
core=Part(); core.add(sphere(0.225,20,12,(0,0,cz)),"core")
core.add(sphere(0.06,10,6,(0.08,-0.09,cz+0.13)),"core_hi")
core.add(lathe([(0.17,rb(0)-0.08),(0.235,rb(0)-0.02),(0.245,rb(0)+0.05),(0.21,rb(0)+0.08),(0.17,rb(0)+0.05)],24,Matrix(),closed=True),"gold")
for k in range(4):                                                                                                    # cradle prongs
    t=math.pi/4+k*math.pi/2; c=Vector((0.215*math.cos(t),0.215*math.sin(t),cz-0.02))
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector((v.co.x*0.05,v.co.y*0.05,v.co.z*0.22))
    bmesh.ops.transform(bm,matrix=Matrix.Translation(c)@Matrix.Rotation(-0.35,4,Vector((-math.sin(t),math.cos(t),0))),verts=bm.verts); core.add(bm,"gold",0.012,1)
am=-0.65; rc=0.175; hl=0.14; pz=rb(am)+rc*0.8
mod=Part(); prof=[(rc*math.sin(t),-hl-rc*math.cos(t)) for t in [math.pi/2*k/4 for k in range(5)]]+[(rc*math.sin(t),hl+rc*math.cos(t)) for t in [math.pi/2*k/4 for k in range(4,-1,-1)]]
prof[0]=(0.0001,prof[0][1]); prof[-1]=(0.0001,prof[-1][1])
mod.add(lathe(prof,16,Matrix.Translation((0,A(am),pz))@RY),"violet")
mod.add(lathe([(rc+0.008,-0.04),(rc+0.008,0.04)],16,Matrix.Translation((0,A(am),pz))@RY,closed=False),"violet_glow")   # glow band
for s in (-1,1):
    mod.add(lathe([(rc+0.012,s*hl-0.03),(rc+0.02,s*hl),(rc+0.012,s*hl+0.03)],16,Matrix.Translation((0,A(am),pz))@RY),"gold")
mod.add(box((0,A(am),rb(am)+0.03),(0.22,0.3,0.08)),"gold",0.015,2)                                                   # saddle clamp
boo=Part(); M=Matrix.Translation((0,0,0))@RY
boo.add(lathe([(0.35,1.58),(0.4,1.6),(0.44,1.7),(0.45,1.8),(0.45,1.86),(0.42,1.9),(0.36,1.9),(0.33,1.86)],24,M,closed=False),"orange",0.0)
boo.add(lathe([(0.33,1.86),(0.31,1.7),(0.33,1.6),(0.35,1.58)],24,M),"orange_glow")
for k in range(8):
    t=2*math.pi*k/8; boo.add(hexbolt(0.018,0.025,toM((0.44*math.cos(t),A(1.72),0.44*math.sin(t)),(math.cos(t),0,math.sin(t)))),"steel")
# ================= objects =================
root=bpy.data.objects.new("Wrecker_v2",None); bpy.context.scene.collection.objects.link(root)
def obj(name,part,loc=(0,0,0),attach=None):
    me=bpy.data.meshes.new(name); part.bm.to_mesh(me)
    for k in KEYS: me.materials.append(MAT[k].m)
    o=bpy.data.objects.new(name,me); o.location=loc; bpy.context.scene.collection.objects.link(o); o.parent=root
    if attach: o["attach_to"]=attach
    return o
PV=(0,0,1.5)
O={}
O["Carriage"]=obj("Carriage",car)
bw=big_wheel(); O["Wheel_FL"]=obj("Wheel_FL",bw,(0.97,A(0.15),0.6))
O["Wheel_RL"]=obj("Wheel_RL",roller(),(0.6,A(-1.35),0.35))
O["Barrel"]=obj("Barrel",bar,PV); O["Slot_Core"]=obj("Slot_Core",core,PV,"Barrel"); O["Slot_Module"]=obj("Slot_Module",mod,PV,"Barrel"); O["Slot_Booster"]=obj("Slot_Booster",boo,PV,"Barrel")
hpx=[]
for i,p in enumerate(HPX):
    o=obj(f"HPX_{i:03d}",p); o.parent=None; hpx.append(o)
bpy.context.view_layer.update()
def tris(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
# ---------- HP copies (bevel x4 + weighted normals) ----------
HP=[]
for n,o in O.items():
    h=o.copy(); h.data=o.data.copy(); h.name=n+"_HP"; bpy.context.scene.collection.objects.link(h); h.parent=None; h.matrix_world=o.matrix_world.copy()
    b=h.modifiers.new("bev",'BEVEL'); b.width=0.008; b.segments=4; b.limit_method='ANGLE'; b.angle_limit=math.radians(35); b.harden_normals=True
    HP.append(h)
for h in hpx: HP.append(h)
# HP mirrors for symmetric halves
def mirror_hp(o):
    h=o.copy(); h.data=o.data.copy(); h.name=o.name+"_M"; bpy.context.scene.collection.objects.link(h)
    bm=bmesh.new(); bm.from_mesh(h.data); bmesh.ops.scale(bm,vec=(-1,1,1),verts=bm.verts); bmesh.ops.reverse_faces(bm,faces=bm.faces); bm.to_mesh(h.data); bm.free(); h.location.x*=-1; return h
for M_ in TYRE_RIVETS:
    p=Part().add(dome(0.016,8,M_),"steel"); o_=obj("HPR",p,(0.97,A(0.15),0.6)); o_.parent=None; o_.matrix_world=Matrix.Translation((0.97,A(0.15),0.6)); o_.modifiers.new("b",'BEVEL').segments=2; HP.append(o_); hpx.append(o_)
for n in ("Wheel_FL","Wheel_RL"):
    m=mirror_hp([h for h in HP if h.name==n+"_HP"][0]); HP.append(m)
# ---------- LP: halve symmetric objects, unwrap uniques, then mirror ----------
SYM=["Carriage","Barrel","Slot_Core","Slot_Module","Slot_Booster"]
for n in SYM:
    o=O[n]; bm=bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=(0,0,0),plane_no=(-1,0,0),clear_outer=True)
    bm.to_mesh(o.data); bm.free()
uniq=list(O.values())
for o in uniq:
    o.data.uv_layers.new(name="UV0")
bpy.ops.object.select_all(action='DESELECT')
for o in uniq: o.select_set(True)
bpy.context.view_layer.objects.active=uniq[0]
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(55),island_margin=0.002,scale_to_bounds=False)
bpy.ops.uv.select_all(action='SELECT'); bpy.ops.uv.average_islands_scale()
# texel priority: downward-facing (never seen in gameplay) islands at 40%
bpy.ops.object.mode_set(mode='OBJECT')
for o in uniq:
    me=o.data; uv=me.uv_layers["UV0"]
    for p in me.polygons:
        nw=o.matrix_world.to_3x3()@p.normal
        if nw.z<-0.8 or (o.name=="Carriage" and nw.x<-0.8 and p.center.x>0.6):
            cx=sum(uv.data[li].uv.x for li in p.loop_indices)/p.loop_total; cy=sum(uv.data[li].uv.y for li in p.loop_indices)/p.loop_total
            for li in p.loop_indices: uv.data[li].uv=(cx+(uv.data[li].uv.x-cx)*0.35, cy+(uv.data[li].uv.y-cy)*0.35)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(margin=0.002,rotate=True,shape_method='CONCAVE')
bpy.ops.object.mode_set(mode='OBJECT')
# bake target = join of unique LP pieces
tgt=None
cp=[]
for o in uniq:
    c=o.copy(); c.data=o.data.copy(); bpy.context.scene.collection.objects.link(c); c.parent=None; c.matrix_world=o.matrix_world.copy(); cp.append(c)
bpy.ops.object.select_all(action='DESELECT')
for c in cp: c.select_set(True)
bpy.context.view_layer.objects.active=cp[0]; bpy.ops.object.join(); tgt=cp[0]; tgt.name="BAKE_LP"
for o in list(O.values())+hpx: o.hide_render=True
tgt.hide_render=False
for h in HP: h.hide_render=False
print("BAKING...",flush=True)
paths=hp.bake_maps(tgt,HP,2048,"Wrecker",TEX,extrusion=0.025,ray=0.05,emissive=True,ao_samples=64,ao_dist=0.25)
for k,p in paths.items(): print("MAP",k,p,hp.png_stats(p))
# texel density (unique UV area)
import bmesh as _b
bm=_b.new(); bm.from_mesh(tgt.data); uvl=bm.loops.layers.uv["UV0"]; A3=AU=0
for f in bm.faces:
    A3+=f.calc_area(); pts=[l[uvl].uv for l in f.loops]; AU+=abs(sum(pts[i].x*pts[i-1].y-pts[i-1].x*pts[i].y for i in range(len(pts)))/2)
bm.free(); print("TEXEL_ALL %.0f px/m"%(math.sqrt(AU*2048*2048/A3)))
# ---------- mirror LP halves + make FR/RR ----------
def mirror_merge(o):
    bm=bmesh.new(); bm.from_mesh(o.data); d=bmesh.ops.duplicate(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:])
    vs=[g for g in d['geom'] if isinstance(g,bmesh.types.BMVert)]; fs=[g for g in d['geom'] if isinstance(g,bmesh.types.BMFace)]
    bmesh.ops.scale(bm,vec=(-1,1,1),verts=vs); bmesh.ops.reverse_faces(bm,faces=fs)
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-5); bm.to_mesh(o.data); bm.free()
for n in SYM: mirror_merge(O[n])
def mirror_obj(src,name):
    o=src.copy(); o.data=src.data.copy(); o.name=name; bpy.context.scene.collection.objects.link(o); o.parent=root; o.location.x*=-1
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.scale(bm,vec=(-1,1,1),verts=bm.verts); bmesh.ops.reverse_faces(bm,faces=bm.faces); bm.to_mesh(o.data); bm.free(); return o
O["Wheel_FR"]=mirror_obj(O["Wheel_FL"],"Wheel_FR"); O["Wheel_RR"]=mirror_obj(O["Wheel_RL"],"Wheel_RR")
# ---------- final materials ----------
def pbr(name,emis=False):
    m=bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree; b=nt.nodes["Principled BSDF"]
    def im(k,cs):
        t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(paths[k],check_existing=False); t.image.colorspace_settings.name=cs; return t
    nt.links.new(im("A","sRGB").outputs[0],b.inputs["Base Color"])
    s=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(im("ORM","Non-Color").outputs[0],s.inputs[0])
    nt.links.new(s.outputs[1],b.inputs["Roughness"]); nt.links.new(s.outputs[2],b.inputs["Metallic"])
    nm=nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(im("N","Non-Color").outputs[0],nm.inputs["Color"]); nt.links.new(nm.outputs[0],b.inputs["Normal"])
    if emis: nt.links.new(im("E","sRGB").outputs[0],b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value=2.5
    m["shader"]="Crashverse/GlossyPBR"; return m
MW=pbr("M_Wrecker"); ME=pbr("M_Wrecker_Emissive",True)
for n,o in O.items():
    gl=[KEYS[p.material_index] in GLOW for p in o.data.polygons]
    o.data.materials.clear(); o.data.materials.append(MW)
    if any(gl):
        o.data.materials.append(ME)
        for p,g in zip(o.data.polygons,gl): p.material_index=1 if g else 0
    else:
        for p in o.data.polygons: p.material_index=0
    ca=o.data.color_attributes.new("Col",'BYTE_COLOR','CORNER')
    for d in ca.data: d.color=(0,0,0,0)
# colliders + sockets
def ucx(name,bm,attach,loc=(0,0,0)):
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); o=bpy.data.objects.new(name,me); o.location=loc; bpy.context.scene.collection.objects.link(o); o.parent=root; o["attach_to"]=attach; o.display_type='WIRE'; return o
ucx("UCX_Carriage_01",box((0,A(-0.25),0.85),(1.62,2.55,1.2)),"Carriage")
ucx("UCX_Barrel_01",cyl(0.45,2.7,10,Matrix.Translation((0,A(0.3),0))@RY),"Barrel",PV)
for n,loc,att in (("Socket_Muzzle",(0,A(1.95),1.5),"Barrel"),("Socket_Core",(0,0,1.5+cz),"Barrel"),("Socket_Module",(0,A(am),1.5+pz),"Barrel"),
                  ("Socket_Booster",(0,A(1.75),1.5),"Barrel"),("Socket_WheelDust",(0,A(0.15),0.0),"Wrecker_v2")):
    e=bpy.data.objects.new(n,None); e.location=loc; bpy.context.scene.collection.objects.link(e); e.parent=root; e["attach_to"]=att
# remove bake helpers from export scene but keep HP in source blend (separate collection)
hpcol=bpy.data.collections.new("HP_source"); bpy.context.scene.collection.children.link(hpcol)
for h in HP+[tgt]:
    for c in h.users_collection: c.objects.unlink(h)
    hpcol.objects.link(h); h.hide_render=True; h.hide_viewport=True
for o in hpx:
    if o.name in bpy.context.scene.collection.objects: bpy.context.scene.collection.objects.unlink(o)
    if o.name not in hpcol.objects: hpcol.objects.link(o)
for o in O.values(): o.hide_render=False
TOT=0
for n,o in O.items(): t=tris(o); TOT+=t; print("TRIS LOD0",n,t)
print("TOTAL LOD0",TOT)
hpcol.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=f"{SRC}/Wrecker_v2.blend",compress=True)
print("DONE")
# ---------- export LOD0/1/2 (FBX = Unity deliverable, GLB = source preview) ----------
def export(fbx,glb=None):
    bpy.ops.export_scene.fbx(filepath=fbx, use_visible=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        bake_space_transform=True, use_custom_props=True, path_mode='RELATIVE', embed_textures=False, mesh_smooth_type='FACE', colors_type='LINEAR',
        add_leaf_bones=False, object_types={'EMPTY','MESH'}, use_mesh_modifiers=True)
    if glb: bpy.ops.export_scene.gltf(filepath=glb, export_format='GLB', use_visible=True, export_yup=True, export_extras=True)
export(f"{ASSET}/Wrecker_v2_LOD0.fbx", f"{W}/models/Wrecker_v2/source/Wrecker_v2.glb")
LODT={}
for lvl,ratio in ((1,0.46),(2,0.19)):
    for n,o in O.items():
        for m in list(o.modifiers): o.modifiers.remove(m)
        d=o.modifiers.new("lod",'DECIMATE'); d.ratio=ratio; d.use_collapse_triangulate=True
    bpy.context.view_layer.update(); dg=bpy.context.evaluated_depsgraph_get(); t=0
    for n,o in O.items():
        e=o.evaluated_get(dg); k=sum(len(p.vertices)-2 for p in e.data.polygons); t+=k; print(f"TRIS LOD{lvl}",n,k)
    print(f"TOTAL LOD{lvl}",t)
    export(f"{ASSET}/Wrecker_v2_LOD{lvl}.fbx")
for o in O.values():
    for m in list(o.modifiers): o.modifiers.remove(m)
print("EXPORTED")
