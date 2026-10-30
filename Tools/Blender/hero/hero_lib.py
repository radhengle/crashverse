"""CRASHVERSE hero buildings v1 - shared library.
A building = a set of breakable MODULES (Kit_<B>_<Module>, #28 class Kit under BuildingKit/Kit_<B>/) + a layout JSON.
Each module is authored as CONVEX PARTS (hull shells, tagged with a material class). From the parts we derive:
  * LP LOD0 (parts minus faces buried inside another part), LOD1/LOD2 by collapse decimation (same UVs)
  * HP detail generated per face/class (individual bevelled stone blocks on world-z 0.3 m courses, roof tiles in rows,
    planks, rivets, leaded glass, cloth folds) -> baked into ONE 2048 atlas per building: T_<B>_A / _N / _ORM (AO in R)
  * UCX_<Module>_LOD0_## hulls (<=8, <=255 tris) for the intact piece
  * <Module>_Fractured: voronoi cells clip the structural parts (details stay whole and go with the cell holding their
    centre); cut + buried faces = M_<B>_Inner (T_<B>_Inner_A/N/ORM, per-class cells). UCX_<frag> exact, pivot = COM,
    mass_kg from per-class density, JNT_ from shared voronoi faces, ANCHOR_ on the bearing side.
"""
import sys,os,bpy,bmesh,math,random,json,subprocess,zlib
from mathutils import Vector,Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); BL=os.path.dirname(HERE)
for p in (BL,os.path.join(BL,"v3")):
    if p not in sys.path: sys.path.insert(0,p)
import cv_lib as CV
import hp
ROOT="/workspace/wt/3d-modeler-v3"; KITP="Assets/_Project/Models/BuildingKit"; SRC="/workspace/crashverse/models_src"
CLS=["stone","stone_dk","roof","wood","wood_dk","paint","plaster","iron","gold","cloth","cloth_red","glass","leaf","lamp"]
CI={c:i for i,c in enumerate(CLS)}
DENS={"stone":2500,"stone_dk":2500,"roof":1800,"wood":600,"wood_dk":600,"paint":600,"plaster":1600,"iron":800,"gold":800,
      "cloth":300,"cloth_red":300,"glass":2500,"leaf":200,"lamp":800}
RNG={"Stone":(4,10),"Roof":(8,16),"Wood":(8,16),"Steel":(4,10),"Brick":(12,24),"Glass":(8,16)}
INNER={"stone":("#B8A074","#8F7A55",0.85,0),"stone_dk":("#A38C64","#7D6848",0.85,0),"roof":("#B5543A","#8A3424",0.8,0),
       "wood":("#D8AE73","#A87A45",0.75,0),"wood_dk":("#B88A55","#7E5A35",0.75,0),"paint":("#D0A86E","#9C7442",0.75,0),
       "plaster":("#E6DAC0","#BFAF8F",0.9,0),"iron":("#6A707B","#4A4F58",0.5,1),"gold":("#D9A845","#A87A28",0.35,1),
       "cloth":("#3A64D8","#24449E",0.9,0),"cloth_red":("#D8443A","#9E2A24",0.9,0),"glass":("#DDF2FF","#A9CDE6",0.15,0),
       "leaf":("#5DBB45","#3A8A2E",0.9,0),"lamp":("#FFE6A8","#E8B860",0.3,0)}
LAYERS=("cls","hid","fid","cut","mod")
# ------------------------------------------------------------------ parts
class Part:
    def __init__(s,bm,cls,detail=False):
        s.bm=bm; s.cls=cls; s.detail=detail
        for L in LAYERS:
            if L not in bm.faces.layers.int: bm.faces.layers.int.new(L)
        c=bm.faces.layers.int["cls"]
        for f in bm.faces: f[c]=CI[cls]
    def copy(s): p=Part.__new__(Part); p.bm=s.bm.copy(); p.cls=s.cls; p.detail=s.detail; p.arc=getattr(s,"arc",None); return p
    def bbox(s):
        vs=[v.co for v in s.bm.verts]; return Vector([min(v[i] for v in vs) for i in range(3)]),Vector([max(v[i] for v in vs) for i in range(3)])
    def centre(s): a,b=s.bbox(); return (a+b)/2
    def vol(s): return abs(s.bm.calc_volume())
def hull_bm(points):
    bm=bmesh.new()
    for p in points: bm.verts.new(Vector(p))
    bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5)
    r=bmesh.ops.convex_hull(bm,input=bm.verts[:],use_existing_faces=False)
    junk=list({g for g in r["geom_interior"]+r["geom_unused"] if isinstance(g,bmesh.types.BMVert)})
    if junk: bmesh.ops.delete(bm,geom=junk,context='VERTS')
    bmesh.ops.dissolve_limit(bm,angle_limit=math.radians(0.5),verts=bm.verts[:],edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); bm.normal_update(); return bm
def P(points,cls,detail=False,bev=0.0,seg=1,angle=30):
    bm=hull_bm(points)
    if bev>0: CV.bevel_hard(bm,bev,seg,angle); bm.normal_update()
    return Part(bm,cls,detail)
def box(x0,x1,y0,y1,z0,z1,cls,**k):
    return P([Vector((x,y,z)) for x in (x0,x1) for y in (y0,y1) for z in (z0,z1)],cls,**k)
def bxc(c,s,cls,R=None,**k):
    R=R or Matrix.Identity(3); c=Vector(c)
    return P([c+R@Vector((sx*s[0]/2,sy*s[1]/2,sz*s[2]/2)) for sx in (-1,1) for sy in (-1,1) for sz in (-1,1)],cls,**k)
def prism_xz(poly,y0,y1,cls,**k): return P([Vector((x,y,z)) for (x,z) in poly for y in (y0,y1)],cls,**k)
def prism_xy(poly,z0,z1,cls,**k): return P([Vector((x,y,z)) for (x,y) in poly for z in (z0,z1)],cls,**k)
def pol(r,a): return (r*math.sin(a),-r*math.cos(a))        # a=0 -> -Y (front)
def frustum(r0,r1,z0,z1,seg,cls,a0=0.0,**k):
    pts=[Vector(pol(r0,a0+2*math.pi*i/seg)+(z0,)) for i in range(seg)]
    pts+=[Vector((0,0,z1))] if r1<1e-3 else [Vector(pol(r1,a0+2*math.pi*i/seg)+(z1,)) for i in range(seg)]
    return P(pts,cls,**k)
def pyramid(w0,w1,z0,z1,cls,**k):
    pts=[Vector((sx*w0/2,sy*w0/2,z0)) for sx in (-1,1) for sy in (-1,1)]
    pts+=[Vector((0,0,z1))] if w1<1e-3 else [Vector((sx*w1/2,sy*w1/2,z1)) for sx in (-1,1) for sy in (-1,1)]
    return P(pts,cls,**k)
def wedge(ri,ro,a0,a1,z0,z1,cls,n=2,ro1=None,**k):
    """ring segment a0..a1 (rad): inner chord, outer arc with n facets; ro1 = outer radius at z1 (batter)"""
    ro1=ro if ro1 is None else ro1
    pts=[Vector(pol(ri,a)+(z,)) for a in (a0,a1) for z in (z0,z1)]
    for i in range(n+1):
        a=a0+(a1-a0)*i/n; pts+=[Vector(pol(ro,a)+(z0,)),Vector(pol(ro1,a)+(z1,))]
    return P(pts,cls,**k)
def sphere(c,r,cls,seg=8,rings=5,**k):
    c=Vector(c); pts=[c+Vector((0,0,r)),c-Vector((0,0,r))]
    for j in range(1,rings):
        t=math.pi*j/rings
        for i in range(seg): a=2*math.pi*i/seg; pts.append(c+Vector((r*math.sin(t)*math.cos(a),r*math.sin(t)*math.sin(a),r*math.cos(t))))
    return P(pts,cls,**k)
def cyl_x(x0,x1,y,z,r,seg,cls,**k):
    return P([Vector((x,y+r*math.cos(2*math.pi*i/seg),z+r*math.sin(2*math.pi*i/seg))) for x in (x0,x1) for i in range(seg)],cls,**k)
def cyl_z(cx,cy,z0,z1,r,seg,cls,**k):
    return P([Vector((cx+r*math.cos(2*math.pi*i/seg),cy+r*math.sin(2*math.pi*i/seg),z)) for z in (z0,z1) for i in range(seg)],cls,**k)
def seg_box(p0,p1,w,d,cls,up=Vector((0,1,0)),**k):
    """box from p0 to p1 (beam / brace), cross-section w (in plane) x d (along `up`)"""
    p0,p1=Vector(p0),Vector(p1); ax=(p1-p0); L=ax.length; ax.normalize(); u=Vector(up).normalized(); s=ax.cross(u).normalized(); u=s.cross(ax)
    R=Matrix((ax,s,u)).transposed(); return bxc((p0+p1)/2,(L,w,d),cls,R=R,**k)
def xf(parts,M):
    for p in parts: p.bm.transform(M); p.bm.normal_update()
    return parts
