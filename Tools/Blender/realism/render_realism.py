"""Realism test renders from the EXPORTED FBX files (1024 texture copies, Cycles, AgX).
blender -b -P render_realism.py -- [shots]   shots: hero,closeup,tree,gameplay,neutral,side   (default all)"""
import bpy,math,os,sys,random,glob,subprocess
from mathutils import Vector
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import rlib as R, rscene as S, ground as GR
REAL=R.REAL; OUT="/workspace/crashverse_previews/realism_test_v1"
SH=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv and sys.argv[-1]!="--" else ["hero","closeup","tree","gameplay","neutral"]
SAMPLES=int(os.environ.get("RT_SAMPLES","128"))
MAT={"M_Bld_Townhouse":(f"{REAL}/Buildings/Hero/Bld_Townhouse/Textures/T_Bld_Townhouse",False),
     "M_Props_Village":(f"{REAL}/Props/Props_Village/Textures/T_Props_Village",False),
     "M_Ground_Patch":(f"{REAL}/Props/Ground_Patch/Textures/T_Ground_Patch",False),
     "M_GrassClump":(f"{REAL}/Foliage/GrassClump/Textures/T_GrassClump",True)}
for sp in ("Oak","Pine","Bush"):
    MAT[f"M_{sp}"]=(f"{REAL}/Foliage/{sp}/Textures/T_{sp}",True); MAT[f"M_{sp}_Billboard"]=(f"{REAL}/Foliage/{sp}/Textures/T_{sp}_Billboard",True)
mats={}
def M(name):
    if name not in mats:
        p,a=MAT[name]; mats[name]=R.lit_material(name+"_R",p,alpha=a,vcol=not name.endswith("_Billboard"))
    return mats[name]
