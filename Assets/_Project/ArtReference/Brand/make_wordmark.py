from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
import numpy as np
LIL='/usr/share/fonts/truetype/sand-box/google/Lilita One/LilitaOne-Regular.ttf'
S=2  # supersample
f=ImageFont.truetype(LIL,400*S)
parts=[('CRASH',(255,228,92),(255,138,18)),('VERSE',(86,222,255),(146,88,250))]
gap=16*S; ws=[f.getbbox(t)[2]-f.getbbox(t)[0] for t,*_ in parts]
pad=140*S; W=sum(ws)+gap+2*pad; H=f.getbbox('CRASHVERSE')[3]+2*pad
oy=pad-f.getbbox('C')[1]
def mask(stroke=0,only=None):
    m=Image.new('L',(W,H),0); d=ImageDraw.Draw(m); x=pad
    for i,((t,*_),w) in enumerate(zip(parts,ws)):
        if only is None or only==i: d.text((x-f.getbbox(t)[0],oy),t,font=f,fill=255,stroke_width=stroke,stroke_fill=255)
        x+=w+gap
    return m
def vgrad(c1,c2,y0,y1):
    t=np.clip((np.arange(H)[:,None]-y0)/(y1-y0),0,1)[...,None]
    a=np.array(c1)*(1-t)+np.array(c2)*t; return np.broadcast_to(a,(H,W,3)).astype(np.uint8)
ty0=pad; ty1=H-pad
out=np.zeros((H,W,4),float)
def put(rgb,m):
    a=np.asarray(m,float)[...,None]/255; out[...,:3]=out[...,:3]*(1-a)+rgb*a; out[...,3]=np.maximum(out[...,3],a[...,0]*255) if True else 0
navy=np.array([22,26,62]); 
outer=mask(34*S); gold=mask(20*S); face=mask()
# drop shadow
sh=outer.filter(ImageFilter.GaussianBlur(18*S)); sh=ImageChops.offset(sh,0,26*S)
put(np.zeros(3),sh.point(lambda v:v*0.55))
# extrusion (navy, deep)
for dy in range(30*S,0,-2*S): put(navy*0.7,ImageChops.offset(outer,0,dy))
put(navy,outer)
# gold bevel rim with gradient
put(vgrad((255,236,160),(176,112,30),ty0,ty1).astype(float),gold)
# inner dark line
put(np.array([120,70,20]),mask(5*S))
# faces
fm=np.asarray(face,float)
for i,(t,c1,c2) in enumerate(parts):
    put(vgrad(c1,c2,ty0+20*S,ty1-20*S).astype(float),mask(only=i))
# inner bevel: darken bottom edge, lighten top edge
fa=fm/255; b=np.asarray(face.filter(ImageFilter.GaussianBlur(10*S)),float)/255
up=np.roll(b,8*S,0); dn=np.roll(b,-8*S,0)
lit=np.clip(b-up,0,1)*fa; drk=np.clip(b-dn,0,1)*fa
out[...,:3]=out[...,:3]*(1-drk[...,None]*0.6)
out[...,:3]=out[...,:3]+(255-out[...,:3])*np.clip(lit*1.4,0,1)[...,None]
# gloss band on upper half
er=np.asarray(face.filter(ImageFilter.MinFilter(int(18*S)|1)).filter(ImageFilter.GaussianBlur(3*S)),float)/255
yy=np.arange(H)[:,None]; band=((yy>ty0)&(yy<ty0+(ty1-ty0)*0.40)).astype(float)
band=np.asarray(Image.fromarray((band*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6*S)),float)/255
g=er*band*0.38; out[...,:3]=out[...,:3]+(255-out[...,:3])*g[...,None]
img=Image.fromarray(out.clip(0,255).astype(np.uint8),'RGBA')
img=img.crop(img.getchannel('A').point(lambda v:255 if v>8 else 0).getbbox())
def fit(im,w): return im.resize((w,round(im.height*w/im.width)),Image.LANCZOS)
main=fit(img,2048); main.save('crashverse_wordmark.png')
fit(img,1024).save('crashverse_wordmark_1024.png'); fit(img,512).save('crashverse_wordmark_512.png')
# single colour (white, with alpha) — silhouette of outer shape with face cutout
mono=Image.new('RGBA',main.size,(255,255,255,0)); a=main.getchannel('A').point(lambda v:255 if v>128 else int(v*2))
mono.putalpha(a); mono.save('crashverse_wordmark_mono_white.png')
mb=Image.new('RGBA',main.size,(22,26,62,0)); mb.putalpha(a); mb.save('crashverse_wordmark_mono_navy.png')
# preview
pw=fit(img,900); P=Image.new('RGBA',(2000,600))
nav=Image.new('RGBA',(1000,600),(16,22,52,255))
sk=Image.fromarray(vgrad((40,110,230),(150,210,255),0,600)[:600,:1000]).convert('RGBA')
P.paste(nav,(0,0)); P.paste(sk,(1000,0))
for x in (50,1050): P.alpha_composite(pw,(x,(600-pw.height)//2))
P.convert('RGB').save('wordmark_preview_on_navy_and_sky.png')
print(main.size)