def Rz(deg): return Matrix.Rotation(math.radians(deg),4,'Z')
def T(x,y,z): return Matrix.Translation((x,y,z))
def arch_voussoirs(xc,zs,r,W,ztop,y0,y1,n,cls,key=True,bev=0.025):
    """semicircular arch (centre xc, springing zs, radius r) filled up to the rectangle |x-xc|<=W/2, z<=ztop by n convex
    voussoir prisms (inner chord -> rectangle edge). Returns parts; the keystone is the middle one."""
    out=[]
    def edge(a):    # ray from centre at angle a (0=right, pi=left) hits rectangle
        dx,dz=math.cos(a),math.sin(a); ts=[]
        if abs(dx)>1e-6: ts.append((W/2)/abs(dx))
        if dz>1e-6: ts.append((ztop-zs)/dz)
        t=min(ts); return (xc+dx*t,zs+dz*t)
    for i in range(n):
        a0=math.pi*i/n; a1=math.pi*(i+1)/n
        poly=[(xc+r*math.cos(a0),zs+r*math.sin(a0)),(xc+r*math.cos(a1),zs+r*math.sin(a1)),edge(a1),edge(a0)]
        for a in (math.atan2(ztop-zs,W/2),math.atan2(ztop-zs,-W/2)):
            if a0<a<a1: poly.append((xc+W/2 if a<math.pi/2 else xc-W/2,ztop))
        out.append(prism_xz(poly,y0,y1,cls,bev=bev))
    return out
# ------------------------------------------------------------------ modules
class Module:
    def __init__(s,B,name,frac,parts,anchor="bottom",anchor_to="Ground",hide=(),note="",uvscale=1.0,interior=None):
        assert len(f"Kit_{B}_{name}_frag_00")<=28,f"Kit_{B}_{name}: JNT_<A>__<B> would exceed the 63-char object name limit"; s.interior=interior; s.uvscale=uvscale; s.B=B; s.short=name; s.name=f"Kit_{B}_{name}"; s.frac=frac; s.parts=parts; s.anchor=anchor; s.anchor_to=anchor_to
        s.hide=set(hide); s.note=note
def _planes(part): return [(f.normal.copy(),f.verts[0].co.copy()) for f in part.bm.faces]
def mark_hidden(mod):
    """faces lying completely inside-or-on another part are never visible: removed from the intact LODs, Inner when fractured"""
    P_=mod.parts; pls=[_planes(p) for p in P_]; bbs=[p.bbox() for p in P_]
    zs=[v.co.z for p in P_ for v in p.bm.verts]; z0,z1=min(zs),max(zs)
    for i,p in enumerate(P_):
        h=p.bm.faces.layers.int["hid"]
        for f in p.bm.faces:
            pts=[v.co for v in f.verts]+[f.calc_center_median()]
            c=pts[-1]
            if ("bottom" in mod.hide and f.normal.z<-0.99 and abs(c.z-z0)<1e-3) or ("top" in mod.hide and f.normal.z>0.99 and abs(c.z-z1)<1e-3):
                f[h]=1; continue
            for j,q in enumerate(P_):
                if j==i: continue
                a,b=bbs[j]
                if any(pt[k]<a[k]-1e-3 or pt[k]>b[k]+1e-3 for pt in pts for k in range(3)): continue
                if all(all((pt-pp).dot(n)<=2e-4 for n,pp in pls[j]) for pt in pts): f[h]=1; break
def bm_merge(dst,src_bm):
    me=bpy.data.meshes.new("_t"); src_bm.to_mesh(me); dst.from_mesh(me); bpy.data.meshes.remove(me)
def ensure_layers(bm):
    for L in LAYERS:
        if L not in bm.faces.layers.int: bm.faces.layers.int.new(L)
    bm.loops.layers.uv.verify()
    return bm
def visible_copy(part):
    b=part.bm.copy(); h=b.faces.layers.int["hid"]
    dead=[f for f in b.faces if f[h]]
    if dead: bmesh.ops.delete(b,geom=dead,context='FACES_ONLY')
    loose=[v for v in b.verts if not v.link_faces]
    if loose: bmesh.ops.delete(b,geom=loose,context='VERTS')
    for e in [e for e in b.edges if not e.link_faces]: b.edges.remove(e)
    return b
def tri_count(bm): return sum(len(f.verts)-2 for f in bm.faces)
# ------------------------------------------------------------------ HP detail generation
class HPAcc:
    def __init__(s): s.d={}; s.ground=0.0
    def add(s,key,bm,tint=(0.5,0.5,0.5)):
        V,F,C=s.d.setdefault(key,([],[],[])); o=len(V)
        for v in bm.verts: V.append(tuple(v.co)); C.append((tint[0],tint[1],tint[2],s.ground))
        idx={v:i for i,v in enumerate(bm.verts)}
        for f in bm.faces: F.append([o+idx[v] for v in f.verts])
        bm.free()
    def objects(s,mats):
        out=[]
        for key,(V,F,C) in s.d.items():
            me=bpy.data.meshes.new("HP_"+key); me.from_pydata(V,[],F); me.update()
            a=me.color_attributes.new("tint",'FLOAT_COLOR','POINT'); g=me.color_attributes.new("gz",'FLOAT_COLOR','POINT')
            for i,c in enumerate(C): a.data[i].color=(c[0],c[1],c[2],1); g.data[i].color=(V[i][2],c[3],0,1)
            for p in me.polygons: p.use_smooth=True
            me.materials.append(mats[key].m); ob=bpy.data.objects.new("HP_"+key,me); bpy.context.scene.collection.objects.link(ob); out.append(ob)
        return out
def _frame(f):
    n=f.normal.normalized()
    u=Vector((0,0,1)).cross(n) if abs(n.z)<0.95 else Vector((1,0,0)).cross(n)
    u.normalize(); v=n.cross(u).normalized(); o=f.calc_center_median()
    return o,u,v,n,[((p.co-o).dot(u),(p.co-o).dot(v)) for p in f.verts]
def _span(poly,vv):
    xs=[]
    for i in range(len(poly)):
        (a,b),(c,d)=poly[i],poly[i-1]
        if (b-vv)*(d-vv)<=0 and abs(d-b)>1e-9: xs.append(a+(c-a)*(vv-b)/(d-b))
        elif abs(b-vv)<1e-9: xs.append(a)
    return (min(xs),max(xs)) if xs else None
def _blk(o,u,v,n,cu,cv,su,sv,d0,d1,rot=0.0,tilt=0.0,bev=0.02,seg=2):
    R=Matrix.Rotation(rot,3,n)@Matrix((u,v,n)).transposed()
    if tilt: R=R@Matrix.Rotation(tilt,3,'X')
    c=o+u*cu+v*cv+n*((d0+d1)/2)
    bm=hull_bm([c+R@Vector((a*su/2,b*sv/2,cc*(d1-d0)/2)) for a in (-1,1) for b in (-1,1) for cc in (-1,1)])
    if bev>0: bmesh.ops.bevel(bm,geom=bm.edges[:],offset=min(bev,su*0.3,sv*0.3),segments=seg,affect='EDGES',clamp_overlap=True)
    return bm
COURSES=(0.0,0.36,0.64)      # course boundaries within every metre (heights 0.36/0.28/0.36): stacked 3 m modules line up
def _bounds(lo,hi,world):
    if not world: 
        n=int(math.floor(lo/0.3)); out=[]
        while n*0.3<hi+0.3: out.append(n*0.3); n+=1
        return out
    out=[m+c for m in range(int(math.floor(lo))-1,int(math.ceil(hi))+1) for c in COURSES]
    return sorted(out)
def hp_courses(acc,key,f,R,lmin=0.26,lmax=0.95,gap=0.034,world_z=True,depth=(0.045,0.05),tints=True,bev=0.042):
    """stone blocks on a convex face: courses on world-z (vertical faces), lengths random, edges clipped to the face"""
    o,u,v,n,poly=_frame(f); vs=[p[1] for p in poly]; v0,v1=min(vs),max(vs)
    wz=world_z and abs(n.z)<0.3; zo=o.z if wz else 0.0
    bd=_bounds(zo+v0,zo+v1,wz)
    for j in range(len(bd)-1):
        a=max(bd[j]-zo,v0); b=min(bd[j+1]-zo,v1)
        if b-a<0.06: continue
        s1=_span(poly,a+0.004); s2=_span(poly,b-0.004)
        if not s1 or not s2: continue
        lo_=max(s1[0],s2[0]); hi_=min(s1[1],s2[1])
        if hi_-lo_<0.08: continue
        x=lo_-R.uniform(0.05,lmax)
        while x<hi_:
            L=R.uniform(lmin,lmax); x0=max(x,lo_); x1=min(x+L,hi_); x+=L
            if x1-x0<0.07: continue
            for (c0,c1) in (((a,(a+b)/2),((a+b)/2,b)) if (b-a>0.24 and x1-x0<0.55 and R.random()<0.3) else ((a,b),)):
                pr=R.uniform(0.014,depth[1])
                bm=_blk(o,u,v,n,(x0+x1)/2+R.uniform(-0.006,0.006),(c0+c1)/2,x1-x0-gap*R.uniform(0.8,1.25),c1-c0-gap*R.uniform(0.8,1.25),-depth[0],pr,R.uniform(-0.03,0.03),R.uniform(-0.04,0.04),bev,3)
                acc.add(key,bm,(R.random(),R.random(),R.random()) if tints else (0.5,0.5,0.5))
