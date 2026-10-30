# Village buildings assembled from the v3.1 BuildingKit. #28 has no building/assembly asset class, so a building is a
# LAYOUT JSON (Assets/_Project/Models/Buildings/<Bld>.layout.json) of kit-piece instances (Unity transforms, supports,
# connections) - no new FBX. Format: models/Buildings/LAYOUT_FORMAT.md. Also writes a preview .blend (models_src/Buildings)
# usage: blender -b --python build_buildings.py
import sys,os,bpy,json,math,glob; sys.path.insert(0,os.path.dirname(__file__)); from cv_lib import tris,SRC
from mathutils import Vector,Matrix
ROOT="/workspace/wt/3d-modeler-v3"; KITP="Assets/_Project/Models/BuildingKit"; OUTJ=f"{ROOT}/Assets/_Project/Models/Buildings"
os.makedirs(OUTJ,exist_ok=True); os.makedirs(f"{ROOT}/models/Buildings/source",exist_ok=True); os.makedirs(f"{SRC}/Buildings",exist_ok=True)
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))           # Blender (Z up, -Y fwd export) -> Unity (Y up, left-handed): p_u = C p_b
def kitdir(k): return k.split("_")[1] if not k.startswith("Kit_Base") else "Base"
# ---------- kit library (loaded once, instanced by copy) ----------
LIB={}; STATS={}
def lib(k):
    if k in LIB: return LIB[k]
    blend=f"{SRC}/Kit_{kitdir(k)}/{k}.blend"
    with bpy.data.libraries.load(blend) as (s,d): d.objects=[n for n in s.objects if n.startswith(k+"_LOD")]
    objs={o.name[len(k)+1:]:o for o in d.objects if o}
    st={"LOD0":tris(objs["LOD0"]),"LOD1":tris(objs["LOD1"]),"LOD2":tris(objs["LOD2"])}
    fb=f"{SRC}/Kit_{kitdir(k)}/{k}_Fractured.blend"
    if os.path.exists(fb):
        with bpy.data.libraries.load(fb) as (s,d): d.objects=[n for n in s.objects if n.startswith(k+"_frag_")]
        st["Fractured"]=sum(tris(o) for o in d.objects if o); st["frags"]=len([o for o in d.objects if o])
        for o in d.objects:
            if o: bpy.data.objects.remove(o)
    else: st["Fractured"]=0; st["frags"]=0
    for im in bpy.data.images:
        if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
            h=glob.glob(f"{ROOT}/{KITP}/**/{os.path.basename(im.filepath)}",recursive=True)
            if h: im.filepath=h[0]
    LIB[k]=objs; STATS[k]=st; return objs
# ---------- authoring helpers (Blender space, metres) ----------
def Rz(deg): return Matrix.Rotation(math.radians(deg),4,'Z')
class Bld:
    def __init__(s,name,origin,rot): s.name=name; s.M=Matrix.Translation(origin)@Rz(rot); s.P=[]; s.ids={}
    def add(s,iid,k,pos,rot=0,on="Ground",M=None):
        L=M if M is not None else Matrix.Translation(pos)@Rz(rot)
        s.P.append(dict(id=iid,kit=k,L=L,on=on)); return iid
