"""Realistic v1: the one render pass (usage-capped). Scenes: (a) lineup of all 14 L1-10 + Daily Boss pieces (LOD0, 1024 maps),
(b) L3 Brick Bakery and DB Ironjaw Golem assembled from the layout blocks with realistic pieces (scaled per block, UV0 rebuilt by the
metric box rule), (c) destruction: fractured sets mid-break. Writes /workspace/crashverse_previews/realistic_v1/*.png."""
import bpy,sys,os,json,math,random,subprocess
from mathutils import Vector,Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(HERE,"..","..",".."))
sys.path.insert(0,os.path.join(HERE,"..","realism")); sys.path.insert(0,HERE)
import rlib as R, rscene as S, resurf as RS
OUT="/workspace/crashverse_previews/realistic_v1"; REAL=RS.REAL; SAMPLES=int(os.environ.get("RT_SAMPLES","64")); W=int(os.environ.get("RT_W","1200"))
SC=json.load(open(ROOT+"/models/Realistic_Scope_L1-10.json")); ORDER=SC["order"]
mats={}
def kit_of(a): return a.split("_")[1][4:]
def MAT(name):
    if name not in mats:
        k=name[6:].replace("_Inner",""); inner=name.endswith("_Inner"); fol=RS.SETS[k][1]
        mats[name]=R.lit_material(name+"_R",f"{REAL}/{fol}/Textures/T_Real{k}{'_Inner' if inner else ''}",vcol=False)
    return mats[name]
import re
def imp(path,keep=lambda o:re.search(r"_LOD0(\.\d+)?$",o.name)):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path,axis_forward='-Z',axis_up='Y'); new=[o for o in bpy.data.objects if o not in before]
    vis=[]
    for o in new:
        if o.type=='MESH':
            if o.name.startswith("UCX_") or not keep(o): o.hide_render=True; continue
            for s in o.material_slots: s.material=MAT(s.material.name.split(".")[0])
            vis.append(o)
        elif o.type=='EMPTY' and o.parent is not None: o.hide_render=True
    return vis
def path(a,frac=False): k=kit_of(a); return f"{REAL}/{RS.SETS[k][1]}/{a}{'_Fractured' if frac else ''}.fbx"
def granite():
    m=bpy.data.materials.new("Granite"); m.use_nodes=True; b=m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value=(0.067,0.077,0.104,1); b.inputs["Roughness"].default_value=.45; return m
bpy.ops.wm.read_factory_settings(use_empty=True)
S.setup(res=(W,int(W*2/3)),samples=SAMPLES,sun_el=50,sun_az=-35,sun_str=4.0,hdri_str=1.0,hdri_rot=60)
# ground: graded CC0 grass, tiling 2 m
bpy.ops.mesh.primitive_plane_add(size=400); gp=bpy.context.object
gm=R.lit_material("Ground",f"{REAL}/Materials/Textures/T_Grass",vcol=False); gp.data.materials.append(gm)
gp.data.uv_layers[0].name="UV0"
for l in gp.data.loops: gp.data.uv_layers[0].data[l.index].uv=(gp.data.vertices[l.vertex_index].co.x/2,gp.data.vertices[l.vertex_index].co.y/2)
# (a) lineup
x=0.0; LINE={}
for a in ORDER:
    vis=imp(path(a)); o=vis[0]; bb=[o.matrix_world@Vector(c) for c in o.bound_box]; wx=max(v.x for v in bb)-min(v.x for v in bb)
    for v in vis: v.location.x+=x+wx/2; v.location.y+=0
    LINE[a]=x+wx/2; x+=wx+0.6
LX=x
# (b) assemblies at y=+30 (bakery) and y=+60 (golem)
blk={}
for a,u in SC["assets"].items():
    for ref in u["blocks"]: blk[ref]=a
