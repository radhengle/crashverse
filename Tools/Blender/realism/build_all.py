"""Realism test: build every asset (LODs), bake vertex AO, UV2, billboards, colliders, export FBX to
Assets/_Project/Models/Realistic/..., and save the review scene /tmp/realism_scene.blend.
blender -b -P build_all.py"""
import bpy,bmesh,math,os,sys,json,random,subprocess
from mathutils import Vector,Matrix
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import rlib as R, townhouse as TH, trees as TR, props as PR, ground as GR, rscene as S
REAL=R.REAL; STATS={}
def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def strip_attrs(o):
    for a in [a.name for a in o.data.attributes if a.name in ("cell",)]: o.data.attributes.remove(o.data.attributes[a])
def export(objs,root_name,path,props=None):
    root=bpy.data.objects.new(root_name,None); bpy.context.scene.collection.objects.link(root)
    for k,v in (props or {}).items(): root[k]=v
    for o in objs: o.parent=root
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for o in objs: o.select_set(True)
    os.makedirs(os.path.dirname(path),exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,use_custom_props=True,path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='OFF',
        use_tspace=True,colors_type='LINEAR',add_leaf_bones=False,object_types={'EMPTY','MESH'})
    return root
def info(name,objs,mats,tex):
    STATS[name]={"tris":{o.name:R.tri_count(o) for o in objs},"materials":sorted({m for o in objs for m in [s.material.name for s in o.material_slots if s.material]}),
                 "submeshes_per_lod":{o.name:len(o.material_slots) for o in objs},"textures":tex}
def tex_info(prefix):
    out={}
    for k in ("A","N","ORM"):
        p=f"{prefix}_{k}.png"
        if os.path.exists(p):
            im=bpy.data.images.load(p,check_existing=True); out[os.path.basename(p)]=f"{im.size[0]}x{im.size[1]} authored -> 1024 Android" ; 
    return out
def hull(name,pts):
    bm=bmesh.new()
    for p in pts: bm.verts.new(Vector(p))
    bmesh.ops.convex_hull(bm,input=bm.verts[:]); me=bpy.data.meshes.new(name); bm.to_mesh(me)
    o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o); return o
def box_pts(x0,x1,y0,y1,z0,z1): return [(x,y,z) for x in (x0,x1) for y in (y0,y1) for z in (z0,z1)]
# ------------------------------------------------------------------ townhouse
def build_townhouse():
    clean(); P=TH.TEX+"/Textures/T_Bld_Townhouse"; m=R.lit_material("M_Bld_Townhouse",P)
    objs=[TH.build(l,m) for l in (0,1,2)]
    gp=bpy.data.objects.new("bakeground",bpy.data.meshes.new("g")); bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=1,y_segments=1,size=30); bm.to_mesh(gp.data); bpy.context.scene.collection.objects.link(gp)
    for o in objs: R.vertex_ao(o,[o,gp],rays=48,dist=.8,strength=.5)
    bpy.data.objects.remove(gp)
    for o in objs: R.add_uv2(o); strip_attrs(o)
    XF,YF,YB,Z0,Z1,Z2,J=TH.XF,TH.YF,TH.YB,TH.Z0,TH.Z1,TH.Z2,TH.JET; yf=YF-J; yc=(YB+yf)/2; zr=Z2+(YB-yf)/2*math.tan(TH.PITCH)
    hs=[hull("UCX_Bld_Townhouse_LOD0_00",box_pts(-XF-.3,XF+.3,YF-J-.26,YB+.26,0,Z0)),
        hull("UCX_Bld_Townhouse_LOD0_01",box_pts(-XF-.05,XF+.05,YF-.05,YB+.05,Z0,Z1)),
        hull("UCX_Bld_Townhouse_LOD0_02",box_pts(-XF-.1,XF+.1,yf-.1,YB+.1,Z1,Z2)),
        hull("UCX_Bld_Townhouse_LOD0_03",[(x,y,z) for x in (-XF-.45,XF+.45) for (y,z) in ((yf-.5,Z2-.4),(YB+.5,Z2-.4),(yc,zr+.2))]),
        hull("UCX_Bld_Townhouse_LOD0_04",box_pts(XF-1.45,XF-.55,yc+.4,yc+1.1,Z2,zr+1.3)),
        hull("UCX_Bld_Townhouse_LOD0_05",box_pts(-XF-2.2,-XF-.25,-1.8,1.6,0,3.0))]
    out=REAL+"/Buildings/Hero/Bld_Townhouse/Bld_Townhouse.fbx"
    export(objs+hs,"Bld_Townhouse",out,{"fracture_class":"Stone","asset_class":"HeroBuilding"})
    info("Bld_Townhouse",objs,1,tex_info(P)); STATS["Bld_Townhouse"]["colliders"]=len(hs)
    bpy.ops.wm.save_as_mainfile(filepath="/tmp/rt_townhouse.blend")
