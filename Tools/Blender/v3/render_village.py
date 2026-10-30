# Village on Island_Sunnyville built FROM the layout JSONs (Unity space -> Blender), Wrecker on the sand pad.
# usage: blender -b --python render_village.py -- [shots]   shots: gameplay,beauty,destruction,buildings,sheet (default all)
import sys,os,bpy,json,math,glob,random,subprocess,shutil; sys.path.insert(0,os.path.dirname(__file__)); import rig
from mathutils import Vector,Matrix,Quaternion,Euler
ROOT="/workspace/wt/3d-modeler-v3"; SRC="/workspace/crashverse/models_src"; OUT=f"{ROOT}/models/previews/v3_buildings"; PUB="/workspace/crashverse_previews/models_v3_buildings"
os.makedirs(OUT,exist_ok=True); os.makedirs(PUB,exist_ok=True)
A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []; SHOTS=A[0].split(",") if A else ["gameplay","beauty","destruction","buildings","sheet"]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0))); Ci=C.inverted()
def fix_images():
    for im in bpy.data.images:
        if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
            h=glob.glob(f"{ROOT}/Assets/_Project/Models/**/{os.path.basename(im.filepath)}",recursive=True)
            if h: im.filepath=h[0]; im.reload()
def load_all(blend,keep):
    with bpy.data.libraries.load(blend) as (s,d): d.objects=[n for n in s.objects if keep(n)]
    fix_images(); return [o for o in d.objects if o]
def place(blend,M,keep):
    L=load_all(blend,keep)
    for o in L: bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.update(); W={o.name:o.matrix_world.copy() for o in L}   # world matrices are only valid once linked + evaluated
    out=[]
    for o in L:
        if o.type!='MESH': continue
        o.parent=None; o.matrix_world=M@W[o.name]; out.append(o)
    return out
CACHE={}
def inst(blend,M,keep):
    if blend not in CACHE: CACHE[blend]=[o for o in load_all(blend,keep) if o.type=='MESH']
    out=[]
    for o in CACHE[blend]:
        c=o.copy(); c.parent=None; c.matrix_world=M@o.matrix_world; bpy.context.scene.collection.objects.link(c); out.append(c)
    return out
def u2b(pos,rot):
    R=Ci@Quaternion((rot["w"],rot["x"],rot["y"],rot["z"])).to_matrix()@C; t=Ci@Vector((pos["x"],pos["y"],pos["z"]))
    return Matrix.Translation(t)@R.to_4x4()
def kitblend(k,fr=False):
    sub="Base" if k.startswith("Kit_Base") else k.split("_")[1]; return f"{SRC}/Kit_{sub}/{k}{'_Fractured' if fr else ''}.blend"
BLD={}
def building(path,fractured_above=None):
    J=json.load(open(path)); R=u2b(J["root"]["position"],J["root"]["rotation"]); objs=[]; frags=[]
    for p in J["pieces"]:
        M=R@u2b(p["position"],p["rotation"]); k=p["kit"]
        if fractured_above is not None and p["fractured"] and (M.translation.z>fractured_above):
            f=inst(kitblend(k,True),M,lambda n,k=k:n.startswith(k+"_frag_")); frags+=f; objs+=f
        else: objs+=inst(kitblend(k),M,lambda n,k=k:n==k+"_LOD0")
    return J["building"],objs,frags
sc=rig.setup(128,res=(1600,900))
isl=place(f"{SRC}/Island_Sunnyville/Island_Sunnyville.blend",Matrix(),lambda n:not n.startswith("UCX") and not n.endswith(("_LOD1","_LOD2")) and "HP" not in n and not n.startswith(("HPR","BAKE")))
isl=[o for o in isl if not o.name.endswith(("_HP","_M"))]
wr=place(f"{SRC}/Wrecker_v2/Wrecker_v2.blend",Matrix.Translation((0,18.0,0)),lambda n:not n.startswith("UCX") and not n.endswith(("_LOD1","_LOD2","_HP")) and not n.startswith(("HPR","BAKE")))
for o in wr: 
    if o.users_collection and "HP" in o.users_collection[0].name: o.hide_render=True
LAY=sorted(glob.glob(f"{ROOT}/Assets/_Project/Models/Buildings/*.layout.json"))
for p in LAY: n,o,_=building(p); BLD[n]=o
vil=[o for v in BLD.values() for o in v]
if os.environ.get("VDBG"):
    bpy.context.view_layer.update(); dg=bpy.context.evaluated_depsgraph_get()
    for p in [(0,-1),(-1,0),(0,2),(1,-3),(-2,-2),(3,1)]:
        h=sc.ray_cast(dg,Vector((p[0],p[1],30)),Vector((0,0,-1))); print("RAY",p,h[0],h[4].name if h[4] else None,tuple(round(x,2) for x in h[1]))
    for o in sc.objects:
        if o.type=='MESH' and not o.hide_render:
            d=o.dimensions
            if max(d)>4 and "." not in o.name[:4]: print("BIG",o.name,tuple(round(x,1) for x in d),tuple(round(x,1) for x in o.matrix_world.translation))
    raise SystemExit
