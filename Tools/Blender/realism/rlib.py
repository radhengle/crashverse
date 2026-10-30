"""Realism-test Blender helpers: parts built in a local frame -> grid-cut on the atlas cell size -> tiling-cell UVs
(seamless, ~256 px/m at 1024) -> transformed -> organic jitter -> merged into ONE mesh / ONE material per asset."""
import bpy,bmesh,json,math,random,os
from mathutils import Vector,Matrix,noise
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"../../.."))
REAL=ROOT+"/Assets/_Project/Models/Realistic"
class Atlas:
    def __init__(s,path): s.d=json.load(open(path))["cells"]
    def rect(s,c): return s.d[c]["uv"]
    def m(s,c): return s.d[c]["m"]
def bm_box(sx,sy,sz,center=(0,0,0)):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    c=Vector(center)
    for v in bm.verts: v.co=Vector((v.co.x*sx,v.co.y*sy,v.co.z*sz))+c
    return bm
def bevel(bm,off,seg=1,prof=.5,clamp=True):
    if off<=0: return bm
    bmesh.ops.bevel(bm,geom=bm.edges[:]+bm.verts[:],offset=off,segments=seg,profile=prof,affect='EDGES',clamp_overlap=clamp)
    bm.normal_update(); return bm
def cut_grid(bm,axis,S,off=0.0):
    if S<=0: return
    vs=[v.co[axis] for v in bm.verts]; lo,hi=min(vs),max(vs)
    k=math.floor((lo-off)/S)+1
    n=Vector((0,0,0)); n[axis]=1
    while off+k*S<hi-1e-4:
        c=Vector((0,0,0)); c[axis]=off+k*S
        bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=c,plane_no=n); k+=1
def uv_cells(bm,atlas,cell,grain=None,off=(0.0,0.0,0.0),cut=True,faces=None,inset=.0005):
    """box-projection UVs in the CURRENT coords; u along `grain` axis when it lies in the face plane."""
    Sx,Sy=atlas.m(cell); r=atlas.rect(cell)
    if cut:
        for a in range(3): cut_grid(bm,a,Sx if (grain is None or a==grain) else Sy,off[a])
    uv=bm.loops.layers.uv.get("UV0") or bm.loops.layers.uv.new("UV0")
    cl=bm.faces.layers.int.get("cell") or bm.faces.layers.int.new("cell")
    bm.normal_update()
    for f in (faces or bm.faces):
        n=f.normal; d=max(range(3),key=lambda i:abs(n[i])); ax=[i for i in range(3) if i!=d]
        if grain is not None and grain in ax: ua=grain; va=[i for i in ax if i!=grain][0]
        else: ua,va=ax
        Su=Sx if (grain is None or ua==grain) else Sy; Sv=Sy
        if grain is None: Su,Sv=Sx,Sy
        c=f.calc_center_median()
        ku=math.floor((c[ua]-off[ua])/Su); kv=math.floor((c[va]-off[va])/Sv)
        sgn=-1 if (d==0 and n[0]<0) or (d==1 and n[1]>0) else 1      # keep texture un-mirrored when viewed from outside
        for L in f.loops:
            u=(L.vert.co[ua]-off[ua]-ku*Su)/Su; v=(L.vert.co[va]-off[va]-kv*Sv)/Sv
            u=min(max(u,0),1); v=min(max(v,0),1)
            if sgn<0: u=1-u
            L[uv].uv=(r[0]+inset+(r[2]-r[0]-2*inset)*u, r[1]+inset+(r[3]-r[1]-2*inset)*v)
        f[cl]=1
def uv_fixed(bm,atlas,cell,faces,uvfn):
    """explicit (u,v) in 0..1 of the cell, uvfn(loop)->(u,v)"""
    r=atlas.rect(cell); uv=bm.loops.layers.uv.get("UV0") or bm.loops.layers.uv.new("UV0")
    for f in faces:
        for L in f.loops:
            u,v=uvfn(L); L[uv].uv=(r[0]+(r[2]-r[0])*min(max(u,0),1), r[1]+(r[3]-r[1])*min(max(v,0),1))
def transform(bm,M):
    bmesh.ops.transform(bm,matrix=M,verts=bm.verts[:])
    if M.to_3x3().determinant()<0: bmesh.ops.reverse_faces(bm,faces=bm.faces[:])   # mirrored frame: keep outward winding
    bm.normal_update()
def closed(bm): return all(len(e.link_faces)==2 for e in bm.edges)
def orient(bm,expected,faces=None):
    """flip faces whose normal points against expected(face)->Vector"""
    bm.normal_update(); fl=[f for f in (faces or bm.faces) if f.normal.dot(expected(f))<0]
    if fl: bmesh.ops.reverse_faces(bm,faces=fl); bm.normal_update()
    return len(fl)
def jitter(bm,amp,freq=1.7,seed=0,axes=(1,1,1)):
    o=Vector((seed*13.1,seed*7.7,seed*3.3))
    for v in bm.verts:
        d=noise.noise_vector(v.co*freq+o)
        v.co+=Vector((d.x*axes[0],d.y*axes[1],d.z*axes[2]))*amp
    bm.normal_update()
