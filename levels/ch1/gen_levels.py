#!/usr/bin/env python3
"""Authors Ch1 L1-L10 layouts (LayoutData JSON, Developer's ContentModels.cs schema) + LevelEntry list.
Run: python3 gen_levels.py  -> layouts/*.json, chapter1_levels.json. Camera assumed on +z side (occlusion in sim)."""
import json, os
HERE=os.path.dirname(os.path.abspath(__file__))
def B(id,mat,p,s,sup=(),joint=False,role="",nonObj=False,color="",indestructible=False):
    return dict(id=id,mat=mat,p=list(p),s=list(s),sup=list(sup),joint=joint,role=role,nonObj=nonObj,color=color,
                explosive=False,indestructible=indestructible,spinAxis=[],pivot=[],spinDps=0.0)
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

def L3():  # MEDIUM "Two-Storey Bakery", rev 8 INTERIM: 3D Modeler's modular realistic Bld_Townhouse (realistic-v1
    # @ e725121, Kit_RealHouse) at 1:1 from its module transforms, until the 8x6 m bakery lands (due Oct 31).
    # One layout block per Level Designer MODULE (the README splits modules into pieces only for the 1,500-tri FBX budget
    # and 63-char joint names; all pieces of a module drop in at the module pivot). Block ids = module ids.
    # House frame = layout space: +z = street (ShopFront), +x = the house's left (Wall_Left, Roof_Left). face_wrecker turns
    # +z to -x, so the street faces the cannon and the house's left is screen-left. Supports = module anchor_to (which
    # match the 69 piece contacts); materials per fracture class (Stone -> brick, Wood -> wood, Roof clay tile -> brick).
    # Base = indestructible, non-objective, held. L3's beat (brick needs support loss: two wood props) is kept by standing
    # the house on a timber cellar: Base -> props -> cellar slabs -> the four ground-wall modules. The cellar is the only
    # layer that is not in the model (the contacts have Base -> ground walls); it lifts the house by C = 1.8 m.
    # Boxes are cut at the contact heights so no two cubes overlap (LevelBuilder spawns physics boxes): ground walls
    # 0.35-2.72, Floor 2.72-3.22 (front jetty to z 2.91), UpperStorey shell 3.22-5.44 (gables + balcony are in its mesh),
    # roof halves 5.44-6.9 (ridge contact 6.73-6.96), Chimney from the roof to its real top 9.55.
    # The optional Annex is left out (ground mass that would not fall with the house). rev 7's shop-awning decoy stays.
    C=1.8
    def H(id,mat,x0,x1,y0,y1,z0,z1,sup,**k):   # box from house-local extents (y lifted by the cellar)
        return B(id,mat,(round((x0+x1)/2,3),round((y0+y1)/2+C,3),round((z0+z1)/2,3)),(round(x1-x0,3),round(y1-y0,3),round(z1-z0,3)),sup,**k)
    b=[B("Base","brick",(0,.175,.15),(7.04,.35,6.04),nonObj=True,indestructible=True,color="8A8A8A",role="island"),
       B("propL","wood",(1.6,1.05,1.8),(.45,1.4,.45),["Base"],role="prop"),      # house-left = screen-left
       B("propR","wood",(-1.6,1.05,1.8),(.45,1.4,.45),["Base"],role="prop"),
       B("slabL","brick",(1.605,1.95,.03),(3.21,.4,5.46),["propL"]),
       B("slabR","brick",(-1.605,1.95,.03),(3.21,.4,5.46),["propR"])]
    S=["slabL","slabR"]; g0,g1=.35,2.72
    b+=[H("ShopFront","brick",-3.31,3.31,g0,g1,2.16,2.76,S,role="wall"),       # door, window, lantern, hanging sign
        H("Wall_Left","brick",2.745,3.31,g0,g1,-2.15,2.15,S,role="wall"),
        H("Wall_Right","brick",-3.31,-2.745,g0,g1,-2.15,2.15,S,role="wall"),
        H("Wall_Back","brick",-3.21,3.21,g0,g1,-2.7,-2.16,S,role="wall"),
        H("Floor","wood",-3.21,3.21,2.72,3.22,-2.17,2.91,["ShopFront","Wall_Back","Wall_Left","Wall_Right"]),
        H("UpperStorey","wood",-3.28,3.28,3.22,5.44,-2.7,3.0,["Floor"],role="wall"),
        H("Roof_Left","brick",-.05,3.28,5.44,6.9,-2.7,3.0,["UpperStorey"],role="roof",color="B5523B"),
        H("Roof_Right","brick",-3.28,-.05,5.44,6.9,-2.7,3.0,["UpperStorey"],role="roof",color="B5523B"),
        H("Chimney","brick",-2.635,-1.765,6.9,9.55,-.96,-.24,["Roof_Right","UpperStorey"]),
        B("awningPost","wood",(3.45,1.35,3.0),(.3,2.0,.3),["Base"],role="prop"),      # decoy: holds only the shop awning
        B("awning","wood",(3.1,2.5,3.1),(1.0,.3,.6),["awningPost"])]
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

