"""Realistic v1 castle set: shared geometry library (Bld_StoneTower, Bld_CastleKeep, Kit_RealWall, Bld_Gatehouse).
One shared tiling-cell atlas T_RealCastle (Buildings/RealCastle, graded CC0 sandstone/wood/terracotta/rock + procedural granite/steel/banner).
Builders are axis-aligned and LOD aware (s.lod 0..2): stone panels with boolean openings (arrow slits, round-arched doors), quoins,
iron-bound timber ring beams, corbel courses, timber hoarding, crenellated parapets, tiled pyramid / cone roofs, round drums, banners."""
import bpy,bmesh,math,os,sys,random,json
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE); sys.path.insert(0,os.path.join(HERE,"..","realism"))
import townhouse as TH, house_modular as HM, modkit as MK
from house_modular import R
Mt=TH.Mt
CDIR=R.REAL+"/Buildings/RealCastle"; TH.TEX=CDIR; TEXC=CDIR+"/Textures/T_RealCastle"
INNER=R.REAL+"/Buildings/Bld_Townhouse/Textures/T_RealHouse_Inner"
IRON=(.30,.29,.28)   # blackened wrought iron (Col tint on the Steel cell)
OAK=(.74,.63,.53)    # weathered oak (Col tint on WoodBeam/WoodPlank)
def hexa(P):
    """closed 6-face solid from 8 corners (bottom a0..a3, top b0..b3)"""
    bm=bmesh.new(); v=[bm.verts.new(Vector(p)) for p in P]
    for f in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)): bm.faces.new([v[i] for i in f])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); bm.normal_update(); return bm
def prism(prof,fr,d0,d1):
    """extrude a convex/simple 2D profile [(a,z)] between depths d0..d1; fr(a,z,d)->Vector"""
    bm=bmesh.new(); A=[bm.verts.new(fr(a,z,d0)) for a,z in prof]; B=[bm.verts.new(fr(a,z,d1)) for a,z in prof]; n=len(prof)
    bm.faces.new(A); bm.faces.new(B[::-1])
    for i in range(n): j=(i+1)%n; bm.faces.new((A[i],A[j],B[j],B[i]))
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); bm.normal_update(); return bm
def arch_prof(w,h,n=10,z0=0.0):
    """round-arched opening profile: rectangle w x (h-w/2) + semicircle"""
    r=w/2; zs=z0+h-r; P=[(-r,z0),(r,z0)]
    for k in range(n+1): t=math.pi*k/n; P.append((r*math.cos(t),zs+r*math.sin(t)))
    return P
