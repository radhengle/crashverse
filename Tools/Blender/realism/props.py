"""Props_Village (shared atlas, ONE material M_Props_Village): Barrel, Crate, Cart, Rock_A/B/C. LOD0 + LOD1."""
import bpy,bmesh,math,random,os,sys
from mathutils import Vector,Matrix,noise
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import rlib as R
TEX=R.REAL+"/Props/Props_Village"
def A(): return R.Atlas(TEX+"/cells.json")
def Mt(loc,rx=0,ry=0,rz=0): return Matrix.Translation(Vector(loc))@Matrix.Rotation(rz,4,'Z')@Matrix.Rotation(ry,4,'Y')@Matrix.Rotation(rx,4,'X')
def barrel(lod,seed=1):
    at=A(); B=R.Builder(at); rng=random.Random(seed); H=.92; n=16 if lod==0 else 10; rings=7 if lod==0 else 4
    r=at.rect("WoodPlank"); rad=lambda z:.27+.05*math.sin(math.pi*z/H)
    for k in range(n):   # individual staves (own verts -> real seams), slight per-stave tone + offset
        a0,a1=2*math.pi*k/n,2*math.pi*(k+1)/n; bm=bmesh.new(); uv=bm.loops.layers.uv.new("UV0"); ou=rng.random()*.8; tn=.9+.12*rng.random()
        cols=[]
        for j in range(rings+1):
            z=H*j/rings; row=[]
            for (a,ins) in ((a0+.004,.004),((a0+a1)/2,0),(a1-.004,.004)):
                rr=rad(z)-ins; row.append(bm.verts.new((rr*math.cos(a),rr*math.sin(a),z)))
            cols.append(row)
        for j in range(rings):
            for i in range(2):
                f=bm.faces.new((cols[j][i],cols[j][i+1],cols[j+1][i+1],cols[j+1][i]))
                for L in f.loops:
                    jj=next(q for q in range(rings+1) if L.vert in cols[q]); ii=cols[jj].index(L.vert)
                    L[uv].uv=(r[0]+(r[2]-r[0])*(ou+ii*.06),r[1]+(r[3]-r[1])*(.04+.9*jj/rings))
        bm.normal_update(); R.orient(bm,lambda f:Vector((f.calc_center_median().x,f.calc_center_median().y,0))); B.add(bm,(tn,tn*.98,tn*.96))
    # lids (end grain planks) set slightly inside
    for z,s in ((.03,-1),(H-.03,1)):
        bm=bmesh.new(); m=n; vs=[bm.verts.new((rad(z)*.97*math.cos(2*math.pi*i/m),rad(z)*.97*math.sin(2*math.pi*i/m),z)) for i in range(m)]
        f=bm.faces.new(vs if s>0 else vs[::-1]); bm.normal_update(); R.uv_cells(bm,at,"WoodPlank",None,(.2,.3,0),cut=False); B.add(bm,(.92,.9,.88))
    # steel hoops
    for zc in ((.1,.3,H-.3,H-.1) if lod==0 else (.12,H-.12)):
        bm=bmesh.new(); hw=.035; m=n*2 if lod==0 else n
        rows=[[bm.verts.new(((rad(zc+dz)+.008)*math.cos(2*math.pi*i/m),(rad(zc+dz)+.008)*math.sin(2*math.pi*i/m),zc+dz)) for i in range(m)] for dz in (-hw,hw)]
        for i in range(m): bm.faces.new((rows[0][i],rows[0][(i+1)%m],rows[1][(i+1)%m],rows[1][i]))
        bm.normal_update(); R.orient(bm,lambda f:Vector((f.calc_center_median().x,f.calc_center_median().y,0))); R.uv_cells(bm,at,"Steel",None,cut=False); B.add(bm)
    return B
def crate(lod,seed=2,size=(.8,.8,.7)):
    at=A(); B=R.Builder(at); rng=random.Random(seed); sx,sy,sz=size; t=.025; bev=.006 if lod==0 else 0
    np_=3
    for ax,sgn in ((0,1),(0,-1),(1,1),(1,-1)):   # side panels of horizontal planks with gaps
        L=sy if ax==0 else sx
        for k in range(np_):
            h=(sz-.02)/np_; z=.01+h*(k+.5)
            c=[0,0,z]; c[ax]=sgn*((sx if ax==0 else sy)/2-t/2)
            dims=(L-.06,t,h-.012) if ax==1 else (t,L-.06,h-.012)
            tn=.9+.12*rng.random(); B.part(dims,Mt(c),"WoodPlank",1 if ax==0 else 0,0,1,col=(tn,tn,tn*.97),seed=rng.randint(0,9999))
    for k in range(4):   # top + bottom planks
        w=(sx-.02)/4; x=-sx/2+.01+w*(k+.5)
        for z in (.012,sz-.012): B.part((w-.01,sy-.02,t),Mt((x,0,z)),"WoodPlank",1,0,1,seed=rng.randint(0,9999))
    for (x,y) in ((1,1),(1,-1),(-1,1),(-1,-1)):   # corner battens (end grain on the cut ends)
        B.part((sz,.06,.06),Mt((x*(sx/2-.02),y*(sy/2-.02),sz/2),ry=math.pi/2),"WoodBeam",0,bev*2,1,seed=rng.randint(0,9999))
    if lod==0:   # diagonal braces on two faces
        for sgn in (1,-1):
            d=math.atan2(sz-.1,sx-.1); L=math.hypot(sx-.1,sz-.1)
            B.part((L,.02,.07),Mt((0,sgn*(sy/2+.005),sz/2),ry=-d*sgn),"WoodBeam",0,.004,1,seed=rng.randint(0,9999))
    return B
