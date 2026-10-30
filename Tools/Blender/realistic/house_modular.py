"""Bld_Townhouse, modular + destructible (realistic v1). Reuses the approved realism-test geometry (Tools/Blender/realism/townhouse.py)
and its graded CC0 atlas, split into Kit_RealHouse_<Module> pieces with charm details (balcony, flower boxes, lantern, shop sign).
usage: blender -b -P house_modular.py            -> Assets/_Project/Models/Realistic/Buildings/Bld_Townhouse/*
Per module: Kit_RealHouse_<M>.fbx (LOD0-2 + UCX box) and Kit_RealHouse_<M>_Fractured.fbx (frag_## + exact UCX + JNT + ANCHOR, mass_kg,
pivot = centre of mass, fracture_class). Layout: Bld_Townhouse.layout.json. Stats: models/HeroBuildings/Kit_RealHouse_stats.json."""
import bpy,bmesh,math,random,sys,os,json,re
from mathutils import Vector,Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(HERE,"..","..",".."))
sys.path.insert(0,os.path.join(HERE,"..","realism")); import rlib as R, townhouse as TH
from townhouse import XF,YF,YB,Z0,Z1,Z2,JET,PITCH,Mt
OUT=R.REAL+"/Buildings/Bld_Townhouse"; TH.TEX=OUT; KIT="Kit_RealHouse"; STATS=ROOT+"/models/HeroBuildings/Kit_RealHouse_stats.json"
RNG={"Wood":(8,16),"Stone":(4,10),"Roof":(8,16),"Steel":(4,10)}; DENS={"Stone":2300,"Wood":600,"Roof":1500,"Steel":7850}; MINAX=.15
TS,TU=.45,.17          # stone wall / upper plaster wall thickness
FR_X=-0.6              # front ground wall split (window segment | door segment)
class House(TH.House):
    def __init__(s,lod):
        super().__init__(lod); s.tags=[]; s.tile_seg0=2   # 2-segment tiles at LOD0 keeps the intact house inside 15-20k
    joist=.45
    def sill(s,kind): return 0.02 if kind=="door" else 1.05
    def ground_charm(s,name):
        if name=="StoneWall_FrontDoor": s.lantern(); s.sign()
        if name=="StoneWall_FrontWin": s.flower_box(-1.55,YF,Z0+1.05,.95)
    def upper_frames(s,yf): return {}
    def upper_charm(s,name,yf):
        if name=="UpperWall_Front": s.flower_box(-1.5,yf-.01,Z1+.85,.95)
    def upper_extra(s,name,yf):
        if name=="UpperWall_Front": s.balcony(yf-.01); s.mark("Balcony")
    def mark(s,mod): s.tags+= [mod]*(len(s.B.parts)-len(s.tags))
    # ---------- ground storey: 6 stone wall segments (openings, frames, shutters and charm included)
    def stone_storey(s):
        yf=YF; segs={"StoneWall_FrontWin":((-XF,FR_X),(YF,YF+TS)),"StoneWall_FrontDoor":((FR_X,XF),(YF,YF+TS)),
              "StoneWall_BackDoor":((0.0,XF),(YB-TS,YB)),"StoneWall_BackWin":((-XF,0.0),(YB-TS,YB)),
              "StoneWall_Left":((-XF,-XF+TS),(YF+TS,YB-TS)),"StoneWall_Right":((XF-TS,XF),(YF+TS,YB-TS))}
        s.gopen=[]
        for kind,side,c,w,h in s.OPEN_G:
            z0=Z0+s.sill(kind); dep=.22; sx,sy=side; wall=YF if sy<0 else YB if sy>0 else (XF if sx>0 else -XF)
            s.gopen.append((kind,side,c,w,h,z0,dep,wall))
        def seg_of(o):
            kind,side,c=o[0],o[1],o[2]
            if side==(0,-1): return "StoneWall_FrontWin" if c<FR_X else "StoneWall_FrontDoor"
            if side==(0,1): return "StoneWall_BackDoor" if c>0 else "StoneWall_BackWin"
            return "StoneWall_Right" if side[0]>0 else "StoneWall_Left"
        for name,((x0,x1),(y0,y1)) in segs.items():
            bm=R.bm_box(x1-x0,y1-y0,Z1-Z0,((x0+x1)/2,(y0+y1)/2,(Z0+Z1)/2)); R.bevel(bm,.035*s.wbev,s.seg)
            cut=[]
            for kind,side,c,w,h,z0,dep,wall in s.gopen:
                if seg_of((kind,side,c))!=name: continue
                cut.append(R.bm_box(w,dep*2,h,(c,wall,z0+h/2)) if side[1] else R.bm_box(dep*2,w,h,(wall,c,z0+h/2)))
            if cut: bm=R.boolean_diff(bm,cut)
            R.uv_cells(bm,s.A,"Sandstone",None,(.37,.11,.05))
            if s.lod==0: R.jitter(bm,.011,1.4,3)
            g=lambda L:(lambda zz:(1-.05*max(0,1-(zz-Z0)/.6),)*3)(L.vert.co.z)
            R.tint(bm,lambda L:(*g(L),1)); s.B.add(bm)
            for o in s.gopen:
                if seg_of(o)==name: s.opening_frame(o[1],o[2],o[3],o[4],o[5],o[6],o[7],o[0],shut=(o[1]==(0,-1) and o[0]=="win"))
            s.ground_charm(name)
            s.mark(name)
    # ---------- floor / beam: interior floor, jetty boards, joist ends and corner brackets
    def floor(s):
        s.block((0,(YF+YB)/2,Z1-.1),(2*XF-2*TS+.04,YB-YF-2*TS+.04,.2),"WoodPlank",.0)
        yf=YF-JET; s.block((0,(yf+YF+TS)/2,Z1+.015),(2*XF+.02,YF+TS-yf,.05),"WoodPlank",.006)
        if s.lod<2:
            step=s.joist if s.lod==0 else s.joist*2; x=-XF+.25
            while x<XF-.2:
                s.beam((x,YF+.05,Z1-.07),(x,YF-JET+.03,Z1-.07),.14,.12,(0,0,1),bev=.012,warp=0); x+=step
            for sx in (-1,1): s.beam((sx*(XF-.12),YF+.02,Z1-.95),(sx*(XF-.12),YF-JET+.05,Z1-.14),.13,.12,(sx,0,0),warp=0)
        s.mark("Floor")
    # ---------- upper storey: 4 timber-frame walls (plaster panels + frame + windows + charm)
    def upper(s):
        yf=YF-JET; X0,X1=-XF-.02,XF+.02; Y0,Y1=yf-.01,YB+.01
        walls={"UpperWall_Front":((X0,X1),(Y0,Y0+TU),(0,-1)),"UpperWall_Back":((X0,X1),(Y1-TU,Y1),(0,1)),
               "UpperWall_Left":((X0,X0+TU),(Y0+TU,Y1-TU),(-1,0)),"UpperWall_Right":((X1-TU,X1),(Y0+TU,Y1-TU),(1,0))}
        T=.2; d=.07
        def face_frame(side,wall,a0,a1,posts,braces):
            sx,sy=side; n=Vector((sx,sy,0))
            def p(a,z): return Vector((a,wall+sy*d/2,z)) if sy else Vector((wall+sx*d/2,a,z))
            s.beam(p(a0,Z1+T/2),p(a1,Z1+T/2),T,d+.02,n); s.beam(p(a0,Z2-.09),p(a1,Z2-.09),.18,d,n)
            for a in posts: s.beam(p(a,Z1+T),p(a,Z2-.18),.18 if a in (a0+.09,a1-.09) else .15,d,n,warp=.01)
            if s.lod<2:
                for (a,b) in braces: s.beam(p(a,Z1+T+.02),p(b,Z2-.2),.13,d-.01,n,warp=.006,bev=0,ends=False)
        frames={"UpperWall_Front":((0,-1),yf-.01,-XF-.02,XF+.02,[-XF+.07,-2.1,-.9,.3,1.8,XF-.07],[(-.9+.08,.3-.08)]),
                "UpperWall_Back":((0,1),YB+.01,-XF-.02,XF+.02,[-XF+.07,-.25,1.05,XF-.07],[(-XF+.2,-.35),(1.15,XF-.2)]),
                "UpperWall_Right":((1,0),XF+.02,yf-.02,YB+.02,[yf+.07,-.85,.25,YB-.07],[(yf+.15,-.9)]),
                "UpperWall_Left":((-1,0),-XF-.02,yf-.02,YB+.02,[yf+.07,-.45,1.05,YB-.07],[(1.2,YB-.15)])}
        for name,((x0,x1),(y0,y1),side) in walls.items():
            bm=R.bm_box(x1-x0,y1-y0,Z2-Z1,((x0+x1)/2,(y0+y1)/2,(Z1+Z2)/2)); R.bevel(bm,.025*s.wbev,1)
            ops=[]
            for sd,c,w,h in s.OPEN_U:
                if sd!=side: continue
                z0=Z1+.85; sx,sy=sd; wall=yf-.01 if sy<0 else YB+.01 if sy>0 else (XF+.02 if sx>0 else -XF-.02)
                ops.append((sd,c,w,h,z0,wall))
            cut=[R.bm_box(w,.4,h,(c,wall,z0+h/2)) if sd[1] else R.bm_box(.4,w,h,(wall,c,z0+h/2)) for sd,c,w,h,z0,wall in ops]
            if cut: bm=R.boolean_diff(bm,cut)
            R.uv_cells(bm,s.A,"Plaster",None,(.2,.4,.3))
            if s.lod==0: R.jitter(bm,.006,1.1,5)
            s.B.add(bm,(.98,.97,.95))
            for sd,c,w,h,z0,wall in ops: s.opening_frame(sd,c,w,h,z0,TU,wall,"win",shut=(sd==(0,-1)),lin=None)
            face_frame(*s.upper_frames(yf).get(name,frames.get(name)))
            s.upper_charm(name,yf)
            s.mark(name)
            s.upper_extra(name,yf)
    # ---------- roof: two halves (deck, fascia, barge boards, rafter tails, tiles; ridge on the back half), 2 gables, chimney
    def roof(s):
        yf=YF-JET; half=(YB-yf)/2; yc=(YB+yf)/2; ov=.45; vx=XF+.4; rise=half*math.tan(PITCH); zr=Z2+rise; s.ridge=(yc,zr); s.eave=ov
        for sx in (-1,1):
            x=sx*XF; bm=bmesh.new(); pts=[(yf,Z2),(YB,Z2),(yc,zr-.05)]
            vs=[bm.verts.new((x-sx*.2+sx*a,y,z)) for a in (0,.2) for (y,z) in pts]
            bm.faces.new(vs[:3]); bm.faces.new(vs[3:])
            for i in range(3): j=(i+1)%3; bm.faces.new((vs[i],vs[j],vs[3+j],vs[3+i]))
            bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); R.uv_cells(bm,s.A,"Plaster",None,(.1,.3,.2)); s.B.add(bm,(.97,.96,.94))
            n=(sx,0,0); xo=x+sx*.03
            s.beam((xo,yc,Z2),(xo,yc,zr-.25),.16,.07,n); s.beam((xo,yf+.3,Z2+.55),(xo,YB-.3,Z2+.55),.15,.07,n)
            if s.lod<2: s.beam((xo,yc-.08,Z2+.62),(xo,yf+.95,Z2+.05),.12,.06,n); s.beam((xo,yc+.08,Z2+.62),(xo,YB-.95,Z2+.05),.12,.06,n)
            s.mark("Gable_Left" if sx<0 else "Gable_Right")
        for sy in (-1,1):
            ye=(yf if sy<0 else YB)+sy*ov; ze=Z2-ov*math.tan(PITCH)
            e_s=Vector((0,-sy*math.cos(PITCH),math.sin(PITCH))); nrm=Vector((0,sy*math.sin(PITCH),math.cos(PITCH)))
            Ls=(Vector((0,yc,zr))-Vector((0,ye,ze))).length; p0=Vector((0,ye,ze))
            M=Matrix((Vector((1,0,0)),e_s,nrm)).transposed().to_4x4(); M.translation=p0+e_s*Ls/2-nrm*.11
            s.B.part((2*vx,Ls,.12),M,"WoodBeam",0,0,1,seed=40+sy)
            s.beam((-vx,ye+sy*.03,ze-.1),(vx,ye+sy*.03,ze-.1),.22,.04,(0,sy,0))
            for sx in (-1,1):
                a=p0+Vector((sx*(vx+.02),0,0))-nrm*.02; b=a+e_s*(Ls+.05); s.beam(a,b,.26,.045,(sx,0,0),warp=.01)
            if s.lod==0 and sy<0:      # rafter tails on the street side only (budget)
                x=-XF+.1
                while x<XF:
                    a=Vector((x,(yf if sy<0 else YB),Z2-.05)); b=p0+Vector((x,0,0))-nrm*.16+e_s*.05; s.beam(a,b,.13,.09,nrm,warp=0,bev=0); x+=.62
            s.tiles(p0,e_s,nrm,2*vx,Ls,sy)
            if sy>0:
                if s.lod<2: s.ridge_tiles(vx,yc,zr)
                else: s.beam((-vx,yc,zr+.06),(vx,yc,zr+.06),.16,.2,(0,0,1),cell="Terracotta",ends=False)
            s.mark("Roof_Front" if sy<0 else "Roof_Back")
        s.chimney(yc,zr); s.mark("Chimney")
    # ---------- charm details
    def flower_box(s,x,wall,zs,w,br=True):
        """plank box on two brackets under a sill, with a geranium mound (Flowers cell)"""
        L=w+.1; y=wall-.13; z=zs-.11
        s.B.part((L,.03,.18),Mt((x,y-.1,z)),"WoodPlank",0,.006*s.bev,1,seed=s.rnd.randint(0,1e6))
        if s.lod<2:
            for sx in (-1,1): s.B.part((.03,.2,.18),Mt((x+sx*L/2,y,z)),"WoodPlank",0,0,1,seed=s.rnd.randint(0,1e6))
            for sx in ((-1,1) if br else ()): s.beam((x+sx*(L/2-.1),wall-.01,z-.32),(x+sx*(L/2-.1),y-.08,z-.09),.05,.05,(sx,0,0),bev=0,warp=0,ends=False)
        s.B.part((L-.02,.2,.04),Mt((x,y,z-.09)),"WoodPlank",0,0,1,seed=s.rnd.randint(0,1e6))
        n=3 if s.lod==0 else 2 if s.lod==1 else 1
        for k in range(n):
            bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1 if s.lod<2 else 1,radius=1)
            cx=x+(k-(n-1)/2)*L/n
            for v in bm.verts: v.co=Vector((cx+v.co.x*L/n*.62,y-.01+v.co.y*.14,z+.08+max(v.co.z,-.3)*.14))
            R.jitter(bm,.015,6,k+3); R.uv_cells(bm,s.A,"Flowers",None,(k*.3,0,0),cut=False); s.B.add(bm)
    def sign(s):
        """hanging shop sign: steel wall plate + bracket arm + brace, painted board (SignBoard cell, icon only: no text to localise)"""
        x,z=getattr(s,"SIGN_X",FR_X+.3),Z0+2.45; y=YF
        s.B.part((.05,.03,.36),Mt((x,y-.015,z+.05)),"Steel",0,0,1,seed=21)
        s.beam((x,y-.02,z+.2),(x,y-.82,z+.2),.035,.035,(0,0,1),cell="Steel",bev=0,warp=0,ends=False)
        if s.lod<2: s.beam((x,y-.02,z-.1),(x,y-.5,z+.19),.025,.025,(1,0,0),cell="Steel",bev=0,warp=0,ends=False)
        bw,bh,bt=.6,.3,.05; yc=y-.47; zc=z+.2-.06-bh/2
        s.B.part((bt,bw,bh),Mt((x,yc,zc)),"WoodPlank",0,.006*s.bev,1,seed=22)
        for sx in (-1,1):
            xs=x+sx*(bt/2+.002); pts=[(xs,yc-bw/2+.01,zc-bh/2+.01),(xs,yc+bw/2-.01,zc-bh/2+.01),(xs,yc+bw/2-.01,zc+bh/2-.01),(xs,yc-bw/2+.01,zc+bh/2-.01)]
            uvs=[(0,0),(1,0),(1,1),(0,1)] if sx<0 else [(1,0),(0,0),(0,1),(1,1)]
            s.quad(pts,"SignBoard",uvs,facing=(sx,0,0))
        if s.lod<2:
            for dy in (-.22,.22): s.B.part((.012,.012,.07),Mt((x,yc+dy,z+.2-.035)),"Steel",0,0,1,seed=23)
    def balcony(s,wall):
        """timber balcony under the right upper window: deck, two brackets, posts, rails, flat balusters, rail flower box"""
        x0,x1=.42,1.98; d=.72; zd=Z1+.78; y0=wall-d
        s.B.part((x1-x0,d,.07),Mt(((x0+x1)/2,wall-d/2,zd)),"WoodPlank",0,.006*s.bev,1,seed=31)
        for xx in (x0+.12,x1-.12): s.beam((xx,wall-.02,zd-.55),(xx,y0+.1,zd-.05),.09,.08,(1,0,0),warp=0)
        hr=.9
        for xx,yy in ((x0+.04,y0+.04),(x1-.04,y0+.04),(x0+.04,wall-.04),(x1-.04,wall-.04)): s.beam((xx,yy,zd+.035),(xx,yy,zd+hr),.07,.07,(0,-1,0),warp=0)
        s.beam((x0,y0+.04,zd+hr),(x1,y0+.04,zd+hr),.09,.06,(0,0,1),warp=0)
        for xx in (x0+.04,x1-.04): s.beam((xx,y0+.04,zd+hr),(xx,wall,zd+hr),.09,.06,(0,0,1),warp=0)
        if s.lod<2:
            s.beam((x0,y0+.04,zd+.12),(x1,y0+.04,zd+.12),.06,.05,(0,0,1),warp=0,ends=False)
            nb=9 if s.lod==0 else 5
            for k in range(nb):
                xx=x0+.12+k*(x1-x0-.24)/(nb-1); s.B.part((.07,.025,hr-.17),Mt((xx,y0+.04,zd+.12+(hr-.17)/2)),"WoodPlank",0,0,1,seed=40+k)
        s.flower_box((x0+x1)/2,y0+.17,zd+hr+.245,.9,br=False)
    def annex(s):
        """lean-to shed: realism-test geometry with thicker plank walls/posts (Physics minimum fragment size 0.15 m)"""
        x0,x1=-XF-.3,-XF-2.1; y0,y1=-1.7,1.5; zt,zl=3.0,2.15; t=.16
        for (a,b,c_,dd) in (((x1,y0),(x1,y1),(-1,0),1),((x1,y0),(x0,y0),(0,-1),0),((x1,y1),(x0,y1),(0,1),0)):
            A_,B_=Vector((*a,0)),Vector((*b,0)); mid=(A_+B_)/2; L=(B_-A_).length
            bm=R.bm_box(t,L,zl,(mid.x+t/2-.03,mid.y,zl/2)) if dd else R.bm_box(L,t,zl,(mid.x,mid.y+(t/2-.03)*(1 if c_[1]<0 else -1),zl/2))
            R.bevel(bm,.006*s.bev,1)
            if not dd:
                for v in bm.verts:
                    if v.co.z>zl-.01: v.co.z=zl+(zt-zl)*(v.co.x-x1)/(x0-x1)
            R.uv_cells(bm,s.A,"WoodPlank",None,(.3,.6,0)); R.tint(bm,(.9,.87,.85)); s.B.add(bm)
        for (px,py) in ((x1,y0),(x1,y1)): s.beam((px,py,0),(px,py,zl+.05),.17,.17,(-1,0,0))
        s.beam((x1,y0-.1,zl),(x1,y1+.1,zl),.17,.16,(-1,0,0))
        if s.lod<2: s.beam((x1,y0+.07,.3),(x1,(y0+y1)/2,zl-.15),.11,.1,(-1,0,0))
        s.B.part((.8,.05,1.8),Mt(((x0+x1)/2,y0-.04,.92)),"WoodPlank",0,.006*s.bev,1,col=(.85,.82,.8),seed=77)
        if s.lod<2:
            for zz in (.35,1.5): s.B.part((.7,.01,.05),Mt(((x0+x1)/2,y0-.07,zz)),"Steel",0,0,1,seed=5)
        ang=math.atan2(zt-zl,x0-x1); e_s=Vector((math.cos(ang),0,math.sin(ang))); n=Vector((-math.sin(ang),0,math.cos(ang)))
        p0=Vector((x1-.35,0,zl-.35*math.tan(ang)+.05)); Ls=(x0-x1+.35)/math.cos(ang); U=y1-y0+.6
        M=Matrix((Vector((0,1,0)),e_s,n)).transposed().to_4x4(); M.translation=p0+e_s*Ls/2-n*.05+Vector((0,(y0+y1)/2,0))
        s.B.part((U,Ls,.1),M,"WoodPlank",0,.01*s.bev,1,seed=88)
        s._tiles_frame(p0+Vector((0,(y0+y1)/2,0)),e_s,n,U,Ls)
        s.mark("Annex")
    def build(s):
        s.plinth(); s.mark("Base")
        s.stone_storey(); s.floor(); s.upper()
        yf=YF-JET; yc=(YB+yf)/2; half=(YB-yf)/2; zr=Z2+half*math.tan(PITCH); s.chim_box=(XF-1.0-.375,XF-1.0+.375,Z2,zr+.9,1)
        s.roof(); s.annex(); return s