def imp(path):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path,colors_type='LINEAR'); new=[o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type=='MESH':
            for s in o.material_slots:
                base=s.material.name.split(".")[0]; s.material=M(base)
            if o.name.startswith("UCX_") or any(o.name.endswith(f"_LOD{k}") for k in (1,2,3)): o.hide_render=True; o.hide_viewport=True
    root=[o for o in new if o.parent is None][0]; return root,new
def place(root,loc,rz=0,s=1.0):
    root.location=loc; root.rotation_euler=(math.radians(90) if False else root.rotation_euler[0],0,rz); root.scale=(s*root.scale[0],)*3 if False else root.scale*s
def inst(objs,loc,rz=0,s=1.0):
    """linked copies of the visible LOD0 mesh(es)"""
    out=[]
    for o in objs:
        if o.type!='MESH' or o.hide_render: continue
        c=bpy.data.objects.new(o.name+"_i",o.data); bpy.context.scene.collection.objects.link(c)
        c.matrix_world=o.matrix_world.copy(); out.append(c)
    e=bpy.data.objects.new("grp",None); bpy.context.scene.collection.objects.link(e)
    for c in out:
        mw=c.matrix_world.copy(); c.parent=e; c.matrix_parent_inverse=e.matrix_world.inverted(); c.matrix_world=mw
    e.location=loc; e.rotation_euler.z=rz; e.scale=(s,s,s); return e
bpy.ops.wm.read_factory_settings(use_empty=True)
house,hobj=imp(f"{REAL}/Buildings/Hero/Bld_Townhouse/Bld_Townhouse.fbx")
ground,_=imp(f"{REAL}/Props/Ground_Patch/Ground_Patch.fbx")
trees={sp:imp(f"{REAL}/Foliage/{sp}/{sp}.fbx") for sp in ("Oak","Pine","Bush")}
props={k:imp(f"{REAL}/Props/Props_Village/Prop_{k}.fbx") for k in ("Barrel","Crate","Cart","Rock_A","Rock_B","Rock_C")}
grass,gobj=imp(f"{REAL}/Foliage/GrassClump/GrassClump.fbx")
bpy.context.view_layer.update()
H=lambda x,y:GR.height(x,y)
# hero trees + bushes
place(trees["Oak"][0],(-9.2,-2.8,H(-9.2,-2.8)-.05),.4)
place(trees["Pine"][0],(8.4,3.4,H(8.4,3.4)-.05),0)
inst(trees["Pine"][1],(11.6,-2.2,H(11.6,-2.2)-.05),1.3,.82)
inst(trees["Oak"][1],(-14.5,6.5,H(-14.5,6.5)),2.1,.9)
place(trees["Bush"][0],(4.7,-4.25,H(4.7,-4.25)),0)
for (x,y,rz,s) in ((-4.45,-3.55,1.2,.8),(6.3,-1.2,2.0,1.1),(-6.8,1.9,.5,1.25),(3.6,4.4,.7,1.0),(-2.4,-9.8,1.9,.75)):
    inst(trees["Bush"][1],(x,y,H(x,y)),rz,s)
# props
place(props["Barrel"][0],(2.55,-3.75,H(2.55,-3.75)),.3)
inst(props["Barrel"][1],(3.2,-3.55,0),1.1,1.0)
place(props["Crate"][0],(3.35,-4.55,H(3.35,-4.55)),.25)
inst(props["Crate"][1],(3.3,-4.5,.71),.65,.85)
place(props["Cart"][0],(-2.8,-6.4,H(-2.8,-6.4)),math.radians(-28))
for k,(x,y,rz,s) in (("Rock_A",(1.9,-7.9,.4,1)),("Rock_B",(-.9,-6.0,1.2,1)),("Rock_C",(-3.3,-11.4,2.0,1)),("Rock_B",(1.4,-12.6,0,1.3)),("Rock_A",(-6.5,-5.5,1,.8)),("Rock_C",(7.2,-7.4,.2,.9))):
    root,ob=props[k]
    if root.get("_used"): inst(ob,(x,y,H(x,y)-.06),rz,s)
    else: place(root,(x,y,H(x,y)-.06),rz,s); root["_used"]=1
# grass clumps (instanced; in Unity: GPU-instanced detail mesh, 1 material)
g0=[o for o in gobj if o.name.endswith("_LOD0")][0]
GR.scatter(g0,int(os.environ.get("RT_GRASS","14000")))
g0.hide_render=True
# backdrop (render only): far meadow + distant trees so the horizon isn't empty
import bmesh
bd=bpy.data.objects.new("backdrop",bpy.data.meshes.new("bd")); bm=bmesh.new(); bmesh.ops.create_circle(bm,cap_ends=True,segments=48,radius=320); bm.to_mesh(bd.data); bpy.context.scene.collection.objects.link(bd)
bd.location.z=-.12
mb=bpy.data.materials.new("backdrop"); mb.use_nodes=True; nt=mb.node_tree; t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(f"{REAL}/Materials/Textures/T_Grass_A.png")
tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(1/4,1/4,1)
nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],t.inputs[0]); nt.links.new(t.outputs[0],nt.nodes["Principled BSDF"].inputs["Base Color"]); nt.nodes["Principled BSDF"].inputs["Roughness"].default_value=.85
bd.data.materials.append(mb)
rng=random.Random(9)
for i in range(70):
    a=rng.uniform(0,6.283); r=rng.uniform(30,70); x,y=math.cos(a)*r,math.sin(a)*r
    sp=rng.choice(["Pine","Pine","Oak"]); inst(trees[sp][1],(x,y,-.1),rng.uniform(0,6),rng.uniform(.8,1.3))
S.cap_textures()
def shot(name,loc,tg,lens,res=(1600,1067),view="AgX",look="Medium High Contrast",samples=None):
    S.setup(res=res,samples=samples or SAMPLES,view=view,look=look,sun_el=57,sun_az=-35,sun_str=4.0,hdri_str=1.0,hdri_rot=60) if not bpy.context.scene.world or bpy.context.scene.world.name!="Sky" else None
    sc=bpy.context.scene; sc.render.resolution_x,sc.render.resolution_y=res; sc.view_settings.view_transform=view
    try: sc.view_settings.look=look
    except Exception: sc.view_settings.look='None'
    sc.cycles.samples=samples or SAMPLES
    S.camera(loc,tg,lens); S.render(f"{OUT}/{name}.png"); print("SHOT",name)
if "hero" in SH: shot("01_hero_3q",(12.5,-15.5,6.2),(-.6,0,3.3),38)
if "closeup" in SH: shot("02_closeup_wall_window_roof",(-4.9,-8.6,2.3),(-1.5,-2.75,4.15),27)
if "tree" in SH: shot("03_tree_closeup",(-4.6,-11.2,2.3),(-9.0,-2.8,4.1),35)
if "gameplay" in SH: shot("04_gameplay_camera",(-1.2,-24.5,5.6),(0,0,2.4),30)
if "neutral" in SH: shot("04b_gameplay_camera_NeutralTonemap",(-1.2,-24.5,5.6),(0,0,2.4),30,view="Khronos PBR Neutral",look="None")
bpy.ops.wm.save_as_mainfile(filepath="/tmp/realism_scene.blend")
