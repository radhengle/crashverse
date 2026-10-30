"""Chest: the cyan seam glow is close to the green key, which left see-through holes. Close them and despill toward cyan."""
import sys, numpy as np, cv2
from PIL import Image
sys.path.insert(0,'.'); from key import key_green
rgb=np.array(Image.open('src/misc_sheet.png').convert('RGB'))
x,y,w,h=43,466,367,343
crop=rgb[y-10:y+h+10, x-10:x+w+10]
col,al=key_green(crop)
hard=(al>128).astype(np.uint8)
P=40; hp=np.pad(hard,P); closed=cv2.morphologyEx(hp,cv2.MORPH_CLOSE,np.ones((31,31),np.uint8))
# fill any remaining enclosed holes
ff=closed.copy(); m=np.zeros((ff.shape[0]+2,ff.shape[1]+2),np.uint8); cv2.floodFill(ff,m,(0,0),1)
holes=(ff==0)
fill=((closed>0)|holes)[P:-P,P:-P]
fill_in=cv2.erode(fill.astype(np.uint8),np.ones((5,5),np.uint8))>0
newly=fill_in&(al<250)
a=crop.astype(np.int32).copy()
# despill newly-solid pixels toward cyan: G <= B+15
a[...,1]=np.where(newly,np.minimum(a[...,1],a[...,2]+15),a[...,1])
col=np.where(newly[...,None],a,col).astype(np.uint8)
al=np.where(fill_in,255,al).astype(np.uint8)
rgba=np.dstack([col,al]); H,W=al.shape
s=int(max(H,W)/(1-0.08)); cv=Image.new('RGBA',(s,s),(0,0,0,0)); cv.paste(Image.fromarray(rgba),((s-W)//2,(s-H)//2))
cv.resize((256,256),Image.LANCZOS).save('chest.png'); print('newly filled',newly.sum())