def hp_arc(acc,key,ro,a0,a1,z0,z1,R,off=Vector(),gap=0.034,depth=(0.045,0.05),bev=0.042):
    """stone courses running around a round wall (pattern continues across wedges: per-course RNG keyed on radius + z)"""
    import zlib
    lmax=min(0.9,ro*0.45); lmin=min(0.28,lmax*0.5); bd=_bounds(z0,z1,True); span=(a1-a0)%(2*math.pi) or 2*math.pi
    for j in range(len(bd)-1):
        za=max(bd[j],z0); zb=min(bd[j+1],z1)
        if zb-za<0.06: continue
        Rc=random.Random(zlib.crc32(f"{ro:.3f}:{round(bd[j]*1000)}".encode())); s0=Rc.uniform(0,2*math.pi); t=0.0
        while t<2*math.pi-1e-6:
            L=min(Rc.uniform(lmin,lmax)/ro,2*math.pi-t); s_=s0+t; e_=s_+L; t+=L; m=(s_+e_)/2
            if (m-a0)%(2*math.pi)>=span: continue
            if L*ro<0.12: continue
            n=Vector((math.sin(m),-math.cos(m),0)); u=Vector((math.cos(m),math.sin(m),0)); v=Vector((0,0,1))
            o=off+n*ro+Vector((0,0,(za+zb)/2)); w=2*ro*math.sin(L/2)-gap
            for (c0,c1) in (((za,(za+zb)/2),((za+zb)/2,zb)) if (zb-za>0.24 and w<0.5 and Rc.random()<0.3) else ((za,zb),)):
                bm=_blk(o,u,v,n,0,(c0+c1)/2-(za+zb)/2,w,c1-c0-gap,-depth[0],R.uniform(0.014,depth[1]),R.uniform(-0.03,0.03),R.uniform(-0.04,0.04),bev,3)
                acc.add(key,bm,(R.random(),R.random(),R.random()))
def hp_tiles(acc,f,R,row=0.19,w=0.17):
    o,u,v,n,poly=_frame(f)
    if n.z<0: v=-v
    if n.z>0.97: return False
    vs=[(p[0],p[1]*(1 if n.z>=0 else -1)) for p in poly]; poly=vs
    v0,v1=min(p[1] for p in poly),max(p[1] for p in poly); nrow=int((v1-v0)/row)+1
    for j in range(nrow):
        a=v0+j*row; b=min(a+row*1.5,v1)
        if b-a<0.05: break
        s1=_span(poly,a+0.005); s2=_span(poly,min(a+row,v1)-0.005)
        if not s1 or not s2: continue
        lo_=max(s1[0],s2[0]); hi_=min(s1[1],s2[1]); off=(j%2)*w/2
        k=math.floor((lo_-off)/w)
        while off+k*w<hi_:
            x0=max(off+k*w,lo_); x1=min(off+(k+1)*w,hi_); k+=1
            if x1-x0<0.05: continue
            bm=_blk(o,u,v,n,(x0+x1)/2,(a+b)/2,x1-x0-0.02,b-a,0.0,R.uniform(0.026,0.038),R.uniform(-0.05,0.05),math.radians(R.uniform(-8,-4)),0.012,2)
            acc.add("roof",bm,(R.random(),R.random(),R.random()))
    return True
def hp_planks(acc,key,f,R,width=(0.14,0.2),gap=0.012):
    o,u,v,n,poly=_frame(f); us=[p[0] for p in poly]; vs=[p[1] for p in poly]
    du,dv=max(us)-min(us),max(vs)-min(vs)
    if du>dv: poly=[(b,a) for a,b in poly]; u,v=v,u; n_=n; us,vs=vs,us
    # planks run along v; stacked across u
    x=min(p[0] for p in poly); X=max(p[0] for p in poly)
    while x<X-0.03:
        W=R.uniform(*width); x0=x; x1=min(x+W,X); x+=W
        s1=_span([(b,a) for a,b in poly],x0+0.003); s2=_span([(b,a) for a,b in poly],x1-0.003)
        if not s1 or not s2: continue
        a=max(s1[0],s2[0]); b=min(s1[1],s2[1])
        if b-a<0.05: continue
        bm=_blk(o,u,v,n,(x0+x1)/2,(a+b)/2,x1-x0-gap,b-a-0.004,-0.02,R.uniform(0.003,0.012),0,0,0.006,2)
        acc.add(key,bm,(R.random(),R.random(),R.random()))
def hp_rivets(acc,f,R,step=0.16,inset=0.05,r=0.016):
    o,u,v,n,poly=_frame(f)
    for i in range(len(poly)):
        a=Vector(poly[i-1]); b=Vector(poly[i]); L=(b-a).length
        if L<0.12: continue
        cen=Vector((sum(p[0] for p in poly)/len(poly),sum(p[1] for p in poly)/len(poly)))
        for k in range(max(1,int(L/step))):
            p=a+(b-a)*((k+0.5)/max(1,int(L/step))); p=p+(cen-p).normalized()*inset
            acc.add("iron",hull_bm([o+u*p.x+v*p.y+n*0.0+Vector(q)*r for q in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]]+[o+u*p.x+v*p.y+n*r*0.9]),(0.5,0.5,0.5))
def hp_lead(acc,f,R,step=0.16,w=0.018):
    """diamond leaded-glass came on a glazing face"""
    o,u,v,n,poly=_frame(f); us=[p[0] for p in poly]; vs=[p[1] for p in poly]
    u0,u1,v0,v1=min(us),max(us),min(vs),max(vs); D=(u1-u0)+(v1-v0)
    for s in (1,-1):
        k=-D
        while k<D:
            # line u - s*v = k clipped to rect
            pts=[]
            for uu in (u0,u1):
                vv=(uu-k)*s
                if v0-1e-6<=vv<=v1+1e-6: pts.append((uu,vv))
            for vv in (v0,v1):
                uu=k+s*vv
                if u0-1e-6<=uu<=u1+1e-6: pts.append((uu,vv))
            if len(pts)>=2:
                (a,b),(c,d)=pts[0],pts[-1]
                if math.hypot(c-a,d-b)>0.05:
                    p0=o+u*a+v*b+n*0.004; p1=o+u*c+v*d+n*0.004
                    acc.add("iron",seg_box(p0,p1,w,0.012,"iron",up=n).bm,(0.5,0.5,0.5))
            k+=step
def hp_copy(acc,key,part_bm,bev=0.012,seg=2,tint=None,R=None):
    b=part_bm.copy()
    for L in list(b.faces.layers.int): b.faces.layers.int.remove(L)
    if bev>0:
        b.normal_update(); es=[e for e in b.edges if len(e.link_faces)==2 and e.calc_face_angle(0)>math.radians(25)]
        if es: bmesh.ops.bevel(b,geom=es,offset=bev,segments=seg,affect='EDGES',clamp_overlap=True)
    acc.add(key,b,tint or ((R.random(),R.random(),R.random()) if R else (0.5,0.5,0.5)))
def gen_hp(acc,part,R,mod):
    """HP for one LP part. Hidden faces are skipped."""
    bm=part.bm; h=bm.faces.layers.int["hid"]; c=part.cls; dims=[b-a for a,b in zip(*part.bbox())]; mn=min(dims)
    vis=[f for f in bm.faces if not f[h]]
    if c in ("stone","stone_dk"):
        hp_copy(acc,"mortar",bm,0.0); arc=getattr(part,"arc",None); arc_done=False
        for f in vis:
            if f.calc_area()<0.02: continue
            if arc:
                off=getattr(part,"off",Vector()); cc=f.calc_center_median()-off; rd=Vector((cc.x,cc.y,0))
                if rd.length>arc[0]*0.85 and abs(f.normal.z)<0.3 and f.normal.dot(rd.normalized())>0.8:
                    if not arc_done:
                        a_,b_=part.bbox(); hp_arc(acc,c,arc[0],arc[1],arc[2],a_.z,b_.z,R,off); arc_done=True
                    continue
            if part.detail and f.calc_area()<0.6:   # small dressed stones (merlon caps, corbels, sills): one block per face
                o,u,v,n,poly=_frame(f); us=[p[0] for p in poly]; vs=[p[1] for p in poly]
                acc.add(c,_blk(o,u,v,n,(max(us)+min(us))/2,(max(vs)+min(vs))/2,max(us)-min(us)-0.03,max(vs)-min(vs)-0.03,-0.04,R.uniform(0.01,0.025),0,0,0.03,3),
                        (R.random(),R.random(),R.random()))
            else: hp_courses(acc,c,f,R)
    elif c=="roof":
        hp_copy(acc,"roof_under",bm,0.0)
        for f in vis:
            if f.calc_area()>0.03 and f.normal.z>0.12: hp_tiles(acc,f,R)
    elif c in ("wood","wood_dk","paint"):
        big=[f for f in vis if f.calc_area()>0.35 and mn<0.25]
        if big:
            hp_copy(acc,c+"_gap",bm,0.0)
            for f in big: hp_planks(acc,c,f,R)
            for f in vis:
                if f not in big: pass
        else: hp_copy(acc,c,bm,min(0.012,mn*0.2),2,R=R)
        if big:
            for f in big: hp_rivets(acc,f,R,step=0.3,inset=0.06,r=0.012)
    elif c=="iron":
        hp_copy(acc,"iron",bm,min(0.006,mn*0.2),2)
        for f in vis:
            if f.calc_area()>0.06 and mn>0.03: hp_rivets(acc,f,R,step=0.14,inset=0.035,r=0.012)
    elif c=="glass":
        hp_copy(acc,"glass",bm,0.0)
        for f in vis:
            if f.calc_area()>0.1: hp_lead(acc,f,R)
    elif c=="leaf":
        hp_copy(acc,"leaf",bm,0.0,R=R)
        a,b=part.bbox()
        for i in range(int(18*(b.x-a.x+0.2))):
            p=Vector((R.uniform(a.x,b.x),R.uniform(a.y,b.y),b.z-R.uniform(0,0.05)))
            acc.add("flower",hull_bm([p+Vector(q)*0.035 for q in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-.4)]]),(R.random(),R.random(),R.random()))
    elif c in ("cloth","cloth_red"):
        hp_copy(acc,c,bm,min(0.01,mn*0.3),2)
    else:
        hp_copy(acc,c,bm,min(0.01,mn*0.25),2)
