"""Leaf-card atlases (one per species, 2048 authored -> 1024 on Android) composited from CC0 ambientCG leaf scans
(LeafSet016 oak leaves, LeafSet019 conifer sprigs) + graded CC0 bark strip. Alpha-clipped (A.alpha), N, ORM.
Writes Realistic/Foliage/<Asset>/Textures/T_<Asset>_{A,N,ORM}.png + cards.json (UV rects)."""
import numpy as np, json, os, math, colorsys
from PIL import Image, ImageDraw, ImageFilter
from grade import OUT as MAT, ROOT, rgb2hsv, hsv2rgb
ACG="/workspace/cc0/ambientcg"; R=ROOT+"/Assets/_Project/Models/Realistic/Foliage"
rng=np.random.default_rng(3)
def f32(im): return np.asarray(im).astype(np.float32)/255
def sprites(name,grid):
    c=Image.open(f"{ACG}/{name}/{name}_2K-PNG_Color.png").convert("RGB"); a=Image.open(f"{ACG}/{name}/{name}_2K-PNG_Opacity.png").convert("L")
    n=Image.open(f"{ACG}/{name}/{name}_2K-PNG_NormalGL.png").convert("RGB"); r=Image.open(f"{ACG}/{name}/{name}_2K-PNG_Roughness.png").convert("L")
    W,H=c.size; out=[]
    for (x0,y0,x1,y1) in grid(W,H):
        box=(x0,y0,x1,y1); bb=a.crop(box).getbbox()
        if not bb: continue
        bb=(x0+bb[0],y0+bb[1],x0+bb[2],y0+bb[3])
        out.append([im.crop(bb) for im in (c,a,n,r)])
    return out
def grade_leaf(C,target_hex,var=.08):
    t=np.array([int(target_hex[i:i+2],16) for i in (1,3,5)])/255; th,ts,tv=colorsys.rgb_to_hsv(*t)
    h,s,v=rgb2hsv(C); m=v>0.05
    h=np.full_like(h,th)+(h-np.median(h[m]))*.15
    s=np.clip(ts*(1+(s/max(s[m].mean(),1e-3)-1)*.5),0,1)
    v=np.clip(tv*(1+(v/max(v[m].mean(),1e-3)-1)*.6),0,1)
    return hsv2rgb(h%1,s,v)
def paste(canvas,spr,cx,cy,scale,ang,shade,tint_hex,ao):
    C,A,N,Rg=spr; w=max(4,int(C.size[0]*scale)); h=max(4,int(C.size[1]*scale))
    C,A,N,Rg=[im.resize((w,h),Image.LANCZOS) for im in (C,A,N,Rg)]
    C,A,N,Rg=[im.rotate(ang,Image.BICUBIC,expand=True) for im in (C,A,N,Rg)]
    c=grade_leaf(f32(C),tint_hex)*shade; a=f32(A); n=f32(N)*2-1; rr=f32(Rg)
    th=math.radians(ang); ca,sa=math.cos(th),math.sin(th)   # PIL rotates CCW; rotate xy of the normal with it (y up)
    nx=n[...,0]*ca-n[...,1]*sa; ny=n[...,0]*sa+n[...,1]*ca; n[...,0]=nx; n[...,1]=ny
    H,W=canvas["a"].shape; x0=int(cx-c.shape[1]/2); y0=int(cy-c.shape[0]/2)
    xs,ys=max(0,x0),max(0,y0); xe,ye=min(W,x0+c.shape[1]),min(H,y0+c.shape[0])
    if xe<=xs or ye<=ys: return
    sl=(slice(ys,ye),slice(xs,xe)); ss=(slice(ys-y0,ye-y0),slice(xs-x0,xe-x0))
    al=a[ss][...,None]; al=np.where(al>.5,1.0,al*.0)                  # crisp: clip the scan alpha
    for k,src in (("c",c[ss]),("n",n[ss])): canvas[k][sl]=canvas[k][sl]*(1-al)+src*al
    canvas["r"][sl]=canvas["r"][sl]*(1-al[...,0])+rr[ss]*al[...,0]
    canvas["ao"][sl]=canvas["ao"][sl]*(1-al[...,0])+ao*al[...,0]
    canvas["a"][sl]=np.maximum(canvas["a"][sl],al[...,0])
