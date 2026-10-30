"""star_off.png: luminance-mapped slate-navy recolour of the Canva star_on (same silhouette and gloss)."""
import numpy as np
from PIL import Image
a=np.array(Image.open('star_on.png').convert('RGBA')).astype(np.float32)
lum=(0.299*a[...,0]+0.587*a[...,1]+0.114*a[...,2])/255.0
dark=np.array([28,38,70],np.float32); light=np.array([120,140,185],np.float32)
t=np.clip((lum-0.35)/0.6,0,1)[...,None]**1.2
Image.fromarray(np.dstack([dark*(1-t)+light*t,a[...,3]]).clip(0,255).astype(np.uint8)).save('star_off.png')
