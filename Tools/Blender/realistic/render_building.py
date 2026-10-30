"""Generic modular-building sheet renders from the EXPORTED FBX + layout JSON (realistic v1): hero 3/4, gameplay camera,
module exploded view (no foliage between camera and building), destruction with rigid-body settled debris.
blender -b -P render_building.py -- <cfg>     cfg in CFG below (bakery, stallarch). Output: /workspace/crashverse_previews/realistic_buildings/<cfg>_*.png"""
import bpy,math,os,sys,random,json
from mathutils import Vector,Euler,Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,os.path.join(HERE,"..","realism"))
import rlib as R, rscene as S, ground as GR
REAL=R.REAL; ROOTP=REAL.split("/Assets/")[0]; OUT="/workspace/crashverse_previews/realistic_buildings"
NAME=sys.argv[sys.argv.index("--")+1]; SAMPLES=int(os.environ.get("RT_SAMPLES","64")); W=int(os.environ.get("RT_W","1200"))
B=REAL+"/Buildings"
CFG={
 "bakery":dict(layouts=[(f"{B}/Bld_Bakery/Bld_Bakery.layout.json",(0,0,0),())],
   mats={"M_RealBakery":f"{B}/Bld_Bakery/Textures/T_RealBakery"},
   hero=((13.5,-16.5,6.8),(-.4,0,3.6),36), game=((-1.2,-26.5,6.0),(0,0,2.8),30), exp=((24,-31,15.5),(0,0,6.6),32),
   EXP={"Base":(0,0,0),"ShopFront":(0,-2.6,0),"Wall_Back":(0,2.4,0),"Wall_Left":(-2.6,0,0),"Wall_Right":(2.6,0,0),"Floor":(0,0,1.6),
        "UpperStorey":(0,0,3.4),"Roof_Left":(-1.5,0,5.6),"Roof_Right":(1.5,0,5.6),"Chimney":(-1.2,2.6,8.2)},
   spread={"UpFL":(-.4,-1.3,0),"UpFC":(0,-1.3,0),"UpFR":(.4,-1.3,0),"UpB":(0,1.3,0),"UpL":(-1.3,0,0),"UpR":(1.3,0,0),"GabL":(-1.5,0,.9),"GabR":(1.5,0,.9)},
   BREAK=["ShopW","ShopD","UpFL","UpFC","UpFR"], blast=((0,-1.0,2.8),30.0), dest=((10.5,-16.5,4.4),(-.3,-2.2,2.4),34)),
 "stonetower":dict(layouts=[(f"{B}/Bld_StoneTower/Bld_StoneTower.layout.json",(0,0,0),())],
   mats={"M_RealTower":f"{B}/RealCastle/Textures/T_RealCastle"},
   hero=((12.0,-9.0,4.6),(0,0,5.0),30), game=((25.0,-3.5,5.2),(0,0,4.9),50), foliage_rot=math.pi/2, exp=((21,-15.5,9.5),(0,0,8.0),26),
   EXP={"ring0":(0,0,0),"wallF1":(1.2,0,.8),"wallB1":(-1.2,0,.8),"wallL1":(0,1.2,.8),"wallR1":(0,-1.2,.8),"ring1":(0,0,1.6),
        "wallF2":(1.2,0,2.4),"wallB2":(-1.2,0,2.4),"wallL2":(0,1.2,2.4),"wallR2":(0,-1.2,2.4),"ring2":(0,0,3.2),"crownFloor":(0,0,4.0),
        "crownWood":(0,0,4.8),"crownBrick":(0,0,5.6),"crownRoof":(0,0,6.4)},
   spread={"CrWdA":(0,0,0),"CrRfA":(.25,0,0),"CrRfB":(-.25,0,0)},
   BREAK=["W1F","W1R","R1"], TOPPLE=["W2F","W2B","W2L","W2R","R2","CrFl","CrWdA","CrWdB","CrBk","CrRfA","CrRfB"],
   blast=((1.0,-1.0,1.6),15.0), dest=((13.5,-11.0,4.6),(0,-.5,2.6),28)),
 "castlekeep":dict(layouts=[(f"{B}/Bld_CastleKeep/Bld_CastleKeep.layout.json",(0,0,0),())],
   mats={"M_RealKeep":f"{B}/RealCastle/Textures/T_RealCastle"},
   hero=((15.5,-18.5,6.0),(0,0,4.6),32), game=((-1.2,-31.0,6.2),(0,0,4.6),34), exp=((25,-31,17),(0,0,7.5),28),
   EXP={"Base":(0,0,0),"Wall_F1":(0,-2.4,0),"Wall_B1":(0,2.4,0),"Wall_L1":(-2.4,0,0),"Wall_R1":(2.4,0,0),"Floor_1":(0,0,1.4),
        "Wall_F2":(0,-2.4,2.6),"Wall_B2":(0,2.4,2.6),"Wall_L2":(-2.4,0,2.6),"Wall_R2":(2.4,0,2.6),"Crown":(0,0,4.6),
        "Parapet_F":(0,-1.6,5.6),"Parapet_B":(0,1.6,5.6),"Parapet_L":(-1.6,0,5.6),"Parapet_R":(1.6,0,5.6),"Roof":(0,0,7.6),
        "Turret_FL":(-2.6,-2.6,6.2),"Turret_FR":(2.6,-2.6,6.2),"Turret_BL":(-2.6,2.6,6.2),"Turret_BR":(2.6,2.6,6.2)},
   spread={"W1FL":(-.35,0,0),"W1FR":(.35,0,0),"CrownL":(-.35,0,0),"CrownR":(.35,0,0),"RoofA":(-.45,0,0),"RoofB":(.45,0,0),"CFL":(0,0,1.2),"CFR":(0,0,1.2),"CBL":(0,0,1.2),"CBR":(0,0,1.2)},
   BREAK=["W1FL","W1FR","Floor"], TOPPLE=["W2F","CrownL","CrownR","PF","PB","PL","PR","RoofA","RoofB","TFL","TFR","TBL","TBR","CFL","CFR","CBL","CBR"],
   blast=((0,-3.8,1.6),18.0), dest=((14.0,-17.5,5.4),(0,-1.5,2.8),30)),
 "gatehouse":dict(layouts=[(f"{B}/Bld_Gatehouse/Bld_Gatehouse.layout.json",(0,0,0),()),(f"{B}/Bld_Gatehouse/Bld_Gatehouse_WallRuns.layout.json",(0,0,0),())],
   mats={"M_RealGate":f"{B}/RealCastle/Textures/T_RealCastle","M_RealTower":f"{B}/RealCastle/Textures/T_RealCastle","M_RealWall":f"{B}/RealCastle/Textures/T_RealCastle"},
   hero=((13.5,-17.5,5.2),(0,0,3.0),28), game=((-0.8,-33.0,6.0),(0,0,3.0),30), exp=((24,-30,15),(0,0,5.2),26),
   EXP={"Arch":(0,0,0),"Portcullis":(0,-2.2,1.0),"WallWalk":(0,0,2.6),"run-1":(-2.2,0,0),"run1":(2.2,0,0),
        **{f"{t}_{m}":(sx*1.6+(sx*1.0 if m[-1] in "LR" and m.startswith("wall") else 0),0,dz) for t,sx in (("TL",-1),("TR",1)) for m,dz in (("doorSteps",0),("ring0",0),("wallF1",.8),("wallB1",.8),("wallL1",.8),("wallR1",.8),("ring1",1.6),("wallF2",2.4),("wallB2",2.4),("wallL2",2.4),("wallR2",2.4),("crownBrick",3.4))}},
   spread={"ArchL":(.3,0,0),"ArchR":(-.3,0,0)},
   BREAK=["ArchL","ArchR","Portc","TR_W1R","TR_W1L"], TOPPLE=["Cren1","Cren2","TR_R1","TR_W2F","TR_W2B","TR_W2L","TR_W2R","TR_CrBk"],
   blast=((-.6,-1.6,1.6),16.0), dest=((12.5,-16.5,5.0),(0,-1.0,2.2),30)),
 "golem":dict(layouts=[(f"{REAL}/Bosses/Golem_Ironjaw/Golem_Ironjaw.layout.json",(0,0,0),())],
   mats={"M_RealGolem":f"{REAL}/Bosses/RealGolem/Textures/T_RealGolem"}, emit={"M_RealGolem":(f"{REAL}/Bosses/RealGolem/Textures/T_RealGolem_E.png",6.0)},
   hero=((13.0,-9.5,4.4),(0,0,5.2),30), game=((25.0,-2.5,5.0),(0,0,5.2),40), exp=((20,-15.5,8.5),(0,0,6.6),26), foliage_rot=math.pi/2,
   EXP={"legL":(0,1.0,0),"legR":(0,-1.0,0),"pelvis":(0,0,.8),"belly":(0,0,1.6),"chest":(0,0,2.6),"neck":(0,0,3.4),"head":(0,0,4.2),
        "shL":(0,1.3,2.6),"shR":(0,-1.3,2.6),"armL":(0,2.2,1.6),"armR":(0,-2.2,1.6)},
   spread={}, BREAK=["legR","armR","shR"], TOPPLE=["pelvis","belly","chest","neck","head","shL","armL"],
   blast=((1.6,-2.2,2.0),18.0), dest=((14.0,-11.0,4.6),(0,-.5,2.6),30)),
 "stallarch":dict(layouts=[(f"{B}/Bld_MarketArch/Bld_MarketArch.layout.json",(0,0,0),())]+
     [(f"{B}/Kit_RealStall/Kit_RealStall.layout.json",(x,0,0),(skip,)) for x,skip in ((2.4,"StallB"),(.8,"StallA"),(-.8,"StallB"),(-2.4,"StallA"))],
   mats={"M_RealStall":f"{B}/Kit_RealStall/Textures/T_RealStall"},
   hero=((10.5,-13.5,4.2),(0,0,2.2),32), game=((-1.0,-24.0,5.4),(0,0,2.2),30), exp=((13,-17,7.5),(0,0,3.2),34),
   EXP={"Base":(0,0,0),"Post_Left":(-1.4,0,0),"Post_Right":(1.4,0,0),"Beam":(0,0,1.4),"Sign":(0,0,2.6),"Keystone":(0,0,0),"Stall":(0,-.4,1.0)},
   spread={}, BREAK=["StallA","StallB","Keystn","PostR","Beam","Sign"], blast=((2.4,-.6,1.2),12.0), dest=((9.5,-12.5,3.6),(0,0,1.8),32)),
}
C=CFG[NAME]
MAT={"M_Ground_Patch":(f"{REAL}/Props/Ground_Patch/Textures/T_Ground_Patch",False,True),"M_GrassClump":(f"{REAL}/Foliage/GrassClump/Textures/T_GrassClump",True,True),
     "M_Props_Village":(f"{REAL}/Props/Props_Village/Textures/T_Props_Village",False,True)}