def new_canvas(w,h):
    return {"c":np.zeros((h,w,3),np.float32),"n":np.dstack([np.zeros((h,w)),np.zeros((h,w)),np.ones((h,w))]).astype(np.float32),
            "r":np.full((h,w),.7,np.float32),"ao":np.ones((h,w),np.float32),"a":np.zeros((h,w),np.float32)}
def stem(canvas,pts,w0,w1,col=(0x6B,0x4A,0x2E)):
    H,W=canvas["a"].shape; im=Image.new("L",(W,H),0); d=ImageDraw.Draw(im)
    for i in range(len(pts)-1):
        t=i/(len(pts)-1); d.line([pts[i],pts[i+1]],fill=255,width=max(1,int(w0+(w1-w0)*t)))
    m=f32(im)[...,None]
    canvas["c"]=canvas["c"]*(1-m)+np.array(col)/255*m; canvas["a"]=np.maximum(canvas["a"],m[...,0])
    canvas["n"]=canvas["n"]*(1-m)+np.array([0,0,1.])*m; canvas["r"]=canvas["r"]*(1-m[...,0])+.85*m[...,0]
def curve(p0,p1,bend,n=12):
    p0,p1=np.array(p0,float),np.array(p1,float); d=p1-p0; perp=np.array([-d[1],d[0]])
    return [tuple(p0+d*t+perp*bend*math.sin(math.pi*t)) for t in np.linspace(0,1,n)]
