"""System-python (PIL+numpy) texture generator for CRASHVERSE v3 (CrashverseGlossyPBR).
Maps: Albedo (sRGB), Normal (tangent space, OpenGL Y+), ORM (R=AO, G=roughness, B=metallic, linear)."""
import numpy as np, sys, os
from PIL import Image
def hx(h): return np.array([int(h[i:i+2],16) for i in (1,3,5)],float)/255
def save(a,p,mode="RGB"): os.makedirs(os.path.dirname(p),exist_ok=True); Image.fromarray(np.clip(a*255+0.5,0,255).astype(np.uint8),mode).save(p)
def normal_from_height(h,strength):
    gy,gx=np.gradient(h); n=np.dstack([-gx*strength,gy*strength,np.ones_like(h)])  # OpenGL: +Y up (green = up)
    n/=np.linalg.norm(n,axis=2,keepdims=True); return n*0.5+0.5
def noise(n,s,seed):
    r=np.random.default_rng(seed); a=r.random((n//s+2,n//s+2))
    im=Image.fromarray((a*255).astype(np.uint8)).resize((n+2*s,n+2*s),Image.BICUBIC); return np.asarray(im,float)[s:s+n,s:s+n]/255
def sheet(kind,out,N=512):
    """tiling material sheet. 512 px = 2 m  -> 256 px/m"""
    y,x=np.mgrid[0:N,0:N]; rng=np.random.default_rng(1); ppm=N/2
    if kind=="Brick":   # 0.5 x 0.25 m bricks, 0.04 m mortar groove (deep, reads at distance)
        bw,bh,g=int(0.5*ppm),int(0.25*ppm),int(0.04*ppm/2)
        row=y//bh; xo=(x+(row%2)*bw//2)%N; col=xo//bw
        lx=xo%bw; ly=y%bh; edge=np.minimum(np.minimum(lx,bw-1-lx),np.minimum(ly,bh-1-ly))
        h=np.clip(edge/g,0,1)**0.5; tint=rng.uniform(0.88,1.1,(N//bh+1,N//bw+2))[row,col]
        base=hx("#D2664C")[None,None]*tint[...,None]*(0.94+0.12*noise(N,16,2)[...,None])
        alb=np.where(h[...,None]>0.15,base*(0.92+0.1*h[...,None]),hx("#6A2A1E"))
        rough=np.where(h>0.15,0.62,0.9); metal=np.zeros_like(h); ao=0.55+0.45*h; nstr=6
    elif kind=="Wood":  # planks 0.25 m wide, vertical grain
        pw=int(0.25*ppm); pl=x//pw; lx=x%pw; edge=np.minimum(lx,pw-1-lx); h=np.clip(edge/3,0,1)
        grain=0.5+0.5*np.sin((x+12*noise(N,64,3)*N/8)*0.35+pl*7); tint=rng.uniform(0.9,1.08,N//pw+1)[pl]
        alb=hx("#E3A766")[None,None]*(tint*(0.9+0.12*grain))[...,None]; alb=np.where(h[...,None]>0.3,alb,hx("#8A5A2E"))
        rough=0.5+0.1*grain; metal=np.zeros_like(h); ao=0.6+0.4*h; nstr=4; h=h+0.05*grain
    elif kind=="Glass":  # clear pale-cyan glass, faint streaks, very smooth
        streak=noise(N,32,6); h=0.1*streak
        alb=hx("#BFEFFF")[None,None]*(0.95+0.08*streak[...,None]); rough=0.05+0.05*streak; metal=np.zeros_like(h); ao=np.ones_like(h); nstr=2
    elif kind=="Stone":  # Art #10: warm sandstone #C9B48A, seams #8A7350, chipped edges, warm per-block variation, rough ~0.7
        bw,bh,g=int(0.75*ppm),int(0.5*ppm),int(0.03*ppm)
        row=y//bh; xo=(x+(row%2)*bw//2)%N; col=xo//bw; lx=xo%bw; ly=y%bh
        edge=np.minimum(np.minimum(lx,bw-1-lx),np.minimum(ly,bh-1-ly)).astype(float)
        chip=noise(N,6,11)*g*1.2; h=np.clip((edge-chip*0.6)/g,0,1)**0.6
        tint=rng.uniform(0.9,1.08,(N//bh+2,N//bw+3,3))[row,col]*np.array([1.0,0.98,0.94])
        base=hx("#C9B48A")[None,None]*tint*(0.92+0.14*noise(N,12,12)[...,None])
        alb=np.where(h[...,None]>0.2,base,hx("#8A7350")); rough=np.where(h>0.2,0.7,0.85)+0.03*noise(N,8,13)
        metal=np.zeros_like(h); ao=0.55+0.45*h; nstr=7; h=h+0.08*noise(N,8,14)
    elif kind=="Granite":  # Art #10: indestructible, monolithic charcoal #4A4F5C -> #2E3340, no seams, smoothness ~0.5
        n=0.6*noise(N,24,15)+0.4*noise(N,3,16); sp=(noise(N,1,17)>0.82)
        alb=hx("#2E3340")[None,None]*(1-n[...,None])+hx("#4A4F5C")[None,None]*n[...,None]; alb=np.where(sp[...,None],alb*1.25,alb)
        h=0.05*n; rough=0.5+0.05*n; metal=np.zeros_like(h); ao=np.ones_like(h)*0.95; nstr=2
    elif kind=="Gold":   # Art #10 trim #E8B53A metallic 1
        n=noise(N,16,18); h=0.03*n; alb=hx("#E8B53A")[None,None]*(0.95+0.08*n[...,None]); rough=0.28+0.05*n; metal=np.ones_like(h); ao=np.ones_like(h); nstr=1
    else:               # Steel: 1 m plates, seams + rivets, brushed
        pp=int(1.0*ppm); lx=x%pp; ly=y%pp; edge=np.minimum(np.minimum(lx,pp-1-lx),np.minimum(ly,pp-1-ly))
        h=np.clip(edge/3,0,1); rv=((np.minimum(lx,pp-1-lx)-12)**2+(np.minimum(ly,pp-1-ly)%64-32)**2)<36
        h=h+0.6*rv; brushed=noise(N,2,4)[:, :]*0.06
        alb=hx("#AEB6C2")[None,None]*(0.97+brushed)[...,None]; alb=np.where((h<0.3)[...,None],hx("#7E8794"),alb)
        rough=0.3+0.05*noise(N,8,5); metal=np.ones_like(h); ao=0.65+0.35*np.clip(h,0,1); nstr=5
    save(alb,f"{out}/T_{kind}_A.png"); save(normal_from_height(h.astype(float),nstr),f"{out}/T_{kind}_N.png")
    save(np.dstack([ao,rough,metal]),f"{out}/T_{kind}_ORM.png")
def inner(kind,out,N=256):
    base={"Wood":"#C68A52","Brick":"#B0533D","Steel":"#949CA8","Glass":"#D8F6FF","Stone":"#A8916A"}[kind]
    n=noise(N,8,7)*0.6+noise(N,32,8)*0.4; alb=hx(base)[None,None]*(0.82+0.25*n[...,None])
    save(alb,f"{out}/T_{kind}_Inner_A.png")
def detail(out,N=256):
    for k,c,s in (("Grass","#7ED957",4),("Rock","#C98A4B",16),("Sand","#FFE7A8",2)):
        n=noise(N,s,hash(k)%99); save(hx(c)[None,None]*(0.85+0.3*n[...,None]),f"{out}/T_Detail{k}_A.png")
        save(normal_from_height(n,3.0),f"{out}/T_Detail{k}_N.png")
if __name__=="__main__":
    cmd=sys.argv[1]
    if cmd=="sheet": sheet(sys.argv[2],sys.argv[3])
    elif cmd=="inner": inner(sys.argv[2],sys.argv[3])
    elif cmd=="detail": detail(sys.argv[2])