class CK(HM.House):
    def __init__(s,lod):
        super().__init__(lod); s.rr=random.Random(5+lod)
    # ------------------------------------------------------------ frames on an axis-aligned wall
    @staticmethod
    def wf(n,lo,hi):
        """wall frame for outward normal n=(nx,ny): returns fr(a,z,d) with d = distance outward from the OUTER face, and (na,ta,wo,t)"""
        lo,hi=Vector(lo),Vector(hi)
        na=0 if n[0] else 1; ta=1-na; sg=n[na]; wo=hi[na] if sg>0 else lo[na]; t=hi[na]-lo[na]
        def fr(a,z,d):
            p=[0,0,z]; p[na]=wo+sg*d; p[ta]=a; return Vector(p)
        return fr,na,ta,sg,wo,t
    def stone(s,lo,hi,cuts=(),cell="Sandstone",jit=.012,col=(1,1,1),bev=.03):
        lo,hi=Vector(lo),Vector(hi); bm=R.bm_box(*(hi-lo),tuple((lo+hi)/2)); R.bevel(bm,bev*s.wbev,s.seg)
        cs=[c for c,ml in cuts if s.lod<=ml]
        if cs: bm=R.boolean_diff(bm,cs)
        R.uv_cells(bm,s.A,cell,None,(s.rr.random()*2,s.rr.random()*2,s.rr.random()*2))
        if s.lod==0 and jit: R.jitter(bm,jit,1.4,s.rr.randint(0,99))
        z0=lo.z; R.tint(bm,lambda L:(*(c*(1-.06*max(0,1-(L.vert.co.z-z0)/.7)) for c in col),1)); s.B.add(bm); return bm
    def dressed(s,c,size,cell="SandDressed",col=None,bev=.012):
        r=s.rr.random(); col=col or (1-.05*r,1-.05*r,1-.06*r); return s.block(c,size,cell,bev,col=col)
    def wall(s,n,lo,hi,opens=(),quoins=False,cell="Sandstone",col=(1,1,1),putlogs=0):
        """stone wall panel with openings: ('slit',a,z0,h) ('door',a,z0,w,h) ('win',a,z0,w,h); quoins on both tangent ends"""
        lo,hi=Vector(lo),Vector(hi); fr,na,ta,sg,wo,t=s.wf(n,lo,hi); cuts=[]
        for o in opens:
            k=o[0]
            if k=="slit":
                _,a,z0,h=o; cuts.append((prism([(a-.055,z0),(a+.055,z0),(a+.055,z0+h),(a-.055,z0+h)],fr,-t-.2,.2),1))
                cuts.append((prism([(a-.2,z0+h*.55-.04),(a+.2,z0+h*.55-.04),(a+.2,z0+h*.55+.04),(a-.2,z0+h*.55+.04)],fr,-t*.5,.2),0))   # cross oillet
                cuts.append((prism([(a-.2,z0-.05),(a+.2,z0-.05),(a+.12,z0+h+.05),(a-.12,z0+h+.05)],fr,-t-.2,-t+.12),1))           # inner splay
            elif k in ("door","win"):
                _,a,z0,w,h=o; cuts.append((prism([(a+p,z) for p,z in arch_prof(w,h,10 if s.lod==0 else 6,z0)],fr,-t-.2,.2),2))
        if putlogs and s.lod==0:
            for i in range(putlogs):
                a=lo[ta]+(hi[ta]-lo[ta])*(i+.5)/putlogs; z=lo.z+(hi.z-lo.z)*(.25+.5*((i*37)%7)/7)
                cuts.append((prism([(a-.06,z-.06),(a+.06,z-.06),(a+.06,z+.06),(a-.06,z+.06)],fr,-.1,.2),0))
        s.stone(lo,hi,cuts,cell,col=col)
        for o in opens: getattr(s,"dress_"+o[0])(fr,t,*o[1:])
        if quoins and s.lod<2:
            for end,sgn in ((lo[ta],1),(hi[ta],-1)):
                z=lo.z+.02; i=0
                while z+.26<=hi.z-.01:
                    L=.42 if i%2==0 else .26; c=[0,0,z+.13]; c[ta]=end+sgn*(L/2-.012); c[na]=(lo[na]+hi[na])/2
                    sz=[0,0,.25]; sz[ta]=L; sz[na]=t+.03; s.dressed(tuple(c),tuple(sz),bev=0); z+=.28; i+=1
    def dress_slit(s,fr,t,a,z0,h):
        if s.lod>1: return
        for p,q,zz,hh in ((a-.13,a-.055,z0+h/2,h+.1),(a+.055,a+.13,z0+h/2,h+.1),(a-.2,a+.2,z0+h+.1,.14),(a-.2,a+.2,z0-.07,.12)):
            bm=prism([(p,zz-hh/2),(q,zz-hh/2),(q,zz+hh/2),(p,zz+hh/2)],fr,-.03,.02); R.uv_cells(bm,s.A,"SandDressed",None,(a,0,z0),cut=False)
            r=s.rr.random(); s.B.add(bm,(1-.05*r,1-.05*r,1-.06*r))
    def dress_win(s,fr,t,a,z0,w,h): s.dress_door(fr,t,a,z0,w,h,leaf=False)
    def dress_door(s,fr,t,a,z0,w,h,leaf=True):
        n=7 if s.lod==0 else 5; r=w/2; zs=z0+h-r; R2=r+.24
        if s.lod<2:   # voussoirs + jamb stones
            for k in range(n):
                t0=math.pi*k/n+.01; t1=math.pi*(k+1)/n-.01
                P=[(r*math.cos(t0),zs+r*math.sin(t0)),(R2*math.cos(t0),zs+R2*math.sin(t0)),(R2*math.cos(t1),zs+R2*math.sin(t1)),(r*math.cos(t1),zs+r*math.sin(t1))]
                bm=prism([(a+p,z) for p,z in P],fr,-.04,.025); R.uv_cells(bm,s.A,"SandDressed",None,(k*.3,0,0),cut=False); q=s.rr.random(); s.B.add(bm,(1-.06*q,1-.06*q,1-.07*q))
            for sgn in (-1,1):
                zz=z0
                for i in range(3 if s.lod==0 else 2):
                    hh=(zs-z0)/(3 if s.lod==0 else 2); W2=.24 if i%2==0 else .17
                    p0,p1=(a+sgn*r,a+sgn*(r+W2)); bm=prism([(min(p0,p1),zz+.01),(max(p0,p1),zz+.01),(max(p0,p1),zz+hh-.01),(min(p0,p1),zz+hh-.01)],fr,-.04,.025)
                    R.uv_cells(bm,s.A,"SandDressed",None,(i*.4,0,0),cut=False); q=s.rr.random(); s.B.add(bm,(1-.06*q,1-.06*q,1-.07*q)); zz+=hh
        if not leaf: return
        P=[(a+p,z) for p,z in arch_prof(w-.02,h-.01,10 if s.lod==0 else 6,z0)]
        bm=prism(P,fr,-.2,-.14); R.uv_cells(bm,s.A,"WoodPlank",2,(a,0,0),cut=False); s.B.add(bm,(.78,.66,.56))
        if s.lod<2:
            for zz in (z0+.3,z0+h*.5,z0+h-r-.05):
                bm=prism([(a-w/2+.04,zz-.03),(a+w/2-.04,zz-.03),(a+w/2-.04,zz+.03),(a-w/2+.04,zz+.03)],fr,-.145,-.13); R.uv_cells(bm,s.A,"Steel",None,cut=False); s.B.add(bm,IRON)
            if s.lod==0:
                for zz in (z0+.3,z0+h*.5):
                    for k in range(4):
                        aa=a-w/2+.12+k*(w-.24)/3; bm=prism([(aa-.02,zz-.02),(aa+.02,zz-.02),(aa+.02,zz+.02),(aa-.02,zz+.02)],fr,-.135,-.115); R.uv_cells(bm,s.A,"Steel",None,cut=False); s.B.add(bm,IRON)
            bm=prism([(a-w/2-.1,z0-.06),(a+w/2+.1,z0-.06),(a+w/2+.1,z0+.02),(a-w/2-.1,z0+.02)],fr,-t+.02,.08); R.uv_cells(bm,s.A,"Granite",None,cut=False); s.B.add(bm)   # threshold
    # ------------------------------------------------------------ iron-bound oak ring beam (storey seam)
    def ring(s,z0,W=2.6,h=.5,floor=True):
        hw=W/2; d=.3; bh=h/2
        for lev in (0,1):
            z=z0+bh*(lev+.5); full_x=(lev==0)
            for sx in (-1,1):   # beams along Y at x=+-(hw-d/2)
                L=W if full_x else W-2*d; x=sx*(hw-d/2)
                s.beam((x,-L/2,z),(x,L/2,z),bh-.01,d,(sx,0,0),warp=.004,col=OAK)
            for sy in (-1,1):
                L=W-2*d if full_x else W; y=sy*(hw-d/2)
                s.beam((-L/2,y,z),(L/2,y,z),bh-.01,d,(0,sy,0),warp=.004,col=OAK)
        if floor: s.block((0,0,z0+h-.03),(W-2*d+.02,W-2*d+.02,.06),"WoodPlank",0)
        # iron hoop on the lap joint + corner plates and bolts
        if s.lod<2:
            for sx in (-1,1): s.B.part((.012,W+.02,.07),Mt((sx*(hw+.006),0,z0+bh)),"Steel",0,0,1,col=IRON,seed=1)
            for sy in (-1,1): s.B.part((W+.02,.012,.07),Mt((0,sy*(hw+.006),z0+bh)),"Steel",0,0,1,col=IRON,seed=2)
            for sx in (-1,1):
                for sy in (-1,1):
                    for ax in (0,1):
                        c=[sx*(hw+.009),sy*(hw-.14),z0+bh] if ax==0 else [sx*(hw-.14),sy*(hw+.009),z0+bh]
                        sz=(.012,.26,.3) if ax==0 else (.26,.012,.3); s.B.part(sz,Mt(tuple(c)),"Steel",0,0,1,col=IRON,seed=3)
    # ------------------------------------------------------------ stone corbel course with coping (machicolation look)
    def corbel_course(s,z0,Wc=2.6,Wo=3.2,h=.4,step=.45):
        hc=Wc/2; ho=Wo/2; zt=z0+h-.14
        s.stone((-hc,-hc,z0),(hc,hc,zt),(),"Sandstone",jit=.008)
        s.dressed((0,0,zt+.07),(Wo,Wo,.14))
        nps=max(2,int(round(Wc/step)))
        for side in range(4):
            for i in range(nps if s.lod<2 else 3):
                n=nps if s.lod<2 else 3; a=-hc+.2+(Wc-.4)*i/max(1,n-1)
                for k,(out,hh) in enumerate(((ho-hc-.08,.12),(ho-hc-.02,.12))):
                    zz=zt-.02-hh/2-(1-k)*.12; o=out/2
                    c={0:(hc+o,a),1:(-hc-o,a),2:(a,hc+o),3:(a,-hc-o)}[side]
                    sz=(out,.2,hh) if side<2 else (.2,out,hh); s.dressed((c[0],c[1],zz),sz,bev=0)
    # ------------------------------------------------------------ timber hoarding / gallery (wood)
    def hoarding(s,z0,W=2.8,h=1.4,loops=2):
        hw=W/2; bw=.23 if s.lod==0 else .36 if s.lod==1 else W/3
        s.block((0,0,z0+.03),(W,W,.06),"WoodPlank",.004,col=OAK)
        s.block((0,0,z0+h-.03),(W-.04,W-.04,.06),"WoodPlank",.004,col=OAK)
        for sx in (-1,1):
            for sy in (-1,1): s.beam((sx*(hw-.07),sy*(hw-.07),z0+.06),(sx*(hw-.07),sy*(hw-.07),z0+h-.06),.14,.14,(sx,0,0),warp=0,col=(.62,.52,.44))
        for side in range(4):
            sx,sy=((1,0),(-1,0),(0,1),(0,-1))[side]; nb=int(W/bw); L=W-.28
            for zz in (z0+.12,z0+h-.12):   # sill + head rails
                a0,a1=-L/2,L/2; p0=(sx*(hw-.07),a0,zz) if sx else (a0,sy*(hw-.07),zz); p1=(sx*(hw-.07),a1,zz) if sx else (a1,sy*(hw-.07),zz)
                s.beam(p0,p1,.1,.12,(sx,sy,0),bev=0,warp=0,col=(.62,.52,.44))
            if s.lod<2:   # mid post
                c=(sx*(hw-.07),0) if sx else (0,sy*(hw-.07)); s.beam((c[0],c[1],z0+.17),(c[0],c[1],z0+h-.17),.12,.12,(sx,sy,0),warp=0,col=(.62,.52,.44))
            lh=[(-L/4,.0)] if loops==1 else [(-L/4,0),(L/4,0)]
            for i in range(nb):
                a=-W/2+.14+bw*(i+.5)*((W-.28)/(bw*nb))
                if abs(a)>L/2: continue
                hole=any(abs(a-c)<bw*.6 for c,_ in lh) and s.lod<2
                spans=[(z0+.06,z0+.55),(z0+.85,z0+h-.06)] if hole else [(z0+.06,z0+h-.06)]
                for za,zb in spans:
                    c=(sx*(hw+.0),a) if sx else (a,sy*(hw+.0)); q=1-.1*s.rr.random(); col=(OAK[0]*q,OAK[1]*q,OAK[2]*q)
                    sz=(.035,bw-.012,zb-za) if sx else (bw-.012,.035,zb-za)
                    s.B.part(sz,Mt((c[0],c[1],(za+zb)/2)),"WoodPlank",2,0,1,col=col,seed=s.rr.randint(0,1e6))
    # ------------------------------------------------------------ crenellated parapet ring (4 sides)
    def parapet(s,z0,W=2.4,h=.8,t=.25,mh=.35,crenel=.42,merlon_gap=None):
        hw=W/2; z1=z0+h; zc=z1-mh
        sides=[((1,0),(hw-t,-hw,z0),(hw,hw,z1)),((-1,0),(-hw,-hw,z0),(-hw+t,hw,z1)),((0,1),(-hw+t,hw-t,z0),(hw-t,hw,z1)),((0,-1),(-hw+t,-hw,z0),(hw-t,-hw+t,z1))]
        for n,lo,hi in sides:
            fr,na,ta,sg,wo,tt=s.wf(n,lo,hi); L=W; nc=max(1,int(round(L/1.2)))
            cuts=[]
            for i in range(nc):
                a=-L/2+L*(i+.5)/nc; cuts.append((prism([(a-crenel/2,zc),(a+crenel/2,zc),(a+crenel/2,z1+.1),(a-crenel/2,z1+.1)],fr,-tt-.1,.1),2))
            s.stone(lo,hi,cuts,"Sandstone",jit=.01)
    # ------------------------------------------------------------ tiled roofs (terracotta tiles on a plank deck + timber eaves)
    def _tile(s,P0,P1,P2,P3,nrm,th=.028,lift=.03,col=None):
        lo=[P0+nrm*(.012+lift),P1+nrm*(.012+lift),P2+nrm*.012,P3+nrm*.012]
        pts=[tuple(p) for p in lo]+[tuple(p+nrm*th) for p in lo]
        bm=hexa(pts); R.uv_cells(bm,s.A,"Terracotta",None,(s.rr.random(),s.rr.random(),0),cut=False)
        r=s.rr.random(); s.B.add(bm,col or (1-.10*r,1-.12*r,1-.12*r))
    def pyr_roof(s,cx,cy,z0,W,H,rows=7,tw=.26):
        hw=W/2; c=Vector((cx,cy,z0)); top=.9
        rows={0:rows,1:max(3,rows-2),2:0}[s.lod]; tw={0:tw,1:tw*1.8,2:tw}[s.lod]
        bm=bmesh.new(); vs=[bm.verts.new(c+Vector((x*hw*.98,y*hw*.98,0))) for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))]; ap=bm.verts.new(c+Vector((0,0,H*.985)))
        bm.faces.new(vs[::-1])
        for i in range(4): bm.faces.new((vs[i],vs[(i+1)%4],ap))
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); R.uv_cells(bm,s.A,"Terracotta" if s.lod==2 else "WoodPlank",None,cut=False); s.B.add(bm,(.8,.7,.6) if s.lod<2 else (1,1,1))
        for side in range(4):
            th=side*math.pi/2; d=Vector((math.cos(th),math.sin(th),0)); e=Vector((-math.sin(th),math.cos(th),0))
            nrm=(d*H+Vector((0,0,hw))).normalized()
            P=lambda t,u:c+d*hw*(1-t)+e*u*hw*(1-t)+Vector((0,0,H*t))
            for k in range(rows):
                t0=top*k/rows; t1=min(top,top*(k+1)/rows+.035); tm=(t0+t1)/2; n=max(2,int(round(W*(1-tm)/tw))); off=.5*(k%2)
                for i in range(-1,n+1):
                    ua=-1+2*(i+off)/n; ub=-1+2*(i+1+off)/n; ua,ub=max(-1,ua),min(1,ub)
                    if ub-ua<.15/n: continue
                    g=.012/(W*(1-tm)/2)
                    self_ua,self_ub=ua+g,ub-g
                    s._tile(P(t0,self_ua),P(t0,self_ub),P(t1,self_ub),P(t1,self_ua),nrm)
        for x,y in ((1,1),(-1,1),(-1,-1),(1,-1)):   # hip caps
            p0=c+Vector((x*hw*1.01,y*hw*1.01,.03)); p1=c+Vector((0,0,H+.03)); nn=Vector((x,y,0)).normalized()*H+Vector((0,0,hw*1.4))
            s.beam(tuple(p0),tuple(p1),.1,.09,tuple(nn.normalized()),cell="Terracotta",bev=.02,warp=0,ends=False,col=(.92,.86,.82))
        if s.lod<2:
            s.beam((cx,cy,z0+H-.05),(cx,cy,z0+H+.45),.035,.035,(1,0,0),cell="Steel",bev=0,warp=0,ends=False,col=IRON)
            bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1,radius=.07); bmesh.ops.translate(bm,verts=bm.verts,vec=Vector((cx,cy,z0+H+.3)))
            R.uv_cells(bm,s.A,"Gold",None,cut=False); s.B.add(bm)
            for sx in (-1,1):   # timber wall plate under the eaves + rafter feet
                s.beam((cx+sx*(hw-.2),cy-hw+.2,z0-.07),(cx+sx*(hw-.2),cy+hw-.2,z0-.07),.12,.14,(sx,0,0),warp=0)
                s.beam((cx-hw+.34,cy+sx*(hw-.2),z0-.07),(cx+hw-.34,cy+sx*(hw-.2),z0-.07),.12,.14,(0,sx,0),warp=0)
    def cone_roof(s,cx,cy,z0,Rr,H,rows=6,tw=.24,nseg=14):
        c=Vector((cx,cy,z0)); top=.88; rows={0:rows,1:max(3,rows-2),2:0}[s.lod]; tw={0:tw,1:tw*1.8,2:tw}[s.lod]; ns=nseg if s.lod<2 else 8
        bm=bmesh.new(); vs=[bm.verts.new(c+Vector((Rr*.98*math.cos(2*math.pi*i/ns),Rr*.98*math.sin(2*math.pi*i/ns),0))) for i in range(ns)]; ap=bm.verts.new(c+Vector((0,0,H*.985)))
        bm.faces.new(vs[::-1])
        for i in range(ns): bm.faces.new((vs[i],vs[(i+1)%ns],ap))
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); R.uv_cells(bm,s.A,"Terracotta" if s.lod==2 else "WoodPlank",None,cut=False); s.B.add(bm,(.8,.7,.6) if s.lod<2 else (1,1,1))
        for k in range(rows):
            t0=top*k/rows; t1=min(top,top*(k+1)/rows+.035); tm=(t0+t1)/2; n=max(5,int(round(2*math.pi*Rr*(1-tm)/tw))); off=.5*(k%2)
            for i in range(n):
                f0=2*math.pi*(i+off)/n+.012/(Rr*(1-tm)); f1=2*math.pi*(i+1+off)/n-.012/(Rr*(1-tm))
                P=lambda t,f:c+Vector((Rr*(1-t)*math.cos(f),Rr*(1-t)*math.sin(f),H*t))
                fm=(f0+f1)/2; nrm=Vector((math.cos(fm)*H,math.sin(fm)*H,Rr)).normalized()
                s._tile(P(t0,f0),P(t0,f1),P(t1,f1),P(t1,f0),nrm)
        if s.lod<2:
            s.beam((cx,cy,z0+H-.05),(cx,cy,z0+H+.4),.03,.03,(1,0,0),cell="Steel",bev=0,warp=0,ends=False,col=IRON)
            bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1,radius=.06); bmesh.ops.translate(bm,verts=bm.verts,vec=Vector((cx,cy,z0+H+.28))); R.uv_cells(bm,s.A,"Gold",None,cut=False); s.B.add(bm)
    # ------------------------------------------------------------ round stone drum (turret), optional slits
    def drum(s,cx,cy,z0,z1,Rr,t=.3,slits=(),nseg=16,cell="Sandstone",a0=0.0,a1=2*math.pi):
        ns=nseg if s.lod<2 else max(8,nseg//2); full=abs(a1-a0-2*math.pi)<1e-4
        bm=bmesh.new(); rings=[]
        m=ns if full else ns+1
        for z in (z0,z1):
            for rr in (Rr,Rr-t):
                rings.append([bm.verts.new((cx+rr*math.cos(a0+(a1-a0)*i/ns),cy+rr*math.sin(a0+(a1-a0)*i/ns),z)) for i in range(m)])
        Ob,Ib,Ot,It=rings
        for i in range(ns):
            j=(i+1)%m
            bm.faces.new((Ob[i],Ob[j],Ot[j],Ot[i])); bm.faces.new((Ib[j],Ib[i],It[i],It[j])); bm.faces.new((Ot[i],Ot[j],It[j],It[i])); bm.faces.new((Ib[i],Ib[j],Ob[j],Ob[i]))
        if not full:
            for i in (0,m-1): bm.faces.new((Ob[i],Ib[i],It[i],Ot[i]))
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
        if s.lod<2 and slits:
            cs=[]
            for ang,zz,hh in slits:
                d=Vector((math.cos(ang),math.sin(ang),0)); e=Vector((-d.y,d.x,0))
                fr=lambda a,z,dd,d=d,e=e:Vector((cx,cy,0))+d*(Rr+dd)+e*a+Vector((0,0,z))
                cs.append(prism([(-.055,zz),(.055,zz),(.055,zz+hh),(-.055,zz+hh)],fr,-t-.25,.2))
            bm=R.boolean_diff(bm,cs)
        R.uv_cells(bm,s.A,cell,None,(s.rr.random(),s.rr.random(),0))
        if s.lod==0: R.jitter(bm,.01,1.4,s.rr.randint(0,99))
        R.tint(bm,lambda L:((1-.06*max(0,1-(L.vert.co.z-z0)/.7)),)*3+(1,)); s.B.add(bm); return bm
    # ------------------------------------------------------------ banner on a pole with iron brackets
    def banner(s,n,wo,a,ztop,w=.6,L=1.7,pole=True):
        """hangs from ztop in front of an axis-aligned face (outward normal n, face coordinate wo), centred at tangent a"""
        na=0 if n[0] else 1; sg=n[na]; rows=8 if s.lod==0 else 3 if s.lod==1 else 1
        def P(u,v,dd):   # u across 0..1, v down 0..1, dd thickness offset
            wave=.05*math.sin(v*math.pi*1.3+u*.8)*(s.lod<2); p=[0,0,ztop-v*L]; p[na]=wo+sg*(.07+wave+dd); p[1-na]=a+(u-.5)*w; return Vector(p)
        bm=bmesh.new(); F=[[bm.verts.new(P(u,v/rows,0)) for u in (0,1)] for v in range(rows+1)]; Bk=[[bm.verts.new(P(u,v/rows,-.018)) for u in (0,1)] for v in range(rows+1)]
        for v in range(rows):
            bm.faces.new((F[v][0],F[v+1][0],F[v+1][1],F[v][1])); bm.faces.new((Bk[v][1],Bk[v+1][1],Bk[v+1][0],Bk[v][0]))
            bm.faces.new((F[v][0],Bk[v][0],Bk[v+1][0],F[v+1][0])); bm.faces.new((F[v+1][1],Bk[v+1][1],Bk[v][1],F[v][1]))
        bm.faces.new((F[0][1],Bk[0][1],Bk[0][0],F[0][0])); bm.faces.new((F[rows][0],Bk[rows][0],Bk[rows][1],F[rows][1]))
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); bm.normal_update()
        uvl=bm.loops.layers.uv.get("UV0") or bm.loops.layers.uv.new("UV0"); r=s.A.rect("Banner")
        for f in bm.faces:
            for l in f.loops:
                co=l.vert.co; u=(co[1-na]-(a-w/2))/w; v=(ztop-co.z)/L
                if sg*(1 if na==0 else -1)<0: u=1-u
                l[uvl].uv=(r[0]+(r[2]-r[0])*min(max(u,0),1),r[1]+(r[3]-r[1])*(1-min(max(v,0),1)))
        s.B.add(bm)
        if pole and s.lod<2:
            p0=[0,0,ztop]; p1=[0,0,ztop]; p0[na]=p1[na]=wo+sg*.07; p0[1-na]=a-w/2-.08; p1[1-na]=a+w/2+.08
            s.beam(tuple(p0),tuple(p1),.045,.045,tuple(Vector((n[0],n[1],0))),cell="WoodBeam",bev=0,warp=0,ends=False)
            for e in (-1,1):
                q=[0,0,ztop+.02]; q[na]=wo+sg*.04; q[1-na]=a+e*(w/2+.02); sz=[.08,.08,.06]; sz[na]=.09
                s.B.part(tuple(sz),Mt(tuple(q)),"Steel",0,0,1,col=IRON,seed=8)
def write_stats_alias(path,texture_set="RealCastle"):
    d=json.load(open(path)); d["texture_set"]=texture_set; json.dump(d,open(path,"w"),indent=1)