# ------------------------------------------------------------------ HP materials (bake sources)
def _attr(nt):
    a=nt.nodes.new("ShaderNodeAttribute"); a.attribute_name="tint"; s=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(a.outputs["Color"],s.inputs[0]); return s
def _mixc(nt,fac,a,b,blend='MIX'):
    m=nt.nodes.new("ShaderNodeMix"); m.data_type='RGBA'; m.blend_type=blend
    if isinstance(fac,(int,float)): m.inputs[0].default_value=fac
    else: nt.links.new(fac,m.inputs[0])
    for i,v in ((6,a),(7,b)):
        if isinstance(v,tuple): m.inputs[i].default_value=tuple(v[:3])+(1,)
        else: nt.links.new(v,m.inputs[i])
    return m.outputs[2]
def _mr(nt,x,a,b,lo=0.0,hi=1.0):
    m=nt.nodes.new("ShaderNodeMapRange"); nt.links.new(x,m.inputs[0]); m.inputs[1].default_value=lo; m.inputs[2].default_value=hi; m.inputs[3].default_value=a; m.inputs[4].default_value=b; return m.outputs[0]
def _noise(nt,scale,detail=6,coord="Object"):
    tc=nt.nodes.new("ShaderNodeTexCoord"); n=nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value=scale; n.inputs["Detail"].default_value=detail
    nt.links.new(tc.outputs[coord],n.inputs["Vector"]); return n.outputs[0]
def _edge(nt,radius=0.012,lo=0.9):
    bv=nt.nodes.new("ShaderNodeBevel"); bv.inputs["Radius"].default_value=radius; bv.samples=8
    g=nt.nodes.new("ShaderNodeNewGeometry"); d=nt.nodes.new("ShaderNodeVectorMath"); d.operation='DOT_PRODUCT'
    nt.links.new(bv.outputs[0],d.inputs[0]); nt.links.new(g.outputs["Normal"],d.inputs[1])
    m=nt.nodes.new("ShaderNodeMapRange"); m.inputs[1].default_value=1.0; m.inputs[2].default_value=lo; m.clamp=True; nt.links.new(d.outputs["Value"],m.inputs[0]); return m.outputs[0]
def tinted(base,alt,lo=0.86,hi=1.1,edge=None,grime=0.12,nscale=7):
    def f(nt):
        s=_attr(nt); c=_mixc(nt,s.outputs[1],hp.lin(base),hp.lin(alt))
        k=_mr(nt,s.outputs[0],lo,hi); vm=nt.nodes.new("ShaderNodeVectorMath"); vm.operation='SCALE'; nt.links.new(c,vm.inputs[0]); nt.links.new(k,vm.inputs[3])
        c=vm.outputs[0]
        if grime: c=_mixc(nt,_mr(nt,_noise(nt,nscale),0,grime,0.4,0.75),c,tuple(x*0.55 for x in hp.lin(base)))
        if edge: c=_mixc(nt,_edge(nt,0.015,0.88),c,hp.lin(edge))
        return c
    return f
def wood_col(base,alt,edge=None):
    def f(nt):
        c=tinted(base,alt,0.85,1.1,edge,0.0)(nt)
        w=nt.nodes.new("ShaderNodeTexWave"); w.wave_type='BANDS'; w.inputs["Scale"].default_value=9; w.inputs["Distortion"].default_value=7; w.inputs["Detail"].default_value=4
        tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(0.12,1,0.12); nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],w.inputs[0])
        return _mixc(nt,0.28,c,w.outputs[0],'MULTIPLY') if False else _mixc(nt,_mr(nt,w.outputs[0],0,0.3),c,tuple(x*0.6 for x in hp.lin(base)))
    return f
def _gz(nt):
    a=nt.nodes.new("ShaderNodeAttribute"); a.attribute_name="gz"; s=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(a.outputs["Color"],s.inputs[0]); return s
def _math(nt,op,a,b):
    m=nt.nodes.new("ShaderNodeMath"); m.operation=op; m.use_clamp=True
    for i,v in ((0,a),(1,b)):
        if isinstance(v,(int,float)): m.inputs[i].default_value=v
        else: nt.links.new(v,m.inputs[i])
    return m.outputs[0]
def _masks(nt,moss=1.0,dirt=1.0):
    """(dirt, moss) masks: dirt/moss band at the base of ground-standing modules (gz.G=1, gz.R = height), moss patches on up-facing ledges"""
    g=_gz(nt); base=_math(nt,'MULTIPLY',_mr(nt,g.outputs[0],1,0,0.0,1.0),g.outputs[1])
    geo=nt.nodes.new("ShaderNodeNewGeometry"); sn=nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Normal"],sn.inputs[0])
    up=_mr(nt,sn.outputs[2],0,1,0.55,0.9)
    n1=_noise(nt,3.5,8); n2=_noise(nt,11,6)
    d=_math(nt,'MULTIPLY',_math(nt,'MULTIPLY',base,_mr(nt,n1,0.3,1.6,0.35,0.7)),dirt)
    mz=_math(nt,'MAXIMUM',_math(nt,'MULTIPLY',base,1.3),_math(nt,'MULTIPLY',up,0.55))
    mm=_math(nt,'MULTIPLY',_mr(nt,_math(nt,'MULTIPLY',mz,n2),0,1,0.22,0.42),moss)
    return d,mm
def weathered(colf,moss=1.0,dirt=1.0):
    def f(nt):
        c=colf(nt); d,mm=_masks(nt,moss,dirt)
        c=_mixc(nt,_math(nt,'MULTIPLY',d,0.6),c,hp.lin("#7A5E3E"))
        return _mixc(nt,mm,c,_mixc(nt,_noise(nt,30,4),hp.lin("#4F7F27"),hp.lin("#7FA83A")))
    return f
def rough_w(lo,hi,moss=1.0):
    def f(nt):
        s=_attr(nt); r=_mr(nt,s.outputs[2],lo,hi); d,mm=_masks(nt,moss,1.0)
        r=_mixc(nt,_noise(nt,18,5),(0,0,0),(0,0,0)) if False else r
        return _mr(nt,_math(nt,'MAXIMUM',mm,_math(nt,'MULTIPLY',d,0.7)),0,1,0,1) if False else _mixf(nt,_math(nt,'MAXIMUM',mm,_math(nt,'MULTIPLY',d,0.7)),r,0.95)
    return f
def _mixf(nt,fac,a,b):
    m=nt.nodes.new("ShaderNodeMix"); m.data_type='FLOAT'; nt.links.new(fac,m.inputs[0])
    if isinstance(a,(int,float)): m.inputs[2].default_value=a
    else: nt.links.new(a,m.inputs[2])
    if isinstance(b,(int,float)): m.inputs[3].default_value=b
    else: nt.links.new(b,m.inputs[3])
    return m.outputs[0]
def stone_col(base,alt,grey,edge):
    def f(nt):
        c=tinted(base,alt,0.74,1.16,None,0.18,6)(nt); s=_attr(nt)
        c=_mixc(nt,_mr(nt,s.outputs[1],0,1,0.82,0.86),c,hp.lin(grey))            # ~15% odd stones: cooler grey
        c=_mixc(nt,_mr(nt,s.outputs[2],0,1,0.1,0.06),c,tuple(x*0.72 for x in hp.lin(base)))   # ~6% dark stones
        c=_mixc(nt,_math(nt,'MULTIPLY',_edge(nt,0.018,0.85),0.55),c,hp.lin(edge))   # worn, sun-bleached edges (subtle)
        return c
    return f
def stone_bump(nt):
    b1=hp.noise_bump(nt,9,0.55,0.025)
    tc=nt.nodes.new("ShaderNodeTexCoord"); vo=nt.nodes.new("ShaderNodeTexVoronoi"); vo.inputs["Scale"].default_value=38; nt.links.new(tc.outputs["Object"],vo.inputs["Vector"])
    b=nt.nodes.new("ShaderNodeBump"); b.inputs["Strength"].default_value=0.35; b.inputs["Distance"].default_value=0.006; nt.links.new(vo.outputs["Distance"],b.inputs["Height"]); nt.links.new(b1,b.inputs["Normal"])
    return b.outputs[0]
