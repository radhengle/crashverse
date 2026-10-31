#!/usr/bin/env python3
"""Derive hint aim angles (hintYaw, hintPitch) from each level's hintTarget piece. Never hand-edit the angles.

Geometry (code, not guesses): Wrecker at x=WreckerSpec.WreckerX (-20), barrel pivot y=1.5, projectile spawn 1.95 along the
barrel (SpawnAlong), barrel +x rotated by elevation about z, then by yaw about y (AimController: Euler(0,-yaw,0), so +yaw → +z).
Layout block positions are relative to GameSession.structureBase = (WreckerX + level.targetDistance, 0, 0) (default 30 m -> (10,0,0)). launchSpeed from wrecker.json, g = 9.81 × gravityScale.
Low-arc ballistic solve, no drag; clamped to the aim envelope (elevation 5–70°, yaw ±60°) and rounded to 0.5°.

Usage: derive_hints.py [chapter.json] [--layouts DIR] [--check] [--season-only]
  Default: Resources/Data/chapter1.json with Resources/Data/Layouts. Only hintYaw/hintPitch are written; nothing else in the
  Level Designer's data is touched. CI runs --check (logic tests), so a moved hintTarget piece fails until re-derived.
"""
import json, math, os, sys, pathlib
ROOT = pathlib.Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/_Project/Resources/Data"
WRECKER_X, PIVOT_Y, SPAWN_ALONG = -20.0, 1.5, 1.95
BASE = (10.0, 0.0, 0.0)
MIN_EL, MAX_EL, MAX_YAW = 5.0, 70.0, 60.0

def muzzle(yaw, el):
    y, e = math.radians(yaw), math.radians(el)
    d = (math.cos(e) * math.cos(y), math.sin(e), math.cos(e) * math.sin(y))
    return tuple(p + SPAWN_ALONG * c for p, c in zip((WRECKER_X, PIVOT_Y, 0.0), d))

def solve(target, v, g):
    yaw, el = 0.0, 20.0
    for _ in range(20):
        m = muzzle(yaw, el)
        dx, dy, dz = (t - a for t, a in zip(target, m))
        yaw = math.degrees(math.atan2(dz, dx))
        r = math.hypot(dx, dz)
        disc = v**4 - g * (g * r * r + 2 * dy * v * v)
        if disc < 0: return None  # out of range
        el = math.degrees(math.atan((v * v - math.sqrt(disc)) / (g * r)))
    rnd = lambda a: round(a * 2) / 2
    return rnd(max(-MAX_YAW, min(MAX_YAW, yaw))), rnd(max(MIN_EL, min(MAX_EL, el)))

def derive(levels, layouts_dir, v, g):
    out = {}
    for L in levels:
        t = L.get("hintTarget") or ""
        if not t: continue
        lay = json.load(open(os.path.join(layouts_dir, L["layout"] + ".json")))
        b = next((b for b in lay["blocks"] if b["id"] == t), None)
        if b is None: raise SystemExit(f"{L['id']}: hintTarget '{t}' not in layout {L['layout']}")
        d = float(L.get("targetDistance") or 30.0); d = max(12.0, min(40.0, d))  # ArenaLayoutRules.Distance
        base = (WRECKER_X + d, BASE[1], BASE[2])
        s = solve(tuple(base[i] + b["p"][i] for i in range(3)), v, g)
        if s is None: raise SystemExit(f"{L['id']}: target '{t}' out of range at {v} m/s")
        out[L["id"]] = s
    return out

def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    chap = pathlib.Path(args[0]) if args else DATA / "chapter1.json"
    lay = sys.argv[sys.argv.index("--layouts") + 1] if "--layouts" in sys.argv else str(DATA / "Layouts")
    season_p = DATA / "season_s1.json"
    v = json.load(open(DATA / "wrecker.json")).get("launchSpeed", 28.0)
    doc = json.load(open(chap))
    season = json.load(open(season_p)) if season_p.exists() and not args else None
    groups = [(doc["levels"], 1.0)]
    if season:
        # BUG-08: event levels reuse the campaign hint target of the same layout; angles derived under the week's gravity.
        by_layout = {L["layout"]: L.get("hintTarget", "") for L in doc["levels"]}
        for w in season.get("weeks", []):
            for L in w["levels"]: L["hintTarget"] = by_layout.get(L["layout"], L.get("hintTarget", ""))
            groups.append((w["levels"], (w.get("mod") or {}).get("gravityScale") or 1.0))
        fin = season.get("finale")
        if fin: fin["hintTarget"] = by_layout.get(fin["layout"], fin.get("hintTarget", "")); groups.append(([fin], 1.0))
    stale = []
    for levels, gs in groups:
        res = derive(levels, lay, v, 9.81 * gs)
        for L in levels:
            if L["id"] in res and (L.get("hintYaw"), L.get("hintPitch")) != res[L["id"]]:
                stale.append((L["id"], (L.get("hintYaw"), L.get("hintPitch")), res[L["id"]]))
                L["hintYaw"], L["hintPitch"] = res[L["id"]]
    # Daily Boss / ranked Titan arenas (titan_templates.json): the arena hint is derived the same way (gravity x1.0).
    titan_p = DATA / "titan_templates.json"
    titan = json.load(open(titan_p)) if titan_p.exists() and not args else None
    if titan:
        res = derive([dict(id=a["id"], layout=a["layout"], hintTarget=a.get("hintTarget", "")) for a in titan.get("arenas", [])], lay, v, 9.81)
        for a in titan.get("arenas", []):
            if a["id"] in res and (a.get("hintYaw"), a.get("hintPitch")) != res[a["id"]]:
                stale.append(("titan:" + a["id"], (a.get("hintYaw"), a.get("hintPitch")), res[a["id"]]))
                a["hintYaw"], a["hintPitch"] = res[a["id"]]
    if "--check" in sys.argv:
        for x in stale: print("STALE", *x)
        print("hints up to date" if not stale else f"{len(stale)} stale hint(s): run python3 Tools/ContentGen/derive_hints.py")
        sys.exit(1 if stale else 0)
    if "--season-only" not in sys.argv: json.dump(doc, open(chap, "w"), indent=1)
    elif any(x[0] in {L["id"] for L in doc["levels"]} for x in stale): print("NOTE: chapter hints are stale; Level Designer owns chapter1.json, run derive_hints.py there")
    if season: json.dump(season, open(season_p, "w"), indent=1)
    if titan: json.dump(titan, open(titan_p, "w"), indent=1)
    for x in stale: print("updated", *x)
    print(f"{len(stale)} hint(s) updated")

if __name__ == "__main__": main()
