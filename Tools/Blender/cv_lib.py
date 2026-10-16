import random, os, math, subprocess, sys
try:
    import bpy, bmesh
    from mathutils import Vector
except ImportError: pass
HERE=os.path.dirname(os.path.abspath(__file__))
PAL = ["#E3A766","#A8703A","#D2664C","#7A3326","#AEB6C2","#5B6472","#3E4C6E","#E8B04B",
       "#22D3EE","#8B5CF6","#FF7A1A","#101828","#7ED957","#C98A4B","#F2D49B","#4D7BC4"]
CELL = {n:i for i,n in enumerate(["wood","wood_dk","brick","mortar","steel","steel_dk","navy","gold",
       "cyan","violet","orange","outline","grass","cliff","sand","roof"])}
DENS = {"Wood":600,"Brick":1900,"Steel":800}  # steel = hollow effective density (Physics sign-off)
def hexrgb(h): return tuple(int(h[i:i+2],16)/255 for i in (1,3,5))
def write_palette(path):
    try: import PIL
    except ImportError: return subprocess.check_call(['/usr/bin/python3','-c',f'import sys;sys.path.insert(0,{HERE!r});import cv_lib;cv_lib.write_palette({path!r})'])
    from PIL import Image, ImageDraw
    im=Image.new("RGB",(256,256)); d=ImageDraw.Draw(im)
    for i,h in enumerate(PAL):
        x,y=(i%4)*64,(i//4)*64; c=hexrgb(h)
        for r in range(64):  # soft vertical gradient = glossy toy feel
            k=1.12-0.24*r/63; d.line([x,y+r,x+63,y+r],fill=tuple(min(255,int(v*255*k)) for v in c))
    im.save(path)
def write_inner(path, base, dark):
    try: import PIL
    except ImportError: return subprocess.check_call(['/usr/bin/python3','-c',f'import sys;sys.path.insert(0,{HERE!r});import cv_lib;cv_lib.write_inner({path!r},{base!r},{dark!r})'])
    from PIL import Image; import random as R
    R.seed(7); a,b=hexrgb(base),hexrgb(dark); im=Image.new("RGB",(256,256)); px=im.load()
    for y in range(256):
        for x in range(256):
            t=R.random()*0.35+0.25*((x//32+y//32)%2); px[x,y]=tuple(int(255*(a[i]*(1-t)+b[i]*t)) for i in range(3))
    im.save(path)
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def material(name, img, rough=0.35, metal=0.0):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True
    nt=m.node_tree; b=nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value=rough; b.inputs["Metallic"].default_value=metal
    t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(img, check_existing=True); t.interpolation="Closest"
    nt.links.new(t.outputs[0], b.inputs[0]); return m
def uv_cell(u, cellname):
    i=CELL[cellname]; return ((i%4)*64+32)/256, 1-((i//4)*64+32)/256
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
        mesh_smooth_type='FACE', add_leaf_bones=False, object_types={'EMPTY','MESH'})
    if path_glb:
        os.makedirs(os.path.dirname(path_glb),exist_ok=True)
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
