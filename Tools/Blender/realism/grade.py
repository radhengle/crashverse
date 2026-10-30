"""De-light + grade CC0 scans to the v3 locked albedo targets (style_board_v3 §2-§4).
Writes graded tileable sets (A/N/ORM, 2048 authored) to Assets/_Project/Models/Realistic/Materials/Textures/
ORM packing is done by pack_orm() (R=AO strength<=0.5, G=roughness remapped to target range, B=metal)."""
import numpy as np, os, sys, colorsys
from PIL import Image, ImageFilter
PH="/workspace/cc0/polyhaven"; ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"../../.."))
OUT=ROOT+"/Assets/_Project/Models/Realistic/Materials/Textures"   # shipped shared tiling surfaces: 1024 (Android cap)
MASTER="/workspace/crashverse/realism_masters"           # 2048 graded masters (rebuildable, kept out of git)
def hx(h): h=h.lstrip("#"); return np.array([int(h[i:i+2],16) for i in (0,2,4)],float)
# name: (scan, target avg hex, value variation limit, rough range, deLight strength)
MATS={
 "Sandstone": ("rustic_stone_wall","#C9B48A",.12,(.70,.85)),
 "Plaster":   ("white_rough_plaster","#EADCC0",.06,(.80,.90)),
 "WoodBeam":  ("rough_wood","#E3A766",.12,(.60,.75)),
 "WoodPlank": ("worn_planks","#E3A766",.12,(.60,.75)),
 "Terracotta":("clay_floor_001","#D9703F",.10,(.60,.70)),
 "Grass":     ("leafy_grass","#55A83A",.24,(.60,.80)),
 "Dirt":      ("brown_mud_leaves_01","#B48A5C",.12,(.80,.95)),
 "Cobble":    ("cobblestone_floor_08","#B8A88C",.14,(.70,.85)),
 "Rock":      ("mossy_rock","#9A968A",.14,(.75,.90)),
 "OakBark":   ("jolcham_oak_bark_01","#8E7A62",.16,(.80,.95)),
 "PineBark":  ("pine_bark","#9A6E52",.16,(.80,.95)),
 "SandFace":  ("mossy_rock","#CDBA90",.08,(.70,.82)),
 # realistic v1 (L1-10 + Daily Boss block kit): brick running bond, brushed steel plate, clay roof tiles + fracture interiors
 "Brick":     ("brick_wall_001","#D2664C",.12,(.75,.85)),
 "Steel":     ("metal_plate","#AEB6C2",.08,(.25,.35),1.0),
 "Roof":      ("clay_roof_tiles","#D9703F",.10,(.60,.70)),
 "BrickInner":("clay_floor_001","#E9997C",.08,(.85,.95)),
 "WoodInner": ("rough_wood","#EBC28E",.10,(.75,.90)),
}
def load(p,mode="RGB"): return np.asarray(Image.open(p).convert(mode)).astype(np.float32)/255
def blur(a,r):
    im=Image.fromarray((np.clip(a,0,1)*65535).astype(np.uint16) if False else (np.clip(a,0,1)*255).astype(np.uint8))
    # tile-aware blur: pad by wrap so tiles stay seamless
    h,w=a.shape; p=int(r*3); big=np.pad(a,p,mode="wrap")
    im=Image.fromarray((big*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
    return (np.asarray(im).astype(np.float32)/255)[p:p+h,p:p+w]
def rgb2hsv(a):
    r,g,b=a[...,0],a[...,1],a[...,2]; mx=a.max(-1); mn=a.min(-1); d=mx-mn+1e-6
    h=np.where(mx==r,((g-b)/d)%6,np.where(mx==g,(b-r)/d+2,(r-g)/d+4))/6
    s=np.where(mx>0,(mx-mn)/(mx+1e-6),0); return h,s,mx
def hsv2rgb(h,s,v):
    i=np.floor(h*6)%6; f=h*6-np.floor(h*6); p=v*(1-s); q=v*(1-f*s); t=v*(1-(1-f)*s)
    c=[(v,t,p),(q,v,p),(p,v,t),(p,q,v),(t,p,v),(v,p,q)]
    out=np.zeros(h.shape+(3,),np.float32)
    for k in range(6):
        m=i==k
        for j in range(3): out[...,j]=np.where(m,c[k][j],out[...,j])
    return out
def delight(A,AO):
    L=A.mean(-1); low=blur(L,A.shape[0]/24); S=low/low.mean()
    A=A/np.clip(S,.3,3)[...,None]**.85            # remove large-scale baked light gradients
    A=A/np.clip(AO,.25,1)[...,None]**.6           # remove baked cavity shadow
    return np.clip(A,0,1)
KEEP={"Steel":(0,.2),"WoodInner":(.1,.35),"Brick":(.3,.8),"Roof":(.3,.8),"BrickInner":(.2,.5),"Plaster":(0,.15),"WoodBeam":(.1,.35),"WoodPlank":(.15,.4),"OakBark":(.12,.5),"PineBark":(.12,.4),"Rock":(.2,.5),"SandFace":(.05,.3)}
def grade(A,target,var,keep=(.35,1)):
    t=hx(target)/255; th,ts,tv=colorsys.rgb_to_hsv(*t)
    h,s,v=rgb2hsv(A)
    hm=np.angle(np.exp(2j*np.pi*h).mean())/(2*np.pi)%1
    dh=(h-hm+.5)%1-.5; h=(th+dh*keep[0])%1                       # hue to target, keep a little natural variation
    sm=max(s.mean(),1e-3); s=np.clip(ts*(1+(s/sm-1)*keep[1]),0,1)
    dv=(v-v.mean())/max(v.mean(),1e-3); lim=np.percentile(np.abs(dv),95)
    dv=dv*(var/max(lim,1e-3))                                  # 95% of pixels within ±var
    v=np.clip(tv*(1+dv),0,1)
    out=hsv2rgb(h,s,v)
    for _ in range(4): out=np.clip(out*(t/out.reshape(-1,3).mean(0)),0,1)   # lock the average exactly
    return np.clip(out,52/255,236/255)   # margin so LANCZOS downscales stay inside 50..240
def pack_orm(ao,rough,metal,ao_strength=.5,rr=None):
    ao=1-(1-ao)*ao_strength
    if rr: rough=rr[0]+(rough-rough.mean())*(rr[1]-rr[0])/max(4*rough.std(),1e-3)+ (rr[1]-rr[0])/2
    rough=np.clip(rough,0,1); metal=np.broadcast_to(metal,ao.shape)
    return np.stack([ao,rough,metal],-1)
def save(a,p,size=2048):
    os.makedirs(os.path.dirname(p),exist_ok=True)
    im=Image.fromarray((np.clip(a,0,1)*255+.5).astype(np.uint8))
    if im.size!=(size,size): im=im.resize((size,size),Image.LANCZOS)
    im.save(p)
def run(names=None):
    for n,(scan,tgt,var,rr,*mt) in MATS.items():
        if names and n not in names: continue
        b=f"{PH}/{scan}/{scan}_"
        A=load(b+"Diffuse_2k.jpg"); AO=load(b+"AO_2k.jpg","L"); R=load(b+"Rough_2k.jpg","L")
        N=load(b+"nor_gl_2k.png"); H=load(b+"Displacement_2k.png","L")
        G=grade(delight(A,AO),tgt,var,KEEP.get(n,(.35,1)))
        O=pack_orm(AO,R,mt[0] if mt else 0.0,rr=rr)
        for d,sz in ((MASTER,2048),(OUT,1024)):
            save(G,f"{d}/T_{n}_A.png",sz); save(N,f"{d}/T_{n}_N.png",sz); save(O,f"{d}/T_{n}_ORM.png",sz)
        g=(G*255).reshape(-1,3).mean(0); hh,ss,vv=colorsys.rgb_to_hsv(*(g/255))
        print(f"{n:11s} avg #{''.join(f'{int(c):02X}' for c in g)} target {tgt} V{vv:.2f} S{ss:.2f}")
if __name__=="__main__": run(sys.argv[1:] or None)
