"""Bld_Bakery (L3), realistic v1: modular + destructible, same pipeline as Bld_Townhouse (house_modular + modkit) in a bakery colourway
(T_RealBakery: darker timber, cream plaster, red/cream striped awning cell). 8 m (X) x 6 m (Z), 2 storeys, eaves 6.3 m, ridge ~8.5 m, chimney ~10 m.
usage: blender -b -P bakery.py"""
import bpy,bmesh,math,os,sys,json
from mathutils import Vector,Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE); sys.path.insert(0,os.path.join(HERE,"..","realism"))
import townhouse as TH, house_modular as HM, modkit as MK
from house_modular import R
G=dict(XF=4.0,YF=-3.0,YB=3.0,Z0=.35,Z1=3.5,Z2=6.3,JET=.3,PITCH=math.radians(34))
for m in (TH,HM):
    for k,v in G.items(): setattr(m,k,v)
XF,YF,YB,Z0,Z1,Z2,JET=(G[k] for k in ("XF","YF","YB","Z0","Z1","Z2","JET")); Mt=TH.Mt
OUT=R.REAL+"/Buildings/Bld_Bakery"; REL="Assets/_Project/Models/Realistic/Buildings/Bld_Bakery"; TH.TEX=OUT; HM.FR_X=0.2
CHIM=(-1.4,1.55,1.1,.8)   # cx, cy (back slope), w, d : oven flue at the back
class Bakery(HM.House):
    OPEN_G=[("shop",(0,-1),-1.8,3.0,1.75),("door",(0,-1),1.2,1.1,2.3),("win",(1,0),-.3,.9,1.25),("win",(-1,0),.3,.9,1.25),
            ("win",(0,1),-2.0,.9,1.25),("door",(0,1),2.2,1.0,2.15)]
    OPEN_U=[((0,-1),-2.6,.9,1.25),((0,-1),0.0,.9,1.25),((0,-1),2.6,.9,1.25),((0,1),-1.6,.9,1.25),((0,1),1.6,.9,1.25),((1,0),0.0,.9,1.25),((-1,0),0.0,.9,1.25)]
    joist=.6; LANTERN_X=2.25; SIGN_X=3.05; TILE=(.4,.52,.43)   # larger pantiles than the townhouse (budget + visual difference)
    def __init__(s,lod): super().__init__(lod); s.tile_seg0=2
    def sill(s,kind): return {"door":.02,"shop":.5}.get(kind,1.05)
    def opening_frame(s,side,c,w,h,z0,depth,wall,kind,shut=True,lin="SandDressed"):
        if kind!="shop": return super().opening_frame(side,c,w,h,z0,depth,wall,kind,shut,lin)
        # wide display window: oak bressummer lintel, stone sill, 4 panes with glazing bars, stall-board below
        y=wall; fd=y+depth-.05 if False else y+.17
        s.beam((c-w/2-.25,y-.05,z0+h+.12),(c+w/2+.25,y-.05,z0+h+.12),.24,.16,(0,-1,0))
        s.B.part((w+.24,.18,.09),Mt((c,y-.02,z0-.045)),"SandDressed",0,.02*s.bev,1,jit=.003,seed=5)
        fw=.08; yy=y+.15
        for x in (c-w/2+fw/2,c+w/2-fw/2): s.beam((x,yy,z0),(x,yy,z0+h),fw,.08,(0,-1,0),bev=0,warp=0,ends=False)
        s.beam((c-w/2,yy,z0+fw/2),(c+w/2,yy,z0+fw/2),fw,.08,(0,0,1),bev=0,warp=0,ends=False)
        s.beam((c-w/2,yy,z0+h-fw/2),(c+w/2,yy,z0+h-fw/2),fw,.08,(0,0,1),bev=0,warp=0,ends=False)
        n=4; bars=[c-w/2+fw+(w-2*fw)*i/n for i in range(n+1)]
        if s.lod<2:
            for x in bars[1:-1]: s.beam((x,yy-.005,z0+fw),(x,yy-.005,z0+h-fw),.05,.06,(0,-1,0),bev=0,warp=0,ends=False)
            s.beam((c-w/2+fw,yy-.005,z0+h*.72),(c+w/2-fw,yy-.005,z0+h*.72),.05,.06,(0,0,1),bev=0,warp=0,ends=False)
        for a0,a1 in zip(bars[:-1],bars[1:]):
            s.quad([(a0,yy+.01,z0+fw),(a1,yy+.01,z0+fw),(a1,yy+.01,z0+h-fw),(a0,yy+.01,z0+h-fw)],"Glass",[(.1,.1),(.9,.1),(.9,.9),(.1,.9)],facing=(0,-1,0))
        if s.lod<2:   # display shelf with loaves behind the glass
            s.B.part((w-.2,.35,.05),Mt((c,yy+.3,z0+.35)),"WoodPlank",0,0,1,seed=7)
            for i in range(7 if s.lod==0 else 4):
                bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1,radius=1); xx=c-w/2+.35+i*(w-.7)/((7 if s.lod==0 else 4)-1)
                for v in bm.verts: v.co=Vector((xx+v.co.x*.15,yy+.3+v.co.y*.09,z0+.42+max(v.co.z,-.4)*.08))
                R.uv_cells(bm,s.A,"SandDressed",None,(i*.3,0,0),cut=False); R.tint(bm,(1.0,.66,.36)); s.B.add(bm)
    def awning(s,c,w,ztop,proj=.95,drop=.45):
        """striped canvas awning on steel arms: sloped panel + scalloped-look valance (Stripe cell, explicit UVs)"""
        y=YF; z1=ztop-drop; y1=y-proj
        for t_ in (0,.025):
            pass
        pts=[(c-w/2,y-.02,ztop),(c+w/2,y-.02,ztop),(c+w/2,y1,z1),(c-w/2,y1,z1)]
        bm=bmesh.new(); vs=[bm.verts.new(p) for p in pts]; vb=[bm.verts.new((p[0],p[1],p[2]-.03)) for p in pts]
        faces=[bm.faces.new(vs),bm.faces.new(vb[::-1])]
        for i in range(4): j=(i+1)%4; faces.append(bm.faces.new((vs[j],vs[i],vb[i],vb[j])))
        bm.normal_update(); bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
        R.uv_fixed(bm,s.A,"Stripe",bm.faces[:],lambda L:((L.vert.co.x-(c-w/2))/w,.1+.8*(L.vert.co.y-y1)/max(.01,(y-.02-y1))))
        s.B.add(bm,(1,1,1))
        bm=R.bm_box(w,.025,.22,(c,y1-.012,z1-.11)); R.uv_fixed(bm,s.A,"Stripe",bm.faces[:],lambda L:((L.vert.co.x-(c-w/2))/w,.5+.4*(L.vert.co.z-(z1-.22))/.22)); s.B.add(bm)
        for x in (c-w/2+.08,c+w/2-.08):
            s.beam((x,y-.02,ztop-.75),(x,y1+.03,z1-.02),.035,.035,(1,0,0),cell="Steel",bev=0,warp=0,ends=False)
        s.beam((c-w/2,y-.03,ztop+.02),(c+w/2,y-.03,ztop+.02),.07,.06,(0,0,1),cell="WoodBeam",bev=0,warp=0,ends=False)
    def ground_charm(s,name):
        if name=="StoneWall_FrontDoor": s.lantern(); s.sign()
        if name=="StoneWall_FrontWin": s.awning(-1.8,3.5,Z0+.5+1.75+.42)
    def upper_frames(s,yf):
        """posts at the corners and both sides of every window, braces in the two end bays"""
        out={}
        for name,side,wall,a0,a1 in (("UpperWall_Front",(0,-1),yf-.01,-XF-.02,XF+.02),("UpperWall_Back",(0,1),YB+.01,-XF-.02,XF+.02),
                                      ("UpperWall_Right",(1,0),XF+.02,yf-.02,YB+.02),("UpperWall_Left",(-1,0),-XF-.02,yf-.02,YB+.02)):
            ws=sorted(c for sd,c,w,h in s.OPEN_U if sd==side); posts=[a0+.09]
            for c in ws: posts+=[c-.45-.09,c+.45+.09]
            posts.append(a1-.09); br=[]
            if ws and ws[0]-.54-(a0+.09)>.9: br.append((a0+.2,ws[0]-.65))
            if ws and (a1-.09)-(ws[-1]+.54)>.9: br.append((a1-.2,ws[-1]+.65))
            out[name]=(side,wall,a0,a1,posts,br)
        return out
    def upper_charm(s,name,yf):
        if name=="UpperWall_Front":
            for c in (-2.6,2.6): s.flower_box(c,yf-.01,Z1+.85,.95)
    def upper_extra(s,name,yf): pass
    def chimney(s,yc,zr):
        cx,cy,w,d=CHIM; top=zr+1.3
        bm=R.bm_box(w,d,top-Z1+.2,(cx,cy,(top+Z1-.2)/2)); R.bevel(bm,.025*s.bev,1)
        R.uv_cells(bm,s.A,"Sandstone",None,(.1,.2,.3))
        if s.lod==0: R.jitter(bm,.007,2,9)
        s.B.add(bm)
        s.block((cx,cy,top+.05),(w+.14,d+.14,.1),"SandDressed",.02,.003)
        if s.lod<2:
            for k,dx in enumerate((-.25,.25)):
                bm=bmesh.new(); seg=8 if s.lod==0 else 6; rr=.1; h=.34
                bot=[bm.verts.new((cx+dx+rr*math.cos(2*math.pi*i/seg),cy+rr*math.sin(2*math.pi*i/seg),top+.1)) for i in range(seg)]
                tp=[bm.verts.new((cx+dx+rr*.8*math.cos(2*math.pi*i/seg),cy+rr*.8*math.sin(2*math.pi*i/seg),top+.1+h)) for i in range(seg)]
                for i in range(seg): j=(i+1)%seg; bm.faces.new((bot[i],bot[j],tp[j],tp[i]))
                bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); R.uv_cells(bm,s.A,"Terracotta",None,cut=False); R.tint(bm,(.95,.92,.9)); s.B.add(bm)
    def build(s):
        s.plinth(); s.mark("Base"); s.stone_storey(); s.floor(); s.upper()
        yf=YF-JET; yc=(YB+yf)/2; zr=Z2+(YB-yf)/2*math.tan(G["PITCH"]); cx,cy,w,d=CHIM
        s.chim_box=(cx-w/2,cx+w/2,Z2,zr+1.3,1); s.roof(); return s
