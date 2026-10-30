# Bld_Gatehouse: arched gate passage with a half-raised portcullis, coat of arms and banners, two square flanking
# towers (quoins, slits, windows, machicolated crowns, red pyramid roofs + flags), curtain walls with battlements,
# ending in two round Keep turrets.
import sys,os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from hero_parts import *
B="Gate"; GW=5.0; GD=3.2; GH=6.0
def crown_strip(L,y0=-0.3,y1=0.3,nm=None):
    P_=[box(-L/2,L/2,y0,y1,0,0.4,"stone"),box(-L/2,L/2,y0-0.36,y0+0.06,0.4,1.0,"stone",bev=0.03),box(-L/2,L/2,y1-0.16,y1,0.4,0.85,"stone")]
    nc=int(L/0.7); 
    for i in range(nc):
        x=-L/2+0.35+i*(L-0.7)/(nc-1)
        P_.append(P([Vector((xx,y,z)) for xx in (x-0.12,x+0.12) for (y,z) in ((y0,0.0),(y0-0.12,0.0),(y0,0.4),(y0-0.36,0.4))],"stone_dk",detail=True))
    nm=nm or int((L+0.4)/1.0)
    for i in range(nm):
        x=-L/2+0.3+i*(L-0.6)/(nm-1)
        P_.append(box(x-0.3,x+0.3,y0-0.36,y0+0.06,1.0,1.55,"stone",bev=0.03)); P_.append(box(x-0.34,x+0.34,y0-0.4,y0+0.1,1.55,1.64,"stone_dk",detail=True,bev=0.02))
    return P_
def arms(xc,y,zc,s=1.0):
    sh=[(-0.42,0.45),(0.42,0.45),(0.42,-0.05),(0,-0.55),(-0.42,-0.05)]
    P_=[prism_xz([(xc+x*s*1.12,zc+z*s*1.12) for x,z in sh],y-0.04,y+0.02,"gold",detail=True),prism_xz([(xc+x*s,zc+z*s) for x,z in sh],y-0.08,y-0.04,"paint",detail=True)]
    P_.append(prism_xz([(xc,zc+0.3*s),(xc+0.2*s,zc),(xc,zc-0.3*s),(xc-0.2*s,zc)],y-0.11,y-0.08,"gold",detail=True))
    return P_
def gate_arch():
    r=1.25; zs=2.6; y0,y1=-GD/2,GD/2
    P_=slab_with_arch(-GW/2,GW/2,y0,y1,0,GH,0,2*r,0.0,zs,nvous=7,fill_top=4.4)
    for yy,sg in ((y0,-1),(y1,1)):
        n=9
        for i in range(n):
            a0=math.pi*i/n; a1=math.pi*(i+1)/n; r2=r+0.3
            ya,yb=(yy-0.09,yy+0.02) if sg<0 else (yy-0.02,yy+0.09)
            P_.append(prism_xz([(r*math.cos(a0),zs+r*math.sin(a0)),(r*math.cos(a1),zs+r*math.sin(a1)),(r2*math.cos(a1),zs+r2*math.sin(a1)),(r2*math.cos(a0),zs+r2*math.sin(a0))],ya,yb,"stone_dk",detail=True))
        ya,yb=(yy-0.07,yy+0.02) if sg<0 else (yy-0.02,yy+0.07)
        for s in (-1,1): P_.append(box(min(s*r,s*(r+0.3)),max(s*r,s*(r+0.3)),ya,yb,0,zs,"stone_dk",detail=True))
    for s in (-1,1): P_.append(box(min(s*(r-0.03),s*(r+0.01)),max(s*(r-0.03),s*(r+0.01)),-1.12,-0.88,0,zs,"iron",detail=True))   # portcullis grooves
    P_+=arms(0,y0,4.95,1.0)
    for s in (-1,1): P_+=banner(s*1.75,y0-0.12,5.55,0.7,2.2,y_wall=y0)
    P_+=lantern(-1.75,y0,2.3)+lantern(1.75,y0,2.3)
    P_.append(box(-GW/2,GW/2,y0-0.08,y0+0.02,5.72,5.86,"stone_dk",bev=0.02))
    P_.append(box(-GW/2,GW/2,y1-0.02,y1+0.08,5.72,5.86,"stone_dk",bev=0.02))
    return P_
def portcullis(W=2.5,Hh=3.7):
    P_=[]; xs=[-W/2+0.18+i*(W-0.36)/5 for i in range(6)]
    for x in xs:
        P_.append(box(x-0.08,x+0.08,-0.08,0.08,0.25,Hh,"wood_dk"))
        P_.append(pyramid(0.16,0.0,0.0,0.25,"iron",detail=True)); xf([P_[-1]],T(x,0,0))
    for z in (0.55,1.25,1.95,2.65,3.35):
        P_.append(box(-W/2+0.05,W/2-0.05,-0.13,-0.0,z,z+0.16,"wood_dk"))
        for x in xs: P_.append(box(x-0.05,x+0.05,-0.16,-0.13,z+0.03,z+0.13,"iron",detail=True))
    return P_
