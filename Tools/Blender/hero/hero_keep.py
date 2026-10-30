# Bld_CastleKeep: square sandstone keep (6 x 6 m, 3 storeys) with four round corner turrets (4 rings, corbelled crowns,
# red conical roofs + flags), machicolated battlements, arched windows with blue shutters, banners, studded gate.
# usage: blender -b --python hero_keep.py -- [size] [Module,Module]
import sys,os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from hero_parts import *
B="Keep"; L=4.2; TH=0.6; H=3.0; y0,y1=-TH/2,TH/2; RT=1.2
def band(z=2.82): return box(-L/2,L/2,y0-0.07,y0+0.05,z,z+0.16,"stone_dk",bev=0.02)
def plinth(xa=-L/2,xb=L/2): return P([Vector((x,y,z)) for x in (xa,xb) for (y,z) in ((y0-0.2,0),(y0-0.2,0.3),(y0,0.62),(y0,0))],"stone_dk",bev=0.02)
def wall_window(banners=False,flowers=False,balc=False):
    P_=slab_with_arch(-L/2,L/2,y0,y1,0,H,0,0.8,0.9,1.9,nvous=5)
    P_+=window_arch(0,y0,TH,0.8,0.9,1.9,sill=not balc); P_.append(band())
    if flowers: P_+=flower_box(0,y0-0.1,0.78,1.0)
    if balc: P_+=balcony(0,y0,0.86,1.7,0.7,posts=6)+beam_ends((-1.3,1.3),y0,0.4)+vine(-1.6,y0,0.0,2.7,seed=5,spread=0.25)
    if banners:
        for s in (-1,1): P_+=banner(s*1.45,y0-0.1,2.62,0.62,1.75,y_wall=y0)
    return P_
def wall_slit():
    P_=[box(-L/2,-0.07,y0,y1,0,H,"stone",bev=0.03),box(0.07,L/2,y0,y1,0,H,"stone",bev=0.03),box(-0.07,0.07,y0,y1,0,1.0,"stone"),box(-0.07,0.07,y0,y1,2.1,H,"stone")]
    for s in (-1,1): P_.append(box(min(s*0.07,s*0.27),max(s*0.07,s*0.27),y0-0.04,y0+0.02,1.0,2.1,"stone_dk",detail=True,bev=0.015))
    P_.append(box(-0.27,0.27,y0-0.05,y0+0.02,2.1,2.3,"stone_dk",detail=True,bev=0.015)); P_.append(box(-0.27,0.27,y0-0.07,y0+0.02,0.88,1.0,"stone_dk",detail=True,bev=0.015))
    P_.append(box(-0.07,0.07,0.2,0.24,1.0,2.1,"iron",detail=True))
    P_+=[plinth(),band()]+lantern(-1.25,y0,1.75)+lantern(1.25,y0,1.75)+vine(1.6,y0,0.3,2.75,seed=2,spread=0.3)
    return P_
def wall_door():
    w=1.5; r=w/2; zs=1.9
    P_=slab_with_arch(-L/2,L/2,y0,y1,0,H,0,w,0.0,zs,nvous=6)
    n=7
    for i in range(n):   # archivolt
        a0=math.pi*i/n; a1=math.pi*(i+1)/n; r2=r+0.24
        P_.append(prism_xz([(r*math.cos(a0),zs+r*math.sin(a0)),(r*math.cos(a1),zs+r*math.sin(a1)),(r2*math.cos(a1),zs+r2*math.sin(a1)),(r2*math.cos(a0),zs+r2*math.sin(a0))],y0-0.08,y0+0.02,"stone_dk",detail=True))
    for s in (-1,1): P_.append(box(min(s*r,s*(r+0.24)),max(s*r,s*(r+0.24)),y0-0.06,y0+0.02,0,zs,"stone_dk",detail=True,bev=0.015))
    yd0,yd1=-0.06,0.06
    for s in (-1,1):
        P_.append(box(min(0,s*r),max(0,s*r),yd0,yd1,0,zs,"wood",detail=True,bev=0.01))
        P_.append(prism_xz([(0,zs)]+[(s*r*math.cos(math.pi/2*k/4),zs+r*math.sin(math.pi/2*k/4)) for k in range(5)],yd0,yd1,"wood",detail=True))
        for zz in (0.35,1.0,1.65): P_.append(box(min(0,s*(r-0.04)),max(0,s*(r-0.04)),yd0-0.02,yd0,zz,zz+0.08,"iron",detail=True))
        P_.append(box(s*0.12-0.04,s*0.12+0.04,yd0-0.07,yd0,1.0,1.14,"iron",detail=True))
    P_.append(box(-r-0.3,r+0.3,y0-0.45,y0+0.05,0,0.14,"stone_dk",bev=0.02))
    P_+=[plinth(-L/2,-r-0.24),plinth(r+0.24,L/2),band()]
    P_+=lantern(-1.35,y0,1.85)+lantern(1.35,y0,1.85)
    P_+=[box(-0.36,0.36,y0-0.05,y0+0.02,zs+r+0.26,zs+r+0.36,"stone_dk",detail=True,bev=0.015)]
    return P_
