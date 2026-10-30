"""Bld_Townhouse (realism test) - original geometry, ONE material (M_Bld_Townhouse, tiling-cell atlas).
blender -b -P townhouse.py -- [lod]   -> builds LOD0..2 into the current scene (import as module: build(lod))"""
import bpy,bmesh,math,random,sys,os
from mathutils import Vector,Matrix
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import rlib as R
TEX=R.REAL+"/Buildings/Hero/Bld_Townhouse"
XF,YF,YB=3.2,-2.6,2.6          # stone storey half-width, front, back
Z0,Z1,Z2=0.35,3.2,5.75         # plinth top, jetty floor, top plate
JET=0.3                        # front jetty overhang
PITCH=math.radians(42)
def Mt(loc,rx=0,ry=0,rz=0):
    return Matrix.Translation(Vector(loc))@Matrix.Rotation(rz,4,'Z')@Matrix.Rotation(ry,4,'Y')@Matrix.Rotation(rx,4,'X')
def frame(p0,p1,nrm):
    p0,p1,n=Vector(p0),Vector(p1),Vector(nrm); x=(p1-p0); L=x.length; x.normalize()
    y=(n-x*n.dot(x)).normalized(); z=x.cross(y)
    M=Matrix((x,y,z)).transposed().to_4x4(); M.translation=(p0+p1)/2; return M,L
