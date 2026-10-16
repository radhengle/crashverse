#!/usr/bin/env python3
"""Authors Ch1 L1-L10 layouts (LayoutData JSON, Developer's ContentModels.cs schema) + LevelEntry list.
Run: python3 gen_levels.py  -> layouts/*.json, chapter1_levels.json. Camera assumed on +z side (occlusion in sim)."""
import json, os
HERE=os.path.dirname(os.path.abspath(__file__))
def B(id,mat,p,s,sup=(),joint=False,role="",nonObj=False,color="",indestructible=False):
    return dict(id=id,mat=mat,p=list(p),s=list(s),sup=list(sup),joint=joint,role=role,nonObj=nonObj,color=color,
                explosive=False,indestructible=indestructible,spinAxis=[],pivot=[],spinDps=0.0)
ROCK=lambda id,p,s: B(id,"brick",p,s,nonObj=True,indestructible=True,color="8A8A8A",role="island")

def L1():  # lookout hut on one stilt: one clean shot on the post drops everything
    return [ROCK("rock",(0,.6,0),(4,1.2,4)),
      B("post","wood",(0,2.2,.6),(.5,2.0,.5),["rock"],role="support"),
      B("deck","wood",(0,3.4,0),(3,.4,3),["post"]),
      B("wallF","wood",(0,4.3,1.3),(2.6,1.4,.2),["deck"],role="wall"),
      B("wallB","wood",(0,4.3,-1.3),(2.6,1.4,.2),["deck"],role="wall"),
      B("wallL","wood",(-1.3,4.3,0),(.2,1.4,2.4),["deck"],role="wall"),
      B("wallR","wood",(1.3,4.3,0),(.2,1.4,2.4),["deck"],role="wall"),
      B("roof","wood",(0,5.3,0),(3.2,.4,3.2),["wallF","wallB","wallL","wallR"],role="roof"),
      B("flag","wood",(0,6.0,0),(.1,1.0,.1),["roof"],nonObj=True,color="E5484D")]

def L2():  # stilt water tower: 4 stilts meet at a wooden collar; collar is the weak point
    b=[ROCK("rock",(0,.6,0),(5,1.2,4)),B("sill","wood",(0,1.4,.2),(3.2,.4,2.8),["rock"],role="support")]
    for i,(x,z) in enumerate([(-1.2,-1),(1.2,-1),(-1.2,1),(1.2,1)]):
        b.append(B(f"stilt{i}","wood",(x,3.0,z),(.35,2.8,.35),["sill"],role="stilt"))
    b+=[B("frame","wood",(0,4.6,0),(3,.4,2.6),[f"stilt{i}" for i in range(4)]),
        B("tankLo","wood",(0,5.4,0),(2.4,1.2,2.4),["frame"],role="tank"),
        B("tankHi","wood",(0,6.6,0),(2.4,1.2,2.4),["tankLo"],role="tank"),
        B("tankLid","wood",(0,7.4,0),(2.6,.4,2.6),["tankHi"],role="tank"),
        B("ladder","wood",(1.7,5.4,0),(.2,2.6,.6),["frame"]),
        B("pipe","wood",(-1.7,5.6,0),(.3,2.2,.3),["frame"])]
    return b

def L3():  # bakery: brick on two wood props; brick faces take one shot each and achieve little
    b=[ROCK("rock",(0,.6,0),(6,1.2,4))]
    b+=[B("propL","wood",(-1.6,1.9,1.4),(.4,1.4,.4),["rock"],role="prop"),
        B("propR","wood",(1.6,1.9,1.4),(.4,1.4,.4),["rock"],role="prop"),
        B("slabL","brick",(-1,2.8,0),(2,.4,3.4),["propL"]),
        B("slabR","brick",(1,2.8,0),(2,.4,3.4),["propR"]),
        B("wallF","brick",(0,3.8,1.5),(3.6,1.6,.3),["slabL","slabR"],role="wall"),
        B("wallB","brick",(0,3.8,-1.5),(3.6,1.6,.3),["slabL","slabR"],role="wall"),
        B("wallL","brick",(-1.8,3.8,0),(.3,1.6,2.8),["slabL","slabR"],role="wall"),
        B("wallR","brick",(1.8,3.8,0),(.3,1.6,2.8),["slabL","slabR"],role="wall"),
        B("oven","brick",(0,3.6,0),(1.2,1.2,1.2),["slabL","slabR"]),
        B("roof","brick",(0,4.85,0),(4,.5,3.4),["wallF","wallB","wallL","wallR"],role="roof"),
        B("chimney","brick",(1,5.6,-.8),(.6,1.0,.6),["roof"])]
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
       B("roof","brick",(0,8.4,0),(2.4,.8,2.4),["drum2"])]
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
    return b

