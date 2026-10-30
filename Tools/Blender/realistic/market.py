"""L8 market kit, realistic v1: Kit_RealStall (market stall + separate wooden keystone skid it rests on; awning colour variants A/B = same mesh,
UV offset between the two stripe cells of T_RealStall, one material) and Bld_MarketArch (10.4 m timber arch framing 4 stalls on a 2.2 m pitch,
3.5 m clear, steel sign box without text). Same pipeline/conventions as Bld_Townhouse (house_modular + modkit). usage: blender -b -P market.py"""
import bpy,bmesh,math,os,sys,json
from mathutils import Vector,Matrix
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE); sys.path.insert(0,os.path.join(HERE,"..","realism"))
import townhouse as TH, house_modular as HM, modkit as MK
from house_modular import R
Mt=TH.Mt; KDIR=R.REAL+"/Buildings/Kit_RealStall"; ADIR=R.REAL+"/Buildings/Bld_MarketArch"; TH.TEX=KDIR
STATS=HM.ROOT+"/models/HeroBuildings/Kit_RealStall_stats.json"; INNER=R.REAL+"/Buildings/Bld_Townhouse/Textures/T_RealHouse_Inner"
SW,SD,KH=1.6,1.8,.22     # stall width (X), depth (Y), keystone height; stall top ~2.4 m above ground
class Market(HM.House):
    def cloth(s,pts,cell="Stripe",th=.025):
        """awning cloth panel (closed thin slab) with stripes across X (explicit UVs in the stripe cell)"""
        bm=bmesh.new(); vs=[bm.verts.new(p) for p in pts]; vb=[bm.verts.new((p[0],p[1],p[2]-th)) for p in pts]
        bm.faces.new(vs); bm.faces.new(vb[::-1])
        for i in range(4): j=(i+1)%4; bm.faces.new((vs[j],vs[i],vb[i],vb[j]))
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]); x0=min(p[0] for p in pts); x1=max(p[0] for p in pts)
        y0=min(p[1] for p in pts); y1=max(p[1] for p in pts)
        R.uv_fixed(bm,s.A,cell,bm.faces[:],lambda L:(.02+.96*(L.vert.co.x-x0)/(x1-x0),.1+.8*(L.vert.co.y-y0)/max(.01,y1-y0))); s.B.add(bm); return bm
    def stall(s):
        z0=KH; x=SW/2; yf=-SD/2; yb=SD/2
        # corner posts (front 2.0 m, back 2.18 m above the skid)
        for px,py,h in ((-x+.05,yf+.05,1.95),(x-.05,yf+.05,1.95),(-x+.05,yb-.05,2.15),(x-.05,yb-.05,2.15)):
            s.beam((px,py,z0),(px,py,z0+h),.09,.09,(0,-1,0),warp=.004)
        # counter: plank front panel, side panels, top board with overhang, shelf
        s.B.part((SW-.1,.04,.86),Mt((0,yf+.1,z0+.45)),"WoodPlank",0,.006*s.bev,1,seed=1)
        for sx in (-1,1): s.B.part((.04,.5,.86),Mt((sx*(x-.07),yf+.33,z0+.45)),"WoodPlank",0,.006*s.bev,1,seed=2+sx)
        s.B.part((SW+.06,.62,.05),Mt((0,yf+.27,z0+.905)),"WoodPlank",0,.006*s.bev,1,seed=4)
        s.B.part((SW-.14,.45,.04),Mt((0,yf+.33,z0+.35)),"WoodPlank",0,0,1,seed=5)
        if s.lod<2:
            for k in range(3 if s.lod==0 else 2):   # counter front battens
                s.beam((-x+.12,yf+.07,z0+.15+k*.3),(x-.12,yf+.07,z0+.15+k*.3),.07,.025,(0,-1,0),cell="WoodPlank",bev=0,warp=0,ends=False)
        # back rail + rear shelf, side rails
        s.beam((-x+.05,yb-.05,z0+1.1),(x-.05,yb-.05,z0+1.1),.08,.06,(0,1,0))
        s.B.part((SW-.2,.35,.04),Mt((0,yb-.25,z0+1.12)),"WoodPlank",0,0,1,seed=6)
        for sx in (-1,1):
            s.beam((sx*(x-.05),yf+.05,z0+1.9),(sx*(x-.05),yb-.05,z0+2.1),.08,.06,(sx,0,0))
        s.beam((-x+.05,yf+.05,z0+1.9),(x-.05,yf+.05,z0+1.9),.08,.06,(0,-1,0))
        if s.lod<2:   # produce on the counter (crate of loaves look: reuse planks + sand-dressed loaves) and two baskets
            for i in range(5 if s.lod==0 else 3):
                bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1,radius=1); xx=-x+.3+i*(SW-.6)/((5 if s.lod==0 else 3)-1)
                for v in bm.verts: v.co=Vector((xx+v.co.x*.12,yf+.25+v.co.y*.08,z0+.97+max(v.co.z,-.4)*.07))
                R.uv_cells(bm,s.A,"SandDressed",None,(i*.3,0,0),cut=False); R.tint(bm,(1.0,.64,.34)); s.B.add(bm)
        s.mark("Stall")
        # awning: sloped striped cloth from the back top to beyond the front + front valance
        zb,zf=z0+2.18,z0+1.98; yo=yf-.35
        s.cloth([(-x-.05,yb,zb),(x+.05,yb,zb),(x+.05,yo,zf-.12),(-x-.05,yo,zf-.12)])
        bm=R.bm_box(SW+.1,.025,.2,(0,yo-.012,zf-.12-.1)); R.uv_fixed(bm,s.A,"Stripe",bm.faces[:],lambda L:(.02+.96*(L.vert.co.x+x+.05)/(SW+.1),.5)); s.B.add(bm)
        if s.lod<2:
            for sx in (-1,1): s.beam((sx*(x-.05),yf+.05,zf-.02),(sx*(x-.05),yo+.03,zf-.13),.04,.04,(sx,0,0),bev=0,warp=0,ends=False)
        s.mark("Stall")
    def keystone(s):
        """wooden skid the stall rests on: 3 bearers + deck planks (Physics keystone: removing it drops the stall)"""
        for xx in (-SW/2+.12,0,SW/2-.12): s.beam((xx,-SD/2,.07),(xx,SD/2,.07),.14,.16,(0,0,1),cell="WoodBeam")
        n=6 if s.lod<2 else 3
        for k in range(n):
            y=-SD/2+SD*(k+.5)/n; s.B.part((SW,SD/n-.02,.05),Mt((0,y,KH-.03)),"WoodPlank",1,.006*s.bev,1,seed=10+k)
        s.mark("Keystone")
    # ---------------- arch
    def arch(s):
        X=5.2; P=.4; CL=3.5; BH=.36
        for sx in (-1,1):
            s.block((sx*(X-P/2),0,.1),(P+.3,P+.3,.2),"Granite",.02)
            s.mark("Pads")
        for sx,tag in ((-1,"PostL"),(1,"PostR")):
            xc=sx*(X-P/2)
            s.beam((xc,0,.2),(xc,0,CL+BH+.1),P,P,(0,-1,0),warp=.006)
            if s.lod<2:
                s.B.part((P+.04,P+.04,.08),Mt((xc,0,.26)),"Steel",0,0,1,seed=3)   # steel shoe band
                s.beam((xc-sx*P/2,0,2.75),(sx*(X-P-1.0),0,CL+.02),.16,.18,(0,-1,0))   # knee brace into the beam
            s.mark(tag)
        s.beam((-X-.25,0,CL+BH/2),(X+.25,0,CL+BH/2),BH,.32,(0,-1,0),warp=.008)
        s.beam((-X-.35,0,CL+BH+.06),(X+.35,0,CL+BH+.06),.12,.42,(0,-1,0),cell="WoodPlank")
        if s.lod<2:
            for xx in (-3.3,-1.1,1.1,3.3): s.B.part((.06,.34,.14),Mt((xx,0,CL+.03)),"Steel",0,0,1,seed=4)   # lantern hooks over the stall slots
        s.mark("Beam")
        # steel sign box on two posts standing on the beam (no text: plain painted steel panel with a gold border)
        zb=CL+BH+.12; w,h,d=2.6,.7,.16
        bm=R.bm_box(w,d,h,(0,0,zb+.25+h/2)); R.bevel(bm,.01*s.bev,1); R.uv_cells(bm,s.A,"Steel",None,cut=False); R.tint(bm,(.55,.62,.58)); s.B.add(bm)
        if s.lod<2:
            for sy in (-1,1):
                for (cx,cz,ww,hh) in ((0,zb+.25+h-.04,w-.04,.04),(0,zb+.25+.04,w-.04,.04),(-w/2+.04,zb+.25+h/2,.04,h-.04),(w/2-.04,zb+.25+h/2,.04,h-.04)):
                    s.B.part((ww,.012,hh),Mt((cx,sy*(d/2+.004),cz)),"Gold",0,0,1,seed=5)
        for xx in (-.9,.9): s.beam((xx,0,zb),(xx,0,zb+.3),.08,.08,(0,-1,0),cell="Steel",bev=0,warp=0,ends=False)
        s.mark("Sign")
