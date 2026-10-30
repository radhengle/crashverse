#!/usr/bin/env python3
"""Seasons 2-4 (Frozen Peaks, Neon City, Primeval Peaks): layouts + chapter files in the integration/v2 format
(ChapterData/LevelEntry incl. heroSlot; LayoutData/BlockData). Authoring frame: +z = front, x = lateral, y = up;
face_wrecker() turns every layout so the front faces the Wrecker (it fires along +x), exactly like levels/ch1/gen_levels.py."""
import json, os, copy
HERE=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(HERE,'data'); LAY=os.path.join(OUT,'Layouts')
def B(id,mat,p,s,sup=(),joint=False,role="",nonObj=False,color="",indestructible=False,explosive=False):
    return dict(id=id,mat=mat,p=list(p),s=list(s),sup=list(sup),joint=joint,role=role,nonObj=nonObj,color=color,
                explosive=explosive,indestructible=indestructible,spinAxis=[],pivot=[],spinDps=0.0)
def ROCK(id,p,s): return B(id,"brick",p,s,nonObj=True,indestructible=True,color="8A8A8A",role="island")   # ruling 6 stone base
def BARREL(id,p,color="E5484D",role="barrel"): return B(id,"wood",p,(.8,1.0,.8),["ground"],nonObj=True,color=color,explosive=True,role=role)
def SPIN(b,axis,pivot,dps): b=dict(b); b["spinAxis"]=list(axis); b["pivot"]=list(pivot); b["spinDps"]=dps; return b
GROUND=lambda w=12,d=6: ROCK("ground",(0,.6,0),(w,1.2,d))
# season palettes (colour only; materials are the engine's five)
ICE="CFE8F7"; SNOW="F1F6FA"; FROST_STEEL="7FA7C9"; NEON_STEEL="3B3F5C"; NEON_GLASS="E879F9"; CORE="22D3EE"; AMBER="F59E0B"; FOSSIL="C9B48A"; FERN="5E8C31"

# ---------------- Season 2: Frozen Peaks (c2, levels 11-20) ----------------
def c2_01():  # Easy: Ski Lodge on one log pile
    return [GROUND(6,6),B("logpile","wood",(0,1.6,.8),(1.4,.8,1.0),["ground"],role="support"),
      B("floor","wood",(0,2.2,0),(4,.4,3),["logpile"]),
      B("wallF","wood",(0,3.1,1.4),(3.8,1.4,.2),["floor"],role="wall"),B("wallB","wood",(0,3.1,-1.4),(3.8,1.4,.2),["floor"],role="wall"),
      B("roof","stone",(0,4.1,0),(4.4,.6,3.4),["wallF","wallB"],role="roof",color=SNOW),
      B("chimney","stone",(1.2,4.9,-.8),(.6,1.0,.6),["roof"],color=ICE)]
def c2_02():  # Medium: Snow-stone chalet on two pine props; stone faces are 2-shot traps
    return [GROUND(8,5),B("propL","wood",(-1.6,1.9,1.3),(.4,1.4,.4),["ground"],role="prop"),B("propR","wood",(1.6,1.9,1.3),(.4,1.4,.4),["ground"],role="prop"),
      B("slabL","brick",(-1,2.8,0),(2,.4,3.2),["propL"]),B("slabR","brick",(1,2.8,0),(2,.4,3.2),["propR"]),
      B("wallF","stone",(0,3.8,1.4),(3.6,1.6,.4),["slabL","slabR"],role="wall",color=ICE),B("wallB","stone",(0,3.8,-1.4),(3.6,1.6,.4),["slabL","slabR"],role="wall",color=ICE),
      B("roof","stone",(0,4.9,0),(4,.6,3.4),["wallF","wallB"],role="roof",color=SNOW),
      B("sled","wood",(3.0,1.6,1.6),(1.2,.4,.6),["ground"],role="decoy",nonObj=True)]
def c2_03():  # Medium, Mega Bomb forced: frost depot on a 3x2 grid of pine stilts under a frosted-steel loft (no-PU: 6+ shots)
    b=[GROUND(10,6)]
    for r,z in (("F",.6),("B",-.6)):
        for i,x in enumerate((-1.0,0,1.0)): b.append(B(f"stilt{r}{i}","wood",(x,1.9,z),(.4,1.4,.4),["ground"],role="prop"))
    b+=[B("loft","steel",(0,2.8,0),(3.6,.4,2.4),[f"stilt{r}{i}" for r in "FB" for i in range(3)],color=FROST_STEEL),
        B("depot","stone",(0,3.9,0),(3.2,1.8,2.0),["loft"],color=ICE),B("depotRoof","stone",(0,5.1,0),(3.6,.6,2.4),["depot"],color=SNOW)]
    return b
def c2_04():  # Hard: Ice-bridge fort: two towers on steel joints among stone decoys; bridge falls when both towers go
    b=[GROUND(12,6)]
    for s,x in (("L",-3.5),("R",3.5)):
        b+=[ROCK(f"base{s}",(x,2.2,0),(2,2,2)),
            B(f"joint{s}","steel",(x,3.45,.6),(1.6,.5,.8),[f"base{s}"],joint=True,role="steel_joint",color=FROST_STEEL),
            B(f"tower{s}","stone",(x,4.9,0),(1.8,2.4,1.8),[f"joint{s}"],color=ICE),
            B(f"turret{s}","brick",(x,6.6,0),(2.0,1.0,2.0),[f"tower{s}"],color=SNOW)]
    b+=[B("bridge","stone",(0,5.6,0),(5.2,.5,1.4),["towerL","towerR"],color=ICE),
        B("arch","stone",(0,2.4,1.6),(2.4,2.4,.6),["ground"],role="wall",color=ICE,nonObj=True)]
    return b
