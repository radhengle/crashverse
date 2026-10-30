"""Bld_Townhouse modular sheet (one render pass, from the EXPORTED FBX + layout JSON): hero 3/4, gameplay camera, module exploded view, destruction.
blender -b -P render_townhouse.py   -> /workspace/crashverse_previews/realistic_buildings/townhouse_*.png (+ compose with compose_townhouse in this file)"""
import bpy,math,os,sys,random,json,glob
from mathutils import Vector,Euler
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,os.path.join(HERE,"..","realism"))
import rlib as R, rscene as S, ground as GR
REAL=R.REAL; B=f"{REAL}/Buildings/Bld_Townhouse"; OUT="/workspace/crashverse_previews/realistic_buildings"
SAMPLES=int(os.environ.get("RT_SAMPLES","64")); W=int(os.environ.get("RT_W","1200"))
MAT={"M_RealHouse":(f"{B}/Textures/T_RealHouse",False,True),"M_RealHouse_Inner":(f"{B}/Textures/T_RealHouse_Inner",False,False),
     "M_Ground_Patch":(f"{REAL}/Props/Ground_Patch/Textures/T_Ground_Patch",False,True),"M_GrassClump":(f"{REAL}/Foliage/GrassClump/Textures/T_GrassClump",True,True)}
for sp in ("Oak","Pine","Bush"):
    MAT[f"M_{sp}"]=(f"{REAL}/Foliage/{sp}/Textures/T_{sp}",True,True); MAT[f"M_{sp}_Billboard"]=(f"{REAL}/Foliage/{sp}/Textures/T_{sp}_Billboard",True,False)
mats={}
def M(n):
    if n not in mats: p,a,v=MAT[n]; mats[n]=R.lit_material(n+"_R",p,alpha=a,vcol=v)
    return mats[n]
def imp(path):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path,colors_type='LINEAR'); new=[o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type=='MESH':
            for s in o.material_slots: s.material=M(s.material.name.split(".")[0])
            if o.name.startswith("UCX_") or any(o.name.endswith(f"_LOD{k}") for k in (1,2,3)): o.hide_render=True
    return [o for o in new if o.parent is None][0],new
def inst(objs,loc,rz=0,s=1.0):
    e=bpy.data.objects.new("grp",None); bpy.context.scene.collection.objects.link(e)
    for o in objs:
        if o.type!='MESH' or o.hide_render: continue
        c=bpy.data.objects.new(o.name+"_i",o.data); bpy.context.scene.collection.objects.link(c); c.matrix_world=o.matrix_world.copy(); c.parent=e
    e.location=loc; e.rotation_euler.z=rz; e.scale=(s,s,s); return e
bpy.ops.wm.read_factory_settings(use_empty=True)
L=json.load(open(f"{B}/Bld_Townhouse.layout.json")); PIV={m["id"]:m for m in L["modules"]}
u2b=lambda p:Vector((-p["x"],-p["z"],p["y"]))
mods={}
for p in L["pieces"]:
    root,new=imp(os.path.join(REAL.split("/Assets/")[0],p["intact"])); root.location=u2b(p["position"]); mods[p["id"]]=(root,new,p)
ground,_=imp(f"{REAL}/Props/Ground_Patch/Ground_Patch.fbx")
trees={sp:imp(f"{REAL}/Foliage/{sp}/{sp}.fbx") for sp in ("Oak","Pine","Bush")}
grass,gobj=imp(f"{REAL}/Foliage/GrassClump/GrassClump.fbx"); bpy.context.view_layer.update()
H=lambda x,y:GR.height(x,y)
trees["Oak"][0].location=(-11.5,-2.8,H(-11.5,-2.8)-.05); trees["Pine"][0].location=(8.6,3.4,H(8.6,3.4)-.05)
inst(trees["Pine"][1],(11.6,-2.2,H(11.6,-2.2)-.05),1.3,.82); inst(trees["Oak"][1],(-14.5,6.5,H(-14.5,6.5)),2.1,.9)
trees["Bush"][0].location=(5.2,-4.25,H(5.2,-4.25))
for (x,y,rz,s) in ((-6.9,-3.4,1.2,.8),(6.3,-1.2,2.0,1.1),(3.6,4.4,.7,1.0),(-2.4,-9.8,1.9,.75)): inst(trees["Bush"][1],(x,y,H(x,y)),rz,s)
g0=[o for o in gobj if o.name.endswith("_LOD0")][0]; GR.scatter(g0,int(os.environ.get("RT_GRASS","5000"))); g0.hide_render=True
import bmesh
bd=bpy.data.objects.new("backdrop",bpy.data.meshes.new("bd")); bm=bmesh.new(); bmesh.ops.create_circle(bm,cap_ends=True,segments=48,radius=320); bm.to_mesh(bd.data)
bpy.context.scene.collection.objects.link(bd); bd.location.z=-.12
mb=bpy.data.materials.new("backdrop"); mb.use_nodes=True; nt=mb.node_tree; t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(f"{REAL}/Materials/Textures/T_Grass_A.png")
tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(1/4,1/4,1)
nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],t.inputs[0]); nt.links.new(t.outputs[0],nt.nodes["Principled BSDF"].inputs["Base Color"]); bd.data.materials.append(mb)
rng=random.Random(9)
for i in range(50):
    a=rng.uniform(0,6.283); r=rng.uniform(30,70); inst(trees[rng.choice(["Pine","Pine","Oak"])][1],(math.cos(a)*r,math.sin(a)*r,-.1),rng.uniform(0,6),rng.uniform(.8,1.3))
