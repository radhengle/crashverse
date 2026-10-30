"""Shared render setup for the realism test: CC0 HDRI sky + warm sun (#FFF4E0 at ~57 deg), Cycles, AgX view.
Textures are swapped to 1024 copies (TEXCAP) so the renders show what Android gets."""
import bpy,os,math
from mathutils import Vector
import subprocess
HDRI="/workspace/cc0/polyhaven/hdri/kloofendal_48d_partly_cloudy_puresky_4k.exr"
CAP="/tmp/realism_tex1024"
def cap_textures(cap=1024):
    os.makedirs(CAP,exist_ok=True)
    base=cap
    for im in bpy.data.images:
        p=bpy.path.abspath(im.filepath); cap=512 if "/Props/Props_Village/" in p else base   # Android: props 512, tiling 1024
        if not p or not os.path.exists(p) or im.size[0]<=cap: continue
        q=f"{CAP}/{cap}_{abs(hash(p))%10**8}_{os.path.basename(p)}"
        if not os.path.exists(q) or os.path.getmtime(q)<os.path.getmtime(p):
            subprocess.check_call(["/usr/bin/python3","-c",f"from PIL import Image;I=Image.open({p!r});w,h=I.size;I.resize(({cap},max(1,h*{cap}//w)),Image.LANCZOS).save({q!r})"])
        im.filepath=q; im.reload()
def setup(res=(1600,1000),samples=96,view="AgX",look="Medium High Contrast",sun_el=57,sun_az=-38,sun_str=4.2,hdri_str=.9,hdri_rot=0):
    sc=bpy.context.scene; sc.render.engine='CYCLES'; c=sc.cycles; c.device='CPU'; c.samples=samples; c.use_denoising=True
    c.max_bounces=6; c.diffuse_bounces=3; c.glossy_bounces=3; c.transparent_max_bounces=16
    try: c.denoiser='OPENIMAGEDENOISE'
    except Exception: pass
    sc.render.resolution_x,sc.render.resolution_y=res; sc.render.resolution_percentage=100
    sc.view_settings.view_transform=view
    try: sc.view_settings.look=look
    except Exception: sc.view_settings.look='None'
    sc.view_settings.exposure=0; sc.render.film_transparent=False
    w=bpy.data.worlds.new("Sky"); sc.world=w; w.use_nodes=True; N=w.node_tree.nodes; L=w.node_tree.links
    env=N.new("ShaderNodeTexEnvironment"); env.image=bpy.data.images.load(HDRI,check_existing=True)
    mp=N.new("ShaderNodeMapping"); tc=N.new("ShaderNodeTexCoord"); L.new(tc.outputs["Generated"],mp.inputs[0]); L.new(mp.outputs[0],env.inputs[0])
    mp.inputs["Rotation"].default_value[2]=math.radians(hdri_rot)
    bg=N["Background"]; bg.inputs[1].default_value=hdri_str; L.new(env.outputs[0],bg.inputs[0])
    sd=bpy.data.lights.new("Sun",'SUN'); sd.energy=sun_str; sd.color=(1.0,0.955,0.878); sd.angle=math.radians(1.2)
    so=bpy.data.objects.new("Sun",sd); sc.collection.objects.link(so)
    so.rotation_euler=(math.radians(90-sun_el),0,math.radians(sun_az))
    return so
def camera(loc,target,lens=50,name="Cam"):
    cd=bpy.data.cameras.new(name); cd.lens=lens; cd.clip_end=500; co=bpy.data.objects.new(name,cd); bpy.context.scene.collection.objects.link(co)
    co.location=Vector(loc); d=Vector(target)-Vector(loc); co.rotation_euler=d.to_track_quat('-Z','Y').to_euler()
    bpy.context.scene.camera=co; return co
def render(path):
    os.makedirs(os.path.dirname(path),exist_ok=True); bpy.context.scene.render.filepath=path; bpy.ops.render.render(write_still=True)
