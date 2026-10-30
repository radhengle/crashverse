"""Bld_CastleKeep (realistic v1, L9 / Daily Boss arena backdrop): 7.0 m square sandstone keep, 2 storeys, walls split by face and storey,
interior oak floor, corbelled crown, 4 crenellated parapets, terracotta pyramid roof, 4 corbelled corner bartizans (turrets) with cone roofs,
round-arched oak door, cross arrow slits, arched windows, 2 banners. Same builders as Bld_StoneTower (castle_lib), shared T_RealCastle atlas.
usage: blender -b -P keep.py -> Assets/_Project/Models/Realistic/Buildings/Bld_CastleKeep/*"""
import bpy,os,sys,math
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE)
import castle_lib as C
from castle_lib import R,HM,MK
OUT=R.REAL+"/Buildings/Bld_CastleKeep"; KIT="Kit_RealKeep"; STATS=HM.ROOT+"/models/HeroBuildings/Kit_RealKeep_stats.json"
K=3.5; T=.6; Z0=.3; Z1=3.3; Z2=6.0; WO=7.6
FACES={"F":((0,-1),(-K,-K),(K,-K+T)),"B":((0,1),(-K,K-T),(K,K)),"L":((-1,0),(-K,-K+T),(-K+T,K-T)),"R":((1,0),(K-T,-K+T),(K,K-T))}
OPEN={"F1":[("door",0.0,Z0+.02,1.4,2.6),("slit",-2.1,Z0+.9,1.0),("slit",2.1,Z0+.9,1.0)],"B1":[("slit",-1.4,Z0+.9,1.0),("slit",1.4,Z0+.9,1.0)],
      "L1":[("slit",-1.2,Z0+.9,1.0),("slit",1.2,Z0+.9,1.0)],"R1":[("slit",-1.2,Z0+.9,1.0),("slit",1.2,Z0+.9,1.0)],
      "F2":[("win",-2.1,Z1+.55,.6,1.25),("win",2.1,Z1+.55,.6,1.25)],"B2":[("win",-1.4,Z1+.55,.6,1.25),("win",1.4,Z1+.55,.6,1.25)],
      "L2":[("win",0.0,Z1+.55,.6,1.25)],"R2":[("win",0.0,Z1+.55,.6,1.25)]}
TUR={"FL":(-1,-1),"FR":(1,-1),"BL":(-1,1),"BR":(1,1)}
class Keep(C.CK):
    def build_all(s):
        s.block((0,0,Z0/2),(2*K+.5,2*K+.5,Z0),"Granite",.03); s.mark("Base")
        for k,(za,zb) in ((1,(Z0,Z1)),(2,(Z1,Z2))):
            for f,(n,lo,hi) in FACES.items():
                s.wall(n,(lo[0],lo[1],za),(hi[0],hi[1],zb),OPEN[f"{f}{k}"],quoins=f in "FB",putlogs=0)
                if k==1 and s.lod<2:   # string course at the floor line (part of the storey-1 wall)
                    c=[0,0,Z1-.07]; sz=[0,0,.14]; na=0 if n[0] else 1; c[na]=(lo[na]+hi[na])/2+n[na]*.04; c[1-na]=(lo[1-na]+hi[1-na])/2; sz[na]=T+.08; sz[1-na]=hi[1-na]-lo[1-na]
                    s.dressed(tuple(c),tuple(sz),bev=0)
                s.mark(f"W{k}{f}")
            if k==1:   # interior oak floor on 3 beams
                for x in (-1.6,0,1.6): s.beam((x,-K+T,Z1-.32),(x,K-T,Z1-.32),.24,.22,(0,0,1),col=C.OAK)
                s.block((0,0,Z1-.12),(2*K-2*T,2*K-2*T,.08),"WoodPlank",.0,col=C.OAK); s.mark("Floor")
        for a in (-.75,.75): s.banner((0,-1),-K,a,Z2-.06,.6,1.9)
        s.corbel_course(Z2,Wc=2*K,Wo=WO,h=.4,step=.8); s.mark("Crown")
        hw=WO/2; t=.3; zp=Z2+.4; z1=zp+.9; zc=z1-.45
        for f,(n,lo,hi) in {"F":((0,-1),(-hw,-hw,zp),(hw,-hw+t,z1)),"B":((0,1),(-hw,hw-t,zp),(hw,hw,z1)),"L":((-1,0),(-hw,-hw+t,zp),(-hw+t,hw-t,z1)),"R":((1,0),(hw-t,-hw+t,zp),(hw,hw-t,z1))}.items():
            fr,na,ta,sg,wo,tt=s.wf(n,lo,hi); Lf=hi[ta]-lo[ta]; nc=5; cuts=[]
            for i in range(nc):
                a=(lo[ta]+hi[ta])/2-Lf/2+Lf*(i+.5)/nc
                if abs(abs(a)-hw)<1.1: continue   # corners carry the bartizans
                cuts.append((C.prism([(a-.26,zc),(a+.26,zc),(a+.26,z1+.1),(a-.26,z1+.1)],fr,-tt-.1,.1),2))
            s.stone(lo,hi,cuts,"Sandstone",jit=.01); s.mark(f"P{f}")
        s.pyr_roof(0,0,Z2+.4,2*K-.9,3.0,rows=6,tw=.62); s.mark("Roof")
        for c,(sx,sy) in TUR.items():
            cx,cy=sx*(hw-.55),sy*(hw-.55); zb=Z2-.9
            for k,(rr,dz) in enumerate(((.5,.3),(.65,.3))):   # stepped corbel rings under the drum
                s.drum(cx,cy,zb+k*.3,zb+k*.3+dz,rr,rr-.02,(),nseg=10)
            ang=math.atan2(sy,sx)
            s.drum(cx,cy,zb+.6,z1+.3,.8,.22,((ang,zb+1.5,.8),),nseg=14); s.mark(f"T{c}")
            s.cone_roof(cx,cy,z1+.3,.95,1.8,rows=4,tw=.36,nseg=12); s.mark(f"C{c}")
