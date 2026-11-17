"""Original procedural, tileable water-wave normal map for the L1 sea (no third-party source).
python3 make_water_normal.py out.jpg"""
import sys, numpy as np
from PIL import Image
N = 512; rng = np.random.default_rng(7)
kx = np.fft.fftfreq(N)[None, :] * N; ky = np.fft.fftfreq(N)[:, None] * N
k = np.sqrt(kx ** 2 + ky ** 2) + 1e-6
ang = np.arctan2(ky, kx)
# wind-driven spectrum: peak around 6-14 cycles/tile, stretched along the wind, small chop up to ~60
spec = np.exp(-((np.log(k) - np.log(9.0)) ** 2) / 0.45) * (0.35 + 0.65 * np.cos(ang - 0.4) ** 2) + 0.12 * np.exp(-((np.log(k) - np.log(30.0)) ** 2) / 0.6)
spec[0, 0] = 0
h = np.real(np.fft.ifft2(spec * (rng.normal(size=(N, N)) + 1j * rng.normal(size=(N, N)))))
h = np.sign(h) * np.abs(h) ** 0.8  # sharper crests
h /= np.abs(h).max()
gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * N * 0.02
gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * N * 0.02
n = np.stack([-gx, -gy, np.ones_like(h)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
Image.fromarray(((n * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8)).save(sys.argv[1], quality=92)
print("ok", np.abs(gx).mean())
