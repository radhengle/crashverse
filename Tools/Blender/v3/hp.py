"""CRASHVERSE v3 high-poly -> low-poly bake helpers (Cycles CPU).
Maps: T_<X>_A (sRGB), T_<X>_N (tangent, OpenGL Y+ = Blender native), T_<X>_ORM (R AO min 0.35, G rough, B metal), T_<X>_E."""
import bpy, bmesh, os, math, numpy as np, subprocess
PASSES=("A","R","M","E","N")
def lin(h): return tuple((int(h[i:i+2],16)/255)**2.2 for i in (1,3,5))
def srgb(h): return tuple(int(h[i:i+2],16)/255 for i in (1,3,5))
class HPMat:
    """material with switchable output: A=base colour, R=roughness, M=metallic, E=emission, N=full BSDF w/ bump"""
    def __init__(s,name,color,rough,metal,emis=None,bump=None):
        m=bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree; nt.nodes.clear(); s.m=m; s.nt=nt
        s.out=nt.nodes.new("ShaderNodeOutputMaterial"); s.em=nt.nodes.new("ShaderNodeEmission"); s.bsdf=nt.nodes.new("ShaderNodeBsdfPrincipled")
        s.col=color(nt) if callable(color) else s.rgb(color)
        s.rough=rough(nt) if callable(rough) else s.val(rough)
        s.metal=metal(nt) if callable(metal) else s.val(metal)
        s.emis=(emis(nt) if callable(emis) else s.rgb(emis)) if emis else s.rgb((0,0,0))
        nt.links.new(s.col,s.bsdf.inputs["Base Color"]); nt.links.new(s.rough,s.bsdf.inputs["Roughness"]); nt.links.new(s.metal,s.bsdf.inputs["Metallic"])
        if bump: nt.links.new(bump(nt),s.bsdf.inputs["Normal"])
        ALL.append(s)
    def rgb(s,c):
        n=s.nt.nodes.new("ShaderNodeRGB"); n.outputs[0].default_value=tuple(c)+(1,); return n.outputs[0]
    def val(s,v):
        n=s.nt.nodes.new("ShaderNodeValue"); n.outputs[0].default_value=v; return n.outputs[0]
    def set(s,p):
        nt=s.nt
        for l in list(s.out.inputs[0].links): nt.links.remove(l)
        for l in list(s.em.inputs[0].links): nt.links.remove(l)
        if p=="N": nt.links.new(s.bsdf.outputs[0],s.out.inputs[0]); return
        nt.links.new({"A":s.col,"R":s.rough,"M":s.metal,"E":s.emis}[p],s.em.inputs[0]); nt.links.new(s.em.outputs[0],s.out.inputs[0])
ALL=[]
def set_pass(p):
    for h in ALL: h.set(p)
def noise_bump(nt,scale=40,strength=0.15,dist=0.01,kind="noise",extra=None):
    tc=nt.nodes.new("ShaderNodeTexCoord")
    if kind=="wave":
        t=nt.nodes.new("ShaderNodeTexWave"); t.inputs["Scale"].default_value=scale; t.inputs["Distortion"].default_value=6; t.wave_type='RINGS'
    elif kind=="voronoi":
        t=nt.nodes.new("ShaderNodeTexVoronoi"); t.inputs["Scale"].default_value=scale
    else:
        t=nt.nodes.new("ShaderNodeTexNoise"); t.inputs["Scale"].default_value=scale; t.inputs["Detail"].default_value=8
    nt.links.new(tc.outputs["Object"],t.inputs["Vector"])
    b=nt.nodes.new("ShaderNodeBump"); b.inputs["Strength"].default_value=strength; b.inputs["Distance"].default_value=dist
    nt.links.new(t.outputs[0] if kind!="voronoi" else t.outputs["Distance"],b.inputs["Height"]); return b.outputs[0]
