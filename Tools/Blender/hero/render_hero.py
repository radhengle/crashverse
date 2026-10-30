# Hero buildings v1 renders: each building from its layout JSON (Unity space -> Blender) on Island_Sunnyville,
# art sky gradient + soft clouds, warm sun, soft shadows; one mid-destruction shot with the fractured sets.
# usage: blender -b --python render_hero.py -- [shots]   shots: Bld_*,destruction,lineup,sheet (default all)
import sys,os,bpy,json,math,glob,random,subprocess,shutil; sys.path.insert(0,"/workspace/wt/3d-modeler-v3/Tools/Blender/v3"); import rig
from mathutils import Vector,Matrix,Quaternion,Euler
ROOT="/workspace/wt/3d-modeler-v3"; SRC="/workspace/crashverse/models_src"; VER=os.environ.get("HVER","v2"); OUT=f"{ROOT}/models/previews/hero_buildings_{VER}"; PUB=f"/workspace/crashverse_previews/hero_buildings_{VER}"
os.makedirs(OUT,exist_ok=True); os.makedirs(PUB,exist_ok=True)
A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
BLDS=["Bld_CastleKeep","Bld_Watchtower","Bld_Gatehouse","Bld_Townhouse"]
SHOTS=A[0].split(",") if A else BLDS+["destruction","detail","lineup","sheet"]
TEXCAP=int(os.environ.get("HTEX","1024"))   # Android cap for building textures: render what the phone gets
SAMPLES=int(os.environ.get("HSAMPLES","160"))
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0))); Ci=C.inverted()
CAPPED=set(); ORIG={}
def cap_image(im,cap=None):
    """point the image at a LANCZOS-downscaled copy (what the Android import gets) - real files, so Cycles can't use the source"""
    cap=cap or TEXCAP; src=ORIG.setdefault(im.name,bpy.path.abspath(im.filepath)); d=f"/tmp/hero_tex{cap}"; os.makedirs(d,exist_ok=True)
    dst=f"{d}/{os.path.basename(src)}"
    if not os.path.exists(dst) or os.path.getmtime(dst)<os.path.getmtime(src):
        subprocess.check_call(["/usr/bin/python3","-c",f"from PIL import Image;Image.open({src!r}).resize(({cap},{cap}),Image.LANCZOS).save({dst!r})"])
    im.filepath=dst; im.reload(); CAPPED.add(im.name)
def uncap_all():
    for n,src in ORIG.items():
        im=bpy.data.images.get(n)
        if im: im.filepath=src; im.reload()
def fix_images():
    for im in bpy.data.images:
        if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
            h=glob.glob(f"{ROOT}/Assets/_Project/Models/**/{os.path.basename(im.filepath)}",recursive=True)
            if h: im.filepath=h[0]; im.reload()
        if TEXCAP and im.name.startswith("T_") and im.name not in CAPPED and im.size[0]>TEXCAP: cap_image(im)
LIB={}
def lib(blend,keep):
    key=(blend,keep.__name__ if hasattr(keep,"__name__") else str(keep))
    if blend not in LIB:
        with bpy.data.libraries.load(blend) as (s,d): d.objects=[n for n in s.objects if not n.startswith(("UCX_","JNT_","ANCHOR_"))]
        fix_images(); objs=[o for o in d.objects if o]
        tmp=bpy.data.collections.new("tmp"); bpy.context.scene.collection.children.link(tmp)
        for o in objs: tmp.objects.link(o)
        bpy.context.view_layer.update(); W={o.name:o.matrix_world.copy() for o in objs}
        for o in objs: tmp.objects.unlink(o)
        bpy.context.scene.collection.children.unlink(tmp)
        LIB[blend]=[(o,W[o.name]) for o in objs if o.type=='MESH']
    return [(o,w) for o,w in LIB[blend] if keep(o.name)]
def inst(blend,M,keep,col):
    out=[]
    for o,w in lib(blend,keep):
        c=o.copy(); c.parent=None; c.matrix_world=M@w; col.objects.link(c); out.append(c)
    return out
def u2b(pos,rot):
    R=Ci@Quaternion((rot["w"],rot["x"],rot["y"],rot["z"])).to_matrix()@C; t=Ci@Vector((pos["x"],pos["y"],pos["z"]))
    return Matrix.Translation(t)@R.to_4x4()
