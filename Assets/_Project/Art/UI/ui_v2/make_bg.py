"""Canva PORTRAIT_1_2 render -> 1080x2340: scale to 2340 high, centre-crop width, mild unsharp. Reports detail density in safe bands."""
import sys, numpy as np, cv2
from PIL import Image, ImageFilter
def make(src, dst, xshift=0):
    im=Image.open(src).convert('RGB')
    w,h=im.size; s=2340/h; W=round(w*s)
    im=im.resize((W,2340),Image.LANCZOS)
    x=(W-1080)//2+xshift
    im=im.crop((x,0,x+1080,2340)).filter(ImageFilter.UnsharpMask(radius=2,percent=60,threshold=2))
    im.save(dst)
    g=cv2.cvtColor(np.array(im),cv2.COLOR_RGB2GRAY); e=cv2.Canny(g,60,160)
    print(dst, 'edge density top158=%.3f mid=%.3f bottom72=%.3f'%(e[:158].mean()/255, e[158:-72].mean()/255, e[-72:].mean()/255))
if __name__=='__main__': make(sys.argv[1],sys.argv[2])