# ------------------------------------------------------------------ trees + billboards
def render_billboard(sp,o,size=1024):
    """orthographic front (-Y) and side (+X) views -> T_<sp>_Billboard_A (albedo*AO, alpha) and _N (tangent-space)"""
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.cycles.use_denoising=False; sc.render.film_transparent=True
    sc.cycles.pixel_filter_type='BOX'
    sc.world=bpy.data.worlds.new("bbW"); sc.world.use_nodes=True; sc.world.node_tree.nodes["Background"].inputs[1].default_value=0
    mn=Vector([min((o.matrix_world@Vector(c))[i] for c in o.bound_box) for i in range(3)]); mx=Vector([max((o.matrix_world@Vector(c))[i] for c in o.bound_box) for i in range(3)])
    W=max(mx.x-mn.x,mx.y-mn.y)*1.02; H=(mx.z-mn.z)*1.02; ortho=max(W,H/2*1)*1.0; ortho=max(W*1,H/2); ortho=max(W,H)*1.0
    # camera ortho_scale covers the larger image side (height=2*width here)
    tall=H>1.4*W; th=size if tall else size//2
    sc.render.resolution_x=size//2; sc.render.resolution_y=th
    ortho=max(H,2*W) if tall else max(H,W)
    src=o.material_slots[0].material; outs={}
    for mode in ("A","N"):
        m=src.copy(); nt=m.node_tree; N=nt.nodes; L=nt.links; b=N["Principled BSDF"]; out=N["Material Output"]
        em=N.new("ShaderNodeEmission"); mix=N.new("ShaderNodeMixShader"); tr=N.new("ShaderNodeBsdfTransparent")
        alpha_src=[l.from_node for l in nt.links if l.to_socket==b.inputs["Alpha"]][0]
        L.new(alpha_src.outputs[0],mix.inputs[0]); L.new(tr.outputs[0],mix.inputs[1]); L.new(em.outputs[0],mix.inputs[2]); L.new(mix.outputs[0],out.inputs["Surface"])
        if mode=="A":
            base=[l.from_socket for l in nt.links if l.to_socket==b.inputs["Base Color"]][0]; L.new(base,em.inputs[0])
        else:
            geo=N.new("ShaderNodeNewGeometry"); nm=[l.from_socket for l in nt.links if l.to_socket==b.inputs["Normal"]][0]
            sep=N.new("ShaderNodeSeparateXYZ"); L.new(nm,sep.inputs[0]); comb=N.new("ShaderNodeCombineXYZ"); m["sep"]=1
            outs["sepnode"]=(sep,comb)
            L.new(comb.outputs[0],em.inputs[0])
        o.material_slots[0].material=m
        for view,(cl,tg,mapn) in enumerate((((0,-60,0),(0,0,0),("x","z","-y")),((60,0,0),(0,0,0),("y","z","x")))):
            if mode=="N":
                sep,comb=outs["sepnode"]
                for l in list(nt.links):
                    if l.to_node==comb: nt.links.remove(l)
                for i,ax in enumerate(mapn):
                    neg=ax.startswith("-"); k="xyz".index(ax[-1])
                    mm=N.new("ShaderNodeMath"); mm.operation='MULTIPLY_ADD'; mm.inputs[1].default_value=-.5 if neg else .5; mm.inputs[2].default_value=.5
                    L.new(sep.outputs[k],mm.inputs[0]); L.new(mm.outputs[0],comb.inputs[i])
            sc.view_settings.view_transform='Standard' if mode=="A" else 'Raw'; sc.view_settings.look='None'
            cd=bpy.data.cameras.new("bb"); cd.type='ORTHO'; cd.ortho_scale=ortho; co=bpy.data.objects.new("bb",cd); sc.collection.objects.link(co)
            c=Vector(((mn.x+mx.x)/2,(mn.y+mx.y)/2,(mn.z+mx.z)/2)); d=Vector(cl).normalized(); co.location=c+d*60
            co.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler(); sc.camera=co; cd.clip_end=200
            S.render(f"/tmp/bb_{sp}_{mode}{view}.png"); bpy.data.objects.remove(co)
        o.material_slots[0].material=src
    tex=f"{REAL}/Foliage/{sp}/Textures/T_{sp}_Billboard"
    code=f"""
th={th}
from PIL import Image, ImageFilter
import numpy as np
for k in ('A','N'):
    a=Image.open('/tmp/bb_{sp}_'+k+'0.png').convert('RGBA'); b=Image.open('/tmp/bb_{sp}_'+k+'1.png').convert('RGBA')
    im=Image.new('RGBA',({size},th)); im.paste(a,(0,0)); im.paste(b,({size}//2,0))
    if k=='A':
        arr=np.asarray(im).astype(np.float32); m=arr[...,3]>127
        bl=np.asarray(im.convert('RGB').filter(ImageFilter.GaussianBlur(6))).astype(np.float32)
        arr[...,:3]=np.where(m[...,None],arr[...,:3],bl); arr[...,3]=np.where(m,255,0); im=Image.fromarray(arr.astype(np.uint8),'RGBA')
        im.save('{tex}_A.png')
    else:
        arr=np.asarray(im).astype(np.float32); m=arr[...,3]>10; arr[...,:3]=np.where(m[...,None],arr[...,:3],[128,128,255]); Image.fromarray(arr[...,:3].astype(np.uint8)).save('{tex}_N.png')
o=np.zeros((th,{size},3),np.uint8); o[...]=(230,200,0); Image.fromarray(o).save('{tex}_ORM.png')
"""
    subprocess.check_call(["/usr/bin/python3","-c",code])
    sc.render.film_transparent=False
    return mn,mx,tex,ortho,tall