def c2_05():  # Breather, Shield forced: snow-cannon row: the frost-gas canister hides behind a snow fence (the likely whiff)
    b=[GROUND(10,5),B("fence","wood",(-.75,2.0,2.7),(1.6,1.6,.3),["ground"],role="wall",nonObj=True,color=SNOW),
       BARREL("canister",(-.75,1.7,1.9),color="E5484D")]
    for i,x in enumerate((-1.5,0,1.5)): b.append(B(f"leg{i}","wood",(x,2.2,.6),(.4,2.0,.4),["ground"],role="prop"))
    b+=[B("hutFloor","wood",(0,3.4,0),(4.6,.4,2.6),["leg0","leg1","leg2"]),
        B("hut","brick",(0,4.4,0),(4.2,1.6,2.2),["hutFloor"],color=SNOW),B("hutRoof","wood",(0,5.5,0),(4.8,.6,2.8),["hut"],color=SNOW),
        B("flagpole","wood",(2.6,6.0,0),(.1,1.6,.1),["hutRoof"],nonObj=True,color="E5484D")]
    return b
def c2_06():  # Medium: avalanche shelter roof on two gold steel braces (2x) over grey piers
    b=[GROUND(8,5)]
    for s,x in (("L",-1.6),("R",1.6)):
        b+=[ROCK(f"pier{s}",(x,2.2,1.6),(.6,2.0,.6)),B(f"brace{s}","steel",(x,3.45,1.6),(.9,.5,.9),[f"pier{s}"],joint=True,role="brace",color=FROST_STEEL)]
    b+=[B("roofDeck","stone",(0,3.9,0),(4.2,.4,3),["braceL","braceR"],color=ICE),
        B("snowPackL","stone",(-1,4.8,0),(1.8,1.4,2.6),["roofDeck"],color=SNOW),B("snowPackR","stone",(1,4.8,0),(1.8,1.4,2.6),["roofDeck"],color=SNOW),
        B("cornice","stone",(0,5.8,0),(4.4,.6,3.0),["snowPackL","snowPackR"],color=SNOW),
        B("backWall","stone",(0,2.4,-1.3),(3.6,2.4,.4),["ground"],role="wall",color=ICE,nonObj=True)]
    return b
def c2_07():  # Hard, Laser forced: glacier vault: the key strut hides behind two stone slabs
    return [GROUND(10,6),
      B("slabA","stone",(-1.2,2.4,2.2),(1.4,2.0,.4),["ground"],role="wall",color=ICE,nonObj=True),B("slabB","stone",(-1.2,2.4,1.6),(1.4,2.0,.4),["ground"],role="wall",color=ICE,nonObj=True),
      B("keyA","steel",(-1.2,2.4,.8),(.6,.6,.6),["ground"],joint=True,role="key",color=FROST_STEEL),
      B("keyB","wood",(1.6,2.2,1.0),(.5,2.0,.5),["ground"],role="key"),
      B("vaultFloor","brick",(0,3.6,0),(4.4,.4,3),["keyA","keyB"]),
      B("vault","stone",(0,4.8,0),(3.8,2.0,2.6),["vaultFloor"],color=ICE),B("vaultCap","stone",(0,6.1,0),(4.2,.6,3),["vault"],color=SNOW),
      B("icicles","stone",(2.4,4.6,0),(.6,1.6,2.4),["vaultFloor"],color=ICE)]
def c2_08():  # Breather: icicle gantry on two pine posts, 2 shots, very readable
    b=[GROUND(12,5),B("postL","wood",(-3,2.3,.8),(.5,2.2,.5),["ground"],role="prop"),B("postR","wood",(3,2.3,.8),(.5,2.2,.5),["ground"],role="prop"),
       B("gantry","wood",(0,3.6,0),(7,.4,2),["postL","postR"])]
    for i,x in enumerate((-2.4,-.8,.8,2.4)): b.append(B(f"icicle{i}","stone",(x,4.6,0),(1.2,1.6,1.4),["gantry"],color=ICE))
    return b
def c2_09():  # Medium: cable-car station on one steel joint + one pine post; the gondola is a decoy
    return [GROUND(9,5),ROCK("pylon",(-1.6,2.2,1.6),(.6,2.0,.6)),
      B("joint","steel",(-1.6,3.45,1.6),(.9,.5,.9),["pylon"],joint=True,role="steel_joint",color=FROST_STEEL),
      B("post","wood",(1.6,2.3,.8),(.5,2.2,.5),["ground"],role="prop"),
      B("platform","brick",(0,3.9,0),(4.4,.4,3),["joint","post"]),
      B("station","stone",(0,5.0,0),(3.8,1.8,2.4),["platform"],color=ICE),B("wheelhouse","wood",(0,6.3,0),(2.4,.8,2.0),["station"]),
      B("gondola","steel",(3.6,3.0,0),(1.2,1.2,1.2),["ground"],role="decoy",color="E5484D",nonObj=True)]
def c2_10():  # HARD (skull, level 20): Frost Citadel: 3 exact shots: open the wooden gate, then both steel joints
    b=[GROUND(12,6),
       B("gate","wood",(1.8,2.6,2.0),(1.6,2.4,.4),["ground"],role="gate"),
       ROCK("pierL",(-1.8,2.2,1.0),(.8,2.0,.8)),ROCK("pierR",(1.8,2.2,1.0),(.8,2.0,.8)),
       B("jointL","steel",(-1.8,3.45,1.0),(1.0,.5,1.0),["pierL"],joint=True,role="steel_joint",color=FROST_STEEL),
       B("jointR","steel",(1.8,3.45,1.0),(1.0,.5,1.0),["pierR"],joint=True,role="steel_joint",color=FROST_STEEL),
       B("hall","stone",(0,4.0,0),(4.8,.5,3.2),["jointL","jointR"],color=ICE),
       B("keepL","stone",(-1.4,5.4,0),(1.8,2.4,2.6),["hall"],color=ICE),B("keepR","stone",(1.4,5.4,0),(1.8,2.4,2.6),["hall"],color=ICE),
       B("battlement","stone",(0,7.0,0),(5,.8,3.2),["keepL","keepR"],color=SNOW),
       B("spire","steel",(0,8.2,0),(1,1.6,1),["battlement"],color=FROST_STEEL),
       B("frontWall","stone",(-3.6,2.6,2.0),(1.2,2.4,.4),["ground"],role="wall",color=ICE,nonObj=True)]
    return b

