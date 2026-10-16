from PIL import Image, ImageDraw, ImageFont, ImageFilter
import os
W,H=1080,2340; SAFE_T=158; SAFE_B=72
LIL='/usr/share/fonts/truetype/sand-box/google/Lilita One/LilitaOne-Regular.ttf'
RB='/usr/share/fonts/truetype/sand-box/google/Rubik/Rubik-VariableFont_wght.ttf'
def lil(s): return ImageFont.truetype(LIL,s)
def rb(s,w='Medium'):
    f=ImageFont.truetype(RB,s)
    try: f.set_variation_by_name(w)
    except Exception: pass
    return f
NAVY=(20,27,58); NAVY2=(30,40,82); CHAR=(38,42,54); GOLD=(244,190,64); GOLDD=(170,112,30)
ORANGE=(255,122,26); CYAN=(34,211,238); VIOLET=(139,92,246); WHITE=(255,255,255); MUTED=(200,210,235); ERR=(255,107,107)
os.makedirs('slices',exist_ok=True); os.makedirs('mockups',exist_ok=True)
# background
raw=Image.open('src/bg_canva_raw.jpg').convert('RGB')
s=H/raw.height; bg=raw.resize((round(raw.width*s),H),Image.LANCZOS)
x0=(bg.width-W)//2; bg=bg.crop((x0,0,x0+W,H))
bg.save('slices/bg_age_gate_1080x2340.png')
def vgrad(w,h,c1,c2):
    g=Image.new('RGB',(w,h))
    d=ImageDraw.Draw(g)
    for y in range(h):
        t=y/max(1,h-1); d.line([(0,y),(w,y)],fill=tuple(int(c1[i]+(c2[i]-c1[i])*t) for i in range(3)))
    return g
def rr_mask(w,h,r):
    m=Image.new('L',(w,h),0); ImageDraw.Draw(m).rounded_rectangle((0,0,w-1,h-1),r,fill=255); return m
def panel(w,h):
    im=Image.new('RGBA',(w+40,h+40),(0,0,0,0))
    sh=Image.new('RGBA',im.size,(0,0,0,0)); ImageDraw.Draw(sh).rounded_rectangle((20,34,w+20,h+34),36,fill=(0,0,0,150))
    im=Image.alpha_composite(im,sh.filter(ImageFilter.GaussianBlur(12)))
    gold=vgrad(w,h,(255,222,120),GOLDD); im.paste(gold,(20,20),rr_mask(w,h,36))
    inner=vgrad(w-16,h-16,NAVY2,(14,18,42)); im.paste(inner,(28,28),rr_mask(w-16,h-16,30))
    d=ImageDraw.Draw(im); d.rounded_rectangle((28,28,w+11,h+11),30,outline=(255,240,190,90),width=2)
    return im
def frame_only(w,h):
    p=panel(w,h); a=p.copy(); m=Image.new('L',p.size,255); ImageDraw.Draw(m).rounded_rectangle((28,28,w+11,h+11),30,fill=0)
    a.putalpha(Image.composite(p.getchannel('A'),Image.new('L',p.size,0),m)); return a