def make_hp_mats():
    hp.ALL.clear(); M={}
    def mk(k,**kw): M[k]=hp.HPMat("HPM_"+k,**kw)
    mk("mortar",color=weathered(tinted("#8E7653","#826B4A",0.9,1.05,None,0.2,12),1.0),rough=rough_w(0.92,0.97,1.0),metal=0.0,bump=lambda nt:hp.noise_bump(nt,60,0.6,0.006))
    mk("stone",color=weathered(stone_col("#E3C28E","#EDD3A2","#CFC6B0","#FBEED2")),rough=rough_w(0.55,0.86),metal=0.0,bump=stone_bump)
    mk("stone_dk",color=weathered(stone_col("#D0AC78","#C4A06E","#BDB39C","#F0DCB4")),rough=rough_w(0.55,0.86),metal=0.0,bump=stone_bump)
    mk("roof",color=weathered(tinted("#C8452F","#E0663F",0.72,1.18,"#F59A6A",0.16),0.35,0.0),rough=rough_w(0.38,0.7,0.35),metal=0.0,bump=lambda nt:hp.noise_bump(nt,30,0.3,0.004))
    mk("roof_under",color=hp.lin("#6E2418"),rough=0.7,metal=0.0)
    for k,b,a,e in (("wood","#B07840","#C48A4E","#D9A869"),("wood_dk","#6A4326","#7A4E2C","#94653D"),("paint","#2F6FB5","#3A7FC8","#9CC2E6")):
        mk(k,color=weathered(wood_col(b,a,e),0.5,0.8),rough=rough_w(0.55,0.8,0.5) if k!="paint" else 0.5,metal=0.0,bump=lambda nt:hp.noise_bump(nt,40,0.25,0.002,"wave"))
        mk(k+"_gap",color=hp.lin("#3A2414"),rough=0.8,metal=0.0)
    mk("plaster",color=weathered(tinted("#F3E7CF","#EADBBE",0.9,1.04,None,0.16,4),0.4),rough=0.88,metal=0.0,bump=lambda nt:hp.noise_bump(nt,25,0.25,0.004))
    mk("iron",color=tinted("#454B56","#3A3F48",0.9,1.1,"#9AA3B0",0.0),rough=0.42,metal=1.0,bump=lambda nt:hp.noise_bump(nt,120,0.15,0.0008))
    mk("gold",color=tinted("#E8B53A","#F4C95A",0.95,1.05,"#FFE7A0",0.0),rough=0.28,metal=1.0)
    mk("cloth",color=tinted("#2E5BD6","#3566E0",0.92,1.05,None,0.06,3),rough=0.82,metal=0.0,bump=lambda nt:hp.noise_bump(nt,6,0.3,0.01,"wave"))
    mk("cloth_red",color=tinted("#D8342C","#E4453A",0.92,1.05,None,0.06,3),rough=0.82,metal=0.0,bump=lambda nt:hp.noise_bump(nt,6,0.3,0.01,"wave"))
    def glass(nt):
        tc=nt.nodes.new("ShaderNodeTexCoord"); w=nt.nodes.new("ShaderNodeTexWave"); w.inputs["Scale"].default_value=1.2; w.bands_direction='DIAGONAL'
        nt.links.new(tc.outputs["Object"],w.inputs[0]); return _mixc(nt,_mr(nt,w.outputs[0],0,0.55,0.78,0.95),hp.lin("#1D3558"),hp.lin("#9FD0FF"))
    mk("glass",color=glass,rough=0.08,metal=0.0)
    mk("leaf",color=tinted("#4CA83A","#6CC64F",0.8,1.1,None,0.1,9),rough=0.8,metal=0.0,bump=lambda nt:hp.noise_bump(nt,40,0.5,0.01))
    mk("flower",color=tinted("#FF5D8F","#FFD23F",0.95,1.05,None,0.0),rough=0.6,metal=0.0)
    mk("lamp",color=tinted("#FFE3A0","#FFD27A",0.95,1.05,None,0.0),rough=0.2,metal=0.0)
    return M
def write_inner(texdir,B):
    os.makedirs(texdir,exist_ok=True); spec=json.dumps({c:INNER[c] for c in CLS}); order=json.dumps(CLS)
    subprocess.check_call(["/usr/bin/python3","-c",f"""
import json,random
from PIL import Image,ImageFilter
import numpy as np
S=json.loads({spec!r}); O=json.loads({order!r}); N=512; G=4; c=N//G
A=np.zeros((N,N,3),np.float32); ORM=np.zeros((N,N,3),np.uint8); H=np.zeros((N,N),np.float32)
rng=np.random.default_rng(5)
def hx(h): return np.array([int(h[i:i+2],16) for i in (1,3,5)],np.float32)
for i,k in enumerate(O):
    a,b,r,m=S[k]; x,y=(i%G)*c,(i//G)*c
    n=rng.random((c//8,c//8)); n=np.kron(n,np.ones((8,8))); n=(n+rng.random((c,c))*0.6)/1.6
    A[y:y+c,x:x+c]=hx(a)[None,None,:]*(1-n[...,None]*0.45)+hx(b)[None,None,:]*(n[...,None]*0.45)
    H[y:y+c,x:x+c]=n; ORM[y:y+c,x:x+c]=(255,int(r*255),int(m*255))
Image.fromarray(A.clip(0,255).astype(np.uint8)).save('{texdir}/T_{B}_Inner_A.png')
Hs=np.asarray(Image.fromarray((H*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5)),np.float32)/255
gy,gx=np.gradient(Hs); nx=-gx*6; ny=gy*6; nz=np.ones_like(nx); l=np.sqrt(nx*nx+ny*ny+nz*nz)
Nn=np.stack([nx/l,ny/l,nz/l],-1)*0.5+0.5; Image.fromarray((Nn*255).astype(np.uint8)).save('{texdir}/T_{B}_Inner_N.png')
Image.fromarray(ORM).save('{texdir}/T_{B}_Inner_ORM.png')
"""])
def inner_uv_rect(cls):
    i=CI[cls]; G=4; x,y=(i%G)/G,(i//G)/G; return x+0.01,1-y-0.25+0.01,0.23   # (u0,v0,size) image y flipped
# ------------------------------------------------------------------ build a building set
def smooth_by_angle(me,deg=35):
    for p in me.polygons: p.use_smooth=True
    me.set_sharp_from_angle(angle=math.radians(deg))
def shrink_down_uv(me,uvname="UV0",k=0.3):
    uv=me.uv_layers[uvname]
    for p in me.polygons:
        if p.normal.z<-0.8:
            cx=sum(uv.data[li].uv.x for li in p.loop_indices)/p.loop_total; cy=sum(uv.data[li].uv.y for li in p.loop_indices)/p.loop_total
            for li in p.loop_indices: uv.data[li].uv=(cx+(uv.data[li].uv.x-cx)*k,cy+(uv.data[li].uv.y-cy)*k)
def build_set(B,modules,size=2048,seed=11,only_fracture=None,ao_samples=64):
    """B = building key (Keep, Tower, Gate, House). Writes BuildingKit/Kit_<B>/ (FBX + textures) and returns stats."""
    CV.reset(); R=random.Random(seed)
    outdir=f"{ROOT}/{KITP}/Kit_{B}"; texdir=f"{outdir}/textures"; glbdir=f"{ROOT}/models/BuildingKit/Kit_{B}/source"
    os.makedirs(texdir,exist_ok=True); os.makedirs(glbdir,exist_ok=True)
    fid=0
    for mi,m in enumerate(modules):
        mark_hidden(m)
        for p in m.parts:
            L=p.bm.faces.layers.int
            for f in p.bm.faces: f[L["fid"]]=fid; f[L["mod"]]=mi; fid+=1
    # ---- bake LP (all unique modules side by side) + HP
    lp=bmesh.new(); ensure_layers(lp); acc=HPAcc(); x=0.0; OFF=[]
    for m in modules:
        a=Vector((1e9,)*3); b=Vector((-1e9,)*3)
        for p in m.parts:
            pa,pb=p.bbox(); a=Vector([min(a[i],pa[i]) for i in range(3)]); b=Vector([max(b[i],pb[i]) for i in range(3)])
        off=Vector((x-a.x,0,0)); OFF.append(off); x+=(b.x-a.x)+3.0; m.aabb=(a,b)
        for p in m.parts:
            vc=visible_copy(p); bmesh.ops.translate(vc,verts=vc.verts,vec=off); bm_merge(lp,vc); vc.free()
            acc.ground=1.0 if m.anchor_to=="Ground" else 0.0; q=p.copy(); q.off=off.copy(); q.bm.transform(Matrix.Translation(off)); q.bm.normal_update(); gen_hp(acc,q,R,m); q.bm.free()
    me=bpy.data.meshes.new("BAKE_LP"); lp.to_mesh(me); lp.free()
    if me.uv_layers: me.uv_layers[0].name="UV0"
    else: me.uv_layers.new(name="UV0")
    lpo=bpy.data.objects.new("BAKE_LP",me); bpy.context.scene.collection.objects.link(lpo)
    smooth_by_angle(me)
    bpy.ops.object.select_all(action='DESELECT'); lpo.select_set(True); bpy.context.view_layer.objects.active=lpo
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(50),island_margin=0.002,scale_to_bounds=False)
    bpy.ops.uv.select_all(action='SELECT'); bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode='OBJECT'); shrink_down_uv(me)
    if any(getattr(m,"interior",None) for m in modules):   # inward faces of hollow walls/rings: low texel (seen only through breaches)
        bmu=bmesh.new(); bmu.from_mesh(me); ul=bmu.loops.layers.uv["UV0"]; mlay=bmu.faces.layers.int["mod"]; nin=0
        for f in bmu.faces:
            m=modules[f[mlay]]; mode=getattr(m,"interior",None)
            if not mode: continue
            c=f.calc_center_median()-OFF[f[mlay]]; n=f.normal
            if mode=="y+": hit=n.y>0.8 and c.y>m.aabb[1].y-0.05
            else:
                r=Vector((c.x,c.y,0)); hit=abs(n.z)<0.3 and r.length>1e-3 and n.dot(r.normalized())<-0.6
            if not hit: continue
            cu=sum((l[ul].uv for l in f.loops),Vector((0,0)))/len(f.loops); nin+=1
            for l in f.loops: l[ul].uv=cu+(l[ul].uv-cu)*0.4
        bmu.to_mesh(me); bmu.free(); print(f"[{B}] interior faces shrunk: {nin}",flush=True)
    low={i for i,m in enumerate(modules) if getattr(m,"uvscale",1.0)!=1.0}
    if low:
        bmu=bmesh.new(); bmu.from_mesh(me); ul=bmu.loops.layers.uv["UV0"]; mlay=bmu.faces.layers.int["mod"]
        for f in bmu.faces:
            k=getattr(modules[f[mlay]],"uvscale",1.0)
            if k==1.0: continue
            c=sum((l[ul].uv for l in f.loops),Vector((0,0)))/len(f.loops)
            for l in f.loops: l[ul].uv=c+(l[ul].uv-c)*k
        bmu.to_mesh(me); bmu.free()
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(margin=0.0015,rotate=True,shape_method='CONCAVE'); bpy.ops.object.mode_set(mode='OBJECT')
    HM=make_hp_mats(); hpo=acc.objects(HM)
    print(f"[{B}] LP bake tris={sum(len(p.vertices)-2 for p in me.polygons)} HP faces={sum(len(o.data.polygons) for o in hpo)}",flush=True)
    paths=hp.bake_maps(lpo,hpo,size,B,texdir,extrusion=0.06,ray=0.115,emissive=False,ao_samples=ao_samples,ao_dist=0.4)
    write_inner(texdir,B)
    # texel density
    bmt=bmesh.new(); bmt.from_mesh(me); uvl=bmt.loops.layers.uv["UV0"]; A3=AU=0
    for f in bmt.faces:
        if f.normal.z<-0.8: continue
        A3+=f.calc_area(); pts=[l[uvl].uv for l in f.loops]; AU+=abs(sum(pts[i].x*pts[i-1].y-pts[i-1].x*pts[i].y for i in range(len(pts)))/2)
    texel=math.sqrt(AU*size*size/max(A3,1e-6))
    # uv per fid: list of (local co, uv)
    UVM={}; fl=bmt.faces.layers.int["fid"]; ml=bmt.faces.layers.int["mod"]
    for f in bmt.faces:
        o=OFF[f[ml]]; UVM[f[fl]]=[(l.vert.co-o,l[uvl].uv.copy()) for l in f.loops]
    bmt.free()
    for o in hpo: bpy.data.meshes.remove(o.data)
    bpy.data.meshes.remove(me)
    stats={"building":B,"texel_px_per_m":round(texel,1),"atlas":size,"modules":{}}
    for m in modules:
        if only_fracture and m.short not in only_fracture: continue
        st=export_module(m,UVM,texdir,outdir,glbdir,R); stats["modules"][m.short]=st
        print(f"BUILT {m.name} LOD0={st['LOD0']} LOD1={st['LOD1']} LOD2={st['LOD2']} frags={st['frags']} frag_tris_max={st['frag_max']} frac_total={st['Fractured']} mass={st['mass']} ucx={st['ucx']} cutbev={st.get('cut_edges_bevelled')}",flush=True)
    os.makedirs(f"{ROOT}/models/HeroBuildings",exist_ok=True)
    json.dump(stats,open(f"{ROOT}/models/HeroBuildings/Kit_{B}_stats.json","w"),indent=1)
    print(f"[{B}] texel {texel:.0f} px/m (atlas {size}); bury applied {BURY[True]} / reverted {BURY[False]} fragments",flush=True)
    return stats
