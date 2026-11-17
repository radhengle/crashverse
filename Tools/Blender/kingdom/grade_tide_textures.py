# L1 Tide Watchtower PBR sets: Poly Haven CC0 1K (diffuse / nor_gl / arm) graded toward L01_v2_goal.png and
# written as T_TW_<name>{,_N,_ORM}.jpg (KingdomMaterials.Make picks _N/_ORM up automatically).
# Source: /workspace/assets/ph (fetched via api.polyhaven.com/files/<id>, 1k jpg). Licence: CC0 (polyhaven.com/license).
import shutil, sys
from PIL import Image, ImageEnhance
SRC = '/workspace/assets/ph/'; O = '/workspace/cv_p1/Assets/_Project/Resources/Kingdom/Textures/'
def mean(im): return tuple(round(v / 255, 3) for v in im.resize((1, 1), Image.BOX).getpixel((0, 0)))
def grade(src, dst, target, sat=1.0, con=1.1):
    im = Image.open(SRC + src + '_diff.jpg').convert('RGB')
    im = ImageEnhance.Contrast(im).enhance(con); im = ImageEnhance.Color(im).enhance(sat)
    m = mean(im); ch = im.split()
    ch = [c.point(lambda v, k=target[i] / max(0.02, m[i]): min(255, int(v * k))) for i, c in enumerate(ch)]
    im = Image.merge('RGB', ch); im.save(O + dst + '.jpg', quality=90)
    shutil.copy(SRC + src + '_nor.jpg', O + dst + '_N.jpg'); shutil.copy(SRC + src + '_arm.jpg', O + dst + '_ORM.jpg')
    print(dst, src, mean(im))
grade('rock_boulder_dry', 'T_TW_Stone', (0.56, 0.53, 0.49), sat=0.8, con=1.35)    # weathered block faces (the blocks themselves are modelled)
grade('brown_planks_09', 'T_TW_Deck', (0.50, 0.33, 0.20), sat=1.0, con=1.2)        # warm worn deck boards
grade('dark_wooden_planks', 'T_TW_Timber', (0.40, 0.25, 0.15), sat=1.0, con=1.15)  # scaffold posts/beams
grade('clay_roof_tiles_02', 'T_TW_Roof', (0.78, 0.20, 0.11), sat=1.2, con=1.15)    # saturated red clay tiles
grade('rock_face_03', 'T_TW_Rock', (0.52, 0.51, 0.48), sat=0.4, con=1.25)          # grey wet sea rock
grade('aerial_grass_rock', 'T_TW_Grass', (0.28, 0.45, 0.12), sat=1.3, con=1.1)     # mossy cliff caps
