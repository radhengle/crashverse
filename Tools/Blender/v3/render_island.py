import sys,os,bpy,math,random; sys.path.insert(0,os.path.dirname(__file__)); import rig
from mathutils import Vector
OUT="/workspace/wt/3d-modeler-v3/models/previews/v3"; os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="/workspace/crashverse/models_src/Island_Sunnyville/Island_Sunnyville.blend")
for o in bpy.data.objects:
    if o.name.startswith("UCX_"): o.hide_render=True
for l in list(bpy.data.lights): bpy.data.lights.remove(l)
objs=[o for o in bpy.data.objects if o.type=='MESH' and not o.hide_render]
sc=rig.setup(128)
# art sky gradient #2F8CFF -> #BDEBFF
cr=[n for n in sc.world.node_tree.nodes if n.type=='VALTORGB'][0]; e=cr.color_ramp.elements
e[0].color=rig.lin("#9CC7E8"); e[0].position=0.0; e[-1].color=rig.lin("#2F8CFF")
for el in e[1:-1]: el.color=rig.lin("#BDEBFF"); el.position=0.5
wn=sc.world.node_tree; bg=wn.nodes["Background"]; lp=wn.nodes.new("ShaderNodeLightPath")
sky=wn.nodes.new("ShaderNodeBackground"); mx=wn.nodes.new("ShaderNodeMixShader"); out=wn.nodes["World Output"]
cr2=wn.nodes.new("ShaderNodeValToRGB"); tcw=wn.nodes.new("ShaderNodeTexCoord"); spw=wn.nodes.new("ShaderNodeSeparateXYZ"); wn.links.new(tcw.outputs["Window"],spw.inputs[0]); wn.links.new(spw.outputs[1],cr2.inputs[0]); e2=cr2.color_ramp.elements
e2[0].position=0.25; e2[0].color=rig.lin("#BDEBFF"); e2[1].position=1.0; e2[1].color=rig.lin("#2F8CFF")
wn.links.new(cr2.outputs[0],sky.inputs[0]); sky.inputs[1].default_value=0.8
wn.links.new(lp.outputs["Is Camera Ray"],mx.inputs[0]); wn.links.new(bg.outputs[0],mx.inputs[1]); wn.links.new(sky.outputs[0],mx.inputs[2]); wn.links.new(mx.outputs[0],out.inputs[0])
sun=bpy.data.objects["Sun"]; sun.data.energy=6.5; sun.data.color=rig.lin("#FFE3B8")[:3]; sun.data.angle=math.radians(3)
# preview-only: AO multiplied into base colour (Unity shader does this from ORM.R)
m=bpy.data.materials.get("M_Island")
if m:
    nt=m.node_tree; b=nt.nodes["Principled BSDF"]
    tex=[n for n in nt.nodes if n.type=='TEX_IMAGE']; alb=[t for t in tex if "_A" in t.image.name][0]; orm=[t for t in tex if "ORM" in t.image.name]
    if orm:
        sp=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(orm[0].outputs[0],sp.inputs[0])
        mu=nt.nodes.new("ShaderNodeMix"); mu.data_type='RGBA'; mu.blend_type='MULTIPLY'; mu.inputs[0].default_value=1
        nt.links.new(alb.outputs[0],mu.inputs[6]); nt.links.new(sp.outputs[0],mu.inputs[7]); nt.links.new(mu.outputs[2],b.inputs["Base Color"])
# preview-only clouds
cm=bpy.data.materials.new("PreviewCloud"); cm.use_nodes=True; pb=cm.node_tree.nodes["Principled BSDF"]; pb.inputs["Base Color"].default_value=(1,1,1,1); pb.inputs["Roughness"].default_value=1
pb.inputs["Emission Color"].default_value=(1,1,1,1); pb.inputs["Emission Strength"].default_value=0.35
R=random.Random(7)
for k in range(9):
    c=Vector((R.uniform(-90,90),R.uniform(-90,-40) if k%2 else R.uniform(40,90),R.uniform(4,22)))
    for j in range(5):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=R.uniform(4,8),location=c+Vector((j*5-10,R.uniform(-2,2),R.uniform(-1,2))))
        o=bpy.context.object; o.scale.z=0.55; o.data.materials.append(cm); bpy.ops.object.shade_smooth()
sc.render.film_transparent=True
import subprocess
SKY="""import sys;from PIL import Image
p=sys.argv[1];im=Image.open(p).convert('RGBA');w,h=im.size;bg=Image.new('RGBA',(w,h))
a=(0x2F,0x8C,0xFF);b=(0xBD,0xEB,0xFF)
for y in range(h):
  t=min(1,y/(h*0.8));bg.paste(tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))+(255,),(0,y,w,y+1))
Image.alpha_composite(bg,im).convert('RGB').save(p)"""
_shoot=rig.shoot; _cap=rig.caption
def shoot(p,*a,**k):
    rig.caption=lambda *x,**y:None; _shoot(p,*a,**k); rig.caption=_cap
    subprocess.check_call(["/usr/bin/python3","-c",SKY,p]); _cap(p)
rig.shoot=shoot
rig.shoot(f"{OUT}/Island_Sunnyville_gameplay.png",objs,(0.0,1.0,0.5),lens=32,fill=0.95)
rig.shoot(f"{OUT}/Island_Sunnyville_beauty.png",objs,(0.9,-0.6,0.22),lens=50,dof=True,fill=1.0,fstop=4.0)
