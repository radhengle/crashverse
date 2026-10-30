# Kit_Props: barrel, crate, small crate (original designs) - shared by all hero buildings.
import sys,os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from hero_parts import *
B="Props"
def barrel(h=0.92,r0=0.28,r1=0.34,seg=12):
    prof=[(0,r0),(0.18,r0+0.04),(0.5,r1),(0.82,r0+0.04),(1.0,r0)]
    pts=[Vector((r*math.cos(2*math.pi*i/seg+math.pi/seg),r*math.sin(2*math.pi*i/seg+math.pi/seg),t*h)) for t,r in prof for i in range(seg)]
    P_=[P(pts,"wood")]
    for zc in (0.13,0.87):
        rr=r0+0.04+0.012; P_+=ring(rr-0.03,rr,zc*h-0.035,zc*h+0.035,6,cls="iron",n=2)
    P_+=ring(r1-0.03,r1+0.012,0.46*h,0.54*h,6,cls="iron",n=2)
    P_.append(cyl_z(0,0,h-0.01,h+0.012,r0-0.02,12,"wood_dk",detail=True))
    return P_
def crate(s=0.8):
    h=s/2; b=0.07; P_=[box(-h+0.02,h-0.02,-h+0.02,h-0.02,0.02,s-0.02,"wood")]
    for x in (-1,1):
        for y in (-1,1): P_.append(box(x*h-b if x>0 else -h,x*h if x>0 else -h+b,y*h-b if y>0 else -h,y*h if y>0 else -h+b,0,s,"wood_dk",detail=True))
    for z0 in (0,s-b):
        for y in (-1,1): P_.append(box(-h+b,h-b,(y*h-b) if y>0 else -h,y*h if y>0 else -h+b,z0,z0+b,"wood_dk",detail=True))
        for x in (-1,1): P_.append(box((x*h-b) if x>0 else -h,x*h if x>0 else -h+b,-h+b,h-b,z0,z0+b,"wood_dk",detail=True))
    for ax in range(4):   # diagonal braces on the four sides
        R_=Rz(90*ax); br=seg_box((-h+b,-h-0.005,b),(h-b,-h-0.005,s-b),0.08,0.035,"wood_dk",up=Vector((0,1,0)),detail=True); xf([br],R_); P_.append(br)
    return P_
def shed():
    """lean-to stone annex (2.4 x 1.8 m), back (+Y) against a host wall: plank door, shuttered window, mono-pitch tile roof,
    chimney with pot, lantern, ivy"""
    P_=[]; y0,y1=-0.9,-0.62; H0,H1=2.0,2.6
    P_+=[box(-1.2,-0.7,y0,y1,0,H0,"stone",bev=0.03),box(-0.7,0.1,y0,y1,1.62,H0,"stone"),box(0.1,0.5,y0,y1,0,H0,"stone",bev=0.03),
         box(0.5,0.95,y0,y1,0,0.9,"stone"),box(0.5,0.95,y0,y1,1.5,H0,"stone"),box(0.95,1.2,y0,y1,0,H0,"stone",bev=0.03)]
    for xa,xb in ((-1.2,-0.92),(0.92,1.2)): P_.append(P([Vector((x,y,z)) for x in (xa,xb) for (y,z) in ((y1,0),(0.9,0),(y1,H0+0.02),(0.9,H1))],"stone"))
    P_.append(box(-0.92,0.92,0.62,0.9,0,H1,"stone"))
    for x0,x1 in ((-0.78,-0.7),(0.1,0.18)): P_.append(box(x0,x1,y0-0.05,y0+0.02,0,1.62,"stone_dk",detail=True,bev=0.012))
    P_.append(box(-0.8,0.2,y0-0.07,y0+0.02,1.62,1.76,"stone_dk",detail=True,bev=0.015))
    P_.append(box(-0.7,0.1,-0.8,-0.72,0,1.62,"wood",detail=True))
    for zz in (0.3,1.25): P_.append(box(-0.66,0.06,-0.83,-0.8,zz,zz+0.07,"iron",detail=True))
    P_.append(sphere((-0.04,-0.85,0.85),0.035,"gold",seg=6,rings=3,detail=True))
    P_.append(box(0.5,0.95,-0.78,-0.75,0.9,1.5,"glass",detail=True))
    P_+=[box(0.46,0.99,y0-0.1,y0+0.02,0.84,0.9,"stone_dk",detail=True,bev=0.012),box(0.46,0.99,y0-0.04,y0+0.02,1.5,1.58,"wood_dk",detail=True)]
    for s in (-1,1):
        hx=0.725+s*0.27; Rr=Matrix.Rotation(-s*20*D2R,3,'Z'); c=Vector((hx,y0-0.04,0))+Rr@Vector((s*0.12,-0.02,0)); c.z=1.2
        P_.append(bxc(c,(0.24,0.035,0.58),"paint",R=Rr,detail=True))
    th=math.atan2(H1-H0,1.9); n=Vector((0,-math.sin(th),math.cos(th))); d=Vector((0,-math.cos(th),-math.sin(th)))
    top=Vector((0,0.95,H1+0.02)); L=2.25/math.cos(th)
    P_.append(seg_box(top,top+d*L,2.7,0.07,"wood",up=n))
    nc=6
    for k in range(nc):
        c=top+d*(L*(k+0.5)/nc)+n*0.06; Rr=Matrix((Vector((1,0,0)),d,n)).transposed()@Matrix.Rotation(5*D2R,3,'X')
        P_.append(bxc(c,(2.75,L/nc+0.05,0.06),"roof",R=Rr,bev=0.015))
    P_.append(seg_box(top+Vector((0,0,0.08)),top+Vector((0,-0.2,0.08)),2.8,0.08,"roof",up=n))
    for sx in (-1,1): P_.append(seg_box(top+Vector((sx*1.36,0,0)),top+d*L+Vector((sx*1.36,0,0)),0.06,0.18,"wood_dk",up=Vector((1,0,0)),detail=True))
    P_+=[box(0.45,0.85,0.25,0.62,1.9,3.5,"stone",bev=0.025),box(0.41,0.89,0.21,0.66,3.5,3.62,"stone_dk",detail=True,bev=0.015),
         cyl_z(0.65,0.43,3.62,3.88,0.09,8,"roof",detail=True)]
    P_+=lantern(-0.95,y0,1.55,0.85)+vine(1.08,y0,0.1,1.9,seed=3,spread=0.15)
    P_.append(box(-0.85,0.25,y0-0.35,y0+0.02,0,0.1,"stone_dk",bev=0.015))
    return P_
MODS=[Module(B,"Barrel","Wood",barrel(),note="oak barrel, iron hoops"),
      Module(B,"Crate","Wood",crate(0.8),note="0.8 m crate"),
      Module(B,"CrateSmall","Wood",crate(0.55),"bottom","Support",note="0.55 m crate (stacks)"),
      Module(B,"Shed","Stone",shed(),hide=("bottom",),note="lean-to stone annex with tile roof, chimney, door, window, ivy (back +Y on a host wall)",interior="centre")]
if __name__=="__main__":
    A=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    build_set(B,MODS,size=int(A[0]) if A and A[0] else 2048)
