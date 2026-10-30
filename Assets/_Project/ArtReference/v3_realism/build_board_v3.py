from PIL import Image, ImageDraw, ImageFont, ImageOps, ImageFilter
W, H = 1920, 1080
NAVY9, NAVY7, NAVY5 = (11,21,48), (20,40,92), (34,64,138)
GOLD, WHITE, PALE = (245,196,81), (255,255,255), (207,227,255)
NB = "/usr/share/fonts/truetype/sand-box/google/Nunito/Nunito-VariableFont_wght.ttf"
LG = "/usr/share/fonts/truetype/sand-box/google/Luckiest Guy/LuckiestGuy-Regular.ttf"
def nun(sz, w=800):
    f = ImageFont.truetype(NB, sz)
    try: f.set_variation_by_axes([w])
    except Exception: pass
    return f
F_T, F_H, F_B, F_S = ImageFont.truetype(LG, 40), nun(21, 900), nun(17, 700), nun(15, 700)
S = "src/"
bg = Image.new("RGB", (W, H), NAVY9)
d = ImageDraw.Draw(bg)
for y in range(H):  # subtle navy gradient
    t = y / H; d.line([(0, y), (W, y)], fill=tuple(int(NAVY9[i]*(1-t)+NAVY7[i]*t) for i in range(3)))

def card(im, box, label, sub=None, top=False):
    x, y, w, h = box
    im = ImageOps.fit(im.convert("RGB"), (w, h), Image.LANCZOS)
    sh = Image.new("RGBA", (w+24, h+24), (0,0,0,0)); ImageDraw.Draw(sh).rounded_rectangle([12,12,w+12,h+12], 14, fill=(3,8,24,170))
    bg.paste(sh.filter(ImageFilter.GaussianBlur(7)), (x-6, y), sh.filter(ImageFilter.GaussianBlur(7)))
    m = Image.new("L", (w, h), 0); ImageDraw.Draw(m).rounded_rectangle([0,0,w-1,h-1], 12, fill=255)
    bg.paste(im, (x, y), m)
    d.rounded_rectangle([x-2, y-2, x+w+1, y+h+1], 14, outline=GOLD, width=3)
    tw = d.textlength(label or "", font=F_S) + 20
    if not label: return
    ly = y+10 if top else y+h-38
    d.rounded_rectangle([x+10, ly, x+10+tw, ly+28], 9, fill=(11,21,48))
    d.text((x+20, ly+3), label, font=F_S, fill=WHITE)
    if sub:
        d.text((x, y+h+8), sub, font=F_S, fill=PALE)

def head(x, y, t): d.text((x, y), t, font=F_H, fill=GOLD)

# header
wm = Image.open("/workspace/art/v1/brand/crashverse_wordmark.png").convert("RGBA")
wm.thumbnail((300, 70), Image.LANCZOS); bg.paste(wm, (30, 12), wm)
d.text((350, 20), "STYLE BOARD v3  ·  REALISM DIRECTION", font=F_T, fill=WHITE)
d.text((352, 62), "Realistic PBR buildings & trees  ·  same bright palette  ·  11:00 sun  ·  UI unchanged   (DRAFT for PM + 3D Modeler check, 24 Oct 2026)", font=F_B, fill=PALE)
d.line([(30, 92), (W-30, 92)], fill=GOLD, width=2)

# col 1
town = Image.open(S+"mood_01_town.png"); card(town, (30, 108, 880, 495), "Realistic brick + sandstone town, floating island (mood)")
sw = Image.open(S+"mood_03_swatches.png"); sw = sw.crop((20, 295, 1756, 600))
head(30, 622, "REAL MATERIAL DETAIL (mood)"); d.text((370, 626), "brick · sandstone · granite+gold · steel · wood · terracotta", font=F_S, fill=PALE)
card(sw, (30, 652, 880, 155), None)
head(30, 828, "LOCKED ALBEDO TARGETS (avg, graded, sRGB)")
locked = [("Brick", "#D2664C", "mortar #7A3326"), ("Mortar", "#7A3326", "always visible"),
          ("Sandstone", "#C9B48A", "breakable"), ("Granite base", "#4A4F5C", "indestructible"),
          ("Gold trim", "#F5C451", "band on bases"), ("Steel", "#AEB6C2", "M1 R0.3 +probe"),
          ("Wood", "#E3A766", "weakest"), ("Roof", "#D9703F", "terracotta")]
