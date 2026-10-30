"""2x2 sheet for the modular Bld_Townhouse (render_townhouse.py outputs)."""
from PIL import Image, ImageDraw, ImageFont
import os
O="/workspace/crashverse_previews/realistic_buildings"
def font(s):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf","/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f,s)
    return ImageFont.load_default()
P=[("01_hero","Hero 3/4: intact LOD0, 19,692 tris, 23 pieces / 11 modules, 1024 tiling maps"),
   ("02_gameplay","Gameplay camera"),
   ("03_exploded","Modules: Base, ShopFront, Wall_L/R/Back, Floor, UpperStorey, Roof_L/R, Chimney, Annex"),
   ("04_destruction","Destruction: shop front, upper front, balcony, street roof slopes as fractured sets")]
ims=[Image.open(f"{O}/townhouse_{n}.png").convert("RGB") for n,_ in P]; w,h=ims[0].size; g=8
S=Image.new("RGB",(2*w+g,2*h+g+56),(14,20,40)); d=ImageDraw.Draw(S)
d.text((16,12),"Bld_Townhouse realistic v1, modular + destructible (Kit_RealHouse_*)",fill=(245,196,81),font=font(30))
for i,(im,(n,c)) in enumerate(zip(ims,P)):
    x,y=(i%2)*(w+g),56+(i//2)*(h+g); S.paste(im,(x,y)); d.rectangle((x,y,x+w,y+38),fill=(14,20,40)); d.text((x+12,y+7),c,fill=(245,196,81),font=font(21))
S.save(f"{O}/townhouse_sheet.png"); print(S.size)