def kitblend(k,fr=False): return f"{SRC}/Kit_{k.split('_')[1]}/{k}{'_Fractured' if fr else ''}.blend"
def building(name,M,fractured=lambda p,Mw:False):
    J=json.load(open(f"{ROOT}/Assets/_Project/Models/Buildings/{name}.layout.json")); col=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    objs=[]; frags=[]
    for p in J["pieces"]:
        Mw=M@u2b(p["position"],p["rotation"]); k=p["kit"]
        if fractured(p,Mw):
            f=inst(kitblend(k,True),Mw,lambda n,k=k:n.startswith(k+"_frag_"),col); frags+=f; objs+=f
        else: objs+=inst(kitblend(k),Mw,lambda n,k=k:n==k+"_LOD0",col)
    return objs,frags,col,J
RES=tuple(int(x) for x in os.environ.get("HRES","1600x900").split("x")); OUT=os.environ.get("HOUT",OUT); PUB=os.environ.get("HPUB",PUB); os.makedirs(OUT,exist_ok=True); os.makedirs(PUB,exist_ok=True)
sc=rig.setup(SAMPLES,res=RES,ambient=0.75)
sun=bpy.data.objects["Sun"]; sun.data.energy=4.6; sun.data.angle=math.radians(7); sun.data.color=rig.lin("#FFE7C2")[:3]
def sun_dir(d): sun.rotation_euler=(-Vector(d)).normalized().to_track_quat('Z','Y').to_euler()
sun_dir((0.45,-0.75,-0.55))
def load_place(blend,M,keep):
    with bpy.data.libraries.load(blend) as (s,d): d.objects=[n for n in s.objects if keep(n)]
    fix_images(); L=[o for o in d.objects if o]
    for o in L: sc.collection.objects.link(o)
    bpy.context.view_layer.update(); W={o.name:o.matrix_world.copy() for o in L}; out=[]
    for o in L:
        if o.type!='MESH': continue
        o.parent=None; o.matrix_world=M@W[o.name]; out.append(o)
    return out
isl=load_place(f"{SRC}/Island_Sunnyville/Island_Sunnyville.blend",Matrix(),lambda n:n.startswith(("Island_Sunnyville_Mesh","Island_Sunnyville_Satellite","VFX_Waterfall")) and not n.endswith(("_LOD1","_LOD2")))
wr=load_place(f"{SRC}/Wrecker_v2/Wrecker_v2.blend",Matrix.Translation((0,18.0,0)),lambda n:not n.startswith("UCX") and not n.endswith(("_LOD1","_LOD2","_HP")) and not n.startswith(("HPR","BAKE"))) if os.path.exists(f"{SRC}/Wrecker_v2/Wrecker_v2.blend") else []
for o in wr:
    if o.users_collection and "HP" in o.users_collection[0].name: o.hide_render=True
sc.render.film_transparent=True
SKY=r"""
import sys,random;from PIL import Image,ImageDraw,ImageFilter
p=sys.argv[1];im=Image.open(p).convert('RGBA');w,h=im.size;bg=Image.new('RGBA',(w,h))
a=(0x2F,0x8C,0xFF);b=(0xBD,0xEB,0xFF)
for y in range(h):
  t=min(1,y/(h*0.8));bg.paste(tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))+(255,),(0,y,w,y+1))
cl=Image.new('RGBA',(w,h),(0,0,0,0));d=ImageDraw.Draw(cl);R=random.Random(int(sys.argv[2]))
for c in range(7):
  cx=R.uniform(0,w);cy=R.uniform(h*0.05,h*0.42);s=R.uniform(0.6,1.3)*w/14
  for k in range(7):
    x=cx+R.uniform(-1.6,1.6)*s;y=cy+R.uniform(-0.25,0.3)*s;r=R.uniform(0.45,0.9)*s
    d.ellipse([x-r*1.3,y-r*0.7,x+r*1.3,y+r*0.7],fill=(255,255,255,int(R.uniform(150,215))))
cl=cl.filter(ImageFilter.GaussianBlur(w/220))
bg=Image.alpha_composite(bg,cl)
Image.alpha_composite(bg,im).convert('RGB').save(p)"""
def pub(p):
    d=f"{PUB}/{os.path.basename(p)}"
    if os.path.abspath(d)!=os.path.abspath(p): shutil.copy(p,d)