GREENS=["#7BD34A","#6CC843","#5FBF3A","#4FAE33","#3E9A2B","#357F33"]
def oak_cluster(w,h,leaves,density=1.0,leaf_scale=.2,round_=False):
    """leaf clump: twigs from a base point fan out to leaf positions filling an ellipse (back->front painting)"""
    cv=new_canvas(w,h); base=np.array([w/2,h*.93]); cx,cy=w/2,h*.47; rx,ry=w*.36,h*.36
    n=int(42*density*(1.25 if round_ else 1)); pts=[]
    while len(pts)<n:
        q=rng.uniform(-1,1,2)
        if (q**2).sum()<1: pts.append(np.array([cx+q[0]*rx,cy+q[1]*ry]))
    # twigs to a subset of points (visible woody structure)
    for p in pts[::5]:
        stem(cv,curve(tuple(base),tuple(p),rng.uniform(-.12,.12),8),max(2,w//110),1)
    stem(cv,curve(tuple(base),(cx,cy+ry*.3),.03,8),max(3,w//70),2)
    items=[(rng.uniform(0,1),p,math.degrees(math.atan2(-(p-base)[1],(p-base)[0]))-90+rng.uniform(-50,50)) for p in pts]
    items.sort(key=lambda x:x[0])
    for depth,p,a in items:
        spr=leaves[rng.integers(len(leaves))]; sc=leaf_scale*w/spr[0].size[1]*rng.uniform(.75,1.15)
        r_=np.hypot((p[0]-cx)/rx,(p[1]-cy)/ry)
        g=GREENS[min(5,max(0,int((1-depth)*3.2+(1-r_)*1.8+rng.uniform(0,1))))]
        paste(cv,spr,p[0],p[1],sc,a,1.0,g,.5+.3*depth+.2*r_)
    return cv
def pine_card(w,h,sprigs):
    """horizontal branch card: woody axis left->right with conifer sprigs fanning on both sides"""
    cv=new_canvas(w,h); y=h*.5; pts=curve((0,y),(w*.98,y+h*.06),.02); stem(cv,pts,max(3,h//40),1)
    items=[]
    for t in np.linspace(.0,.95,15):
        p=np.array(pts[int(t*(len(pts)-1))])
        for side in (-1,1): items.append((rng.uniform(0,1),p,side*rng.uniform(25,55)+rng.uniform(-8,8),t))
    items.sort(key=lambda x:x[0])
    for depth,p,a,t in items:
        spr=sprigs[rng.integers(len(sprigs))]; sc=(h*.62*(1.05-.45*t))/spr[0].size[0]*rng.uniform(.8,1.1)
        # sprig image points right (tip on the right): place so its base sits on the axis
        ang=a; L=spr[0].size[0]*sc; off=np.array([math.cos(math.radians(ang)),-math.sin(math.radians(ang))])*L*.45
        g=["#4FAE33","#45A230","#3E9A2B","#357F33"][min(3,int((1-depth)*3+rng.uniform(0,1)))]
        paste(cv,spr,p[0]+off[0],p[1]+off[1],sc,ang,1.0,g,.55+.45*depth)
    # tip sprig
    spr=sprigs[0]; sc=h*.5/spr[0].size[0]; paste(cv,spr,w*.9,y+h*.05,sc,0,1,"#5FBF3A",1)
    return cv
def pine_top(w,h,sprigs):
    cv=new_canvas(w,h); stem(cv,curve((w/2,h),(w/2,h*.02),.0),max(3,w//30),1)
    for t in np.linspace(.15,.95,12):
        y=h*(1-t)
        for side in (-1,1):
            spr=sprigs[rng.integers(len(sprigs))]; sc=(w*.55*(1.1-.8*t))/spr[0].size[0]
            ang=90+side*(55+rng.uniform(-10,10)) if False else (side>0 and 35+rng.uniform(-10,10) or 145+rng.uniform(-10,10))
            L=spr[0].size[0]*sc; off=np.array([math.cos(math.radians(ang)),-math.sin(math.radians(ang))])*L*.45
            paste(cv,spr,w/2+off[0],y+off[1],sc,ang,1,["#5FBF3A","#4FAE33","#3E9A2B"][rng.integers(3)],.8+.2*t)
    spr=sprigs[0]; paste(cv,spr,w/2,h*.12,w*.35/spr[0].size[0],90,1,"#6CC843",1)
    return cv
def bleed(c,a,r=6):
    """push leaf colour into transparent texels so mips / bilinear never pull in black"""
    m=(a>.5).astype(np.float32); acc=c*m[...,None]; wsum=m.copy()
    for rad in (2,4,8,16,32):
        im=lambda x: np.asarray(Image.fromarray((np.clip(x,0,1)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(rad))).astype(np.float32)/255
        bl=np.stack([im(acc[...,i]) for i in range(3)],-1); ws=im(wsum)
        fill=bl/np.maximum(ws[...,None],1e-4); c=np.where(m[...,None]>0,c,np.where(ws[...,None]>1e-3,fill,c))
        m=np.maximum(m,(ws>1e-3).astype(np.float32)); acc=c*m[...,None]; wsum=m.copy()
    return c
def assemble(asset,size,bark,bark_rect,bark_m,cards):
    A=np.zeros((size,size,4),np.float32); N=np.zeros((size,size,3),np.float32); N[...]=(.5,.5,1); O=np.zeros((size,size,3),np.float32); O[...]=(1,.75,0)
    meta={"size":size,"cards":{}}
    if bark:
        x,y,w,h=bark_rect; g=16
        from atlas import cell
        a,n,o=cell(bark,w-2*g,h-2*g,bark_m)
        A[y:y+h,x:x+w,:3]=np.pad(a,((g,g),(g,g),(0,0)),mode="wrap"); A[y:y+h,x:x+w,3]=1
        N[y:y+h,x:x+w]=np.pad(n,((g,g),(g,g),(0,0)),mode="wrap"); O[y:y+h,x:x+w]=np.pad(o,((g,g),(g,g),(0,0)),mode="wrap")
        meta["bark"]={"uv":[(x+g)/size,1-(y+h-g)/size,(x+w-g)/size,1-(y+g)/size],"m":list(bark_m)}
    for name,((x,y,w,h),cv) in cards.items():
        c=bleed(cv["c"],cv["a"]); A[y:y+h,x:x+w,:3]=c; A[y:y+h,x:x+w,3]=cv["a"]
        n=cv["n"]; n=n/np.maximum(np.linalg.norm(n,axis=-1,keepdims=True),1e-4); N[y:y+h,x:x+w]=n*.5+.5
        O[y:y+h,x:x+w]=np.stack([1-(1-cv["ao"])*.5,np.clip(cv["r"]*.4+.45,.6,.8),np.zeros_like(cv["r"])],-1)
        # visible-pixel bbox -> card UV + aspect (so geometry matches the art, less overdraw)
        ys,xs=np.nonzero(cv["a"]>.5); bx=(xs.min(),ys.min(),xs.max()+1,ys.max()+1) if len(xs) else (0,0,w,h)
        meta["cards"][name]={"uv":[(x+bx[0])/size,1-(y+bx[3])/size,(x+bx[2])/size,1-(y+bx[1])/size],
                             "aspect":(bx[2]-bx[0])/max(1,bx[3]-bx[1]),"fill":float((cv["a"]>.5).mean())}
    d=f"{R}/{asset}/Textures"; os.makedirs(d,exist_ok=True)
    Image.fromarray((np.clip(A,0,1)*255+.5).astype(np.uint8),"RGBA").save(f"{d}/T_{asset}_A.png")
    Image.fromarray((np.clip(N,0,1)*255+.5).astype(np.uint8)).save(f"{d}/T_{asset}_N.png")
    Image.fromarray((np.clip(O,0,1)*255+.5).astype(np.uint8)).save(f"{d}/T_{asset}_ORM.png")
    json.dump(meta,open(f"{R}/{asset}/cards.json","w"),indent=1)
    a=A[...,:3][A[...,3]>.5]; avg=a.mean(0)*255; print(asset,"leaf/bark visible avg",'#'+''.join(f'{int(v):02X}' for v in avg),"cards",len(cards))
if __name__=="__main__":
    oakL=sprites("LeafSet016",lambda W,H:[(x*W//3,y*H//2,(x+1)*W//3,(y+1)*H//2) for y in range(2) for x in range(3)])
    pineS=sprites("LeafSet019",lambda W,H:[(0,0,W,int(H*.3)),(0,int(H*.3),W,int(H*.62)),(0,int(H*.62),W,H)])
    # flip sprigs so the tip points right->left consistent: LeafSet019 tips point right already
    S=2048
    assemble("Oak",S,"OakBark",(0,0,512,2048),(1.0,4.0),{
        "C0":((512,0,768,768),oak_cluster(768,768,oakL)),"C1":((1280,0,768,768),oak_cluster(768,768,oakL,1.1)),
        "C2":((512,768,768,768),oak_cluster(768,768,oakL,.9)),"C3":((1280,768,768,768),oak_cluster(768,768,oakL,1.2,.22)),
        "S0":((512,1536,512,512),oak_cluster(512,512,oakL,.8,.26)),"S1":((1024,1536,512,512),oak_cluster(512,512,oakL,.8,.26)),
        "S2":((1536,1536,512,512),oak_cluster(512,512,oakL,.9,.24,True))})
    assemble("Pine",S,"PineBark",(0,0,512,2048),(1.0,4.0),{
        "B0":((512,0,1536,512),pine_card(1536,512,pineS)),"B1":((512,512,1536,512),pine_card(1536,512,pineS)),
        "B2":((512,1024,1024,512),pine_card(1024,512,pineS)),"B3":((512,1536,1024,512),pine_card(1024,512,pineS)),
        "T0":((1536,1024,512,1024),pine_top(512,1024,pineS))})
    assemble("Bush",S,"OakBark",(0,0,256,2048),(.5,4.0),{
        "R0":((256,0,896,896),oak_cluster(896,896,oakL,1.4,.15,True)),"R1":((1152,0,896,896),oak_cluster(896,896,oakL,1.5,.14,True)),
        "R2":((256,896,896,896),oak_cluster(896,896,oakL,1.3,.16,True)),"R3":((1152,896,896,896),oak_cluster(896,896,oakL,1.6,.15,True)),
        "R4":((256,1792,896,256),oak_cluster(896,256,oakL,.7,.3,True)),"R5":((1152,1792,896,256),oak_cluster(896,256,oakL,.7,.3,True))})