# ---------------- Season 3: Neon City (c3, levels 21-30) ----------------
def c3_01():  # Easy: neon kiosk on one steel joint pole
    return [GROUND(6,5),ROCK("plinth",(0,1.9,1.5),(.6,1.4,.6)),
      B("pole","steel",(0,2.85,1.5),(.8,.5,.8),["plinth"],joint=True,role="steel_joint",color=NEON_STEEL),
      B("deck","steel",(0,3.3,0),(3.4,.3,2.6),["pole"],color=NEON_STEEL),
      B("paneF","glass",(0,4.1,1.2),(3.2,1.4,.15),["deck"],role="wall",color=NEON_GLASS),B("paneB","glass",(0,4.1,-1.2),(3.2,1.4,.15),["deck"],role="wall",color=NEON_GLASS),
      B("sign","brick",(0,5.1,0),(3.4,.6,2.6),["paneF","paneB"],color=CORE)]
def c3_02():  # Medium: glass atrium on two brick columns; glass skins are cheap decoys
    b=[GROUND(8,5)]
    for s,x in (("L",-1.8),("R",1.8)): b.append(B(f"col{s}","brick",(x,2.4,.9),(.6,2.4,.6),["ground"],role="column"))
    b+=[B("ring","steel",(0,3.85,0),(4.4,.5,2.8),["colL","colR"],color=NEON_STEEL)]
    for i,z in enumerate((1.2,-1.2)): b.append(B(f"glass{i}","glass",(0,5.0,z),(4,1.8,.15),["ring"],role="wall",color=NEON_GLASS))
    b+=[B("dome","steel",(0,6.2,0),(3.6,.6,2.6),["glass0","glass1"],color=NEON_STEEL),
        B("lobbyGlass","glass",(0,2.4,1.6),(2.6,2.2,.15),["ground"],role="wall",color=NEON_GLASS,nonObj=True)]
    return b
def c3_03():  # Medium, Guided Rocket forced: rooftop billboard on a joint behind two steel hoardings; its left side is open
    return [GROUND(9,5),
      B("hoardA","steel",(-1.2,2.9,2.2),(1.6,3.4,.2),["ground"],role="wall",nonObj=True,color=NEON_STEEL),
      B("hoardB","steel",(-1.2,2.9,1.8),(1.6,3.4,.2),["ground"],role="wall",nonObj=True,color=NEON_STEEL),
      ROCK("stub",(-1.2,2.4,1.0),(.6,2.4,.6)),
      B("jointA","steel",(-1.2,3.85,1.0),(.8,.5,.8),["stub"],joint=True,role="steel_joint",color=NEON_STEEL),
      B("legB","brick",(1.4,2.6,1.0),(.6,2.8,.6),["ground"],role="column"),
      B("frame","steel",(0,4.3,0),(4,.4,2.4),["jointA","legB"],color=NEON_STEEL),
      B("board","glass",(0,5.6,.6),(4,2.2,.2),["frame"],color=NEON_GLASS),B("boardBack","brick",(0,5.6,-.4),(3.6,2.2,.6),["frame"]),
      B("topLight","glass",(0,7.0,0),(3.8,.4,1.2),["board","boardBack"],color=CORE)]
def c3_04():  # Hard: energy-core tower: a core blast clears glass + brick but not steel; two joints still needed
    b=[GROUND(10,6),B("core","glass",(0,1.7,2.6),(.8,1.0,.8),["ground"],nonObj=True,explosive=True,color=CORE,role="core"),
       B("glassL","glass",(-1.2,2.1,2.4),(1.0,1.8,.15),["ground"],role="wall",color=NEON_GLASS),B("glassR","glass",(1.2,2.1,2.4),(1.0,1.8,.15),["ground"],role="wall",color=NEON_GLASS),
       ROCK("pierL",(-1.6,2.2,1.6),(.6,2.0,.6)),ROCK("pierR",(1.6,2.2,1.6),(.6,2.0,.6)),
       B("jointL","steel",(-1.6,3.45,1.6),(.9,.5,.9),["pierL"],joint=True,role="steel_joint",color=NEON_STEEL),
       B("jointR","steel",(1.6,3.45,1.6),(.9,.5,.9),["pierR"],joint=True,role="steel_joint",color=NEON_STEEL),
       B("floor1","brick",(0,3.9,0),(4.4,.4,2.8),["jointL","jointR"])]
    for i in range(3): b.append(B(f"tier{i}","glass" if i%2 else "brick",(0,4.9+i*1.4,0),(3.6-i*.4,1.2,2.4-i*.3),["floor1" if i==0 else f"tier{i-1}"],color=NEON_GLASS if i%2 else ""))
    b.append(B("antenna","steel",(0,9.2,0),(.4,1.6,.4),["tier2"],color=NEON_STEEL))
    return b
def c3_05():  # Breather: arcade core chain: one core sets off its neighbours; the arcade's brick legs go
    b=[GROUND(12,5)]
    for i,x in enumerate((-3,-1,1,3)): b.append(B(f"core{i}","glass",(x,1.7,1.6),(.8,1.0,.8),["ground"],nonObj=True,explosive=True,color=CORE,role="core"))
    for i,x in enumerate((-3,-1,1,3)): b.append(B(f"leg{i}","brick",(x,2.4,.4),(.5,2.4,.5),["ground"],role="column"))
    b+=[B("arcade","steel",(0,3.85,0),(7.4,.5,2.4),[f"leg{i}" for i in range(4)],color=NEON_STEEL)]
    for i,x in enumerate((-2.4,0,2.4)): b.append(B(f"booth{i}","glass",(x,4.8,0),(2.2,1.4,2.0),["arcade"],color=NEON_GLASS))
    b.append(B("marquee","brick",(0,5.9,0),(7.4,.6,2.2),["booth0","booth1","booth2"],color="E879F9"))
    return b
