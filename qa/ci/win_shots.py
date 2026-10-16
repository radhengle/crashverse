#!/usr/bin/env python3
"""Print per-shot aims for a level from levels/ch1/ci_win_shots.json (PR #13): one line per shot,
'yaw pitch fx fy tx ty' (swipe px at 1080x2340). Exit 2 if file/level missing (caller falls back to l1_aim.env).
Usage: win_shots.py <json> <level_id> [gscale=1.0]"""
import json, sys
try:
    path, lid = sys.argv[1], sys.argv[2]; g = sys.argv[3] if len(sys.argv) > 3 else "1.0"
    lvl = next(l for l in json.load(open(path))["levels"] if l["id"] == lid)
    for s in sorted(lvl["shots"], key=lambda s: s["shot"]):
        a = s["aim"][g]; sw = a["swipe"]
        print(a.get("hintYaw", a["yaw"]), a.get("hintPitch", a["elevation"]), *sw)
except Exception as e:
    print(f"win_shots: {e}", file=sys.stderr); sys.exit(2)
