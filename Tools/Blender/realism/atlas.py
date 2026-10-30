"""Builds per-asset tiling-cell atlases (A/N/ORM) from the graded CC0 sets + procedural granite/steel/gold/glass.
Each cell = an exact number of texture repeats with a 16px wrapped gutter, so faces cut on the cell grid tile seamlessly.
Writes <asset>/Textures/T_<Asset>_{A,N,ORM}.png (authored size) + cells.json (UV rects + metres per cell)."""
import numpy as np, json, os, sys
from PIL import Image
from grade import MASTER as MAT, ROOT, save, pack_orm
R=ROOT+"/Assets/_Project/Models/Realistic"
SRC_M={"SandFace":1.5,"Sandstone":1.52,"Plaster":1.0,"WoodBeam":.5,"WoodPlank":1.4,"Terracotta":2.0,"Grass":2.0,"Dirt":1.3,"Cobble":2.0,"Rock":3.0,"OakBark":1.0,"PineBark":2.0}
def ld(n,k): return np.asarray(Image.open(f"{MAT}/T_{n}_{k}.png").convert("RGB")).astype(np.float32)/255
def rs(a,w,h): return np.asarray(Image.fromarray((a*255+.5).astype(np.uint8)).resize((w,h),Image.LANCZOS)).astype(np.float32)/255
def rs_wrap(a,w,h,p=48):
    """seam-free resample of a tileable map: wrap-pad, resize, crop (plain LANCZOS clamps at the borders -> visible seam lines)"""
    H,W=a.shape[:2]; big=np.pad(a,((p,p),(p,p),(0,0)),mode="wrap"); sx,sy=w/W,h/H
    r=rs(big,int(round((W+2*p)*sx)),int(round((H+2*p)*sy))); ox,oy=int(round(p*sx)),int(round(p*sy))
    out=r[oy:oy+h,ox:ox+w]
    if out.shape[:2]!=(h,w): out=rs(out,w,h)
    return out
def rot90(a,isN=False):
    b=np.rot90(a).copy()
    if isN: x=b[...,0].copy(); b[...,0]=1-b[...,1]; b[...,1]=x      # rotate tangent-space vector with the image
    return b