def billboard_mesh(sp,mn,mx,tex,ortho,tall):
    m=R.lit_material(f"M_{sp}_Billboard",tex,alpha=True,vcol=False)
    bm=bmesh.new(); uv=bm.loops.layers.uv.new("UV0"); uv2=bm.loops.layers.uv.new("UV2"); col=bm.loops.layers.float_color.new("Col")
    cx,cy=(mn.x+mx.x)/2,(mn.y+mx.y)/2; H=ortho; W=ortho/2 if tall else ortho; z0=mn.z+(mx.z-mn.z)/2-H/2
    for (u0,a,b) in ((0,Vector((1,0,0)),Vector((0,-1,0))),(.5,Vector((0,1,0)),Vector((1,0,0)))):
        c=Vector((cx,cy,0)); vs=[bm.verts.new(c+a*W*(x-.5)+Vector((0,0,z0+H*y))) for x,y in ((0,0),(1,0),(1,1),(0,1))]
        f=bm.faces.new(vs)
        if f.normal.dot(b)<0: f.normal_flip()
        for L in f.loops:
            q=L.vert.co; x=(q-c).dot(a)/W+.5; y=(q.z-z0)/H; L[uv].uv=(u0+.5*x,y); L[uv2].uv=(y,0); L[col]=(1,1,1,1)
    me=bpy.data.meshes.new(f"{sp}_LOD3"); bm.to_mesh(me); o=bpy.data.objects.new(me.name,me); bpy.context.scene.collection.objects.link(o); me.materials.append(m)
    return o
