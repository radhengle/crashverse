# Bld_Townhouse: two-storey townhouse - sandstone ground floor (door, shop sign, lantern, windows), jettied
# timber-frame + plaster upper floor (braces, shuttered windows, flower boxes), timber-framed gables with an oculus,
# steep red tile roof (stepped courses, bargeboards, ridge cap, dormer) and a stone chimney.
import sys,os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from hero_parts import *
B="House"; GL=4.0; GT=0.4; GH=2.8; UL=4.6; UT=0.3; UH=2.78; ZU=3.12; RW=4.9; RIDGE=2.6
def rect_window(xc,y_out,T_,w,z0,z1,shut=True,flowers=True,frame="wood_dk"):
    P_=[box(xc-w/2,xc+w/2,y_out+T_*0.5,y_out+T_*0.5+0.03,z0,z1,"glass",detail=True)]
    f=0.07
    P_+=[box(xc-w/2-f,xc-w/2,y_out-0.04,y_out+0.03,z0-f,z1+f,frame,detail=True),box(xc+w/2,xc+w/2+f,y_out-0.04,y_out+0.03,z0-f,z1+f,frame,detail=True),
         box(xc-w/2,xc+w/2,y_out-0.04,y_out+0.03,z1,z1+f,frame,detail=True),box(xc-w/2-0.06,xc+w/2+0.06,y_out-0.12,y_out+0.03,z0-f-0.04,z0,frame,detail=True),
         box(xc-0.025,xc+0.025,y_out+T_*0.5-0.03,y_out+T_*0.5,z0,z1,frame,detail=True),box(xc-w/2,xc+w/2,y_out+T_*0.5-0.03,y_out+T_*0.5,(z0+z1)/2-0.025,(z0+z1)/2+0.025,frame,detail=True)]
    if shut:
        h=z1-z0
        for s in (-1,1):
            hx=xc+s*(w/2+f); R=Matrix.Rotation(-s*18*D2R,3,'Z'); c=Vector((hx,y_out-0.05,0))+R@Vector((s*w*0.26,-0.03,0)); c.z=(z0+z1)/2
            P_.append(bxc(c,(w*0.5,0.04,h),"paint",R=R,detail=True))
            for zz in (0.18,h-0.18):
                cc=Vector((hx,y_out-0.05,0))+R@Vector((s*w*0.2,-0.056,0)); cc.z=z0+zz; P_.append(bxc(cc,(w*0.38,0.012,0.045),"iron",R=R,detail=True))
    if flowers:
        P_.append(box(xc-w/2-0.05,xc+w/2+0.05,y_out-0.34,y_out-0.1,z0-0.36,z0-0.13,"wood",detail=True))
        P_.append(P([Vector((xc+x,y_out+y,z0-0.13)) for x in (-w/2,w/2) for y in (-0.32,-0.12)]+[Vector((xc+x*0.85,y_out-0.22+y,z0+0.05)) for x in (-w/2,w/2) for y in (-0.08,0.08)],"leaf",detail=True))
    return P_