ASM=[("ShopFront","Shop front: stone street wall with wide display window, striped awning, door, lantern, hanging loaf sign"),
     ("Wall_Left","Ground-floor stone side wall, left (window)"),("Wall_Right","Ground-floor stone side wall, right (window)"),
     ("Wall_Back","Ground-floor stone back wall (back door + window)"),("Floor","First-floor slab / beam with jetty joists (rests on the ground walls)"),
     ("UpperStorey","Timber-frame upper storey resting on the Floor slab: 4 walls, 2 gables, flower boxes"),
     ("Roof_Left","Clay-tile roof, left half (both slopes), seen from the street"),("Roof_Right","Clay-tile roof, right half"),
     ("Chimney","Oven chimney at the back (stone, two pots)"),("Base","Granite plinth (indestructible)")]
SPLIT_UF=((0,-1.3,-1),)
MODS=[("Base","Granite plinth","Base",None,"Base"),
 ("ShopW","Shop front, display window + awning","StoneWall_FrontWin",None,"ShopFront"),("ShopD","Shop front, door + lantern + sign","StoneWall_FrontDoor",None,"ShopFront"),
 ("WallL","Side wall left","StoneWall_Left",None,"Wall_Left"),("WallR","Side wall right","StoneWall_Right",None,"Wall_Right"),
 ("BackD","Back wall, door part","StoneWall_BackDoor",None,"Wall_Back"),("BackW","Back wall, window part","StoneWall_BackWin",None,"Wall_Back"),
 ("Floor","First-floor slab","Floor",None,"Floor"),
 ("UpFL","Upper front wall, left bay","UpperWall_Front",(0,-1.3,-1),"UpperStorey"),("UpFC","Upper front wall, centre bay","UpperWall_Front",((0,-1.3,1),(0,1.3,-1)),"UpperStorey"),
 ("UpFR","Upper front wall, right bay","UpperWall_Front",(0,1.3,1),"UpperStorey"),("UpB","Upper back wall","UpperWall_Back",None,"UpperStorey"),
 ("UpL","Upper side wall left","UpperWall_Left",None,"UpperStorey"),("UpR","Upper side wall right","UpperWall_Right",None,"UpperStorey"),
 ("GabL","Gable left","Gable_Left",None,"UpperStorey"),("GabR","Gable right","Gable_Right",None,"UpperStorey"),
 ("RfLF","Roof left half, street slope","Roof_Front",(0,0.0,-1),"Roof_Left"),("RfLB","Roof left half, back slope + ridge","Roof_Back",(0,0.0,-1),"Roof_Left"),
 ("RfRF","Roof right half, street slope","Roof_Front",(0,0.0,1),"Roof_Right"),("RfRB","Roof right half, back slope + ridge","Roof_Back",(0,0.0,1),"Roof_Right"),
 ("Chim","Oven chimney","Chimney",None,"Chimney")]