def build(fn,l):
    m=Market(l); fn(m); assert len(m.tags)==len(m.B.parts); return (m.B.parts,m.tags)
def uv_to_B(atlas):
    a=atlas.rect("Stripe"); b=atlas.rect("StripeB"); du,dv=b[0]-a[0],b[1]-a[1]
    def f(bm):
        uv=bm.loops.layers.uv.get("UV0")
        for fc in bm.faces:
            for L in fc.loops:
                u,v=L[uv].uv
                if a[0]-1e-4<=u<=a[2]+1e-4 and a[1]-1e-4<=v<=a[3]+1e-4: L[uv].uv=(u+du,v+dv)
    return f
CLS={"Stall":"Wood","Keystone":"Wood","PostL":"Wood","PostR":"Wood","Beam":"Wood","Sign":"Steel","Pads":None}
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if os.path.exists(STATS): os.remove(STATS)
    A=R.Atlas(KDIR+"/cells.json")
    P={l:build(lambda m:(m.keystone(),m.stall()),l) for l in (0,1,2)}
    lay,_=MK.export_set(dict(kit="Kit_RealStall",out=KDIR,rel="Assets/_Project/Models/Realistic/Buildings/Kit_RealStall",building="Kit_RealStall",mat="M_RealStall",
        tex=KDIR+"/Textures/T_RealStall",inner_tex=INNER,stats=STATS,parts=P,merge_stats=True,
        MODS=[("Keystn","Keystone: wooden skid the stall rests on (separate prop piece)","Keystone",None,"Keystone"),
              ("StallA","Market stall, red/cream awning (variant A)","Stall",None,"Stall"),
              ("StallB","Market stall, blue/cream awning (variant B: same mesh, UV offset to the StripeB cell)","Stall",None,"Stall",uv_to_B(A))],
        ASM=[("Stall","Market stall: counter, corner posts, rails, striped cloth awning (choose StallA or StallB)"),("Keystone","Wooden keystone skid under the stall")],
        ATTACH={"Keystn":"Ground","StallA":["Keystn"],"StallB":["Keystn"]},cls_of=lambda t:CLS[t],ground=("Keystn",),front="counter front faces +Z"))
    for p in lay["pieces"]:
        if p["id"]=="StallB": p["variant_of"]="StallA"; p["default"]=False
        if p["id"]=="StallA": p["default"]=True; p["variants"]=["StallB"]
    lay["variant_note"]="StallA/StallB: identical geometry and tris, one material M_RealStall; the awning UVs sit in the Stripe (red) or StripeB (blue) atlas cell. Place exactly one of them on the Keystone."
    json.dump(lay,open(KDIR+"/Kit_RealStall.layout.json","w"),indent=1)
    P={l:build(lambda m:m.arch(),l) for l in (0,1,2)}
    U=MK.U; slots=[{"slot":i+1,"position":U(Vector((x,0,0))),"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0},"piece":"Kit_RealStall (Keystn + StallA/StallB)"} for i,x in enumerate((-3.3,-1.1,1.1,3.3))]
    MK.export_set(dict(kit="Kit_RealStall",out=ADIR,rel="Assets/_Project/Models/Realistic/Buildings/Bld_MarketArch",building="Bld_MarketArch",mat="M_RealStall",
        tex=KDIR+"/Textures/T_RealStall",inner_tex=INNER,stats=STATS,parts=P,merge_stats=True,
        MODS=[("Pads","Granite post pads (indestructible)","Pads",None,"Base"),("PostL","Arch post left + knee brace + steel shoe","PostL",None,"Post_Left"),
              ("PostR","Arch post right + knee brace + steel shoe","PostR",None,"Post_Right"),("Beam","Arch beam (10.4 m) + cap board + lantern hooks","Beam",None,"Beam"),
              ("Sign","Steel sign box (no text) on two steel posts","Sign",None,"Sign")],
        ASM=[("Base","Granite post pads (indestructible)"),("Post_Left","Timber post, left"),("Post_Right","Timber post, right"),("Beam","Timber beam spanning 10.4 m, 3.5 m clear"),("Sign","Steel sign, no text")],
        ATTACH={"Pads":"Ground","PostL":["Pads"],"PostR":["Pads"],"Beam":["PostL","PostR"],"Sign":["Beam"]},cls_of=lambda t:CLS[t],ground=("PostL","PostR"),
        front="arch plane is X/Y; +Z = market front",extra={"stall_slots":slots,"clear_span_m":9.6,"clear_height_m":3.5,"pitch_m":2.2,"overall_span_m":10.4}))
