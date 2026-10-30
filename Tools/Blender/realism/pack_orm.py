"""Scripted map packing (kept separate so the layout can change later).
  python3 pack_orm.py orm  <ao> <rough> <metal|0|1> <out.png>          -> ORM  (R=AO, G=roughness, B=metal)  [current]
  python3 pack_orm.py mask <in_ORM.png> <out.png>                        -> Unity HDRP-style mask map (R=metal, G=AO, B=detail 0, A=smoothness)
  python3 pack_orm.py all-mask <Realistic root>                          -> writes *_MASK.png next to every *_ORM.png"""
import sys, glob, numpy as np
from PIL import Image
def L(p): return np.asarray(Image.open(p).convert("L")).astype(np.float32)/255
def orm(ao,rough,metal,out,ao_strength=.5):
    a=L(ao); a=1-(1-a)*ao_strength; r=L(rough); m=np.full_like(a,float(metal)) if metal in ("0","1") else L(metal)
    Image.fromarray((np.stack([a,r,m],-1)*255+.5).astype(np.uint8)).save(out)
def mask(src,out):
    o=np.asarray(Image.open(src).convert("RGB")).astype(np.float32)/255
    m=np.stack([o[...,2],o[...,0],np.zeros_like(o[...,0]),1-o[...,1]],-1)
    Image.fromarray((m*255+.5).astype(np.uint8),"RGBA").save(out)
if __name__=="__main__":
    c=sys.argv[1]
    if c=="orm": orm(*sys.argv[2:6])
    elif c=="mask": mask(sys.argv[2],sys.argv[3])
    elif c=="all-mask":
        for p in glob.glob(sys.argv[2]+"/**/*_ORM.png",recursive=True): mask(p,p.replace("_ORM.png","_MASK.png")); print("mask",p)
