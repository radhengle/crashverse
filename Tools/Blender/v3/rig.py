"""Preview rig (VFX spec 4b/§3 + art/m1b arena brief): sun #FFF2D6 from upper back-left w/ soft shadows, gradient ambient
sky #8CC8FF / equator #DCEFFF / ground #6FAE5A, raytraced AO + reflections (stands in for one probe), Filmic exposure 0
(Unity uses ACES), bloom thr 1.0 int ~0.4, light DoF on beauty only. Captions: 'Blender Filmic preview, final look sign-off in Unity ACES'."""
import bpy, math, os, subprocess
from mathutils import Vector, Matrix
def lin(h): return tuple((int(h[i:i+2],16)/255)**2.2 for i in (1,3,5))+(1,)
def setup(samples=128,res=(1280,720),probe=True,ambient=0.9):
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=samples; sc.cycles.use_denoising=True
    sc.cycles.denoiser='OPENIMAGEDENOISE'; sc.cycles.max_bounces=6
    sc.view_settings.view_transform='Filmic'; sc.view_settings.look='Medium High Contrast'; sc.view_settings.exposure=0
    sc.render.resolution_x,sc.render.resolution_y=res
    w=bpy.data.worlds.new("ArenaAmbient"); sc.world=w; w.use_nodes=True; nt=w.node_tree; bg=nt.nodes["Background"]
    tc=nt.nodes.new("ShaderNodeTexCoord"); sp=nt.nodes.new("ShaderNodeSeparateXYZ"); mr=nt.nodes.new("ShaderNodeMapRange"); cr=nt.nodes.new("ShaderNodeValToRGB")
    nt.links.new(tc.outputs["Generated"],sp.inputs[0]); nt.links.new(sp.outputs[2],mr.inputs[0]); mr.inputs[1].default_value=-1; mr.inputs[2].default_value=1
    nt.links.new(mr.outputs[0],cr.inputs[0]); e=cr.color_ramp.elements; e[0].color=lin("#6FAE5A"); e[1].color=lin("#8CC8FF"); el=e.new(0.52); el.color=lin("#DCEFFF")
    nt.links.new(cr.outputs[0],bg.inputs[0]); bg.inputs[1].default_value=ambient
    if not probe: w.cycles_visibility.glossy=False   # no-probe: no environment reflections
    sun=bpy.data.objects.new("Sun",bpy.data.lights.new("Sun",'SUN')); sc.collection.objects.link(sun)
    sun.data.energy=4.0; sun.data.color=lin("#FFF2D6")[:3]; sun.data.angle=math.radians(4)
    sun.rotation_euler=(math.radians(42),0,math.radians(-140))
    sc.use_nodes=True; ct=sc.node_tree; rl=ct.nodes["Render Layers"]; comp=ct.nodes["Composite"]
    g=ct.nodes.new("CompositorNodeGlare")
    try: g.glare_type='BLOOM'
    except Exception: g.glare_type='FOG_GLOW'
    g.threshold=1.0; g.mix=-0.6; g.size=7; ct.links.new(rl.outputs[0],g.inputs[0]); ct.links.new(g.outputs[0],comp.inputs[0])
    return sc
def ground(radius=6,z=0.0,color="#7ED957"):
    bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=radius,depth=0.4,location=(0,0,z-0.2))
    g=bpy.context.object; g.name="PreviewGround"; m=bpy.data.materials.new("PreviewGrass"); m.use_nodes=True
    b=m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value=lin(color); b.inputs["Roughness"].default_value=0.8
    g.data.materials.append(m); bpy.ops.object.shade_smooth(); return g
def bounds(objs):
    pts=[o.matrix_world@Vector(c) for o in objs for c in o.bound_box]
    mn=Vector([min(p[i] for p in pts) for i in range(3)]); mx=Vector([max(p[i] for p in pts) for i in range(3)]); return (mn+mx)/2,(mx-mn).length/2
def shoot(path,objs,dirv,lens=50,dof=False,fill=1.0,fstop=2.8,target=None):
    sc=bpy.context.scene; c,r=bounds(objs)
    if target is not None: c=Vector(target)
    cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); cam.data.lens=lens
    d=Vector(dirv).normalized(); dist=r*fill*lens/22.0; cam.location=c+d*dist
    f=(c-cam.location).normalized(); rg=f.cross(Vector((0,0,1))).normalized(); up=rg.cross(f)
    cam.rotation_euler=Matrix((rg,up,-f)).transposed().to_euler(); sc.camera=cam; cam.data.clip_end=2000
    cam.data.dof.use_dof=dof
    if dof: cam.data.dof.focus_distance=dist; cam.data.dof.aperture_fstop=fstop
    sc.render.filepath=path; bpy.ops.render.render(write_still=True); bpy.data.objects.remove(cam); caption(path); print("PREVIEW",path,flush=True)
def caption(path,text="Blender Filmic preview, final look sign-off in Unity ACES"):
    subprocess.check_call(["/usr/bin/python3","-c",f"""
from PIL import Image,ImageDraw,ImageFont
im=Image.open({path!r}).convert('RGB'); d=ImageDraw.Draw(im); w,h=im.size
try: f=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',max(12,h//45))
except Exception: f=ImageFont.load_default()
t={text!r}; b=d.textbbox((0,0),t,font=f); pad=8
d.rectangle([w-b[2]-3*pad,h-b[3]-3*pad,w-pad,h-pad],fill=(16,24,40)); d.text((w-b[2]-2*pad,h-b[3]-2*pad),t,font=f,fill=(232,176,75)); im.save({path!r})"""])
def strip(paths,out,cols=4,cell=360,title=""):
    subprocess.check_call(["/usr/bin/python3","-c",f"""
from PIL import Image,ImageDraw
P={paths!r}; c={cols}; s={cell}; r=(len(P)+c-1)//c
S=Image.new('RGB',(c*s,r*s+28),(16,24,40)); d=ImageDraw.Draw(S); d.text((8,8),{title!r},fill=(232,176,75))
for i,p in enumerate(P): S.paste(Image.open(p).convert('RGB').resize((s,s)),((i%c)*s,28+(i//c)*s))
S.save({out!r})"""])
