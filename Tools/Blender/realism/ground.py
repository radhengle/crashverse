"""Ground_Patch (ONE material, tiling-cell atlas: grass / dirt / cobble / rock) + GrassClump cards (ONE alpha material)."""
import bpy,bmesh,math,random,os,sys,json
from mathutils import Vector,noise
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import rlib as R
TEX=R.REAL+"/Props/Ground_Patch"; GTEX=R.REAL+"/Foliage/GrassClump"
SIZE=44.0
PATH=[Vector(p) for p in ((.95,-3.25),(.85,-5.0),(.2,-7.2),(-1.0,-9.6),(-1.6,-12.0),(-1.3,-15.2),(-.4,-18.5),(.3,-22.5))]
def height(x,y):
    h=.10*noise.noise(Vector((x*.12,y*.12,.3)))+.03*noise.noise(Vector((x*.5,y*.5,1.7)))
    d=max(0,max(abs(x)-3.6,0)+max(abs(y+.2)-3.3,0)) if True else 0
    inside=abs(x)<3.8 and -3.6<y<3.2
    return 0 if inside else h*min(1,d/1.5)
def curve_pts(step=.25):
    pts=[]
    for i in range(len(PATH)-1):
        p0=PATH[max(i-1,0)];p1=PATH[i];p2=PATH[i+1];p3=PATH[min(i+2,len(PATH)-1)]
        n=int((p2-p1).length/step)+1
        for k in range(n):
            t=k/n; t2,t3=t*t,t*t*t
            pts.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3))
    pts.append(PATH[-1]); return pts
def path_dist(x,y,pts):
    return min((Vector((x,y))-p).length for p in pts)
def strip(bm,at,cell,pts,a0,a1,lift,ncross,wnoise,seed,cl):
    """strip between signed offsets a0..a1 (m) from the centre line; v restarts every cell length (duplicate row)"""
    uv=bm.loops.layers.uv.get("UV0") or bm.loops.layers.uv.new("UV0"); r=at.rect(cell); S=at.m(cell)[1]; Su=at.m(cell)[0]
    col=bm.loops.layers.float_color.get("Col") or bm.loops.layers.float_color.new("Col")
    rows=[]; acc=random.Random(seed).random()*S; prev=None
    def mk(i,vv):
        p=pts[i]; d=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized(); nrm=Vector((-d.y,d.x))
        row=[]
        for k in range(ncross+1):
            a=a0+(a1-a0)*k/ncross
            if k in (0,ncross): a+=wnoise*noise.noise(Vector((p.x*1.3,p.y*1.3,seed+k)))
            q=p+nrm*a; z=height(q.x,q.y)+lift*(1-.6*(k in (0,ncross)))
            row.append((bm.verts.new((q.x,q.y,z)),(a-a0)/(Su),vv))
        return row
    for i in range(len(pts)):
        if i>0: acc+=(pts[i]-pts[i-1]).length
        if prev is not None and acc/S>1:
            f=1.0; end=mk(i,1.0); face(bm,prev,end,uv,r,col,cl); acc-=S; prev=mk(i,0.0); continue
        row=mk(i,acc/S)
        if prev is not None: face(bm,prev,row,uv,r,col,cl)
        prev=row
def face(bm,a,b,uv,r,col,cl):
    for k in range(len(a)-1):
        q=(a[k],a[k+1],b[k+1],b[k]); f=bm.faces.new([x[0] for x in q]); f.normal_update()
        if f.normal.z<0: f.normal_flip()
        for L,x in zip(f.loops,q):
            L[uv].uv=(r[0]+(r[2]-r[0])*min(1,max(0,x[1])),r[1]+(r[3]-r[1])*min(1,max(0,x[2]))); L[col]=cl
def ground(lod=0):
    at=R.Atlas(TEX+"/cells.json"); B=R.Builder(at); n=int(SIZE/(1.0 if lod==0 else 2.0))
    bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=n,y_segments=n,size=SIZE/2)
    for v in bm.verts: v.co.z=height(v.co.x,v.co.y)
    # skirt so the patch reads as a slab, not a sheet
    R.uv_cells(bm,at,"Grass",None,(0,0,0),cut=False)
    pts=curve_pts()
    R.tint(bm,lambda L:(lambda d:((.93+.07*min(1,d/2.5)),)*3+(1,))(path_dist(L.vert.co.x,L.vert.co.y,pts)))
    B.add(bm)
    sb=bmesh.new(); strip(sb,at,"Cobble",pts,-.85,.85,.035,4 if lod==0 else 2,.12,3,(1,1,1,1)); B.add(sb)
    for (a0,a1,sd) in ((-1.5,-.8,5),(.8,1.5,6)):
        db=bmesh.new(); strip(db,at,"Dirt",pts,a0,a1,.018,2 if lod==0 else 1,.25,sd,(.97,.97,.97,1)); B.add(db)
    return B
def grass_clump(lod):
    meta=json.load(open(GTEX+"/cards.json"))["cards"]; bm=bmesh.new(); uv=bm.loops.layers.uv.new("UV0"); uv2=bm.loops.layers.uv.new("UV2")
    col=bm.loops.layers.float_color.new("Col"); n=3 if lod==0 else 2; W,H=.5,.26
    for k in range(n):
        a=math.pi*k/n; d=Vector((math.cos(a),math.sin(a),0)); r=meta["G0" if k%2==0 else "G1"]["uv"]
        vs=[bm.verts.new(p) for p in (-d*W/2,d*W/2,d*W/2+Vector((0,0,H)),-d*W/2+Vector((0,0,H)))]
        f=bm.faces.new(vs)
        for L,(u,v) in zip(f.loops,((0,0),(1,0),(1,1),(0,1))):
            L[uv].uv=(r[0]+(r[2]-r[0])*u,r[1]+(r[3]-r[1])*v); L[uv2].uv=(v,0); L[col]=(.88+.12*v,)*3+(1,)
    me=bpy.data.meshes.new(f"GrassClump_LOD{lod}"); bm.to_mesh(me)
    for p in me.polygons: p.use_smooth=True
    me.normals_split_custom_set_from_vertices([(0,0,1)]*len(me.vertices))   # up-facing normals: soft, even grass lighting
    o=bpy.data.objects.new(me.name,me); bpy.context.scene.collection.objects.link(o); return o
def scatter(proto,n,seed=4,avoid_house=True):
    rng=random.Random(seed); pts=curve_pts(); out=[]
    tries=0
    while len(out)<n and tries<n*30:
        tries+=1; x,y=rng.uniform(-SIZE/2+.5,SIZE/2-.5),rng.uniform(-SIZE/2+.5,SIZE/2-.5)
        if abs(x)<3.75 and -3.55<y<3.1: continue       # plinth
        if -5.3<x<-3.4 and -1.9<y<1.7: continue        # annex
        d=path_dist(x,y,pts)
        if d<1.1: continue
        dens=1.0 if d>1.8 else .35+.65*(d-1.1)/.7
        near_wall=(abs(x)<4.4 and -4.2<y<3.8)
        if rng.random()>dens*(1.6 if near_wall else 1): continue
        if rng.random()<.35 and not near_wall and d>2.5: continue
        o=bpy.data.objects.new("grass",proto.data); bpy.context.scene.collection.objects.link(o)
        s=rng.uniform(.7,1.35); o.scale=(s,s,s*rng.uniform(.8,1.25)); o.rotation_euler.z=rng.uniform(0,6.3)
        o.location=(x,y,height(x,y)-.02); o.visible_shadow=False; out.append(o)   # mobile: detail grass casts no shadows
    return out
