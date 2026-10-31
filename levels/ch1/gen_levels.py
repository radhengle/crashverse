#!/usr/bin/env python3
"""Authors Ch1 L1-L10 layouts (LayoutData JSON, Developer's ContentModels.cs schema) + LevelEntry list.
Run: python3 gen_levels.py  -> layouts/*.json, chapter1_levels.json. Camera assumed on +z side (occlusion in sim)."""
import json, os
HERE=os.path.dirname(os.path.abspath(__file__))
def B(id,mat,p,s,sup=(),joint=False,role="",nonObj=False,color="",indestructible=False):
    return dict(id=id,mat=mat,p=list(p),s=list(s),sup=list(sup),joint=joint,role=role,nonObj=nonObj,color=color,
                explosive=False,indestructible=indestructible,spinAxis=[],pivot=[],spinDps=0.0)
RB="Assets/_Project/Models/Realistic/Buildings/"
def MOD(b,folder,kit,pieces):
    """rev 9: dress a block with a realistic module (integration/v2 loader: BlockData.parts spawned at the block's
    base-footprint centre, 1:1, yawed by rotY). rotY -90 turns the module's +Z street/front to face the cannon (-x).
    The block box is centred on the module pivot (x/z) and starts at the pivot height, so the parts land where
    3D Modeler placed them (plus any level lift)."""
    b=dict(b); b["parts"]=[f"{RB}{folder}/{kit}_{p}.fbx" for p in pieces]; b["rotY"]=-90.0; b["modelScale"]=[1.0,1.0,1.0]; return b
ROCK=lambda id,p,s: B(id,"brick",p,s,nonObj=True,indestructible=True,color="8A8A8A",role="island")

def L1():  # EASY "Seagull Perch": T-shaped cliff lookout on ONE crooked post; side cabins + bell ride the deck
    return [ROCK("rock",(0,.6,0),(4,1.2,4)),
      B("post","wood",(0,2.2,.6),(.5,2.0,.5),["rock"],role="support"),
      B("deck","wood",(0,3.4,0),(6,.4,2.4),["post"]),
      B("cabinL","wood",(-2.1,4.2,0),(1.6,1.2,1.8),["deck"],role="wall"),
      B("cabinR","wood",(2.1,4.2,0),(1.6,1.2,1.8),["deck"],role="wall"),
      B("hut","wood",(0,4.4,0),(2.0,1.6,2.0),["deck"],role="wall"),
      B("roof","wood",(0,5.5,0),(2.6,.6,2.6),["hut"],role="roof"),   # big, tempting, useless (decoy)
      B("bellFrame","wood",(0,6.4,0),(.8,1.2,.3),["roof"]),
      B("bell","wood",(0,6.3,.25),(.4,.5,.4),["bellFrame"]),
      B("flag","wood",(2.1,5.3,0),(.1,1.0,.1),["cabinR"],nonObj=True,color="E5484D")]

def L2():  # EASY "Leaning Water Tower": stilts stand on TWO sills (rev 6: L1 already taught the one-support shot;
    # L2 teaches "find every support"); a catwalk links the tank to a pump shed -> domino
    b=[ROCK("rock",(0,.6,0),(9,1.2,4)),
       B("sillL","wood",(-1.2,1.4,.2),(1.1,.4,2.8),["rock"],role="support"),
       B("sillR","wood",(1.2,1.4,.2),(1.1,.4,2.8),["rock"],role="support")]
    for i,(x,z) in enumerate([(-1.2,-1),(1.2,-1),(-1.2,1),(1.2,1)]):
        b.append(B(f"stilt{i}","wood",(x,3.0,z),(.35,2.8,.35),["sillL" if x<0 else "sillR"],role="stilt"))
    b+=[B("frame","wood",(0,4.6,0),(3,.4,2.6),[f"stilt{i}" for i in range(4)]),
        B("tankLo","wood",(0,5.4,0),(2.4,1.2,2.4),["frame"],role="tank"),
        B("tankHi","wood",(0,6.6,0),(2.4,1.2,2.4),["tankLo"],role="tank"),
        B("tankLid","wood",(0,7.4,0),(2.6,.4,2.6),["tankHi"],role="tank"),
        B("catwalk","wood",(2.9,4.6,0),(2.8,.3,1.0),["frame"]),
        B("shedPost","wood",(4.3,2.9,0),(.3,3.2,.3),["catwalk"]),    # shed hangs off the catwalk (stilted over the cliff)
        B("shed","wood",(4.3,4.0,-1.2),(1.6,1.4,1.4),["catwalk"]),
        B("shedRoof","wood",(4.3,5.0,-1.2),(1.8,.4,1.6),["shed"])]
    return b

