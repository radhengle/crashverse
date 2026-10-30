"""Bld_StoneTower (realistic v1, L7 m1_clockwork_tower 1:1 / L9 lighthouse scaled): square sandstone watchtower, 2 stone storeys on
iron-bound oak ring beams, corbelled crown, timber hoarding with loopholes, crenellated parapet, terracotta pyramid roof, banners.
Every module = one level block id of m1_clockwork_tower (PR #57 levels/prefabs/PREFAB_MAP.md), sized and placed so the building root at the
plinth top (Unity 0,1.2,0) lands each module on its block. Storeys rest on the module below (ANCHOR Support) so a hit topples the upper part.
usage: blender -b -P tower.py  -> Assets/_Project/Models/Realistic/Buildings/Bld_StoneTower/*"""
import bpy,os,sys,json
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE)
import castle_lib as C
from castle_lib import R,HM,MK
OUT=R.REAL+"/Buildings/Bld_StoneTower"; KIT="Kit_RealTower"; STATS=HM.ROOT+"/models/HeroBuildings/Kit_RealTower_stats.json"
W=2.6; T=.35; H=W/2
Z={"ring0":0.0,"s1":.5,"ring1":2.75,"s2":3.25,"ring2":5.5,"crownFloor":6.0,"crownWood":6.4,"crownBrick":7.8,"crownRoof":8.6}
WALLS={  # level id: outward normal (Blender), lo, hi (xy)   Blender +X = Unity -X = level 'F' side (door)
 "F":((1,0),(H-T,-H),(H,H)),"B":((-1,0),(-H,-H),(-H+T,H)),"L":((0,1),(-H+T,H-T),(H-T,H)),"R":((0,-1),(-H+T,-H),(H-T,-H+T))}
OPEN={"F1":[("door",0.0,.52,.9,1.85)],"B1":[("slit",0.0,1.2,.85)],"L1":[("slit",.25,1.3,.85)],"R1":[("slit",-.25,1.3,.85)],
      "F2":[("slit",0.0,3.85,.95)],"B2":[("slit",0.0,3.85,.95)],"L2":[("slit",-.2,3.95,.95)],"R2":[("slit",.2,3.95,.95)]}
class Tower(C.CK):
    def storey(s,k):
        z0,z1=(Z["s1"],Z["ring1"]) if k==1 else (Z["s2"],Z["ring2"])
        for f,(n,lo,hi) in WALLS.items():
            s.wall(n,(lo[0],lo[1],z0),(hi[0],hi[1],z1),OPEN[f"{f}{k}"],quoins=f in "FB",putlogs=2)
            if f=="F" and k==2:
                for a in (-.72,.72): s.banner((1,0),H,a,z1-.08,.56,1.45)   # banners on iron hooks under the ring beam
            s.mark(f"wall{f}{k}")
    def steps(s):   # granite steps up to the door sill (own indestructible piece: the door is 0.5 m above the plinth top)
        s.block((H+.2,0,.125),(.42,1.2,.25),"Granite",.015); s.block((H+.08,0,.375),(.2,1.1,.25),"Granite",.015)
    def build_all(s):
        s.steps(); s.mark("doorSteps")
        s.ring(Z["ring0"]); s.mark("ring0")
        s.storey(1)
        s.ring(Z["ring1"]); s.mark("ring1")
        s.storey(2)
        s.ring(Z["ring2"]); s.mark("ring2")
        s.corbel_course(Z["crownFloor"],step=.52)
        s.mark("crownFloor")
        s.hoarding(Z["crownWood"]); s.mark("crownWood")
        s.parapet(Z["crownBrick"]); s.mark("crownBrick")
        s.pyr_roof(0,0,Z["crownRoof"],2.6,1.6); s.mark("crownRoof")
def build(l):
    t=Tower(l); t.build_all(); assert len(t.tags)==len(t.B.parts); return (t.B.parts,t.tags)
