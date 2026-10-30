"""Kit_RealWall (realistic v1 castle wall kit; the asked name Kit_RealCastleWall_ cannot fit the 63-char JNT name cap): tiling sandstone
curtain-wall pieces sized for the L9 seawall (sea0..2 1.0 x 1.5 x 1.4 on a 1.5 m pitch, seaCrest 1.2 wide) and reused by Bld_Gatehouse.
Pieces (each its own module, pivot = centre of its base, length along Unity X, front = Unity +Z):
 Seg 1.5 x 1.0 x 1.5 straight segment | SegSl same with a cross arrow slit | Corner 1.0 x 1.0 x 1.5 corner pier (quoins) |
 Cren 1.5 x 1.2 x 0.7 wall-walk coping + crenellated front parapet (one merlon + one crenel per 1.5 m, tiles) | CrenC 1.2 x 1.2 corner crenellation.
usage: blender -b -P wallkit.py -> Assets/_Project/Models/Realistic/Buildings/Kit_RealWall/*"""
import bpy,os,sys
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE)
import castle_lib as C
from castle_lib import R,HM,MK
OUT=R.REAL+"/Buildings/Kit_RealWall"; KIT="Kit_RealWall"; STATS=HM.ROOT+"/models/HeroBuildings/Kit_RealWall_stats.json"
L,T,H=1.5,1.0,1.5
class Wall(C.CK):
    def segment(s,slit):
        s.wall((0,-1),(-L/2,-T/2,0),(L/2,T/2,H),[("slit",0.0,.35,.85)] if slit else [],putlogs=1)
        s.dressed((0,0,.11),(L,T+.12,.22),bev=.01)   # dressed plinth course, proud both faces
    def corner(s):
        s.wall((0,-1),(-T/2,-T/2,0),(T/2,T/2,H),[],quoins=True); s.dressed((0,0,.11),(T+.12,T+.12,.22),bev=.01)
    def cren(s,corner=False):
        W=1.2 if corner else L; D=1.2
        s.dressed((0,0,.1),(W,D,.2),bev=.01)                                    # coping / wall-walk slab
        y0=-D/2
        if corner:
            s.stone((-W/2,y0,.2),(W/2,y0+.3,.7),(),"Sandstone",jit=.008)           # front merlon
            s.stone((-W/2,y0+.3,.2),(-W/2+.3,D/2,.7),(),"Sandstone",jit=.008)      # side merlon (L-shaped corner)
        else:
            fr,na,ta,sg,wo,t=s.wf((0,-1),(-W/2,y0,.2),(W/2,y0+.3,.7))
            cut=[(C.prism([(.375-.27,.42),(.375+.27,.42),(.375+.27,.8),(.375-.27,.8)],fr,-.5,.1),2)]
            s.stone((-W/2,y0,.2),(W/2,y0+.3,.7),cut,"Sandstone",jit=.008)
def build(l):
    out={}
    for code,fn in (("Seg",lambda w:w.segment(False)),("SegSl",lambda w:w.segment(True)),("Corner",lambda w:w.corner()),("Cren",lambda w:w.cren()),("CrenC",lambda w:w.cren(True))):
        w=Wall(l); fn(w); w.mark(code); out[code]=(w.B.parts,w.tags)
    parts=[];tags=[]
    for k,(p,t) in out.items(): parts+=p; tags+=t
    return (parts,tags)
LBL={"Seg":"Straight wall segment 1.5 x 1.0 x 1.5 (tiles on a 1.5 m pitch)","SegSl":"Straight wall segment with a cross arrow slit",
     "Corner":"Corner pier 1.0 x 1.0 x 1.5 with quoins (joins two runs at 90 deg)","Cren":"Crenellation 1.5 x 1.2 x 0.7 (coping + front merlon/crenel), sits on Seg/SegSl",
     "CrenC":"Corner crenellation 1.2 x 1.2 x 0.7 (L-shaped merlon), sits on Corner"}
if __name__=="__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True); os.makedirs(OUT,exist_ok=True)
    P={l:build(l) for l in (0,1,2)}
    MK.export_set(dict(kit=KIT,out=OUT,rel="Assets/_Project/Models/Realistic/Buildings/Kit_RealWall",building="Kit_RealWall",mat="M_RealWall",
        tex=C.TEXC,inner_tex=C.INNER,stats=STATS,parts=P,MODS=[(c,LBL[c],c,None,c) for c in LBL],ASM=[(c,LBL[c]) for c in LBL],
        ATTACH={"Seg":"Ground","SegSl":"Ground","Corner":"Ground","Cren":["Seg"],"CrenC":["Corner"]},cls_of=lambda t:"Stone",ground=("Seg","SegSl","Corner"),
        front="wall face (slit, merlons) faces +Z; length along X",
        extra={"kit_note":"all pieces are authored at the origin (each its own module): place Seg/SegSl every 1.5 m along local X, Corner at run ends, Cren/CrenC on top (y = 1.5)",
               "texture_set":"Assets/_Project/Models/Realistic/Buildings/RealCastle/Textures/T_RealCastle",
               "level_fit":{"c1_l09_lighthouse_gauntlet":{"yaw_deg":90,"pieces":{"sea0":{"piece":"Seg","position":{"x":-0.6,"y":3.25,"z":1.5}},"sea1":{"piece":"SegSl","position":{"x":-0.6,"y":3.25,"z":3.0}},
                   "sea2":{"piece":"Seg","position":{"x":-0.6,"y":3.25,"z":4.5}},"seaCrest":{"piece":"3 x Cren","positions_z":[1.5,3.0,4.5],"y":4.75}},
                   "note":"segments are 1.5 m long (the level blocks are 1.4 m on a 1.5 m pitch): the kit closes the 0.1 m gaps; crest pieces are 0.7 m tall (block 0.5) because of the merlons"}}}))
    C.write_stats_alias(STATS)