def L3():  # MEDIUM "Two-Storey Bakery" (rev 9): 3D Modeler's Bld_Bakery (realistic-v1 @ a24f5ec, Kit_RealBakery), 1:1.
    # Walls 8 x 6 m, eaves 6.30, ridge 8.66, shop front (+Z) faces the cannon after face_wrecker (parts yawed -90).
    # One block per LD module, supports = module anchor_to (= the 69 contacts), materials per fracture class
    # (Stone -> brick, Wood -> wood, Roof pantile -> brick). Base = indestructible, non-objective, held.
    # The rev 8 timber cellar stays (Base -> two wood props -> two brick slabs -> the four ground walls, lift C = 1.8 m):
    # it is the only layer not in the model and it keeps L3's two-prop beat. Every module box is centred on its module
    # pivot and starts at the pivot height (+C) so the loader's parts land in place; boxes are trimmed at the next
    # module's pivot so no two cubes overlap (ground walls 0.34-2.535, Floor 2.535-3.495, UpperStorey 3.495-5.78,
    # roof halves 5.78-8.66). The chimney's pivot (3.30) sits inside the Floor/UpperStorey/roof, so its block is the
    # stub above the roof with no parts (generic RealPieceRules brick; gap reported to Developer).
    # The street props (props[]: three bread crates, one stacked, and a bench) stand on the street in front of the
    # shop. crateL and the stacked crateTop are the decoy (ground mass that never falls with the house: props alone
    # = 93%, 3 stars); the >=95% mastery needs one crate shot on top of the props (3 shots, as rev 8's awning post).
    # crateR and the bench stay indestructible dressing as 3D Modeler authored them (crateTop also rests on crateR).
    C=1.8; F="Bld_Bakery"; K="Kit_RealBakery"
    def H(id,mat,cx,cz,w,d,y0,y1,sup,pieces=None,**k):   # box centred on (cx, cz), base y0 (house-local), lifted by C
        b=B(id,mat,(cx,round((y0+y1)/2+C,4),cz),(w,round(y1-y0,4),d),sup,**k)
        return MOD(b,F,K,pieces) if pieces else b
    b=[MOD(B("Base","brick",(0,.175,.15),(8.64,.35,6.84),nonObj=True,indestructible=True,color="8A8A8A",role="island"),F,K,["Base"]),
       B("propL","wood",(2.0,1.045,2.6),(.45,1.39,.45),["Base"],role="prop"),     # house-left = screen-left
       B("propR","wood",(-2.0,1.045,2.6),(.45,1.39,.45),["Base"],role="prop"),
       B("slabL","brick",(2.0555,1.9415,.182),(4.111,.4,6.586),["propL"]),
       B("slabR","brick",(-2.0555,1.9415,.182),(4.111,.4,6.586),["propR"])]
    S=["slabL","slabR"]; g1=2.5351; gf=g1-.001; gs=g1-.003   # 1-3 mm clearances (no touching-face overlaps)
    b+=[H("ShopFront","brick",.0007,3.1747,8.0,.6,.3417,gf,S,["ShopW","ShopD"],role="wall"),   # window+awning, door+loaf sign
        H("Wall_Left","brick",3.8271,.0017,.34,5.08,.3458,gs,S,["WallL"],role="wall"),
        H("Wall_Right","brick",-3.828,-.0012,.34,5.08,.3438,gs,S,["WallR"],role="wall"),
        H("Wall_Back","brick",.0008,-2.828,7.998,.566,.3417,gs,S,["BackD","BackW"],role="wall"),
        H("Floor","wood",0,.368,8.02,5.876,g1,3.4944,["ShopFront","Wall_Back","Wall_Left","Wall_Right"],["Floor"]),
        H("UpperStorey","wood",-.0005,.2269,8.262,6.696,3.4954,5.7815,["Floor"],
          ["UpFL","UpFC","UpFR","UpB","UpL","UpR","GabL","GabR"],role="wall"),
        H("Roof_Left","brick",2.205,.1498,4.41,7.316,5.7825,8.66,["UpperStorey"],["RfLF","RfLB"],role="roof",color="B5523B"),
        H("Roof_Right","brick",-2.235,.1501,4.47,7.315,5.7825,8.66,["UpperStorey"],["RfRF","RfRB"],role="roof",color="B5523B"),
        H("Chimney","brick",1.4005,-1.5496,1.0,.8,8.661,10.165,["Floor","Roof_Left","UpperStorey"])]
    # street props (props[]), on the street at y 0 in front of the Base (bakery-local z 4.15-4.4)
    b+=[B("crateL","wood",(2.9,.21,4.35),(.7,.42,.5),[],role="prop"),
        B("crateR","wood",(2.05,.21,4.4),(.7,.42,.5),[],nonObj=True,indestructible=True,color="8B6B4A"),
        B("crateTop","wood",(2.5,.63,4.38),(.7,.42,.5),["crateL","crateR"],role="prop"),
        B("bench","wood",(.75,.225,4.15),(1.4,.45,.45),[],nonObj=True,indestructible=True,color="8B6B4A")]
    for i,c in enumerate(b[-4:]):
        b[len(b)-4+i]=dict(c,parts=["Assets/_Project/Models/Realistic/Props/Props_Village/"+("Prop_Bench.fbx" if c["id"]=="bench" else "Prop_BreadCrate.fbx")],
                           rotY=round(-90.0+{"crateL":-4.6,"crateR":6.9,"crateTop":-17.2}.get(c["id"],0.0),1),modelScale=[1.0,1.0,1.0])
    # 1:1 at the prop's base centre (props[] positions and yaws); crate box .7 x .42 x .5 and bench 1.4 x .45 x .45 are
    # LD estimates (props[] carries no bounds; the stack height .42 is from the stacked crate's y)
    return b

