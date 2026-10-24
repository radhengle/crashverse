#!/usr/bin/env python3
"""BUG-23 gate: fail if a screenshot has a large share of pixels near #FF00FF (Unity's missing-shader colour).
Pure-stdlib PNG decoder (8-bit RGB/RGBA, non-interlaced: what `adb screencap -p` writes). Usage: no_magenta.py MAX_SHARE png..."""
import struct, sys, zlib

def pixels(path):
    d = open(path, "rb").read(); assert d[:8] == b"\x89PNG\r\n\x1a\n", path
    i, idat = 8, b""
    while i < len(d):
        n, t = struct.unpack(">I4s", d[i:i + 8]); c = d[i + 8:i + 8 + n]; i += 12 + n
        if t == b"IHDR": w, h, bd, ct, _, _, il = struct.unpack(">IIBBBBB", c)
        elif t == b"IDAT": idat += c
    assert bd == 8 and ct in (2, 6) and il == 0, f"{path}: unsupported PNG {bd}/{ct}/{il}"
    bpp = 4 if ct == 6 else 3; stride = w * bpp; raw = zlib.decompress(idat); prev = bytearray(stride); p = 0
    for _ in range(h):
        f = raw[p]; line = bytearray(raw[p + 1:p + 1 + stride]); p += 1 + stride
        if f == 1:
            for x in range(bpp, stride): line[x] = (line[x] + line[x - bpp]) & 255
        elif f == 2:
            for x in range(stride): line[x] = (line[x] + prev[x]) & 255
        elif f == 3:
            for x in range(stride): line[x] = (line[x] + ((line[x - bpp] if x >= bpp else 0) + prev[x]) // 2) & 255
        elif f == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0; b = prev[x]; cc = prev[x - bpp] if x >= bpp else 0
                pa, pb, pc = abs(b - cc), abs(a - cc), abs(a + b - 2 * cc)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else cc)) & 255
        yield line[0::bpp], line[1::bpp], line[2::bpp]; prev = line

def share(path):
    tot = mag = 0
    for r, g, b in pixels(path):
        for x in range(0, len(r), 2):  # every 2nd pixel is plenty
            tot += 1
            if r[x] > 200 and b[x] > 200 and g[x] < 70: mag += 1
    return mag / max(tot, 1)

if __name__ == "__main__":
    lim = float(sys.argv[1]); bad = 0
    for f in sys.argv[2:]:
        s = share(f); print(f"{f}: magenta share {s:.4f}")
        if s > lim: print(f"::error::BUG-23 magenta {s:.1%} > {lim:.0%} in {f}"); bad += 1
    sys.exit(1 if bad else 0)