def c3_06():  # Medium: sky bridge between two glass towers, each on one brick podium leg
    b=[GROUND(12,5)]
    for s,x in (("L",-3),("R",3)):
        b+=[B(f"leg{s}","brick",(x,2.4,.8),(.8,2.4,.8),["ground"],role="column"),
            B(f"tower{s}","glass",(x,4.6,0),(2.2,2.0,2.2),[f"leg{s}"],color=NEON_GLASS),
            B(f"crown{s}","steel",(x,6.0,0),(2.4,.8,2.4),[f"tower{s}"],color=NEON_STEEL)]
    b+=[B("skybridge","steel",(0,5.6,0),(3.8,.6,1.2),["towerL","towerR"],color=NEON_STEEL),
        B("kiosk","glass",(0,1.9,1.4),(1.6,1.4,1.0),["ground"],role="decoy",color=NEON_GLASS,nonObj=True)]
    return b
def c3_07():  # Hard, Time Freeze forced: penthouse on two joints; a 4-blade hologram turbine sweeps across the right joint
    b=[GROUND(10,6),ROCK("pierL",(-1.8,2.2,1.6),(.6,2.0,.6)),ROCK("pierR",(1.8,2.2,1.6),(.6,2.0,.6)),
       B("jointL","steel",(-1.8,3.45,1.6),(.9,.5,.9),["pierL"],joint=True,role="steel_joint",color=NEON_STEEL),
       B("jointR","steel",(1.8,3.45,1.6),(.9,.5,.9),["pierR"],joint=True,role="steel_joint",color=NEON_STEEL),
       B("slab","brick",(0,3.9,0),(4.6,.4,3),["jointL","jointR"]),
       B("pent","glass",(0,5.0,0),(4,1.8,2.6),["slab"],color=NEON_GLASS),B("penRoof","steel",(0,6.2,0),(4.4,.6,3),["pent"],color=NEON_STEEL),
       B("pool","brick",(0,6.9,0),(3,.8,2),["penRoof"],color=CORE),
       B("garageL","brick",(-1.8,2.0,3.1),(1.2,1.6,.4),["ground"],role="wall",nonObj=True)]
    hub=(1.8,4.75,2.6)   # turbine hub 1.3 m above the right joint, in a plane 1 m in front of it; 90 deg/s
    b.append(SPIN(B("turbineV","glass",hub,(1.16,3.6,.15),["ground"],nonObj=True,indestructible=True,color=CORE,role="moving"),(0,0,1),hub,90))
    b.append(SPIN(B("turbineH","glass",hub,(3.6,1.16,.15),["ground"],nonObj=True,indestructible=True,color=CORE,role="moving"),(0,0,1),hub,90))
    return b
def c3_08():  # Breather: greenhouse on two wooden props
    return [GROUND(8,5),B("propL","wood",(-1.6,1.9,1.2),(.4,1.4,.4),["ground"],role="prop"),B("propR","wood",(1.6,1.9,1.2),(.4,1.4,.4),["ground"],role="prop"),
      B("bed","brick",(0,2.8,0),(4,.4,3),["propL","propR"]),
      B("hothouse","glass",(0,3.9,0),(3.6,1.8,2.6),["bed"],color=NEON_GLASS),B("ridge","steel",(0,5.0,0),(3.8,.4,1.0),["hothouse"],color=NEON_STEEL),
      B("planter","brick",(0,4.0,0),(2.6,.8,1.6),["bed"],color="22C55E")]
def c3_09():  # Medium: data centre: server racks on a raised floor held by one steel joint + one core-adjacent brick leg
    return [GROUND(10,6),ROCK("pier",(-1.8,2.2,1.6),(.6,2.0,.6)),
      B("joint","steel",(-1.8,3.45,1.6),(.9,.5,.9),["pier"],joint=True,role="steel_joint",color=NEON_STEEL),
      B("leg","brick",(1.8,2.4,.8),(.6,2.4,.6),["ground"],role="column"),
      B("core","glass",(3.4,1.7,1.8),(.8,1.0,.8),["ground"],nonObj=True,explosive=True,color=CORE,role="core"),
      B("floor","steel",(0,3.9,0),(4.6,.4,3),["joint","leg"],color=NEON_STEEL),
      B("rackL","steel",(-1.2,5.0,0),(1.4,1.8,2.4),["floor"],color=NEON_STEEL),B("rackR","steel",(1.2,5.0,0),(1.4,1.8,2.4),["floor"],color=NEON_STEEL),
      B("cooling","glass",(0,6.2,0),(4.2,.6,2.6),["rackL","rackR"],color=CORE)]
def c3_10():  # HARD (skull, level 30): Neon Spire: 3 exact shots (glass curtain, then two hidden joints)
    b=[GROUND(12,6),
       B("curtainL","glass",(-1.8,2.4,2.0),(1.6,2.6,.15),["ground"],role="wall",color=NEON_GLASS),
       B("curtainR","brick",(3.8,2.4,2.0),(1.2,2.6,.4),["ground"],role="wall",nonObj=True),
       ROCK("pierL",(-1.8,2.2,1.0),(.8,2.0,.8)),ROCK("pierR",(1.8,2.2,1.0),(.8,2.0,.8)),
       B("jointL","steel",(-1.8,3.45,1.0),(1.0,.5,1.0),["pierL"],joint=True,role="steel_joint",color=NEON_STEEL),
       B("jointR","steel",(1.8,3.45,1.0),(1.0,.5,1.0),["pierR"],joint=True,role="steel_joint",color=NEON_STEEL),
       B("podium","steel",(0,4.0,0),(4.8,.5,3.2),["jointL","jointR"],color=NEON_STEEL)]
    for i in range(4): b.append(B(f"spire{i}","glass" if i%2==0 else "steel",(0,5.2+i*1.3,0),(3.4-i*.6,1.2,2.6-i*.4),["podium" if i==0 else f"spire{i-1}"],color=NEON_GLASS if i%2==0 else NEON_STEEL))
    b.append(B("beacon","glass",(0,10.5,0),(.6,1.0,.6),["spire3"],color=CORE))
    return b