sx, sy, sw_w = 30, 860, 104
for i, (n, hx, note) in enumerate(locked):
    x = sx + i*(sw_w+7); c = tuple(int(hx[j:j+2], 16) for j in (1,3,5))
    d.rounded_rectangle([x, sy, x+sw_w, sy+110], 10, fill=c, outline=(255,255,255), width=2)
    if n == "Granite base": d.rectangle([x+2, sy+70, x+sw_w-2, sy+82], fill=GOLD)
    g = int(.299*c[0]+.587*c[1]+.114*c[2])  # greyscale chip
    d.rectangle([x+sw_w-26, sy+6, x+sw_w-6, sy+26], fill=(g,g,g), outline=(255,255,255))
    d.text((x+2, sy+116), n, font=nun(15, 900), fill=WHITE)
    d.text((x+2, sy+135), hx, font=F_S, fill=GOLD)
    d.text((x+2, sy+153), note, font=nun(13, 700), fill=PALE)
d.text((30, 1050), "Corner chip = greyscale value. Breakable (light) vs indestructible granite (dark) must stay distinct.", font=nun(14,700), fill=PALE)

# col 2
gp = Image.open(S+"mood_04_gameplay.png"); card(gp, (940, 108, 600, 338), "Gameplay camera: brick wall shatter (mood)")
gs = ImageOps.fit(ImageOps.grayscale(gp).convert("RGB"), (190, 107), Image.LANCZOS)
bg.paste(gs, (940+600-200, 108+10)); d.rectangle([940+400-1, 117, 940+590, 118+107], outline=WHITE, width=2)
d.rectangle([940+399, 118+107, 940+520, 118+107+20], fill=NAVY9); d.text((940+405, 118+107+1), "greyscale check", font=nun(13,800), fill=WHITE)
tr = Image.open(S+"mood_02_trees.png"); card(tr, (940, 476, 600, 338), "Lush realistic species: oak · pine · birch · blossom (mood)")
head(940, 828, "PALETTE (unchanged)")
pal = [("Navy","#0B1530"),("Navy 5","#22408A"),("Charcoal","#3A3F48"),("Gold","#F5C451"),
       ("Orange","#FF7A1A"),("Cyan Core","#22D3EE"),("Violet","#8B5CF6")]
for i,(n,hx) in enumerate(pal):
    x = 940 + i*86; c = tuple(int(hx[j:j+2],16) for j in (1,3,5))
    d.rounded_rectangle([x, 860, x+80, 940], 10, fill=c, outline=(255,255,255), width=2)
    d.text((x+2, 946), n, font=nun(14,900), fill=WHITE); d.text((x+2, 964), hx, font=nun(13,700), fill=GOLD)
d.text((940, 992), "Cyan = player / Wrecker Core only. Never in environment.", font=nun(15,800), fill=(34,211,238))
d.text((940, 1014), "Bloom only on Core + FX (HDR threshold ≥1.1). ACES or Neutral tonemap.", font=nun(15,800), fill=PALE)

# col 3: UI inset + rules
ui = Image.open("/workspace/art/v2/screens/01_login.png")
card(ui, (1570, 108, 320, 569), "v2 UI stays as-is", top=True)
head(1570, 692, "KEY RULES")
rules = ["Real material reads, game-readable scale", "De-light + grade scans UP to locked hex",
         "Avg albedo V ≥ 0.55 (granite excepted)", "Light AO/dirt: AO ≤0.5, grime ≤10%",
         "Lush saturated greens, no olive/brown", "Sun 11:00, deep blue sky, cool light shade",
         "Steel needs a reflection probe", "Granite bases never crack, gold band",
         "Same Wrecker/Guardian shapes", "Budgets: Graphics team to confirm"]
for i, r in enumerate(rules):
    y = 724 + i*33
    d.ellipse([1572, y+6, 1582, y+16], fill=(255,122,26))
    d.text((1590, y), r, font=nun(16,800), fill=WHITE)
d.text((1570, 1056), "Mood images: original, Canva-generated. Not final assets.", font=nun(12,700), fill=PALE)
bg.save("style_board_v3.png")
print("ok")