CLASS={"Base":None,"Floor":"Wood","Roof_Front":"Roof","Roof_Back":"Roof","Chimney":"Stone","Annex":"Wood"}
def cls_of(m): return CLASS.get(m,"Stone" if m.startswith("StoneWall") else "Wood")
# ======================================================================= assembly / export
def mesh_obj(name,bms,mats):
    me=bpy.data.meshes.new(name); big=bmesh.new(); tmp=bpy.data.meshes.new("tmp")
    for bm in bms: bm.to_mesh(tmp); big.from_mesh(tmp)
    bpy.data.meshes.remove(tmp); bmesh.ops.triangulate(big,faces=[f for f in big.faces if len(f.verts)>3]); big.to_mesh(me); big.free()
    o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
    for m in mats: me.materials.append(m)
    for p in me.polygons: p.use_smooth=True
    me.set_sharp_from_angle(angle=math.radians(40)); return o
def bbox_bms(bms):
    vs=[v.co for bm in bms for v in bm.verts]; return Vector([min(v[i] for v in vs) for i in range(3)]),Vector([max(v[i] for v in vs) for i in range(3)])
def empty(name,loc,parent=None,size=.1):
    e=bpy.data.objects.new(name,None); e.empty_display_size=size; bpy.context.scene.collection.objects.link(e); e.location=loc
    if parent: e.parent=parent
    return e
