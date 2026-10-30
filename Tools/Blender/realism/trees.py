"""Realistic Oak / Pine / Bush: branching bark tubes + alpha-clipped leaf-cluster cards, ONE material per LOD.
LOD0..2 procedural from the same skeleton (seeded), LOD3 = crossed-quad billboard (rendered impostor, see export).
Vertex colour Col = canopy AO (inside of the crown darker, >=0.5) ; UV2 = wind (x sway weight 0..1, y phase)."""
import bpy,bmesh,math,random,json,os,sys
from mathutils import Vector,Matrix,noise
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import rlib as R
FOL=R.REAL+"/Foliage"
def meta(asset): return json.load(open(f"{FOL}/{asset}/cards.json"))
class TB:
    def __init__(s,asset,crown_c,crown_r):
        s.bm=bmesh.new(); s.uv=s.bm.loops.layers.uv.new("UV0"); s.uv2=s.bm.loops.layers.uv.new("UV2")
        s.col=s.bm.loops.layers.float_color.new("Col"); s.M=meta(asset); s.vn={}; s.cc=Vector(crown_c); s.cr=Vector(crown_r)
    def crown_n(s,p):
        d=p-s.cc; d=Vector((d.x/s.cr.x**2,d.y/s.cr.y**2,d.z/s.cr.z**2)); return d.normalized() if d.length>1e-6 else Vector((0,0,1))
    def crown_ao(s,p):
        d=p-s.cc; r=math.sqrt((d.x/s.cr.x)**2+(d.y/s.cr.y)**2+(d.z/s.cr.z)**2)
        return max(.55,min(1.0,.5+.5*r))*(1-.12*max(0,(s.cc.z-p.z)/s.cr.z))
    def tube(s,nodes,sides,usc=1.0,sway=(0,0),phase=0.0,ao=None,cap=False):
        """nodes: list of (pos Vector, radius). Bark UVs wrap the cell width per circumference; v restarts (seam ring) at the cell top."""
        r=s.M["bark"]["uv"]; Lm=s.M["bark"]["m"][1]
        rings=[]; v=random.random()*.4; prev=None; acc=0
        frames=[]
        for i,(p,rad) in enumerate(nodes):
            d=(nodes[min(i+1,len(nodes)-1)][0]-nodes[max(i-1,0)][0]).normalized()
            if prev is None: x=d.orthogonal().normalized()
            else: x=(prev-d*prev.dot(d)).normalized()
            prev=x; y=d.cross(x); frames.append((p,rad,d,x,y))
        def ring(p,rad,d,x,y,vv,t):
            out=[]
            for k in range(sides+1):
                a=2*math.pi*k/sides; nrm=(x*math.cos(a)+y*math.sin(a))
                bump=1+.06*noise.noise(p*3+nrm*2) if rad>.08 else 1
                vt=s.bm.verts.new(p+nrm*rad*bump); s.vn[vt]=nrm
                out.append((vt,k/sides,vv,t))
            return out
        L=sum((nodes[i+1][0]-nodes[i][0]).length for i in range(len(nodes)-1)) or 1
        vacc=v; cur=None; tl=0
        for i,(p,rad,d,x,y) in enumerate(frames):
            if i>0: seg=(p-frames[i-1][0]).length; tl+=seg; vacc+=seg/Lm
            t=tl/L
            if vacc>1 and cur is not None:
                # seam: finish the strip at v=1 on a duplicate ring, restart at v=vacc-1
                f=(1-(vacc-seg/Lm))/(seg/Lm); pp=frames[i-1][0].lerp(p,f); rr=frames[i-1][1]+(rad-frames[i-1][1])*f
                end=ring(pp,rr,d,x,y,1.0,t); s._strip(cur,end,r,usc,sway,phase,ao); cur=ring(pp,rr,d,x,y,0.0,t); vacc-=1
            nr=ring(p,rad,d,x,y,vacc,t)
            if cur is not None: s._strip(cur,nr,r,usc,sway,phase,ao)
            cur=nr
    def _strip(s,a,b,r,usc,sway,phase,ao):
        for k in range(len(a)-1):
            vs=(a[k],a[k+1],b[k+1],b[k])
            try: f=s.bm.faces.new([q[0] for q in vs])
            except ValueError: continue
            for L,q in zip(f.loops,vs):
                L[s.uv].uv=(r[0]+(r[2]-r[0])*q[1]*usc, r[1]+(r[3]-r[1])*min(1,max(0,q[2])))
                w=sway[0]+(sway[1]-sway[0])*q[3]; L[s.uv2].uv=(w,phase)
                a_=ao(L.vert.co) if ao else 1.0; L[s.col]=(a_,a_,a_,1)
    def card(s,name,base,up,nrm,H,fold=0.0,sway=1.0,phase=0.0,bend=0.0,segs=1,basefrac=.04,clump=None):
        """leaf card: texture bottom-centre sits on `base`, extends along `up`, faces `nrm`. fold>0 -> V-folded (4 tris)."""
        m=s.M["cards"][name]; r=m["uv"]; W=H*m["aspect"]
        up=up.normalized(); side=up.cross(nrm).normalized(); nrm=side.cross(up).normalized()
        cols=[0,.5,1] if fold>0 else [0,1]
        grid=[]
        for j in range(segs+1):
            t=j/segs; row=[]
            for u in cols:
                off=(fold*W if (fold>0 and u==.5) else 0)
                p=base+up*(H*(t-basefrac))+side*(W*(u-.5))+nrm*off-Vector((0,0,1))*bend*H*t*t
                vt=s.bm.verts.new(p); cn=s.crown_n(p)
                if clump: cn=((p-clump[0]).normalized()*.65+cn*.35).normalized()
                s.vn[vt]=(cn*.6+nrm*.4).normalized()   # more card normal = more leaf-level light/shade
                row.append((vt,u,t)); 
            grid.append(row)
        for j in range(segs):
            for i in range(len(cols)-1):
                q=(grid[j][i],grid[j][i+1],grid[j+1][i+1],grid[j+1][i])
                f=s.bm.faces.new([x[0] for x in q])
                for L,x in zip(f.loops,q):
                    L[s.uv].uv=(r[0]+(r[2]-r[0])*x[1], r[1]+(r[3]-r[1])*x[2])
                    a_=s.crown_ao(L.vert.co)
                    if clump:
                        rr=(L.vert.co-clump[0]).length/clump[1]; a_=min(a_,max(.5,.35+.65*rr)); tn=clump[2]
                    else: tn=1
                    L[s.col]=(a_*tn,a_*tn*1.01,a_*tn*.97,1); L[s.uv2].uv=(sway*(.6+.4*x[2]),phase)
    def to_object(s,name,mat):
        me=bpy.data.meshes.new(name); s.bm.verts.index_update()
        vn=[None]*len(s.bm.verts)
        for v in s.bm.verts: vn[v.index]=s.vn.get(v,Vector((0,0,1)))
        s.bm.to_mesh(me); o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
        me.materials.append(mat)
        for p in me.polygons: p.use_smooth=True
        me.normals_split_custom_set_from_vertices([tuple(n) for n in vn])
        return o