def build_tree(sp):
    clean(); P=f"{REAL}/Foliage/{sp}/Textures/T_{sp}"; m=R.lit_material(f"M_{sp}",P,alpha=True)
    objs=[TR.SPECIES[sp](l,m) for l in (0,1,2)]
    for o in objs[1:]: o.hide_render=True
    mn,mx,tex,ortho,tall=render_billboard(sp,objs[0])
    for o in objs: o.hide_render=False
    b=billboard_mesh(sp,mn,mx,tex,ortho,tall); objs.append(b)
    export(objs,sp,f"{REAL}/Foliage/{sp}/{sp}.fbx",{"asset_class":"Tree" if sp!="Bush" else "Bush","wind_uv2":"x=sway weight,y=phase"})
    info(sp,objs,1,{**tex_info(P),**tex_info(tex)})
    bpy.ops.wm.save_as_mainfile(filepath=f"/tmp/rt_{sp}.blend")
def build_props2():
    """bakery street props only (does not touch the existing Prop_* exports)"""
    clean(); P=PR.TEX+"/Textures/T_Props_Village"; m=R.lit_material("M_Props_Village",P)
    for k,f in PR.PROPS2.items():
        objs=[f(l).to_object(f"Prop_{k}_LOD{l}",m) for l in (0,1)]
        for o in objs: R.add_uv2(o); strip_attrs(o)
        export(objs,f"Prop_{k}",f"{REAL}/Props/Props_Village/Prop_{k}.fbx",{"asset_class":"Prop"})
        info(f"Prop_{k}",objs,1,tex_info(P))
        for o in objs: o.parent=None
def build_props():
    clean(); P=PR.TEX+"/Textures/T_Props_Village"; m=R.lit_material("M_Props_Village",P)
    for k,f in PR.PROPS.items():
        objs=[f(l).to_object(f"Prop_{k}_LOD{l}",m) for l in (0,1)]
        for o in objs: R.add_uv2(o); strip_attrs(o)
        export(objs,f"Prop_{k}",f"{REAL}/Props/Props_Village/Prop_{k}.fbx",{"asset_class":"Prop"})
        info(f"Prop_{k}",objs,1,tex_info(P))
        for o in objs: o.parent=None
    bpy.ops.wm.save_as_mainfile(filepath="/tmp/rt_props.blend")
def build_ground():
    clean(); P=GR.TEX+"/Textures/T_Ground_Patch"; m=R.lit_material("M_Ground_Patch",P)
    g=GR.ground(0).to_object("Ground_Patch_LOD0",m); R.add_uv2(g); strip_attrs(g)
    export([g],"Ground_Patch",f"{REAL}/Props/Ground_Patch/Ground_Patch.fbx",{"asset_class":"Terrain"}); info("Ground_Patch",[g],1,tex_info(P))
    gm=R.lit_material("M_GrassClump",GR.GTEX+"/Textures/T_GrassClump",alpha=True)
    cl=[GR.grass_clump(l) for l in (0,1)]
    for o in cl: o.data.materials.append(gm)
    export(cl,"GrassClump",f"{REAL}/Foliage/GrassClump/GrassClump.fbx",{"asset_class":"Detail","wind_uv2":"x=sway weight"}); info("GrassClump",cl,1,tex_info(GR.GTEX+"/Textures/T_GrassClump"))
if __name__=="__main__":
    W=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else ["town","Oak","Pine","Bush","props","ground"]
    if "town" in W: build_townhouse()
    for sp in ("Oak","Pine","Bush"):
        if sp in W: build_tree(sp)
    if "props" in W: build_props()
    if "props2" in W: build_props2()
    if "ground" in W: build_ground()
    p=REAL+"/realism_stats.json"; old=json.load(open(p)) if os.path.exists(p) else {}
    old.update(STATS); json.dump(old,open(p,"w"),indent=1); print("STATS",json.dumps(STATS)[:3000])
