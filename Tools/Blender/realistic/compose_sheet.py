"""One sheet for the whole realistic v1 set (usage cap): realism v1.1 gameplay view + 14-piece lineup + assembled L3/DB + destruction."""
from PIL import Image, ImageDraw, ImageFont
import os
O="/workspace/crashverse_previews/realistic_v1"; G="/workspace/crashverse_previews/realism_test_v1_1/04_gameplay_camera.png"
def font(s):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf","/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f,s)
    return ImageFont.load_default()
P=[(G,"Realism v1.1: townhouse, oak/pine/bush, denser grass (gameplay camera, AgX, 1024 maps)"),
   (f"{O}/01_lineup_pieces.png","14 realistic pieces for L1-10 + Daily Boss, most-used first (kit v3.1 geometry, graded CC0)"),
   (f"{O}/02_L3_bakery_DB_golem.png","Assembled from layout blocks: L3 Brick Bakery | DB Ironjaw Golem (granite = held island)"),
   (f"{O}/03_destruction.png","Break-apart: brick wall, wood/steel blocks, barrel, roof (UCX/JNT/ANCHOR fragment sets)")]
W=1600; ims=[]
for p,c in P:
    im=Image.open(p).convert("RGB")
    if "lineup" in p: im=im.crop((0,int(im.height*.28),im.width,int(im.height*.78)))   # trim empty sky/grass, no re-render
    im=im.resize((W,int(im.height*W/im.width)),Image.LANCZOS); d=ImageDraw.Draw(im)
    d.rectangle((0,0,W,44),fill=(14,20,40)); d.text((14,8),c,fill=(245,196,81),font=font(24)); ims.append(im)
S=Image.new("RGB",(W,sum(i.height for i in ims)+8*len(ims)),(14,20,40)); y=0
for i in ims: S.paste(i,(0,y)); y+=i.height+8
S.save(f"{O}/sheet.png"); os.makedirs(os.path.dirname(G),exist_ok=True); S.save(os.path.join(os.path.dirname(G),"sheet.png")); print(S.size)