def tint(bm,val):
    """per-vertex colour multiplier (also carries baked AO later): 'Col' attribute (point domain via loops)"""
    c=bm.loops.layers.float_color.get("Col") or bm.loops.layers.float_color.new("Col")
    for f in bm.faces:
        for L in f.loops: L[c]=(val[0],val[1],val[2],1) if not callable(val) else val(L)
class Builder:
    """collects part bmeshes; .add(bm) merges into one big bmesh"""
    def __init__(s,atlas): s.A=atlas; s.parts=[]
    def add(s,bm,col=(1,1,1)):
        if closed(bm) and len(bm.faces)>3: bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); bm.normal_update()
        if not bm.loops.layers.float_color.get("Col"): tint(bm,col)
        s.parts.append(bm); return bm
    def part(s,size,M,cell,grain=0,bev=.01,seg=1,off=None,warp=0.0,jit=0.0,col=(1,1,1),seed=None,faces_cell=None):
        """box of `size` centred at origin, local x = grain; M = world matrix"""
        rnd=random.Random(seed if seed is not None else len(s.parts))
        bm=bm_box(*size); bevel(bm,min(bev,min(size)/2.2),seg)
        off=off if off is not None else (rnd.random()*3,rnd.random()*3,rnd.random()*3)
        uv_cells(bm,s.A,cell,grain,off)
        if faces_cell: faces_cell(bm)
        if warp:
            L=size[0]; ph=rnd.random()*6
            for v in bm.verts:
                t=v.co.x/L; v.co.y+=warp*math.sin(math.pi*(t+.5))*(.6+.4*math.sin(ph)); v.co.z+=warp*.5*math.sin(2*math.pi*t+ph)
        transform(bm,M)
        if jit: jitter(bm,jit,2.3,rnd.randint(0,999))
        c=(col[0],col[1],col[2]); return s.add(bm,c)
    def to_object(s,name,mat):
        me=bpy.data.meshes.new(name); big=bmesh.new()
        tmp=bpy.data.meshes.new("tmp")
        for bm in s.parts:
            bm.to_mesh(tmp); big.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
        bmesh.ops.triangulate(big,faces=[f for f in big.faces if len(f.verts)>4])
        big.to_mesh(me); big.free()
        o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
        me.materials.append(mat)
        for p in me.polygons: p.use_smooth=True
        me.set_sharp_from_angle(angle=math.radians(40))
        return o
def tri_count(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
def mesh_from(bm,name="tmp"):
    me=bpy.data.meshes.new(name); bm.to_mesh(me); o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o); return o
def boolean_diff(bm,cutters,solver='EXACT'):
    """cutters: list of bmesh; returns new bmesh"""
    o=mesh_from(bm,"booltgt"); cs=[]
    col=bpy.data.collections.new("cutters"); bpy.context.scene.collection.children.link(col)
    for c in cutters:
        me=bpy.data.meshes.new("cut"); c.to_mesh(me); co=bpy.data.objects.new("cut",me); col.objects.link(co); cs.append(co)
    m=o.modifiers.new("b",'BOOLEAN'); m.operation='DIFFERENCE'; m.operand_type='COLLECTION'; m.collection=col; m.solver=solver
    dg=bpy.context.evaluated_depsgraph_get(); ev=o.evaluated_get(dg)
    out=bmesh.new(); out.from_mesh(ev.to_mesh()); ev.to_mesh_clear()
    for co in cs: bpy.data.objects.remove(co)
    bpy.data.collections.remove(col); bpy.data.objects.remove(o)
    return out
# ---------------------------------------------------------------- materials (Blender preview of the URP Lit setup)
def image(path,noncolor=False):
    im=bpy.data.images.load(path,check_existing=True)
    if noncolor: im.colorspace_settings.name='Non-Color'
    return im
def lit_material(name,tex_prefix,alpha=False,vcol=True):
    """Base = A * vertexColour ; ORM: R AO (multiplied, strength already <=0.5), G rough, B metal ; N = OpenGL normal"""
    m=bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree; N=nt.nodes; Lk=nt.links
    b=N["Principled BSDF"]
    def tex(sfx,nc):
        t=N.new("ShaderNodeTexImage"); t.image=image(f"{tex_prefix}_{sfx}.png",nc); t.interpolation='Linear'; return t
    A=tex("A",False); O=tex("ORM",True); Nn=tex("N",True)
    sep=N.new("ShaderNodeSeparateColor"); Lk.new(O.outputs[0],sep.inputs[0])
    mul=N.new("ShaderNodeMix"); mul.data_type='RGBA'; mul.blend_type='MULTIPLY'; mul.inputs[0].default_value=1
    Lk.new(A.outputs[0],mul.inputs[6])
    if vcol:
        vc=N.new("ShaderNodeVertexColor"); vc.layer_name="Col"; Lk.new(vc.outputs[0],mul.inputs[7])
    else: mul.inputs[7].default_value=(1,1,1,1)
    mul2=N.new("ShaderNodeMix"); mul2.data_type='RGBA'; mul2.blend_type='MULTIPLY'; mul2.inputs[0].default_value=1
    Lk.new(mul.outputs[2],mul2.inputs[6]); Lk.new(sep.outputs[0],mul2.inputs[7])
    Lk.new(mul2.outputs[2],b.inputs["Base Color"]); Lk.new(sep.outputs[1],b.inputs["Roughness"]); Lk.new(sep.outputs[2],b.inputs["Metallic"])
    nm=N.new("ShaderNodeNormalMap"); nm.uv_map="UV0"; Lk.new(Nn.outputs[0],nm.inputs[1]); Lk.new(nm.outputs[0],b.inputs["Normal"])
    for t in (A,O,Nn):
        u=N.new("ShaderNodeUVMap"); u.uv_map="UV0"; Lk.new(u.outputs[0],t.inputs[0])
    if alpha:
        gt=N.new("ShaderNodeMath"); gt.operation='GREATER_THAN'; gt.inputs[1].default_value=.5
        Lk.new(A.outputs[1],gt.inputs[0]); Lk.new(gt.outputs[0],b.inputs["Alpha"])
        try: m.surface_render_method='DITHERED'
        except Exception: pass
        b.inputs["Subsurface Weight"].default_value=0.0
    return m
