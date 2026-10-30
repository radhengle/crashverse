"""Reusable convex-part assemblies for the hero buildings (all original designs). Local frame: x along the wall,
outer face towards -Y, z up from the module base."""
import math
from mathutils import Vector,Matrix
from hero_lib import *
D2R=math.pi/180
def window_arch(xc,y_out,T,w,z_sill,zs,cls_glass="glass",shutters="paint",sill=True,archivolt=True,out=0.06):
    """arched window detail set (the wall around the opening is built by the caller): glass, archivolt, sill, open shutters"""
    r=w/2; P_=[]
    P_.append(box(xc-r,xc+r,y_out+T*0.5,y_out+T*0.5+0.04,z_sill,zs+r,cls_glass,detail=True))
    if archivolt:
        n=7
        for i in range(n):
            a0=math.pi*i/n; a1=math.pi*(i+1)/n; r2=r+0.2
            poly=[(xc+r*math.cos(a0),zs+r*math.sin(a0)),(xc+r*math.cos(a1),zs+r*math.sin(a1)),(xc+r2*math.cos(a1),zs+r2*math.sin(a1)),(xc+r2*math.cos(a0),zs+r2*math.sin(a0))]
            P_.append(prism_xz(poly,y_out-out,y_out+0.02,"stone_dk",detail=True))
        for s in (-1,1):   # jamb stones
            P_.append(box(xc+s*r-(0.2 if s<0 else 0),xc+s*r+(0.2 if s>0 else 0),y_out-out*0.6,y_out+0.02,z_sill+0.05,zs,"stone_dk",detail=True,bev=0.015))
    if sill: P_.append(box(xc-r-0.15,xc+r+0.15,y_out-0.13,y_out+0.1,z_sill-0.1,z_sill,"stone_dk",detail=True,bev=0.02))
    if shutters:
        h=zs-z_sill-0.05
        for s in (-1,1):
            hx=xc+s*(r+0.2); R=Matrix.Rotation(-s*14*D2R,3,'Z')
            c=Vector((hx,y_out-0.04,0))+R@Vector((s*r*0.52,-0.03,0)); c.z=z_sill+0.03+h/2
            P_.append(bxc(c,(r*1.0,0.045,h),shutters,R=R,detail=True))
            for zz in (0.22,h-0.22):   # hinge straps
                cc=Vector((hx,y_out-0.04,0))+R@Vector((s*r*0.42,-0.058,0)); cc.z=z_sill+0.03+zz
                P_.append(bxc(cc,(r*0.8,0.012,0.05),"iron",R=R,detail=True))
    return P_
def slab_with_arch(x0,x1,y0,y1,z0,z1,xc,w,zb,zs,cls="stone",bev=0.03,nvous=5,fill_top=None):
    """wall slab x0..x1 with an arched opening (width w, bottom zb, springing zs) - convex parts only"""
    r=w/2; P_=[]
    P_.append(box(x0,xc-r,y0,y1,z0,z1,cls,bev=bev)); P_.append(box(xc+r,x1,y0,y1,z0,z1,cls,bev=bev))
    if zb>z0+1e-3: P_.append(box(xc-r,xc+r,y0,y1,z0,zb,cls))
    P_+=arch_voussoirs(xc,zs,r,w,z1 if fill_top is None else fill_top,y0,y1,nvous,cls,bev=0.0)
    if fill_top is not None and fill_top<z1-1e-3: P_.append(box(xc-r,xc+r,y0,y1,fill_top,z1,cls))
    return P_
def lantern(x,y_wall,z,s=1.0):
    """wall lantern: iron bracket, glass body, iron cap"""
    P_=[box(x-0.03*s,x+0.03*s,y_wall-0.32*s,y_wall+0.02,z+0.3*s,z+0.36*s,"iron",detail=True),
        box(x-0.07*s,x+0.07*s,y_wall-0.03,y_wall+0.02,z+0.18*s,z+0.42*s,"iron",detail=True),
        box(x-0.1*s,x+0.1*s,y_wall-0.4*s,y_wall-0.2*s,z,z+0.24*s,"lamp",detail=True),
        pyramid(0.26*s,0.04*s,0,0.14*s,"iron",detail=True),
        box(x-0.12*s,x+0.12*s,y_wall-0.42*s,y_wall-0.18*s,z-0.04*s,z,"iron",detail=True)]
    xf([P_[3]],T(x,y_wall-0.3*s,z+0.24*s)); return P_
