# Bld_Watchtower: round sandstone watchtower (dia 4 m, 3 rings), projecting door portal, arched windows with shutters
# and flower boxes, corbelled crown, tall red conical roof with two dormers, finial and pennant.
import sys,os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from hero_parts import *
B="Tower"; RO=2.0; RI=1.4; H=3.0; NW=16
def flowerbox(xc,y_out,z,w=0.8):
    return [box(xc-w/2,xc+w/2,y_out-0.26,y_out-0.02,z-0.2,z,"wood",detail=True),
            P([Vector((xc+x,y_out+y,z+zz)) for x in (-w/2+0.05,w/2-0.05) for y in (-0.24,-0.04) for zz in (0,)]+
              [Vector((xc+x*0.8,y_out-0.14+y*0.6,z+0.16)) for x in (-w/2+0.05,w/2-0.05) for y in (-0.1,0.1)],"leaf",detail=True)]
def portal_window(a,z0=0.6,z1=2.6,w=0.7,zs=1.85,zb=1.05,flowers=True):
    """flat projecting window surround on the ring at angle a (deg)"""
    yo=-RO-0.1; P_=slab_with_arch(-0.75,0.75,yo,-RI+0.15,z0,z1,0,w,zb,zs,cls="stone_dk",bev=0.025,nvous=5)
    P_+=window_arch(0,yo,0.6,w,zb,zs,archivolt=True)
    if flowers: P_+=flowerbox(0,yo-0.12,zb-0.12,0.9)
    P_.append(box(-0.85,0.85,yo-0.06,yo+0.2,z1,z1+0.12,"stone_dk",detail=True,bev=0.02))
    return xf(P_,Rz(a))
def split_wedge(i,z_cuts):
    da=2*math.pi/NW; a=i*da-da/2; return [wedge(RI,RO,a,a+da,za,zb,"stone") for za,zb in z_cuts]
def ring_door():
    P_=ring(RI,RO,0,H,NW,skip=(0,))
    yo=-RO-0.12; w=1.1; r=w/2; zs=1.75
    P_+=slab_with_arch(-0.95,0.95,yo,-RI+0.1,0,H,0,w,0.0,zs,cls="stone_dk",bev=0.025,nvous=5)
    n=7
    for i in range(n):
        a0=math.pi*i/n; a1=math.pi*(i+1)/n; r2=r+0.22
        P_.append(prism_xz([(r*math.cos(a0),zs+r*math.sin(a0)),(r*math.cos(a1),zs+r*math.sin(a1)),(r2*math.cos(a1),zs+r2*math.sin(a1)),(r2*math.cos(a0),zs+r2*math.sin(a0))],yo-0.07,yo+0.02,"stone",detail=True))
    yd0,yd1=yo+0.3,yo+0.42
    for s in (-1,1):
        P_.append(box(min(0,s*r),max(0,s*r),yd0,yd1,0,zs,"paint",detail=True))
        P_.append(prism_xz([(0,zs)]+[(s*r*math.cos(math.pi/2*k/4),zs+r*math.sin(math.pi/2*k/4)) for k in range(5)],yd0,yd1,"paint",detail=True))
        for zz in (0.3,0.95,1.6): P_.append(box(min(0,s*(r-0.04)),max(0,s*(r-0.04)),yd0-0.02,yd0,zz,zz+0.07,"iron",detail=True))
    P_.append(box(-0.04,0.04,yd0-0.08,yd0,0.95,1.1,"gold",detail=True))
    P_.append(box(-1.1,1.1,yo-0.5,yo+0.3,0,0.15,"stone_dk",bev=0.02))
    P_.append(box(-0.85,0.85,yo-0.3,yo+0.3,0.15,0.28,"stone_dk",bev=0.02))
    P_+=lantern(-0.72,yo,1.9,1.0)+lantern(0.72,yo,1.9,1.0)
    P_+=ring(RO-0.1,RO+0.08,0,0.45,NW,cls="stone_dk",a0=math.pi/NW,skip=(0,NW-1))
    P_+=xf(vine(0,-RO,0.45,2.9,seed=4,spread=0.2),Rz(-42))
    return P_
def ring_window():
    P_=ring(RI,RO,0,H,NW,skip=(0,8))
    for i in (0,8): P_+=split_wedge(i,[(0,0.6),(2.6,H)])
    P_+=portal_window(0)+portal_window(180,flowers=False)
    return P_
def ring_slit():
    P_=ring(RI,RO,0,H,NW,skip=(0,4,12)); da=2*math.pi/NW; d=0.07/RO
    for i in (0,4,12):
        a=i*da
        P_+=[wedge(RI,RO,a-da/2,a+da/2,0,1.0,"stone"),wedge(RI,RO,a-da/2,a+da/2,2.0,H,"stone"),wedge(RI,RO,a-da/2,a-d,1.0,2.0,"stone",n=1),wedge(RI,RO,a+d,a+da/2,1.0,2.0,"stone",n=1)]
        for s in (-1,1): P_.append(wedge(RO-0.05,RO+0.04,a+min(s*d,s*(d+0.14)),a+max(s*d,s*(d+0.14)),1.0,2.0,"stone_dk",n=1,detail=True))
    P_+=banner(0,-RO-0.08,2.75,0.9,2.1,y_wall=-RO+0.12)+xf(vine(0,-RO,0.0,2.0,seed=6,spread=0.2),Rz(-42))
    P_+=ring(RO-0.1,RO+0.08,2.82,2.98,NW,cls="stone_dk",a0=math.pi/NW)
    return P_