def apply_uv(bm,UVM):
    uv=bm.loops.layers.uv.verify(); fl=bm.faces.layers.int["fid"]
    for f in bm.faces:
        ent=UVM.get(f[fl])
        if not ent: continue
        for l in f.loops:
            l[uv].uv=min(ent,key=lambda e:(e[0]-l.vert.co).length_squared)[1]
def inner_uv(bm,f):
    uv=bm.loops.layers.uv.verify(); c=CLS[f[bm.faces.layers.int["cls"]]]; u0,v0,s=inner_uv_rect(c)
    n=f.normal; t=n.orthogonal().normalized(); b=n.cross(t); pts=[(l.vert.co.dot(t),l.vert.co.dot(b)) for l in f.loops]
    mu=min(p[0] for p in pts); mv=min(p[1] for p in pts); sc=max(max(p[0] for p in pts)-mu,max(p[1] for p in pts)-mv,0.5)
    for l,p in zip(f.loops,pts): l[uv].uv=(u0+s*(p[0]-mu)/sc,v0+s*(p[1]-mv)/sc)
def mats(B,texdir):
    M=CV.material_pbr(f"M_{B}",f"{texdir}/T_{B}_A.png",f"{texdir}/T_{B}_N.png",f"{texdir}/T_{B}_ORM.png")
    MI=CV.material_pbr(f"M_{B}_Inner",f"{texdir}/T_{B}_Inner_A.png",f"{texdir}/T_{B}_Inner_N.png",f"{texdir}/T_{B}_Inner_ORM.png",rough=0.8)
    return M,MI
def obj_from_bm(name,bm,mlist,parent=None,loc=(0,0,0)):
    for L in list(bm.faces.layers.int): bm.faces.layers.int.remove(L)
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    if me.uv_layers: me.uv_layers[0].name="UV0"
    for m in mlist: me.materials.append(m)
    ob=bpy.data.objects.new(name,me); ob.location=loc; bpy.context.scene.collection.objects.link(ob)
    if parent: ob.parent=parent
    return ob
def hull_obj(name,points,parent,loc=(0,0,0),maxtri=255):
    pts=[Vector(p) for p in points]; g=0.0
    while True:
        q=pts if g==0 else list({(round(p.x/g)*g,round(p.y/g)*g,round(p.z/g)*g) for p in pts})
        bm=hull_bm(q); bmesh.ops.triangulate(bm,faces=bm.faces[:])
        if len(bm.faces)<=maxtri or g>0.6: break
        bm.free(); g=0.04 if g==0 else g*1.5
    ob=obj_from_bm(name,bm,[],parent,loc); ob.display_type='WIRE'; return ob
def decimate_copy(src,name,ratio,parent):
    ob=src.copy(); ob.data=src.data.copy(); ob.name=name; ob.data.name=name; bpy.context.scene.collection.objects.link(ob); ob.parent=parent
    md=ob.modifiers.new("d",'DECIMATE'); md.ratio=ratio; md.use_collapse_triangulate=False
    bpy.context.view_layer.objects.active=ob; bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier="d"); smooth_by_angle(ob.data); return ob
def tris_ob(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
def export_module(m,UVM,texdir,outdir,glbdir,R):
    CV.reset(); M,MI=mats(m.B,texdir); name=m.name
    grp=CV.empty(name,(0,0,0))
    lod0=bmesh.new(); ensure_layers(lod0)
    for p in m.parts:
        vc=visible_copy(p); bm_merge(lod0,vc); vc.free()
    apply_uv(lod0,UVM); o0=obj_from_bm(f"{name}_LOD0",lod0,[M],grp); smooth_by_angle(o0.data); CV.add_vcol(o0); o0["static"]=True
    t0=tris_ob(o0)
    # LOD1: drop tiny details (<1.2 L) then collapse; LOD2: structure + big details, stronger collapse
    def lodbm(minvol):
        b=bmesh.new(); ensure_layers(b)
        for p in m.parts:
            if p.detail and p.vol()<minvol: continue
            vc=visible_copy(p); bm_merge(b,vc); vc.free()
        apply_uv(b,UVM); return b
    o1=obj_from_bm(f"{name}_LOD1_src",lodbm(0.0012),[M],grp); l1=decimate_copy(o1,f"{name}_LOD1",min(1.0,0.5*t0/max(1,tris_ob(o1))),grp); bpy.data.objects.remove(o1)
    o2=obj_from_bm(f"{name}_LOD2_src",lodbm(0.02),[M],grp); l2=decimate_copy(o2,f"{name}_LOD2",min(1.0,0.2*t0/max(1,tris_ob(o2))),grp); bpy.data.objects.remove(o2)
    for o in (l1,l2): CV.add_vcol(o); o["static"]=True
    # intact hulls: cluster structural parts into <=8 groups along the longest axes
    st=[p for p in m.parts if not p.detail] or m.parts
    groups=cluster([p.centre() for p in st],min(8,len(st)))
    nh=0
    for gi,g in enumerate(groups):
        pts=[v.co.copy() for i in g for v in st[i].bm.verts]
        # attach details to nearest group for coverage of protruding merlons etc.
        if not pts: continue
        nh+=1; hull_obj(f"UCX_{name}_LOD0_{nh:02d}",pts,grp)
    CV.add_lightmap_uv([o0,l1,l2])
    CV.save_src(f"Kit_{m.B}",name)
    CV.export(f"{outdir}/{name}.fbx",f"{glbdir}/{name}.glb")
    stats=dict(LOD0=t0,LOD1=tris_ob(l1),LOD2=tris_ob(l2),ucx=nh,aabb=[list(m.aabb[0]),list(m.aabb[1])],frac=m.frac,anchor=m.anchor,anchor_to=m.anchor_to,note=m.note)
    stats.update(fracture(m,UVM,texdir,outdir,glbdir,R))
    return stats
def cluster(cs,k):
    if k<=1 or len(cs)<=1: return [list(range(len(cs)))]
    rnd=random.Random(3); cen=[cs[i] for i in rnd.sample(range(len(cs)),k)]
    for _ in range(25):
        g=[[] for _ in cen]
        for i,c in enumerate(cs): g[min(range(len(cen)),key=lambda j:(c-cen[j]).length_squared)].append(i)
        cen=[sum((cs[i] for i in gg),Vector())/len(gg) if gg else cen[j] for j,gg in enumerate(g)]
    return [gg for gg in g if gg]
# ------------------------------------------------------------------ fracture
def clip_part(bm,seeds,i,cand):
    s=seeds[i]; cut=bm.faces.layers.int["cut"]; hid=bm.faces.layers.int["hid"]; cl=bm.faces.layers.int["cls"]; ci=next(iter(bm.faces))[cl]
    for j in cand:
        o=seeds[j]; mid=(s+o)/2; n=(o-s).normalized()
        d=[(v.co-mid).dot(n) for v in bm.verts]
        if max(d)<=1e-6: continue
        if min(d)>=-1e-6: return False
        r=bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=mid,plane_no=n,clear_outer=True,dist=1e-6)
        if len(bm.verts)<4: return False
        ce=[e for e in r['geom_cut'] if isinstance(e,bmesh.types.BMEdge)]
        if len(ce)>=3:
            nf=bmesh.ops.contextual_create(bm,geom=ce)['faces']
            for f in nf: f[cut]=j+1; f[hid]=2; f[cl]=ci
    bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); bm.normal_update()
    return len(bm.faces)>=4 and abs(bm.calc_volume())>1e-6
