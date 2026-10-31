"""Season 1 kingdom look: repaint the realistic building atlases' cells with graded CC0 Poly Haven surfaces.

Usage: python3 repaint_atlases.py <polyhaven_dl_dir> <original_atlas_dir>
Layouts come from each building's cells.json. UVs are unchanged; only the cell pixels are replaced.
"""
import json, os, sys
from PIL import Image, ImageEnhance

DL, ORIG = sys.argv[1], sys.argv[2]
ROOT = os.path.join(os.path.dirname(__file__), '../../../Assets/_Project/Models/Realistic/Buildings')
SRC_M = 2.0  # metres covered by one Poly Haven 1k texture (approx.)

def graded(name, sat=1.2, bri=1.0, con=1.08, tint=(1, 1, 1)):
    im = Image.open(os.path.join(DL, name)).convert('RGB')
    im = ImageEnhance.Contrast(im).enhance(con); im = ImageEnhance.Color(im).enhance(sat); im = ImageEnhance.Brightness(im).enhance(bri)
    r, g, b = im.split()
    r = r.point(lambda v: min(255, int(v * tint[0]))); g = g.point(lambda v: min(255, int(v * tint[1]))); b = b.point(lambda v: min(255, int(v * tint[2])))
    return Image.merge('RGB', (r, g, b))

S = {
    'brick':  lambda: graded('red_brick_03.jpg', 1.25, 1.05, 1.1, (1.08, 0.92, 0.88)),
    'stone':  lambda: graded('castle_wall_varriation.jpg', 0.9, 1.1, 1.12, (0.98, 1.0, 1.05)),
    'slate':  lambda: graded('castle_wall_slates.jpg', 0.9, 1.1, 1.1),
    'blocks': lambda: graded('medieval_blocks_02.jpg', 0.9, 1.1, 1.1),
    'wood':   lambda: graded('brown_planks_03.jpg', 1.3, 1.0, 1.12, (1.1, 0.9, 0.72)),
    'beam':   lambda: graded('weathered_planks.jpg', 1.3, 0.95, 1.12, (1.1, 0.88, 0.7)),
    'roof':   lambda: graded('clay_roof_tiles_02.jpg', 0.9, 0.88, 1.15, (1.08, 0.62, 0.55)),
    'rock':   lambda: graded('rock_face_03.jpg', 0.85, 1.05, 1.1),
}
PLAN = {
    'Bld_Bakery':    {'Sandstone': 'brick', 'Sandstone2': 'blocks', 'SandDressed': 'blocks', 'WoodBeam': 'beam', 'WoodPlank': 'wood', 'Terracotta': 'roof', 'Granite': 'rock'},
    'Bld_Townhouse': {'Sandstone': 'slate', 'Sandstone2': 'blocks', 'SandDressed': 'blocks', 'WoodBeam': 'beam', 'WoodPlank': 'wood', 'Terracotta': 'roof', 'Granite': 'rock'},
    'Kit_RealStall': {'Sandstone': 'brick', 'SandDressed': 'blocks', 'WoodBeam': 'beam', 'WoodPlank': 'wood', 'Terracotta': 'roof', 'Granite': 'rock'},
    'RealCastle':    {'Sandstone': 'stone', 'Sandstone2': 'blocks', 'SandDressed': 'blocks', 'WoodBeam': 'beam', 'WoodPlank': 'wood', 'Terracotta': 'roof', 'Granite': 'rock', 'Rock': 'rock'},
}
for bld, plan in PLAN.items():
    d = os.path.join(ROOT, bld); cells = json.load(open(os.path.join(d, 'cells.json')))
    tex = [f for f in os.listdir(os.path.join(d, 'Textures')) if f.endswith('_A.png') and 'Inner' not in f][0]
    atlas = Image.open(os.path.join(ORIG, tex)).convert('RGB'); W, H = atlas.size; k = W / cells['size']
    for cname, src in plan.items():
        c = cells['cells'].get(cname)
        if not c: continue
        u0, v0, u1, v1 = c['uv']; x0, x1 = int(round(u0 * W)), int(round(u1 * W)); y0, y1 = int(round((1 - v1) * H)), int(round((1 - v0) * H))
        px = max(8, int(round(c['pxm'] * k * SRC_M)))
        tile = S[src]().resize((px, px), Image.LANCZOS)
        cell = Image.new('RGB', (x1 - x0, y1 - y0))
        for yy in range(0, y1 - y0, px):
            for xx in range(0, x1 - x0, px): cell.paste(tile, (xx, yy))
        atlas.paste(cell, (x0, y0))
    atlas.save(os.path.join(d, 'Textures', tex)); print('repainted', bld, tex)
