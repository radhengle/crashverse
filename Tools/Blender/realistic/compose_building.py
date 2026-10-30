"""2x2 sheet from render_building.py outputs. python3 compose_building.py <cfg> <out_name> "<title>" "<cap1>" "<cap2>" "<cap3>" "<cap4>" """
from PIL import Image, ImageDraw, ImageFont
import os,sys
O="/workspace/crashverse_previews/realistic_buildings"; cfg,out,title=sys.argv[1:4]; caps=sys.argv[4:8]
def font(s):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf","/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f,s)
    return ImageFont.load_default()
ims=[Image.open(f"{O}/{cfg}_{n}.png").convert("RGB") for n in ("01_hero","02_gameplay","03_exploded","04_destruction")]; w,h=ims[0].size; g=8
S=Image.new("RGB",(2*w+g,2*h+g+56),(14,20,40)); d=ImageDraw.Draw(S); d.text((16,12),title,fill=(245,196,81),font=font(30))
for i,(im,c) in enumerate(zip(ims,caps)):
    x,y=(i%2)*(w+g),56+(i//2)*(h+g); S.paste(im,(x,y)); d.rectangle((x,y,x+w,y+38),fill=(14,20,40)); d.text((x+12,y+7),c,fill=(245,196,81),font=font(21))
S.save(f"{O}/{out}"); print(S.size)