def sample_seeds(parts,k,rnd,dmin):
    vols=[p.vol() for p in parts]; Vt=sum(vols); S=[]; tries=0
    pls=[_planes(p) for p in parts]; bbs=[p.bbox() for p in parts]
    while len(S)<k and tries<6000:
        tries+=1; r=rnd.random()*Vt; i=0
        while r>vols[i] and i<len(vols)-1: r-=vols[i]; i+=1
        a,b=bbs[i]; q=Vector([rnd.uniform(a[t],b[t]) for t in range(3)])
        if not all((q-pp).dot(n)<=-0.02 for n,pp in pls[i]): continue
        if all((q-s).length>=dmin for s in S): S.append(q)
    return S
def fracture(m,UVM,texdir,outdir,glbdir,R):
    lo,hi=RNG[m.frac]; name=m.name; MINAX=0.15
    struct=[p for p in m.parts if not p.detail]; det=[p for p in m.parts if p.detail]
    Vt=sum(p.vol() for p in struct); a,b=m.aabb; ext=b-a
    best=None; dropped=[0]; k=int(round((lo+hi)/2)); fails={"tris":0,"total":0}
    for attempt in range(240):
        dropped[0]=0
        rnd=random.Random(zlib.crc32(name.encode())%997+attempt*13)
        k=max(lo,min(hi,k))
        dmin=max(0.25,(Vt/k)**(1/3)*0.6*(0.97**(attempt%40)))
        seeds=sample_seeds(struct,k,rnd,dmin)
        if len(seeds)<lo: continue
        frs=[]
        for i in range(len(seeds)):
            pcs=[]
            cand=sorted([j for j in range(len(seeds)) if j!=i],key=lambda j:(seeds[j]-seeds[i]).length)
            for p in struct:
                q=p.bm.copy(); apply_uv(q,UVM)
                if clip_part(q,seeds,i,cand): pcs.append([q,False,p.vol(),abs(_vol_full(q)),p.cls])
                else: q.free()
            frs.append(pcs)
        for p in det:   # details stay whole, with the cell containing their centre
            c=p.centre(); i=min(range(len(seeds)),key=lambda j:(seeds[j]-c).length_squared)
            q=p.bm.copy(); apply_uv(q,UVM); frs[i].append([q,True,p.vol(),p.vol(),p.cls])
        frs=[(pcs,i) for i,pcs in enumerate(frs) if pcs and any(not x[1] for x in pcs)]
        for pcs,_ in frs: bury_safe(pcs)
        # budget: drop the smallest trims (bolts, straps, studs) from over-budget fragments, never structure
        def ft(pcs): return sum(tri_count(x[0]) for x in pcs)
        def total(): return sum(ft(pcs) for pcs,_ in frs)
        for pcs,_ in frs:
            while ft(pcs)>=300:
                sm=[x for x in pcs if x[1] and x[2]<0.01]
                if not sm: break
                x=min(sm,key=lambda x:x[2]); pcs.remove(x); x[0].free(); dropped[0]+=1
        while total()>1500:
            sm=[(x,pcs) for pcs,_ in frs for x in pcs if x[1] and x[2]<0.004]
            if not sm: break
            x,pcs=min(sm,key=lambda t:t[0][2]); pcs.remove(x); x[0].free(); dropped[0]+=1
        ok=lo<=len(frs)<=hi; why=""
        tot=0; tmax=0; merged=[]
        for pcs,si in frs:
            fb=bmesh.new(); ensure_layers(fb); mass=0.0
            for x in pcs:
                mass+=x[3]*DENS[x[4]]
                if len(x[0].faces): bm_merge(fb,x[0])
                x[0].free()
            merged.append((fb,si,mass))
            t=tri_count(fb); tot+=t; tmax=max(tmax,t)
            vs=[v.co for v in fb.verts]; e=min(max(v[t_]for v in vs)-min(v[t_] for v in vs) for t_ in range(3))
            if t>=300: ok=False; why=f"frag tris {t}"
            if e<MINAX-1e-4: ok=False; why=f"min axis {e:.3f}"
            if mass<0.5: ok=False; why=f"mass {mass:.2f}"
        frs=merged
        if tot>1500: ok=False; why=f"total {tot}"
        if not lo<=len(frs)<=hi: why=f"count {len(frs)}"
        if ok: best=(frs,seeds); break
        for fb,_,_ in frs: fb.free()
        if why.startswith("frag tris"):
            fails["tris"]+=1
            if fails["tris"]%6==0: k+=1
        elif why.startswith("total"):
            fails["total"]+=1
            if fails["total"]%6==0: k-=1
        if attempt%20==19: print(f"   [{name}] attempt {attempt+1}: k={k} {why}",flush=True)
    if not best: raise RuntimeError(f"fracture failed for {name}")
    frs,seeds=best
    # chipped cut edges: bevel the rim where a cut face meets the original surface, smallest fragments first, inside budgets
    budget=1500-sum(tri_count(fb) for fb,_,_ in frs); nbev=0
    for i in sorted(range(len(frs)),key=lambda i:tri_count(frs[i][0])):
        fb,si,mass=frs[i]; t0=tri_count(fb); c=fb.copy(); hid=c.faces.layers.int["hid"]; cut=c.faces.layers.int["cut"]
        es=[e for e in c.edges if len(e.link_faces)==2 and (e.link_faces[0][cut]>0)!=(e.link_faces[1][cut]>0) and e.calc_face_angle(0)>math.radians(30)]
        if not es: c.free(); continue
        try: r=bmesh.ops.bevel(c,geom=es,offset=0.03,segments=1,affect='EDGES',clamp_overlap=True)
        except Exception: c.free(); continue
        for f in r["faces"]: f[hid]=2; f[cut]=0
        t1=tri_count(c)
        if t1<300 and t1-t0<=budget: budget-=t1-t0; fb.free(); frs[i]=(c,si,mass); nbev+=1
        else: c.free()
    m.cut_bevel=(nbev,len(frs))
    CV.reset(); M,MI=mats(m.B,texdir); root=CV.empty(name+"_Fractured"); root["fracture_class"]=m.frac; frags=[]; cutinfo=[]
    for n_,(fb,si,mass) in enumerate(frs):
        fn=f"{name}_frag_{n_+1:02d}"
        hid=fb.faces.layers.int["hid"]; cut=fb.faces.layers.int["cut"]
        for f in fb.faces:
            if f[hid]: f.material_index=1; inner_uv(fb,f)
            else: f.material_index=0
        mass=round(mass,2)
        com=_com(fb); faces_on={}
        for f in fb.faces:
            if f[cut]: faces_on.setdefault(f[cut]-1,[]).append((f.calc_center_median(),f.calc_area()))
        cutinfo.append((si,faces_on))
        pts=[v.co-com for v in fb.verts]
        bmesh.ops.translate(fb,verts=fb.verts,vec=-com)
        ob=obj_from_bm(fn,fb,[M,MI],root,com); smooth_by_angle(ob.data); CV.add_vcol(ob)
        ob["mass_kg"]=mass; ob["density_kg_m3"]=round(mass/max(_vol_ob(ob),1e-6),1)
        hull_obj("UCX_"+fn,pts,root,com); frags.append(ob)
    bpy.context.view_layer.update()
    nj=0; idx={si:k for k,(si,_) in enumerate(cutinfo)}
    for k,(si,fo) in enumerate(cutinfo):
        for sj,lst in fo.items():
            if sj not in idx or idx[sj]<=k: continue
            other=cutinfo[idx[sj]][1].get(si)
            if not other: continue
            A=sum(a for _,a in lst)
            if A<0.004: continue
            p=sum((c*a for c,a in lst),Vector())/A
            e=CV.empty(f"JNT_{frags[k].name}__{frags[idx[sj]].name}",p,root,0.1); e["break_force_N"]=""; e["break_torque_Nm"]=""; nj+=1
    W={o.name:[o.matrix_world@v.co for v in o.data.vertices] for o in frags}
    allp=[w for ws in W.values() for w in ws]; z0=min(p.z for p in allp); z1=max(p.z for p in allp)
    X0,X1=min(p.x for p in allp),max(p.x for p in allp); Y0,Y1=min(p.y for p in allp),max(p.y for p in allp)
    na=0
    for o in frags:
        ws=W[o.name]
        if m.anchor=="bottom": sel=[w for w in ws if w.z<z0+0.02]
        elif m.anchor=="top": sel=[w for w in ws if w.z>z1-0.02]
        else: sel=[w for w in ws if min(w.x-X0,X1-w.x,w.y-Y0,Y1-w.y)<0.25]
        if sel:
            c=sum(sel,Vector())/len(sel); e=CV.empty("ANCHOR_"+o.name,c,root,0.15); e["anchor_to"]=m.anchor_to; na+=1
    CV.export(f"{outdir}/{name}_Fractured.fbx",f"{glbdir}/{name}_Fractured.glb")
    CV.save_src(f"Kit_{m.B}",name+"_Fractured")
    ft=[tris_ob(o) for o in frags]; ms=[o["mass_kg"] for o in frags]
    return dict(frags=len(frags),frag_max=max(ft),Fractured=sum(ft),mass=f"{min(ms):.1f}-{max(ms):.1f}",joints=nj,anchors=na,trims_dropped=dropped[0],cut_edges_bevelled=f"{m.cut_bevel[0]}/{m.cut_bevel[1]}")