CLS=lambda tag:None if tag=="doorSteps" else "Wood" if tag.startswith("ring") or tag=="crownWood" else "Roof" if tag=="crownRoof" else "Stone"
LABEL={"F":"front (door side)","B":"back","L":"left","R":"right"}
def mods():
    M=[("Steps","doorSteps: granite steps up to the door sill (indestructible, not a level block)","doorSteps",None,"doorSteps"),
       ("R0","ring0: iron-bound oak ring beam on the plinth","ring0",None,"ring0")]
    for k in (1,2):
        for f in "FBLR":
            what={"F1":"round-arched oak door, dressed voussoirs, iron straps","F2":"cross arrow slit, 2 red/gold banners on iron hooks"}.get(f"{f}{k}","cross arrow slit")
            M.append((f"W{k}{f}",f"wall{f}{k}: storey {k} stone wall, {LABEL[f]}: {what}, quoins" if f in "FB" else f"wall{f}{k}: storey {k} stone wall, {LABEL[f]}: {what}",f"wall{f}{k}",None,f"wall{f}{k}"))
        M.append((f"R{k}",f"ring{k}: iron-bound oak ring beam + floor",f"ring{k}",None,f"ring{k}"))
    M+=[("CrFl","crownFloor: corbelled stone course + coping","crownFloor",None,"crownFloor"),
        ("CrWdA","crownWood: timber hoarding with loopholes, front half","crownWood",(0,0.0,1),"crownWood"),
        ("CrWdB","crownWood: timber hoarding with loopholes, back half","crownWood",(0,0.0,-1),"crownWood"),
        ("CrBk","crownBrick: crenellated stone parapet","crownBrick",None,"crownBrick"),
        ("CrRfA","crownRoof: terracotta pyramid roof, front half (+finial)","crownRoof",(0,0.0,1),"crownRoof"),
        ("CrRfB","crownRoof: terracotta pyramid roof, back half","crownRoof",(0,0.0,-1),"crownRoof")]
    return M
ATT={"Steps":"Ground","R0":"Ground","W1F":["R0"],"W1B":["R0"],"W1L":["R0"],"W1R":["R0"],"R1":["W1F","W1B","W1L","W1R"],
     "W2F":["R1"],"W2B":["R1"],"W2L":["R1"],"W2R":["R1"],"R2":["W2F","W2B","W2L","W2R"],"CrFl":["R2"],"CrWdA":["CrFl"],"CrWdB":["CrFl"],"CrBk":["CrWdA","CrWdB"],"CrRfA":["CrBk"],"CrRfB":["CrBk"]}
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True); os.makedirs(OUT,exist_ok=True)
    P={l:build(l) for l in (0,1,2)}
    M=mods(); ASM=[(m[4],m[1].split(": ",1)[1] if ": " in m[1] else m[1]) for m in M if m[0] not in ("CrRfB","CrWdB")]
    ASM=[(a,("terracotta pyramid roof (2 halves)" if a=="crownRoof" else "timber hoarding with loopholes (2 halves)" if a=="crownWood" else l)) for a,l in ASM]
    lay,st=MK.export_set(dict(kit=KIT,out=OUT,rel="Assets/_Project/Models/Realistic/Buildings/Bld_StoneTower",building="Bld_StoneTower",mat="M_RealTower",
        tex=C.TEXC,inner_tex=C.INNER,stats=STATS,parts=P,MODS=M,ASM=ASM,ATTACH=ATT,cls_of=CLS,ground=("R0",),
        front="door (wallF1) faces Unity -X like m1_clockwork_tower; module ids = level block ids",
        extra={"texture_set":"Assets/_Project/Models/Realistic/Buildings/RealCastle/Textures/T_RealCastle (shared castle atlas; cells.json beside it)",
               "level_fit":{"m1_clockwork_tower":{"root_position":{"x":0.0,"y":1.2,"z":0.0},"scale":1.0,"note":"each module id = block id; module footprint/height = block size (crownFloor 3.2, crownWood 2.8, crownBrick 2.4, crownRoof 2.6 x 1.6, walls F/B 0.35x2.25x2.6, L/R 1.85x2.25x0.35, rings 2.6x0.5)"},
                            "c1_l09_lighthouse_gauntlet":{"root_position":{"x":1.0,"y":3.2,"z":-3.0},"scale":{"x":0.85,"y":0.89,"z":0.85},"map":{"lhRing":"ring0","lhShaft0":"storey 1 walls","lhShaft1":"storey 2 walls (scale 0.77 x/z)","lhGallery":"crownFloor","lhLantern":"crownWood","lhCap":"crownRoof"},"note":"lighthouse shafts are 2.2/2.0 m with 2 m storeys; non-uniform scale on the same modules (no separate set)"}}}))
    C.write_stats_alias(STATS)