def export(objs,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',global_scale=1.0,
        bake_space_transform=True,use_custom_props=True,path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='OFF',use_tspace=True,
        colors_type='LINEAR',add_leaf_bones=False,object_types={'EMPTY','MESH'})
def tri(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
# ---------- fracture helpers
def comps(bm):
    """split a bmesh into connected components (copies)"""
    bm.faces.ensure_lookup_table(); seen=set(); out=[]
    for f in bm.faces:
        if f.index in seen: continue
        stack=[f]; isl=[]
        while stack:
            g=stack.pop()
            if g.index in seen: continue
            seen.add(g.index); isl.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: stack.append(h)
        c=bm.copy(); c.faces.ensure_lookup_table(); keep=set(isl)
        bmesh.ops.delete(c,geom=[c.faces[i] for i in range(len(c.faces)) if i not in keep],context='FACES')
        bmesh.ops.delete(c,geom=[v for v in c.verts if not v.link_faces],context='VERTS'); out.append(c)
    return out
def closed(bm): return len(bm.faces)>3 and all(len(e.link_faces)==2 for e in bm.edges)
def half(bm,co,no,keep_neg):
    c=bm.copy(); g=c.verts[:]+c.edges[:]+c.faces[:]
    bmesh.ops.bisect_plane(c,geom=g,plane_co=co,plane_no=no,clear_outer=keep_neg,clear_inner=not keep_neg)
    if not c.faces: c.free(); return None
    bnd=[e for e in c.edges if len(e.link_faces)==1]
    if bnd:
        r=bmesh.ops.holes_fill(c,edges=bnd,sides=0)
        for f in r["faces"]: f.material_index=1; f.tag=True
    if not closed(c): c.free(); return None
    return c
def split_long(bm,L):
    """cut a closed component with axis planes every ~L (staggered courses for walls); caps -> material 1 (inner)"""
    lo,hi=bbox_bms([bm]); ext=hi-lo; pieces=[bm]
    axes=sorted(range(3),key=lambda a:-ext[a])[:2]
    for k,a in enumerate(axes):
        n=int(ext[a]/L+.5)
        if n<2: continue
        nxt=[]
        for p in pieces:
            plo,phi=bbox_bms([p]); cur=p; stag=(L*.35 if (k==1 and int((plo[axes[0]]-lo[axes[0]])/L+.5)%2) else 0)
            cuts=[lo[a]+ext[a]*i/n+stag*(ext[a]/n/L)+.013 for i in range(1,n)]
            for c_ in cuts:
                if not(plo[a]+.15<c_<phi[a]-.15) or cur is None: continue
                no=Vector((0,0,0)); no[a]=1; co=Vector((0,0,0)); co[a]=c_
                A=half(cur,co,no,True); B_=half(cur,co,no,False)
                if A is None or B_ is None: continue
                nxt.append(A); cur=B_
            if cur is not None: nxt.append(cur)
        pieces=nxt
    return pieces
def info(bm):
    if closed(bm) and bm.calc_volume(signed=True)<0: bmesh.ops.reverse_faces(bm,faces=bm.faces[:])
    lo,hi=bbox_bms([bm]); cv=abs(bm.calc_volume()) if closed(bm) else 0.0; vol=abs(bm.calc_volume()) if closed(bm) else sum(f.calc_area() for f in bm.faces)*.012   # open shells (tiles, glass): ~2.4 cm skin
    c=sum((v.co for v in bm.verts),Vector())/max(1,len(bm.verts)); return dict(lo=lo,hi=hi,vol=vol,cv=cv,c=c,t=sum(len(f.verts)-2 for f in bm.faces))
def gap(a,b): return sum(max(0,max(a["lo"][i]-b["hi"][i],b["lo"][i]-a["hi"][i]))**2 for i in range(3))**.5+.05*(a["c"]-b["c"]).length
def merge_info(a,b): return dict(lo=Vector([min(a["lo"][i],b["lo"][i]) for i in range(3)]),hi=Vector([max(a["hi"][i],b["hi"][i]) for i in range(3)]),
                                 vol=a["vol"]+b["vol"],c=(a["c"]*a["vol"]+b["c"]*b["vol"])/max(1e-9,a["vol"]+b["vol"]),t=a["t"]+b["t"],m=a["m"]+b["m"],cv=a["cv"]+b["cv"])
def minax(i): return min((i["hi"]-i["lo"])[k] for k in range(3))
def cluster(items,lo_n,hi_n):
    """items: list of (info, [bm]); merge thin/small pieces into neighbours, then down to the class count range"""
    cl=[dict(i,bms=b) for i,b in items]
    def mrg(i,j):
        a,b=cl[i],cl[j]; n=merge_info(a,b); n["bms"]=a["bms"]+b["bms"]; cl[i]=n; cl.pop(j)
    while True:
        bad=[k for k,c in enumerate(cl) if minax(c)<MINAX+.002 or c["vol"]*DENS_C<.6 or c["cv"]<.003]   # every fragment carries solid volume (COM)
        if not bad or len(cl)<2: break
        k=min(bad,key=lambda k:cl[k]["vol"]); j=min((j for j in range(len(cl)) if j!=k),key=lambda j:gap(cl[k],cl[j])+(1e3 if cl[k]["t"]+cl[j]["t"]>290 else 0))
        mrg(j,k) if j<k else mrg(j,k)
    target=min(hi_n,max(lo_n,len(cl)))
    while len(cl)>target:
        k=min(range(len(cl)),key=lambda k:cl[k]["vol"]); j=min((j for j in range(len(cl)) if j!=k),key=lambda j:gap(cl[k],cl[j])+(1e3 if cl[k]["t"]+cl[j]["t"]>290 else 0))
        mrg(j,k)
    return cl
DENS_C=600
def hull_bm(points,nmax=60):
    pts=list(points)
    if len(pts)>nmax:
        ext=set()
        for a in range(3):
            ext.add(min(range(len(pts)),key=lambda i:pts[i][a])); ext.add(max(range(len(pts)),key=lambda i:pts[i][a]))
        rnd=random.Random(1); rest=[i for i in range(len(pts)) if i not in ext]; rnd.shuffle(rest); pts=[pts[i] for i in list(ext)+rest[:nmax-len(ext)]]
    bm=bmesh.new(); vs=[bm.verts.new(p) for p in pts]; r=bmesh.ops.convex_hull(bm,input=vs)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bmesh.ops.delete(bm,geom=r.get("geom_interior",[])+r.get("geom_unused",[]),context='VERTS') if False else None
    bmesh.ops.triangulate(bm,faces=bm.faces[:]); bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); return bm
