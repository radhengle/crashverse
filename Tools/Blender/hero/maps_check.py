"""Texture readability check at the Android cap: every hero atlas downscaled to 1024, shown whole (384 px) plus a 1:1
384 px crop of the 1024 map (densest region by local contrast). Also prints per-map contrast stats at 2048 vs 1024.
usage: python3 maps_check.py <out.png>"""
import sys,os,glob,numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT="/workspace/wt/3d-modeler-v3"; K=f"{ROOT}/Assets/_Project/Models/BuildingKit"; CAP=1024; S=384
out=sys.argv[1] if len(sys.argv)>1 else f"{ROOT}/models/previews/hero_buildings_v2/maps_1024_check.png"
rows=[]
for B in ("Keep","Tower","Gate","House","Props"):
    ims=[]
    for m in ("A","N","ORM"):
        p=f"{K}/Kit_{B}/textures/T_{B}_{m}.png"
        if not os.path.exists(p): continue
        im=Image.open(p).convert("RGB"); sm=im.resize((CAP,CAP),Image.LANCZOS)
        g=np.asarray(sm.convert("L"),np.float32); hi=np.asarray(sm.convert("L").filter(ImageFilter.FIND_EDGES),np.float32)
        best=(0,0,0)
        for y in range(0,CAP-S+1,64):
            for x in range(0,CAP-S+1,64):
                v=hi[y:y+S,x:x+S].mean()
                if v>best[0]: best=(v,x,y)
        _,x,y=best; ims.append((m,sm.resize((S,S),Image.LANCZOS),sm.crop((x,y,x+S,y+S))))
        a=np.asarray(im.convert("L"),np.float32); print(f"T_{B}_{m}: std 2048={a.std():.1f} 1024={g.std():.1f}  edge energy 1024={hi.mean():.2f}")
    rows.append((B,ims))
W=6*S+7*8; H=len(rows)*(S+30)+50; sheet=Image.new("RGB",(W,H),(24,26,32)); d=ImageDraw.Draw(sheet)
d.text((10,12),"Hero atlases at the 1024 Android cap: whole map | 1:1 crop of the 1024 map (A, N, ORM = AO/rough/metal)",fill=(240,240,240))
for r,(B,ims) in enumerate(rows):
    y=50+r*(S+30); d.text((10,y-2),f"T_{B}",fill=(255,210,120))
    for i,(m,whole,crop) in enumerate(ims):
        x=8+i*2*(S+8); sheet.paste(whole,(x,y+12)); sheet.paste(crop,(x+S+8,y+12)); d.text((x+4,y+16),m,fill=(255,255,255))
os.makedirs(os.path.dirname(out),exist_ok=True); sheet.save(out); print("WROTE",out)