# ---------------- Season 4: Primeval Peaks (c4, levels 31-40) ----------------
def c4_01():  # Easy: fern hut on one giant log
    return [GROUND(6,6),B("log","wood",(0,1.7,.6),(.6,1.0,.6),["ground"],role="support",color="8B5A2B"),
      B("platform","wood",(0,2.4,0),(3.6,.4,3),["log"]),
      B("hut","wood",(0,3.4,0),(3,1.6,2.4),["platform"],color=FERN),B("thatch","wood",(0,4.5,0),(3.6,.6,3),["hut"],color=FERN),
      B("skull","stone",(0,5.1,0),(.8,.6,.8),["thatch"],color=FOSSIL)]
def c4_02():  # Medium: amber arch: a fossil lintel on two amber-crystal pillars; stone faces are traps
    return [GROUND(8,5),B("amberL","glass",(-1.6,2.3,1.0),(.6,2.2,.6),["ground"],role="pillar",color=AMBER),
      B("amberR","glass",(1.6,2.3,1.0),(.6,2.2,.6),["ground"],role="pillar",color=AMBER),
      B("lintel","stone",(0,3.7,0),(4.4,.6,2.6),["amberL","amberR"],color=FOSSIL),
      B("fossilL","stone",(-1,4.8,0),(1.8,1.6,2.2),["lintel"],color=FOSSIL),B("fossilR","stone",(1,4.8,0),(1.8,1.6,2.2),["lintel"],color=FOSSIL),
      B("crest","stone",(0,6.0,0),(4,.8,2.2),["fossilL","fossilR"],color=FOSSIL),
      B("backCliff","stone",(0,2.4,-1.4),(3.6,2.4,.6),["ground"],role="wall",color=FOSSIL,nonObj=True)]
def c4_03():  # Medium: boulder cradle: two heavy boulders on a fern-timber cradle with two legs
    return [GROUND(8,5),B("legL","wood",(-1.6,2.0,1.0),(.5,1.6,.5),["ground"],role="prop",color="8B5A2B"),B("legR","wood",(1.6,2.0,1.0),(.5,1.6,.5),["ground"],role="prop",color="8B5A2B"),
      B("cradle","wood",(0,3.0,0),(4.2,.4,2.6),["legL","legR"],color="8B5A2B"),
      B("boulderL","stone",(-1,4.0,0),(1.8,1.6,1.8),["cradle"],color=FOSSIL),B("boulderR","stone",(1,4.0,0),(1.8,1.6,1.8),["cradle"],color=FOSSIL),
      B("cap","stone",(0,5.2,0),(1.6,.8,1.6),["boulderL","boulderR"],color=FOSSIL),
      B("brace","stone",(0,1.9,1.4),(1.6,1.4,.4),["ground"],role="wall",color=FOSSIL,nonObj=True)]
def c4_04():  # Hard: fossil bridge: stone everywhere; two hidden-in-plain-sight wooden pins hold the span
    b=[GROUND(12,6)]
    for s,x in (("L",-3.2),("R",3.2)):
        b+=[B(f"tower{s}","stone",(x,2.6,-.4),(1.8,2.8,1.8),["ground"],role="wall",color=FOSSIL,nonObj=True),
            B(f"pin{s}","wood",(x*0.55,3.0,1.0),(.4,.6,.4),[f"tower{s}"],role="pin",color="8B5A2B")]
    b+=[B("span","stone",(0,3.6,.2),(5.6,.5,2.2),["pinL","pinR"],color=FOSSIL),
        B("ribs","stone",(0,4.6,.2),(4.8,1.4,1.8),["span"],color=FOSSIL),B("spine","stone",(0,5.6,.2),(5.2,.6,1.2),["ribs"],color=FOSSIL),
        B("skullRock","stone",(0,2.0,1.8),(1.6,1.6,.6),["ground"],role="wall",color=FOSSIL,nonObj=True)]
    return b
def c4_05():  # Breather: tar-pit totems: one volatile amber node sets off two more; the totem legs go
    b=[GROUND(10,5)]
    for i,x in enumerate((-2,0,2)): b.append(B(f"tar{i}","glass",(x,1.7,1.6),(.8,1.0,.8),["ground"],nonObj=True,explosive=True,color=AMBER,role="volatile"))
    for i,x in enumerate((-2,0,2)): b.append(B(f"leg{i}","wood",(x,2.3,.4),(.5,2.2,.5),["ground"],role="prop",color="8B5A2B"))
    b+=[B("deck","wood",(0,3.6,0),(5.6,.4,2.4),["leg0","leg1","leg2"],color="8B5A2B")]
    for i,x in enumerate((-1.8,0,1.8)): b.append(B(f"totem{i}","stone",(x,4.8,0),(1.2,2.0,1.2),["deck"],color=FOSSIL))
    b+=[B(f"fern{i}","wood",(x,1.8,-1.6),(1.0,1.2,1.0),["ground"],nonObj=True,color=FERN,role="decoy") for i,x in enumerate((-3.6,3.6))]
    b+=[B(f"boulder{i}","stone",(x,1.9,1.8),(1.0,1.4,1.0),["ground"],nonObj=True,color=FOSSIL,role="decoy") for i,x in enumerate((-4.2,4.2))]
    return b
def c4_06():  # Medium: cliff dwelling: two timber platforms pinned to a grey cliff by one post each; upper hangs off lower
    return [GROUND(10,6),ROCK("cliff",(0,4.0,-1.8),(8,8,1.2)),
      B("postL","wood",(-2,2.0,1.0),(.5,1.6,.5),["ground"],role="prop",color="8B5A2B"),
      B("platL","wood",(-2,3.0,0),(3,.4,2.4),["postL"],color="8B5A2B"),
      B("homeL","stone",(-2,4.0,0),(2.4,1.6,1.8),["platL"],color=FOSSIL),
      B("postR","wood",(2,2.0,1.0),(.5,1.6,.5),["ground"],role="prop",color="8B5A2B"),
      B("platR","wood",(2,3.0,0),(3,.4,2.4),["postR"],color="8B5A2B"),
      B("homeR","stone",(2,4.0,0),(2.4,1.6,1.8),["platR"],color=FOSSIL),
      B("ledge","wood",(0,5.2,0),(6.2,.4,1.6),["homeL","homeR"],color="8B5A2B"),
      B("lookout","stone",(0,6.1,0),(2,1.4,1.4),["ledge"],color=FOSSIL)]