# module code (short: Blender/FBX names cap at 63 chars and JNT_<fragA>__<fragB> must fit), label, source tag, optional split plane
# (axis, value, side) so every intact module stays inside the #28 Kit budget of 1500 LOD0 tris
# Level Designer modules (L3): one clearly named assembly each; physics pieces inside stay inside the #28 Kit budget (1500 LOD0 tris,
# <=16/32 fragments, <=1500 fractured tris), so big assemblies are made of 2-8 pieces that all share the assembly pivot.
ASM=[("ShopFront","Shop front: ground-floor street wall with shop door, window, flower box, lantern, hanging sign"),
     ("Wall_Left","Ground-floor stone side wall, left (window)"),("Wall_Right","Ground-floor stone side wall, right (window)"),
     ("Wall_Back","Ground-floor stone back wall (back door + window)"),("Floor","First floor / beam: slab, jetty boards, joists, brackets"),
     ("UpperStorey","Timber-frame upper storey: 4 plaster/timber walls, 2 gables, balcony, flower box"),
     ("Roof_Left","Clay-tile roof, left half (both slopes), seen from the street"),("Roof_Right","Clay-tile roof, right half"),
     ("Chimney","Stone chimney with two clay pots"),("Base","Granite plinth (indestructible)"),("Annex","Lean-to plank annex (optional extra)")]