GRAN=granite()
def assemble(tag,layout,origin):
    lay=json.loads(subprocess.check_output(["git","-C",ROOT,"show",f"{SC['ref']}:Assets/_Project/Resources/Data/Layouts/{layout}.json"]))
    for b in lay["blocks"]:
        a=blk.get(f"{tag}:{b['id']}","HELD"); sx,sy,sz=b["s"]; px,py,pz=b["p"]       # Unity (x, y up, z) -> Blender (x, z, y)
        size=Vector((sx,sz,sy)); ctr=Vector(origin)+Vector((px,pz,py))
        if a=="HELD" or b.get("indestructible") and a=="HELD":
            bpy.ops.mesh.primitive_cube_add(size=1); o=bpy.context.object; o.scale=size; o.location=ctr; o.data.materials.append(GRAN); continue
        vis=imp(path(a)); o=vis[0]
        me=o.data.copy(); o.data=me; o.parent=None; o.matrix_world=Matrix.Identity(4)
        co=[v.co.copy() for v in me.vertices]; lo=Vector([min(c[i] for c in co) for i in range(3)]); hi=Vector([max(c[i] for c in co) for i in range(3)]); ext=hi-lo
        # orient: piece long/thin axes onto the block's
        tgt=sorted(range(3),key=lambda i:size[i]); src=sorted(range(3),key=lambda i:ext[i])
        if a.endswith("_Joint") or a.endswith("_Wall") or a.endswith("_Floor") or a.endswith("_Roof"):
            perm={src[k]:tgt[k] for k in range(3)}
        else: perm={0:0,1:1,2:2}
        for v in me.vertices:
            c=(v.co-(lo+hi)/2); n=Vector((0,0,0))
            for s_,t_ in perm.items(): n[t_]=c[s_]/max(ext[s_],1e-6)*size[t_]
            v.co=n
        me.update(); o.location=ctr
        k=kit_of(a); RS.box_uv([o],RS.SETS[k][4],RS.SETS[k][6],max(range(3),key=lambda i:size[i]),props=(k=="Props"))
assemble("L3","c1_l03_bakery",(LX+6,0,0)); assemble("DB1","titan_ash_bridge",(LX+22,0,0))
# (c) destruction
random.seed(3); DX=LX+40
for i,(a,off) in enumerate((("Kit_RealBrick_Wall",(0,0,0)),("Kit_RealWood_Block",(3.2,-1.2,0)),("Kit_RealSteel_Block",(-2.4,-1.6,0)),("Kit_RealProps_Barrel",(1.6,-2.6,0)),("Kit_RealRoof_Roof",(-0.4,1.2,2.9)))):
    vis=imp(path(a,True),keep=lambda o:"_frag_" in o.name)
    c=sum((o.matrix_world.translation for o in vis),Vector())/len(vis)
    for o in vis:
        d=(o.matrix_world.translation-c); d.z=abs(d.z)+.3; t=random.uniform(.25,.9)
        o.location=o.location+Vector(off)+Vector((DX,0,0))+d.normalized()*t*1.4+Vector((0,0,random.uniform(0,.9)))
        o.rotation_euler=(random.uniform(-.6,.6),random.uniform(-.6,.6),random.uniform(-.6,.6))
def shot(name,loc,tg,lens,res=None):
    sc=bpy.context.scene; r=res or (W,int(W*2/3)); sc.render.resolution_x,sc.render.resolution_y=r
    S.camera(loc,tg,lens,name); S.render(f"{OUT}/{name}.png"); print("SHOT",name,flush=True)
S.cap_textures()
SH=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
if not SH or "lineup" in SH: shot("01_lineup_pieces",(LX/2,-LX*0.80,LX*0.20),(LX/2,0,0.6),27,(W,int(W*0.42)))
if not SH or "levels" in SH: shot("02_L3_bakery_DB_golem",(LX+14,-24,8.5),(LX+14,0,2.6),33)
if not SH or "destruction" in SH: shot("03_destruction",(DX+0.5,-10.5,3.6),(DX+0.3,-0.4,1.3),35)
