"""Avatars: clean geometric circle alpha (teal hair is too close to the green key, so no chroma key inside the ring).
Badges: re-cut with one common scale so bronze/silver/gold read as a sized family."""
import sys, numpy as np, cv2
from PIL import Image, ImageDraw
sys.path.insert(0,'.'); from key import key_green
src=np.array(Image.open('src/avatar_sheet.png').convert('RGB'))
H,W,_=src.shape
boxes=[(48,48,569,576),(647,48,569,576),(48,652,569,578),(647,652,569,578)]
SS=4
for i,(x,y,w,h) in enumerate(boxes):
    cx,cy=x+w/2,y+h/2; r=min(w,h)/2-3
    s=int(2*r)+8; x0,y0=int(cx-s/2),int(cy-s/2)
    crop=Image.fromarray(src[y0:y0+s,x0:x0+s])
    m=Image.new('L',(s*SS,s*SS),0); d=ImageDraw.Draw(m)
    c=s*SS/2; d.ellipse((c-r*SS,c-r*SS,c+r*SS,c+r*SS),fill=255)
    m=m.resize((s,s),Image.LANCZOS)
    crop.putalpha(m)
    pad=int(s/(1-0.06)); cv=Image.new('RGBA',(pad,pad),(0,0,0,0)); cv.paste(crop,((pad-s)//2,(pad-s)//2))
    cv.resize((256,256),Image.LANCZOS).save(f'avatar_0{i+1}.png'); print('avatar',i+1)
# badges common scale
rgb=np.array(Image.open('src/badge_sheet.png').convert('RGB'))
col,al=key_green(rgb); al=cv2.morphologyEx(al,cv2.MORPH_OPEN,np.ones((3,3),np.uint8))
rgba=np.dstack([col,al])
bb=[(37,167,515,571,'badge_01_bronze'),(582,167,516,571,'badge_02_silver'),(1128,136,516,602,'badge_03_gold')]
S=int(602/(1-0.08))
bottom=max(y+h for x,y,w,h,_ in bb)
for x,y,w,h,n in bb:
    crop=Image.fromarray(rgba[y:y+h,x:x+w]); cv=Image.new('RGBA',(S,S),(0,0,0,0))
    # align bottoms so ribbons sit on a common baseline
    cv.paste(crop,((S-w)//2, (S-602)//2 + (602-h) - (bottom-(y+h))))
    cv.resize((256,256),Image.LANCZOS).save(n+'.png'); print(n)