def c4_07():  # Hard: standing-stone gate: lintels on stone pillars (90 HP); two amber wedges are the real weak points
    b=[GROUND(12,6)]
    for s,x in (("L",-2.4),("R",2.4)):
        b+=[B(f"pillar{s}","stone",(x,2.6,1.1),(1.4,2.8,1.4),["ground"],role="wall",color=FOSSIL,nonObj=True),
            B(f"wedge{s}","glass",(x,4.25,1.1),(1.0,.5,1.0),[f"pillar{s}"],role="wedge",color=AMBER)]
    b+=[B("lintel","stone",(0,4.8,.2),(6.2,.6,1.6),["wedgeL","wedgeR"],color=FOSSIL),
        B("capstone","stone",(0,5.6,.2),(3.6,1.0,1.4),["lintel"],color=FOSSIL),
        B("altar","stone",(0,1.8,1.6),(1.6,1.2,.8),["ground"],role="wall",color=FOSSIL,nonObj=True)]
    return b
def c4_08():  # Breather: nest tower: a giant nest on one timber trunk
    return [GROUND(6,6),B("trunk","wood",(0,2.4,.6),(.7,2.4,.7),["ground"],role="support",color="8B5A2B"),
      B("fork","wood",(0,3.9,0),(3,.6,3),["trunk"],color="8B5A2B"),
      B("nest","wood",(0,4.8,0),(3.4,1.2,3.4),["fork"],color=FERN),
      B("eggA","stone",(-.6,5.8,0),(1,1.2,1),["nest"],color="F1E3C3"),B("eggB","stone",(.6,5.8,0),(1,1.2,1),["nest"],color="F1E3C3")]+[
      B(f"fern{i}","wood",(x,1.9,z),(1.0,1.4,1.0),["ground"],nonObj=True,color=FERN,role="decoy") for i,(x,z) in enumerate(((-2.4,1.6),(2.4,1.6),(-2.4,-1.6),(2.4,-1.6)))]+[
      B("bigEgg","stone",(1.6,4.4,0),(1.0,1.2,1.0),["fork"],color="F1E3C3")]
def c4_09():  # Medium: waterfall mill: wheel house on a timber pier + amber crystal brace
    return [GROUND(10,6),B("pier","wood",(-1.6,2.2,1.0),(.5,2.0,.5),["ground"],role="prop",color="8B5A2B"),
      B("crystal","glass",(1.6,2.2,1.0),(.6,2.0,.6),["ground"],role="pillar",color=AMBER),
      B("mill","stone",(0,3.6,0),(4.4,.6,3),["pier","crystal"],color=FOSSIL),
      B("house","wood",(0,4.8,0),(3.6,1.8,2.4),["mill"],color="8B5A2B"),B("roof","stone",(0,6.0,0),(4,.6,2.8),["house"],color=FOSSIL),
      SPIN(B("wheel","wood",(2.6,3.0,0),(.3,2.4,2.4),["mill"],nonObj=True,color="8B5A2B",role="moving"),(1,0,0),(2.6,3.0,0),45),
      B("chute","stone",(-2.6,2.4,0),(.8,2.4,1.6),["ground"],role="wall",color=FOSSIL,nonObj=True)]
def c4_10():  # HARD (skull, level 40): Primeval Fortress: 3 exact shots: amber gate crystal, then the two timber pins
    b=[GROUND(12,6),
       B("gateCrystal","glass",(1.8,2.6,2.0),(1.6,2.4,.4),["ground"],role="gate",color=AMBER),
       B("bonewall","stone",(-3.8,2.6,2.0),(1.2,2.4,.6),["ground"],role="wall",color=FOSSIL,nonObj=True),
       ROCK("pierL",(-1.8,2.2,1.0),(.8,2.0,.8)),ROCK("pierR",(1.8,2.2,1.0),(.8,2.0,.8)),
       B("pinL","wood",(-1.8,3.45,1.0),(1.0,.5,1.0),["pierL"],role="pin",color="8B5A2B"),
       B("pinR","wood",(1.8,3.45,1.0),(1.0,.5,1.0),["pierR"],role="pin",color="8B5A2B"),
       B("keepFloor","stone",(0,4.0,0),(4.8,.5,3.2),["pinL","pinR"],color=FOSSIL),
       B("keepL","stone",(-1.4,5.4,0),(1.8,2.4,2.6),["keepFloor"],color=FOSSIL),B("keepR","stone",(1.4,5.4,0),(1.8,2.4,2.6),["keepFloor"],color=FOSSIL),
       B("tusks","stone",(0,7.0,0),(5,.8,3.2),["keepL","keepR"],color="F1E3C3"),
       B("amberCrown","glass",(0,8.0,0),(1.4,1.2,1.4),["tusks"],color=AMBER)]
    return b

def face_wrecker(b):
    x,y,z=b["p"]; sx,sy,sz=b["s"]; b=dict(b); b["p"]=[round(-z,4),y,x]; b["s"]=[sz,sy,sx]
    if b.get("pivot"): px,py,pz=b["pivot"]; b["pivot"]=[round(-pz,4),py,px]
    if b.get("spinAxis"): ax,ay,az=b["spinAxis"]; b["spinAxis"]=[-az,ay,ax]
    return b