class House:
    def __init__(s,lod):
        s.lod=lod; s.A=R.Atlas(TEX+"/cells.json"); s.B=R.Builder(s.A); s.rnd=random.Random(11)
        s.seg=2 if lod==0 else 1; s.bev=1.0 if lod==0 else 0.0; s.wbev=1.0 if lod<2 else 0.0
    # ---------- generic pieces
    def beam(s,p0,p1,w,d,nrm,cell="WoodBeam",bev=.018,warp=.008,col=None,ends=True):
        M,L=frame(p0,p1,nrm); r=s.rnd.random()
        col=col or (1-.06*r,1-.07*r,1-.09*r)
        def endg(bm):
            if not ends or s.lod==2: return
            fs=[f for f in bm.faces if abs(f.normal.x)>.9]
            R.uv_cells(bm,s.A,"EndGrain",None,(r,r,r),cut=False,faces=fs)
        return s.B.part((L,d,w),M,cell,0,bev*s.bev,1,warp=warp if s.lod<2 else 0,col=col,faces_cell=endg,seed=s.rnd.randint(0,1e6))
    def block(s,c,size,cell,bev=.02,jit=0.0,col=(1,1,1),rz=0,seg=1,grain=0):
        return s.B.part(size,Mt(c,rz=rz),cell,grain,bev*s.bev,seg,jit=jit if s.lod<2 else 0,col=col,seed=s.rnd.randint(0,1e6))
    def quad(s,pts,cell,uvs=None,col=(1,1,1),facing=None):
        bm=bmesh.new(); vs=[bm.verts.new(Vector(p)) for p in pts]; f=bm.faces.new(vs); bm.normal_update()
        if facing is not None: R.orient(bm,lambda f:Vector(facing))
        uvs=uvs or [(0,0),(1,0),(1,1),(0,1)]; bm.verts.index_update(); m={v.index:uv for v,uv in zip(vs,uvs)}
        R.uv_fixed(bm,s.A,cell,[f],lambda L:m[L.vert.index]); return s.B.add(bm,col)
    # ---------- plinth (indestructible granite + gold band)
    def plinth(s):
        bm=R.bm_box(2*XF+.6,YB-YF+JET+.5,Z0,(0,(YB+YF-JET)/2,Z0/2)); R.bevel(bm,.03*s.bev,s.seg)
        R.uv_cells(bm,s.A,"Granite",None,(0,0,0)); s.B.add(bm)
        if s.lod<2:
            hx,y0,y1=XF+.31,YF-JET-.26,YB+.26; z=Z0-.07
            for c,sz in (((0,y0,z),(2*hx+.02,.02,.035)),((0,y1,z),(2*hx+.02,.02,.035)),((-hx,(y0+y1)/2,z),(.02,y1-y0,.035)),((hx,(y0+y1)/2,z),(.02,y1-y0,.035))):
                bm=R.bm_box(*sz,c); R.uv_cells(bm,s.A,"Gold",None,cut=False); s.B.add(bm)
    # ---------- stone ground storey with recessed openings
    OPEN_G=[("door",(0,-1),.95,1.1,2.25),("win",(0,-1),-1.55,.95,1.3),("win",(1,0),0.2,.9,1.25),("win",(-1,0),-.6,.9,1.25),("win",(0,1),-.9,.9,1.25),("door",(0,1),1.3,1.0,2.15)]
    def opening_frame(s,side,c,w,h,z0,depth,wall,kind,shut=True,lin="SandDressed"):
        """side=(sx,sy) outward normal; c = position along wall; wall = face coordinate"""
        sx,sy=side; n=Vector((sx,sy,0)); t=Vector((-sy,sx,0)) if sy else Vector((0,sx,0))
        if sy: t=Vector((1,0,0))
        else: t=Vector((0,-sx,0))
        def P(a,b,dn): # a along wall (relative to c), b=z, dn outward offset from wall face
            base=Vector((c,wall,0)) if sy else Vector((wall,c,0))
            return base+t*a+Vector((0,0,b))+n*dn
        back=-depth
        # stone surround (lintel/sill/jambs) in dressed sandstone
        if kind=="win" or kind=="door":
            if lin:
                s.B.part((w+.34,.08,.26),s._M(P(0,z0+h+.13,.02),n,t),lin,0,.02*s.bev,1,jit=.004,seed=s.rnd.randint(0,1e6))
            if kind=="win":
                s.B.part((w+.22,.16,.09),s._M(P(0,z0-.045,.03),n,t),"SandDressed",0,.02*s.bev,1,jit=.003,seed=s.rnd.randint(0,1e6))
            if s.lod==0 and lin and side==(0,-1):
                for sgn in (-1,1):
                    for k,(zz,hh,ww) in enumerate(((z0+.02,.42,.24),(z0+.44,.38,.18),(z0+.82,.48,.24))+(((z0+1.3,.5,.18),) if kind=="door" else ())):
                        if zz+hh>z0+h+.01: hh=z0+h-zz
                        if hh<.12: continue
                        s.B.part((ww,.05,hh-.01),s._M(P(sgn*(w/2+ww/2-.02),zz+hh/2,.012),n,t),"SandDressed",0,.015,1,jit=.003,seed=s.rnd.randint(0,1e6))
        # timber frame inside the reveal
        fd=back+.05
        if kind=="win" and s.lod==2:
            s.quad([P(-w/2,z0,back+.04),P(w/2,z0,back+.04),P(w/2,z0+h,back+.04),P(-w/2,z0+h,back+.04)],"Glass",[(.1,.1),(.9,.1),(.9,.9),(.1,.9)],facing=n)
        elif kind=="win":
            fw=.07
            for a0,a1,b0,b1 in ((-w/2,w/2,z0,z0+fw),(-w/2,w/2,z0+h-fw,z0+h),(-w/2,-w/2+fw,z0,z0+h),(w/2-fw,w/2,z0,z0+h)):
                s.beam(P(a0,(b0+b1)/2,fd) if b1-b0<.1 else P((a0+a1)/2,b0,fd), P(a1,(b0+b1)/2,fd) if b1-b0<.1 else P((a0+a1)/2,b1,fd),
                       fw if b1-b0<.1 else a1-a0,.07,n,bev=0,warp=0,ends=False)
            if s.lod<2:
                s.beam(P(0,z0+fw,fd+.005),P(0,z0+h-fw,fd+.005),.045,.05,n,bev=0,warp=0,ends=False)
                s.beam(P(-w/2+fw,z0+h*.62,fd+.005),P(w/2-fw,z0+h*.62,fd+.005),.045,.05,n,bev=0,warp=0,ends=False)
            # glass with slight per-pane tilt (breaks up reflections like old crown glass)
            for (a0,a1) in ((-w/2+fw,0),(0,w/2-fw)):
                for (b0,b1) in ((z0+fw,z0+h*.62),(z0+h*.62,z0+h-fw)):
                    tilt=(s.rnd.random()-.5)*.012
                    s.quad([P(a0,b0,fd-.01+tilt),P(a1,b0,fd-.01-tilt),P(a1,b1,fd-.01+tilt),P(a0,b1,fd-.01-tilt)],"Glass",
                           [(.1,.1),(.9,.1),(.9,.9),(.1,.9)],facing=n)
            if shut and s.lod<2:
                for sgn in (-1,1):   # open shutters against the wall: plank leaf + Z battens + steel strap hinges
                    ac=sgn*(w/2+.17+w/4)
                    rr=s.rnd.random(); col=(1-.05*rr,1-.05*rr,1-.06*rr)
                    s.B.part((w/2-.01,.045,h-.02),s._M(P(ac,z0+h/2,.05),n,t),"WoodPlank",0,.008*s.bev,1,col=col,seed=s.rnd.randint(0,1e6))
                    if s.lod==0:
                        for zz in (z0+.2,z0+h-.2):
                            s.beam(P(ac-w/4+.04,zz,.085),P(ac+w/4-.04,zz,.085),.1,.03,n,cell="WoodPlank",bev=0,warp=0,ends=False)
                            s.B.part((w/2*.7,.006,.022),s._M(P(ac-sgn*.06,zz,.104),n,t),"Steel",0,0,1,seed=1)
        elif kind=="door":
            # plank door with battens and steel straps, set back in the reveal
            s.B.part((w,.06,h),s._M(P(0,z0+h/2,back+.03),n,t),"WoodPlank",2 if False else 0,.006*s.bev,1,col=(.93,.9,.88),seed=s.rnd.randint(0,1e6))
            if s.lod<2:
                for zz in (z0+.3,z0+h/2,z0+h-.3):
                    s.B.part((w*.92,.012,.06),s._M(P(0,zz,back+.066),n,t),"Steel",0,0,1,seed=2)
                s.B.part((.05,.04,.16),s._M(P(w*.32,z0+1.0,back+.08),n,t),"Steel",0,0,1,seed=3)
    def _M(s,c,n,t):
        z=Vector((0,0,1)); M=Matrix((t,n,z)).transposed().to_4x4(); M.translation=c; return M
    def stone_storey(s):
        bm=R.bm_box(2*XF,YB-YF,Z1-Z0,(0,(YB+YF)/2,(Z0+Z1)/2)); R.bevel(bm,.035*s.wbev,s.seg)
        cut=[]; s.gopen=[]
        for kind,side,c,w,h in s.OPEN_G:
            z0=Z0+(0.02 if kind=="door" else 1.05); dep=.22
            sx,sy=side; wall=YF if sy<0 else YB if sy>0 else (XF if sx>0 else -XF)
            if sy: cb=R.bm_box(w,dep*2,h,(c,wall,z0+h/2))
            else: cb=R.bm_box(dep*2,w,h,(wall,c,z0+h/2))
            cut.append(cb); s.gopen.append((kind,side,c,w,h,z0,dep,wall))
        bm=R.boolean_diff(bm,cut)
        R.uv_cells(bm,s.A,"Sandstone",None,(.37,.11,.05))
        # extra horizontal cuts near the ground/under the jetty give the vertex AO something to hold
        if s.lod==0: R.jitter(bm,.011,1.4,3)
        g=lambda L:(lambda zz:(1-.05*max(0,1-(zz-Z0)/.6),)*3)(L.vert.co.z)
        R.tint(bm,lambda L:(*g(L),1)); s.B.add(bm)
        for o in s.gopen: s.opening_frame(o[1],o[2],o[3],o[4],o[5],o[6],o[7],o[0],shut=(o[1]==(0,-1)))
        if s.lod<2:   # quoins: alternating long/short dressed blocks proud of the rubble wall
            for sx in (-1,1):
                for sy in (-1,):
                    z=Z0+.02; k=0; yc=YF if sy<0 else YB
                    while z<Z1-.25:
                        hh=.3+.12*s.rnd.random(); hh=min(hh,Z1-z-.02); L1,L2=(.5,.3) if k%2 else (.3,.5)
                        col=(lambda r:(1-.05*r,1-.05*r,1-.04*r))(s.rnd.random())
                        cxq=sx*(XF+.02)-sx*L1/2; cyq=yc+sy*.02-sy*L2/2
                        s.B.part((L1,L2,hh-.012),Mt((cxq,cyq,z+hh/2)),"SandDressed",0,.022*s.bev,1,jit=.005,col=col,seed=s.rnd.randint(0,1e6))
                        z+=hh; k+=1
        # door steps
        for i,(dz,dy) in enumerate(((Z0*.5,.75),(Z0,.38))):
            s.block((.95,YF-dy/2-.0-(.35 if i==0 else 0)*0,dz/2),(1.6-.2*i,dy,dz),"SandDressed",.02,.004,seg=1)
    # ---------- jettied timber-framed upper storey
    OPEN_U=[((0,-1),-1.5,.95,1.15),((0,-1),1.2,.95,1.15),((1,0),0.0,.85,1.05),((-1,0),.4,.85,1.05),((0,1),.4,.95,1.1)]
    def upper(s):
        yf=YF-JET; bm=R.bm_box(2*XF+.04,YB-yf+.02,Z2-Z1,(0,(YB+yf)/2,(Z1+Z2)/2)); R.bevel(bm,.025*s.wbev,1)
        cut=[]; s.uopen=[]
        for side,c,w,h in s.OPEN_U:
            z0=Z1+.85; sx,sy=side; wall=yf-.01 if sy<0 else YB+.01 if sy>0 else (XF+.02 if sx>0 else -XF-.02)
            cut.append(R.bm_box(w,.3,h,(c,wall,z0+h/2)) if sy else R.bm_box(.3,w,h,(wall,c,z0+h/2)))
            s.uopen.append((side,c,w,h,z0,wall))
        bm=R.boolean_diff(bm,cut); R.uv_cells(bm,s.A,"Plaster",None,(.2,.4,.3))
        if s.lod==0: R.jitter(bm,.006,1.1,5)
        s.B.add(bm,(.98,.97,.95))
        for side,c,w,h,z0,wall in s.uopen: s.opening_frame(side,c,w,h,z0,.15,wall,"win",shut=(side==(0,-1)),lin=None)
        # timber frame proud of the plaster
        T=.2; d=.07
        def face_frame(side,wall,a0,a1,posts,braces):
            sx,sy=side; n=Vector((sx,sy,0))
            def p(a,z): return Vector((a,wall+sy*d/2,z)) if sy else Vector((wall+sx*d/2,a,z))
            s.beam(p(a0,Z1+T/2),p(a1,Z1+T/2),T,d+.02,n)                 # sill beam (bressumer)
            s.beam(p(a0,Z2-.09),p(a1,Z2-.09),.18,d,n)                    # top plate
            for a in posts: s.beam(p(a,Z1+T),p(a,Z2-.18),.18 if a in (a0+.09,a1-.09) else .15,d,n,warp=.01)
            if s.lod<2:
                for (a,b) in braces: s.beam(p(a,Z1+T+.02),p(b,Z2-.2),.13,d-.01,n,warp=.006,bev=0,ends=False)
            # mid rail between posts (not across windows)
            if s.lod==0:
                for (a,b) in getattr(s,"_rails",[]): pass
        yf=YF-JET
        face_frame((0,-1),yf-.01,-XF-.02,XF+.02,[-XF+.07,-2.1,-.9,.3,1.8,XF-.07],[(-.9+.08,.3-.08)] )
        face_frame((0,1),YB+.01,-XF-.02,XF+.02,[-XF+.07,-.25,1.05,XF-.07],[(-XF+.2,-.35),(1.15,XF-.2)])
        for sx in (-1,1):
            w=sx*(XF+.02); mid=(yf+YB)/2
            face_frame((sx,0),w,yf-.02,YB+.02,[yf+.07,(-.3 if sx>0 else .1)-.55,(-.3 if sx>0 else .1)+.55+.4*(sx<0),YB-.07],
                       [(yf+.15,-.9)] if sx>0 else [(1.2,YB-.15)])
        # jetty: joist ends + corner brackets
        if s.lod<2:
            step=.45 if s.lod==0 else .9; x=-XF+.25
            while x<XF-.2:
                s.beam((x,YF+.05,Z1-.07),(x,YF-JET+.03,Z1-.07),.14,.12,(0,0,1),bev=.012,warp=0); x+=step
            for sx in (-1,1):
                s.beam((sx*(XF-.12),YF+.02,Z1-.95),(sx*(XF-.12),YF-JET+.05,Z1-.14),.13,.12,(sx,0,0),warp=0)
    # ---------- roof
    def roof(s):
        yf=YF-JET; half=(YB-yf)/2+.0; yc=(YB+yf)/2; ov=.45; vx=XF+.4
        rise=(half)*math.tan(PITCH); zr=Z2+rise
        s.ridge=(yc,zr); s.eave=ov
        # gable triangles (plaster with a king post) at both ends
        for sx in (-1,1):
            x=sx*(XF+.0)
            bm=bmesh.new(); pts=[(yf,Z2),(YB,Z2),(yc,zr-.05)]
            vs=[bm.verts.new((x-sx*.12+sx*a,y,z)) for a in (0,.12) for (y,z) in pts]
            bm.faces.new(vs[:3][::-1] if sx>0 else vs[:3]); bm.faces.new(vs[3:] if sx>0 else vs[3:][::-1])
            for i in range(3): j=(i+1)%3; f=bm.faces.new((vs[i],vs[j],vs[3+j],vs[3+i])) 
            bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); R.uv_cells(bm,s.A,"Plaster",None,(.1,.3,.2)); s.B.add(bm,(.97,.96,.94))
            n=(sx,0,0); xo=x+sx*.03
            s.beam((xo,yc,Z2),(xo,yc,zr-.25),.16,.07,n)
            s.beam((xo,yf+.3,Z2+.55),(xo,YB-.3,Z2+.55),.15,.07,n)
            if s.lod<2:
                s.beam((xo,yc-.08,Z2+.62),(xo,yf+.95,Z2+.05),.12,.06,n); s.beam((xo,yc+.08,Z2+.62),(xo,YB-.95,Z2+.05),.12,.06,n)
        # roof deck per slope (soffit + verge visible) and tiles
        for sy in (-1,1):
            ye=(yf if sy<0 else YB)+sy*ov; ze=Z2-ov*math.tan(PITCH)
            e_s=Vector((0,-sy*math.cos(PITCH),math.sin(PITCH))); nrm=Vector((0,sy*math.sin(PITCH),math.cos(PITCH)))
            Ls=(Vector((0,yc,zr))-Vector((0,ye,ze))).length
            p0=Vector((0,ye,ze))
            # deck slab (planks underneath)
            M=Matrix((Vector((1,0,0)),e_s,nrm)).transposed().to_4x4(); M.translation=p0+e_s*Ls/2-nrm*.11
            s.B.part((2*vx,Ls,.12),M,"WoodBeam",0,0,1,seed=40+sy)
            # fascia + barge boards
            s.beam((-vx,ye+sy*.03,ze-.1),(vx,ye+sy*.03,ze-.1),.22,.04,(0,sy,0))
            for sx in (-1,1):
                a=p0+Vector((sx*(vx+.02),0,0))-nrm*.02; b=a+e_s*(Ls+.05)
                s.beam(a,b,.26,.045,(sx,0,0),warp=.01)
            # rafter tails under the eave
            if s.lod==0:
                x=-XF+.1
                while x<XF:
                    a=Vector((x,(yf if sy<0 else YB),Z2-.05)); b=p0+Vector((x,0,0))-nrm*.16+e_s*.05
                    s.beam(a,b,.13,.09,nrm,warp=0,bev=0); x+=.62
            s.tiles(p0+nrm*.0,e_s,nrm,2*vx,Ls,sy)
        # ridge
        if s.lod<2: s.ridge_tiles(vx,yc,zr)
        else: s.beam((-vx,yc,zr+.06),(vx,yc,zr+.06),.16,.2,(0,0,1),cell="Terracotta",ends=False)
        s.chimney(yc,zr)
    def tiles(s,p0,e_s,n,U,Ls,sy,chim=True):
        tw,tl,ex=.34,.46,.38; ncol=int(U/tw); nrow=int((Ls-.05)/ex)+1; th=.045
        e_u=Vector((1,0,0)); A=s.A
        if s.lod==2:   # corrugated sheet: valleys/ridges only, no per-tile geometry
            bm=bmesh.new(); seg=2; cols=ncol*seg; rows=[]
            for j in range(2):
                row=[]
                for i in range(cols+1):
                    x=-U/2+i*U/cols; h=th*(1 if i%2 else 0)
                    row.append(bm.verts.new(p0+e_u*x+e_s*(j*Ls)+n*(h+.02)))
                rows.append(row)
            for i in range(cols):
                bm.faces.new((rows[0][i],rows[0][i+1],rows[1][i+1],rows[1][i]))
            bm.normal_update()
            uvl=bm.loops.layers.uv.new("UV0"); r=A.rect("Terracotta")
            for f in bm.faces:
                for L in f.loops:
                    q=L.vert.co-p0; u=(q.dot(e_u)%1.0); v=q.dot(e_s)/Ls
                    L[uvl].uv=(r[0]+(r[2]-r[0])*((q.dot(e_u)+U/2)/U), r[1]+(r[3]-r[1])*v)
            R.orient(bm,lambda f:n); s.B.add(bm,(.97,.95,.93)); return
        seg=getattr(s,"tile_seg0",3) if s.lod==0 else 2; lip=s.lod==0
        bm=bmesh.new(); uvl=bm.loops.layers.uv.new("UV0"); cl=bm.loops.layers.float_color.new("Col"); r=A.rect("Terracotta")
        cx0=getattr(s,"chim_box",None)
        for j in range(nrow):
            for i in range(ncol):
                x=-U/2+tw/2+i*tw+(tw/2 if False else 0); t=j*ex
                if t+tl>Ls+.06: t=Ls+.06-tl
                c=p0+e_u*x+e_s*(t+tl/2)
                if cx0 and sy==cx0[4] and cx0[0]-.12<c.x<cx0[1]+.12 and cx0[2]-.1<c.z<cx0[3]: continue
                rr=s.rnd; sag=.04*math.sin(math.pi*(x/U+.5))*math.sin(math.pi*min(1,t/Ls))
                rot=Matrix.Rotation((rr.random()-.5)*.03,3,n)@Matrix.Rotation((rr.random()-.5)*.02,3,e_u)
                du=(rr.random()-.5)*.012; lift=(rr.random()-.5)*.008-sag
                ou,ov=rr.random()*(1-tw),rr.random()*(1-tl)
                v_=.86+.18*rr.random()-(.1 if rr.random()<.07 else 0); col=(v_*(1+.04*(rr.random()-.5)),v_*(1+.1*(rr.random()-.5)),v_*(1+.14*(rr.random()-.5)),1)
                def P(a,b,h):  # a across [-.5,.5], b along [0,1] (0 = lower end), h normal offset
                    lo=th*.75*(1-b)   # lower end rides on the tile below
                    q=rot@(e_u*(a*tw+du)+e_s*((b-.5)*tl)+n*(h+lo+lift+.025))
                    return c+q
                prof=[(a,th*(1-(2*a)**2)*.9) for a in [k/seg-.5 for k in range(seg+1)]]
                lo_=[bm.verts.new(P(a,0,h)) for a,h in prof]; hi_=[bm.verts.new(P(a,1,h)) for a,h in prof]
                fs=[bm.faces.new((lo_[k],lo_[k+1],hi_[k+1],hi_[k])) for k in range(seg)]
                for f in fs:
                    for L in f.loops:
                        k=(lo_+hi_).index(L.vert); a=prof[k%(seg+1)][0]; b=0 if k<=seg else 1
                        L[uvl].uv=(r[0]+(r[2]-r[0])*(ou+(a+.5)*tw), r[1]+(r[3]-r[1])*(ov+b*tl)); L[cl]=col
                if lip:
                    dn=[bm.verts.new(P(a,0,h)-n*.022-e_s*.004) for a,h in prof]
                    for k in range(seg):
                        f=bm.faces.new((dn[k],dn[k+1],lo_[k+1],lo_[k]))
                        for L in f.loops:
                            kk=(dn+lo_).index(L.vert); a=prof[kk%(seg+1)][0]
                            L[uvl].uv=(r[0]+(r[2]-r[0])*(ou+(a+.5)*tw), r[1]+(r[3]-r[1])*(ov+(.0 if kk<=seg else .05))); L[cl]=(col[0]*.85,col[1]*.85,col[2]*.85,1)
        bm.normal_update()
        R.orient(bm,lambda f:(n if abs(f.normal.dot(n))>.5 else -e_s))
        s.B.parts.append(bm)
    def ridge_tiles(s,vx,yc,zr):
        bm=bmesh.new(); uvl=bm.loops.layers.uv.new("UV0"); cl=bm.loops.layers.float_color.new("Col"); r=s.A.rect("Terracotta")
        seg=4 if s.lod==0 else 3; L=.42; step=.37; x=-vx-.05; rad=.13
        while x<vx-.1:
            rr=s.rnd; v_=.9+.12*rr.random(); col=(v_,v_*(1+.05*(rr.random()-.5)),v_,1); tilt=(rr.random()-.5)*.03
            ring=lambda xx,lift:[bm.verts.new((xx,yc+rad*1.15*math.cos(math.pi*k/seg),zr+.07+lift+rad*math.sin(math.pi*k/seg))) for k in range(seg+1)]
            a=ring(x,.02+tilt); b=ring(x+L,-.0-tilt)
            for k in range(seg):
                f=bm.faces.new((a[k],b[k],b[k+1],a[k+1]))
                for Lp in f.loops:
                    i=(a+b).index(Lp.vert); u=(i%(seg+1))/seg*.45; v=(0 if i<=seg else L)
                    Lp[uvl].uv=(r[0]+(r[2]-r[0])*(.2+u),r[1]+(r[3]-r[1])*(.1+v)); Lp[cl]=col
            if False:  # open end lip
                c=[bm.verts.new((x-.004,yc+(rad-.03)*1.15*math.cos(math.pi*k/seg),zr+.07+.02+tilt+(rad-.03)*math.sin(math.pi*k/seg))) for k in range(seg+1)]
                for k in range(seg):
                    f=bm.faces.new((c[k],a[k],a[k+1],c[k+1]))
                    for Lp in f.loops: Lp[uvl].uv=(r[0]+.01,r[1]+.01); Lp[cl]=(col[0]*.8,col[1]*.8,col[2]*.8,1)
            x+=step
        bm.normal_update(); R.orient(bm,lambda f:f.calc_center_median()-Vector((f.calc_center_median().x,yc,zr+.07)))
        s.B.parts.append(bm)
    def chimney(s,yc,zr):
        cx,cy=XF-1.0,yc+.75; w,d=.75,.6; top=zr+.9
        bm=R.bm_box(w,d,top-Z2+.2,(cx,cy,(top+Z2-.2)/2)); R.bevel(bm,.025*s.bev,1)
        R.uv_cells(bm,s.A,"Sandstone2",None,(.1,.2,.3))
        if s.lod==0: R.jitter(bm,.007,2,9)
        s.B.add(bm)
        s.block((cx,cy,top+.05),(w+.12,d+.12,.1),"SandDressed",.02,.003)
        if s.lod<2:
            for k,dx in enumerate((-.17,.17)):
                bm=bmesh.new(); seg=8 if s.lod==0 else 6; rr=.09; h=.32
                bot=[bm.verts.new((cx+dx+rr*math.cos(2*math.pi*i/seg),cy+rr*math.sin(2*math.pi*i/seg),top+.1)) for i in range(seg)]
                tp=[bm.verts.new((cx+dx+rr*.8*math.cos(2*math.pi*i/seg),cy+rr*.8*math.sin(2*math.pi*i/seg),top+.1+h)) for i in range(seg)]
                inn=[bm.verts.new((cx+dx+rr*.6*math.cos(2*math.pi*i/seg),cy+rr*.6*math.sin(2*math.pi*i/seg),top+.1+h-.08)) for i in range(seg)]
                for i in range(seg):
                    j=(i+1)%seg; bm.faces.new((bot[i],bot[j],tp[j],tp[i])); bm.faces.new((tp[i],tp[j],inn[j],inn[i]))
                bm.normal_update(); R.orient(bm,lambda f,cx=cx+dx:(lambda c:Vector((c.x-cx,c.y-cy,0)) if f.normal.z<.5 or True else Vector((0,0,1)))(f.calc_center_median()) if abs(f.normal.z)<.6 else Vector((cx-f.calc_center_median().x,cy-f.calc_center_median().y,.3)))
                R.uv_cells(bm,s.A,"Terracotta",None,cut=False); R.tint(bm,(.95,.92,.9)); s.B.add(bm)
        # remember the footprint so tiles skip it (front/back slope = sign of cy-yc)
        s.chim_box=(cx-w/2,cx+w/2,Z2,top,1 if cy>yc else -1)
    # ---------- lean-to annex (plank shed on the -X side, lower roof line = height variety)
    def annex(s):
        x0,x1=-XF-.3,-XF-2.1; y0,y1=-1.7,1.5; zt,zl=3.0,2.15
        # plank walls (vertical planks), timber corner posts
        for (a,b,c_,dd) in (((x1,y0),(x1,y1),(-1,0),1),((x1,y0),(x0,y0),(0,-1),0),((x1,y1),(x0,y1),(0,1),0)):
            A_,B_=Vector((*a,0)),Vector((*b,0)); mid=(A_+B_)/2; L=(B_-A_).length
            hh=zl if dd else (zl+zt)/2
            if dd: bm=R.bm_box(.06,L,zl,(mid.x,mid.y,zl/2))
            else: bm=R.bm_box(L,.06,zl,(mid.x,mid.y,zl/2))
            R.bevel(bm,.006*s.bev,1)
            if not dd:   # slope the top to the roof line
                for v in bm.verts:
                    if v.co.z>zl-.01: v.co.z=zl+(zt-zl)*(v.co.x-x1)/(x0-x1)
            R.uv_cells(bm,s.A,"WoodPlank",None,(.3,.6,0)); R.tint(bm,(.9,.87,.85)); s.B.add(bm)
        for (px,py) in ((x1,y0),(x1,y1)):
            s.beam((px,py,0),(px,py,zl+.05),.14,.14,(-1,0,0))
        s.beam((x1,y0-.1,zl),(x1,y1+.1,zl),.16,.14,(-1,0,0))
        if s.lod<2: s.beam((x1,y0+.07,.3),(x1,(y0+y1)/2,zl-.15),.11,.1,(-1,0,0))
        # small window opening as dark planks gap: hinge door on front
        s.B.part((.8,.05,1.8),Mt(((x0+x1)/2,y0-.04,.92)),"WoodPlank",0,.006*s.bev,1,col=(.85,.82,.8),seed=77)
        if s.lod<2:
            for zz in (.35,1.5): s.B.part((.7,.01,.05),Mt(((x0+x1)/2,y0-.07,zz)),"Steel",0,0,1,seed=5)
        # lean-to roof: deck + tiles
        ang=math.atan2(zt-zl,x0-x1); e_s=Vector((math.cos(ang),0,math.sin(ang))); n=Vector((-math.sin(ang),0,math.cos(ang)))
        p0=Vector((x1-.35,0,zl-.35*math.tan(ang)+.05)); Ls=(x0-x1+.35)/math.cos(ang); U=y1-y0+.6
        M=Matrix((Vector((0,1,0)),e_s,n)).transposed().to_4x4(); M.translation=p0+e_s*Ls/2-n*.05+Vector((0,(y0+y1)/2,0))
        s.B.part((U,Ls,.1),M,"WoodPlank",0,.01*s.bev,1,seed=88)
        # reuse the tile generator in a rotated frame: build with e_u = +Y
        s._tiles_frame(p0+Vector((0,(y0+y1)/2,0)),e_s,n,U,Ls)
    def _tiles_frame(s,p0,e_s,n,U,Ls):
        before=len(s.B.parts); s.tiles(Vector((0,0,0)),Vector((0,1,0)) if False else Vector((0,math.cos(0),0))*0+Vector((0,0,0))+Vector((0,1,0))*0+Vector((0,0,0))+Vector((0,0,0))+Vector((0,1,0)) if False else Vector((0,0,0)),Vector((0,1,0)),Vector((0,0,1)),U,Ls,0,chim=False) if False else None
        # generate tiles in a canonical frame (e_u=X, e_s=Y, n=Z) then rotate into place
        cb=getattr(s,"chim_box",None); s.chim_box=None
        s.tiles(Vector((0,0,0)),Vector((0,1,0)),Vector((0,0,1)),U,Ls,0)
        s.chim_box=cb
        bm=s.B.parts[-1]; M=Matrix((Vector((0,1,0)),e_s,n)).transposed().to_4x4(); M.translation=p0
        R.transform(bm,M)
    # ---------- lantern by the door
    def lantern(s):
        if s.lod==2: return
        x,y,z=.95+.85,YF-.02,Z0+2.05
        s.B.part((.04,.35,.04),Mt((x,y-.17,z+.25)),"Steel",1,.008*s.bev,1,seed=6)
        s.B.part((.2,.2,.04),Mt((x,y-.32,z+.18)),"Steel",0,.01*s.bev,1,seed=7)
        s.B.part((.22,.22,.05),Mt((x,y-.32,z-.12)),"Steel",0,.01*s.bev,1,seed=8)
        for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
            c=Vector((x+dx*.085,y-.32+dy*.085,z+.03))
            fc=(dx,dy,0)
            if dx: s.quad([c+Vector((0,-.08,-.14)),c+Vector((0,.08,-.14)),c+Vector((0,.08,.14)),c+Vector((0,-.08,.14))],"Glass",facing=fc)
            else: s.quad([c+Vector((-.08,0,-.14)),c+Vector((.08,0,-.14)),c+Vector((.08,0,.14)),c+Vector((-.08,0,.14))],"Glass",facing=fc)
        bm=bmesh.new(); b=[bm.verts.new((x+sx*.11,y-.32+sy*.11,z+.2)) for sx,sy in ((-1,-1),(1,-1),(1,1),(-1,1))]; tp=bm.verts.new((x,y-.32,z+.34))
        for i in range(4): bm.faces.new((b[i],b[(i+1)%4],tp))
        bm.normal_update(); R.orient(bm,lambda f:f.calc_center_median()-Vector((x,y-.32,z+.2))); R.uv_cells(bm,s.A,"Steel",None,cut=False); s.B.add(bm)
    def build(s):
        s.plinth(); s.stone_storey(); s.upper()
        s.chim_box=None
        # chimney footprint is needed before the tiles: compute it first
        yf=YF-JET; yc=(YB+yf)/2; half=(YB-yf)/2; zr=Z2+half*math.tan(PITCH)
        s.chim_box=(XF-1.0-.375,XF-1.0+.375,Z2,zr+.9,1)
        s.roof(); s.annex(); s.lantern()
        return s.B
def build(lod,mat):
    h=House(lod); B=h.build(); o=B.to_object(f"Bld_Townhouse_LOD{lod}",mat); return o
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m=R.lit_material("M_Bld_Townhouse",TEX+"/Textures/T_Bld_Townhouse")
    for l in (0,1,2):
        o=build(l,m); print(o.name,"tris",R.tri_count(o))
    bpy.ops.wm.save_as_mainfile(filepath="/tmp/townhouse_test.blend")