for k,v in C["mats"].items(): MAT[k]=(v,False,True); MAT[k+"_Inner"]=(f"{B}/Bld_Townhouse/Textures/T_RealHouse_Inner",False,False)
for sp in ("Oak","Pine","Bush"):
    MAT[f"M_{sp}"]=(f"{REAL}/Foliage/{sp}/Textures/T_{sp}",True,True); MAT[f"M_{sp}_Billboard"]=(f"{REAL}/Foliage/{sp}/Textures/T_{sp}_Billboard",True,False)
mats={}
def emit(m,mask,strength):
    """preview of the boss emissive: Emission = BaseMap x mask x strength (Unity shader hookup is Graphics')"""
    nt=m.node_tree; bs=nt.nodes["Principled BSDF"]; A=[n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image and n.image.name.endswith("_A.png")][0]
    t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(mask); t.image.colorspace_settings.name='Non-Color'; nt.links.new(A.inputs[0].links[0].from_socket if A.inputs[0].links else nt.nodes.new("ShaderNodeUVMap").outputs[0],t.inputs[0])
    mx=nt.nodes.new("ShaderNodeMixRGB"); mx.blend_type='MULTIPLY'; mx.inputs[0].default_value=1.0; nt.links.new(A.outputs[0],mx.inputs[1]); nt.links.new(t.outputs[0],mx.inputs[2])
    nt.links.new(mx.outputs[0],bs.inputs["Emission Color"]); bs.inputs["Emission Strength"].default_value=strength
