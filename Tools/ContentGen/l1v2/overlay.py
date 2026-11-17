"""Project every layout block with the goal camera and draw its outline on the goal image (alignment check)."""
import json, sys
from PIL import Image, ImageDraw
sys.path.insert(0, '/workspace/p1/v2'); from proj import to_screen
lay = json.load(open('/workspace/cv_p1/Assets/_Project/Resources/Data/Layouts/c1_l01_tide_watchtower.json'))
im = Image.open('/workspace/cv_p1/design/level_concepts_v1/L01_v2_goal.png').convert('RGB'); W, H = im.size
d = ImageDraw.Draw(im)
col = {'island': (0, 255, 255), 'core': (255, 0, 255), 'wall': (255, 255, 0), 'roof': (255, 80, 0), 'floor': (0, 255, 0), 'support': (80, 160, 255), 'prop': (255, 255, 255)}
edges = [(0,1),(0,2),(0,4),(1,3),(1,5),(2,3),(2,6),(3,7),(4,5),(4,6),(5,7),(6,7)]
for b in lay['blocks']:
    c = [b['p'][0] - 3.0, b['p'][1], b['p'][2]]; s = b['s']
    pts = [to_screen((c[0] + (s[0]/2 if i&1 else -s[0]/2), c[1] + (s[1]/2 if i&2 else -s[1]/2), c[2] + (s[2]/2 if i&4 else -s[2]/2))) for i in range(8)]
    pts = [(u*W/100, v*H/100) for u, v in pts]
    for a, e in edges: d.line([pts[a], pts[e]], fill=col.get(b['role'], (255,255,255)), width=2)
im.save('/workspace/v2/overlay_layout.png'); print('ok')