S.cap_textures()
S.setup(res=(W,W*2//3),samples=SAMPLES,sun_el=57,sun_az=-35,sun_str=4.0,hdri_str=1.0,hdri_rot=60)
def shot(name,loc,tg,lens): S.camera(loc,tg,lens,name); S.render(f"{OUT}/townhouse_{name}.png"); print("SHOT",name,flush=True)
shot("01_hero",(12.5,-15.5,6.2),(-.6,0,3.3),36)
shot("02_gameplay",(-1.2,-24.5,5.6),(0,0,2.4),30)
# ---- exploded: modules pushed apart (pieces of one module move together), labelled in the composite caption
EXP={"Base":(0,0,0),"ShopFront":(0,-2.2,0),"Wall_Back":(0,2.2,0),"Wall_Left":(-2.4,0,0),"Wall_Right":(2.4,0,0),"Floor":(0,0,1.4),
     "UpperStorey":(0,0,3.0),"Roof_Left":(-1.4,0,5.0),"Roof_Right":(1.4,0,5.0),"Chimney":(2.2,1.2,7.6),"Annex":(-2.6,0,0)}
for k,(r,n,p) in mods.items():
    off=Vector(EXP[p["module"]])
    if p["module"]=="UpperStorey":   # spread the storey's walls a little so the pieces read
        off+=Vector({"UpFL":(-.3,-1.2,0),"UpFR":(.3,-1.2,0),"Balc":(.6,-2.4,0),"UpB":(0,1.2,0),"UpL":(-1.2,0,0),"UpR":(1.2,0,0),"GabL":(-1.4,0,.8),"GabR":(1.4,0,.8)}[k])
    r["home"]=tuple(r.location); r.location=r.location+off
shot("03_exploded",(19,-24,11.5),(0,0,5.4),34)
for k,(r,n,p) in mods.items(): r.location=Vector(r["home"])
# ---- destruction: street side shattered (fractured sets), fragments thrown forward/down
rnd=random.Random(3); BREAK=["ShopD","ShopW","UpFL","UpFR","Balc","RoofLF","RoofRF","GabL"]
for k in BREAK:
    r,n,p=mods[k]
    for o in n: o.hide_render=True
    fr,fn=imp(os.path.join(REAL.split("/Assets/")[0],p["fractured"])); fr.location=r.location
    for o in fn:
        if o.type!='MESH' or o.name.startswith("UCX_"): o.hide_render=True; continue
        h=(o.matrix_world.translation.z)
        fall=rnd.random()<.55
        o.location+=Vector((rnd.uniform(-.6,.6),-rnd.uniform(.4,2.6 if fall else 1.0),-(h*rnd.uniform(.6,.95) if fall else rnd.uniform(0,.4))))
        o.rotation_euler=Euler((rnd.uniform(-.9,.9),rnd.uniform(-.9,.9),rnd.uniform(-.9,.9)))
    bpy.context.view_layer.update()
    for o in fn:   # keep debris above the ground
        if o.type=='MESH' and not o.hide_render:
            zmin=min((o.matrix_world@v.co).z for v in o.data.vertices); gz=H(o.matrix_world.translation.x,o.matrix_world.translation.y)
            if zmin<gz: o.location.z+=gz-zmin
shot("04_destruction",(9.5,-15.5,4.6),(-.4,-1.5,2.6),34)