def tower(window=False):
    w=3.0; t=0.5; h=3.0; P_=[]
    P_+= [box(-w/2,w/2,w/2-t,w/2,0,h,"stone",bev=0.03),box(-w/2,-w/2+t,-w/2+t,w/2-t,0,h,"stone",bev=0.03),box(w/2-t,w/2,-w/2+t,w/2-t,0,h,"stone",bev=0.03)]
    if window:
        P_+=slab_with_arch(-w/2,w/2,-w/2,-w/2+t,0,h,0,0.6,1.0,1.85,nvous=5)
        P_+=window_arch(0,-w/2,t,0.6,1.0,1.85)
    else:
        P_+=[box(-w/2,-0.07,-w/2,-w/2+t,0,h,"stone",bev=0.03),box(0.07,w/2,-w/2,-w/2+t,0,h,"stone",bev=0.03),box(-0.07,0.07,-w/2,-w/2+t,0,0.9,"stone"),box(-0.07,0.07,-w/2,-w/2+t,2.0,h,"stone")]
        for s in (-1,1): P_.append(box(min(s*0.07,s*0.25),max(s*0.07,s*0.25),-w/2-0.04,-w/2+0.02,0.9,2.0,"stone_dk",detail=True))
        P_.append(box(-0.25,0.25,-w/2-0.05,-w/2+0.02,2.0,2.18,"stone_dk",detail=True))
    for cx in (-1,1):    # quoins: alternating long/short dressed corner stones
        for cy in (-1,1):
            for k in range(5):
                z=k*0.6; lx,ly=(0.55,0.3) if k%2==0 else (0.3,0.55)
                x0,x1=sorted((cx*w/2+cx*0.04,cx*w/2-cx*lx)); y0,y1=sorted((cy*w/2+cy*0.04,cy*w/2-cy*ly))
                P_.append(box(x0,x1,y0,y1,z+0.02,z+0.58,"stone_dk",detail=True))
    P_.append(box(-w/2-0.06,w/2+0.06,-w/2-0.06,-w/2+0.1,2.84,2.98,"stone_dk",bev=0.02))
    return P_
def tower_crown():
    w=3.0; o=0.3; P_=[]
    for side in range(4):
        R_=Rz(90*side)
        for i in range(4):
            x=-1.05+i*0.7
            c=P([Vector((xx,y,z)) for xx in (x-0.12,x+0.12) for (y,z) in ((-w/2,0),(-w/2-0.12,0),(-w/2,0.45),(-w/2-o,0.45))],"stone_dk",detail=True); P_+=xf([c],R_)
        P_+=xf([box(-w/2-o,w/2+o-0.3,-w/2-o,-w/2-o+0.36,0.45,1.2,"stone",bev=0.03)],R_)
        for i in range(3):
            x=-w/2-o+0.3+i*(w+2*o-0.6)/2
            if i==2: continue    # corner merlon comes from the next side (pinwheel)
            P_+=xf([box(x-0.27,x+0.27,-w/2-o,-w/2-o+0.36,1.2,1.75,"stone",bev=0.03)],R_)
    P_.append(box(-w/2,w/2,-w/2,w/2,0,0.45,"stone"))
    return P_
def tower_roof():
    P_=[pyramid(3.0,2.7,0,0.35,"roof")]; H=4.4; n=6
    for k in range(n):
        za=0.35+(H-0.35)*k/n; zb=0.35+(H-0.35)*(k+1)/n; wa=2.7*(1-(za-0.35)/(H-0.35)); wb=2.7*(1-(zb-0.35)/(H-0.35))
        q=pyramid(wa+(0.08 if k else 0),wb if k<n-1 else 0.0,0,zb-za+(0.02 if k<n-1 else 0),"roof"); xf([q],T(0,0,za)); P_.append(q)
    P_+=hip_caps(2.7,0.35,H)+cone_dormer(1.4,0.55,0.7)
    P_.append(cyl_z(0,0,H-0.2,H+0.2,0.05,6,"iron",detail=True)); P_.append(sphere((0,0,H+0.25),0.1,"gold",detail=True))
    P_.append(cyl_z(0,0,H+0.3,H+1.4,0.025,6,"iron",detail=True))
    for i in range(3):
        x0=0.02+i*0.3; ang=(10 if i%2 else -10)*D2R
        P_.append(bxc((x0+0.15,0.02*(i%2),H+1.12-0.03*i),(0.31,0.025,0.5-0.04*i),"cloth",R=Matrix.Rotation(ang,3,'Z'),detail=True))
    return P_