# piece code (<=6 chars: Blender/FBX names cap at 63 and JNT_<fragA>__<fragB> must fit), label, source tag, split plane (axis,value,side), assembly
MODS=[("Base","Granite plinth (indestructible)","Base",None,"Base"),
 ("ShopW","Shop front, window part + flower box","StoneWall_FrontWin",None,"ShopFront"),("ShopD","Shop front, door part + lantern + hanging sign","StoneWall_FrontDoor",None,"ShopFront"),
 ("BackD","Back wall, door part","StoneWall_BackDoor",None,"Wall_Back"),("BackW","Back wall, window part","StoneWall_BackWin",None,"Wall_Back"),
 ("WallL","Side wall left","StoneWall_Left",None,"Wall_Left"),("WallR","Side wall right","StoneWall_Right",None,"Wall_Right"),
 ("Floor","First floor / beam","Floor",None,"Floor"),
 ("UpFL","Upper front wall, left window + flower box","UpperWall_Front",(0,-.15,-1),"UpperStorey"),("UpFR","Upper front wall, right window","UpperWall_Front",(0,-.15,1),"UpperStorey"),
 ("Balc","Balcony with rail flower box","Balcony",None,"UpperStorey"),
 ("UpB","Upper back wall","UpperWall_Back",None,"UpperStorey"),("UpL","Upper side wall left","UpperWall_Left",None,"UpperStorey"),("UpR","Upper side wall right","UpperWall_Right",None,"UpperStorey"),
 ("GabL","Gable left","Gable_Left",None,"UpperStorey"),("GabR","Gable right","Gable_Right",None,"UpperStorey"),
 ("RoofLF","Roof left half, street slope","Roof_Front",(0,0.0,-1),"Roof_Left"),("RoofLB","Roof left half, back slope (ridge)","Roof_Back",(0,0.0,-1),"Roof_Left"),
 ("RoofRF","Roof right half, street slope","Roof_Front",(0,0.0,1),"Roof_Right"),("RoofRB","Roof right half, back slope (ridge)","Roof_Back",(0,0.0,1),"Roof_Right"),
 ("Chim","Chimney","Chimney",None,"Chimney"),("AnnexF","Annex front half (door)","Annex",(1,-.1,-1),"Annex"),("AnnexB","Annex back half","Annex",(1,-.1,1),"Annex")]
