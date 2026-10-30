from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np
LIL='/usr/share/fonts/truetype/sand-box/google/Lilita One/LilitaOne-Regular.ttf'
s=Image.open('src/feature_canva_a.jpg').convert('RGB')
W,H=1024,500; s=s.resize((W,round(s.height*W/s.width)),Image.LANCZOS); top=(s.height-H)//2
bg=s.crop((0,top,W,top+H)).convert('RGBA')
def lockup(bg,size=104,cy=92,tag=True):
    f=ImageFont.truetype(LIL,size); txt='CRASHVERSE'
    L=Image.new('RGBA',bg.size,(0,0,0,0)); d=ImageDraw.Draw(L)
    d.text((W//2+5,cy+8),txt,font=f,anchor='mm',fill=(0,0,0,150),stroke_width=12,stroke_fill=(0,0,0,150))
    L=L.filter(ImageFilter.GaussianBlur(5)); bg=Image.alpha_composite(bg,L)
    d=ImageDraw.Draw(bg); d.text((W//2,cy),txt,font=f,anchor='mm',fill=(18,28,70),stroke_width=12,stroke_fill=(18,28,70))
    d.text((W//2,cy),txt,font=f,anchor='mm',fill=(214,160,58),stroke_width=6,stroke_fill=(214,160,58))
    m=Image.new('L',bg.size,0); ImageDraw.Draw(m).text((W//2,cy),txt,font=f,anchor='mm',fill=255)
    g=np.zeros((H,W,4),np.uint8); y=np.linspace(0,1,H)[:,None]
    t=np.clip((y-(cy-size/2))/size,0,1)
    g[...,0]=255; g[...,1]=(236-110*t).astype(np.uint8)[:, :1].repeat(W,1); g[...,2]=(90-70*t).clip(0).astype(np.uint8)[:, :1].repeat(W,1); g[...,3]=255
    bg.paste(Image.fromarray(g,'RGBA'),(0,0),m)
    if tag:
        f2=ImageFont.truetype(LIL,34)
        d.text((W//2,cy+76),'AIM. BLAST. WRECK IT ALL.',font=f2,anchor='mm',fill=(255,255,255),stroke_width=4,stroke_fill=(18,28,70))
    return bg
lockup(bg.copy()).convert('RGB').save('feature_1024x500.png')
lockup(bg.copy()).convert('RGB').save('feature_1024x500.jpg',quality=95)
lockup(bg.copy(),tag=False).convert('RGB').save('feature_1024x500_var_notagline.png')
bg.convert('RGB').save('src/feature_art_clean_1024x500.png')
# safe-zone preview
p=Image.open('feature_1024x500.png'); d=ImageDraw.Draw(p); d.rectangle((102,50,922,450),outline=(255,0,0),width=2); p.save('src/feature_safezone_preview.png')