def M(n):
    if n not in mats:
        p,a,v=MAT[n]; mats[n]=R.lit_material(n+"_R",p,alpha=a,vcol=v)
        if n in C.get("emit",{}): emit(mats[n],*C["emit"][n])
    return mats[n]
def imp(path):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path,colors_type='LINEAR'); new=[o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type=='MESH':
            for s in o.material_slots: s.material=M(s.material.name.split(".")[0])
            if o.name.startswith("UCX_") or any(o.name.split(".")[0].endswith(f"_LOD{k}") for k in (1,2,3)): o.hide_render=True
    return [o for o in new if o.parent is None][0],new
def inst(objs,loc,rz=0,s=1.0):
    e=bpy.data.objects.new("grp",None); bpy.context.scene.collection.objects.link(e)
    for o in objs:
        if o.type!='MESH' or o.hide_render: continue
        c=bpy.data.objects.new(o.name+"_i",o.data); bpy.context.scene.collection.objects.link(c); c.matrix_world=o.matrix_world.copy(); c.parent=e
    e.location=loc; e.rotation_euler.z=rz; e.scale=(s,s,s); return e
bpy.ops.wm.read_factory_settings(use_empty=True)
u2b=lambda p:Vector((-p["x"],-p["z"],p["y"]))
mods={}; props=[]
for li,(lp,off,skip) in enumerate(C["layouts"]):
    L=json.load(open(lp))
    for p in L["pieces"]:
        if p["id"] in skip: continue
        r,n=imp(os.path.join(ROOTP,p["intact"])); r.location=u2b(p["position"])+Vector(off); r.rotation_euler.z=-math.radians(p.get("euler_zxy_deg",{}).get("y",0.0)); mods[p["id"] if li==0 else f'{p["id"]}#{li}']=(r,n,p)
    for q in L.get("props",[]):
        r,n=imp(os.path.join(ROOTP,q["asset"])); r.location=u2b(q["position"])+Vector(off); r.rotation_euler.z+= -2*math.atan2(q["rotation"]["y"],q["rotation"]["w"]); props.append((r,n))
ground,gn=imp(f"{REAL}/Props/Ground_Patch/Ground_Patch.fbx")
trees={sp:imp(f"{REAL}/Foliage/{sp}/{sp}.fbx") for sp in ("Oak","Pine","Bush")}
grass,gobj=imp(f"{REAL}/Foliage/GrassClump/GrassClump.fbx"); bpy.context.view_layer.update()
H=lambda x,y:GR.height(x,y)
FR=C.get("foliage_rot",0.0)   # rotate the foliage dressing about the origin so it stays behind/beside the building for this cfg's cameras
def fp(x,y): c,s_=math.cos(FR),math.sin(FR); return (c*x-s_*y,s_*x+c*y)
# foliage only behind / beside the building (nothing between the cameras and the facade)
for k,(x,y) in (("Oak",(-12.5,4.5)),("Pine",(9.6,6.4))): x,y=fp(x,y); trees[k][0].location=(x,y,H(x,y)-.05)
for k,(x,y),rz,sc in (("Pine",(13.0,1.5),1.3,.82),("Oak",(-15.5,9.5),2.1,.9)): x,y=fp(x,y); inst(trees[k][1],(x,y,H(x,y)-.05),rz,sc)
x,y=fp(-6.6,2.5); trees["Bush"][0].location=(x,y,H(x,y))
for (x,y,rz,s) in ((6.8,2.2,2.0,1.1),(3.6,5.4,.7,1.0),(-4.0,5.6,1.9,.85)): x,y=fp(x,y); inst(trees["Bush"][1],(x,y,H(x,y)),rz,s)
g0=[o for o in gobj if o.name.endswith("_LOD0")][0]; GR.scatter(g0,int(os.environ.get("RT_GRASS","4000"))); g0.hide_render=True
import bmesh
bd=bpy.data.objects.new("backdrop",bpy.data.meshes.new("bd")); bm=bmesh.new(); bmesh.ops.create_circle(bm,cap_ends=True,segments=48,radius=320); bm.to_mesh(bd.data)
bpy.context.scene.collection.objects.link(bd); bd.location.z=-.12
mb=bpy.data.materials.new("backdrop"); mb.use_nodes=True; nt=mb.node_tree; t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(f"{REAL}/Materials/Textures/T_Grass_A.png")
tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(1/4,1/4,1)
nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],t.inputs[0]); nt.links.new(t.outputs[0],nt.nodes["Principled BSDF"].inputs["Base Color"]); bd.data.materials.append(mb)
rng=random.Random(9)
for i in range(45):
    a=rng.uniform(.2,2.9)+FR; r=rng.uniform(32,70); inst(trees[rng.choice(["Pine","Pine","Oak"])][1],(math.cos(a)*r,math.sin(a)*r,-.1),rng.uniform(0,6),rng.uniform(.8,1.3))