def L4():  # windmill: brick drums on wooden ring-beams; hitting low beams chains everything above
    b=[ROCK("rock",(0,.6,0),(5,1.2,5)),
       B("drum0","brick",(0,2.2,0),(2.8,2.0,2.8),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("beamL","wood",(-1.2,3.4,1.3),(1.0,.4,.6),["drum0"],role="support"),
       B("beamR","wood",(1.2,3.4,1.3),(1.0,.4,.6),["drum0"],role="support"),
       B("drum1F","brick",(0,4.6,.6),(2.4,2.0,1.2),["beamL","beamR"],role="wall"),
       B("drum1B","brick",(0,4.6,-.6),(2.4,2.0,1.2),["beamL","beamR"],role="wall"),
       B("cap","wood",(0,5.8,1.2),(2.4,.4,.4),["drum1F","drum1B"],role="support"),
       B("drum2","brick",(0,7.0,0),(2.0,2.0,2.0),["cap"],role="wall"),
       B("hub","wood",(0,7.4,1.3),(.8,.8,.6),["drum2"]),
       B("roof","brick",(0,8.4,0),(2.4,.8,2.4),["drum2"]),
       B("annexBeam","wood",(2.3,5.8,0),(2.2,.4,1.2),["drum1F","drum1B"]),
       B("annex","brick",(2.8,6.6,0),(1.4,1.2,1.4),["annexBeam"]),
       B("annexRoof","wood",(2.8,7.4,0),(1.6,.4,1.6),["annex"])]
    sails=[((0,8.6),(.5,1.6)),((0,6.2),(.5,1.6)),((-1.2,7.4),(1.6,.5)),((1.2,7.4),(1.6,.5))]
    for i,((x,y),(w,h)) in enumerate(sails): b.append(B(f"sail{i}","wood",(x,y,1.8),(w,h,.1),["hub"]))
    return b

def L5():  # twin cottages on one bridge: the bridge plank is the shared support
    # rev 6: 10.6 m wide (was 13 m) so both rocks sit inside the fixed behind-cannon frame
    b=[ROCK("rockL",(-4.1,.6,0),(2.4,1.2,3)),ROCK("rockR",(4.1,.6,0),(2.4,1.2,3)),
       B("pierL","brick",(-2.9,2.0,0),(.8,1.6,.8),["rockL"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("pierR","brick",(2.9,2.0,0),(.8,1.6,.8),["rockR"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("bridge","wood",(0,3.0,0),(6.6,.4,2.0),["pierL","pierR"],role="bridge")]
    for side,x in (("L",-1.75),("R",1.75)):
        b+=[B(f"floor{side}","wood",(x,3.4,0),(2.2,.4,2),["bridge"]),
            B(f"wallF{side}","brick",(x,4.2,.85),(2.0,1.2,.3),[f"floor{side}"],role="wall"),
            B(f"wallB{side}","brick",(x,4.2,-.85),(2.0,1.2,.3),[f"floor{side}"],role="wall"),
            B(f"roof{side}","wood",(x,5.1,0),(2.4,.6,2.2),[f"wallF{side}",f"wallB{side}"],role="roof")]
    b+=[B("wheelAxle","wood",(0,2.4,.9),(.4,.8,.4),["bridge"]),          # water-wheel hangs under the bridge: falls with it
        B("wheel","wood",(0,1.6,1.2),(1.8,1.8,.3),["wheelAxle"]),
        B("lanterns","wood",(0,4.6,0),(1.2,.2,.2),["roofL","roofR"])]       # string between roofs: 2nd domino
    return b

def L6():  # MEDIUM "Grain Silo": tall 3-drum brick silo on two gold steel braces over stone piers; side hopper rides the deck
    b=[ROCK("rock",(0,.6,0),(7,1.2,4))]
    for s_,x in (("L",-1.4),("R",1.4)):
        b+=[B(f"col{s_}","brick",(x,2.4,.9),(.6,2.4,.6),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
            B(f"brace{s_}","steel",(x,3.85,.9),(.9,.5,.9),[f"col{s_}"],joint=True,role="steel_joint")]
    # rev 6: role steel_joint (mastery ALL_ROLE_BROKEN counts role steel_joint; "brace" made the goal impossible).
    # braceM now hangs under the deck (was bolted to the rock, so it never fell and capped the best line at 87%).
    b+=[B("braceM","steel",(0,3.85,-.9),(.9,.5,.9),["deck"],joint=True,role="steel_joint"),
        B("deck","brick",(0,4.3,0),(3.6,.4,3),["braceL","braceR"]),
        B("drum0","brick",(0,5.5,0),(2.6,2.0,2.6),["deck"],role="wall"),
        B("drum1","brick",(0,7.5,0),(2.4,2.0,2.4),["drum0"],role="wall"),
        B("drum2","brick",(0,9.3,0),(2.2,1.6,2.2),["drum1"],role="wall"),
        B("dome","steel",(0,10.4,0),(1.8,.6,1.8),["drum2"]),
        B("chute","brick",(2.3,5.2,0),(1.0,1.4,1.0),["deck"]),
        B("hopper","brick",(2.3,6.3,0),(1.4,.8,1.4),["chute"])]
    return b

def L7():  # HARD "Clockwork Watchtower" (rev 7, PM overrode the M1 layout lock to fix the spike).
    # Re-skinned to 3D Modeler's Bld_Watchtower proportions (ring storeys, crown, spire roof, annex shed, barrels and
    # crates) at ~0.55 scale so it fits the fixed portrait frame. The wood scaffold that hid ring0 is gone: the gold
    # base ring is now in plain sight and is the hint. Ground-level annex/crates/barrel/poles do NOT fall with the tower
    # (8.5 of 49 objective mass), so ring0 alone leaves 82.7% (short). ring0 + the annex or the crate stack (the two
    # heavy ground pieces) wins for 3 stars; ring0 + a single pole/barrel does not, two of them do (3 shots = 2 stars).
    # Front storey walls are 2.6 m wide and cover the side walls, so the shootable set is smaller and readable.
    b=[B("plinth","brick",(0,.6,0),(6.8,1.2,4.5),nonObj=True,indestructible=True,color="8A8A8A",role="island"),
       B("ring0","steel",(0,1.45,0),(2.6,.5,2.6),["plinth"],joint=True,role="steel_joint")]
    jy=[1.45,4.2,6.95]
    for i in (1,2):
        y0=jy[i-1]+.25; y1=jy[i]-.25; m=(y0+y1)/2; h=y1-y0; below=f"ring{i-1}"
        w=[B(f"wallF{i}","brick",(0,m,1.1),(2.6,h,.35),[below],role="wall"),
           B(f"wallB{i}","brick",(0,m,-1.1),(2.6,h,.35),[below],role="wall"),
           B(f"wallL{i}","brick",(-1.1,m,0),(.35,h,1.85),[below],role="wall"),
           B(f"wallR{i}","brick",(1.1,m,0),(.35,h,1.85),[below],role="wall")]
        b+=w; b.append(B(f"ring{i}","steel",(0,jy[i],0),(2.6,.5,2.6),[x["id"] for x in w],joint=True,role="steel_joint"))
    b+=[B("crownFloor","brick",(0,7.4,0),(3.2,.4,3.2),["ring2"],role="crown"),
        B("crownWood","wood",(0,8.3,0),(2.8,1.4,2.8),["crownFloor"],role="crown"),
        B("crownBrick","brick",(0,9.4,0),(2.4,.8,2.4),["crownWood"],role="crown"),
        B("crownRoof","brick",(0,10.6,0),(2.6,1.6,2.6),["crownBrick"],role="crown",color="4D7BC4"),   # spire roof
        B("annex","brick",(2.5,2.0,-.4),(1.6,1.6,1.8),["plinth"],role="annex"),        # Watchtower Annex shed
        B("wingL","wood",(-1.9,2.6,1.4),(.3,2.8,.3),["plinth"],role="wing"),           # timber hoist poles
        B("wingR","wood",(1.9,2.6,1.4),(.3,2.8,.3),["plinth"],role="wing"),
        B("crate","wood",(-2.7,1.6,.6),(.8,.8,.8),["plinth"],role="prop"),
        B("crateTop","wood",(-2.7,2.3,.6),(.6,.6,.6),["crate"],role="prop"),
        B("barrel","wood",(-2.7,1.8,-.8),(.7,1.2,.7),["plinth"],role="prop"),
        B("barrel2","wood",(3.15,1.8,1.3),(.7,1.2,.7),["plinth"],role="prop"),
        B("barrelDecor","wood",(4.0,.6,2),(.8,1.2,.8),[],nonObj=True,color="E5484D")]
    return b

L8_PITCH=float(__import__("os").environ.get("L8_PITCH","1.6"))
def L8():  # BREATHER "Royal Market Arcade" (rev 9): 4 Kit_RealStall stalls under 3D Modeler's Bld_MarketArch (a24f5ec; sign refit 8c99d49), 1:1.
    # Each stall stands on its own Keystn (key0-3, the objective keystones, wood), which spans the quay pier gap
    # either side of it; a broken keystone drops its stall 1.06 m to the quay (lands >= 4 m/s -> CollapseRules breaks it).
    # The arch (Pads -> PostL/PostR -> Beam -> steel Sign) frames the row and is non-objective: the posts/beam are a
    # wasted shot and the steel Sign is the trap (steel joint, 2x on a direct hit but it is not the target).
    # Slot pitch: the arch's stall_slots are 2.2 m apart (x +/-1.1, +/-3.3) but Multi Shot's +/-8 deg split spreads
    # only ~1.25 m either side at this range, so key0-2 at 2.2 m cannot be taken by one split (checked with
    # ci_win_shots.first_hit at both gravities). The stalls stand at 1.6 m (x +/-0.8, +/-2.4, the rev 6 keystone
    # spacing, 3.2 m key0-key2 < the 3.5 m split window), centred in the arch; 3D Modeler asked to re-slot.
    P=L8_PITCH; xs=[(i-1.5)*P for i in range(4)]
    b=[ROCK("rock",(0,.6,0),(10.4,1.2,4)),
       MOD(B("pads","brick",(0,1.3,0),(10.7,.2,.7),["rock"],nonObj=True,indestructible=True,color="8A8A8A",role="island"),"Bld_MarketArch","Kit_RealStall",["Pads"])]
    # quay piers (stone, indestructible, under the keystone joints): one between neighbours, one under each outer end
    px=[xs[0]-P/2+.2]+[(xs[i]+xs[i+1])/2 for i in range(3)]+[xs[3]+P/2-.2]
    for i,x in enumerate(px):
        b.append(B(f"pier{i}","brick",(round(x,4),1.82,0),(.4,.84,1.0),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"))
    for i,x in enumerate(xs):
        x=round(x,4)
        b+=[MOD(B(f"key{i}","wood",(x,2.35,0),(1.6,.22,1.8),[f"pier{i}",f"pier{i+1}"],role="keystone"),"Kit_RealStall","Kit_RealStall",["Keystn"]),
            MOD(B(f"stall{i}","wood",(x,3.55,.1873),(1.6,2.18,1.325),[f"key{i}"],role="wall"),"Kit_RealStall","Kit_RealStall",["StallA" if i%2==0 else "StallB"])]
    # The stall collider is centred on the module pivot (z .1873, as the loader places parts there) but only 1.325 m
    # deep: its front stops 5 cm behind the keystone's front (z .85 vs .90), so the counter/awning overhang (to z 1.27)
    # is visual only and a shot at the 22 cm keystone reaches the keystone first (gap reported: no visual offset field).
    # Quay piers are 1.0 m deep (behind the keystone front) for the same reason.
    # (sign = 8c99d49 refit: 2.655 x .824 x .30 bolted to the beam cap, pivot 8 mm into the cap -> box starts on the cap)
    # arch posts at the module pivots (+/-4.49), beam on both, steel sign on the beam (all non-objective)
    b+=[MOD(B("postL","wood",(4.4923,3.03,0),(.4,3.26,.44),["pads"],nonObj=True,role="wall"),"Bld_MarketArch","Kit_RealStall",["PostL"]),
        MOD(B("postR","wood",(-4.4904,3.03,0),(.4,3.26,.44),["pads"],nonObj=True,role="wall"),"Bld_MarketArch","Kit_RealStall",["PostR"]),
        MOD(B("beam","wood",(0,4.922,.0009),(11.1,.524,.422),["postL","postR"],nonObj=True,role="roof"),"Bld_MarketArch","Kit_RealStall",["Beam"]),
        MOD(B("sign","steel",(0,5.592,0),(2.655,.816,.3),["beam"],joint=True,nonObj=True,role="steel_joint"),"Bld_MarketArch","Kit_RealStall",["Sign"])]
    return b

def L9():  # HARD "Sea-Wall Beacon": steel-ringed lighthouse + seawall; a catwalk ties lantern gallery to seawall crest
    b=[ROCK("rock",(0,.6,0),(10.6,1.2,6)),   # rev 6: 10.6 m (was 12) for the fixed camera frame
       B("lhBase","brick",(-3,2.2,-1),(2.6,2.0,2.6),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("lhRing","steel",(-3,3.45,-1),(2.8,.5,2.8),["lhBase"],joint=True,role="steel_joint"),
       B("lhShaft0","brick",(-3,4.7,-1),(2.2,2.0,2.2),["lhRing"]),
       B("lhShaft1","brick",(-3,6.7,-1),(2.0,2.0,2.0),["lhShaft0"]),
       B("lhGallery","wood",(-3,7.9,-1),(2.8,.4,2.8),["lhShaft1"]),
       B("lhLantern","wood",(-3,8.8,-1),(1.6,1.4,1.6),["lhGallery"],role="lantern"),
       B("lhCap","brick",(-3,9.8,-1),(1.9,.6,1.9),["lhLantern"],color="4D7BC4"),
       B("breakwater","brick",(3,2.0,2.0),(5.0,1.6,.6),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("propA","wood",(2,2.0,1.0),(.4,1.4,.4),["rock"],role="prop"),
       B("propB","wood",(4,2.0,1.0),(.4,1.4,.4),["rock"],role="prop"),
       B("seaDeck","brick",(3,3.0,.6),(4.4,.5,1.6),["propA","propB"])]
    b+=[B(f"sea{i}","brick",(1.5+i*1.5,4.0,.6),(1.4,1.5,1.0),["seaDeck"],role="seawall") for i in range(3)]
    b+=[B("seaCrest","brick",(3,5.0,.6),(4.6,.5,1.2),["sea0","sea1","sea2"],role="seawall"),
        B("bellTower","wood",(3,6.0,.6),(1.0,1.5,1.0),["seaCrest"]),
        B("catwalk","wood",(0,7.9,-.2),(4.6,.3,.8),["lhGallery","bellTower"])]   # falls only when BOTH ends go
    return b

def L10():  # Tidewarden: Developer's boss layout (SG-05 rules live in TidewardenController)
    return [B("stack","brick",(0,1.5,0),(5,3,4),nonObj=True,indestructible=True,color="8A8A8A",role="island"),
     B("hips","brick",(0,3.6,0),(3,1.2,2.2),["stack"]),
     B("torso","brick",(0,5.6,0),(3.4,2.8,2.4),["hips"]),
     B("shoulderL","steel",(-2.2,6.6,0),(1,1,1),["torso"],joint=True,role="shoulder"),
     B("shoulderR","steel",(2.2,6.6,0),(1,1,1),["torso"],joint=True,role="shoulder"),
     B("armL","brick",(-2.4,4.9,0),(.8,2.4,.8),["shoulderL"],role="arm",nonObj=True),
     B("armR","brick",(2.4,4.9,0),(.8,2.4,.8),["shoulderR"],role="arm",nonObj=True),
     B("armorTop","wood",(0,6.3,1.35),(3.2,.9,.3),["torso"],role="armor"),
     B("armorBot","wood",(0,5.1,1.35),(3.2,.9,.3),["torso"],role="armor"),
     B("core","steel",(0,5.7,1.4),(1,1,.4),["torso"],role="core",indestructible=True,color="FFB347"),
     B("shutter","brick",(0,5.7,1.75),(1.4,1.4,.25),[],role="shutter",nonObj=True,indestructible=True),
     B("head","wood",(0,7.6,0),(1.6,1.2,1.6),["torso"]),
     B("lamp","wood",(0,8.5,0),(1,.6,1),["head"],color="E8B04B")]

def face_wrecker(b):
    """BUG-24: the Wrecker sits at x=-20 and fires along +x, so the authored front (+z) is turned to face -x
    (rotate -90 deg about Y): (x,y,z) -> (-z,y,x), sizes swap x/z. Authoring above stays in the readable +z-front frame."""
    x,y,z=b["p"]; sx,sy,sz=b["s"]; b=dict(b); b["p"]=[round(-z,4),y,x]; b["s"]=[sz,sy,sx]; return b

LAYOUTS={1:("c1_l01_lookout_hut",L1),2:("c1_l02_stilt_tower",L2),3:("c1_l03_bakery",L3),4:("c1_l04_windmill",L4),
 5:("c1_l05_twin_cottages",L5),6:("c1_l06_granary",L6),7:("m1_clockwork_tower",L7),8:("c1_l08_market_arcade",L8),
 9:("c1_l09_lighthouse_gauntlet",L9),10:("c1_l10_kings_palace",L10)}
M=lambda t,txt,v=0,role="": dict(type=t,value=v,role=role,text=txt)
# rev 6 tuning: readable mastery text (type/value/role unchanged except where noted), hint = first shot of the sim's best line
OVR={1:dict(hintTarget="post",mastery=M("DESTROYED_AT_LEAST","Destroy at least 95% of the lookout",0.95)),
 2:dict(shots=5,hintTarget="sillL",star3Pct=0.4,star2Pct=0.2,teaches="Find every support, not the walls",
        mastery=M("ROLE_BELOW_HEIGHT","Knock the water tank off the cliff",0,"tank")),
 3:dict(hintTarget="propL",mastery=M("DESTROYED_AT_LEAST","Destroy at least 95% of the bakery",0.95)),
 4:dict(hintTarget="beamL",mastery=M("THREE_STARS_NO_ROLE_HITS","3 stars without hitting a brick wall",0,"wall")),
 5:dict(hintTarget="bridge",mastery=M("CLEAR_IN_SHOTS","Drop both cottages with 1 shot",1)),
 6:dict(hintTarget="braceL",mastery=M("ALL_ROLE_BROKEN","Break all 3 gold steel joints",0,"steel_joint")),
 7:dict(hintTarget="ring0",mastery=M("ROLE_INTACT","Topple the tower without breaking the crown",0.8,"crown")),
 8:dict(shots=6,hintTarget="key1",mastery=M("STARS","Earn 3 stars",3)),
 9:dict(shots=5,hintTarget="lhRing",mastery=M("NO_POWERUP","Win without using a power-up")),
 10:dict(shots=7,star3Pct=0.42,star2Pct=0.28,starCapIfPowerupUsed=2,hintTarget="gateLintel",mastery=M("SHOTS_LEFT_AT_LEAST","Take the King's Palace with 2 shots to spare",2))}
if __name__=="__main__":
    os.makedirs(os.path.join(HERE,"layouts"),exist_ok=True)
    for n,(lid,f) in LAYOUTS.items():
        blocks=(json.load(open(os.path.join(HERE,"../../Assets/_Project/Resources/Data/Layouts/c1_l10_kings_palace.json")))["blocks"] if n==10 else [face_wrecker(b) for b in f()])
        json.dump({"id":lid,"blocks":blocks},open(os.path.join(HERE,"layouts",lid+".json"),"w"),indent=1)
    src=os.path.join(HERE,"../../Assets/_Project/Resources/Data/chapter1.json")
    ch=json.load(open(src))
    for L in ch["levels"]: L.update(OVR.get(L["number"],{}))
    json.dump({"levels":ch["levels"]},open(os.path.join(HERE,"chapter1_levels.json"),"w"),indent=1)