G6=["ShopW","ShopD","BackD","BackW","WallL","WallR"]
ATTACH={"Base":"Ground",**{m:["Base"] for m in G6},"Floor":G6,**{m:["Floor"] for m in ("UpFL","UpFC","UpFR","UpB","UpL","UpR")},"GabL":["UpL"],"GabR":["UpR"],
        "RfLF":["UpFL","UpFC","GabL"],"RfRF":["UpFC","UpFR","GabR"],"RfLB":["UpB","GabL"],"RfRB":["UpB","GabR"],"Chim":["RfLB","UpB","Floor"]}
PROPS_DIR="Assets/_Project/Models/Realistic/Props/Props_Village"
def props_layout():
    U=MK.U; out=[]
    for asset,(x,y,rz) in (("Prop_BreadCrate",(-2.9,-4.35,.08)),("Prop_BreadCrate",(-2.05,-4.4,-.12)),("Prop_BreadCrate",(-2.5,-4.38,.3)),("Prop_Bench",(-.75,-4.15,0.0))):
        z=.0 if asset!="Prop_BreadCrate" or len([o for o in out if o["asset"].endswith("BreadCrate.fbx")])<2 else .42
        q=Matrix.Rotation(rz,3,'Z').to_quaternion()
        out.append({"asset":f"{PROPS_DIR}/{asset}.fbx","position":U(Vector((x,y,z))),"rotation":{"x":0.0,"y":round(-q.z,4),"z":0.0,"w":round(q.w,4)},"note":"street side, indestructible prop (shared Props_Village atlas)"})
    return out
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    P={}
    for l in (0,1,2):
        h=Bakery(l).build(); assert len(h.tags)==len(h.B.parts); P[l]=(h.B.parts,h.tags)
    MK.export_set(dict(kit="Kit_RealBakery",out=OUT,rel=REL,building="Bld_Bakery",mat="M_RealBakery",tex=OUT+"/Textures/T_RealBakery",
        inner_tex=R.REAL+"/Buildings/Bld_Townhouse/Textures/T_RealHouse_Inner",stats=HM.ROOT+"/models/HeroBuildings/Kit_RealBakery_stats.json",
        parts=P,MODS=MODS,ASM=ASM,ATTACH=ATTACH,cls_of=HM.cls_of,ground=G6,perim=("Floor",),front="shop front faces +Z (Level Designer rotates the root to face -X)",
        extra={"props":props_layout(),"dimensions_m":{"walls_x":8.0,"walls_z":6.0,"eaves":Z2,"ridge":round(Z2+(YB-YF+JET)/2*math.tan(G['PITCH'])+.1,2)}}))