S.cap_textures()
S.setup(res=(W,W*2//3),samples=SAMPLES,sun_el=57,sun_az=-35,sun_str=4.0,hdri_str=1.0,hdri_rot=60)
def shot(name,cam):
    S.camera(cam[0],cam[1],cam[2],name); S.render(f"{OUT}/{NAME}_{name}.png"); print("SHOT",name,flush=True)
SH=os.environ.get("RT_SHOTS","1234")
if "1" in SH: shot("01_hero",C["hero"])
if "2" in SH: shot("02_gameplay",C["game"])
for k,(r,n,p) in mods.items():
    off=Vector(C["EXP"].get(p["module"],(0,0,0)))+Vector(C["spread"].get(k,(0,0,0))); r["home"]=tuple(r.location); r.location=r.location+off
for r,n in props:
    for o in n: o.hide_render=True
if "3" in SH: shot("03_exploded",C["exp"])
for k,(r,n,p) in mods.items(): r.location=Vector(r["home"])
for r,n in props:
    for o in n:
        if o.type=='MESH' and not o.name.split(".")[0].endswith("_LOD1"): o.hide_render=False
# ---- destruction: fractured sets swapped in, rigid-body simulated until the debris settles
sc=bpy.context.scene; bpy.ops.rigidbody.world_add(); sc.rigidbody_world.substeps_per_frame=12; sc.rigidbody_world.solver_iterations=20
sc.frame_start=1; sc.frame_end=150; sc.rigidbody_world.point_cache.frame_end=150
def rb(o,typ,shape):
    bpy.context.view_layer.objects.active=o; o.select_set(True); bpy.ops.rigidbody.object_add(); o.rigid_body.type=typ; o.rigid_body.collision_shape=shape
    o.rigid_body.friction=.8; o.rigid_body.restitution=.05; o.rigid_body.collision_margin=.01; o.select_set(False); return o
gm=[o for o in gn if o.type=='MESH'][0]; rb(gm,'PASSIVE','MESH')
frs=[]
BRK=[k for k in mods if k.split("#")[0] in C["BREAK"] and (NAME!="stallarch" or "#" not in k or k.endswith("#1") or k.endswith("#2"))]
for k in BRK:
    r,n,p=mods[k]
    for o in n: o.hide_render=True
    fr,fn=imp(os.path.join(ROOTP,p["fractured"])); fr.location=r.location; fr.rotation_euler=r.rotation_euler.copy(); bpy.context.view_layer.update()
    for o in fn:
        if o.type!='MESH' or o.name.startswith("UCX_"): o.hide_render=True; continue
        mw=o.matrix_world.copy(); o.parent=None; o.matrix_world=mw; frs.append(o)
bpy.context.view_layer.update()
for k,(r,n,p) in mods.items():
    if k in BRK: continue
    for o in n:
        if o.type=='MESH' and o.name.split(".")[0].endswith("_LOD0"):
            mw=o.matrix_world.copy(); o.parent=None; o.matrix_world=mw
            if k.split("#")[0] in C.get("TOPPLE",()): rb(o,'ACTIVE','CONVEX_HULL').rigid_body.mass=60   # upper storeys rest on the broken one and topple
            else: rb(o,'PASSIVE','MESH')
for r,n in props:
    for o in n:
        if o.type=='MESH' and o.name.split(".")[0].endswith("_LOD0"): mw=o.matrix_world.copy(); o.parent=None; o.matrix_world=mw; rb(o,'ACTIVE','CONVEX_HULL').rigid_body.mass=15
for o in frs:
    rb(o,'ACTIVE','CONVEX_HULL'); o.rigid_body.mass=max(.5,float(o.get("mass_kg",10)))**.5*4   # compressed mass ratio keeps the solver stable
# blast: a short radial push from inside the shop front (Wrecker hit), then gravity settles everything
fo=bpy.data.objects.new("blast",None); sc.collection.objects.link(fo); fo.location=C["blast"][0]
bpy.context.view_layer.objects.active=fo; fo.select_set(True); bpy.ops.object.forcefield_toggle(); fo.field.type='FORCE'; fo.field.falloff_type='SPHERE'; fo.field.use_max_distance=True; fo.field.distance_max=6
for f,sv in ((1,C["blast"][1]*1000),(6,C["blast"][1]*1000),(8,0.0)):
    fo.field.strength=sv; fo.field.keyframe_insert("strength",frame=f)
for f in range(1,151): sc.frame_set(f)
moving=sum(1 for o in frs if o.matrix_world.translation.z>40 or o.matrix_world.translation.length>60)
print("DEBRIS settled frame 150, lost",moving,flush=True)
for o in frs:   # bake the simulated transform and drop the rigid body so the render uses the settled state
    mw=o.matrix_world.copy(); o.select_set(True)
for o in list(sc.objects):
    if o.rigid_body: mw=o.matrix_world.copy(); bpy.context.view_layer.objects.active=o; o.select_set(True); bpy.ops.rigidbody.object_remove(); o.matrix_world=mw; o.select_set(False)
shot("04_destruction",C["dest"])
