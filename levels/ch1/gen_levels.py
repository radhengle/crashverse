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

def L2():  # EASY "Leaning Water Tower": stilts on one sill; a catwalk links the tank to a pump shed -> domino
    b=[ROCK("rock",(0,.6,0),(9,1.2,4)),B("sill","wood",(0,1.4,.2),(3.2,.4,2.8),["rock"],role="support")]
    for i,(x,z) in enumerate([(-1.2,-1),(1.2,-1),(-1.2,1),(1.2,1)]):
        b.append(B(f"stilt{i}","wood",(x,3.0,z),(.35,2.8,.35),["sill"],role="stilt"))
    b+=[B("frame","wood",(0,4.6,0),(3,.4,2.6),[f"stilt{i}" for i in range(4)]),
        B("tankLo","wood",(0,5.4,0),(2.4,1.2,2.4),["frame"],role="tank"),
        B("tankHi","wood",(0,6.6,0),(2.4,1.2,2.4),["tankLo"],role="tank"),
        B("tankLid","wood",(0,7.4,0),(2.6,.4,2.6),["tankHi"],role="tank"),
        B("catwalk","wood",(2.9,4.6,0),(2.8,.3,1.0),["frame"]),
        B("shedPost","wood",(4.3,2.9,0),(.3,3.2,.3),["catwalk"]),    # shed hangs off the catwalk (stilted over the cliff)
        B("shed","wood",(4.3,4.0,-1.2),(1.6,1.4,1.4),["catwalk"]),
        B("shedRoof","wood",(4.3,5.0,-1.2),(1.8,.4,1.6),["shed"])]
    return b