def cart(lod,seed=3):
    at=A(); B=R.Builder(at); rng=random.Random(seed); bev=.008 if lod==0 else 0
    W,Lb,zb=1.15,1.7,.62
    for k in range(6 if lod==0 else 3):   # bed planks along the cart
        w=W/(6 if lod==0 else 3); y=-W/2+w*(k+.5); tn=.88+.14*rng.random()
        B.part((Lb,w-.012,.04),Mt((0,y,zb)),"WoodPlank",0,bev,1,col=(tn,tn,tn*.97),seed=rng.randint(0,9999))
    for sgn in (1,-1):   # side boards + stakes
        B.part((Lb,.035,.28),Mt((0,sgn*(W/2+.02),zb+.16)),"WoodPlank",0,bev,1,seed=rng.randint(0,9999))
        for x in (-.75,0,.75):
            B.part((.06,.06,.42),Mt((x,sgn*(W/2+.06),zb+.13)),"WoodBeam",0,bev,1,seed=rng.randint(0,9999))
        # shafts / handles
        B.part((2.3,.07,.07),Mt((.95,sgn*.42,zb-.06),ry=.12),"WoodBeam",0,bev,1,seed=rng.randint(0,9999))
    B.part((1.55,.09,.09),Mt((0,0,zb-.08),rz=math.pi/2),"WoodBeam",0,bev,1,seed=11)   # axle bed
    B.part((.07,.07,.55),Mt((1.55,0,.3),ry=.2),"WoodBeam",0,bev,1,seed=12)          # prop leg
    # wheels: rim segments, spokes, hub; steel tyre
    for sgn in (1,-1):
        y=sgn*(W/2+.16); Rw=.5; n=16 if lod==0 else 10
        bm=bmesh.new(); uv=bm.loops.layers.uv.new("UV0"); r=at.rect("WoodBeam"); vs=[]
        for i in range(n):
            a=2*math.pi*i/n; ring=[]
            for (rr,dy) in ((Rw,-.04),(Rw,.04),(Rw-.07,.04),(Rw-.07,-.04)):
                ring.append(bm.verts.new((rr*math.cos(a),y+dy,zb-.12+rr*math.sin(a)-0)))
            vs.append(ring)
        for i in range(n):
            for k in range(4):
                f=bm.faces.new((vs[i][k],vs[(i+1)%n][k],vs[(i+1)%n][(k+1)%4],vs[i][(k+1)%4]))
                for L in f.loops:
                    ii=next(q for q in range(n) if L.vert in vs[q]); kk=vs[ii].index(L.vert)
                    L[uv].uv=(r[0]+(r[2]-r[0])*((ii%4)/4+(1 if ii%4==3 and L.vert in vs[(i+1)%n] else 0)*0),r[1]+(r[3]-r[1])*(.1+.2*kk))
        bm.normal_update()
        for v in bm.verts: v.co.z+= .0
        B.add(bm,(.92,.9,.88))
        for i in range(n):   # steel tyre
            a0,a1=2*math.pi*i/n,2*math.pi*(i+1)/n
        bm=bmesh.new(); rows=[[bm.verts.new(((Rw+.012)*math.cos(2*math.pi*i/n),y+dy,zb-.12+(Rw+.012)*math.sin(2*math.pi*i/n))) for i in range(n)] for dy in (-.045,.045)]
        for i in range(n): bm.faces.new((rows[0][i],rows[1][i],rows[1][(i+1)%n],rows[0][(i+1)%n]))
        bm.normal_update(); R.orient(bm,lambda f:Vector((f.calc_center_median().x,0,f.calc_center_median().z-(zb-.12)))); R.uv_cells(bm,at,"Steel",None,cut=False); B.add(bm)
        for k in range(8 if lod==0 else 6):
            a=2*math.pi*k/(8 if lod==0 else 6)
            c=Vector((math.cos(a)*(Rw-.07)/2,y,zb-.12+math.sin(a)*(Rw-.07)/2))
            B.part((Rw-.1,.04,.04),Mt(c,ry=-a),"WoodBeam",0,0,1,seed=20+k)
        B.part((.2,.18,.2),Mt((0,y,zb-.12)),"EndGrain",0,.02 if lod==0 else 0,1,seed=30)
    return B
