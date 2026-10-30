from PIL import Image, ImageEnhance, ImageDraw, ImageFilter, ImageChops
import math, random
O='/workspace/cv_p1/Assets/_Project/Resources/Kingdom/Textures/'
def grade(src, dst, sat=1.3, bri=1.0, con=1.1, tint=(1,1,1), size=1024):
    im = Image.open(src).convert('RGB').resize((size,size), Image.LANCZOS)
    im = ImageEnhance.Contrast(im).enhance(con); im = ImageEnhance.Color(im).enhance(sat); im = ImageEnhance.Brightness(im).enhance(bri)
    r,g,b = im.split(); r = r.point(lambda v: min(255,int(v*tint[0]))); g = g.point(lambda v: min(255,int(v*tint[1]))); b = b.point(lambda v: min(255,int(v*tint[2])))
    im = Image.merge('RGB',(r,g,b)); im.save(O+dst, quality=92); print(dst, im.resize((1,1)).getpixel((0,0)))
grade('rocky_terrain_02.jpg','T_K_Grass.jpg', sat=1.45, bri=1.3, con=1.05, tint=(0.62,1.0,1.25))
grade('coast_sand_01.jpg','T_K_Sand.jpg', sat=1.05, bri=1.5, con=0.95, tint=(1.06,1.0,0.86))
grade('brown_planks_03.jpg','T_K_Planks.jpg', sat=1.7, bri=1.15, con=1.15, tint=(1.18,0.95,0.7))
grade('weathered_planks.jpg','T_K_Wood.jpg', sat=1.7, bri=1.2, con=1.15, tint=(1.2,0.93,0.66))
grade('castle_wall_varriation.jpg','T_K_Stone.jpg', sat=0.9, bri=1.25, con=1.1, tint=(1.12,1.02,0.86))
grade('red_brick_03.jpg','T_K_Brick.jpg', sat=1.35, bri=1.2, con=1.1, tint=(1.15,0.95,0.85))
grade('clay_roof_tiles_02.jpg','T_K_Roof.jpg', sat=1.0, bri=1.0, con=1.1, tint=(1.1,0.7,0.62))
grade('rock_face_03.jpg','T_K_Rock.jpg', sat=0.9, bri=1.1, con=1.15, tint=(1.02,0.97,0.9))
grade('bark_brown_02.jpg','T_K_Bark.jpg', sat=1.1, bri=1.1, con=1.1, tint=(1.1,0.95,0.8), size=512)
# sky: zenith..horizon of the equirect, saturated
sky = Image.open('kloofendal_48d_partly_cloudy_puresky.jpg').convert('RGB')
w,h = sky.size; top = sky.crop((0,0,w,int(h*0.52))).resize((2048,532), Image.LANCZOS)
top = ImageEnhance.Color(top).enhance(1.35); top = ImageEnhance.Contrast(top).enhance(1.08)
r,g,b = top.split(); r = r.point(lambda v: int(v*0.92)); b = b.point(lambda v: min(255,int(v*1.06))); top = Image.merge('RGB',(r,g,b))
top.save(O+'T_K_Sky.jpg', quality=90); print('sky', top.size)
# royal banner: crimson cloth, gold border, crown + fist crest
S=512; im = Image.new('RGB',(S,S),(150,18,24)); d = ImageDraw.Draw(im)
for y in range(S):
    k = 0.85+0.15*math.sin(y/23.0); d.line([(0,y),(S,y)], fill=(int(160*k),int(20*k),int(28*k)))
gold=(236,178,52); d.rectangle([14,14,S-15,S-15], outline=gold, width=14); d.rectangle([40,40,S-41,S-41], outline=(120,12,18), width=4)
cx, cy = S//2, S//2+20
d.polygon([(cx-120,cy+40),(cx-130,cy-70),(cx-70,cy-10),(cx,cy-110),(cx+70,cy-10),(cx+130,cy-70),(cx+120,cy+40)], fill=gold, outline=(150,90,20))
d.rectangle([cx-125,cy+40,cx+125,cy+75], fill=gold, outline=(150,90,20))
for x in (-90,0,90): d.ellipse([cx+x-16,cy+45,cx+x+16,cy+70], fill=(40,90,200))
for x,yy in ((-130,-70),(0,-110),(130,-70)): d.ellipse([cx+x-14,cy+yy-14,cx+x+14,cy+yy+14], fill=gold)
im = im.filter(ImageFilter.SMOOTH); im.save(O+'T_K_Banner.png'); 
# TNT crate
S=256; im = Image.new('RGB',(S,S),(150,92,48)); d = ImageDraw.Draw(im)
for i in range(0,S,32): d.line([(0,i),(S,i)], fill=(110,64,30), width=3)
d.rectangle([4,4,S-5,S-5], outline=(80,45,20), width=10); d.line([(8,8),(S-8,S-8)], fill=(95,55,25), width=14)
d.rectangle([40,92,S-41,164], fill=(205,30,28), outline=(60,10,10), width=4)
def T(x): 
    d.rectangle([x,104,x+40,116], fill=(250,230,120)); d.rectangle([x+14,104,x+26,154], fill=(250,230,120))
def N(x):
    d.rectangle([x,104,x+11,154], fill=(250,230,120)); d.rectangle([x+29,104,x+40,154], fill=(250,230,120)); d.polygon([(x,104),(x+11,104),(x+40,154),(x+29,154)], fill=(250,230,120))
T(52); N(108); T(164)
im.save(O+'T_K_TNT.png')
print('ok')