def button(w,h,state,label='CONTINUE'):
    im=Image.new('RGBA',(w,h+14),(0,0,0,0))
    if state=='disabled': top,bot,lip,tc,stroke=(92,100,128),(66,72,98),(40,44,62),(170,176,196),(50,54,74)
    elif state=='pressed': top,bot,lip,tc,stroke=(230,100,10),(200,80,0),(150,55,0),WHITE,(140,50,0)
    else: top,bot,lip,tc,stroke=(255,170,70),ORANGE,(176,72,0),WHITE,(150,58,0)
    oy=10 if state=='pressed' else 0
    ImageDraw.Draw(im).rounded_rectangle((0,14,w-1,h+13),34,fill=lip+(255,))
    im.paste(vgrad(w,h,top,bot),(0,oy),rr_mask(w,h,34))
    d=ImageDraw.Draw(im)
    if state!='disabled':
        hl=Image.new('RGBA',(w,h),(0,0,0,0)); ImageDraw.Draw(hl).rounded_rectangle((24,8,w-24,h//2-6),24,fill=(255,255,255,70)); im.alpha_composite(hl,(0,oy))
    d.rounded_rectangle((0,oy,w-1,oy+h-1),34,outline=(255,236,180,255) if state!='disabled' else (120,128,156,255),width=4)
    d.text((w//2,oy+h//2+2),label,font=lil(64),fill=tc,anchor='mm',stroke_width=4 if state!='disabled' else 0,stroke_fill=stroke)
    return im
CHEV='▾'
def field(w,h,state,label,value=None):
    im=Image.new('RGBA',(w,h),(0,0,0,0)); d=ImageDraw.Draw(im)
    border={'empty':(110,124,170),'filled':GOLD,'error':ERR,'focused':CYAN}[state]
    d.rounded_rectangle((0,0,w-1,h-1),30,fill=(10,14,34,235),outline=border,width=6 if state!='empty' else 4)
    d.text((34,40),label.upper(),font=rb(36,'Bold'),fill=(150,164,205) if state!='error' else ERR,anchor='lm')
    if value: d.text((34,112),value,font=lil(60),fill=WHITE,anchor='lm')
    else: d.text((34,112),'Tap to choose',font=rb(42,'Regular'),fill=(140,150,185),anchor='lm')
    cx,cy=w-56,h//2; d.polygon([(cx-20,cy-10),(cx+20,cy-10),(cx,cy+14)],fill=GOLD if state!='empty' else (150,164,205))
    return im
def row(w,h,state,text):
    im=Image.new('RGBA',(w,h),(0,0,0,0)); d=ImageDraw.Draw(im)
    if state=='pressed': d.rounded_rectangle((8,6,w-9,h-7),24,fill=(34,211,238,60))
    if state=='selected':
        d.rounded_rectangle((8,6,w-9,h-7),24,fill=(244,190,64,50),outline=GOLD,width=4)
        d.line((w-90,h//2,w-74,h//2+16),fill=GOLD,width=8); d.line((w-74,h//2+16,w-44,h//2-18),fill=GOLD,width=8)
    d.line((40,h-1,w-40,h-1),fill=(70,82,124),width=2)
    d.text((w//2,h//2),text,font=lil(58) if state!='normal' else lil(54),fill=WHITE if state!='normal' else (225,232,250),anchor='mm')
    return im
# slices
panel(960,1180).save('slices/panel_9slice_960x1180.png')
panel(240,240).save('slices/panel_9slice_src_240.png')
frame_only(960,1180).save('slices/frame_gold_960x1180.png')
for st in ['enabled','pressed','disabled']: button(864,150,st).save(f'slices/btn_continue_{st}_864x164.png')
for st,v in [('empty',None),('filled','March'),('focused',None),('error','December')]: field(420,168,st,'Month',v).save(f'slices/picker_field_{st}_420x168.png')
for st in ['normal','pressed','selected']: row(960,144,st,'2008').save(f'slices/picker_row_{st}_960x144.png')
# medallion
def medallion(sz):
    m=Image.new('RGBA',(sz,sz),(0,0,0,0))
    isl=raw.crop((560,80,944,464)).resize((sz-40,sz-40),Image.LANCZOS)
    mk=Image.new('L',(sz-40,sz-40),0); ImageDraw.Draw(mk).ellipse((0,0,sz-41,sz-41),fill=255)
    d=ImageDraw.Draw(m); d.ellipse((0,0,sz-1,sz-1),fill=GOLDD); d.ellipse((6,6,sz-7,sz-12),fill=(255,214,110))
    m.paste(isl,(20,20),mk); d.ellipse((20,20,sz-21,sz-21),outline=(255,240,190),width=4); return m
medallion(300).save('slices/block_medallion_300.png')
# logo
lock='/workspace/art/v1/crashverse_title_lockup.png'
login=Image.open('/workspace/art/v2/screens/01_login.png').convert('RGBA')
def title_logo():
    import numpy as np
    f=lil(190); parts=[('CRASH',((255,226,90),(255,140,20))),('VERSE',((80,220,255),(150,90,250)))]
    ws=[f.getbbox(t)[2] for t,_ in parts]; tw=sum(ws)+10; th=230
    L=Image.new('RGBA',(tw+80,th+80),(0,0,0,0)); x=40
    for (t,(c1,c2)),w in zip(parts,ws):
        m=Image.new('L',L.size,0); ImageDraw.Draw(m).text((x,30),t,font=f,fill=255)
        st=Image.new('L',L.size,0); ImageDraw.Draw(st).text((x,30),t,font=f,fill=255,stroke_width=12)
        for dy in range(18,0,-2):
            L.paste((30,22,60,255),(0,dy),st)
        L.paste((25,20,52,255),(0,0),st)
        g=vgrad(L.width,L.height,c1,c2); gg=Image.new('RGBA',L.size); gg.paste(g); L.paste(gg,(0,0),m)
        hl=Image.new('L',L.size,0); ImageDraw.Draw(hl).text((x,30),t,font=f,fill=90)
        cut=Image.new('L',L.size,0); ImageDraw.Draw(cut).rectangle((0,0,L.width,30+th*0.42),fill=255)
        hl=Image.fromarray(np.minimum(np.asarray(hl),np.asarray(cut)).astype('uint8')); L.paste((255,255,255,255),(0,0),hl)
        x+=w+10
    sh=Image.new('RGBA',L.size,(0,0,0,0)); sh.putalpha(L.getchannel('A').filter(ImageFilter.GaussianBlur(14)).point(lambda v:v*0.5))
    out=Image.new('RGBA',(L.width,L.height+20),(0,0,0,0)); out.alpha_composite(sh,(0,16)); out.alpha_composite(L)
    if out.width>980: out=out.resize((980,int(out.height*980/out.width)),Image.LANCZOS)
    return out
def base():
    im=bg.convert('RGBA')
    # logo crop from v2 login (our own art) with soft mask
    lg=title_logo(); im.alpha_composite(lg,((W-lg.width)//2,SAFE_T+60)); return im
PX,PY,PW,PH=60,760,960,1180
def gate(state):
    im=base(); p=panel(PW,PH); im.alpha_composite(p,(PX-20,PY-20)); d=ImageDraw.Draw(im)
    # ribbon
    d.rounded_rectangle((240,PY-46,840,PY+50),46,fill=VIOLET,outline=GOLD,width=6)
    d.text((540,PY+2),'BEFORE YOU PLAY',font=lil(56),fill=WHITE,anchor='mm',stroke_width=3,stroke_fill=(70,40,140))
    d.text((540,PY+150),'Enter your birth month and year',font=rb(46,'Medium'),fill=WHITE,anchor='mm')
    d.text((540,PY+215),'This helps us set up the right game for you.',font=rb(36,'Regular'),fill=MUTED,anchor='mm')
    m,y,ms,ys={'empty':(None,None,'empty','empty'),'yearopen':(None,None,'empty','focused'),'enabled':('March','2009','filled','filled'),'error':('December','2026','error','error')}[state]
    im.alpha_composite(field(420,168,ms,'Month',m),(108,PY+300)); im.alpha_composite(field(420,168,ys,'Year',y),(552,PY+300))
    if state=='error':
        d.ellipse((112,PY+508,156,PY+552),fill=ERR); d.text((134,PY+531),'!',font=rb(34,'Bold'),fill=NAVY,anchor='mm')
        d.text((570,PY+530),'That date is in the future. Please check it.',font=rb(38,'Medium'),fill=ERR,anchor='mm')
    im.alpha_composite(button(864,150,'enabled' if state=='enabled' else 'disabled'),(108,PY+640))
    if state in('empty','yearopen'):
        d.text((540,PY+850),'Choose both to continue',font=rb(36,'Regular'),fill=(160,170,205),anchor='mm')
    d.text((540,PY+1060),'Privacy Policy',font=rb(38,'Medium'),fill=(215,224,245),anchor='mm')
    d.line((430,PY+1086,650,PY+1086),fill=(215,224,245),width=2)
    if state=='yearopen':
        dim=Image.new('RGBA',(W,H),(5,8,22,170)); im.alpha_composite(dim)
        SY=1180; sh=panel(1040,H-SY+60); im.alpha_composite(sh,(0,SY))
        d=ImageDraw.Draw(im)
        d.rounded_rectangle((470,SY+44,610,SY+56),6,fill=(120,132,170))
        d.text((540,SY+130),'BIRTH YEAR',font=lil(60),fill=GOLD,anchor='mm')
        d.ellipse((900,SY+82,996,SY+178),fill=(40,52,100),outline=GOLD,width=4); d.line((930,SY+112,966,SY+148),fill=WHITE,width=7); d.line((966,SY+112,930,SY+148),fill=WHITE,width=7)
        top,bot=SY+220,H-SAFE_B-20; rh=144; mid=(top+bot)//2
        years=list(range(2026,1925,-1)); ci=years.index(2008)
        lst=Image.new('RGBA',(960,bot-top),(0,0,0,0))
        n=(bot-top)//rh//2+2
        for k in range(-n,n+1):
            yy=years[ci+k]; ry=(mid-top)-rh//2+k*rh
            lst.alpha_composite(row(960,rh,'normal',str(yy)),(0,ry)) if 0<=ry+rh and ry<lst.height else None
        fm=vgrad(1,lst.height,(0,0,0),(0,0,0)).convert('L'); md=ImageDraw.Draw(fm)
        for yv in range(lst.height):
            e=min(yv,lst.height-yv); md.point((0,yv),fill=min(255,int(e/180*255)))
        lst.putalpha(Image.composite(lst.getchannel('A'),Image.new('L',lst.size,0),fm.resize(lst.size)))
        im.alpha_composite(lst,(60,top))
        # scrollbar
        d.rounded_rectangle((1010,top+40,1022,bot-40),6,fill=(50,60,100)); pos=top+40+int((bot-top-80)*ci/len(years))
        d.rounded_rectangle((1010,pos-40,1022,pos+40),6,fill=GOLD)
    tag(im,state); return im
def tag(im,s):
    d=ImageDraw.Draw(im); d.text((W-40,SAFE_T-30),f'MOCKUP · {s}',font=rb(26,'Medium'),fill=(255,255,255,170),anchor='rm')
def block():
    im=base(); p=panel(PW,1080); im.alpha_composite(p,(PX-20,800)); d=ImageDraw.Draw(im)
    im.alpha_composite(medallion(300),(390,690)); d=ImageDraw.Draw(im)
    d.text((540,1090),'THANKS FOR STOPPING BY!',font=lil(66),fill=GOLD,anchor='mm',stroke_width=3,stroke_fill=(90,55,10))
    for i,t in enumerate(["CRASHVERSE isn't available","for your age right now.","","We hope to see you again","when you're older!"]):
        d.text((540,1200+i*64),t,font=rb(46,'Medium') if i<2 else rb(42,'Regular'),fill=WHITE if i<2 else MUTED,anchor='mm')
    d.text((540,1580),'Grown-ups: read our',font=rb(36,'Regular'),fill=(170,180,212),anchor='mm')
    d.text((540,1640),'Privacy Policy',font=rb(40,'Medium'),fill=(215,224,245),anchor='mm'); d.line((400,1666,680,1666),fill=(215,224,245),width=2)
    tag(im,'under-13 block'); return im
title_logo().save('slices/title_logo.png')
outs=[('01_empty_disabled',gate('empty')),('02_year_picker_open',gate('yearopen')),('03_both_set_enabled',gate('enabled')),('04_error',gate('error')),('05_under13_block',block())]
for n,i in outs: i.convert('RGB').save(f'mockups/{n}.png')
# contact sheet
tw=432; th=936; cs=Image.new('RGB',(40+5*(tw+30),th+160),NAVY); d=ImageDraw.Draw(cs)
d.text((40,40),'CRASHVERSE · Art v2 · Age gate (1080x2340)',font=lil(48),fill=GOLD)
for k,(n,i) in enumerate(outs):
    cs.paste(i.convert('RGB').resize((tw,th),Image.LANCZOS),(40+k*(tw+30),110)); d.text((40+k*(tw+30),110+th+10),n,font=rb(26,'Medium'),fill=WHITE)
cs.save('age_gate_contact_sheet.png')