def curtain():
    L=4.0; y0,y1=-0.7,0.7; h=4.5
    P_=[box(-L/2,L/2,y0,y1,0,h,"stone",bev=0.03)]
    P_.append(P([Vector((x,y,z)) for x in (-L/2,L/2) for (y,z) in ((y0-0.25,0),(y0-0.25,0.35),(y0,0.8),(y0,0))],"stone_dk",bev=0.02))
    P_.append(box(-L/2,L/2,y0,y0+0.45,h,h+0.85,"stone",bev=0.03)); P_.append(box(-L/2,L/2,y1-0.25,y1,h,h+0.6,"stone"))
    for i in range(4):
        x=-L/2+0.5+i*1.0
        P_.append(box(x-0.28,x+0.28,y0,y0+0.45,h+0.85,h+1.4,"stone",bev=0.03)); P_.append(box(x-0.32,x+0.32,y0-0.04,y0+0.49,h+1.4,h+1.48,"stone_dk",detail=True,bev=0.02))
    P_.append(box(-L/2,L/2,y0-0.07,y0+0.05,h-0.25,h-0.1,"stone_dk",bev=0.02))
    P_+=lantern(0.0,y0,2.4)+vine(-1.3,y0,0.6,4.1,seed=7,spread=0.35)+beam_ends((-1.5,-0.5,0.5,1.5),y0,h-0.62)
    return P_
MODS=[Module(B,"Arch","Stone",gate_arch(),hide=("bottom",),note="gate passage, coat of arms, banners, lanterns, portcullis grooves"),
      Module(B,"Portcullis","Steel",portcullis(),"top","Support",note="iron-shod oak portcullis, hangs from the arch"),
      Module(B,"Crown","Stone",crown_strip(GW),"bottom","Support",hide=("bottom",),note="machicolated battlement, 5 m"),
      Module(B,"Tower","Stone",tower(False),"bottom","Support",hide=("bottom","top"),note="square flanking tower storey, quoins, slit",interior="centre"),
      Module(B,"TowerWindow","Stone",tower(True),"bottom","Support",hide=("bottom","top"),note="square tower storey, arched window + shutters",interior="centre"),
      Module(B,"TowerCrown","Stone",tower_crown(),"bottom","Support",hide=("bottom",),note="corbelled square crown, merlons"),
      Module(B,"TowerRoof","Roof",tower_roof(),"bottom","Support",note="stepped red pyramid roof, finial, flag"),
      Module(B,"Wall","Stone",curtain(),hide=("bottom",),note="curtain wall 4 m, battered plinth, battlements, lantern",interior="y+")]
def layout():
    Lo=Layout("Bld_Gatehouse"); a=Lo.add("Arch",B,"Arch",T(0,0,0))
    Lo.add("Portcullis",B,"Portcullis",T(0,-1.0,1.2),[a])
    for k,y,r in (("F",-GD/2+0.3,0),("B",GD/2-0.3,180)): Lo.add(f"Crown_{k}",B,"Crown",T(0,y,GH)@Rz(r),[a])
    for s,x in (("L",-4.0),("R",4.0)):
        t0=Lo.add(f"Tower{s}_S0",B,"Tower",T(x,0,0)); t1=Lo.add(f"Tower{s}_S1",B,"Tower",T(x,0,3)@Rz(90 if s=="R" else -90),[t0])
        top=t1
        if s=="L": top=Lo.add(f"Tower{s}_S2",B,"Tower",T(x,0,6)@Rz(180),[t1])     # left tower one storey taller
        zt=9 if s=="L" else 6
        t2=Lo.add(f"Tower{s}_S{zt//3}",B,"TowerWindow",T(x,0,zt),[top]); c=Lo.add(f"Tower{s}_Crown",B,"TowerCrown",T(x,0,zt+3),[t2])
        Lo.add(f"Tower{s}_Roof",B,"TowerRoof",T(x,0,zt+3.45)@Rz(0 if s=="R" else 180),[c])
        sg=-1 if s=="L" else 1; prev=t0
        for i in range(2): prev=Lo.add(f"Wall{s}{i}",B,"Wall",T(sg*(7.5+4*i),0,0))
        ex=sg*(14.4); e0=Lo.add(f"End{s}_S0","Keep","Turret",T(ex,0,0)@Rz(math.degrees(math.atan2(sg,0.0001))))
        e1=Lo.add(f"End{s}_S1","Keep","Turret",T(ex,0,3)@Rz(math.degrees(math.atan2(sg,1.0))),[e0])
        ec=Lo.add(f"End{s}_Crown","Keep","TurretCrown",T(ex,0,6),[e1]); Lo.add(f"End{s}_Roof","Keep","TurretRoof",T(ex,0,7.3),[ec])
    for i,(x,y,r) in enumerate([(-2.0,-2.4,10),(-2.55,-2.15,50)]): Lo.add(f"Barrel_{i}","Props","Barrel",T(x,y,0)@Rz(r))
    Lo.add("Crate_0","Props","Crate",T(2.3,-2.5,0)@Rz(-12))
    Lo.add("Annex_R","Props","Shed",T(9.5,-1.6,0))
    return Lo
if __name__=="__main__":
    A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(A)>1 and A[1]=="layout": write_layout(layout())
    else: build_set(B,MODS,size=int(A[0]) if A and A[0] else 2048)
