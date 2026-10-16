"""Bake palette-coloured low-poly meshes to unique-UV PBR sets (CrashverseGlossyPBR):
T_<P>_Albedo, T_<P>_Normal (flat, OpenGL Y+), T_<P>_ORM (R=AO G=rough B=metal), optional T_<P>_Emissive."""
SUF={"Albedo":"A","Normal":"N","ORM":"ORM","Emissive":"E"}
import bpy, bmesh, math, os, numpy as np
from cv_lib import PAL, CELL, NAMES, GRID, CPX, hexrgb, material_pbr
ROUGH={"steel":0.3,"steel_dk":0.35,"gold":0.3,"gold_dk":0.35,"navy":0.32,"navy_lt":0.35,"cyan":0.15,"cyan_lt":0.15,"violet":0.25,
       "violet_dk":0.3,"orange":0.3,"wood":0.55,"wood_dk":0.6,"trunk":0.7,"outline":0.5,"water":0.1,"white":0.6}
METAL={"steel":1.0,"steel_dk":1.0,"gold":1.0,"gold_dk":1.0}
EMIS={"cyan":"#22D3EE","cyan_lt":"#BFF6FF","violet":"#8B5CF6","orange":"#FF7A1A"}
def cell_of_uv(u,v):
    i=int(u*256//CPX)+GRID*int((1-v)*256//CPX); return NAMES[i] if 0<=i<len(NAMES) else None
def layout_image(name,fn,cs="sRGB"):
    im=bpy.data.images.new(name,256,256,float_buffer=False); im.colorspace_settings.name=cs; a=np.zeros((256,256,4),np.float32)
    for i,n in enumerate(NAMES):
        x,y=(i%GRID)*CPX,(i//GRID)*CPX; a[256-y-CPX:256-y, x:x+CPX]=list(fn(n))+[1]
    im.pixels.foreach_set(a.ravel()); return im
def face_cells(ob):
    me=ob.data; uv=me.uv_layers[0].data
    return [cell_of_uv(*uv[p.loop_start].uv) for p in me.polygons]
def unwrap(obs, margin=0.003):
    for ob in obs:
        me=ob.data; me.uv_layers[0].name="Palette"
        if "UV0" not in me.uv_layers: me.uv_layers.new(name="UV0")
        me.uv_layers.active=me.uv_layers["UV0"]
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs: ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60),island_margin=margin,scale_to_bounds=False)
    bpy.ops.uv.select_all(action='SELECT'); bpy.ops.uv.average_islands_scale(); bpy.ops.uv.pack_islands(margin=margin,rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
def texel_density(obs,size):
    A3=0; AU=0
    for ob in obs:
        bm=bmesh.new(); bm.from_mesh(ob.data); bm.transform(ob.matrix_world); uv=bm.loops.layers.uv["UV0"]
        for f in bm.faces:
            A3+=f.calc_area(); pts=[l[uv].uv for l in f.loops]
            AU+=abs(sum(pts[i].x*pts[i-1].y-pts[i-1].x*pts[i].y for i in range(len(pts)))/2)
        bm.free()
    return math.sqrt(AU*size*size/A3), A3
def bake(obs, size, prefix, texdir, emissive=False, ao_samples=48):
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=ao_samples
    sc.render.bake.margin=8
    unwrap(obs)
    srcs={"Albedo":layout_image("L_alb",lambda n:list(hexrgb(PAL[CELL[n]]))),  # linear for emit bake into sRGB target
          "ORM":layout_image("L_orm",lambda n:[1,ROUGH.get(n,0.5),METAL.get(n,0.0)],"Non-Color")}
    if emissive: srcs["Emissive"]=layout_image("L_emi",lambda n:list(hexrgb(EMIS[n])) if n in EMIS else [0,0,0])
    targets={}
    for k in list(srcs)+["AO"]:
        im=bpy.data.images.new(f"T_{prefix}_{k}",size,size); im.colorspace_settings.name="sRGB" if k in("Albedo","Emissive") else "Non-Color"; targets[k]=im
    bm_=bpy.data.materials.new("BAKE"); bm_.use_nodes=True; nt=bm_.node_tree; nt.nodes.clear()
    out=nt.nodes.new("ShaderNodeOutputMaterial"); em=nt.nodes.new("ShaderNodeEmission"); nt.links.new(em.outputs[0],out.inputs[0])
    uvn=nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map="Palette"; src=nt.nodes.new("ShaderNodeTexImage"); src.interpolation='Closest'
    nt.links.new(uvn.outputs[0],src.inputs[0]); nt.links.new(src.outputs[0],em.inputs[0])
    tgt=nt.nodes.new("ShaderNodeTexImage"); uv0=nt.nodes.new("ShaderNodeUVMap"); uv0.uv_map="UV0"; nt.links.new(uv0.outputs[0],tgt.inputs[0])
    nt.nodes.active=tgt
    saved={ob.name:[s.material for s in ob.material_slots] for ob in obs}
    for ob in obs:
        ob.data.materials.clear(); ob.data.materials.append(bm_)
        ob.data.uv_layers.active=ob.data.uv_layers["UV0"]
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs: ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    for k,s in srcs.items():
        src.image=s; tgt.image=targets[k]; sc.cycles.samples=1
        tgt.image.colorspace_settings.name="Non-Color" if k=="ORM" else "sRGB"
        bpy.ops.object.bake(type='EMIT',margin=8)
    # AO: real geometry AO (bevel cavities, contact between parts)
    nt.links.new(src.outputs[0],em.inputs[0]); tgt.image=targets["AO"]; sc.cycles.samples=ao_samples
    sc.world=sc.world or bpy.data.worlds.new("w")
    bpy.ops.object.bake(type='AO',margin=8)
    orm=np.array(targets["ORM"].pixels[:]).reshape(size,size,4); ao=np.array(targets["AO"].pixels[:]).reshape(size,size,4)
    orm[...,0]=np.clip(0.35+0.65*ao[...,0],0,1); targets["ORM"].pixels.foreach_set(orm.astype(np.float32).ravel())
    paths={}
    for k in [k for k in targets if k!="AO"]:
        p=os.path.join(texdir,f"T_{prefix}_{SUF[k]}.png"); targets[k].filepath_raw=p; targets[k].file_format='PNG'; targets[k].save(); targets[k].source='FILE'; targets[k].filepath=p; targets[k].reload(); paths[k]=p
    p=os.path.join(texdir,f"T_{prefix}_N.png")
    import subprocess; subprocess.check_call(["/usr/bin/python3","-c",f"from PIL import Image; Image.new('RGB',(64,64),(128,128,255)).save({p!r})"])
    paths["Normal"]=p   # flat tangent-space normal (OpenGL Y+); low-poly bevels carry the shape
    for ob in obs:
        ob.data.uv_layers.remove(ob.data.uv_layers["Palette"]); ob.data.materials.clear()
    td,area=texel_density(obs,size)
    return paths, td, area