rng=np.random.default_rng(7)
def noise(h,w,scales):
    out=np.zeros((h,w),np.float32)
    for s,amp in scales:
        n=rng.random((max(2,h//s),max(2,w//s))).astype(np.float32)
        n=np.tile(n,(1,1)); out+=amp*rs(np.stack([n]*3,-1),w,h)[...,0]
    return out
def proc(kind,w,h):
    flatN=np.stack([np.full((h,w),.5),np.full((h,w),.5),np.ones((h,w))],-1).astype(np.float32)
    if kind in ("Flowers","SignBoard"):   # realistic v1 townhouse charm cells (procedural, Art v3: lush greens, warm accents, no cyan)
        from PIL import Image as _I, ImageDraw as _D
        im=_I.new("RGB",(w,h)); d=_D.Draw(im); r2=np.random.default_rng(5 if kind=="Flowers" else 6)
        if kind=="Flowers":   # geranium box planting: leaf mass + red/pink/white florets
            im.paste((70,150,52),(0,0,w,h))
            for _ in range(int(w*h/90)):
                x,y=r2.integers(0,w),r2.integers(0,h); rr=int(r2.integers(3,9)*w/256); g=r2.integers(-25,30)
                d.ellipse((x-rr,y-rr,x+rr,y+rr),fill=(int(64+g*.6),int(150+g),int(48+g*.5)))
            for _ in range(int(w*h/900)):
                x,y=r2.integers(0,w),r2.integers(0,h); c=[(232,58,62),(240,96,120),(250,238,226),(232,58,62)][r2.integers(4)]; rr=int(r2.integers(4,8)*w/256)
                for k in range(5): a=k*1.256; d.ellipse((x+rr*.6*np.cos(a)-rr*.5,y+rr*.6*np.sin(a)-rr*.5,x+rr*.6*np.cos(a)+rr*.5,y+rr*.6*np.sin(a)+rr*.5),fill=c)
                d.ellipse((x-rr*.3,y-rr*.3,x+rr*.3,y+rr*.3),fill=(245,196,81))
            rough=.85
        else:                 # painted hanging sign: deep green board, gold border, gold loaf + pretzel icon (no text: localisation)
            im.paste((38,86,64),(0,0,w,h)); b=int(w*.06); d.rectangle((b,b,w-b,h-b),outline=(232,176,75),width=max(2,int(w*.03)))
            cx,cy=w*.5,h*.5; R_=w*.2
            d.ellipse((cx-R_*1.3,cy-R_*.6,cx+R_*1.3,cy+R_*.7),fill=(214,150,80),outline=(232,176,75),width=max(2,int(w*.015)))
            for k in (-1,0,1): d.line((cx+k*R_*.6-R_*.2,cy-R_*.4,cx+k*R_*.6+R_*.2,cy+R_*.4),fill=(150,92,44),width=max(2,int(w*.02)))
            rough=.6
        A=np.asarray(im).astype(np.float32)/255; A=np.clip(A,50/255,240/255)
        n=noise(h,w,[(16,.5),(4,.5)]); N=flatN.copy(); N[...,0]+=.06*(n-.5); N[...,1]+=.06*(np.roll(n,3,0)-.5)
        ORM=np.stack([np.ones((h,w))*.95,np.full((h,w),rough),np.zeros((h,w))],-1); return A,N,ORM
    if kind=="Granite":   # fine speckle, ±6% value, polished
        base=np.array([0x4A,0x4F,0x5C])/255; sp=rng.random((h,w)); n=noise(h,w,[(64,.5),(16,.5)])
        v=1+.05*(n-n.mean())/n.std()*.5+np.where(sp>.97,.12,0)-np.where(sp<.03,.10,0)
        A=np.clip(base*v[...,None],0,1); ORM=np.stack([np.ones((h,w)),np.full((h,w),.42)+.05*(n-.5),np.zeros((h,w))],-1)
        return A,flatN,ORM
    if kind in("Steel","Gold"):  # brushed: streaks along u
        base=np.array([0xAE,0xB6,0xC2] if kind=="Steel" else [0xF5,0xC4,0x51])/255
        st=rs(np.stack([np.repeat(rng.random((h,1)),8,1)]*3,-1).astype(np.float32),w,h)[...,0]
        A=np.clip(base*(1+.04*(st-.5))[...,None],0,1)
        r=(.3 if kind=="Steel" else .25)+.06*(st-.5)
        N=flatN.copy(); N[...,1]+= .03*(st-.5)
        return A,N,np.stack([np.ones((h,w)),r,np.ones((h,w))],-1)
    if kind=="Glass":
        A=np.broadcast_to(np.array([0x9F,0xD8,0xEE])/255,(h,w,3)).copy()
        return A,flatN,np.stack([np.ones((h,w)),np.full((h,w),.05),np.zeros((h,w))],-1)
    if kind=="EndGrain":  # raw/split wood #F5C994 with growth rings
        yy,xx=np.mgrid[0:h,0:w]/max(h,w); r=np.hypot(xx-.3,yy-1.2)*60+3*noise(h,w,[(32,1)])
        ring=.5+.5*np.sin(r); A=np.clip(np.array([0xF5,0xC9,0x94])/255*(1-.06*ring[...,None]),0,1)
        N=flatN.copy(); N[...,0]+=.04*np.gradient(ring,axis=1)*20
        return A,N,np.stack([np.ones((h,w)),.7+.05*ring,np.zeros((h,w))],-1)
    if kind=="SandDressed":  # dressed (cut) sandstone: CC0 rock grain graded to fresh sandstone, normal softened
        A,N,O=cell("SandFace",w,h,(1.5,1.5),False,.6); return A,N,O
    if kind=="WinGlass":     # window glass: pale blue body + baked sky/ground reflection gradient (mobile probes are blurry)
        yy=np.linspace(0,1,h)[:,None,None]; top=np.array([0xB8,0xDF,0xF0])/255; bot=np.array([0x5F,0x86,0x9C])/255
        A=np.broadcast_to(top*(1-yy)+bot*yy,(h,w,3)).copy()
        A*=1+.03*(noise(h,w,[(32,1)])-.5)[...,None]
        return A,flatN,np.stack([np.ones((h,w)),np.full((h,w),.04),np.zeros((h,w))],-1)
    raise KeyError(kind)
def cell(kind,wi,hi,S,rot=False,nflat=1.0):
    if kind in SRC_M:
        A,N,O=ld(kind,"A"),ld(kind,"N"),ld(kind,"ORM")
        if rot: A,N,O=rot90(A),rot90(N,True),rot90(O)
        D=SRC_M[kind]; Dx,Dy=(D,D)
        kx=max(1,round(S[0]/Dx)); ky=max(1,round(S[1]/Dy))
        tw,th=wi//kx,hi//ky
        A,N,O=[np.tile(rs_wrap(x,tw,th),(ky,kx,1)) for x in (A,N,O)]
        A,N,O=[rs(x,wi,hi) if x.shape[:2]!=(hi,wi) else x for x in (A,N,O)]
        if nflat!=1: N[...,:2]=.5+(N[...,:2]-.5)*nflat
        return A,N,O
    return proc(kind,wi,hi)
def build(asset,folder,size,cells,g=16):
    A=np.zeros((size,size,3),np.float32)+.5; N=np.zeros_like(A); N[...]=(.5,.5,1); O=np.zeros_like(A); O[...]=(1,.8,0)
    meta={"size":size,"cells":{}}
    for name,(kind,(x,y,w,h),S,*opt) in cells.items():
        o=opt[0] if opt else {}
        wi,hi=w-2*g,h-2*g; c=cell(kind,wi,hi,S,o.get("rot",False),o.get("nflat",1))
        for dst,src in zip((A,N,O),c): dst[y:y+h,x:x+w]=np.pad(src,((g,g),(g,g),(0,0)),mode="wrap")
        # UV rect (Blender: v up from bottom)
        meta["cells"][name]={"uv":[(x+g)/size,1-(y+h-g)/size,(x+w-g)/size,1-(y+g)/size],"m":list(S),"pxm":wi/S[0]}
    d=f"{R}/{folder}/{asset}/Textures"; os.makedirs(d,exist_ok=True)
    save(A,f"{d}/T_{asset}_A.png",size); save(N,f"{d}/T_{asset}_N.png",size); save(O,f"{d}/T_{asset}_ORM.png",size)
    json.dump(meta,open(f"{d}/../cells.json","w"),indent=1); print("atlas",asset,size,len(cells),"cells")
TOWN={ # slot px (authored 2048 = 512px/m nominal), world metres per cell
 "Sandstone":("Sandstone",(0,0,1024,1024),(1.5,1.5)),
 "Plaster":("Plaster",(1024,0,1024,1024),(2.0,2.0),{"nflat":.45}),
 "WoodBeam":("WoodBeam",(0,1024,1024,512),(2.0,1.0),{"rot":True}),
 "WoodPlank":("WoodPlank",(1024,1024,512,512),(1.0,1.0)),
 "Terracotta":("Terracotta",(1536,1024,512,512),(1.0,1.0)),
 "Granite":("Granite",(0,1536,512,512),(2.0,2.0)),
 "Steel":("Steel",(512,1536,256,256),(.5,.5)),
 "Gold":("Gold",(768,1536,256,256),(.5,.5)),
 "Glass":("WinGlass",(512,1792,256,256),(.5,.5)),
 "EndGrain":("EndGrain",(768,1792,256,256),(.5,.5)),
 "SandDressed":("SandDressed",(1024,1536,512,512),(1.0,1.0)),
 "Sandstone2":("Sandstone",(1536,1536,512,512),(.75,.75)),
}
# realistic v1 modular townhouse (Kit_RealHouse_*): same cells, chimney stone shares a smaller slot, + flowers and sign board
HOUSE={**TOWN,"Sandstone2":("Sandstone",(1536,1536,256,256),(.75,.75)),
 "Flowers":("Flowers",(1792,1536,256,256),(.5,.5)),"SignBoard":("SignBoard",(1536,1792,512,256),(.6,.3))}
PROPS={ # authored 1024 -> 512 on Android (props cap)
 "WoodPlank":("WoodPlank",(0,0,512,512),(1.0,1.0)),
 "WoodBeam":("WoodBeam",(512,0,512,256),(1.0,.5),{"rot":True}),
 "Steel":("Steel",(512,256,256,256),(.5,.5)),
 "EndGrain":("EndGrain",(768,256,256,256),(.5,.5)),
 "Rock":("Rock",(0,512,512,512),(1.5,1.5)),  # rock graded #9A968A
 "Terracotta":("Terracotta",(512,512,256,256),(.5,.5)),
 "SandDressed":("SandDressed",(768,512,256,256),(.5,.5)),
 "Dirt":("Dirt",(512,768,512,256),(1.0,.5)),
}
GROUND={
 "Grass":("Grass",(0,0,1024,1024),(4.0,4.0)),
 "Dirt":("Dirt",(1024,0,1024,1024),(2.0,2.0)),
 "Cobble":("Cobble",(0,1024,1024,1024),(2.0,2.0)),
 "Rock":("Rock",(1024,1024,1024,1024),(1.5,1.5)),
}
if __name__=="__main__":
    w=sys.argv[1:] or ["town","props","ground"]
    if "town" in w: build("Bld_Townhouse","Buildings/Hero",2048,TOWN)
    if "house" in w: build("RealHouse","Buildings",2048,HOUSE)   # -> Buildings/RealHouse/Textures; house_modular.py moves it to Bld_Townhouse at 1024
    if "props" in w: build("Props_Village","Props",1024,PROPS)
    if "ground" in w: build("Ground_Patch","Props",2048,GROUND)
