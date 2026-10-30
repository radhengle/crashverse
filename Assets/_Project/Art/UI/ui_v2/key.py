"""Chroma-key Canva icon sheets (flat green bg) and cut each object to a 256x256 transparent PNG."""
import sys, numpy as np, cv2
from PIL import Image

def key_green(rgb):
    a = rgb.astype(np.float32)
    H,W,_=a.shape
    # sample actual bg colour from the 4 corners
    pts=np.concatenate([a[:8,:8].reshape(-1,3),a[:8,-8:].reshape(-1,3),a[-8:,:8].reshape(-1,3),a[-8:,-8:].reshape(-1,3)])
    bg=np.median(pts,0)
    d=np.sqrt(((a-bg)**2).sum(-1))
    alpha=np.clip((d-35)/(215-35),0,1)
    # extra guard: strongly green-screen pixels are bg
    r,g,b=a[...,0],a[...,1],a[...,2]
    alpha=np.where((g-np.maximum(r,b))>150,0,alpha)
    # un-premultiply the bg out of edge pixels
    am=np.maximum(alpha,0.05)[...,None]
    col=(a-(1-am)*bg)/am
    col=np.clip(col,0,255)
    # residual despill
    r,g,b=col[...,0],col[...,1],col[...,2]
    g=np.minimum(g,np.maximum(r,b)+np.where(alpha>0.95,255,25))
    col=np.stack([r,g,b],-1)
    # solid interior: anything well inside the object (eroded hard mask) is fully opaque with original colour
    hard=(alpha>0.5).astype(np.uint8)
    inner=cv2.erode(hard,np.ones((7,7),np.uint8))>0
    alpha=np.where(inner,1.0,alpha)
    col=np.where(inner[...,None],a,col)
    return col.clip(0,255).astype(np.uint8),(alpha*255).astype(np.uint8)

def components(alpha, min_area=4000):
    m = (alpha>128).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((25,25),np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m)
    boxes = [tuple(st[i,:4]) for i in range(1,n) if st[i,4]>=min_area]
    # sort rows then columns
    boxes.sort(key=lambda b:(round((b[1]+b[3]/2)/ (alpha.shape[0]/6)), b[0]))
    return boxes, lab

def cut(path, names, out_dir, margin=0.06, min_area=4000, grid=None):
    rgb = np.array(Image.open(path).convert('RGB'))
    col, al = key_green(rgb)
    # clean isolated speckles in alpha
    al = cv2.morphologyEx(al, cv2.MORPH_OPEN, np.ones((3,3),np.uint8))
    rgba = np.dstack([col, al])
    if grid:  # explicit grid split: (rows, cols)
        R,C = grid; H,W = al.shape; boxes=[]
        for i in range(R):
            for j in range(C):
                sl=(slice(i*H//R,(i+1)*H//R), slice(j*W//C,(j+1)*W//C))
                sub = al[sl].copy()
                m=(sub>8).astype(np.uint8)
                n,lab,st,_=cv2.connectedComponentsWithStats(m)
                big=1+np.argmax(st[1:,4])
                near=cv2.dilate((lab==big).astype(np.uint8),np.ones((41,41),np.uint8))
                for k in range(1,n):
                    if k!=big and not (near[lab==k]).any(): sub[lab==k]=0
                al[sl]=sub; rgba[...,3]=al
                ys,xs = np.where(sub>8)
                boxes.append((j*W//C+xs.min(), i*H//R+ys.min(), xs.max()-xs.min()+1, ys.max()-ys.min()+1))
    else:
        boxes,_ = components(al, min_area)
    assert len(boxes)>=len(names), (len(boxes), boxes)
    for name, (x,y,w,h) in zip(names, boxes):
        if name is None: continue
        crop = Image.fromarray(rgba[y:y+h, x:x+w])
        s = max(w,h); s2 = int(s/(1-2*margin))
        canvas = Image.new('RGBA',(s2,s2),(0,0,0,0))
        canvas.paste(crop, ((s2-w)//2,(s2-h)//2))
        canvas = canvas.resize((256,256), Image.LANCZOS)
        canvas.save(f'{out_dir}/{name}.png')
        print('saved', name, (x,y,w,h))