def L6():  # granary on steel columns; gold steel braces (joints) are the efficient break
    b=[ROCK("rock",(0,.6,0),(6,1.2,4))]
    for s,x in (("L",-1.8),("R",1.8)):
        b+=[B(f"col{s}","brick",(x,2.4,.9),(.6,2.4,.6),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
            B(f"brace{s}","steel",(x,3.85,.9),(.9,.5,.9),[f"col{s}"],joint=True,role="brace")]
    b+=[B("braceM","steel",(0,3.85,-.9),(.9,.5,.9),["rock"],joint=True,role="brace"),
        B("deck","brick",(0,4.3,0),(4.6,.4,3),["braceL","braceR"]),
        B("binF","brick",(0,5.4,1.3),(4.2,1.8,.3),["deck"],role="wall"),
        B("binB","brick",(0,5.4,-1.3),(4.2,1.8,.3),["deck"],role="wall"),
        B("binL","brick",(-2.1,5.4,0),(.3,1.8,2.3),["deck"],role="wall"),
        B("binR","brick",(2.1,5.4,0),(.3,1.8,2.3),["deck"],role="wall"),
        B("loft","brick",(0,6.6,0),(4.6,.5,3),["binF","binB","binL","binR"]),
        B("roof","brick",(0,7.3,0),(3.8,.9,2.6),["loft"])]
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
    b+=[B("sign","steel",(0,7.2,0),(1.4,.6,.3),["stall1","stall2"],joint=True,role="steel_joint")]
    return b

def L9():  # gauntlet: lighthouse on a steel ring + seawall on wood props hidden behind a brick breakwater
    b=[ROCK("rock",(0,.6,0),(12,1.2,6)),
       B("lhBase","brick",(-3,2.2,-1),(2.6,2.0,2.6),["rock"],role="island",nonObj=True,indestructible=True,color="8A8A8A"),
       B("lhRing","steel",(-3,3.45,-1),(2.8,.5,2.8),["lhBase"],joint=True,role="steel_joint"),
       B("lhShaft0","brick",(-3,4.7,-1),(2.2,2.0,2.2),["lhRing"]),
       B("lhShaft1","brick",(-3,6.7,-1),(2.0,2.0,2.0),["lhShaft0"]),
       B("lhGallery","wood",(-3,7.9,-1),(2.8,.4,2.8),["lhShaft1"]),
       B("lhLantern","wood",(-3,8.8,-1),(1.6,1.4,1.6),["lhGallery"],role="lantern"),
       B("lhCap","brick",(-3,9.8,-1),(1.9,.6,1.9),["lhLantern"],color="4D7BC4"),
       B("breakwater","brick",(3,2.0,2.0),(5.0,1.6,.6),["rock"],role="wall",nonObj=True,indestructible=True,color="8A8A8A"),
       B("propA","wood",(2,2.0,1.0),(.4,1.4,.4),["rock"],role="prop"),
       B("propB","wood",(4,2.0,1.0),(.4,1.4,.4),["rock"],role="prop"),
       B("seaDeck","brick",(3,3.0,.6),(4.4,.5,1.6),["propA","propB"])]
    b+=[B(f"sea{i}","brick",(1.5+i*1.5,4.0,.6),(1.4,1.5,1.0),["seaDeck"],role="seawall") for i in range(3)]
    b+=[B("seaCrest","brick",(3,5.0,.6),(4.6,.5,1.2),["sea0","sea1","sea2"],role="seawall")]
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
     B("core","steel",(0,5.7,1.0),(1,1,.4),["torso"],role="core",indestructible=True,color="FFB347"),
     B("shutter","brick",(0,5.7,1.75),(1.4,1.4,.25),[],role="shutter",nonObj=True,indestructible=True),
     B("head","wood",(0,7.6,0),(1.6,1.2,1.6),["torso"]),
     B("lamp","wood",(0,8.5,0),(1,.6,1),["head"],color="E8B04B")]

LAYOUTS={1:("c1_l01_lookout_hut",L1),2:("c1_l02_stilt_tower",L2),3:("c1_l03_bakery",L3),4:("c1_l04_windmill",L4),
 5:("c1_l05_twin_cottages",L5),6:("c1_l06_granary",L6),7:("m1_clockwork_tower",L7),8:("c1_l08_market_arcade",L8),
 9:("c1_l09_lighthouse_gauntlet",L9),10:("c1_l10_tidewarden",L10)}
OVR={1:dict(hintTarget="post",mastery=dict(type="DESTROYED_AT_LEAST",value=0.95,role="",text=">=95% destroyed")),2:dict(hintTarget="sill",mastery=dict(type="ROLE_BELOW_HEIGHT",value=0,role="tank",text="Topple the tank off the island edge")),3:dict(hintTarget="propL"),4:dict(hintTarget="beamL"),
 5:dict(hintTarget="bridge"),6:dict(hintTarget="braceL"),7:dict(hintTarget="scaffold"),8:dict(hintTarget="key1"),
 9:dict(hintTarget="lhRing"),10:dict(shots=8,star3Pct=0.375,star2Pct=0.25,hintTarget="shoulderL",mastery=dict(type="NO_CLOSED_SHUTTER_HIT",value=0,role="shutter",text="Defeat the Tidewarden without hitting the closed shutter"))}
if __name__=="__main__":
    os.makedirs(os.path.join(HERE,"layouts"),exist_ok=True)
    for n,(lid,f) in LAYOUTS.items():
        json.dump({"id":lid,"blocks":f()},open(os.path.join(HERE,"layouts",lid+".json"),"w"),indent=1)
    src=os.path.join(HERE,"../../Assets/_Project/Resources/Data/chapter1.json")
    ch=json.load(open(src))
    for L in ch["levels"]: L.update(OVR.get(L["number"],{}))
    json.dump({"levels":ch["levels"]},open(os.path.join(HERE,"chapter1_levels.json"),"w"),indent=1)
