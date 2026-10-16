#!/usr/bin/env python3
"""Build ordered win-aim candidates for a level. Writes <out>/win_plan_<n>.txt (lines 'yaw pitch fx fy tx ty', one per shot;
last line repeats) and win_plan_<n>.label; prints the candidate count.
Order: 1) ci_win_shots.json per-shot aims (PR #13, Level Designer = data owner)  2) chapter1.json hintYaw/hintPitch, only if it disagrees.
Swipe fallback coords come from ci_win_shots.json, else from --swipe.
Usage: win_shots.py --level c1_l01 --out DIR [--chapter PATH] [--shots PATH] [--gscale 1.0] [--swipe fx,fy,tx,ty] [--yaw Y --pitch P]"""
import argparse, json, os, sys
ap = argparse.ArgumentParser()
ap.add_argument("--level", required=True); ap.add_argument("--out", required=True)
ap.add_argument("--chapter", default="Assets/_Project/Resources/Data/chapter1.json")
ap.add_argument("--shots", default="levels/ch1/ci_win_shots.json"); ap.add_argument("--gscale", default="1.0")
ap.add_argument("--swipe", default=""); ap.add_argument("--yaw"); ap.add_argument("--pitch")
a = ap.parse_args()
def load(p):
    try: return json.load(open(p))
    except Exception as e: print(f"win_shots: {p}: {e}", file=sys.stderr); return None
env_sw = a.swipe.split(",") if a.swipe.count(",") == 3 and all(a.swipe.split(",")) else []
cands = []
# ci_win_shots.json
shots_rows = []
d = load(a.shots)
if d:
    lvl = next((l for l in d.get("levels", []) if l.get("id") == a.level), None)
    if lvl:
        for s in sorted(lvl["shots"], key=lambda s: s["shot"]):
            x = s["aim"][a.gscale]; shots_rows.append([x.get("hintYaw", x["yaw"]), x.get("hintPitch", x["elevation"]), *x["swipe"]])
sw = shots_rows[0][2:] if shots_rows else env_sw
# 1) ci_win_shots.json (PR #13, LD-owned primary)
if shots_rows: cands.append(("ci_win_shots.json", shots_rows))
# 2) chapter1.json hint, only if it disagrees with #13
c = load(a.chapter)
if c:
    levels = c.get("levels", c) if isinstance(c, dict) else c
    l = next((l for l in levels if l.get("id") == a.level), None)
    if l and "hintYaw" in l:
        row = [l["hintYaw"], l["hintPitch"], *sw]
        if not shots_rows or [float(v) for v in shots_rows[0][:2]] != [float(row[0]), float(row[1])]:
            cands.append(("chapter1.json hint", [row]))
if not cands and a.yaw is not None: cands.append(("l1_aim.env", [[a.yaw, a.pitch, *env_sw]]))
os.makedirs(a.out, exist_ok=True)
for i, (lab, rows) in enumerate(cands, 1):
    open(f"{a.out}/win_plan_{i}.txt", "w").write("".join(" ".join(str(v) for v in r) + "\n" for r in rows))
    open(f"{a.out}/win_plan_{i}.label", "w").write(lab)
    print(f"candidate {i}: {lab}: {rows}", file=sys.stderr)
print(len(cands))
