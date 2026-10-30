"""Colourway atlases derived from the graded T_RealHouse set (no new sources): striped awning cloth cells + recoloured timber/plaster.
python3 variant_atlas.py  -> Bld_Bakery/Textures/T_RealBakery_* (1024) and Kit_RealStall/Textures/T_RealStall_* (512) + cells.json"""
from PIL import Image, ImageChops
import json,os
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"..","..",".."))
B=ROOT+"/Assets/_Project/Models/Realistic/Buildings"; SRC=B+"/Bld_Townhouse"
cells=json.load(open(SRC+"/cells.json"))
def rect(c,W): u0,v0,u1,v1=cells["cells"][c]["uv"]; return (round(u0*W),round((1-v1)*W),round(u1*W),round((1-v0)*W))
def mul(im,r,rgb):
    reg=im.crop(r); col=Image.new("RGB",reg.size,tuple(int(255*x) for x in rgb)); im.paste(ImageChops.multiply(reg,col),r)
def stripes(A,N,O,r,c1,c2,n=12):
    """cloth: plaster grain (luminance) x alternating stripes along U; flat-ish normal from plaster; rough .85"""
    pr=rect("Plaster",A.width); x0,y0,x1,y1=r; w,h=x1-x0,y1-y0
    grain=A.crop((pr[0],pr[1],pr[0]+w,pr[1]+h)).convert("L").point(lambda v:int(200+55*v/255))
    st=Image.new("RGB",(w,h))
    for i in range(w): 
        col=c1 if int(i*n/w)%2==0 else c2
        for j in range(h): st.putpixel((i,j),col)
    A.paste(ImageChops.multiply(st,Image.merge("RGB",(grain,)*3)),r)
    N.paste(N.crop((pr[0],pr[1],pr[0]+w,pr[1]+h)),r)
    o=Image.new("RGB",(w,h),(235,215,0)); O.paste(o,r)
def make(asset,out,size,stripe_cells,recolour):
    os.makedirs(out+"/Textures",exist_ok=True)
    A,N,O=[Image.open(f"{SRC}/Textures/T_RealHouse_{k}.png").convert("RGB") for k in ("A","N","ORM")]
    cj=json.loads(json.dumps(cells))
    for cell,rgb in recolour.items(): mul(A,rect(cell,A.width),rgb)
    for slot,(name,c1,c2) in stripe_cells.items():
        stripes(A,N,O,rect(slot,A.width),c1,c2); cj["cells"][name]=cj["cells"].pop(slot); cj["cells"][name]["m"]=[1.0,1.0]
    for k,im in zip(("A","N","ORM"),(A,N,O)):
        im.resize((size,size),Image.LANCZOS).save(f"{out}/Textures/T_{asset}_{k}.png",optimize=True)
    json.dump(cj,open(out+"/cells.json","w"),indent=1); print(asset,size,list(stripe_cells.values()))
CREAM=(239,228,204)
make("RealBakery",B+"/Bld_Bakery",1024,{"Sandstone2":("Stripe",(176,52,44),CREAM)},
     {"WoodBeam":(.56,.44,.36),"WoodPlank":(.62,.5,.42),"EndGrain":(.62,.5,.42),"Plaster":(1.0,.93,.79)})
make("RealStall",B+"/Kit_RealStall",512,{"Sandstone2":("Stripe",(176,52,44),CREAM),"Flowers":("StripeB",(46,104,150),CREAM)},{})