rig.art_sky(sc); upd=lambda: bpy.context.view_layer.update()
SKY="""
import sys;from PIL import Image
p=sys.argv[1];im=Image.open(p).convert('RGBA');w,h=im.size;bg=Image.new('RGBA',(w,h))
a=(0x2F,0x8C,0xFF);b=(0xBD,0xEB,0xFF)
for y in range(h):
  t=min(1,y/(h*0.8));bg.paste(tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))+(255,),(0,y,w,y+1))
Image.alpha_composite(bg,im).convert('RGB').save(p)"""
def shoot_cam(path,loc,target,lens=35,dof=False,fstop=4.0):
    cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); cam.data.lens=lens; cam.data.clip_end=2000
    loc=Vector(loc); f=(Vector(target)-loc).normalized(); rg=f.cross(Vector((0,0,1))).normalized(); up=rg.cross(f)
    cam.location=loc; cam.rotation_euler=Matrix((rg,up,-f)).transposed().to_euler(); sc.camera=cam
    cam.data.dof.use_dof=dof; cam.data.dof.focus_distance=(Vector(target)-loc).length; cam.data.dof.aperture_fstop=fstop
    sc.render.filepath=path; upd(); bpy.ops.render.render(write_still=True); bpy.data.objects.remove(cam)
    subprocess.check_call(["/usr/bin/python3","-c",SKY,path]); rig.caption(path); print("PREVIEW",path,flush=True)
def pub(name):
    shutil.copy(f"{OUT}/{name}",f"{PUB}/{name}")
done=[]
if "gameplay" in SHOTS:   # over-the-shoulder from behind the Wrecker on the sand pad, aiming into the village
    shoot_cam(f"{OUT}/Village_gameplay.png",(2.4,26.5,5.4),(-0.5,3.0,1.2),lens=30); done.append("Village_gameplay.png")
if "beauty" in SHOTS:
    shoot_cam(f"{OUT}/Village_beauty.png",(-19.0,11.0,3.2),(-8.0,0.5,2.2),lens=50,dof=True,fstop=2.8); done.append("Village_beauty.png")
if "destruction" in SHOTS:   # brick house upper storey + roof as fractured sets, fragments flying from a hit on the +Y face
    for o in BLD["Bld_BrickHouse"]: o.hide_render=True
    _,bo,fr=building(f"{ROOT}/Assets/_Project/Models/Buildings/Bld_BrickHouse.layout.json",fractured_above=3.1)
    rnd=random.Random(4); hit=Vector((-4.5,-1.5,4.5))
    for o in fr:
        c=o.matrix_world.translation; d=(c-hit); d.z=0; dist=d.length
        if dist>3.4 and c.z<6.4: continue      # far side stays standing
        v=(d.normalized() if dist>1e-3 else Vector((0,-1,0))) + Vector((0,-0.5,0.55)); k=rnd.uniform(0.4,1.9)
        o.location=c+v*k+Vector((rnd.uniform(-.3,.3),rnd.uniform(-.3,.3),rnd.uniform(0,.8)))
        o.rotation_euler=Euler((rnd.uniform(-1,1),rnd.uniform(-1,1),rnd.uniform(-1,1)))@o.rotation_euler if False else Euler((o.rotation_euler.x+rnd.uniform(-1.2,1.2),o.rotation_euler.y+rnd.uniform(-1.2,1.2),o.rotation_euler.z+rnd.uniform(-1.2,1.2)))
    shoot_cam(f"{OUT}/Village_destruction.png",(1.5,9.5,4.0),(-4.0,-3.0,3.6),lens=32); done.append("Village_destruction.png")
    for o in bo: bpy.data.objects.remove(o)
    for o in BLD["Bld_BrickHouse"]: o.hide_render=False
if "buildings" in SHOTS:   # each building alone on a preview ground disc
    for o in isl+wr+vil: o.hide_render=True
    g=rig.ground(9,color="#7ED957")
    for n,objs in BLD.items():
        c=sum((o.matrix_world.translation for o in objs),Vector())/len(objs); g.location.x,g.location.y=c.x,c.y
        for o in objs: o.hide_render=False
        upd(); rig.shoot(f"{OUT}/{n}.png",objs,(0.55,1.0,0.45),lens=40,fill=2.1); done.append(f"{n}.png")
        for o in objs: o.hide_render=True
    for o in isl+wr+vil: o.hide_render=False
    bpy.data.objects.remove(g)
for d in done: pub(d); print("PUBLISHED",f"{PUB}/{d}",flush=True)
if "sheet" in SHOTS:
    P=[f"{OUT}/{x}" for x in ("Village_gameplay.png","Village_beauty.png","Village_destruction.png")+tuple(f"{n}.png" for n in sorted(BLD))+("Check_Brick_mortar.png","Check_Glass_fracture.png") if os.path.exists(f"{OUT}/{x}")]
    rig.strip(P,f"{OUT}/sheet.png",cols=3,cell=640,aspect=16/9,title="CRASHVERSE models v3 - village buildings from BuildingKit v3.1 (Blender Filmic preview, final look sign-off in Unity ACES)")
    pub("sheet.png"); print("PUBLISHED",f"{PUB}/sheet.png",flush=True)