def ground(door):
    y0,y1=-GT/2,GT/2; P_=[]
    if door:
        dx=-0.75; w=1.0; r=w/2; zs=1.75
        P_+=slab_with_arch(-GL/2,0.35,y0,y1,0,GH,dx,w,0.0,zs,nvous=5)
        P_+=[box(1.15,GL/2,y0,y1,0,GH,"stone",bev=0.03),box(0.35,1.15,y0,y1,0,0.95,"stone"),box(0.35,1.15,y0,y1,1.95,GH,"stone")]
        P_+=rect_window(0.75,y0,GT,0.8,0.95,1.95,shut=True,flowers=True,frame="wood_dk")
        n=7
        for i in range(n):
            a0=math.pi*i/n; a1=math.pi*(i+1)/n; r2=r+0.2
            P_.append(prism_xz([(dx+r*math.cos(a0),zs+r*math.sin(a0)),(dx+r*math.cos(a1),zs+r*math.sin(a1)),(dx+r2*math.cos(a1),zs+r2*math.sin(a1)),(dx+r2*math.cos(a0),zs+r2*math.sin(a0))],y0-0.06,y0+0.02,"stone_dk",detail=True))
        for s in (-1,1): P_.append(box(min(dx+s*r,dx+s*(r+0.2)),max(dx+s*r,dx+s*(r+0.2)),y0-0.05,y0+0.02,0,zs,"stone_dk",detail=True))
        P_.append(box(dx-r,dx+r,-0.04,0.06,0,zs,"paint",detail=True))
        P_.append(prism_xz([(dx-r,zs)]+[(dx+r*math.cos(math.pi*k/8),zs+r*math.sin(math.pi*k/8)) for k in range(8,-1,-1)][::-1],-0.04,0.06,"paint",detail=True))
        for zz in (0.35,1.3): P_.append(box(dx-r+0.04,dx+r-0.04,-0.06,-0.04,zz,zz+0.07,"iron",detail=True))
        P_.append(sphere((dx+0.3,-0.09,1.0),0.04,"gold",seg=6,rings=3,detail=True))
        P_.append(box(dx-r-0.25,dx+r+0.25,y0-0.4,y0+0.02,0,0.13,"stone_dk",bev=0.02))
        # hanging shop sign on an iron bracket
        P_+=[box(-0.05,0.0,y0-0.85,y0+0.02,2.32,2.38,"iron",detail=True),box(-0.05,0.0,y0-0.08,y0+0.02,2.0,2.38,"iron",detail=True),
             box(-0.045,-0.005,y0-0.78,y0-0.18,1.72,2.22,"wood",detail=True),
             prism_xz([(-0.06,1.97+0.12),(0.01,1.97+0.12),(0.01,1.97-0.12),(-0.06,1.97-0.12)],y0-0.62,y0-0.34,"gold",detail=True)]
        xf(P_[-4:],T(0.25,0,0)@Rz(0))
        P_+=lantern(-1.55,y0,1.85)
    else:
        P_+=[box(-GL/2,-0.8,y0,y1,0,GH,"stone",bev=0.03),box(0.8,GL/2,y0,y1,0,GH,"stone",bev=0.03),box(-0.8,0.8,y0,y1,0,0.95,"stone"),box(-0.8,0.8,y0,y1,1.95,GH,"stone")]
        P_+=rect_window(0,y0,GT,1.0,0.95,1.95,shut=True,flowers=True)+vine(1.45,y0,0.4,2.7,seed=8,spread=0.25)
    P_.append(P([Vector((x,y,z)) for x in (-GL/2,GL/2) for (y,z) in ((y0-0.12,0),(y0-0.12,0.25),(y0,0.45),(y0,0))],"stone_dk",bev=0.02))
    return P_
def floor():
    P_=[box(-RW/2,RW/2,-RW/2,RW/2,0.16,0.32,"wood")]
    for x in (-2.0,-1.0,0.0,1.0,2.0): P_.append(box(x-0.08,x+0.08,-RW/2-0.14,RW/2+0.14,0,0.16,"wood_dk"))
    P_.append(box(-RW/2-0.05,RW/2+0.05,-RW/2-0.05,-RW/2+0.1,0.16,0.34,"wood_dk")); P_.append(box(-RW/2-0.05,RW/2+0.05,RW/2-0.1,RW/2+0.05,0.16,0.34,"wood_dk"))
    return P_
def upper(nwin):
    y0,y1=-UT/2,UT/2; L=UL; P_=[]; fy0,fy1=y0-0.04,y0+0.02
    xs=[-1.15,1.15] if nwin==2 else [0.0]; w=0.7; z0,z1=1.0,2.0
    cuts=sorted([x-w/2-0.07 for x in xs]+[x+w/2+0.07 for x in xs]); edges=[-L/2]+cuts+[L/2]
    for i in range(len(edges)-1):
        a,b=edges[i],edges[i+1]
        if i%2==0: P_.append(box(a,b,y0,y1,0,UH,"plaster"))
        else: P_+=[box(a,b,y0,y1,0,z0-0.07,"plaster"),box(a,b,y0,y1,z1+0.07,UH,"plaster")]
    for x in xs: P_+=rect_window(x,y0,UT,w,z0,z1,shut=True,flowers=not (nwin==2 and x<0))
    if nwin==2: P_+=balcony(xs[0],fy0,0.9,1.15,0.6,posts=5)
    P_+=[box(-L/2,L/2,fy0,fy1,0,0.16,"wood_dk"),box(-L/2,L/2,fy0,fy1,UH-0.16,UH,"wood_dk")]
    posts=[-L/2+0.08,L/2-0.08]+([-2.0+0.0+0.3, 0.0, 2.0-0.3] if nwin==2 else [-1.25,1.25])
    for x in posts: P_.append(box(x-0.08,x+0.08,fy0,fy1,0.16,UH-0.16,"wood_dk"))
    if nwin==2:
        for (xa,xb) in ((-0.55,-0.08),(0.08,0.55)):
            P_.append(seg_box((xa,fy0+0.03,0.2),(xb,fy0+0.03,UH-0.2),0.12,0.06,"wood_dk"))
        P_.append(box(-0.5,0.5,fy0,fy1,1.3,1.42,"wood_dk"))
    else:
        for (xa,xb) in ((-L/2+0.16,-1.33),(L/2-0.16,1.33)):
            P_.append(seg_box((xa,fy0+0.03,0.2),(xb,fy0+0.03,UH-0.2),0.12,0.06,"wood_dk")); P_.append(seg_box((xa,fy0+0.03,UH-0.2),(xb,fy0+0.03,0.2),0.12,0.06,"wood_dk"))
        P_.append(box(-L/2,-1.25,fy0,fy1,1.3,1.42,"wood_dk")); P_.append(box(1.25,L/2,fy0,fy1,1.3,1.42,"wood_dk"))
    return P_