def ring(b,pre,mat,W,D,z,front,back,left,right,on,joint=True,frames=True):
    """walls on a W x D (multiples of 3 m) footprint centred at 0; front=-Y, back=+Y, left=-X, right=+X; lists per 3 m module"""
    ids=[]; nx,ny=W//3,D//3
    for side,lst,axis in (("F",front,0),("B",back,0),("L",left,1),("R",right,1)):
        n=nx if axis==0 else ny
        for i,p in enumerate(lst):
            t=-W/2+1.5+3*i if axis==0 else -D/2+1.5+3*i
            pos=(t,-D/2 if side=="F" else D/2,z) if axis==0 else (-W/2 if side=="L" else W/2,t,z)
            rot=(0 if side=="F" else 180) if axis==0 else (-90 if side=="L" else 90)
            k=f"Kit_{mat}_{p}"; iid=b.add(f"{pre}_{side}{i+1}_{p}",k,pos,rot,on); ids.append(iid)
            if frames and mat!="Steel" and p in ("WallDoor","WallWindow"):
                fk="Kit_Wood_DoorFrame" if p=="WallDoor" else "Kit_Wood_WindowFrame"
                fz=z if p=="WallDoor" else z+0.85
                b.add(f"{iid}_Frame",fk,(pos[0],pos[1],fz),rot,"Ground" if (p=="WallDoor" and on=="Ground") else [iid])
    if joint:
        for x in [-W/2+3*i for i in range(nx+1)]:
            for y in (-D/2,D/2):
                if x in (-W/2,W/2) or True: ids.append(b.add(f"{pre}_J_{x:+.0f}_{y:+.0f}",f"Kit_{mat}_Joint",(x,y,z),0,on))
        for y in [-D/2+3*i for i in range(1,ny)]:
            for x in (-W/2,W/2): ids.append(b.add(f"{pre}_J_{x:+.0f}_{y:+.0f}",f"Kit_{mat}_Joint",(x,y,z),0,on))
    return ids
