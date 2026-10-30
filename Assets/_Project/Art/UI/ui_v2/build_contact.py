"""contact_sheet.png: icons on navy (labelled) + the 5 backgrounds at small size. Also src/qa_*.png check sheets."""
from PIL import Image, ImageDraw, ImageFont
ICONS=['mega_bomb','multi_shot','guided_rocket','time_freeze','shield','earthquake','laser_beam','black_hole',
 'star_on','star_off','coin','gem','chest','lock','skull_hard_level','badge_daily_boss',
 'avatar_01','avatar_02','avatar_03','avatar_04','badge_01_bronze','badge_02_silver','badge_03_gold']
BGS=['bg_world_map','bg_season_sunny_shores','bg_season_frozen_peaks','bg_season_neon_city','bg_season_primeval_peaks']
NAVY=(14,22,46); GOLD=(244,190,60)
def font(sz):
    for p in ['/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf','/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf']:
        try: return ImageFont.truetype(p,sz)
        except: pass
    return ImageFont.load_default()
f=font(22); ft=font(44)
COLS=8; CELL=300; IC=220
rows=(len(ICONS)+COLS-1)//COLS
bw=COLS*CELL//5; bh=int(bw*2340/1080)
W=COLS*CELL; H=110+rows*CELL+60+bh+70
im=Image.new('RGB',(W,H),NAVY); d=ImageDraw.Draw(im)
d.text((30,30),'CRASHVERSE UI art pack v2: icons (256px, transparent) + backgrounds (1080x2340)',font=ft,fill=GOLD)
for i,n in enumerate(ICONS):
    r,c=divmod(i,COLS); x=c*CELL; y=110+r*CELL
    ic=Image.open(n+'.png').resize((IC,IC),Image.LANCZOS)
    im.paste(ic,(x+(CELL-IC)//2,y+10),ic)
    tw=d.textlength(n,font=f); d.text((x+(CELL-tw)/2,y+IC+24),n,font=f,fill=(230,236,250))
y0=110+rows*CELL+40
for i,n in enumerate(BGS):
    b=Image.open(n+'.png').resize((bw-20,bh-20*2340//1080),Image.LANCZOS)
    x=i*bw+10; im.paste(b,(x,y0))
    # safe-area guides
    s=(bw-20)/1080; d.rectangle((x,y0,x+bw-21,y0+int(158*s)),outline=(34,211,238),width=2)
    d.rectangle((x,y0+b.size[1]-int(72*s),x+bw-21,y0+b.size[1]),outline=(34,211,238),width=2)
    tw=d.textlength(n,font=f); d.text((x+(bw-20-tw)/2,y0+b.size[1]+12),n,font=f,fill=(230,236,250))
im.save('contact_sheet.png'); print(im.size)
# QA: each icon at 2x on navy / white / checker
for k,(name,bgf) in enumerate([('navy',NAVY),('white',(255,255,255)),('checker',None)]):
    q=Image.new('RGB',(COLS*270,rows*270),bgf or (200,200,200))
    if bgf is None:
        dq=ImageDraw.Draw(q)
        for yy in range(0,q.size[1],16):
            for xx in range(0,q.size[0],16):
                if (xx//16+yy//16)%2: dq.rectangle((xx,yy,xx+15,yy+15),fill=(120,120,120))
    for i,n in enumerate(ICONS):
        r,c=divmod(i,COLS); ic=Image.open(n+'.png'); q.paste(ic,(c*270+7,r*270+7),ic)
    q.save(f'src/qa_icons_on_{name}.png')