def gable():
    y0,y1=-UT/2,UT/2; W=RW; fy0,fy1=y0-0.04,y0+0.02
    P_=[prism_xz([(-W/2,0),(W/2,0),(0,RIDGE)],y0,y1,"plaster")]
    s=RIDGE/(W/2)
    P_+=[box(-W/2+0.1,W/2-0.1,fy0,fy1,0,0.16,"wood_dk"),seg_box((-W/2+0.05,fy0+0.03,0.08),(0,fy0+0.03,RIDGE-0.08),0.16,0.06,"wood_dk"),
         seg_box((W/2-0.05,fy0+0.03,0.08),(0,fy0+0.03,RIDGE-0.08),0.16,0.06,"wood_dk"),box(-0.08,0.08,fy0,fy1,0.16,0.76,"wood_dk"),
         box(-(W/2-0.7/s*1.0)+0.25,(W/2-0.7/s*1.0)-0.25,fy0,fy1,0.7,0.82,"wood_dk")]
    zc=1.45; r=0.3; n=8; poly=[(r*math.cos(2*math.pi*i/n+math.pi/8),zc+r*math.sin(2*math.pi*i/n+math.pi/8)) for i in range(n)]
    P_.append(prism_xz(poly,y0-0.02,y0+0.03,"glass",detail=True))
    for i in range(n):
        a0=2*math.pi*i/n+math.pi/8; a1=a0+2*math.pi/n; r2=r+0.09
        P_.append(prism_xz([(r*math.cos(a0),zc+r*math.sin(a0)),(r*math.cos(a1),zc+r*math.sin(a1)),(r2*math.cos(a1),zc+r2*math.sin(a1)),(r2*math.cos(a0),zc+r2*math.sin(a0))],y0-0.06,y0+0.02,"wood_dk",detail=True))
    return P_
def roof_side():
    """one roof slope (+x side), ridge along y; courses of tiles = bevelled boxes tilted a bit steeper than the roof plane"""
    th=math.atan2(RIDGE,RW/2); ov=0.45; Ly=UL+0.6+0.4; P_=[]
    d=Vector((math.cos(th),0,-math.sin(th))); n=Vector((math.sin(th),0,math.cos(th)))
    top=Vector((0,0,RIDGE+0.1)); S=(RW/2+ov)/math.cos(th)
    P_.append(seg_box(top+n*0.0,top+d*S,Ly,0.08,"wood",up=n))
    nc=11; seg=S/nc
    for k in range(nc):
        c=top+d*(seg*(k+0.5))+n*0.07; Rr=Matrix((d,Vector((0,1,0)),n)).transposed()@Matrix.Rotation(-5*D2R,3,'Y')
        P_.append(bxc(c,(seg+0.06,Ly,0.07),"roof",R=Rr,bev=0.018))
    for sy in (-1,1):
        P_.append(seg_box(top+Vector((0,sy*Ly/2,0))-n*0.0,top+d*S+Vector((0,sy*Ly/2,0))-n*0.0,0.28,0.06,"wood_dk",up=Vector((0,1,0))))
    q=[Vector((0.0+0.16*math.cos(a),y,RIDGE+0.14+0.16*math.sin(a))) for a in [math.pi*i/8 for i in range(0,5)] for y in (-Ly/2-0.04,Ly/2+0.04)]+[Vector((0,y,RIDGE+0.04)) for y in (-Ly/2-0.04,Ly/2+0.04)]
    P_.append(P(q,"roof"))
    # dormer
    zc=1.0; xc=RW/2-zc/math.tan(th)
    D=[box(xc-0.2,xc+0.95,-0.42,0.42,zc-0.05,zc+0.75,"plaster"),prism_xz([(xc-0.25,zc+0.73),(xc+1.05,zc+0.73),(xc-0.25,zc+1.2)],-0.52,0.52,"roof") ]
    D=[box(xc-0.4,xc+0.55,-0.42,0.42,zc-0.2,zc+0.62,"plaster"),P([Vector((x,y,z)) for (x,z) in ((xc-0.45,zc+0.6),(xc+0.62,zc+0.6),(xc-0.45,zc+1.05)) for y in (-0.53,0.53)]+[Vector((xc+0.62,y,zc+0.6)) for y in (-0.05,0.05)],"roof")]
    D=[box(xc-0.4,xc+0.55,-0.42,0.42,zc-0.2,zc+0.6,"plaster"),P([Vector((x,y,z)) for x in (xc-0.45,xc+0.62) for (y,z) in ((-0.53,zc+0.58),(0.53,zc+0.58),(0,zc+1.0))],"roof"),
       box(xc+0.55,xc+0.58,-0.25,0.25,zc-0.05,zc+0.45,"glass",detail=True),box(xc+0.56,xc+0.62,-0.31,0.31,zc-0.11,zc-0.05,"wood_dk",detail=True),
       box(xc+0.56,xc+0.62,-0.31,0.31,zc+0.45,zc+0.51,"wood_dk",detail=True),box(xc+0.56,xc+0.62,-0.025,0.025,zc-0.05,zc+0.45,"wood_dk",detail=True)]
    xf(D,T(0,0,0)); P_+=D
    return P_