def M(type,value=0,text="",role=""): return dict(type=type,value=value,role=role,text=text)
def F(slot,pw): return [dict(slot=slot,powerup=pw)]
# (chapterIdx, n, slug, name, island, fn, shots, materials, teaches, mastery, forced, tag, star3, star2, hero(kind,pos,rotY))
SEASONS={
 2:dict(id="c2_frozen_peaks",name="Frozen Peaks",next="Neon City",prev="c1_l10",islands=[("glacier","Glacier Gate",[0,0,0],""),("pinewood","Pinewood Ridge",[22,4,8],"c2_l03"),("icefall","Icefall Pass",[44,8,0],"c2_l06"),("summit","Frost Summit",[64,12,10],"c2_l09")],
  levels=[(1,"ski_lodge","Ski Lodge","glacier",c2_01,4,["wood","stone"],"New material: snow-stone (90 HP) is tough, hit the timber","DESTROYED_AT_LEAST:0.95:>=95% destroyed",None,"Easy",.6,.3,("keep",[9,0,0],0)),
   (2,"snow_chalet","Snow-Stone Chalet","glacier",c2_02,5,["wood","brick","stone"],"Stone faces cost 2 shots: take the props","THREE_STARS_NO_ROLE_HITS::3 stars with no wall hits:wall",None,"Medium",.6,.3,("gatehouse",[9,0,0],0)),
   (3,"frost_depot","Frost Depot","glacier",c2_03,5,["wood","steel","stone"],"Mega Bomb: one blast takes the whole stilt grid","CLEAR_IN_SHOTS:1:Clear in 1 shot",F("Core","mega_bomb"),"Medium",.6,.3,("keep",[9,0,0],0)),
   (4,"ice_bridge_fort","Ice-Bridge Fort","pinewood",c2_04,5,["brick","steel","stone"],"Frosted steel joints among stone decoys","THREE_STARS_NO_ROLE_HITS::3 stars with no wall hits:wall",None,"Hard",.6,.3,("gatehouse",[9,0,0],0)),
   (5,"snow_cannons","Snow-Cannon Row","pinewood",c2_05,5,["wood","brick"],"Shield: the canister hides behind a snow fence; a shielded miss is free","STARS:3:3 stars",F("Booster","shield"),"Breather",.6,.3,("keep",[9,0,0],0)),
   (6,"avalanche_shelter","Avalanche Shelter","pinewood",c2_06,5,["steel","stone"],"Steel joints take 2x under a heavy snow roof","ALL_ROLE_BROKEN::Break both braces:brace",None,"Medium",.6,.3,("gatehouse",[9,0,0],0)),
   (7,"glacier_vault","Glacier Vault","icefall",c2_07,5,["wood","brick","steel","stone"],"Laser Beam: pierce two slabs to the hidden key","STARS:3:3 stars",F("Booster","laser_beam"),"Hard",.6,.3,("keep",[9,0,0],0)),
   (8,"icicle_gantry","Icicle Gantry","icefall",c2_08,5,["wood","stone"],"Breather: two posts drop a row of icicles","SHOTS_LEFT_AT_LEAST:3:Win with 3+ shots left",None,"Breather",.6,.3,("gatehouse",[9,0,0],0)),
   (9,"cable_car_station","Cable-Car Station","icefall",c2_09,5,["wood","brick","steel","stone"],"Mixed holders: one joint, one post; ignore the gondola","NO_POWERUP::No power-up used",None,"Medium",.6,.3,("keep",[9,0,0],0)),
   (10,"frost_citadel","Frost Citadel","summit",c2_10,7,["wood","brick","steel","stone"],"Hard level: open the gate, then both joints","NO_POWERUP::No power-up used",None,"Hard (skull)",.5,.33,("gatehouse",[9,0,0],0))]),
 3:dict(id="c3_neon_city",name="Neon City",next="Primeval Peaks",prev="c2_l10",islands=[("downtown","Downtown Deck",[0,0,0],""),("arcade","Arcade Row",[22,4,8],"c3_l03"),("skyline","Skyline Heights",[44,8,0],"c3_l06"),("spire","Spire Plaza",[64,12,10],"c3_l09")],
  levels=[(1,"neon_kiosk","Neon Kiosk","downtown",c3_01,4,["brick","steel","glass"],"New material: glass (12 HP) shatters but holds nothing","DESTROYED_AT_LEAST:0.95:>=95% destroyed",None,"Easy",.6,.3,("keep",[9,0,0],0)),
   (2,"glass_atrium","Glass Atrium","downtown",c3_02,5,["brick","steel","glass"],"Glass skins are decoys: hit the columns","THREE_STARS_NO_ROLE_HITS::3 stars with no wall hits:wall",None,"Medium",.6,.3,("gatehouse",[9,0,0],0)),
   (3,"rooftop_billboard","Rooftop Billboard","downtown",c3_03,5,["brick","steel","glass"],"Guided Rocket: steer round the steel hoardings to the joint","CLEAR_IN_SHOTS:2:Clear in 2 shots",F("Booster","guided_rocket"),"Medium",.6,.3,("keep",[9,0,0],0)),
   (4,"core_tower","Energy-Core Tower","arcade",c3_04,5,["brick","steel","glass"],"Energy cores blast glass and brick, not steel","ALL_ROLE_BROKEN::Break both joints:steel_joint",None,"Hard",.6,.3,("gatehouse",[9,0,0],0)),
   (5,"core_arcade","Core Arcade","arcade",c3_05,5,["brick","steel","glass"],"Breather: a core chain clears the arcade","CLEAR_IN_SHOTS:1:Clear in 1 shot",None,"Breather",.6,.3,("keep",[9,0,0],0)),
   (6,"sky_bridge","Sky Bridge","arcade",c3_06,5,["brick","steel","glass"],"Two towers, one bridge: drop both legs","DESTROYED_AT_LEAST:0.95:>=95% destroyed",None,"Medium",.6,.3,("gatehouse",[9,0,0],0)),
   (7,"penthouse","Spinning Penthouse","skyline",c3_07,5,["brick","steel","glass"],"Time Freeze: slow the turbine to shoot through its gap","STARS:3:3 stars",F("Booster","time_freeze"),"Hard",.6,.3,("keep",[9,0,0],0)),
   (8,"greenhouse","Rooftop Greenhouse","skyline",c3_08,5,["wood","brick","steel","glass"],"Breather: two props, one glass house","DESTROYED_AT_LEAST:1.0:100% destroyed",None,"Breather",.6,.3,("gatehouse",[9,0,0],0)),
   (9,"data_center","Data Center","skyline",c3_09,5,["brick","steel","glass"],"Use the core next to the leg; save the joint for last","NO_POWERUP::No power-up used",None,"Medium",.6,.3,("keep",[9,0,0],0)),
   (10,"neon_spire","Neon Spire","spire",c3_10,7,["brick","steel","glass"],"Hard level: shatter the curtain, then both joints","NO_POWERUP::No power-up used",None,"Hard (skull)",.5,.33,("gatehouse",[9,0,0],0))]),
 4:dict(id="c4_primeval_peaks",name="Primeval Peaks",next="",prev="c3_l10",islands=[("fernvale","Fern Vale",[0,0,0],""),("amberfalls","Amber Falls",[22,4,8],"c4_l03"),("fossilcliffs","Fossil Cliffs",[44,8,0],"c4_l06"),("titanpeak","Primeval Summit",[64,12,10],"c4_l09")],
  levels=[(1,"fern_hut","Fern Hut","fernvale",c4_01,4,["wood","stone"],"One log holds the hut","DESTROYED_AT_LEAST:0.95:>=95% destroyed",None,"Easy",.6,.3,("keep",[9,0,0],0)),
   (2,"amber_arch","Amber Arch","fernvale",c4_02,5,["stone","glass"],"Amber crystal pillars are brittle; fossil stone isn't","THREE_STARS_NO_ROLE_HITS::3 stars with no wall hits:wall",None,"Medium",.6,.3,("gatehouse",[9,0,0],0)),
   (3,"boulder_cradle","Boulder Cradle","fernvale",c4_03,5,["wood","stone"],"Heavy boulders crush what they land on","DESTROYED_AT_LEAST:0.95:>=95% destroyed",None,"Medium",.6,.3,("keep",[9,0,0],0)),
   (4,"fossil_bridge","Fossil Bridge","amberfalls",c4_04,5,["wood","stone"],"Find the timber pins in a wall of stone","THREE_STARS_NO_ROLE_HITS::3 stars with no wall hits:wall",None,"Hard",.6,.3,("gatehouse",[9,0,0],0)),
   (5,"tar_totems","Tar-Pit Totems","amberfalls",c4_05,5,["wood","stone"],"Breather: volatile amber chains","CLEAR_IN_SHOTS:1:Clear in 1 shot",None,"Breather",.6,.3,("keep",[9,0,0],0)),
   (6,"cliff_dwelling","Cliff Dwelling","amberfalls",c4_06,5,["wood","stone"],"Two platforms, one ledge between","DESTROYED_AT_LEAST:0.95:>=95% destroyed",None,"Medium",.6,.3,("gatehouse",[9,0,0],0)),
   (7,"standing_stones","Standing-Stone Gate","fossilcliffs",c4_07,5,["stone","glass"],"Amber wedges, not 90-HP pillars","THREE_STARS_NO_ROLE_HITS::3 stars with no wall hits:wall",None,"Hard",.6,.3,("gatehouse",[9,0,0],0)),
   (8,"nest_tower","Nest Tower","fossilcliffs",c4_08,5,["wood","stone"],"Breather: one trunk, one big nest","CLEAR_IN_SHOTS:1:Clear in 1 shot",None,"Breather",.6,.3,("keep",[9,0,0],0)),
   (9,"waterfall_mill","Waterfall Mill","fossilcliffs",c4_09,5,["wood","stone","glass"],"Timber and amber hold the mill; the wheel is decor","NO_POWERUP::No power-up used",None,"Medium",.6,.3,("keep",[9,0,0],0)),
   (10,"primeval_fortress","Primeval Fortress","titanpeak",c4_10,7,["wood","stone","glass"],"Hard level: crack the amber gate, then both pins","NO_POWERUP::No power-up used",None,"Hard (skull)",.5,.33,("gatehouse",[9,0,0],0))])}

