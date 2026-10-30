"""GrassClump card atlas (original, procedural blades; colours from the v3 foliage range). 2048 authored -> 1024."""
import numpy as np, os, math, json
from PIL import Image, ImageDraw, ImageFilter
from grade import ROOT
R=ROOT+"/Assets/_Project/Models/Realistic/Foliage/GrassClump"
rng=np.random.default_rng(12)
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
def hx(h): return tuple(int(h[i:i+2],16) for i in (1,3,5))
def card(W,H,n,flowers=0):
    """dense tuft: many thin blades rooted in a tight band, leaning outward, short/long mix; base colour = graded ground
    grass (#57A740) so tufts melt into the terrain instead of reading as dark spikes"""
    col=Image.new("RGB",(W,H),hx("#57A740")); al=Image.new("L",(W,H),0); nm=Image.new("RGB",(W,H),(128,128,255))
    dc,da,dn=ImageDraw.Draw(col),ImageDraw.Draw(al),ImageDraw.Draw(nm)
    blades=[]
    for k in range(n):
        x0=W*(.5+np.clip(rng.normal(0,.2),-.46,.46)); t=(x0/W-.5)*2
        h=H*np.clip(rng.normal(.55,.18),.18,.97)*(1-.35*abs(t)); lean=(t*.5+rng.normal(0,.18))*h
        blades.append((rng.random(),x0,h,lean,W*rng.uniform(.004,.009)))
    blades.sort()
    for dep,x0,h,lean,w0 in blades:
        base=hx("#57A740"); tip=hx(["#8CD65A","#7FCF50","#73C449","#9BDB6A"][rng.integers(4)])
        if dep<.3: tip=lerp(tip,hx("#4E9E38"),.4)          # back blades a touch darker (depth), never olive
        segs=8; pts=[]
        for j in range(segs+1):
            u=j/segs; pts.append((x0+lean*u*u,H-1-h*(u-.04*u*u),w0*(1-u)**.7))
        for j in range(segs):
            (xa,ya,wa),(xb,yb,wb)=pts[j],pts[j+1]; u=j/segs
            poly=[(xa-wa,ya),(xa+wa,ya),(xb+wb,yb),(xb-wb,yb)]
            dc.polygon(poly,fill=lerp(base,tip,min(1,u**.8*1.15))); da.polygon(poly,fill=255)
            dn.polygon([(xa-wa,ya),(xa,ya),(xb,yb),(xb-wb,yb)],fill=(96,140,236)); dn.polygon([(xa,ya),(xa+wa,ya),(xb+wb,yb),(xb,yb)],fill=(160,140,236))
    for k in range(flowers):
        x=W*(.5+rng.normal(0,.2)); y=H*rng.uniform(.3,.6); r=W*.006
        c=hx("#FFF6D8") if rng.random()<.6 else hx("#F5C451")
        for dx,dy in ((-r,0),(r,0),(0,-r),(0,r)): dc.ellipse((x+dx-r,y+dy-r,x+dx+r,y+dy+r),fill=c); da.ellipse((x+dx-r,y+dy-r,x+dx+r,y+dy+r),fill=255)
        dc.ellipse((x-r*.7,y-r*.7,x+r*.7,y+r*.7),fill=hx("#F5C451"))
    return np.asarray(col),np.asarray(al),np.asarray(nm)
if __name__=="__main__":
    S=2048; A=np.zeros((S,S,4),np.uint8); N=np.zeros((S,S,3),np.uint8); N[...]=(128,128,255); O=np.zeros((S,S,3),np.uint8); O[...]=(255,180,0)
    meta={"size":S,"cards":{}}
    for i,(y,fl) in enumerate(((0,0),(1024,14))):
        c,a,n=card(S,1024,620,fl); A[y:y+1024,:,:3]=c; A[y:y+1024,:,3]=a; N[y:y+1024]=n
        yy=np.linspace(0,1,1024)[:,None]; O[y:y+1024,:,0]=(255*(1-(1-np.clip(1.6-yy*1.0,0,1))*.5)).astype(np.uint8)   # AO darker at the roots
        meta["cards"][f"G{i}"]={"uv":[0,1-(y+1024)/S,1,1-y/S],"aspect":2.0}
    # colour bleed under transparent texels
    rgb=A[...,:3].astype(np.float32); m=(A[...,3]>127).astype(np.float32)
    bl=np.asarray(Image.fromarray(A[...,:3]).filter(ImageFilter.GaussianBlur(8))).astype(np.float32)
    A[...,:3]=np.where(m[...,None]>0,rgb,bl).astype(np.uint8)
    os.makedirs(R+"/Textures",exist_ok=True)
    Image.fromarray(A,"RGBA").save(R+"/Textures/T_GrassClump_A.png"); Image.fromarray(N).save(R+"/Textures/T_GrassClump_N.png"); Image.fromarray(O).save(R+"/Textures/T_GrassClump_ORM.png")
    json.dump(meta,open(R+"/cards.json","w"),indent=1); print("grass atlas ok")