def walk(p,d,L,n,rng,curl=.25,up=0.0):
    pts=[p.copy()]; step=L/n
    for i in range(n):
        d=(d+Vector((rng.uniform(-curl,curl),rng.uniform(-curl,curl),rng.uniform(-curl,curl)+up))).normalized()
        p=p+d*step; pts.append(p.copy())
    return pts
# ------------------------------------------------------------------ OAK
def oak(lod,mat,seed=21):
    random.seed(seed); rng=random.Random(seed); cc=Vector((0,0,5.0)); cr=Vector((3.4,3.2,2.7))
    T=TB("Oak",cc,cr); sides={0:(10,7,5),1:(7,5,4),2:(5,4,3)}[lod]
    ao_tr=lambda p: max(.6,min(1,.6+.4*p.z/2.5)) if p.z<2.5 else T.crown_ao(p)
    # trunk with root flare
    tr=[(Vector((0,0,0)),.42),(Vector((0,0,.25)),.31),(Vector((.03,0,.8)),.27),(Vector((.08,.02,1.6)),.25),(Vector((.1,.03,2.4)),.23)]
    T.tube(tr,sides[0],1.0,(0,.05),0,ao_tr)
    if lod==0:
        for k in range(5):
            a=k*2*math.pi/5+rng.uniform(-.3,.3); d=Vector((math.cos(a),math.sin(a),0))
            T.tube([(Vector((0,0,.35))+d*.15,.13),(d*.55+Vector((0,0,.08)),.08),(d*.85+Vector((0,0,-.05)),.04)],5,.4,(0,0),0,ao_tr)
    top=tr[-1][0]; cards=[]
    nlimb=5 if lod<2 else 4
    for k in range(nlimb):
        a=k*2*math.pi/nlimb+rng.uniform(-.35,.35); el=math.radians(rng.uniform(32,52))
        d=Vector((math.cos(a)*math.sin(el),math.sin(a)*math.sin(el),math.cos(el)))
        L=rng.uniform(2.6,3.3); pts=walk(top+d*.1,d,L,5 if lod<2 else 3,rng,.18,.04)
        T.tube([(p,.2*(1-i/len(pts))+.04) for i,p in enumerate(pts)],sides[1],.7,(.05,.45),rng.random(),T.crown_ao)
        nsub={0:4,1:3,2:0}[lod]
        for j in range(nsub):
            t=rng.uniform(.35,.9); i=int(t*(len(pts)-1)); p=pts[i]; bd=(pts[min(i+1,len(pts)-1)]-pts[i]).normalized()
            sd=(bd+Vector((rng.uniform(-1,1),rng.uniform(-1,1),rng.uniform(-.2,.6)))).normalized()
            sp=walk(p,sd,rng.uniform(1.0,1.7),3 if lod==0 else 2,rng,.2,.05)
            if lod<2: T.tube([(q,.07*(1-ii/len(sp))+.015) for ii,q in enumerate(sp)],sides[2],.35,(.45,.8),rng.random(),T.crown_ao)
            cards+= [(q,(q-p).normalized()) for q in sp[1:]]
        cards+=[(q,(q-pts[max(0,ii-1)]).normalized()) for ii,q in enumerate(pts) if ii>=2]
    # canopy = lumpy sub-clumps at limb/sub-branch ends (gaps between clumps show the branches)
    tips=[c for c in cards]; rng.shuffle(tips)
    clumps=[]
    for p,d in tips:
        if all((p-c[0]).length>1.5 for c in clumps): clumps.append((p+d*.35,rng.uniform(1.15,1.45)))
    tries=0
    while len(clumps)<{0:20,1:16,2:11}[lod] and tries<5000:
        tries+=1; q=Vector((rng.uniform(-1,1),rng.uniform(-1,1),rng.uniform(-.55,1)))
        if not (.45<q.length<.85): continue
        p=cc+Vector((q.x*cr.x,q.y*cr.y,q.z*cr.z))
        if all((p-c[0]).length>1.45 for c in clumps): clumps.append((p,rng.uniform(1.1,1.4)))
    # twig tubes from the nearest branch point to clumps that have no branch (LOD0/1)
    if lod<2:
        for c,rad in clumps:
            a=min(anchors_all,key=lambda x:(x-c).length_squared) if (anchors_all:=[t[0] for t in tips]) else top
            if (a-c).length>.6: T.tube([(a,.05),(a.lerp(c,.5)+Vector((0,0,.1)),.03),(c,.012)],sides[2],.3,(.4,.8),rng.random(),T.crown_ao)
    want={0:640,1:380,2:140}[lod]; per=max(4,want//max(1,len(clumps)))
    names=["C0","C1","C2","C3","S0","S1","S2"] if lod<2 else ["C0","C1","C2","C3"]
    H0={0:1.05,1:1.35,2:2.1}[lod]; made=0
    T.clumps=clumps
    for ci,(c,rad) in enumerate(clumps):
        tint=1+.06*(rng.random()-.5)
        for i in range(per):
            if made>=want: break
            while True:
                q=Vector((rng.uniform(-1,1),rng.uniform(-1,1),rng.uniform(-.7,1)))
                if .5<q.length<1: break
            p=c+q*rad*Vector((1,1,.8))
            nrm=(q.normalized()+Vector((rng.uniform(-.6,.6),rng.uniform(-.6,.6),rng.uniform(-.2,.6)))).normalized()
            up=(q.normalized()*.5+Vector((0,0,.6)))
            nm=names[rng.randrange(len(names))]; H=H0*rng.uniform(.8,1.2)*(.8 if nm[0]=="S" else 1)
            T.card(nm,c.lerp(p,.45),up,nrm,H,fold=.16 if lod==0 else 0,sway=1,phase=(ci*.37)%1,clump=(c,rad,tint))
            made+=1
    return T.to_object(f"Oak_LOD{lod}",mat)
# ------------------------------------------------------------------ PINE
def pine(lod,mat,seed=5):
    random.seed(seed); rng=random.Random(seed); Ht=10.5; cc=Vector((0,0,5.2)); cr=Vector((2.7,2.7,5.2))
    T=TB("Pine",cc,cr); sides={0:(9,3),1:(6,3),2:(5,0)}[lod]
    tr=[(Vector((0,0,0)),.36),(Vector((0,0,.3)),.27)]+[(Vector((.02*math.sin(z),.02*math.cos(z),z)),.25*(1-z/Ht)+.02) for z in (1.5,3,4.5,6,7.5,9,Ht)]
    T.tube(tr,sides[0],1.0,(0,.15),0,lambda p:max(.6,min(1,.6+.4*p.z/3)))
    z=1.7; wh=0; per={0:7,1:5,2:4}[lod]; step={0:.48,1:.62,2:.95}[lod]
    while z<Ht-.9:
        f=(z-1.7)/(Ht-1.7); L=2.9*(1-f)**.95+.45; rot=rng.random()*6
        for k in range(per):
            a=rot+k*2*math.pi/per+rng.uniform(-.2,.2); d=Vector((math.cos(a),math.sin(a),-.18+.25*f))
            d.normalize(); base=Vector((0,0,z+rng.uniform(-.1,.1)))+d*.12
            end=base+d*L+Vector((0,0,-.25*L*(1-f)+.12*L))
            if sides[1] and (lod==0 or k%2==0):
                dr=.18*(1-f)
                mid=base.lerp(end,.45)-Vector((0,0,dr*L*.2)); tip=base.lerp(end,.8)-Vector((0,0,dr*L*.64))
                T.tube([(base,.06*(1-f)+.025),(mid,.03),(tip,.008)],sides[1],.25,(.1,.7),rng.random(),T.crown_ao)
            nm=["B0","B1","B2","B3"][rng.randrange(4) if L>1.6 else 2+rng.randrange(2)]
            hz=Vector((-d.y,d.x,0)).normalized()
            dirv=(end-base).normalized(); n1=dirv.cross(hz).normalized()
            if n1.z<0: n1=-n1
            segs=2 if lod==0 else 1; aspect=T.M["cards"][nm]["aspect"]
            # card's long axis = branch: build with up=branch dir, width across = L/aspect
            for tw in ((0,) if lod==2 else (0,1.05)):
                nn=(Matrix.Rotation(tw*(1 if k%2 else -1),3,dirv)@n1)
                T.card_branch(nm,base,end,nn,L/aspect*1.25,segs,.18*(1-f)) if hasattr(T,"card_branch") else None
        z+=step*rng.uniform(.85,1.15); wh+=1
    T.card("T0",Vector((0,0,Ht-1.25)),Vector((0,0,1)),Vector((1,0,0)),1.9,0,1,.3)
    T.card("T0",Vector((0,0,Ht-1.25)),Vector((0,0,1)),Vector((0,1,0)),1.9,0,1,.6)
    return T.to_object(f"Pine_LOD{lod}",mat)
def card_branch(s,name,base,end,nrm,width,segs,droop):
    """card whose texture x-axis runs along the branch (pine): u 0 at base, 1 at tip; droops toward the tip"""
    m=s.M["cards"][name]; r=m["uv"]; L=(end-base).length; d=(end-base).normalized()
    side=nrm.cross(d).normalized(); grid=[]
    for j in range(segs+1):
        t=j/segs; row=[]
        for v in (0,1):
            p=base+d*L*t+side*width*(v-.5)-Vector((0,0,1))*droop*L*t*t
            vt=s.bm.verts.new(p); s.vn[vt]=(s.crown_n(p)*.7+nrm*.3).normalized(); row.append((vt,t,v))
        grid.append(row)
    for j in range(segs):
        q=(grid[j][0],grid[j+1][0],grid[j+1][1],grid[j][1]); f=s.bm.faces.new([x[0] for x in q])
        for L_,x in zip(f.loops,q):
            L_[s.uv].uv=(r[0]+(r[2]-r[0])*x[1], r[1]+(r[3]-r[1])*x[2])
            a_=s.crown_ao(L_.vert.co); L_[s.col]=(a_,a_,a_,1); L_[s.uv2].uv=(.3+.7*x[1],(base.x*7+base.z)%1)
TB.card_branch=card_branch
# ------------------------------------------------------------------ BUSH
def bush(lod,mat,seed=8):
    random.seed(seed); rng=random.Random(seed); cc=Vector((0,0,.75)); cr=Vector((1.15,1.0,.8))
    T=TB("Bush",cc,cr); n={0:250,1:120,2:46}[lod]
    if lod<2:
        for k in range(5 if lod==0 else 3):
            a=k*2*math.pi/5+rng.uniform(-.3,.3); d=Vector((math.cos(a)*.5,math.sin(a)*.5,1)).normalized()
            pts=walk(Vector((0,0,0)),d,1.0,3 if lod==0 else 2,rng,.2,.0)
            T.tube([(p,.045*(1-i/len(pts))+.01) for i,p in enumerate(pts)],5 if lod==0 else 4,.3,(0,.6),rng.random(),T.crown_ao)
    names=["R0","R1","R2","R3"]; H0={0:.75,1:.95,2:1.35}[lod]
    for i in range(n):
        while True:
            q=Vector((rng.uniform(-1,1),rng.uniform(-1,1),rng.uniform(-.6,1)))
            if .45<q.length<1: break
        p=cc+Vector((q.x*cr.x,q.y*cr.y,q.z*cr.z))*.9; p.z=max(p.z,.15)
        base=cc.lerp(p,.55)+Vector((0,0,-.1)); up=(p-cc).normalized()*.5+Vector((0,0,.6))
        nrm=((p-cc).normalized()+Vector((rng.uniform(-.7,.7),rng.uniform(-.7,.7),rng.uniform(-.2,.7)))).normalized()
        T.card(names[rng.randrange(4)],base,up,nrm,H0*rng.uniform(.8,1.2),fold=.15 if lod==0 else 0,phase=rng.random())
    return T.to_object(f"Bush_LOD{lod}",mat)
SPECIES={"Oak":oak,"Pine":pine,"Bush":bush}
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for sp,fn in SPECIES.items():
        m=R.lit_material(f"M_{sp}",f"{FOL}/{sp}/Textures/T_{sp}",alpha=True)
        for l in (0,1,2):
            o=fn(l,m); o.location.x={"Oak":0,"Pine":9,"Bush":-6}[sp]+l*0; print(o.name,R.tri_count(o))
    bpy.ops.wm.save_as_mainfile(filepath="/tmp/trees_test.blend")