def crown():
    da=2*math.pi/NW; P_=[]
    for i in range(NW):
        c=P([Vector((x,-r,z)) for x in (-0.12,0.12) for (r,z) in ((RO-0.1,0),(RO+0.08,0),(RO-0.1,0.5),(RO+0.32,0.5))],"stone_dk",detail=True)
        xf([c],Rz(math.degrees(i*da))); P_.append(c)
    P_+=ring(RO-0.45,RO+0.34,0.5,1.35,NW,a0=da/2)
    P_+=ring(RO+0.2,RO+0.42,1.28,1.42,NW,cls="stone_dk",a0=da/2)
    return P_
def roof():
    P_=cone_roof(2.75,2.4,0,5.2,seg=24,courses=7,flag="cloth",eave_h=0.35)
    for a in (0,180):
        D=[box(-0.36,0.36,-2.25,-1.0,0.75,1.5,"plaster"),
           prism_xz([(-0.48,1.48),(0.48,1.48),(0,1.98)],-2.38,-0.9,"roof"),
           box(-0.22,0.22,-2.27,-2.23,0.85,1.38,"glass",detail=True),
           box(-0.3,0.3,-2.3,-2.24,0.78,0.86,"wood_dk",detail=True),box(-0.3,0.3,-2.3,-2.24,1.36,1.44,"wood_dk",detail=True),
           box(-0.3,-0.22,-2.3,-2.24,0.78,1.44,"wood_dk",detail=True),box(0.22,0.3,-2.3,-2.24,0.78,1.44,"wood_dk",detail=True),
           box(-0.02,0.02,-2.29,-2.24,0.86,1.36,"wood_dk",detail=True)]
        P_+=xf(D,Rz(a))
    return P_
def floor():
    poly=[pol(RI+0.12,2*math.pi*i/NW+math.pi/NW) for i in range(NW)]
    return [prism_xy(poly,-0.16,0,"wood"),box(-0.11,0.11,-1.3,1.3,-0.36,-0.16,"wood_dk"),box(-1.3,1.3,-0.11,0.11,-0.36,-0.16,"wood_dk")]
MODS=[Module(B,"RingDoor","Stone",ring_door(),hide=("bottom","top"),note="ground ring, projecting door portal, lanterns",interior="centre"),
      Module(B,"RingSlit","Stone",ring_slit(),"bottom","Support",hide=("bottom","top"),note="middle ring, 3 arrow slits, banner",interior="centre"),
      Module(B,"RingWindow","Stone",ring_window(),"bottom","Support",hide=("bottom","top"),note="top ring, 2 arched windows, shutters, flower boxes",interior="centre"),
      Module(B,"Crown","Stone",crown(),"bottom","Support",hide=("bottom","top"),note="corbelled crown"),
      Module(B,"Roof","Roof",roof(),"bottom","Support",note="tall conical roof, 2 dormers, finial, pennant"),
      Module(B,"Floor","Wood",floor(),"perimeter","Support",uvscale=0.45,note="round floor on the ring below")]
def layout():
    Lo=Layout("Bld_Watchtower"); r0=Lo.add("S0_RingDoor",B,"RingDoor",T(0,0,0))
    r1=Lo.add("S1_RingSlit",B,"RingSlit",T(0,0,3)@Rz(0),[r0]); r2=Lo.add("S2_RingSlit",B,"RingSlit",T(0,0,6)@Rz(135),[r1])
    r3=Lo.add("S3_RingWindow",B,"RingWindow",T(0,0,9)@Rz(90),[r2])
    c=Lo.add("Crown",B,"Crown",T(0,0,12),[r3]); Lo.add("Roof",B,"Roof",T(0,0,13.42)@Rz(90),[c])
    for i,(z,on) in enumerate(((3,r0),(6,r1),(9,r2),(12,r3))): Lo.add(f"Floor_{i+1}",B,"Floor",T(0,0,z),[on])
    Lo.add("Annex","Props","Shed",T(-2.65,0.35,0)@Rz(-90))
    for i,(x,y,r) in enumerate([(1.6,-2.35,20),(2.15,-1.85,65)]): Lo.add(f"Barrel_{i}","Props","Barrel",T(x,y,0)@Rz(r))
    Lo.add("Crate_0","Props","Crate",T(-1.75,-2.35,0)@Rz(-15)); Lo.add("Crate_1","Props","CrateSmall",T(-1.7,-2.3,0.8)@Rz(20),["Crate_0"])
    return Lo
if __name__=="__main__":
    A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(A)>1 and A[1]=="layout": write_layout(layout())
    else: build_set(B,MODS,size=int(A[0]) if A and A[0] else 2048)
