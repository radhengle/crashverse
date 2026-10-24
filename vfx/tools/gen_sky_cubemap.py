"""Generates the bright gradient-sky fallback cubemap (Art: sky #8CC8FF / equator #DCEFFF / ground #6FAE5A).
Output: 6x1 horizontal strip, face order +X -X +Y -Y +Z -Z (Unity cubemap 'Auto' layout). Run: python3 vfx/tools/gen_sky_cubemap.py"""
import numpy as np
from PIL import Image
N = 128
hexc = lambda h: np.array([int(h[i:i+2], 16) for i in (1, 3, 5)], np.float32) / 255.0
SKY, EQ, GROUND = hexc("#8CC8FF"), hexc("#DCEFFF"), hexc("#6FAE5A")
def to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def to_srgb(c): return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)
u = (np.arange(N) + 0.5) / N * 2 - 1
a, b = np.meshgrid(u, -u)  # a: right, b: up on the face
faces = [(np.ones_like(a), b, -a), (-np.ones_like(a), b, a), (a, np.ones_like(a), -b),
         (a, -np.ones_like(a), b), (a, b, np.ones_like(a)), (-a, b, -np.ones_like(a))]
out = []
for x, y, z in faces:
    d = y / np.sqrt(x * x + y * y + z * z)
    up = np.clip(d, 0, 1)[..., None] ** 0.6; dn = np.clip(-d, 0, 1)[..., None] ** 0.5
    lin = to_lin(EQ) * (1 - up - dn) + to_lin(SKY) * up + to_lin(GROUND) * dn  # blend in linear
    out.append(np.clip(to_srgb(lin), 0, 1))
img = (np.concatenate(out, axis=1) * 255 + 0.5).astype(np.uint8)
Image.fromarray(img, "RGB").save("Assets/_Project/VFX/Lighting/T_SkyFallback_Cube.png")
print("wrote", img.shape)