def L3():  # MEDIUM "Two-Storey Bakery": brick shop + upper flat on two wood props; awning post is a decoy
    b=[ROCK("rock",(0,.6,0),(7,1.2,4))]
    b+=[B("propL","wood",(-1.6,1.9,1.4),(.4,1.4,.4),["rock"],role="prop"),
        B("propR","wood",(1.6,1.9,1.4),(.4,1.4,.4),["rock"],role="prop"),
        B("slabL","brick",(-1,2.8,0),(2,.4,3.4),["propL"]),
        B("slabR","brick",(1,2.8,0),(2,.4,3.4),["propR"]),
        B("wallF","brick",(0,3.8,1.5),(3.6,1.6,.3),["slabL","slabR"],role="wall"),
        B("wallB","brick",(0,3.8,-1.5),(3.6,1.6,.3),["slabL","slabR"],role="wall"),
        B("oven","brick",(0,3.6,0),(1.2,1.2,1.2),["slabL","slabR"]),
        B("floor2","brick",(0,4.8,0),(4,.4,3.4),["wallF","wallB"]),
        B("flat","brick",(-.6,5.6,0),(2.6,1.2,2.6),["floor2"],role="wall"),
        B("balcony","wood",(1.6,5.2,0),(1.0,.3,2.0),["floor2"]),
        B("roof","brick",(-.6,6.5,0),(3.0,.6,3.0),["flat"],role="roof"),
        B("chimney","brick",(-1.4,7.3,-.8),(.6,1.0,.6),["roof"]),
        B("awningPost","wood",(3.0,2.2,1.6),(.3,2.0,.3),["rock"],role="prop"),   # decoy: holds only the awning
        B("awning","wood",(2.6,3.4,1.6),(1.4,.3,1.0),["awningPost"])]
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
    b=[ROCK("rockL",(-5,.6,0),(3,1.2,3)),ROCK("rockR",(5,.6,0),(3,1.2,3)),
       B("pierL","brick",(-3.2,2.0,0),(.8,1.6,.8),["rockL"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("pierR","brick",(3.2,2.0,0),(.8,1.6,.8),["rockR"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("bridge","wood",(0,3.0,0),(7.2,.4,2.0),["pierL","pierR"],role="bridge")]
    for side,x in (("L",-2),("R",2)):
        b+=[B(f"floor{side}","wood",(x,3.4,0),(2.4,.4,2),["bridge"]),
            B(f"wallF{side}","brick",(x,4.2,.85),(2.2,1.2,.3),[f"floor{side}"],role="wall"),
            B(f"wallB{side}","brick",(x,4.2,-.85),(2.2,1.2,.3),[f"floor{side}"],role="wall"),
            B(f"roof{side}","wood",(x,5.1,0),(2.6,.6,2.2),[f"wallF{side}",f"wallB{side}"],role="roof")]
    b+=[B("wheelAxle","wood",(0,2.4,.9),(.4,.8,.4),["bridge"]),          # water-wheel hangs under the bridge: falls with it
        B("wheel","wood",(0,1.6,1.2),(1.8,1.8,.3),["wheelAxle"]),
        B("lanterns","wood",(0,4.4,0),(1.4,.2,.2),["roofL","roofR"])]       # string between roofs: 2nd domino
    return b

def L6():  # MEDIUM "Grain Silo": tall 3-drum brick silo on two gold steel braces over stone piers; side hopper rides the deck
    b=[ROCK("rock",(0,.6,0),(7,1.2,4))]
    for s_,x in (("L",-1.4),("R",1.4)):
        b+=[B(f"col{s_}","brick",(x,2.4,.9),(.6,2.4,.6),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
            B(f"brace{s_}","steel",(x,3.85,.9),(.9,.5,.9),[f"col{s_}"],joint=True,role="brace")]
    b+=[B("braceM","steel",(0,3.85,-.9),(.9,.5,.9),["rock"],joint=True,role="brace"),
        B("deck","brick",(0,4.3,0),(3.6,.4,3),["braceL","braceR"]),
        B("drum0","brick",(0,5.5,0),(2.6,2.0,2.6),["deck"],role="wall"),
        B("drum1","brick",(0,7.5,0),(2.4,2.0,2.4),["drum0"],role="wall"),
        B("drum2","brick",(0,9.3,0),(2.2,1.6,2.2),["drum1"],role="wall"),
        B("dome","steel",(0,10.4,0),(1.8,.6,1.8),["drum2"]),
        B("chute","brick",(2.3,5.2,0),(1.0,1.4,1.0),["deck"]),
        B("hopper","brick",(2.3,6.3,0),(1.4,.8,1.4),["chute"])]
    return b

def L7():  # M1 Clockwork Tower (contract §7). Plinth indestructible, ring0 seated on plinth behind wood scaffold (REPORT A3/A4)
    b=[B("plinth","brick",(0,.6,0),(4.5,1.2,4.5),nonObj=True,indestructible=True,color="8A8A8A",role="island"),
       B("scaffold","wood",(0,1.9,1.7),(3.0,1.4,.3),["plinth"],role="wing"),
       B("wingL","wood",(-2.6,2.6,0),(.6,2.8,.5),["plinth"],role="wing"),
       B("wingR","wood",(2.6,2.6,0),(.6,2.8,.5),["plinth"],role="wing"),
       B("ring0","steel",(0,1.45,0),(2.6,.5,2.6),["plinth"],joint=True,role="steel_joint")]
    jy=[1.45,4.2,6.95]
    for i in (1,2):
        y0=jy[i-1]+.25; y1=jy[i]-.25; m=(y0+y1)/2; h=y1-y0; below=f"ring{i-1}"
        w=[B(f"wallF{i}","brick",(0,m,1.1),(2.2,h,.35),[below],role="wall"),
           B(f"wallB{i}","brick",(0,m,-1.1),(2.2,h,.35),[below],role="wall"),
           B(f"wallL{i}","brick",(-1.1,m,0),(.35,h,2.2),[below],role="wall"),
           B(f"wallR{i}","brick",(1.1,m,0),(.35,h,2.2),[below],role="wall")]
        b+=w; b.append(B(f"ring{i}","steel",(0,jy[i],0),(2.6,.5,2.6),[x["id"] for x in w],joint=True,role="steel_joint"))
    b+=[B("crownFloor","brick",(0,7.4,0),(3.2,.4,3.2),["ring2"],role="crown"),
        B("crownWood","wood",(0,8.3,0),(2.8,1.4,2.8),["crownFloor"],role="crown"),
        B("crownBrick","brick",(0,9.4,0),(2.4,.8,2.4),["crownWood"],role="crown"),
        B("crownRoof","brick",(0,10.15,0),(3,.7,3),["crownBrick"],role="crown",color="4D7BC4"),
        B("barrelDecor","wood",(5.5,.6,2),(.8,1.2,.8),[],nonObj=True,color="E5484D")]
    return b

def L8():  # market arcade: 4 wood keystones 1.5m apart hold 4 bays on stone piers -> Splitstrike takes 3 at once
    b=[ROCK("rock",(0,.6,0),(14,1.2,4))]
    for i,x in enumerate([-6,-3,0,3,6]): b.append(B(f"pier{i}","brick",(x,2.6,0),(.8,2.8,.8),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"))
    for i,x in enumerate([-4.5,-1.5,1.5,4.5]):
        b+=[B(f"key{i}","wood",(-2.25+1.5*i,4.25,1.1),(.6,.5,.4),[f"pier{i}",f"pier{i+1}"],role="keystone"),
            B(f"bay{i}","brick",(x,4.7,0),(2.8,.5,2),[f"key{i}"]),
            B(f"stall{i}","brick",(x,5.6,0),(2.4,1.2,1.6),[f"bay{i}"]),
            B(f"awning{i}","wood",(x,6.4,.3),(2.8,.3,2.2),[f"stall{i}"])]
    b+=[B("archL","wood",(-.9,7.3,0),(.3,1.4,.3),["stall1"]),B("archR","wood",(.9,7.3,0),(.3,1.4,.3),["stall2"]),
        B("sign","steel",(0,8.1,0),(2.2,.5,.3),["archL","archR"],joint=True,role="steel_joint"),
        B("clock","wood",(0,8.8,0),(1.0,1.0,.3),["sign"])]
    return b

def L9():  # HARD "Lighthouse Gauntlet": steel-ringed lighthouse + seawall; a catwalk ties lantern gallery to seawall crest
    b=[ROCK("rock",(0,.6,0),(12,1.2,6)),
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
OVR={1:dict(hintTarget="post",mastery=dict(type="DESTROYED_AT_LEAST",value=0.95,role="",text=">=95% destroyed")),2:dict(hintTarget="sill",mastery=dict(type="ROLE_BELOW_HEIGHT",value=0,role="tank",text="Topple the tank off the island edge")),3:dict(hintTarget="propL"),4:dict(hintTarget="beamL"),
 5:dict(hintTarget="bridge"),6:dict(hintTarget="braceL"),7:dict(hintTarget="scaffold"),8:dict(hintTarget="key1"),
 9:dict(hintTarget="lhRing"),10:dict(shots=8,star3Pct=0.375,star2Pct=0.25,hintTarget="shoulderL",mastery=dict(type="NO_CLOSED_SHUTTER_HIT",value=0,role="shutter",text="Defeat the Tidewarden without hitting the closed shutter"))}
if __name__=="__main__":
    os.makedirs(os.path.join(HERE,"layouts"),exist_ok=True)
    for n,(lid,f) in LAYOUTS.items():
        blocks=[face_wrecker(b) for b in f()]
        json.dump({"id":lid,"blocks":blocks},open(os.path.join(HERE,"layouts",lid+".json"),"w"),indent=1)
    src=os.path.join(HERE,"../../Assets/_Project/Resources/Data/chapter1.json")
    ch=json.load(open(src))
    for L in ch["levels"]: L.update(OVR.get(L["number"],{}))
    json.dump({"levels":ch["levels"]},open(os.path.join(HERE,"chapter1_levels.json"),"w"),indent=1)
