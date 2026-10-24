# system python: texture-space detail for the island bake (grass light/dark variation, cliff grain, normal from height)
import sys,numpy as np; from PIL import Image, ImageFilter
sys.path.insert(0,__import__('os').path.dirname(__file__)); from textures import noise, normal_from_height
d=sys.argv[1]; A=np.asarray(Image.open(f"{d}/T_Island_A.png").convert("RGB"),float)/255; N=A.shape[0]
hx=lambda h: np.array([int(h[i:i+2],16)/255 for i in (1,3,5)])
g=A[...,1]-np.maximum(A[...,0],A[...,2]); grass=np.clip((g-0.12)*6,0,1)[...,None]
n1=noise(N,96,4); n2=noise(N,24,9); v=np.clip(0.55*n1+0.45*n2,0,1)[...,None]
gv=hx("#4CA83A")*(1-v)+hx("#A6F07A")*v
A2=A*(1-grass*0.75)+gv*grass*0.75
fine=noise(N,4,2); A2*=(0.9+0.2*fine)[...,None]*(1-grass*0.5)+grass*0.5*1.0
Image.fromarray((np.clip(A2,0,1)*255).astype(np.uint8)).save(f"{d}/T_Island_A.png")
lum=np.asarray(Image.fromarray((A.mean(2)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)),float)/255
h=lum*0.6+0.25*noise(N,16,5)+0.15*fine
Image.fromarray((normal_from_height(h,18)*255).astype(np.uint8)).save(f"{d}/T_Island_N.png")
print("island_post ok",N)
