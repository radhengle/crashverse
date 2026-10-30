"""Quick albedo check for graded CRASHVERSE v3 textures (sRGB base-colour maps).
usage: python3 albedo_check.py <material> <texture.png>
material: brick | sandstone | granite | steel | wood | roof
Reports avg hex, HSV V/S averages and % pixels outside the 50..240 floor/ceiling."""
import sys, colorsys
from PIL import Image
import numpy as np
TARGET = {"brick": "#D2664C", "sandstone": "#C9B48A", "granite": "#4A4F5C",
          "steel": "#AEB6C2", "wood": "#E3A766", "roof": "#D9703F"}
SAT_MIN = {"brick": .55, "wood": .50, "sandstone": .28, "roof": .55}
mat, path = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(path).convert("RGB")).reshape(-1, 3).astype(float)
avg = a.mean(0); h, s, v = colorsys.rgb_to_hsv(*(avg / 255))
t = TARGET[mat].lstrip("#"); tr = np.array([int(t[i:i+2], 16) for i in (0, 2, 4)], float)
lum = a.max(1); out = ((lum < 50) | (lum > 240)).mean() * 100
print(f"{mat}: avg #{''.join(f'{int(c):02X}' for c in avg)} target {TARGET[mat]}  "
      f"dist {np.linalg.norm(avg - tr):.1f}  V {v:.2f}  S {s:.2f}  out-of-range {out:.1f}%")
ok = np.linalg.norm(avg - tr) < 25 and out < 2 and (mat == "granite" or v >= .55) and s >= SAT_MIN.get(mat, 0)
print("PASS" if ok else "CHECK: grade toward target")