def turret(slit=True):
    nw=12; P_=ring(0.75,RT,0,H,nw,skip=(0,) if slit else ())
    if slit:
        da=2*math.pi/nw; d=0.07/RT
        P_+=[wedge(0.75,RT,-da/2,da/2,0,1.0,"stone"),wedge(0.75,RT,-da/2,da/2,2.1,H,"stone"),wedge(0.75,RT,-da/2,-d,1.0,2.1,"stone",n=1),wedge(0.75,RT,d,da/2,1.0,2.1,"stone",n=1)]
        for s in (-1,1): P_.append(wedge(RT-0.05,RT+0.04,min(s*d,s*(d+0.15)),max(s*d,s*(d+0.15)),1.0,2.1,"stone_dk",n=1,detail=True))
        P_.append(wedge(RT-0.05,RT+0.05,-d-0.15,d+0.15,2.1,2.28,"stone_dk",n=1,detail=True))
    P_+=ring(1.1,RT+0.08,2.82,2.98,nw,cls="stone_dk",a0=math.pi/nw)
    return P_
def turret_crown():
    nw=12; da=2*math.pi/nw; P_=[]
    for i in range(nw):
        a=i*da; Rm=Matrix.Rotation(a,4,'Z')
        c=P([Vector((x,-r,z)) for x in (-0.11,0.11) for (r,z) in ((1.1,0),(1.3,0),(1.1,0.45),(1.5,0.45))],"stone_dk",detail=True)
        xf([c],Rm); P_.append(c)
    P_+=ring(1.05,1.52,0.45,1.25,nw,a0=da/2)
    P_+=ring(1.4,1.58,1.18,1.3,nw,cls="stone_dk",a0=da/2,n=2)
    return P_
def turret_roof(flag_dir=1): return cone_roof(1.78,1.5,0,3.4,seg=20,courses=6,flag_dir=flag_dir)+cone_dormer(1.45,0.5,0.72)
def crown():
    P_=[box(-L/2,L/2,y0,y1,0,0.4,"stone"),box(-L/2,L/2,y0-0.36,y0+0.06,0.4,1.0,"stone",bev=0.03),box(-L/2,L/2,y1-0.16,y1,0.4,0.85,"stone")]
    for x in (-1.75,-1.05,-0.35,0.35,1.05,1.75):
        P_.append(P([Vector((xx,y,z)) for xx in (x-0.12,x+0.12) for (y,z) in ((y0,0.0),(y0-0.12,0.0),(y0,0.4),(y0-0.36,0.4))],"stone_dk",detail=True))
    for x in (-1.5,-0.5,0.5,1.5):
        P_.append(box(x-0.3,x+0.3,y0-0.36,y0+0.06,1.0,1.55,"stone",bev=0.03))
        P_.append(box(x-0.34,x+0.34,y0-0.4,y0+0.1,1.55,1.64,"stone_dk",detail=True,bev=0.02))
    return P_
def floor():
    P_=[box(-2.8,2.8,-2.8,2.8,-0.16,0,"wood")]
    for x in (-1.8,-0.6,0.6,1.8): P_.append(box(x-0.11,x+0.11,-2.8,2.8,-0.38,-0.16,"wood_dk"))
    P_.append(box(-0.45,0.45,0.9,1.8,0,0.03,"wood_dk",detail=True))
    return P_
