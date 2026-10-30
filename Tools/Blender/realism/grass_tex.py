"""GrassClump card atlas (original, procedural blades; colours from the v3 foliage range). 2048 authored -> 1024."""
import numpy as np, os, math, json
from PIL import Image, ImageDraw, ImageFilter
from grade import ROOT
R=ROOT+"/Assets/_Project/Models/Realistic/Foliage/GrassClump"
rng=np.random.default_rng(12)
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
def hx(h): return tuple(int(h[i:i+2],16) for i in (1,3,5))
def card(W,H,n,flowers=0):
    col=Image.new("RGB",(W,H),hx("#4FAE33")); al=Image.new("L",(W,H),0); nm=Image.new("RGB",(W,H),(128,128,255))
    dc,da,dn=ImageDraw.Draw(col),ImageDraw.Draw(al),ImageDraw.Draw(nm)
    order=sorted(range(n),key=lambda i:rng.random())
    for k in order:
        x0=W*(.5+rng.normal(0,.17)); h=H*rng.uniform(.45,.97); lean=rng.normal(0,.22)*h; w0=W*rng.uniform(.008,.016)
        base=hx("#4FA63A") if rng.random()<.5 else hx("#57AC40"); tip=hx(["#86D455","#78CC4C","#6CC245"][rng.integers(3)])
        segs=10; pts=[]
        for j in range(segs+1):
            t=j/segs; x=x0+lean*t*t; y=H-1-h*t; pts.append((x,y,w0*(1-t)**.8))
        for j in range(segs):
            (xa,ya,wa),(xb,yb,wb)=pts[j],pts[j+1]; t=j/segs
            poly=[(xa-wa,ya),(xa+wa,ya),(xb+wb,yb),(xb-wb,yb)]
            c=lerp(base,tip,min(1,t*1.3)); dc.polygon(poly,fill=c); da.polygon(poly,fill=255)
            # blade fold: left half normal tilts left, right half right
            dn.polygon([(xa-wa,ya),(xa,ya),(xb,yb),(xb-wb,yb)],fill=(90,128,240)); dn.polygon([(xa,ya),(xa+wa,ya),(xb+wb,yb),(xb,yb)],fill=(166,128,240))
    for k in range(flowers):   # a few tiny white/yellow wildflowers (meadow accent)
        x=W*(.5+rng.normal(0,.2)); y=H*rng.uniform(.25,.55); r=W*.008
        c=hx("#FFF6D8") if rng.random()<.6 else hx("#F5C451")
        dc.ellipse((x-r,y-r,x+r,y+r),fill=c); da.ellipse((x-r,y-r,x+r,y+r),fill=255)
    return np.asarray(col),np.asarray(al),np.asarray(nm)
if __name__=="__main__":
    S=2048; A=np.zeros((S,S,4),np.uint8); N=np.zeros((S,S,3),np.uint8); N[...]=(128,128,255); O=np.zeros((S,S,3),np.uint8); O[...]=(255,180,0)
    meta={"size":S,"cards":{}}
    for i,(y,fl) in enumerate(((0,0),(1024,14))):
        c,a,n=card(S,1024,260,fl); A[y:y+1024,:,:3]=c; A[y:y+1024,:,3]=a; N[y:y+1024]=n
        yy=np.linspace(0,1,1024)[:,None]; O[y:y+1024,:,0]=(255*(1-(1-np.clip(1.6-yy*1.0,0,1))*.5)).astype(np.uint8)   # AO darker at the roots
        meta["cards"][f"G{i}"]={"uv":[0,1-(y+1024)/S,1,1-y/S],"aspect":2.0}
    # colour bleed under transparent texels
    rgb=A[...,:3].astype(np.float32); m=(A[...,3]>127).astype(np.float32)
    bl=np.asarray(Image.fromarray(A[...,:3]).filter(ImageFilter.GaussianBlur(8))).astype(np.float32)
    A[...,:3]=np.where(m[...,None]>0,rgb,bl).astype(np.uint8)
    os.makedirs(R+"/Textures",exist_ok=True)
    Image.fromarray(A,"RGBA").save(R+"/Textures/T_GrassClump_A.png"); Image.fromarray(N).save(R+"/Textures/T_GrassClump_N.png"); Image.fromarray(O).save(R+"/Textures/T_GrassClump_ORM.png")
    json.dump(meta,open(R+"/cards.json","w"),indent=1); print("grass atlas ok")