def chimney():
    P_=[box(-0.4,0.4,-0.35,0.35,0,4.8,"stone",bev=0.03),box(-0.48,0.48,-0.43,0.43,4.8,4.98,"stone_dk",bev=0.02),box(-0.44,0.44,-0.39,0.39,3.9,4.02,"stone_dk",bev=0.02)]
    for x in (-0.17,0.17): P_+=[cyl_z(x,0,4.98,5.3,0.11,8,"roof",detail=True),cyl_z(x,0,5.25,5.34,0.135,8,"roof",detail=True)]
    return P_
MODS=[Module(B,"GroundDoor","Stone",ground(True),hide=("bottom","top"),note="ground floor front: arched blue door, window, shop sign, lantern",interior="y+"),
      Module(B,"GroundWin","Stone",ground(False),hide=("bottom","top"),note="ground floor side/back: shuttered window + flower box",interior="y+"),
      Module(B,"Floor","Wood",floor(),"perimeter","Support",uvscale=0.5,note="jettied first floor deck, joist ends"),
      Module(B,"Upper2","Wood",upper(2),"bottom","Support",hide=("bottom","top"),note="timber frame + plaster, 2 windows, timber balcony",interior="y+"),
      Module(B,"Upper1","Wood",upper(1),"bottom","Support",hide=("bottom","top"),note="timber frame + plaster, 1 window, braces",interior="y+"),
      Module(B,"Gable","Wood",gable(),"bottom","Support",hide=("bottom",),note="timber-framed gable, oculus"),
      Module(B,"Roof","Roof",roof_side(),"bottom","Support",note="one roof slope with dormer (mirror = rotate 180)"),
      Module(B,"Chimney","Stone",chimney(),"bottom","Support",note="stone chimney stack, clay pots")]
def layout():
    Lo=Layout("Bld_Townhouse"); o=GL/2; g={}
    pin=[("F",(-0.2,-o),0),("R",(o,-0.2),90),("B",(0.2,o),180),("L",(-o,0.2),-90)]
    for k,(x,y),r in pin: g[k]=Lo.add(f"G_{k}",B,"GroundDoor" if k=="F" else "GroundWin",T(x,y,0)@Rz(r))
    f=Lo.add("Floor1",B,"Floor",T(0,0,GH),[g[k] for k in "FRBL"]); u=UL/2-0.0
    uo=RW/2-UT/2; pin=[("F",(-0.15,-uo),0),("R",(uo,-0.15),90),("B",(0.15,uo),180),("L",(-uo,0.15),-90)]; U={}
    for k,(x,y),r in pin: U[k]=Lo.add(f"U_{k}",B,"Upper2" if k in "FB" else "Upper1",T(x,y,ZU)@Rz(r),[f])
    zg=ZU+UH
    gF=Lo.add("Gable_F",B,"Gable",T(0,-uo,zg),[U["F"],U["L"],U["R"]]); gB=Lo.add("Gable_B",B,"Gable",T(0,uo,zg)@Rz(180),[U["B"],U["L"],U["R"]])
    Lo.add("Roof_E",B,"Roof",T(0,0,zg),[gF,gB,U["R"]]); Lo.add("Roof_W",B,"Roof",T(0,0,zg)@Rz(180),[gF,gB,U["L"]])
    Lo.add("Chimney",B,"Chimney",T(-1.25,1.45,ZU),[f])
    Lo.add("Annex_R","Props","Shed",T(GL/2+0.2+0.9,0.6,0)@Rz(90))
    for i,(x,y,r) in enumerate([(1.55,-2.75,15),(2.05,-2.5,60)]): Lo.add(f"Barrel_{i}","Props","Barrel",T(x,y,0)@Rz(r))
    Lo.add("Crate_0","Props","Crate",T(-2.5,-2.75,0)@Rz(8)); Lo.add("Crate_1","Props","CrateSmall",T(-2.45,-2.7,0.8)@Rz(-25),["Crate_0"])
    return Lo
if __name__=="__main__":
    A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(A)>1 and A[1]=="layout": write_layout(layout())
    else: build_set(B,MODS,size=int(A[0]) if A and A[0] else 2048)
