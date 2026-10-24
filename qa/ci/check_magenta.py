#!/usr/bin/env python3
"""Fail if any screenshot has too many near-#FF00FF pixels (missing/null material, BUG-23).
Usage: check_magenta.py [--threshold 0.002] [--tol 40] img1.png [img2.png ...]
threshold = max allowed fraction of magenta-ish pixels per image."""
import argparse, sys
from PIL import Image
ap = argparse.ArgumentParser()
ap.add_argument("--threshold", type=float, default=0.002)
ap.add_argument("--tol", type=int, default=40)
ap.add_argument("images", nargs="+")
a = ap.parse_args()
bad = 0
for p in a.images:
    im = Image.open(p).convert("RGB"); im.thumbnail((540, 1170))
    raw = im.tobytes(); n = len(raw) // 3; hi, lo = 255 - a.tol, a.tol
    m = sum(1 for i in range(0, len(raw), 3) if raw[i] >= hi and raw[i + 2] >= hi and raw[i + 1] <= lo)
    frac = m / n if n else 0
    status = "FAIL" if frac > a.threshold else "ok"
    print(f"{status} {p}: magenta {frac:.4%} (limit {a.threshold:.4%})")
    bad += status == "FAIL"
if bad: print(f"::error::{bad} screenshot(s) show magenta / missing material"); sys.exit(1)