G6=["ShopW","ShopD","BackD","BackW","WallL","WallR"]
ATTACH={"Base":"Ground","AnnexF":"Ground","AnnexB":"Ground",**{m:["Base"] for m in G6},"Floor":G6,
        **{m:["Floor"] for m in ("UpFL","UpFR","UpB","UpL","UpR")},"Balc":["UpFR","Floor"],"GabL":["UpL"],"GabR":["UpR"],
        "RoofLF":["UpFL","GabL"],"RoofRF":["UpFR","GabR"],"RoofLB":["UpB","GabL"],"RoofRB":["UpB","GabR"],"Chim":["RoofRB","UpR"]}
def split_parts(bms,sp):
    """keep the side of plane sp=(axis,value,side): components by centroid; closed components that straddle it are bisected"""
    if not sp: return bms
    if isinstance(sp[0],tuple):   # several planes: keep the intersection of the kept sides
        for q in sp: bms=split_parts(bms,q)
        return bms
    ax,val,sd=sp; out=[]; no=Vector((0,0,0)); no[ax]=1; co=Vector((0,0,0)); co[ax]=val
    for bm in bms:
        for c in comps(bm):
            lo,hi=bbox_bms([c])
            if lo[ax]<val-.08 and hi[ax]>val+.08 and closed(c):
                h=half(c,co,no,sd<0)
                if h is not None:
                    for f in h.faces:
                        if f.material_index==1: f.material_index=0
                    out.append(h); continue
            cen=sum((v.co for v in c.verts),Vector())/len(c.verts)
            if (cen[ax]-val)*sd>=0: out.append(c)
    return out
def com_exact(bms):
    """volume centroid (same formula as validate_colliders.com_local)"""
    V=0; c=Vector()
    for bm in bms:
        t=bm.copy(); bmesh.ops.triangulate(t,faces=t.faces[:])
        for f in t.faces:
            a,b,d=(v.co for v in f.verts); v=a.dot(b.cross(d))/6; V+=v; c+=v*(a+b+d)/4
        t.free()
    return c/V if abs(V)>1e-6 else None
def cap_uv(bm,wood):
    uv=bm.loops.layers.uv.get("UV0"); col=bm.loops.layers.float_color.get("Col")
    for f in bm.faces:
        if f.material_index!=1: continue
        n=f.normal if f.normal.length>.5 else Vector((0,0,1)); t=n.orthogonal().normalized(); b=n.cross(t)
        pts=[(l.vert.co.dot(t),l.vert.co.dot(b)) for l in f.loops]; mu=min(p[0] for p in pts); mv=min(p[1] for p in pts)
        sc=max(max(p[0] for p in pts)-mu,max(p[1] for p in pts)-mv,1e-6)
        for l,p in zip(f.loops,pts):
            l[uv].uv=((.5 if wood else 0)+.01+.48*(p[0]-mu)/sc,.01+.98*(p[1]-mv)/sc)
            if col: l[col]=(1,1,1,1)
def seal(c):
    """close open shells (tiles, pots, glass/sign quads) so fragment volume, mass and centre of mass are well defined:
    fill boundary loops (inner material); a lone quad gets a reversed back face (zero-volume card)"""
    bnd=[e for e in c.edges if len(e.link_faces)==1]
    if not bnd: return
    if len(c.faces)<=2:
        for f in list(c.faces):
            vs=[c.verts.new(v.co) for v in reversed(f.verts)]; g=c.faces.new(vs); g.material_index=f.material_index
            uv=c.loops.layers.uv.get("UV0")
            if uv:
                for la,lb in zip(reversed(f.loops),g.loops): lb[uv].uv=la[uv].uv
        return
    r=bmesh.ops.holes_fill(c,edges=bnd,sides=0)
    for f in r["faces"]: f.material_index=1
    if closed(c): bmesh.ops.recalc_face_normals(c,faces=c.faces[:])   # consistent winding -> correct signed volume
    if not closed(c):   # non-manifold leftovers (overlapping tile lips): back faces make it a zero-volume card for the COM integral
        uv=c.loops.layers.uv.get("UV0")
        for f in [f for f in c.faces if any(len(e.link_faces)!=2 for e in f.edges)]:
            vs=[c.verts.new(v.co) for v in reversed(f.verts)]
            try: g=c.faces.new(vs)
            except ValueError: continue
            g.material_index=1
            if uv:
                for la,lb in zip(reversed(f.loops),g.loops): lb[uv].uv=la[uv].uv