MODS=[Module(B,"WallDoor","Stone",wall_door(),hide=("bottom","top"),note="ground storey front, studded double gate + lanterns",interior="y+"),
      Module(B,"WallSlit","Stone",wall_slit(),hide=("bottom","top"),note="ground storey side, arrow slit, battered plinth, lanterns",interior="y+"),
      Module(B,"Wall","Stone",wall_window(flowers=True),"bottom","Support",hide=("bottom","top"),note="upper storey, arched window + shutters",interior="y+"),
      Module(B,"WallBanner","Stone",wall_window(True),"bottom","Support",hide=("bottom","top"),note="upper storey front, window + two banners",interior="y+"),
      Module(B,"WallBalcony","Stone",wall_window(balc=True),"bottom","Support",hide=("bottom","top"),note="upper storey, window onto a timber balcony, beam ends, ivy",interior="y+"),
      Module(B,"Turret","Stone",turret(),"bottom","Support",hide=("bottom","top"),note="corner turret ring (stack x4), slit faces outwards",interior="centre"),
      Module(B,"TurretCrown","Stone",turret_crown(),"bottom","Support",hide=("bottom","top"),note="corbelled turret crown"),
      Module(B,"TurretRoof","Roof",turret_roof(),"bottom","Support",note="red conical roof, finial, flag"),
      Module(B,"Crown","Stone",crown(),"bottom","Support",hide=("bottom",),note="machicolated battlement on a wall top"),
      Module(B,"Floor","Wood",floor(),"perimeter","Support",uvscale=0.45,note="storey floor, anchors on the walls")]
def layout():
    Lo=Layout("Bld_CastleKeep"); sides=[("F",(0,-3),0),("B",(0,3),180),("L",(-3,0),-90),("R",(3,0),90)]; W={}
    for s in range(3):
        for k,(x,y),r in sides:
            mod={0:"WallDoor" if k=="F" else "WallSlit",1:"WallBanner" if k=="F" else ("WallBalcony" if k=="R" else "Wall"),2:"WallBalcony" if k=="B" else "Wall"}[s]
            W[(s,k)]=Lo.add(f"S{s}_{k}_{mod}",B,mod,T(x,y,3*s)@Rz(r),"Ground" if s==0 else [W[(s-1,k)]])
    corners=[(-3,-3),(3,-3),(3,3),(-3,3)]; TT={}
    for ci,(x,y) in enumerate(corners):
        th=math.degrees(math.atan2(x,-y))
        ns=5 if ci in (2,3) else 4      # back turrets one storey taller: stepped silhouette
        for s in range(ns): TT[(ci,s)]=Lo.add(f"T{ci}_S{s}_Turret",B,"Turret",T(x,y,3*s)@Rz(th+(30 if s%2 else 0)),"Ground" if s==0 else [TT[(ci,s-1)]])
        c=Lo.add(f"T{ci}_Crown",B,"TurretCrown",T(x,y,3*ns)@Rz(th),[TT[(ci,ns-1)]])
        Lo.add(f"T{ci}_Roof",B,"TurretRoof",T(x,y,3*ns+1.3)@Rz(th),[c])
    for k,(x,y),r in sides: Lo.add(f"Top_{k}_Crown",B,"Crown",T(x,y,9)@Rz(r),[W[(2,k)]])
    for s in (1,2,3): Lo.add(f"Floor_{s}",B,"Floor",T(0,0,3*s),[W[(s-1,k)] for k in "FBLR"])
    Lo.add("Annex_L","Props","Shed",T(-4.2,0.0,0)@Rz(-90))      # lean-to annex on the left wall
    # props at the gate
    for i,(x,y,r) in enumerate([(-2.0,-4.0,10),(-2.55,-3.75,40),(-2.2,-4.55,75)]): Lo.add(f"Barrel_{i}","Props","Barrel",T(x,y,0)@Rz(r))
    for i,(x,y,r) in enumerate([(2.2,-4.1,15),(2.75,-3.8,-20)]): Lo.add(f"Crate_{i}","Props","Crate",T(x,y,0)@Rz(r))
    Lo.add("Crate_2","Props","CrateSmall",T(2.35,-4.15,0.8)@Rz(30),["Crate_0"])
    return Lo
if __name__=="__main__":
    A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    size=int(A[0]) if A and A[0] else 2048
    if len(A)>1 and A[1]=="layout": write_layout(layout())
    else:
        build_set(B,MODS,size=size)
