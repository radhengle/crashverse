"""Bld_Gatehouse (realistic v1, L9 / Daily Boss arena): two Bld_StoneTower stacks (the SAME Kit_RealTower pieces, rotated so the doors face
the castle side; ring2/crown/hoarding/roof left off, the crenellated parapet sits on storey 2) + a round-arched stone gate (Kit_RealGate
ArchL/ArchR halves, voussoirs, banner), an iron-shod oak portcullis (Steel) half raised in its slot, and 2 Kit_RealWall Cren pieces on the
wall walk. Front (field side) = Unity +Z. usage: blender -b -P gatehouse.py  (run tower.py and wallkit.py first)"""
import bpy,os,sys,math,json
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE)
import castle_lib as C
from castle_lib import R,HM,MK
OUT=R.REAL+"/Buildings/Bld_Gatehouse"; KIT="Kit_RealGate"; STATS=HM.ROOT+"/models/HeroBuildings/Kit_RealGate_stats.json"
GX=1.5; GD=1.0; GH=5.5; AW=2.4; AH=3.6; TX=GX+1.3     # gate half width, half depth, height; arch opening; tower centre x
class Gate(C.CK):
    def build_all(s):
        fr,na,ta,sg,wo,t=s.wf((0,-1),(-GX,-GD,0),(GX,GD,GH))
        cut=[(C.prism([(p,z) for p,z in C.arch_prof(AW,AH,12 if s.lod==0 else 8,-.05)],fr,-2*GD-.3,.3),2),
             (C.prism([(-AW/2-.12,-.05),(AW/2+.12,-.05),(AW/2+.12,AH+.12),(-AW/2-.12,AH+.12)],fr,-.62,-.42),1)]   # portcullis slot
        s.stone((-GX,-GD,0),(GX,GD,GH),cut,"Sandstone",jit=.012)
        for face in ((0,-1),(0,1)):
            f2,_,_,_,_,t2=s.wf(face,(-GX,-GD,0),(GX,GD,GH)); s.dress_door(f2,t2,0.0,0.0,AW,AH,leaf=False)
        s.dressed((0,-GD-.05,GH-.55),(2*GX,.12,.14),bev=0)   # string course under the wall walk
        s.banner((0,-1),-GD,0.0,GH-.75,.7,1.25)
        s.mark("Arch")
        # portcullis: oak lattice with iron straps and iron-shod points, half raised (bottom 1.0 m above the road)
        y=-GD+.52; zb=1.0; W=AW+.16; Hh=3.2
        nv=6 if s.lod<2 else 4; nh=6 if s.lod<2 else 3
        for i in range(nv):
            x=-W/2+.06+(W-.12)*i/(nv-1); s.beam((x,y,zb),(x,y,zb+Hh),.11,.11,(0,-1,0),warp=0,col=C.OAK)
            if s.lod<2:
                bm=C.hexa([(x-.05,y-.05,zb),(x+.05,y-.05,zb),(x+.05,y+.05,zb),(x-.05,y+.05,zb),(x-.012,y-.012,zb-.26),(x+.012,y-.012,zb-.26),(x+.012,y+.012,zb-.26),(x-.012,y+.012,zb-.26)])
                R.uv_cells(bm,s.A,"Steel",None,cut=False); s.B.add(bm,C.IRON)
        for j in range(nh):
            z=zb+.25+(Hh-.4)*j/(nh-1); s.beam((-W/2,y-.08,z),(W/2,y-.08,z),.1,.07,(0,-1,0),warp=0,col=C.OAK)
            if s.lod<2: s.B.part((W,.012,.05),Mt((0,y-.12,z)),"Steel",0,0,1,col=C.IRON,seed=j)
        s.mark("Portc")
Mt=C.Mt
def build(l):
    g=Gate(l); g.build_all(); assert len(g.tags)==len(g.B.parts); return (g.B.parts,g.tags)