def shoot(path,loc,target,lens=35,dof=False,fstop=5.6,seed=3):
    cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); cam.data.lens=lens; cam.data.clip_end=3000
    loc=Vector(loc); f=(Vector(target)-loc).normalized(); rg=f.cross(Vector((0,0,1))).normalized(); up=rg.cross(f)
    cam.location=loc; cam.rotation_euler=Matrix((rg,up,-f)).transposed().to_euler(); sc.camera=cam
    cam.data.dof.use_dof=dof; cam.data.dof.focus_distance=(Vector(target)-loc).length; cam.data.dof.aperture_fstop=fstop
    sc.render.filepath=path; bpy.context.view_layer.update(); bpy.ops.render.render(write_still=True); bpy.data.objects.remove(cam)
    subprocess.check_call(["/usr/bin/python3","-c",SKY,path,str(seed)]); rig.caption(path); pub(path); print("PREVIEW",path,flush=True)
# placement: building front (-Y in the layout) turned to face the Wrecker on the sand pad (+Y)
SPOT=Matrix.Translation((0,-1.0,0))@Matrix.Rotation(math.pi,4,'Z')
# per building: view direction (target -> camera), lens, frame margin; camera distance fitted to the bbox
CAM={"Bld_CastleKeep":((0.62,0.72,0.32),35,0.10),
     "Bld_Watchtower":((0.55,0.80,0.22),40,0.10),
     "Bld_Gatehouse":((0.40,0.86,0.30),32,0.06),
     "Bld_Townhouse":((0.62,0.74,0.25),38,0.10)}
def fit(objs,dirv,lens,margin):
    cs=[o.matrix_world@Vector(c) for o in objs for c in o.bound_box]
    a=Vector([min(c[i] for c in cs) for i in range(3)]); b=Vector([max(c[i] for c in cs) for i in range(3)]); t=(a+b)/2
    f=-Vector(dirv).normalized(); rg=f.cross(Vector((0,0,1))).normalized(); up=rg.cross(f)
    tx=18.0/lens; ty=tx*RES[1]/RES[0]; D=0
    for c in cs:
        p=c-t; D=max(D,abs(p.dot(rg))/(tx*(1-margin))-p.dot(f),abs(p.dot(up))/(ty*(1-margin))-p.dot(f))
    return t-f*D,t
done=[]
for n in BLDS:
    if n not in SHOTS: continue
    objs,_,col,J=building(n,SPOT)
    for o in wr: o.hide_render=True
    dv,lens,mg=CAM[n]; loc,tg=fit(objs,dv,lens,mg); shoot(f"{OUT}/{n}.png",loc,tg,lens=lens,seed=BLDS.index(n)+3); done.append(f"{n}.png")
    for o in objs: bpy.data.objects.remove(o)
    bpy.data.collections.remove(col)
if "destruction" in SHOTS:
    hit=Vector((1.2,2.2,7.8))      # impact on the keep's front (world, after SPOT), upper storey / right turret
    def frac(p,Mw):
        c=Mw.translation; return (c-hit).length<3.6 and c.z>4.0
    objs,frags,col,J=building("Bld_CastleKeep",SPOT,frac)
    rnd=random.Random(7); g=Vector((0,0,-9.81))
    for o in frags:   # ballistic snapshot ~0.1-0.7 s after impact: core blown out towards +Y (front), rim pieces still in place
        c=o.matrix_world.translation.copy(); d=c-hit; dist=d.length
        if dist>2.9 and rnd.random()<0.75: continue
        out=Vector((d.x*0.6,abs(d.y)+1.0,0.25)).normalized(); v=out*rnd.uniform(5.0,10.0)*max(0.4,1.3-dist/4)+Vector((rnd.uniform(-1,1),0,rnd.uniform(0.5,2.5)))
        t=rnd.uniform(0.25,0.95); p_=c+v*t+g*(0.5*t*t)
        if p_.z<0.25: p_.z=rnd.uniform(0.15,0.35)
        o.location=p_
        o.rotation_euler=Euler((o.rotation_euler.x+rnd.uniform(-1.6,1.6)*t*2,o.rotation_euler.y+rnd.uniform(-1.6,1.6)*t*2,o.rotation_euler.z+rnd.uniform(-1.6,1.6)*t*2))
    # cannonball + dust puffs at the impact
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.32,location=hit+Vector((0.3,1.6,0.3)),segments=24,ring_count=12); ball=bpy.context.object
    mb=bpy.data.materials.new("ball"); mb.use_nodes=True; bb=mb.node_tree.nodes["Principled BSDF"]; bb.inputs["Base Color"].default_value=rig.lin("#2B2F38"); bb.inputs["Metallic"].default_value=1; bb.inputs["Roughness"].default_value=0.35; ball.data.materials.append(mb); bpy.ops.object.shade_smooth()
    md=bpy.data.materials.new("dust"); md.use_nodes=True; nt=md.node_tree; nt.nodes.clear(); out=nt.nodes.new("ShaderNodeOutputMaterial"); vs=nt.nodes.new("ShaderNodeVolumePrincipled")
    vs.inputs["Color"].default_value=rig.lin("#FFD29A"); vs.inputs["Density"].default_value=1.6; vs.inputs["Absorption Color"].default_value=rig.lin("#FFE2B8"); vs.inputs["Emission Color"].default_value=rig.lin("#F2B877"); vs.inputs["Emission Strength"].default_value=0.35; nt.links.new(vs.outputs[0],out.inputs["Volume"])
    dust=[ball]
    for i in range(22):
        p=hit+Vector((rnd.uniform(-2.2,2.2),rnd.uniform(0.8,4.0),rnd.uniform(-1.8,1.2))); bpy.ops.mesh.primitive_ico_sphere_add(radius=rnd.uniform(0.6,1.4),subdivisions=3,location=p)
        o=bpy.context.object; o.data.materials.append(md); dust.append(o)
    loc,tg=fit(objs,(0.55,0.80,0.22),35,0.06); shoot(f"{OUT}/CastleKeep_destruction.png",loc,tg,lens=35,seed=11); done.append("CastleKeep_destruction.png")
    for o in objs+dust: bpy.data.objects.remove(o)
    bpy.data.collections.remove(col)