def _vol_full(bm):
    V=0.0
    for f in bm.faces:
        vs=[v.co for v in f.verts]
        for t in range(1,len(vs)-1): V+=vs[0].dot(vs[t].cross(vs[t+1]))/6
    return V
def _com_many(bms):
    acc=Vector(); V=0
    for bm in bms:
        for f in bm.faces:
            vs=[v.co for v in f.verts]
            for t in range(1,len(vs)-1):
                v6=vs[0].dot(vs[t].cross(vs[t+1])); acc+=v6*(vs[0]+vs[t]+vs[t+1])/4; V+=v6
    return (acc/V if abs(V)>1e-12 else None),V/6
def bury_safe(pcs):
    """bury(), but keep the closed pieces when deleting buried faces would leave a shell whose divergence-theorem
    volume/COM (what the #28 validator and Unity's mesh-based inertia see) drifts from the closed-piece truth"""
    orig=[x[0].copy() for x in pcs]; c0,V0=_com_many(orig)
    bury(pcs); c1,V1=_com_many([x[0] for x in pcs])
    vs=[v.co for b in orig for v in b.verts]; ext=max(max(v[k] for v in vs)-min(v[k] for v in vs) for k in range(3))
    good=c0 is not None and c1 is not None and abs(V1)>0.6*abs(V0) and (c1-c0).length<0.4*max(0.05,0.1*ext)
    if good:
        for b in orig: b.free()
    else:
        for x,b in zip(pcs,orig): x[0].free(); x[0]=b
    BURY[good]+=1
BURY={True:0,False:0}
def bury(pcs):
    """inside one fragment, faces lying inside-or-on another piece of the same fragment stay buried: delete them
    (mass/volume are taken before this from the closed pieces)"""
    pl=[[(f.normal.copy(),f.verts[0].co.copy()) for f in x[0].faces] for x in pcs]
    bb=[(Vector([min(v.co[k] for v in x[0].verts) for k in range(3)]),Vector([max(v.co[k] for v in x[0].verts) for k in range(3)])) for x in pcs]
    for i,x in enumerate(pcs):
        q=x[0]
        dead=[]
        for f in q.faces:
            pts=[v.co for v in f.verts]+[f.calc_center_median()]
            for j in range(len(pcs)):
                if j==i: continue
                a,b=bb[j]
                if any(pt[k]<a[k]-1e-3 or pt[k]>b[k]+1e-3 for pt in pts for k in range(3)): continue
                if all(all((pt-pp).dot(n)<=2e-4 for n,pp in pl[j]) for pt in pts): dead.append(f); break
        if dead:
            bmesh.ops.delete(q,geom=dead,context='FACES_ONLY')
            lv=[v for v in q.verts if not v.link_faces]
            if lv: bmesh.ops.delete(q,geom=lv,context='VERTS')
def _classes(bm):
    c=bm.faces.layers.int["cls"]; return sorted({f[c] for f in bm.faces})
def _vol_cls(bm,ci):
    """volume of the closed shells whose faces carry class ci (shells are per part, one class each)"""
    c=bm.faces.layers.int["cls"]; V=0.0
    for f in bm.faces:
        if f[c]!=ci: continue
        vs=[v.co for v in f.verts]
        for t in range(1,len(vs)-1): V+=vs[0].dot(vs[t].cross(vs[t+1]))/6
    return V
def _com(bm):
    acc=Vector(); V=0
    for f in bm.faces:
        vs=[v.co for v in f.verts]
        for t in range(1,len(vs)-1):
            v6=vs[0].dot(vs[t].cross(vs[t+1])); acc+=v6*(vs[0]+vs[t]+vs[t+1])/4; V+=v6
    return acc/V if abs(V)>1e-12 else sum((v.co for v in bm.verts),Vector())/len(bm.verts)
def _vol_ob(o):
    V=0
    for p in o.data.polygons:
        vs=[o.data.vertices[i].co for i in p.vertices]
        for t in range(1,len(vs)-1): V+=vs[0].dot(vs[t].cross(vs[t+1]))/6
    return abs(V)

# ------------------------------------------------------------------ layout JSON (format crashverse.building_layout v1, see models/Buildings/LAYOUT_FORMAT.md)
CU=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))        # Blender (Z up) -> Unity (Y up, left-handed), as the FBX export
def r4(v): return round(v+0.0,4)
class Layout:
    def __init__(s,name,origin=(0,0,0),rot=0): s.name=name; s.M=T(*origin)@Rz(rot); s.P=[]
    def add(s,iid,B,mod,M,on="Ground"):
        s.P.append(dict(id=iid,B=B,mod=mod,L=M,on=on)); return iid
def load_stats(B): return json.load(open(f"{ROOT}/models/HeroBuildings/Kit_{B}_stats.json"))["modules"]
def write_layout(L,outdir=f"{ROOT}/Assets/_Project/Models/Buildings"):
    os.makedirs(outdir,exist_ok=True); ST={}; inst=[]; box={}; tot={"LOD0":0,"LOD1":0,"LOD2":0,"Fractured":0}
    for p in L.P:
        if p["B"] not in ST: ST[p["B"]]=load_stats(p["B"])
        st=ST[p["B"]][p["mod"]]; k=f"Kit_{p['B']}_{p['mod']}"
        a,b=Vector(st["aabb"][0]),Vector(st["aabb"][1]); cs=[Vector((x,y,z)) for x in (a.x,b.x) for y in (a.y,b.y) for z in (a.z,b.z)]
        w=[p["L"]@c for c in cs]; box[p["id"]]=(Vector([min(q[i] for q in w) for i in range(3)]),Vector([max(q[i] for q in w) for i in range(3)]))
        for t in tot: tot[t]+=st[t]
        R3=p["L"].to_3x3(); Ru=CU@R3@CU.inverted(); q=Ru.to_quaternion(); e=Ru.to_euler('ZXY'); t=CU@p["L"].translation
        inst.append(dict(id=p["id"],kit=k,intact=f"{KITP}/Kit_{p['B']}/{k}.fbx",fractured=f"{KITP}/Kit_{p['B']}/{k}_Fractured.fbx",
            position={"x":r4(t.x),"y":r4(t.y),"z":r4(t.z)},rotation={"x":r4(q.x),"y":r4(q.y),"z":r4(q.z),"w":r4(q.w)},
            euler_zxy_deg={"x":r4(math.degrees(e.x)),"y":r4(math.degrees(e.y)),"z":r4(math.degrees(e.z))},
            anchor_to=p["on"] if p["on"]=="Ground" else list(p["on"]),fracture_class=st["frac"],
            tris=dict(LOD0=st["LOD0"],LOD1=st["LOD1"],LOD2=st["LOD2"],Fractured=st["Fractured"])))
    ids={i["id"] for i in inst}
    con=[]; ks=list(box)
    for x in range(len(ks)):
        for y in range(x+1,len(ks)):
            (a0,a1),(b0,b1)=box[ks[x]],box[ks[y]]
            if all(a0[i]<=b1[i]+0.03 and b0[i]<=a1[i]+0.03 for i in range(3)):
                pt=CU@Vector([(max(a0[i],b0[i])+min(a1[i],b1[i]))/2 for i in range(3)])
                con.append(dict(a=ks[x],b=ks[y],point={"x":r4(pt.x),"y":r4(pt.y),"z":r4(pt.z)},break_force_N="",break_torque_Nm=""))
    cs={(c["a"],c["b"]) for c in con}|{(c["b"],c["a"]) for c in con}; miss=[]
    for i in inst:
        if i["anchor_to"]=="Ground": continue
        assert all(a in ids for a in i["anchor_to"]),(L.name,i["id"],i["anchor_to"])
        i["anchor_to"]=[s_ for s_ in i["anchor_to"] if (i["id"],s_) in cs]
        if not i["anchor_to"]: miss.append(i["id"])
    t=CU@L.M.translation; q=(CU@L.M.to_3x3()@CU.inverted()).to_quaternion()
    J=dict(format="crashverse.building_layout",version=1,building=L.name,units="m",
           space="Unity (Y up, left-handed); positions/rotations local to the building root",
           root={"position":{"x":r4(t.x),"y":r4(t.y),"z":r4(t.z)},"rotation":{"x":r4(q.x),"y":r4(q.y),"z":r4(q.z),"w":r4(q.w)},"note":"suggested placement on Island_Sunnyville"},
           tris_total=tot,piece_count=len(inst),pieces=inst,connections=con,unsupported_contacts=miss,
           anchor_note="fragment ANCHOR_ empties carry anchor_to 'Ground' or 'Support'; for 'Support' use this instance's anchor_to list")
    json.dump(J,open(f"{outdir}/{L.name}.layout.json","w"),indent=1)
    print("LAYOUT",L.name,"pieces",len(inst),"connections",len(con),"tris",tot,"unsupported",miss,flush=True)
    return J