def floors(b,pre,mat,W,D,z,on):
    return [b.add(f"{pre}_{i}{j}",f"Kit_{mat}_Floor",(-W/2+1.5+3*i,-D/2+1.5+3*j,z),0,on) for i in range(W//3) for j in range(D//3)]
def gable_roof(b,pre,gmat,L,z,on,H=1.05,rise_run=1.5,slab=2.3,roof_on_extra=()):
    """gables at x=+-L/2, ridge along X at height z+H, Kit_Roof_Roof panels (3.6 x 2.3) per 3 m module per side, eave = anchor edge"""
    g=[b.add(f"{pre}_Gable_{s}",f"Kit_{gmat}_Gable",(s*L/2,0,z),90*s,on) for s in (-1,1)]
    S=math.hypot(rise_run,H); r=[]
    for side in (-1,1):
        d=Vector((0,side*rise_run/S,-H/S)); n=Vector((0,side*H/S,rise_run/S)); Y=-d; X=Y.cross(n)
        R=Matrix((X,Y,n)).transposed().to_4x4()
        for i in range(L//3):
            xc=-L/2+1.5+3*i; o=Vector((xc,0,z+H))+d*(slab/2)+n*(0.012*i)
            r.append(b.add(f"{pre}_Roof_{'N' if side>0 else 'S'}{i+1}","Kit_Roof_Roof",None,M=Matrix.Translation(o)@R,on=g+list(on)+list(roof_on_extra)))
    return g,r
# ---------- the five buildings ----------
B=[]
# 1 wooden cottage 6x3, attic floor on the walls, gable roof
b=Bld("Bld_WoodCottage",(-9,1.0,0),180); B.append(b)
w=ring(b,"G","Wood",6,3,0,["WallDoor","WallWindow"],["WallWindow","Wall"],["Wall"],["WallWindow"],"Ground")
f=floors(b,"Attic","Wood",6,3,3.0,[i for i in w if "_J_" not in i and "Frame" not in i])
gable_roof(b,"Top","Wood",6,3.2,f)
# 2 two-storey brick house 3x3
b=Bld("Bld_BrickHouse",(-4.5,-4.0,0),180); B.append(b)
w1=ring(b,"G","Brick",3,3,0,["WallDoor"],["WallWindow"],["Wall"],["WallWindow"],"Ground")
f1=floors(b,"F1","Brick",3,3,3.0,[i for i in w1 if "Frame" not in i])
w2=ring(b,"U","Brick",3,3,3.25,["WallWindow"],["WallWindow"],["WallWindow"],["Wall"],f1)
f2=floors(b,"F2","Brick",3,3,6.25,[i for i in w2 if "Frame" not in i])
gable_roof(b,"Top","Brick",3,6.5,f2)
# 3 steel warehouse 6x3, roller-door opening, flat steel deck roof
b=Bld("Bld_SteelWarehouse",(8.5,-3.0,0),180); B.append(b)
w=ring(b,"G","Steel",6,3,0,["WallDoor","WallWindow"],["Wall","Wall"],["WallWindow"],["Wall"],"Ground")
floors(b,"Deck","Steel",6,3,3.0,w)
# 4 stone watchtower on the indestructible granite plinth (Kit_Base 3x3x1)
b=Bld("Bld_StoneTower",(15,1.5,0),200); B.append(b)
base=b.add("Plinth","Kit_Base_Indestructible",(0,0,0),0,"Ground"); BZ=1.0
w1=ring(b,"G","Stone",3,3,BZ,["WallDoor"],["Wall"],["WallWindow"],["Wall"],[base])
f1=floors(b,"F1","Stone",3,3,BZ+3,[i for i in w1 if "Frame" not in i])
w2=ring(b,"U","Stone",3,3,BZ+3.3,["WallWindow"],["WallWindow"],["WallWindow"],["WallWindow"],f1)
floors(b,"Top","Stone",3,3,BZ+6.3,[i for i in w2 if "Frame" not in i])
# 5 glass kiosk 3x3: glass curtain walls on 3 sides, wood door wall, steel corner posts + steel deck roof
b=Bld("Bld_GlassKiosk",(5.0,3.2,0),180); B.append(b)
w=[b.add("G_F1_WallDoor","Kit_Wood_WallDoor",(0,-1.5,0),0),b.add("G_F1_WallDoor_Frame","Kit_Wood_DoorFrame",(0,-1.5,0),0)]
w+=[b.add("G_B1_Glass","Kit_Glass_Wall",(0,1.5,0),180),b.add("G_L1_Glass","Kit_Glass_Wall",(-1.5,0,0),-90),b.add("G_R1_Glass","Kit_Glass_Wall",(1.5,0,0),90)]
w+=[b.add(f"G_J_{x:+.0f}_{y:+.0f}","Kit_Steel_Joint",(x,y,0),0) for x in (-1.5,1.5) for y in (-1.5,1.5)]
floors(b,"Deck","Steel",3,3,3.0,[i for i in w if "Frame" not in i])
# ---------- resolve, write JSON, preview blend ----------
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
def aabb(objs):
    p=[o.matrix_world@Vector(c) for o in objs for c in o.bound_box]; return Vector([min(q[i] for q in p) for i in range(3)]),Vector([max(q[i] for q in p) for i in range(3)])
def r4(v): return round(v+0.0,4)
REPORT={}
for b in B:
    scn=bpy.data.scenes.new(b.name); bpy.context.window.scene=scn if bpy.context.window else None
    bpy.context.scene.collection.children.link(col:=bpy.data.collections.new(b.name))
    inst=[]; box={}; tot={"LOD0":0,"LOD1":0,"LOD2":0,"Fractured":0}
    for p in b.P:
        objs=lib(p["kit"]); Mw=b.M@p["L"]; made=[]
        for lod,o in objs.items():
            c=o.copy(); c.parent=None; c.matrix_world=Mw@o.matrix_world; c.name=f"{b.name}.{p['id']}__{lod}"; col.objects.link(c); c.hide_render=c.hide_viewport=(lod!="LOD0"); made.append(c)
        bpy.context.view_layer.update(); box[p["id"]]=aabb([m for m in made if m.name.endswith("LOD0")])
        st=STATS[p["kit"]]
        for k_ in tot: tot[k_]+=st[k_]
        # Unity transform of the instance (relative to the building root, and the root itself is in the village file)
        R3=p["L"].to_3x3(); Ru=C@R3@C.inverted(); q=Ru.to_quaternion(); e=Ru.to_euler('ZXY'); t=C@p["L"].translation
        d=p["kit"]; sub=kitdir(d)
        inst.append(dict(id=p["id"],kit=d,
            intact=f"{KITP}/Kit_{sub}/{d}.fbx",
            fractured=(f"{KITP}/Kit_{sub}/{d}_Fractured.fbx" if STATS[d]["frags"] else None),
            position={"x":r4(t.x),"y":r4(t.y),"z":r4(t.z)},rotation={"x":r4(q.x),"y":r4(q.y),"z":r4(q.z),"w":r4(q.w)},
            euler_zxy_deg={"x":r4(math.degrees(e.x)),"y":r4(math.degrees(e.y)),"z":r4(math.degrees(e.z))},
            anchor_to=p["on"] if p["on"]=="Ground" else list(p["on"]),
            indestructible=d.startswith("Kit_Base"),
            tris=dict(LOD0=st["LOD0"],LOD1=st["LOD1"],LOD2=st["LOD2"],Fractured=st["Fractured"])))
    ids={i["id"] for i in inst}
    for i in inst:
        if i["anchor_to"]!="Ground": assert all(a in ids for a in i["anchor_to"]),(b.name,i["id"],i["anchor_to"])
    # connections: AABB contact/overlap between instances (inflated 2 cm) -> inter-piece joints, break values set by Physics
    con=[]; ks=list(box)
    for a in range(len(ks)):
        for c in range(a+1,len(ks)):
            (a0,a1),(b0,b1)=box[ks[a]],box[ks[c]]
            if all(a0[x]<=b1[x]+0.02 and b0[x]<=a1[x]+0.02 for x in range(3)):
                pt=C@(b.M.inverted()@Vector([(max(a0[x],b0[x])+min(a1[x],b1[x]))/2 for x in range(3)]))
                con.append(dict(a=ks[a],b=ks[c],point={"x":r4(pt.x),"y":r4(pt.y),"z":r4(pt.z)},break_force_N="",break_torque_Nm=""))
    # every support must also be a contact
    cs={(x["a"],x["b"]) for x in con}|{(x["b"],x["a"]) for x in con}
    # authoring lists candidate supports; keep only the ones the piece really touches. A piece with none left is a problem.
    miss=[]
    for i in inst:
        if i["anchor_to"]=="Ground": continue
        i["anchor_to"]=[s_ for s_ in i["anchor_to"] if (i["id"],s_) in cs]
        if not i["anchor_to"]: miss.append(i["id"])
    t=C@b.M.translation; q=(C@b.M.to_3x3()@C.inverted()).to_quaternion()
    J=dict(format="crashverse.building_layout",version=1,building=b.name,units="m",space="Unity (Y up, left-handed); positions/rotations local to the building root",
           root={"position":{"x":r4(t.x),"y":r4(t.y),"z":r4(t.z)},"rotation":{"x":r4(q.x),"y":r4(q.y),"z":r4(q.z),"w":r4(q.w)},"note":"suggested village placement on Island_Sunnyville"},
           tris_total=tot,piece_count=len(inst),pieces=inst,connections=con,unsupported_contacts=miss)
    json.dump(J,open(f"{OUTJ}/{b.name}.layout.json","w"),indent=1)
    REPORT[b.name]=dict(pieces=len(inst),connections=len(con),missing_support_contacts=len(miss),**tot)
    print("BUILDING",b.name,REPORT[b.name],flush=True)
    if miss: print("  MISSING",miss,flush=True)
json.dump(REPORT,open(f"{ROOT}/models/Buildings/buildings_tris.json","w"),indent=1)
# preview blend (all buildings, village placement) + one GLB per building
sc=bpy.data.scenes[0]; bpy.context.window.scene=sc if bpy.context.window else None
for b in B:
    if b.name not in sc.collection.children: sc.collection.children.link(bpy.data.collections[b.name])
bpy.ops.wm.save_as_mainfile(filepath=f"{SRC}/Buildings/Village_Buildings.blend",compress=True)
print("DONE",flush=True)
