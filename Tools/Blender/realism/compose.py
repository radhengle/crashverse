"""Side-by-side (v2 vs realism test), greyscale gameplay check, maps @1024 crop sheet, contact sheet."""
from PIL import Image, ImageDraw, ImageFont, ImageOps
import os, glob
O="/workspace/crashverse_previews/realism_test_v1"; V2="/workspace/crashverse_previews/hero_buildings_v2"
REAL="/workspace/wt/3d-modeler-v3/Assets/_Project/Models/Realistic"
def font(s):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf","/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"):
        if os.path.exists(p): return ImageFont.truetype(p,s)
    return ImageFont.load_default()
def label(im,t,s=34):
    d=ImageDraw.Draw(im); f=font(s); w=d.textlength(t,font=f); d.rectangle((0,0,w+28,s+20),fill=(11,21,48)); d.text((14,8),t,font=f,fill=(255,255,255)); return im
def fit(im,h): return im.resize((int(im.width*h/im.height),h),Image.LANCZOS)
H=900
a=fit(Image.open(f"{V2}/Bld_Townhouse.png").convert("RGB"),H); b=fit(Image.open(f"{O}/01_hero_3q.png").convert("RGB"),H)
sb=Image.new("RGB",(a.width+b.width+12,H),(255,255,255)); sb.paste(label(a,"v2 (stylized, baked atlas)"),(0,0)); sb.paste(label(b,"Realism test v1 (CC0 PBR, 1024 maps)"),(a.width+12,0))
sb.save(f"{O}/05_side_by_side_v2_vs_realism.png")
g=Image.open(f"{O}/04_gameplay_camera.png").convert("RGB")
gs=Image.open(f"{O}/04c_gameplay_camera_greyscale.png").convert("RGB") if os.path.exists(f"{O}/04c_gameplay_camera_greyscale.png") else ImageOps.grayscale(g).convert("RGB"); w=g.width//2; h=g.height//2
c=Image.new("RGB",(g.width+gs.width+12,g.height),(255,255,255)); c.paste(label(g.copy(),"Gameplay camera (AgX)"),(0,0)); c.paste(label(gs,"Greyscale readability check"),(g.width+12,0))
c.save(f"{O}/06_gameplay_greyscale_check.png")
if not os.path.exists(f"{O}/04c_gameplay_camera_greyscale.png"): gs.save(f"{O}/04c_gameplay_camera_greyscale.png")
# maps at the Android cap (1024): whole atlas + 1:1 crop
tiles=[]
for p in [f"{REAL}/Buildings/Hero/Bld_Townhouse/Textures/T_Bld_Townhouse_{k}.png" for k in ("A","N","ORM")]+[f"{REAL}/Foliage/Oak/Textures/T_Oak_A.png",f"{REAL}/Foliage/Pine/Textures/T_Pine_A.png",f"{REAL}/Props/Props_Village/Textures/T_Props_Village_A.png"]:
    im=Image.open(p); im=im.convert("RGBA"); bg=Image.new("RGBA",im.size,(255,255,255,255)); bg.alpha_composite(im); im=bg.convert("RGB").resize((1024,1024),Image.LANCZOS)
    t=Image.new("RGB",(512+384+8,512),(255,255,255)); t.paste(im.resize((512,512)),(0,0)); t.paste(im.crop((60,60,444,444)),(520,0)); tiles.append(label(t,os.path.basename(p)+" @1024 (+1:1 crop)",18))
sh=Image.new("RGB",(tiles[0].width*2+8,(512+8)*3),(255,255,255))
for i,t in enumerate(tiles): sh.paste(t,((i%2)*(t.width+8),(i//2)*520))
sh.save(f"{O}/07_maps_at_1024.png")
shots=[p for p in sorted(glob.glob(f"{O}/0*.png")) if "sheet" not in p]
th=[label(fit(Image.open(p).convert("RGB"),360),os.path.basename(p)[:-4],16) for p in shots]
W=max(t.width for t in th); cols=3; rows=(len(th)+cols-1)//cols
S=Image.new("RGB",(cols*(W+8),rows*368),(255,255,255))
for i,t in enumerate(th): S.paste(t,((i%cols)*(W+8),(i//cols)*368))
S.save(f"{O}/sheet.png"); print("composed",len(shots))