def build(l):
    k=Keep(l); k.build_all(); assert len(k.tags)==len(k.B.parts); return (k.B.parts,k.tags)
LAB={"F":"front (door, banners)","B":"back","L":"left","R":"right"}
def mods():
    M=[("Base","Granite footing (indestructible)","Base",None,"Base")]
    for k in (1,2):
        for f in "FBLR":
            if f"{f}{k}"=="F1":
                M+=[("W1FL","Storey 1 wall, front left half: round-arched oak door (left half) + cross slit, string course","W1F",(0,0.0,-1),"Wall_F1"),
                    ("W1FR","Storey 1 wall, front right half: door (right half) + cross slit, string course","W1F",(0,0.0,1),"Wall_F1")]; continue
            M.append((f"W{k}{f}",f"Storey {k} wall, {LAB[f]}" + (": round-arched oak door + 2 cross slits, string course" if f"{f}{k}"=="F1" else ""),f"W{k}{f}",None,f"Wall_{f}{k}"))
        if k==1: M.append(("Floor","Interior oak floor (3 beams + boards)","Floor",None,"Floor_1"))
    M+=[("CrownL","Corbelled crown course + coping, left half (banner)","Crown",(0,0.0,-1),"Crown"),("CrownR","Corbelled crown course + coping, right half (banner)","Crown",(0,0.0,1),"Crown")]
    M+=[(f"P{f}",f"Crenellated parapet, {LAB[f]}",f"P{f}",None,f"Parapet_{f}") for f in "FBLR"]
    M+=[("RoofA","Terracotta pyramid roof, left half","Roof",(0,0.0,-1),"Roof"),("RoofB","Terracotta pyramid roof, right half","Roof",(0,0.0,1),"Roof")]
    for c in TUR: M+=[(f"T{c}",f"Corner turret {c}: corbelled bartizan drum + slit",f"T{c}",None,f"Turret_{c}"),(f"C{c}",f"Corner turret {c}: terracotta cone roof",f"C{c}",None,f"Turret_{c}")]
    return M
CLS=lambda t:None if t=="Base" else "Wood" if t=="Floor" else "Roof" if t=="Roof" or t[0]=="C" and t!="Crown" else "Stone"
ATT={"Base":"Ground",**{f"W1{f}":["Base"] for f in ("FL","FR","B","L","R")},"Floor":["W1FL","W1FR","W1B","W1L","W1R"],"W2F":["W1FL","W1FR"],**{f"W2{f}":[f"W1{f}"] for f in "BLR"},
     "CrownL":["W2F","W2B","W2L"],"CrownR":["W2F","W2B","W2R"],"PF":["CrownL","CrownR"],"PB":["CrownL","CrownR"],"PL":["CrownL"],"PR":["CrownR"],"RoofA":["CrownL"],"RoofB":["CrownR"],
     "TFL":["CrownL"],"TBL":["CrownL"],"TFR":["CrownR"],"TBR":["CrownR"],**{f"C{c}":[f"T{c}"] for c in TUR}}
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True); os.makedirs(OUT,exist_ok=True)
    P={l:build(l) for l in (0,1,2)}; M=mods(); seen=[]; ASM=[]
    for m in M:
        if m[4] not in seen: seen.append(m[4]); ASM.append((m[4],{"Roof":"Terracotta pyramid roof (2 halves)","Crown":"Corbelled crown course + coping, 2 banners (2 halves)","Wall_F1":"Storey 1 wall, front: round-arched oak door + 2 cross slits (2 halves)"}.get(m[4],m[1].split(":")[0] if m[4].startswith("Turret") else m[1])))
    MK.export_set(dict(kit=KIT,out=OUT,rel="Assets/_Project/Models/Realistic/Buildings/Bld_CastleKeep",building="Bld_CastleKeep",mat="M_RealKeep",
        tex=C.TEXC,inner_tex=C.INNER,stats=STATS,parts=P,MODS=M,ASM=ASM,ATTACH=ATT,cls_of=CLS,ground=("W1FL","W1FR","W1B","W1L","W1R"),
        front="door faces +Z",extra={"texture_set":"Assets/_Project/Models/Realistic/Buildings/RealCastle/Textures/T_RealCastle",
          "level_fit":{"note":"no level places a castle keep as a building yet: the Daily Titan arenas map their boss blocks to Kit_Keep modules, and the realistic boss body is Kit_RealGolem (Bosses/Golem_Ironjaw). Use this keep as the arena backdrop; 7.0 m square body, parapet top 7.3 m, turret finials ~9.9 m, roof ridge 9.4 m."}}))
    C.write_stats_alias(STATS)