def fracture(mod,bms_src,cls,dens):
    global DENS_C; DENS_C=dens; lo_n,hi_n=RNG[cls]
    L0={"Stone":1.3,"Wood":.9,"Roof":1.0}.get(cls,.8)
    for L in (L0,L0*1.5,L0*2.2,L0*.8,L0*.65,L0*.5,L0*.4):
        items=[]
        for bm in bms_src:
            for c in comps(bm):
                if not closed(c): seal(c)
                lo,hi=bbox_bms([c]); big=max((hi-lo)[k] for k in range(3))
                parts=split_long(c,L) if closed(c) and big>L*1.25 else [c]
                for p in parts:
                    i=info(p); i["m"]=i["vol"]*dens; items.append((i,[p]))
        cl=cluster(items,lo_n,hi_n)
        ok=lo_n<=len(cl)<=hi_n and all(minax(c)>=MINAX for c in cl) and all(c["t"]<300 for c in cl) and sum(c["t"] for c in cl)<=1500
        if ok: return cl
    raise RuntimeError(f"{mod}: fracture failed ({len(cl)} frags, tris {[c['t'] for c in cl]}, minax {[round(minax(c),3) for c in cl]})")
def run():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    houses={l:House(l).build() for l in (0,1,2)}
    for l,h in houses.items(): assert len(h.tags)==len(h.B.parts),(l,len(h.tags),len(h.B.parts))
    mods=[m[0] for m in MODS]; TAG={m[0]:m[2] for m in MODS}; SPL={m[0]:m[3] for m in MODS}; LBL={m[0]:m[1] for m in MODS}
    cls_m=lambda m:cls_of(TAG[m]); stats={"set":"Kit_RealHouse","building":"Bld_Townhouse","modules":{}}; pieces=[]; tri_tot={"LOD0":0,"LOD1":0,"LOD2":0,"Fractured":0}
    origins={}; aabbs={}; ASMOF={m[0]:m[4] for m in MODS}; abox={}
    for mod in mods:   # assembly footprint = union of member LOD0 bounds; pivot = centre of its base footprint (shared by all its pieces)
        lo,hi=bbox_bms([b for b,t in zip(houses[0].B.parts,houses[0].tags) if t==TAG[mod]] if not SPL[mod] else split_parts([b.copy() for b,t in zip(houses[0].B.parts,houses[0].tags) if t==TAG[mod]],SPL[mod]))
        A=ASMOF[mod]; aabbs[mod]=(lo,hi)
        if A in abox: L_,H_=abox[A]; abox[A]=(Vector([min(L_[k],lo[k]) for k in range(3)]),Vector([max(H_[k],hi[k]) for k in range(3)]))
        else: abox[A]=(lo,hi)
    apiv={A:Vector(((l.x+h.x)/2,(l.y+h.y)/2,l.z)) for A,(l,h) in abox.items()}
    for mod in mods:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        M=R.lit_material("M_RealHouse",OUT+"/Textures/T_RealHouse"); MI=R.lit_material("M_RealHouse_Inner",OUT+"/Textures/T_RealHouse_Inner",vcol=False)
        name=f"{KIT}_{mod}"; get=lambda l:split_parts([b.copy() for b,t in zip(houses[l].B.parts,houses[l].tags) if t==TAG[mod]],SPL[mod])
        lo,hi=aabbs[mod]; org=apiv[ASMOF[mod]]; origins[mod]=org
        root=empty(name,(0,0,0)); lods={}; root["fracture_class"]=cls_m(mod) or "None"
        for l in (0,1,2):
            bms=get(l)
            for bm in bms: bmesh.ops.translate(bm,verts=bm.verts,vec=-org)
            o=mesh_obj(f"{name}_LOD{l}",bms,[M]); o.parent=root; o["static"]=True; R.add_uv2(o); lods[l]=tri(o)
        u=mesh_obj(f"UCX_{name}_LOD0_01",[R.bm_box(*(hi-lo),tuple((lo+hi)/2-org))],[]); u.parent=root; u.display_type='WIRE'
        export([o for o in bpy.data.objects],f"{OUT}/{name}.fbx")
        for k in (0,1,2): tri_tot[f"LOD{k}"]+=lods[k]
        ent={"frac":cls_m(mod),"tris":{f"LOD{k}":lods[k] for k in (0,1,2)},"bounds_m":[round(v,3) for v in (hi-lo)]}
        if cls_m(mod):
            cls=cls_m(mod); dens=DENS[cls]
            bpy.ops.wm.read_factory_settings(use_empty=True)
            M=R.lit_material("M_RealHouse",OUT+"/Textures/T_RealHouse"); MI=R.lit_material("M_RealHouse_Inner",OUT+"/Textures/T_RealHouse_Inner",vcol=False)
            for src_l in (1,2):   # LOD1 geometry first, LOD2 when the 1500-tri set budget cannot be met
                src=get(src_l)
                for bm in src: bmesh.ops.translate(bm,verts=bm.verts,vec=-org)
                try: cl=fracture(mod,src,cls,dens); break
                except RuntimeError as e:
                    if src_l==2: raise
                    print("  retry LOD2:",e)
            rootf=empty(name+"_Fractured",(0,0,0)); rootf["fracture_class"]=cls; frags=[]
            wood=cls!="Stone"
            for i,c in enumerate(cl):
                fn=f"{name}_frag_{i+1:02d}"; com=com_exact(c["bms"]) or c["c"]
                for bm in c["bms"]: cap_uv(bm,wood); bmesh.ops.translate(bm,verts=bm.verts,vec=-com)
                ob=mesh_obj(fn,c["bms"],[M,MI]); ob.parent=rootf; ob.location=com; ob["mass_kg"]=round(max(.5,c["m"]),2); ob["density_kg_m3"]=dens; ob["fracture_class"]=cls
                pts=[v.co.copy() for v in ob.data.vertices]; hb=hull_bm(pts)
                u=mesh_obj("UCX_"+fn,[hb],[]); u.parent=rootf; u.location=com; u.display_type='WIRE'; frags.append((ob,c))
            nj=0
            for i in range(len(frags)):
                for j in range(i+1,len(frags)):
                    a,b=frags[i][1],frags[j][1]; e=.03
                    if all(a["lo"][k]-e<=b["hi"][k] and b["lo"][k]-e<=a["hi"][k] for k in range(3)):
                        p=Vector([(max(a["lo"][k],b["lo"][k])+min(a["hi"][k],b["hi"][k]))/2 for k in range(3)])
                        j_=empty(f"JNT_{frags[i][0].name}__{frags[j][0].name}",p,rootf); j_["break_force_N"]=""; j_["break_torque_Nm"]=""; nj+=1
            zmin=min(c["lo"].z for _,c in frags); X0=min(c["lo"].x for _,c in frags); X1=max(c["hi"].x for _,c in frags); Y0=min(c["lo"].y for _,c in frags); Y1=max(c["hi"].y for _,c in frags)
            ground=ATTACH[mod]=="Ground" or mod in G6; na=0
            for ob,c in frags:
                if mod=="Floor": sel=min(c["lo"].x-X0,X1-c["hi"].x,c["lo"].y-Y0,Y1-c["hi"].y)<.3
                else: sel=c["lo"].z<zmin+.08
                if sel:
                    a=empty("ANCHOR_"+ob.name,(c["c"].x,c["c"].y,c["lo"].z),rootf,.15); a["anchor_to"]="Ground" if ground else "Support"; na+=1
            export([o for o in bpy.data.objects],f"{OUT}/{name}_Fractured.fbx")
            ft=[tri(ob) for ob,_ in frags]; ms=[ob["mass_kg"] for ob,_ in frags]; tri_tot["Fractured"]+=sum(ft)
            ent.update({"fragments":len(frags),"frag_source_lod":src_l,"frag_tris_max":max(ft),"frag_tris_total":sum(ft),"mass_kg_total":round(sum(ms),1),
                        "mass_kg_range":[min(ms),max(ms)],"density_kg_m3":dens,"joints":nj,"anchors":na,"min_axis_m":round(min(minax(c) for _,c in frags),3)})
        stats["modules"][mod]=ent; print("BUILT",mod,ent,flush=True)
    # ---------- layout (Unity space: x=-X, y=Z, z=Y)
    # Unity = C @ Blender with C=((-1,0,0),(0,0,1),(0,-1,0)) (same as hero/v3 layouts): street front faces Unity +Z
    U=lambda v:{"x":round(-v.x,4),"y":round(v.z,4),"z":round(-v.y,4)}
    for mod in mods:
        fr=f"Assets/_Project/Models/Realistic/Buildings/Bld_Townhouse/{KIT}_{mod}_Fractured.fbx" if cls_m(mod) else None
        pieces.append({"id":mod,"label":LBL[mod],"module":ASMOF[mod],"kit":f"{KIT}_{mod}","intact":f"Assets/_Project/Models/Realistic/Buildings/Bld_Townhouse/{KIT}_{mod}.fbx","fractured":fr,
            "position":U(origins[mod]),"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0},"euler_zxy_deg":{"x":0.0,"y":0.0,"z":0.0},
            "anchor_to":ATTACH[mod],"fracture_class":cls_m(mod) or "Indestructible","tris":stats["modules"][mod]["tris"]|({"Fractured":stats["modules"][mod]["frag_tris_total"]} if cls_m(mod) else {})})
    modules=[]
    for A,lab in ASM:
        l,h=abox[A]; mem=[m for m in mods if ASMOF[m]==A]; att=sorted({x for m in mem for x in (ATTACH[m] if isinstance(ATTACH[m],list) else [ATTACH[m]]) if x=="Ground" or ASMOF.get(x)!=A})
        att=["Ground"] if att==["Ground"] else sorted({ASMOF.get(x,x) for x in att})
        modules.append({"id":A,"label":lab,"pivot":U(apiv[A]),"pivot_rule":"centre of the base footprint, +Z = street front","rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0},
            "footprint_m":{"x":round(h.x-l.x,3),"z":round(h.y-l.y,3)},"height_m":round(h.z-l.z,3),"pieces":mem,"anchor_to":att,
            "tris":{k:sum(stats["modules"][m]["tris"][k] for m in mem) for k in ("LOD0","LOD1","LOD2")}})
    conns=[]
    for i,a in enumerate(mods):
        for b in mods[i+1:]:
            (la,ha),(lb,hb)=aabbs[a],aabbs[b]; e=.02
            if all(la[k]-e<=hb[k] and lb[k]-e<=ha[k] for k in range(3)):
                p=Vector([(max(la[k],lb[k])+min(ha[k],hb[k]))/2 for k in range(3)])
                conns.append({"a":a,"b":b,"point":U(p),"break_force_N":"","break_torque_Nm":""})
    lay={"format":"crashverse.building_layout","version":1,"building":"Bld_Townhouse","style":"realistic v1","units":"m",
         "space":"Unity (Y up, left-handed); building front (street, shop door) faces +Z; positions local to the building root. Every piece pivot = its module pivot (centre of the module base footprint), so all pieces of a module drop in at the module position",
         "root":{"position":{"x":0.0,"y":0.0,"z":0.0},"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0}},"tris_total":tri_tot,"footprint_m":{"x":round(max(h.x for l,h in abox.values())-min(l.x for l,h in abox.values()),3),"z":round(max(h.y for l,h in abox.values())-min(l.y for l,h in abox.values()),3)},
         "module_count":len(modules),"modules":modules,"piece_count":len(pieces),"pieces":pieces,
         "connections":conns,"unsupported_contacts":[],
         "anchor_note":"fragment ANCHOR_ empties carry anchor_to 'Ground' or 'Support'; for 'Support' use this piece's anchor_to list. Base is indestructible (no fractured set)."}
    json.dump(lay,open(f"{OUT}/Bld_Townhouse.layout.json","w"),indent=1)
    stats["tris_total"]=tri_tot; json.dump(stats,open(STATS,"w"),indent=1); print("TOTAL",tri_tot)
if __name__=="__main__": run()