def L8():  # market arcade: 4 wood keystones 1.5m apart hold 4 bays on stone piers -> Splitstrike takes 3 at once
    # rev 6: 10.4 m wide (was 14 m, which ran off the fixed behind-cannon frame); keystone spacing 1.6 m keeps any
    # three neighbours inside Multi Shot's 3.5 m spread, and each keystone now sits under its own bay
    b=[ROCK("rock",(0,.6,0),(10.4,1.2,4))]
    for i,x in enumerate([-4.4,-2.2,0,2.2,4.4]): b.append(B(f"pier{i}","brick",(x,2.6,0),(.6,2.8,.8),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"))
    for i,x in enumerate([-3.3,-1.1,1.1,3.3]):
        b+=[B(f"key{i}","wood",(-2.4+1.6*i,4.25,1.1),(.6,.5,.4),[f"pier{i}",f"pier{i+1}"],role="keystone"),
            B(f"bay{i}","brick",(x,4.7,0),(2.1,.5,2),[f"key{i}"]),
            B(f"stall{i}","brick",(x,5.6,0),(1.8,1.2,1.6),[f"bay{i}"]),
            B(f"awning{i}","wood",(x,6.4,.3),(2.1,.3,2.2),[f"stall{i}"])]
    b+=[B("archL","wood",(-.9,7.3,0),(.3,1.4,.3),["stall1"]),B("archR","wood",(.9,7.3,0),(.3,1.4,.3),["stall2"]),
        B("sign","steel",(0,8.1,0),(2.2,.5,.3),["archL","archR"],joint=True,role="steel_joint"),
        B("clock","wood",(0,8.8,0),(1.0,1.0,.3),["sign"])]
    return b

def L9():  # HARD "Lighthouse Gauntlet": steel-ringed lighthouse + seawall; a catwalk ties lantern gallery to seawall crest
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
 9:("c1_l09_lighthouse_gauntlet",L9),10:("c1_l10_tidewarden",L10)}
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
 10:dict(shots=8,star3Pct=0.375,star2Pct=0.25,hintTarget="shoulderL",mastery=M("NO_CLOSED_SHUTTER_HIT","Defeat the Tidewarden without hitting the closed shutter",0,"shutter"))}
if __name__=="__main__":
    os.makedirs(os.path.join(HERE,"layouts"),exist_ok=True)
    for n,(lid,f) in LAYOUTS.items():
        blocks=[face_wrecker(b) for b in f()]
        json.dump({"id":lid,"blocks":blocks},open(os.path.join(HERE,"layouts",lid+".json"),"w"),indent=1)
    src=os.path.join(HERE,"../../Assets/_Project/Resources/Data/chapter1.json")
    ch=json.load(open(src))
    for L in ch["levels"]: L.update(OVR.get(L["number"],{}))
    json.dump({"levels":ch["levels"]},open(os.path.join(HERE,"chapter1_levels.json"),"w"),indent=1)
