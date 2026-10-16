import random, os, math, subprocess, sys
try:
    import bpy, bmesh
    from mathutils import Vector
except ImportError: pass
HERE=os.path.dirname(os.path.abspath(__file__))
NAMES=["wood","wood_dk","brick","mortar","steel","steel_dk","navy","gold",
       "cyan","violet","orange","outline","grass","cliff","sand","roof",
       "cliff_dk","cliff_lt","leaf","leaf_dk","stone","water","cyan_lt","gold_dk",
       "wood_in","brick_in","steel_in","grass_dk","navy_lt","violet_dk","white","trunk"]
PAL=["#E3A766","#A8703A","#D2664C","#7A3326","#AEB6C2","#5B6472","#3E4C6E","#E8B04B",
     "#22D3EE","#8B5CF6","#FF7A1A","#101828","#7ED957","#C98A4B","#F2D49B","#4D7BC4",
     "#8E5A32","#DDA36A","#3FA34D","#2E7D3A","#A9B0B8","#8CD8FF","#BFF6FF","#B98424",
     "#C68A52","#B0533D","#949CA8","#5DBB45","#5A6E9A","#6A3FC4","#F4F7FB","#7A4A26"]
CELL={n:i for i,n in enumerate(NAMES)}
SRC="/workspace/crashverse/models_src"   # .blend sources live outside git (PM rule)
GRID=8; CPX=256//GRID
DENS = {"Wood":600,"Brick":1900,"Steel":800}  # steel = hollow effective density (Physics sign-off)
def hexrgb(h): return tuple(int(h[i:i+2],16)/255 for i in (1,3,5))
def write_palette(path):
    try: import PIL
    except ImportError: return subprocess.check_call(['/usr/bin/python3','-c',f'import sys;sys.path.insert(0,{HERE!r});import cv_lib;cv_lib.write_palette({path!r})'])
    from PIL import Image, ImageDraw
    im=Image.new("RGB",(256,256)); d=ImageDraw.Draw(im)
    for i,h in enumerate(PAL):
        x,y=(i%GRID)*CPX,(i//GRID)*CPX; c=hexrgb(h)
        for r in range(CPX):  # soft vertical gradient = glossy toy feel
            k=1.08-0.16*r/(CPX-1); d.line([x,y+r,x+CPX-1,y+r],fill=tuple(min(255,int(v*255*k)) for v in c))
    im.save(path)
def write_inner(path, base, dark):
    try: import PIL
    except ImportError: return subprocess.check_call(['/usr/bin/python3','-c',f'import sys;sys.path.insert(0,{HERE!r});import cv_lib;cv_lib.write_inner({path!r},{base!r},{dark!r})'])
    from PIL import Image; import random as R
    R.seed(7); a,b=hexrgb(base),hexrgb(dark); im=Image.new("RGB",(256,256)); px=im.load()
    for y in range(256):
        for x in range(256):
            t=0.15+R.random()*0.2; px[x,y]=tuple(int(255*(a[i]*(1-t)+b[i]*t)) for i in range(3))
    im.save(path)
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def material(name, img, rough=0.35, metal=0.0):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True
    nt=m.node_tree; b=nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value=rough; b.inputs["Metallic"].default_value=metal
    t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(img, check_existing=True); t.interpolation="Closest"
    nt.links.new(t.outputs[0], b.inputs[0]); return m
def uv_cell(u, cellname):
    i=CELL[cellname]; return ((i%GRID)*CPX+CPX/2)/256, 1-((i//GRID)*CPX+CPX/2)/256
def paint(bm, fn):
    """fn(face)->cell name ; sets uv to that palette cell center"""
    uv=bm.loops.layers.uv.verify()
    for f in bm.faces:
        c=uv_cell(None, fn(f))
        for l in f.loops: l[uv].uv=c
def tris(ob): return sum(len(p.vertices)-2 for p in ob.data.polygons)
def link(ob, parent=None):
    bpy.context.scene.collection.objects.link(ob)
    if parent: ob.parent=parent
    return ob
def empty(name, loc=(0,0,0), parent=None, size=0.2):
    e=bpy.data.objects.new(name,None); e.empty_display_size=size; e.location=loc; return link(e,parent)
def mesh_from_bm(name, bm, mats, parent=None, loc=(0,0,0)):
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    ob=bpy.data.objects.new(name,me); ob.location=loc; return link(ob,parent)
def export(path_fbx, path_glb=None):
    os.makedirs(os.path.dirname(path_fbx),exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path_fbx, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
        global_scale=1.0, bake_space_transform=True, use_custom_props=True, path_mode='RELATIVE', embed_textures=False,
        mesh_smooth_type='FACE', colors_type='LINEAR', add_leaf_bones=False, object_types={'EMPTY','MESH'})
    if path_glb:
        os.makedirs(os.path.dirname(path_glb),exist_ok=True)
        for im in bpy.data.images:
            if im.filepath: im.filepath=bpy.path.abspath(im.filepath); im.reload()
        bpy.ops.export_scene.gltf(filepath=path_glb, export_format='GLB', export_yup=True, export_extras=True)

# ---------- fracture ----------
def box_bm(sx,sy,sz):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector((v.co.x*sx, v.co.y*sy, v.co.z*sz+sz/2))
    return bm
def voronoi_cells(sx,sy,sz,seeds):
    cells=[]
    for i,s in enumerate(seeds):
        bm=box_bm(sx,sy,sz)
        for j,o in enumerate(seeds):
            if i==j: continue
            mid=(s+o)/2; n=(o-s).normalized()
            geom=bm.verts[:]+bm.edges[:]+bm.faces[:]
            r=bmesh.ops.bisect_plane(bm,geom=geom,plane_co=mid,plane_no=n,clear_outer=True)
            if len(bm.verts)==0: break
            bmesh.ops.contextual_create(bm, geom=[e for e in r['geom_cut'] if isinstance(e,bmesh.types.BMEdge)])
        bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-5)
        cells.append(bm)
    return cells
def on_box(f,sx,sy,sz,eps=1e-4):
    c=f.calc_center_median(); n=f.normal
    return (abs(abs(c.x)-sx/2)<eps and abs(abs(n.x)-1)<1e-3) or (abs(abs(c.y)-sy/2)<eps and abs(abs(n.y)-1)<1e-3) or \
           ((c.z<eps or abs(c.z-sz)<eps) and abs(abs(n.z)-1)<1e-3)

def write_brickface(path):
    """256px tile = 1 m x 1 m: 4 courses of 0.5 x 0.25 m bricks, 0.04 m deep-red mortar groove (reads at distance)"""
    try: import PIL
    except ImportError: return subprocess.check_call(['/usr/bin/python3','-c',f'import sys;sys.path.insert(0,{HERE!r});import cv_lib;cv_lib.write_brickface({path!r})'])
    from PIL import Image, ImageDraw; import random as R
    R.seed(3); im=Image.new("RGB",(256,256),tuple(int(255*v) for v in hexrgb("#6A2A1E"))); d=ImageDraw.Draw(im); g=5
    b=hexrgb("#D2664C")
    for row in range(4):
        off=0 if row%2==0 else 64
        for col in range(-1,3):
            x0=col*128+off; k=0.9+R.random()*0.2
            for yy in range(64-2*g):  # bevel-ish highlight on top
                kk=k*(1.12-0.25*yy/(64-2*g)); d.line([x0+g,row*64+g+yy,x0+128-g,row*64+g+yy],fill=tuple(min(255,int(255*v*kk)) for v in b))
    im.save(path)
def bevel_hard(bm, off=0.03, seg=1, angle=30):
    """1-2 segment bevel on edges whose face angle exceeds `angle` degrees"""
    import math
    bm.normal_update()
    es=[e for e in bm.edges if len(e.link_faces)==2 and e.calc_face_angle(0)>math.radians(angle)]
    if not es: return bm
    old=set(bm.faces); r=bmesh.ops.bevel(bm,geom=es,offset=off,segments=seg,affect='EDGES',clamp_overlap=True)
    uv=bm.loops.layers.uv.active
    if uv:   # bevel faces take the colour of one neighbouring original face (no palette bleeding)
        for f in r['faces']:
            nb=[g for e in f.edges for g in e.link_faces if g in old]
            if nb:
                f.material_index=nb[0].material_index; c=nb[0].loops[0][uv].uv.copy()
                for l in f.loops: l[uv].uv=c
    return bm
def save_src(asset, name):
    d=os.path.join(SRC,asset); os.makedirs(d,exist_ok=True); bpy.ops.wm.save_as_mainfile(filepath=os.path.join(d,name+".blend"),compress=True)

# ---------------- v3 (CrashverseGlossyPBR) helpers ----------------
def gen_tex(*args):
    subprocess.check_call(['/usr/bin/python3',os.path.join(HERE,'textures.py'),*args])
def material_pbr(name, albedo, normal=None, orm=None, emissive=None, rough=0.5, metal=0.0, emis_strength=2.0):
    """Principled setup mirroring CrashverseGlossyPBR: ORM R=AO G=rough B=metal, normal OpenGL Y+"""
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree; b=nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value=rough; b.inputs["Metallic"].default_value=metal
    def img(p,cs):
        t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(p,check_existing=True); t.image.colorspace_settings.name=cs; return t
    nt.links.new(img(albedo,"sRGB").outputs[0],b.inputs["Base Color"])
    if orm:
        t=img(orm,"Non-Color"); s=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0],s.inputs[0])
        nt.links.new(s.outputs[1],b.inputs["Roughness"]); nt.links.new(s.outputs[2],b.inputs["Metallic"])
    if normal:
        t=img(normal,"Non-Color"); nm=nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0],nm.inputs["Color"]); nt.links.new(nm.outputs[0],b.inputs["Normal"])
    if emissive:
        t=img(emissive,"sRGB"); nt.links.new(t.outputs[0],b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value=emis_strength
    m["shader"]="CrashverseGlossyPBR"; return m
def add_vcol(ob, wind=0.0, wind_fn=None):
    """Vertex colour: R = damage mask (0), G,B = 0, A = wind/flex weight"""
    me=ob.data; ca=me.color_attributes.get("Col") or me.color_attributes.new("Col",'BYTE_COLOR','CORNER')
    for poly in me.polygons:
        w=wind_fn(ob,poly) if wind_fn else wind
        for li in poly.loop_indices: ca.data[li].color=(0,0,0,w)
    me.color_attributes.active_color=ca
def add_lightmap_uv(obs, name="UV2_Lightmap", margin=0.02):
    """second UV channel (Unity UV1/'UV2') for lightmaps, non-overlapping, packed per object"""
    for ob in obs:
        me=ob.data
        if name not in me.uv_layers: me.uv_layers.new(name=name)
        first=me.uv_layers[0].name
        bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=ob; ob.select_set(True)
        me.uv_layers.active=me.uv_layers[name]
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=margin,scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        me.uv_layers.active=me.uv_layers[first]; me.uv_layers[first].active_render=True
