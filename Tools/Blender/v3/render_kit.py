# v3 kit / Stone-vs-Base / no-probe steel / hero scene previews. usage: blender -b --python render_kit.py -- <mode>
import sys,os,bpy,math,subprocess; sys.path.insert(0,os.path.dirname(__file__)); import rig
from mathutils import Vector
OUT="/workspace/wt/3d-modeler-v3/models/previews/v3"; SRC="/workspace/crashverse/models_src"; os.makedirs(OUT,exist_ok=True)
MODE=sys.argv[sys.argv.index("--")+1]
for _o in list(bpy.data.objects): bpy.data.objects.remove(_o)   # drop startup cube/camera/light
def load(blend,off=(0,0,0),keep=lambda n:True,rotz=0.0):
    with bpy.data.libraries.load(blend) as (src,dst): dst.objects=[n for n in src.objects]
    out=[]
    for o in dst.objects:
        if o is None: continue
        bpy.context.scene.collection.objects.link(o)
        if o.type!='MESH' or o.name.startswith("UCX_") or not keep(o.name) or (o.users_collection and "HP" in o.users_collection[0].name) or o.name.endswith(("_HP","_M")) or o.name.startswith(("HPR","BAKE")):
            o.hide_render=True; continue
        out.append(o)
    import glob as _g
    for im in bpy.data.images:
        if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
            hit=_g.glob(f"/workspace/wt/3d-modeler-v3/Assets/_Project/Models/**/{os.path.basename(im.filepath)}",recursive=True)
            if hit: im.filepath=hit[0]; im.reload()
    from mathutils import Matrix
    R=Matrix.Translation(off)@Matrix.Rotation(rotz,4,'Z')
    for o in dst.objects:
        if o is not None and o.parent is None: o.matrix_world=R@o.matrix_world
    return out
lod0=lambda n:n.endswith("_LOD0")
def explode(objs,k=0.35):
    c=sum((o.matrix_world.translation for o in objs),Vector())/len(objs)
    for o in objs:
        d=o.matrix_world.translation-c; d.z=max(d.z,0)*0.5; o.location+=d*k
def _upd(): bpy.context.view_layer.update()
def masked_rgb(maskp,imgp):
    return subprocess.check_output(["/usr/bin/python3","-c",f"""
from PIL import Image; import numpy as np
m=np.asarray(Image.open({maskp!r}).convert('RGBA'),float)[...,3]>200; a=np.asarray(Image.open({imgp!r}).convert('RGB'),float); c=a[m].mean(0)
print('#%02X%02X%02X'%tuple(int(x) for x in c), round(float((0.2126*c[0]+0.7152*c[1]+0.0722*c[2])),1))"""]).decode().strip()
def mean_rgb(path):
    return subprocess.check_output(["/usr/bin/python3","-c",f"""
from PIL import Image; import numpy as np
a=np.asarray(Image.open({path!r}).convert('RGBA'),float); m=a[...,3]>200; c=a[...,:3][m].mean(0)
print('#%02X%02X%02X'%tuple(int(x) for x in c), round(float((0.2126*c[0]+0.7152*c[1]+0.0722*c[2])),1))"""]).decode().strip()
if MODE.startswith("kit_"):
    m=MODE[4:]; sc=rig.setup(96); rig.art_sky(sc); rig.ground(5,color="#7ED957")
    a=load(f"{SRC}/Kit_{m}/Kit_{m}_Wall.blend",(-1.8,0,0),lod0)
    b=load(f"{SRC}/Kit_{m}/Kit_{m}_Wall_Fractured.blend",(1.8,0,0)); explode(b)
    _upd(); rig.shoot(f"{OUT}/BuildingKit_{m}_gameplay.png",a+b,(0.3,-1.0,0.4),lens=40,fill=1.45)
elif MODE=="stone_base":
    sc=rig.setup(96); rig.ground(5,color="#7ED957")
    s=load(f"{SRC}/Kit_Stone/Kit_Stone_Wall.blend",(-1.9,0,0),lod0); g=load(f"{SRC}/Kit_Base/Kit_Base_Indestructible.blend",(1.9,0,0),lod0)
    pg=bpy.data.objects["PreviewGround"]
    # value measurement: each alone on transparent film, no ground
    sc.render.film_transparent=True; pg.hide_render=True
    vals={}; rig.caption=lambda *x,**y:None
    for name,show,hide in (("Stone",s,g),("Base",g,s)):
        for o in hide: o.hide_render=True
        for o in show: o.hide_render=False
        sc.render.film_transparent=True; pg.hide_render=True; _upd(); rig.shoot(f"/tmp/3dm_mask_{name}.png",s+g,(0.3,-1.0,0.6),lens=40,fill=1.5)
        for o in hide: o.hide_render=False
        sc.render.film_transparent=False; pg.hide_render=False; _upd(); rig.shoot(f"/tmp/3dm_full_{name}.png",s+g,(0.3,-1.0,0.6),lens=40,fill=1.5)
        vals[name]=masked_rgb(f"/tmp/3dm_mask_{name}.png",f"/tmp/3dm_full_{name}.png")
    for o in s+g: o.hide_render=False
    pg.hide_render=False; sc.render.film_transparent=False
    import importlib; importlib.reload(rig); rig.art_sky(sc)
    _upd(); rig.shoot(f"{OUT}/Stone_vs_Base_colour.png",s+g,(0.3,-1.0,0.6),lens=40,fill=1.5)
    subprocess.check_call(["/usr/bin/python3","-c",f"from PIL import Image; Image.open('{OUT}/Stone_vs_Base_colour.png').convert('L').save('{OUT}/Stone_vs_Base_grey.png')"])
    print("VALUES",vals,flush=True)