def rock(lod,seed,scale):
    at=A(); B=R.Builder(at); bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=3 if lod==0 else 2,radius=1)
    o=Vector((seed*3.1,seed*1.7,seed*.3))
    for v in bm.verts:
        d=v.co.normalized(); v.co=d*(1+.28*noise.noise(d*1.6+o)+.08*noise.noise(d*4+o))
        v.co=Vector((v.co.x*scale[0],v.co.y*scale[1],max(-.15,v.co.z)*scale[2]))
    bm.normal_update(); R.uv_cells(bm,at,"Rock",None,(seed*.37,seed*.11,.05),cut=False); R.tint(bm,lambda L:((.92+.08*min(1,L.vert.co.z/scale[2]+.3),)*3+(1,)))
    B.add(bm); return B
def bread_crate(lod,seed=5,size=(.62,.42,.3)):
    """open-top bakery crate (plank sides, battens) heaped with loaves (SandDressed cell tinted to crust)"""
    at=A(); B=R.Builder(at); rng=random.Random(seed); sx,sy,sz=size; t=.02
    for ax,sgn in ((0,1),(0,-1),(1,1),(1,-1)):
        L=sy if ax==0 else sx
        for k in range(2):
            h=(sz-.02)/2; z=.01+h*(k+.5); c=[0,0,z]; c[ax]=sgn*((sx if ax==0 else sy)/2-t/2)
            dims=(L-.04,t,h-.012) if ax==1 else (t,L-.04,h-.012); tn=.9+.12*rng.random()
            B.part(dims,Mt(c),"WoodPlank",1 if ax==0 else 0,0,1,col=(tn,tn,tn*.97),seed=rng.randint(0,9999))
    B.part((sx-.02,sy-.02,t),Mt((0,0,.012)),"WoodPlank",1,0,1,seed=rng.randint(0,9999))
    for (x,y) in ((1,1),(1,-1),(-1,1),(-1,-1)): B.part((sz,.04,.04),Mt((x*(sx/2-.02),y*(sy/2-.02),sz/2),ry=math.pi/2),"WoodBeam",0,0,1,seed=rng.randint(0,9999))
    n=8 if lod==0 else 4
    for i in range(n):
        bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1 if lod==0 else 1,radius=1)
        cx=(rng.random()-.5)*(sx-.2); cy=(rng.random()-.5)*(sy-.16); cz=sz-.06+.05*(i%3); rz=rng.random()*3
        Mr=Matrix.Rotation(rz,3,'Z')
        for v in bm.verts:
            q=Vector((v.co.x*.13,v.co.y*.075,max(v.co.z,-.5)*.065)); v.co=Mr@q+Vector((cx,cy,cz))
        R.uv_cells(bm,at,"SandDressed",None,(i*.3,0,0),cut=False); R.tint(bm,(1.0,.62,.32)); B.add(bm)
    return B
def bench(lod,seed=6,L=1.6):
    """plank bench: two-plank seat, back rail, splayed legs with stretcher"""
    at=A(); B=R.Builder(at); rng=random.Random(seed); bev=.006 if lod==0 else 0
    for k,y in enumerate((-.08,.08)): B.part((L,.15,.045),Mt((0,y,.45)),"WoodPlank",1,bev,1,seed=rng.randint(0,9999))
    for x in (-L/2+.18,L/2-.18):
        for y in (-.12,.12): B.part((.06,.06,.45),Mt((x,y,.215),rx=(.12 if y>0 else -.12)),"WoodBeam",0,bev,1,seed=rng.randint(0,9999))
        B.part((.06,.34,.06),Mt((x,0,.4)),"WoodBeam",0,0,1,seed=rng.randint(0,9999))
        if lod==0: B.part((.05,.05,.5),Mt((x,.17,.68),rx=-.2),"WoodBeam",0,0,1,seed=rng.randint(0,9999))
    B.part((L-.3,.04,.06),Mt((0,0,.14)),"WoodBeam",0,0,1,seed=rng.randint(0,9999))
    if lod==0: B.part((L,.04,.12),Mt((0,.22,.88),rx=-.2),"WoodPlank",1,bev,1,seed=rng.randint(0,9999))
    return B
PROPS2={"BreadCrate":lambda l:bread_crate(l),"Bench":lambda l:bench(l)}
PROPS={"Barrel":lambda l:barrel(l),"Crate":lambda l:crate(l),"Cart":lambda l:cart(l),
       "Rock_A":lambda l:rock(l,1,(.55,.45,.4)),"Rock_B":lambda l:rock(l,2,(.35,.3,.25)),"Rock_C":lambda l:rock(l,3,(.9,.6,.45))}
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m=R.lit_material("M_Props_Village",TEX+"/Textures/T_Props_Village")
    for i,(k,f) in enumerate(PROPS.items()):
        for l in (0,1):
            o=f(l).to_object(f"{k}_LOD{l}",m); o.location=(i*2.5,l*3,0); print(o.name,R.tri_count(o))
    bpy.ops.wm.save_as_mainfile(filepath="/tmp/props_test.blend")