def build():
    os.makedirs(LAY,exist_ok=True); meta={}
    for c,S in SEASONS.items():
        levels=[]; prev=S["prev"]
        for (n,slug,name,isl,fn,shots,mats,teach,mast,forced,tag,s3,s2,hero) in S["levels"]:
            lid=f"c{c}_l{n:02d}"; layout=f"{lid}_{slug}"
            blocks=[face_wrecker(b) for b in fn()]
            for b in blocks:
                assert b["mat"] in mats or b.get("nonObj"), (lid,b["id"],b["mat"])
            json.dump({"id":layout,"blocks":blocks},open(os.path.join(LAY,layout+".json"),"w"),indent=1)
            t,v,txt,*role=mast.split(":")
            number=(c-1)*10+n
            levels.append({"id":lid,"number":number,"island":isl,"name":name,"layout":layout,"objective":"DESTROY_STRUCTURE",
                "shots":shots,"winThreshold":0.85,"materials":mats,"teaches":teach,
                "mastery":{"type":t,"value":float(v) if v else 0,"role":role[0] if role else "","text":txt},
                "forced":forced or [],"lockedSlots":[],"requires":prev,"bossId":"","reward":[],
                "star3Pct":s3,"star2Pct":s2,"starCapIfPowerupUsed":2 if t=="NO_POWERUP" and n==9 else 3,"islandKillY":-2.0,
                "hintTarget":"","hintYaw":0.0,"hintPitch":0.0,
                "heroSlot":{"kind":hero[0],"fbx":"","pos":hero[1],"rotY":hero[2]}})
            meta[lid]={"tag":tag}
            prev=lid
        levels[-1]["reward"]=[{"type":"coins","amount":500,"id":""}]+([{"type":"gate","amount":1,"id":S["next"].lower().replace(" ","_")}] if S["next"] else [])
        ch={"id":S["id"],"name":S["name"],"nextRegion":S["next"],"islands":[{"id":i,"name":nm,"pos":p,"gateAfterLevel":g} for i,nm,p,g in S["islands"]],"levels":levels}
        json.dump(ch,open(os.path.join(OUT,f"chapter{c}.json"),"w"),indent=1)
    json.dump(meta,open(os.path.join(HERE,"level_tags.json"),"w"),indent=1)
if __name__=="__main__": build()