SKIP=("R2","CrFl","CrWdA","CrWdB","CrRfA","CrRfB"); SKIPM=("ring2","crownFloor","crownWood","crownRoof")
def yaw_u(p,deg):
    t=math.radians(deg); c,s_=math.cos(t),math.sin(t); return {"x":round(p["x"]*c+p["z"]*s_,4),"y":p["y"],"z":round(-p["x"]*s_+p["z"]*c,4)}
def compose(lay,st):
    """append the two reused tower stacks (and the 2 wall-kit crenellations) to the exported gate layout"""
    T=json.load(open(R.REAL+"/Buildings/Bld_StoneTower/Bld_StoneTower.layout.json")); W=json.load(open(R.REAL+"/Buildings/Kit_RealWall/Kit_RealWall.layout.json"))
    YAW=-90.0; q={"x":0.0,"y":round(math.sin(math.radians(YAW)/2),4),"z":0.0,"w":round(math.cos(math.radians(YAW)/2),4)}
    DROP={"CrBk":-2.3,"crownBrick":-2.3}   # parapet straight on storey 2
    tri=lay["tris_total"]
    for side,ux in (("TL",TX),("TR",-TX)):   # Unity x = -Blender x
        for p in T["pieces"]:
            if p["id"] in SKIP: continue
            n=dict(p); pos=yaw_u(p["position"],YAW); pos["x"]=round(pos["x"]+ux,4); pos["y"]=round(pos["y"]+DROP.get(p["id"],0),4)
            n.update({"id":f"{side}_{p['id']}","module":f"{side}_{p['module']}","position":pos,"rotation":q,"euler_zxy_deg":{"x":0.0,"y":YAW,"z":0.0},
                      "anchor_to":p["anchor_to"] if p["anchor_to"]=="Ground" else [f"{side}_{a}" for a in (["W2F","W2B","W2L","W2R"] if p["id"]=="CrBk" else p["anchor_to"])],"reused_from":"Bld_StoneTower"})
            lay["pieces"].append(n)
            for k in ("LOD0","LOD1","LOD2"): tri[k]+=p["tris"][k]
            tri["Fractured"]+=p["tris"].get("Fractured",0)
        for m in T["modules"]:
            if m["id"] in SKIPM: continue
            n=dict(m); pv=yaw_u(m["pivot"],YAW); pv["x"]=round(pv["x"]+ux,4); pv["y"]=round(pv["y"]+DROP.get(m["id"],0),4)
            n.update({"id":f"{side}_{m['id']}","pivot":pv,"rotation":q,"footprint_m":{"x":m["footprint_m"]["z"],"z":m["footprint_m"]["x"]},"top_y_m":round(m["top_y_m"]+DROP.get(m["id"],0),3),
                      "pieces":[f"{side}_{x}" for x in m["pieces"]],"anchor_to":m["anchor_to"] if m["anchor_to"]==["Ground"] else [f"{side}_{a}" for a in (["wallF2","wallB2","wallL2","wallR2"] if m["id"]=="crownBrick" else m["anchor_to"])],
                      "label":("Left" if side=="TL" else "Right")+" tower: "+m["label"],"reused_from":"Bld_StoneTower"})
            lay["modules"].append(n)
        for c in T["connections"]:
            if c["a"] in SKIP or c["b"] in SKIP: continue
            pt=yaw_u(c["point"],YAW); pt["x"]=round(pt["x"]+ux,4); lay["connections"].append({**c,"a":f"{side}_{c['a']}","b":f"{side}_{c['b']}","point":pt})
        for w in ("W2F","W2B","W2L","W2R"): lay["connections"].append({"a":f"{side}_CrBk","b":f"{side}_{w}","point":{"x":ux,"y":5.5,"z":0.0},"break_force_N":"","break_torque_Nm":""})
    cp=[p for p in W["pieces"] if p["id"]=="Cren"][0]; cm=[m for m in W["modules"] if m["id"]=="Cren"][0]
    for i,ux in enumerate((.75,-.75)):
        pos={"x":ux,"y":GH,"z":round(GD-.6,4)}
        lay["pieces"].append({**cp,"id":f"Cren{i+1}","module":"WallWalk","position":pos,"anchor_to":["ArchL","ArchR"],"reused_from":"Kit_RealWall"})
        for k in ("LOD0","LOD1","LOD2"): tri[k]+=cp["tris"][k]
        tri["Fractured"]+=cp["tris"].get("Fractured",0)
        lay["connections"].append({"a":f"Cren{i+1}","b":"ArchL" if ux>0 else "ArchR","point":{"x":ux,"y":GH,"z":round(GD-.6,4)},"break_force_N":"","break_torque_Nm":""})
    lay["modules"].append({**cm,"id":"WallWalk","label":"Wall-walk crenellation on the gate (2 x Kit_RealWall Cren)","pivot":{"x":0.0,"y":GH,"z":round(GD-.6,4)},"footprint_m":{"x":3.0,"z":1.2},"top_y_m":round(GH+cm["height_m"],3),
                           "pieces":["Cren1","Cren2"],"anchor_to":["Arch"],"tris":{k:2*cp["tris"][k] for k in ("LOD0","LOD1","LOD2")},"reused_from":"Kit_RealWall"})
    for side,ux in (("TL",TX),("TR",-TX)):
        lay["connections"].append({"a":"ArchL" if ux>0 else "ArchR","b":f"{side}_W1R","point":{"x":round(ux-math.copysign(1.3,ux),3),"y":1.6,"z":0.0},"break_force_N":"","break_torque_Nm":""})
    lay["piece_count"]=len(lay["pieces"]); lay["module_count"]=len(lay["modules"]); lay["tris_total"]=tri
    lay["footprint_m"]={"x":round(2*TX+2.6,3),"z":2.6}; lay["height_m"]=round(max(m["top_y_m"] for m in lay["modules"]),3)
    lay["reuse_note"]="TL_/TR_ pieces are the Bld_StoneTower FBX (Kit_RealTower_*) placed with yaw -90 (doors face the castle side, -Z); ring2, crownFloor, hoarding and roof are left off and the crenellated parapet (CrBk) sits on storey 2 (y 5.5). Cren1/2 are Kit_RealWall_Cren."
    return lay
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True); os.makedirs(OUT,exist_ok=True)
    P={l:build(l) for l in (0,1,2)}
    lay,st=MK.export_set(dict(kit=KIT,out=OUT,rel="Assets/_Project/Models/Realistic/Buildings/Bld_Gatehouse",building="Bld_Gatehouse",mat="M_RealGate",
        tex=C.TEXC,inner_tex=C.INNER,stats=STATS,parts=P,
        MODS=[("ArchL","Gate arch, left half: round arch with voussoirs both faces, portcullis slot, string course, banner (left half)","Arch",(0,0.0,-1),"Arch"),
              ("ArchR","Gate arch, right half","Arch",(0,0.0,1),"Arch"),("Portc","Portcullis: oak lattice, iron straps and iron-shod points (half raised)","Portc",None,"Portcullis")],
        ASM=[("Arch","Round-arched stone gate between the towers (2 halves)"),("Portcullis","Iron-shod oak portcullis, half raised (Steel)")],
        ATTACH={"ArchL":"Ground","ArchR":"Ground","Portc":["ArchL","ArchR"]},cls_of=lambda t:"Steel" if t=="Portc" else "Stone",ground=("ArchL","ArchR"),
        front="field side faces +Z; passage along Z",extra={"texture_set":"Assets/_Project/Models/Realistic/Buildings/RealCastle/Textures/T_RealCastle",
          "level_fit":{"c1_l09_lighthouse_gauntlet":"the seawall (sea0..2 + seaCrest) uses Kit_RealWall (see its layout level_fit); the gatehouse is the castle-gate prefab for the arena / later levels: 8.2 m wide, 2.6 m deep, parapet top 6.3 m, 2.4 x 3.6 m passage"}}))
    lay=compose(lay,st); json.dump(lay,open(OUT+"/Bld_Gatehouse.layout.json","w"),indent=1); print("GATE TOTAL",lay["tris_total"])
    C.write_stats_alias(STATS)