if "detail" in SHOTS:   # close-up: stone ground floor, shuttered window + flower box + ivy, jetty, tile roof eave, annex roof
    objs,_,col,J=building("Bld_Townhouse",SPOT)
    for o in wr: o.hide_render=True
    DL=((3.4,8.6,2.3),(-1.3,1.0,3.3),36)
    shoot(f"{OUT}/Townhouse_detail_closeup.png",*DL[:2],lens=DL[2],seed=9); done.append("Townhouse_detail_closeup.png")
    if os.environ.get("HDETAIL2048","1")=="1":    # same frame with the 2048 source maps, for the 1024-cap comparison
        for o in objs: bpy.data.objects.remove(o)
        bpy.data.collections.remove(col)
        uncap_all(); objs,_,col,J=building("Bld_Townhouse",SPOT)
        shoot(f"{OUT}/Townhouse_detail_closeup_src2048.png",*DL[:2],lens=DL[2],seed=9); done.append("Townhouse_detail_closeup_src2048.png")
        for n in list(ORIG): cap_image(bpy.data.images[n])
    for o in objs: bpy.data.objects.remove(o)
    bpy.data.collections.remove(col)
if "lineup" in SHOTS:   # all four on the island, Wrecker on the pad (gameplay framing)
    P={"Bld_CastleKeep":(Matrix.Translation((0,-3.0,0))@Matrix.Rotation(math.pi,4,'Z')),"Bld_Watchtower":(Matrix.Translation((-12.5,1.0,0))@Matrix.Rotation(math.pi*0.85,4,'Z')),
       "Bld_Townhouse":(Matrix.Translation((11.0,2.0,0))@Matrix.Rotation(math.pi*1.12,4,'Z')),"Bld_Gatehouse":(Matrix.Translation((0,-9.6,0.7))@Matrix.Rotation(math.pi,4,'Z'))}
    allo=[]
    for n,M in P.items():
        o,_,_,_=building(n,M); allo+=o
    for o in wr: o.hide_render=False
    shoot(f"{OUT}/Hero_lineup_gameplay.png",(3.0,30.0,9.0),(0,-2.0,4.0),lens=26,seed=5); done.append("Hero_lineup_gameplay.png")
if "sheet" in SHOTS:
    Pp=[f"{OUT}/{x}" for x in [f"{n}.png" for n in BLDS]+["CastleKeep_destruction.png","Townhouse_detail_closeup.png","Hero_lineup_gameplay.png"] if os.path.exists(f"{OUT}/{x}")]
    rig.strip(Pp,f"{OUT}/sheet.png",cols=3,cell=640,aspect=16/9,title=f"CRASHVERSE hero buildings {VER} - modular, destructible, textures at {TEXCAP or 2048} px (Blender Filmic preview, final look sign-off in Unity ACES)")
    pub(f"{OUT}/sheet.png"); print("PUBLISHED sheet",flush=True)
print("DONE",done,flush=True)