elif MODE=="steel_noprobe":
    # no local reflection probe: reflections fall back to a flat ambient sky (Unity's default environment reflection), no HDR detail
    sc=rig.setup(96,probe=True); rig.ground(5,color="#7ED957")
    wn=sc.world.node_tree; out=wn.nodes["World Output"]; prev=out.inputs[0].links[0].from_socket
    lp=wn.nodes.new("ShaderNodeLightPath"); fb=wn.nodes.new("ShaderNodeBackground"); fb.inputs[0].default_value=rig.lin("#BFD9EE"); fb.inputs[1].default_value=0.8
    mx=wn.nodes.new("ShaderNodeMixShader"); wn.links.new(lp.outputs["Is Glossy Ray"],mx.inputs[0]); wn.links.new(prev,mx.inputs[1]); wn.links.new(fb.outputs[0],mx.inputs[2]); wn.links.new(mx.outputs[0],out.inputs[0])
    a=load(f"{SRC}/Kit_Steel/Kit_Steel_Wall.blend",(0,0,0),lod0); g=bpy.data.objects["PreviewGround"]
    rig.caption=lambda *x,**y:None
    sc.render.film_transparent=True; g.hide_render=True; _upd(); rig.shoot("/tmp/3dm_mask_steel.png",a,(0.35,-1.0,0.4),lens=40,fill=1.6)
    sc.render.film_transparent=False; g.hide_render=False; _upd(); rig.shoot("/tmp/3dm_full_steel.png",a,(0.35,-1.0,0.4),lens=40,fill=1.6)
    v=masked_rgb("/tmp/3dm_mask_steel.png","/tmp/3dm_full_steel.png")
    import importlib; importlib.reload(rig); rig.art_sky(sc)
    _upd(); rig.shoot(f"{OUT}/Steel_noprobe_gameplay.png",a,(0.35,-1.0,0.4),lens=40,fill=1.6)
    print("STEEL_NOPROBE",v,"target >= #9AA3AF (lum 160.6)",flush=True)
elif MODE=="hero":
    sc=rig.setup(128); 
    isl=load(f"{SRC}/Island_Sunnyville/Island_Sunnyville.blend")
    w=load(f"{SRC}/Wrecker_v2/Wrecker_v2.blend",(0,14.5,0))
    k=[]
    for i,(m,x,y,r) in enumerate((("Wood",-6,0,0.3),("Brick",-2,-2,0),("Steel",2,-2,0),("Stone",6,0,-0.3),("Glass",0,-5,0))):
        k+=load(f"{SRC}/Kit_{m}/Kit_{m}_Wall.blend",(x,y,0),lod0,rotz=r)
    rig.art_sky(sc)
    _upd(); rig.shoot(f"{OUT}/Hero_scene_gameplay.png",w+k,(0.25,1.0,0.42),lens=28,fill=1.25)
    _upd(); rig.shoot(f"{OUT}/Hero_scene_beauty.png",isl,(0.8,0.9,0.35),lens=40,dof=True,fill=0.8,fstop=5.6)
elif MODE=="check":   # brick mortar vs texture alignment + glass crack visibility
    O2="/workspace/wt/3d-modeler-v3/models/previews/v3_buildings"; os.makedirs(O2,exist_ok=True)
    sc=rig.setup(64,res=(1280,900)); rig.art_sky(sc); rig.ground(5)
    a=load(f"{SRC}/Kit_Brick/Kit_Brick_WallDoor.blend",(0,0,0),lod0)
    _upd(); rig.shoot(f"{O2}/Check_Brick_mortar.png",a,(0.15,-1.0,0.1),lens=50,fill=0.9)
    for o in a: o.hide_render=True
    g=load(f"{SRC}/Kit_Glass/Kit_Glass_Wall_Fractured.blend",(0,0,0)); explode(g,0.12)
    _upd(); rig.shoot(f"{O2}/Check_Glass_fracture.png",g,(0.3,-1.0,0.2),lens=50,fill=1.1)