def banner(xc,y,ztop,w,h,cls="cloth",trim="gold",emblem=True,pole=True,folds=3,y_wall=None):
    """hanging banner with pole + gold finials, fold strips, swallowtail and a lozenge emblem"""
    P_=[]
    if pole:
        P_.append(cyl_x(xc-w/2-0.12,xc+w/2+0.12,y,ztop+0.05,0.035,8,"wood_dk",detail=True))
        for s in (-1,1): P_.append(sphere((xc+s*(w/2+0.16),y,ztop+0.05),0.06,"gold",seg=8,rings=4,detail=True))
        yw=y+0.15 if y_wall is None else y_wall
        for s in (-1,1): P_.append(box(xc+s*(w/2)-0.025,xc+s*(w/2)+0.025,y-0.02,yw+0.02,ztop+0.02,ztop+0.08,"iron",detail=True))
    sw=w/folds
    for i in range(folds):
        x0=xc-w/2+i*sw; ang=(8 if i%2 else -8)*D2R
        P_.append(bxc((x0+sw/2,y-0.01*(i%2),ztop-h/2),(sw+0.01,0.03,h),cls,R=Matrix.Rotation(ang,3,'Z'),detail=True))
    tz=ztop-h
    P_.append(prism_xz([(xc-w/2,tz+0.005),(xc,tz+0.005),(xc-w/2,tz-0.35)],y-0.015,y+0.015,cls,detail=True))
    P_.append(prism_xz([(xc,tz+0.005),(xc+w/2,tz+0.005),(xc+w/2,tz-0.35)],y-0.015,y+0.015,cls,detail=True))
    if trim:
        P_.append(box(xc-w/2,xc+w/2,y-0.035,y-0.018,ztop-0.16,ztop-0.06,trim,detail=True))
        P_.append(box(xc-w/2,xc+w/2,y-0.035,y-0.018,tz+0.06,tz+0.14,trim,detail=True))
    if emblem:
        ez=ztop-h*0.45; e=0.22*w/0.8
        P_.append(prism_xz([(xc,ez+e),(xc+e*0.7,ez),(xc,ez-e),(xc-e*0.7,ez)],y-0.05,y-0.02,trim,detail=True))
        P_.append(sphere((xc,y-0.05,ez),0.05*w/0.8,trim,seg=6,rings=3,detail=True))
    return P_
def ring(ri,ro,z0,z1,nw,cls="stone",a0=0.0,skip=(),ro1=None,n=2,bev=0.0):
    """ring of nw wedges (n facets each) starting at a0-half wedge so wedge 0 is centred on a0"""
    P_=[]; da=2*math.pi/nw
    for i in range(nw):
        if i in skip: continue
        a=a0+i*da-da/2; w_=wedge(ri,ro,a,a+da,z0,z1,cls,n=n,ro1=ro1,bev=bev)
        if ro1 is None and cls=="stone" and z1-z0>0.5: w_.arc=(ro,a,a+da)
        P_.append(w_)
    return P_
def cone_roof(r_eave,r_base,z0,h,seg=20,courses=6,flag="cloth_red",finial=True,lip=0.05,eave_h=0.3,flag_dir=1):
    """stylised conical roof: flared eave frustum + stepped tile courses (each a convex frustum) + gold finial + flag"""
    P_=[frustum(r_eave,r_base,z0,z0+eave_h,seg,"roof",a0=math.pi/seg)]
    zb=z0+eave_h; H=h-eave_h; rr=lambda z:r_base*(1-(z-zb)/H)**1.05
    for k in range(courses):
        za=zb+H*k/courses; zc=zb+H*(k+1)/courses if k<courses-1 else zb+H
        P_.append(frustum(rr(za)+(lip if k else 0.0),max(rr(zc),0.0) if k<courses-1 else 0.0,za,zc+(0.0 if k==courses-1 else 0.02),seg,"roof",a0=math.pi/seg))
    top=z0+h
    if finial:
        P_.append(cyl_z(0,0,top-0.25,top+0.25,0.05,6,"iron",detail=True))
        P_.append(sphere((0,0,top+0.3),0.11,"gold",seg=8,rings=5,detail=True))
    if flag:
        P_.append(cyl_z(0,0,top+0.38,top+1.5,0.025,6,"iron",detail=True))
        P_.append(sphere((0,0,top+1.55),0.05,"gold",seg=6,rings=3,detail=True))
        fw=0.95; fh=0.55; zf=top+1.45-fh/2
        for i in range(3):
            x0=flag_dir*(0.02+i*fw/3); x1=flag_dir*(0.02+(i+1)*fw/3)
            ang=(10 if i%2 else -10)*D2R
            P_.append(bxc(((x0+x1)/2,0.02*(i%2),zf-0.03*i),(abs(x1-x0)+0.01,0.025,fh-0.04*i),flag,R=Matrix.Rotation(ang,3,'Z'),detail=True))
    return P_