def add_uv2(o,margin=.003):
    """UV2 lightmap channel (validator: statics need UV0 + UV2)"""
    me=o.data
    if "UV2" not in me.uv_layers: me.uv_layers.new(name="UV2")
    me.uv_layers.active=me.uv_layers["UV2"]
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60),island_margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT'); me.uv_layers.active=me.uv_layers["UV0"]
    for i,l in enumerate(me.uv_layers): pass
def bake_vertex_ao(objs,target,samples=64,dist=1.2,strength=.5,keep_tint=True):
    """Cycles AO bake into the 'Col' attribute of target (multiplied with the existing tint). AO strength <= 0.5."""
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=samples; sc.cycles.device='CPU'
    me=target.data; old=[tuple(d.color) for d in me.color_attributes["Col"].data]
    ca=me.color_attributes.new("AOtmp",'FLOAT_COLOR','CORNER'); me.color_attributes.active_color=ca
    w=bpy.data.worlds.get("bakeW") or bpy.data.worlds.new("bakeW"); sc.world=w; w.light_settings.distance=dist
    sc.render.bake.target='VERTEX_COLORS'
    bpy.ops.object.select_all(action='DESELECT'); target.select_set(True); bpy.context.view_layer.objects.active=target
    bpy.ops.object.bake(type='AO',target='VERTEX_COLORS')
    col=me.color_attributes["Col"]
    if col.domain!='CORNER': raise RuntimeError("Col must be corner domain")
    for i,d in enumerate(ca.data):
        a=d.color[0]; a=1-(1-a)*strength; t=old[i]
        col.data[i].color=(t[0]*a,t[1]*a,t[2]*a,1)
    me.color_attributes.remove(ca); me.color_attributes.active_color=me.color_attributes["Col"]
def vertex_ao(target,occluders,rays=32,dist=1.0,strength=.5,offset=.025,seed=1):
    """ray-traced vertex AO (cosine-weighted hemisphere, origin pushed off the surface so contact/buried verts don't go black).
    Result multiplied into 'Col' (corner domain). Final factor = 1-(1-ao)*strength  (style board: AO <= 0.5)."""
    from mathutils.bvhtree import BVHTree
    dg=bpy.context.evaluated_depsgraph_get(); bms=[]
    allbm=bmesh.new(); tmp=bpy.data.meshes.new("aotmp")
    for o in occluders:
        b=bmesh.new(); b.from_mesh(o.data); b.transform(o.matrix_world); b.to_mesh(tmp); allbm.from_mesh(tmp); b.free()
    bpy.data.meshes.remove(tmp); tree=BVHTree.FromBMesh(allbm)
    rnd=random.Random(seed); dirs=[]
    for i in range(rays):
        u,v=(i+.5)/rays,rnd.random(); r=math.sqrt(u); a=2*math.pi*v; dirs.append((r*math.cos(a),r*math.sin(a),math.sqrt(1-u)))
    me=target.data; M=target.matrix_world; N3=M.to_3x3().inverted().transposed()
    me.calc_normals_split() if hasattr(me,"calc_normals_split") else None
    vao={}
    for v in me.vertices:
        n=(N3@v.normal).normalized(); p=M@v.co+n*offset
        t=n.orthogonal().normalized(); b=n.cross(t); occ=0
        for (x,y,z) in dirs:
            d=t*x+b*y+n*z; hit=tree.ray_cast(p,d,dist)
            if hit[0] is not None and hit[3]>.07: occ+=(1-hit[3]/dist)**.7   # ignore buried/contact hits (avoids dark smears across faces)
        vao[v.index]=1-occ/rays
    col=me.color_attributes["Col"]
    for li,l in enumerate(me.loops):
        a=1-(1-vao[l.vertex_index])*strength; c=col.data[li].color; col.data[li].color=(c[0]*a,c[1]*a,c[2]*a,1)
    allbm.free()