def scene_cycles(samples=32):
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=samples
    sc.cycles.use_denoising=False; sc.render.bake.margin=6
    if not sc.world: sc.world=bpy.data.worlds.new("bakeworld")
    sc.world.use_nodes=True; sc.world.node_tree.nodes["Background"].inputs[0].default_value=(1,1,1,1)
def _img(name,size,cs):
    im=bpy.data.images.new(name,size,size,float_buffer=(cs=="Non-Color")); im.colorspace_settings.name=cs; return im
def bake_maps(lp, hps, size, prefix, outdir, extrusion=0.03, ray=0.06, emissive=False, ao_samples=48, ao_dist=0.5, uv="UV0"):
    """lp: single low-poly object with uv layer `uv`; hps: high-poly objects (HPMat materials)."""
    sc=bpy.context.scene; scene_cycles(); os.makedirs(outdir,exist_ok=True)
    lp.data.uv_layers.active=lp.data.uv_layers[uv]
    tm=bpy.data.materials.new("BAKE_TARGET_"+prefix); tm.use_nodes=True; tn=tm.node_tree.nodes.new("ShaderNodeTexImage"); tm.node_tree.nodes.active=tn
    old=[s.material for s in lp.material_slots]; lp.data.materials.clear(); lp.data.materials.append(tm)
    for p in lp.data.polygons: p.material_index=0
    bpy.ops.object.select_all(action='DESELECT')
    for h in hps: h.select_set(True); h.hide_render=False
    lp.select_set(True); bpy.context.view_layer.objects.active=lp
    bk=sc.render.bake; bk.use_selected_to_active=True; bk.cage_extrusion=extrusion; bk.max_ray_distance=ray; bk.margin=6
    res={}
    def run(name,typ,cs,pss=None,samples=1):
        im=_img(f"T_{prefix}_{name}",size,cs); tn.image=im; sc.cycles.samples=samples
        if pss: set_pass(pss)
        if typ=='NORMAL': bk.normal_space='TANGENT'; bk.normal_r,bk.normal_g,bk.normal_b='POS_X','POS_Y','POS_Z'   # OpenGL Y+
        bpy.ops.object.bake(type=typ,margin=6); res[name]=im
    run("A",'EMIT',"sRGB","A")
    run("R",'EMIT',"Non-Color","R"); run("M",'EMIT',"Non-Color","M")
    if emissive: run("E",'EMIT',"sRGB","E")
    set_pass("N"); run("N",'NORMAL',"Non-Color",None,1)
    sc.world.light_settings.distance=ao_dist; run("AO",'AO',"Non-Color",None,ao_samples)
    def arr(im): return np.array(im.pixels[:],np.float32).reshape(size,size,4)
    ao=arr(res["AO"])[...,0]; orm=np.ones((size,size,4),np.float32)
    orm[...,0]=0.35+0.65*np.clip(ao,0,1); orm[...,1]=arr(res["R"])[...,0]; orm[...,2]=arr(res["M"])[...,0]
    paths={}
    def save(arr_or_img,key,cs):
        p=os.path.join(outdir,f"T_{prefix}_{key}.png")
        if isinstance(arr_or_img,np.ndarray):
            im=_img("tmp_"+key,size,cs); im.pixels.foreach_set(arr_or_img.ravel())
        else: im=arr_or_img
        im.filepath_raw=p; im.file_format='PNG'; im.save(); paths[key]=p
    save(res["A"],"A","sRGB"); save(res["N"],"N","Non-Color"); save(orm,"ORM","Non-Color")
    if emissive: save(res["E"],"E","sRGB")
    lp.data.materials.clear()
    for m in old: lp.data.materials.append(m)
    bk.use_selected_to_active=False
    return paths
def png_stats(p):
    out=subprocess.check_output(["/usr/bin/python3","-c",f"from PIL import Image;import numpy as np;a=np.asarray(Image.open({p!r}).convert('RGB'),float);print(a.reshape(-1,3).mean(0).round(1).tolist(),a.reshape(-1,3).min(0).tolist(),a.reshape(-1,3).max(0).tolist())"])
    return out.decode().strip()