# ------------------------------------------------------------------ v2 detail parts
import random as _rnd, zlib as _z
def vine(x,y_wall,z0,z1,seed=1,spread=0.45,step=0.32):
    """climbing ivy flattened on a -Y wall face: leaf clusters (convex blobs) up a wandering stem"""
    R=_rnd.Random(_z.crc32(f"vine{x:.2f}{z0:.2f}{seed}".encode())); P_=[]; z=z0; xc=x; prev=None
    while z<z1:
        for k in range(2 if z<z1-step else 1):
            cx=xc+R.uniform(-spread,spread)*(0.5+0.5*(z-z0)/max(z1-z0,0.1)); s=R.uniform(0.13,0.2)
            pts=[Vector((cx+R.uniform(-s,s),y_wall-R.uniform(0.01,0.09),z+R.uniform(-s,s)*0.8)) for _ in range(7)]
            P_.append(P(pts,"leaf",detail=True))
        if prev: P_.append(seg_box(Vector((prev,y_wall-0.02,z-step)),Vector((xc,y_wall-0.02,z)),0.035,0.035,"wood_dk",up=Vector((0,-1,0)),detail=True))
        prev=xc; xc+=R.uniform(-0.15,0.15); z+=step
    return P_
def balcony(xc,y_wall,z,w=1.4,d=0.6,posts=5,cls="wood",rail="wood_dk"):
    """timber balcony on two brackets with a railing (front -Y)"""
    y1=y_wall; y0=y_wall-d; P_=[box(xc-w/2,xc+w/2,y0,y1,z-0.08,z,cls)]
    for s in (-0.38,0.38): P_.append(P([Vector((xc+s*w+dx,yy,zz)) for dx in (-0.05,0.05) for (yy,zz) in ((y1,z-0.08),(y0+0.08,z-0.08),(y1,z-0.6))],rail))
    P_.append(box(xc-w/2,xc+w/2,y0,y0+0.07,z+0.82,z+0.9,rail,detail=True))
    for s in (-1,1): P_.append(box(xc+s*w/2-(0.07 if s>0 else 0),xc+s*w/2+(0.07 if s<0 else 0),y0,y1,z+0.82,z+0.9,rail,detail=True))
    for i in range(posts):
        px=xc-w/2+0.04+i*(w-0.08)/(posts-1); P_.append(box(px-0.03,px+0.03,y0+0.005,y0+0.065,z,z+0.82,rail,detail=True))
    P_.append(box(xc-w/2,xc+w/2,y0,y0+0.07,z+0.3,z+0.36,rail,detail=True))
    return P_
def beam_ends(xs,y_wall,z,cls="wood_dk",out=0.32,s=0.16):
    return [box(x-s/2,x+s/2,y_wall-out,y_wall+0.02,z,z+s,cls,detail=True,bev=0.012) for x in xs]
def hip_caps(w,z0,H,cls="roof",t=0.1):
    """ridge caps down the four hips of a square pyramid roof (base width w at z0, apex at H)"""
    return [seg_box(Vector((sx*w/2,sy*w/2,z0+0.04)),Vector((0,0,H)),t,t*0.7,cls,up=Vector((sx,sy,1.4)).normalized(),detail=True) for sx in (-1,1) for sy in (-1,1)]
def flower_box(xc,y_wall,z,w=0.8):
    return [box(xc-w/2,xc+w/2,y_wall-0.26,y_wall-0.02,z-0.2,z,"wood",detail=True),
            P([Vector((xc+x,y_wall+y,z)) for x in (-w/2+0.04,w/2-0.04) for y in (-0.24,-0.04)]+[Vector((xc+x*0.85,y_wall-0.14+y,z+0.16)) for x in (-w/2,w/2) for y in (-0.08,0.08)],"leaf",detail=True)]
def cone_dormer(r_wall,z,s=1.0,a=0.0):
    """small plaster dormer with a tile gable roof and a framed window, sitting on a cone roof at radius r_wall (front -Y)"""
    y=-r_wall
    D=[box(-0.3*s,0.3*s,y,y+0.9*s,z,z+0.6*s,"plaster"),
       prism_xz([(-0.42*s,z+0.58*s),(0.42*s,z+0.58*s),(0,z+0.98*s)],y-0.1*s,y+1.0*s,"roof"),
       box(-0.18*s,0.18*s,y-0.02,y+0.02,z+0.08*s,z+0.5*s,"glass",detail=True),
       box(-0.24*s,0.24*s,y-0.05,y,z+0.04*s,z+0.1*s,"wood_dk",detail=True),box(-0.24*s,0.24*s,y-0.05,y,z+0.48*s,z+0.54*s,"wood_dk",detail=True),
       box(-0.24*s,-0.18*s,y-0.05,y,z+0.04*s,z+0.54*s,"wood_dk",detail=True),box(0.18*s,0.24*s,y-0.05,y,z+0.04*s,z+0.54*s,"wood_dk",detail=True)]
    return xf(D,Rz(a))
