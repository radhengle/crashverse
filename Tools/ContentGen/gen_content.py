#!/usr/bin/env python3
"""Generates bundled CRASHVERSE content JSON from Design Pack v1.
Designers may edit the JSON directly; re-running this overwrites it.
Output: Assets/_Project/Resources/Data/**.json (loaded via Resources at runtime)."""
import json, os
OUT = os.path.join(os.path.dirname(__file__), "../../Assets/_Project/Resources/Data")
LAY = os.path.join(OUT, "Layouts")
# Level Rulings (developer-levels 60ba5d1) are canonical for chapter1.json and season_s1.json (shots, star bands, win
# thresholds). This script never overwrites them once they exist; pass --regenerate-level-data only to rebuild from scratch.
import sys as _sys
LEVEL_RULINGS_OWNED = {"chapter1", "season_s1"}
REGEN_LEVEL_DATA = "--regenerate-level-data" in _sys.argv
# Level Designer owns Chapter 1 (levels/ch1, branch level-designer/ch1-levels). When that folder exists this script never
# writes chapter1.json or any Ch1 layout; Ch1 data comes only from the Level Designer's pipeline.
LD_CH1 = os.path.join(os.path.dirname(__file__), "../../levels/ch1")
LD_OWNS_CH1 = os.path.isdir(LD_CH1)
LD_LAYOUTS = set(os.path.splitext(f)[0] for f in os.listdir(os.path.join(LD_CH1, "layouts"))) if os.path.isdir(os.path.join(LD_CH1, "layouts")) else set()
os.makedirs(LAY, exist_ok=True)

def B(id, mat, p, s, sup=(), joint=False, role="", nonObj=False, color="", explosive=False,
      indestructible=False, spin=None):
    b = {"id": id, "mat": mat, "p": list(p), "s": list(s), "sup": list(sup), "joint": joint,
         "role": role, "nonObj": nonObj, "color": color, "explosive": explosive,
         "indestructible": indestructible, "spinAxis": [], "pivot": [], "spinDps": 0.0}
    if spin:
        b["spinAxis"], b["pivot"], b["spinDps"] = spin
    return b

def face_wrecker(b):
    """BUG-24 (same transform as levels/ch1/gen_levels.py): the Wrecker fires along +x, so a layout authored with its
    front on +z is turned to face -x: (x,y,z) -> (-z,y,x), sizes swap x/z."""
    x, y, z = b["p"]; sx, sy, sz = b["s"]; b = dict(b); b["p"] = [round(-z, 4), y, x]; b["s"] = [sz, sy, sx]; return b

def save_layout(id, blocks):
    if LD_OWNS_CH1 and (id in LD_LAYOUTS or id.startswith("c1_") or id == "m1_clockwork_tower"):
        return  # Level Designer's file: never overwritten
    if id.startswith("titan_"):
        blocks = [face_wrecker(b) for b in blocks]  # Daily Boss / Titan arenas face the cannon (fixed behind-cannon camera)
    ids = {b["id"] for b in blocks}
    for b in blocks:
        for s in b["sup"]:
            assert s in ids, (id, b["id"], s)
    with open(os.path.join(LAY, id + ".json"), "w") as f:
        json.dump({"id": id, "blocks": blocks}, f, indent=1)

# ---------- Chapter 1 layouts (local coords, structure root placed ~30u from Wrecker) ----------
def L1():
    b = [B("floor", "wood", (0, .2, 0), (3, .4, 3))]
    for i, (x, z) in enumerate([(-1.3, -1.3), (1.3, -1.3), (-1.3, 1.3), (1.3, 1.3)]):
        b.append(B(f"post{i}", "wood", (x, 1.6, z), (.35, 2.4, .35), ["floor"], role="support"))
    b.append(B("deck", "wood", (0, 2.95, 0), (3.2, .3, 3.2), ["post0", "post1", "post2", "post3"]))
    b.append(B("wallF", "wood", (0, 3.8, 1.4), (2.8, 1.4, .2), ["deck"], role="wall"))
    b.append(B("wallB", "wood", (0, 3.8, -1.4), (2.8, 1.4, .2), ["deck"], role="wall"))
    b.append(B("roof", "wood", (0, 4.7, 0), (3.4, .35, 3.4), ["wallF", "wallB"], color="4D7BC4"))
    return b

def L2():
    b = []
    for i, (x, z) in enumerate([(-1.2, -1.2), (1.2, -1.2), (-1.2, 1.2), (1.2, 1.2)]):
        b.append(B(f"stilt{i}", "wood", (x, 2.5, z), (.35, 5, .35), [], role="support"))
    b.append(B("braceF", "wood", (0, 2, 1.2), (2.4, .25, .25), ["stilt2", "stilt3"], role="support"))
    b.append(B("braceB", "wood", (0, 2, -1.2), (2.4, .25, .25), ["stilt0", "stilt1"], role="support"))
    b.append(B("platform", "wood", (0, 5.15, 0), (3, .3, 3), ["stilt0", "stilt1", "stilt2", "stilt3"]))
    b.append(B("tank", "wood", (0, 6.6, 0), (2.4, 2.6, 2.4), ["platform"], role="tank"))
    b.append(B("tankLid", "wood", (0, 8.05, 0), (2.6, .3, 2.6), ["tank"], role="tank", color="4D7BC4"))
    return b

def L3():
    b = [B("base", "brick", (0, .4, 0), (5, .8, 3.5))]
    b += [B("wallL", "brick", (-2.2, 2, 0), (.5, 2.4, 3.2), ["base"], role="wall"),
          B("wallR", "brick", (2.2, 2, 0), (.5, 2.4, 3.2), ["base"], role="wall"),
          B("wallB", "brick", (0, 2, -1.5), (3.9, 2.4, .4), ["base"], role="wall"),
          B("propL", "wood", (-1.0, 2, 1.4), (.35, 2.4, .35), ["base"], role="support"),
          B("propR", "wood", (1.0, 2, 1.4), (.35, 2.4, .35), ["base"], role="support"),
          B("lintel", "wood", (0, 3.35, 1.4), (4.6, .3, .4), ["propL", "propR"], role="support"),
          B("roofSlab", "brick", (0, 3.75, 0), (5, .5, 3.6), ["lintel", "wallB"]),
          B("chimney", "brick", (1.5, 4.8, -.8), (.7, 1.6, .7), ["roofSlab"])]
    return b

def L4():
    b = [B("base", "brick", (0, 1.5, 0), (3, 3, 3), role="wall"),
         B("mid", "brick", (0, 4.25, 0), (2.4, 2.5, 2.4), ["base"], role="wall"),
         B("cap", "wood", (0, 6, 0), (2.8, 1, 2.8), ["mid"], role="support"),
         B("hub", "wood", (1.6, 6, 0), (.6, .8, .8), ["cap"], role="support")]
    piv = [1.9, 6, 0]
    for i, (dy, dz, sy, sz) in enumerate([(2, 0, 3, .5), (-2, 0, 3, .5), (0, 2, .5, 3), (0, -2, .5, 3)]):
        b.append(B(f"sail{i}", "wood", (1.95, 6 + dy, dz), (.15, sy, sz), ["hub"], role="sail",
                   spin=([1, 0, 0], piv, 35.0)))
    return b

def L5():
    b = [B("pierL", "brick", (-3.2, 1.5, 0), (1.2, 3, 1.6)),
         B("pierR", "brick", (3.2, 1.5, 0), (1.2, 3, 1.6)),
         B("deck", "wood", (0, 3.15, 0), (7.6, .3, 1.8), ["pierL", "pierR"], role="bridge")]
    for side, x in (("A", -1.7), ("B", 1.7)):
        b += [B(f"cot{side}", "brick", (x, 4.1, 0), (1.8, 1.6, 1.5), ["deck"], role="target"),
              B(f"roof{side}", "wood", (x, 5.1, 0), (2.0, .4, 1.7), [f"cot{side}"], role="target",
                color="4D7BC4")]
    return b

def L6():
    b = [B("plinth", "brick", (0, .5, 0), (4, 1, 4))]
    below = "plinth"; y = 1.0
    for i in range(3):
        seg = f"silo{i}"
        b.append(B(seg, "brick", (0, y + 1.0, 0), (2.6, 2.0, 2.6), [below]))
        j = f"brace{i}"
        b.append(B(j, "steel", (0, y + 2.2, 0), (3.0, .4, 3.0), [seg], joint=True, role="steel_joint"))
        below = j; y += 2.4
    b.append(B("roof", "brick", (0, y + .4, 0), (3.2, .8, 3.2), [below], color="4D7BC4"))
    return b

def L7():  # identical to M1 Clockwork Tower greybox
    b = [B("plinth", "brick", (0, .6, 0), (4.5, 1.2, 4.5)),
         B("wingL", "wood", (-3.2, 2.2, 0), (1.2, 2.8, .5), ["plinth"], role="wing"),
         B("wingR", "wood", (3.2, 2.2, 0), (1.2, 2.8, .5), ["plinth"], role="wing"),
         B("braceL", "wood", (-2.2, 1.8, .8), (1.6, .3, .3), ["wingL", "plinth"], role="wing"),
         B("braceR", "wood", (2.2, 1.8, .8), (1.6, .3, .3), ["wingR", "plinth"], role="wing")]
    jy = [3.5, 5.5, 7.5]; below = "plinth"
    for i in range(3):
        y0 = 1.4 if i == 0 else jy[i - 1] + .35; y1 = jy[i] - .35; m = (y0 + y1) / 2; h = max(.6, y1 - y0)
        w = [B(f"wallF{i}", "brick", (0, m, 1.1), (2.2, h, .35), [below], role="wall"),
             B(f"wallB{i}", "brick", (0, m, -1.1), (2.2, h, .35), [below], role="wall"),
             B(f"wallL{i}", "brick", (-1.1, m, 0), (.35, h, 2.2), [below], role="wall"),
             B(f"wallR{i}", "brick", (1.1, m, 0), (.35, h, 2.2), [below], role="wall")]
        b += w
        b.append(B(f"ring{i}", "steel", (0, jy[i], 0), (2.6, .45, 2.6), [x["id"] for x in w],
                   joint=True, role="steel_joint"))
        below = f"ring{i}"
    b += [B("crownFloor", "brick", (0, 8.3, 0), (3.2, .35, 3.2), [below], role="crown"),
          B("crownWood", "wood", (0, 9.2, 0), (2.8, 1.4, 2.8), ["crownFloor"], role="crown"),
          B("crownBrick", "brick", (0, 10.3, 0), (2.4, .8, 2.4), ["crownWood"], role="crown"),
          B("crownRoof", "brick", (0, 11.1, 0), (3, .7, 3), ["crownBrick"], role="crown", color="4D7BC4"),
          B("barrelDecor", "wood", (5.5, .6, 2), (.8, 1.2, .8), [], nonObj=True, color="E5484D")]
    return b

def L8():
    b = []
    xs = [-6, -3, 0, 3, 6]
    for i, x in enumerate(xs):
        b.append(B(f"col{i}", "brick", (x, 1.75, 0), (.8, 3.5, .8), [], role="support"))
    for i in range(4):
        x = (xs[i] + xs[i + 1]) / 2
        b.append(B(f"beam{i}", "wood", (x, 3.75, 0), (3.2, .4, 1.2), [f"col{i}", f"col{i+1}"]))
    b.append(B("joint", "steel", (0, 4.2, 0), (1.4, .5, 1.4), ["beam1", "beam2"], joint=True, role="steel_joint"))
    for i in range(4):
        x = (xs[i] + xs[i + 1]) / 2
        b.append(B(f"stall{i}", "wood", (x, 4.9, 0), (2.4, 1.0, 1.0), [f"beam{i}"] + (["joint"] if i in (1, 2) else [])))
    b.append(B("crate", "wood", (7.5, .5, 1.5), (.9, .9, .9), [], nonObj=True, color="E5484D", explosive=True))
    return b

def L9():
    b = [B(f"sea{i}", "brick", (-4 + i * 2, .75, 2.5), (2, 1.5, .8), [], role="seawall") for i in range(5)]
    b += [B("lhBase", "brick", (0, 1.25, -1), (3, 2.5, 3)),
          B("lhShaft0", "brick", (0, 3.5, -1), (2.2, 2, 2.2), ["lhBase"]),
          B("lhRing", "steel", (0, 4.75, -1), (2.6, .5, 2.6), ["lhShaft0"], joint=True, role="steel_joint"),
          B("lhShaft1", "brick", (0, 6, -1), (2.0, 2, 2.0), ["lhRing"]),
          B("lhGallery", "wood", (0, 7.2, -1), (2.8, .4, 2.8), ["lhShaft1"]),
          B("lhLantern", "wood", (0, 8.1, -1), (1.6, 1.4, 1.6), ["lhGallery"], role="lantern"),
          B("lhCap", "brick", (0, 9.1, -1), (1.9, .6, 1.9), ["lhLantern"], color="4D7BC4")]
    return b

def L10():
    b = [B("stack", "brick", (0, 1.5, 0), (5, 3, 4), [], nonObj=True, indestructible=True, color="5A2A20"),
         B("hips", "brick", (0, 3.6, 0), (3, 1.2, 2.2), ["stack"]),
         B("torso", "brick", (0, 5.6, 0), (3.4, 2.8, 2.4), ["hips"]),
         B("shoulderL", "steel", (-2.2, 6.6, 0), (1.0, 1.0, 1.0), ["torso"], joint=True, role="shoulder"),
         B("shoulderR", "steel", (2.2, 6.6, 0), (1.0, 1.0, 1.0), ["torso"], joint=True, role="shoulder"),
         B("armL", "brick", (-2.4, 4.9, 0), (.8, 2.4, .8), ["shoulderL"], role="arm"),
         B("armR", "brick", (2.4, 4.9, 0), (.8, 2.4, .8), ["shoulderR"], role="arm"),
         B("armorTop", "wood", (0, 6.3, 1.35), (3.2, .9, .3), ["torso"], role="armor"),
         B("armorBot", "wood", (0, 5.1, 1.35), (3.2, .9, .3), ["torso"], role="armor"),
         B("core", "steel", (0, 5.7, 1.0), (1.0, 1.0, .4), ["torso"], role="core", indestructible=True, color="FFB347"),
         B("shutter", "brick", (0, 5.7, 1.75), (1.4, 1.4, .25), [], role="shutter", nonObj=True, indestructible=True),
         B("head", "wood", (0, 7.6, 0), (1.6, 1.2, 1.6), ["torso"]),
         B("lamp", "wood", (0, 8.5, 0), (1.0, .6, 1.0), ["head"], color="E8B04B")]
    return b

def TITAN(variant):
    # Emberforge Titan colossus, 4 handcrafted arena variants
    w = [1.0, 1.15, .9, 1.25][variant]; leg = ["steel", "brick", "steel", "brick"][variant]
    # Level Designer tuning 2026-10-30: steel legs are gold joints (2x direct) so every arena's best line is the same
    # 2-shot leg topple (steel non-joint legs made forge/slag a 3-shot titan that could only 3-star on 8-shot days).
    b = [B("legL", leg, (-1.2 * w, 2, 0), (1, 4, 1), [], joint=leg == "steel", role="leg"),
         B("legR", leg, (1.2 * w, 2, 0), (1, 4, 1), [], joint=leg == "steel", role="leg"),
         B("pelvis", "steel", (0, 4.3, 0), (3.2 * w, .6, 1.4), ["legL", "legR"], joint=True, role="steel_joint"),
         B("belly", "brick", (0, 5.6, 0), (2.8 * w, 2, 1.8), ["pelvis"]),
         B("chest", "brick", (0, 7.6, 0), (3.4 * w, 2, 2), ["belly"]),
         B("neck", "steel", (0, 8.9, 0), (1, .6, 1), ["chest"], joint=True, role="steel_joint"),
         B("head", "wood", (0, 9.8, 0), (1.8, 1.2, 1.6), ["neck"], color="E5484D"),
         B("shL", "steel", (-2.2 * w, 8.2, 0), (.9, .9, .9), ["chest"], joint=True, role="steel_joint"),
         B("shR", "steel", (2.2 * w, 8.2, 0), (.9, .9, .9), ["chest"], joint=True, role="steel_joint"),
         B("armL", "brick", (-2.4 * w, 6.5, 0), (.8, 2.6, .8), ["shL"]),
         B("armR", "brick", (2.4 * w, 6.5, 0), (.8, 2.6, .8), ["shR"])]
    if variant in (1, 3):
        b.append(B("crate", "wood", (3.5, .5, 1.5), (.9, .9, .9), [], nonObj=True, color="E5484D", explosive=True))
    return b

for fn, lid in [(L1, "c1_l01_lookout_hut"), (L2, "c1_l02_stilt_tower"), (L3, "c1_l03_bakery"),
                (L4, "c1_l04_windmill"), (L5, "c1_l05_twin_cottages"), (L6, "c1_l06_granary"),
                (L7, "m1_clockwork_tower"), (L8, "c1_l08_market_arcade"), (L9, "c1_l09_lighthouse_gauntlet"),
                (L10, "c1_l10_tidewarden")]:
    save_layout(lid, fn())
TITAN_ARENAS = [("forge_floor", "Forge Floor"), ("caldera_rim", "Caldera Rim"),
                ("slag_foundry", "Slag Foundry"), ("ash_bridge", "Ash Bridge")]
for i, (aid, _) in enumerate(TITAN_ARENAS):
    save_layout("titan_" + aid, TITAN(i))

def M(type, value=0, text="", role=""):
    return {"type": type, "value": value, "role": role, "text": text}

HINTS = {1: ("post0", 4, 22), 2: ("stilt2", 3, 20), 3: ("propL", -2, 18), 4: ("cap", 2, 28), 5: ("pierL", -6, 16),
         6: ("brace0", 0, 24), 7: ("ring0", 0, 24), 8: ("joint", 0, 26), 9: ("lhRing", 0, 26)}

def LV(n, island, name, layout, mats, shots, teaches, mastery, forced=(), locked=(), boss="", reward=()):
    return {"id": f"c1_l{n:02d}", "number": n, "island": island, "name": name, "layout": layout,
            "objective": "BOSS" if boss else "DESTROY_STRUCTURE", "shots": shots, "winThreshold": .85,
            "materials": mats, "teaches": teaches, "mastery": mastery,
            "forced": [{"slot": s, "powerup": p} for s, p in forced], "lockedSlots": list(locked),
            "requires": f"c1_l{n-1:02d}" if n > 1 else "", "bossId": boss, "reward": list(reward),
            "star3Pct": .60, "star2Pct": .30, "starCapIfPowerupUsed": 2 if mastery["type"] == "NO_POWERUP" else 3, "islandKillY": -2.0,
            "hintTarget": HINTS.get(n, ("", 0, 0))[0], "hintYaw": HINTS.get(n, ("", 0, 0))[1], "hintPitch": HINTS.get(n, ("", 0, 0))[2]}

W, Bk, S = "wood", "brick", "steel"
chapter = {
  "id": "c1_sunnyville_isles", "name": "Sunnyville Isles", "nextRegion": "Frozen Peaks",
  "islands": [
    {"id": "harbor", "name": "Harbor Isle", "pos": [0, 0, 0], "gateAfterLevel": ""},
    {"id": "mill", "name": "Mill Isle", "pos": [22, 3, 8], "gateAfterLevel": "c1_l03"},
    {"id": "clocktower", "name": "Clocktower Isle", "pos": [44, 6, 0], "gateAfterLevel": "c1_l06"},
    {"id": "storm", "name": "Storm Isle", "pos": [64, 10, 10], "gateAfterLevel": "c1_l09"}],
  "levels": [
    LV(1, "harbor", "Royal Watch Post", "c1_l01_lookout_hut", [W], 4, "Aim + fire, trajectory arc", M("CLEAR_IN_SHOTS", 1, "Clear in 1 shot")),
    LV(2, "harbor", "King's Cistern", "c1_l02_stilt_tower", [W], 4, "Hit supports, not walls", M("ROLE_BELOW_HEIGHT", 0, "Topple tank into sea (off the island)", "tank")),
    LV(3, "harbor", "Royal Bakery", "c1_l03_bakery", [W, Bk], 5, "Brick needs support loss", M("DESTROYED_AT_LEAST", .95, ">=95% destroyed")),
    LV(4, "mill", "Crown Mill", "c1_l04_windmill", [W, Bk], 5, "Collapse chains between pieces", M("THREE_STARS_NO_ROLE_HITS", 0, "3 stars with no wall hits", "wall")),
    LV(5, "mill", "Tax Collectors' Lodges", "c1_l05_twin_cottages", [W, Bk], 5, "One shot, two targets (bridge)", M("CLEAR_IN_SHOTS", 1, "Both in 1 shot")),
    LV(6, "mill", "Royal Granary", "c1_l06_granary", [Bk, S], 5, "Steel joints take 2x damage", M("ALL_ROLE_BROKEN", 0, "Break all 3 joints", "steel_joint")),
    LV(7, "clocktower", "Curfew Clocktower", "m1_clockwork_tower", [W, Bk, S], 5, "Joint sequencing; first power-up pick (Earthquake)", M("ROLE_INTACT", 0.8, "Crown falls intact", "crown"), forced=[("Core", "earthquake")], locked=["Module", "Booster"]),
    LV(8, "clocktower", "Royal Market Arcade", "c1_l08_market_arcade", [W, Bk, S], 5, "Choose loadout deliberately (Multi Shot)", M("STARS", 3, "3 stars"), forced=[("Module", "multi_shot")]),
    LV(9, "clocktower", "Sea-Wall Beacon", "c1_l09_lighthouse_gauntlet", [W, Bk, S], 6, "Tests everything; tight budget", M("NO_POWERUP", 0, "No power-up used")),
    LV(10, "storm", "The King's Palace", "c1_l10_tidewarden", [W, Bk, S], 7, "Boss phases + weak points", M("SHOTS_LEFT_AT_LEAST", 3, "Defeat with 3 shots left"), boss="tidewarden",
       reward=[{"type": "coins", "amount": 500, "id": ""}, {"type": "powerup", "amount": 1, "id": "black_hole"}, {"type": "gate", "amount": 1, "id": "frozen_peaks"}])]}

def P(id, name, slot, effect, charges, utype, ulevel, utext, **p):
    base = {"damage": 0, "linkDepth": 0, "count": 0, "damageMult": 1, "radius": 0, "duration": 0,
            "burstDamage": 0, "passCount": 0, "slowFactor": 1}
    base.update(p)
    return {"id": id, "name": name, "slot": slot, "effect": effect, "charges": charges,
            "unlockType": utype, "unlockLevelId": ulevel, "unlockText": utext, "p": base}

# Power-ups v2 (design/Powerups_v2_Spec.md): the designer's JSON is the source of truth, copied as-is.
powerups = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../design/data/powerups_v2.json")))

def R(type, amount=0, id=""):
    return {"type": type, "amount": amount, "id": id}

def MOD(**k):
    m = {"steelJointMult": 0, "woodHpMult": 0, "brickHpMult": 0, "steelHpMult": 0, "gravityScale": 0, "shotDelta": 0}
    m.update(k); return m

titan = {"archetype": "emberforge_titan", "name": "Emberforge Titan", "resetUtcHour": 0, "rankedGraceMinutes": 15,
         "maxDamageScore": 10000, "perShotLeftBonus": 500, "baseShots": 6,
         "offlineShotDamageCap": 5000,  # SEC-01: max damage gain per unsigned (offline) shot accepted on flush
         "arenas": [{"id": a, "name": n, "layout": "titan_" + a, "hintTarget": "legL", "hintYaw": 0, "hintPitch": 22} for a, n in TITAN_ARENAS],
         "rules": [
            {"id": "heated_steel", "text": "Steel is heated: joints take 3x damage", "mod": MOD(steelJointMult=3)},
            {"id": "brittle_brick", "text": "Cracked kilns: brick HP x0.6", "mod": MOD(brickHpMult=.6)},
            {"id": "petrified_wood", "text": "Petrified wood: wood HP x2", "mod": MOD(woodHpMult=2)},
            {"id": "low_gravity", "text": "Thermal updraft: gravity x0.6", "mod": MOD(gravityScale=.6)},
            {"id": "tight_budget", "text": "Ration the cannon: -1 shot", "mod": MOD(shotDelta=-1)},
            {"id": "tempered_steel", "text": "Tempered plates: steel HP x1.5, joints 2.5x", "mod": MOD(steelHpMult=1.5, steelJointMult=2.5)}],
         "mastery": M("SHOTS_LEFT_AT_LEAST", 2, "Topple with 2 shots left"),
         "completionRewards": [R("coins", 150), R("shard", 1)], "topPercent": .10, "topPercentBonusShards": 3}

daily = {"days": [R("coins", 100), R("charge", 1), R("coins", 150), R("shard", 1), R("coins", 200), R("charge", 1),
                  R("chest", 1, "coins:300;shard:3;gems:10")]}

def EL(eid, n, layout, shots):
    return {"id": f"{eid}_e{n}", "number": n, "island": "event", "name": f"Event {n}", "layout": layout,
            "objective": "DESTROY_STRUCTURE", "shots": shots, "winThreshold": .85, "materials": [W, Bk, S],
            "teaches": "", "mastery": M("STARS", 3, "3 stars"), "forced": [], "lockedSlots": [], "requires": "",
            "bossId": "", "reward": [], "star3Pct": .60, "star2Pct": .30, "starCapIfPowerupUsed": 3, "islandKillY": -2.0, "hintTarget": LAYOUT_HINT.get(layout, ("", 0, 0))[0],
            "hintYaw": LAYOUT_HINT.get(layout, ("", 0, 0))[1], "hintPitch": LAYOUT_HINT.get(layout, ("", 0, 0))[2]}  # BUG-08: events reuse campaign hints
LAYOUT_HINT = {"c1_l01_lookout_hut": HINTS[1], "c1_l03_bakery": HINTS[3], "c1_l06_granary": HINTS[6],
               "c1_l08_market_arcade": HINTS[8], "c1_l09_lighthouse_gauntlet": HINTS[9], "m1_clockwork_tower": HINTS[7]}
lays = ["c1_l01_lookout_hut", "c1_l03_bakery", "c1_l06_granary", "c1_l08_market_arcade", "c1_l09_lighthouse_gauntlet"]
weeks = []
for wid, name, text, mod in [("magnetic_mayhem", "Magnetic Mayhem", "Magnetised steel: joints take 3x", MOD(steelJointMult=3)),
                             ("seismic_surge", "Seismic Surge", "Tremors: brick HP x0.7", MOD(brickHpMult=.7)),
                             ("gravity_games", "Gravity Games", "Gravity x1.6", MOD(gravityScale=1.6)),
                             ("sonic_rupture", "Sonic Rupture", "Resonance: brick x0.6, wood x0.8 HP", MOD(brickHpMult=.6, woodHpMult=.8))]:
    weeks.append({"id": wid, "name": name, "ruleText": text, "mod": mod,
                  "levels": [EL(wid, i + 1, lays[i], 5 if i < 4 else 6) for i in range(5)]})
fin = EL("finale", 1, "m1_clockwork_tower", 5); fin["name"] = "Frontier Finale"
tiers = []
for t in range(1, 31):
    free = R("coins", 50 + 10 * t) if t % 5 else R("chest", 1, "shard:2;gems:10")  # Economy v1: 10 gems every 5th tier
    if t == 30: free = R("chest", 1, "coins:1000;shard:5;gems:10")
    tiers.append({"tier": t, "xp": sum(100 if k <= 10 else 150 if k <= 20 else 200 for k in range(1, t + 1)), "free": free, "premium": R("cosmetic", 1, f"s1_cosmetic_{t:02d}")})
season = {"id": "s1_wrecking_frontier", "name": "The Wrecking Frontier - Season 1", "startUtc": "2026-10-12T00:00:00Z",
          "weekDays": 7, "xpPerEventStar": 10, "xpPerTitanClear": 50, "eventStarCapPerLevel": 3, "bonusChestEveryXp": 300, "bonusChestCap": 10, "unclaimedInboxDays": 30, "weeks": weeks, "finale": fin, "tiers": tiers}

# Gem packs are off in v1 (gems are earned only); flip enableGemPacks only with PM + server flag.
store = {"flags": {"enableGemPacks": False}, "allowedTypes": ["currency_pack", "wrecker_skin", "paint", "decal", "trail", "vfx_skin", "avatar", "frame", "title",
                         "map_decoration", "season_pass", "cosmetic_bundle", "emote", "victory_animation"],
         "items": [
  {"sku": "gems_small", "name": "Pouch of Gems", "type": "currency_pack", "cosmetic": True, "priceType": "real", "price": 99, "grants": "gems:100", "contents": [], "requiresFlag": "enableGemPacks"},
  {"sku": "gems_large", "name": "Chest of Gems", "type": "currency_pack", "cosmetic": True, "priceType": "real", "price": 499, "grants": "gems:600", "contents": [], "requiresFlag": "enableGemPacks"},
  {"sku": "skin_brass_baron", "name": "Brass Baron Wrecker", "type": "wrecker_skin", "cosmetic": True, "priceType": "gems", "price": 300, "grants": "", "contents": []},
  {"sku": "trail_confetti", "name": "Confetti Trail", "type": "trail", "cosmetic": True, "priceType": "gems", "price": 120, "grants": "", "contents": []},
  {"sku": "frame_tidebreaker", "name": "Tidebreaker Frame", "type": "frame", "cosmetic": True, "priceType": "gems", "price": 60, "grants": "", "contents": []},
  {"sku": "deco_lighthouse", "name": "Mini Lighthouse Deco", "type": "map_decoration", "cosmetic": True, "priceType": "coins", "price": 500, "grants": "", "contents": []},
  {"sku": "s1_premium_track", "name": "Wrecking Frontier Premium Track (cosmetics)", "type": "season_pass", "cosmetic": True, "priceType": "gems", "price": 450, "grants": "", "contents": []},
  {"sku": "bundle_harbor", "name": "Harbor Look Bundle", "type": "cosmetic_bundle", "cosmetic": True, "priceType": "gems", "price": 400, "grants": "", "contents": ["skin_brass_baron", "trail_confetti"]},
  {"sku": "emote_salute", "name": "Captain's Salute", "type": "emote", "cosmetic": True, "priceType": "gems", "price": 80, "grants": "", "contents": []}]}
wrecker = {"defaultDamage": 60, "launchSpeed": 28, "projectileMass": 0.8, "projectileScale": 0.45, "splitSpreadDeg": 8, "aimYawDegPerDp": 0.25, "aimPitchDegPerDp": 0.15}
economy = json.load(open(os.path.join(OUT, "economy.json")))  # hand-tuned data file (design/Economy_v1_Earned_Gems.md); not generated
for name, obj in [("wrecker", wrecker), ("store_catalog", store), ("chapter1", chapter), ("powerups", powerups), ("titan_templates", titan),
                  ("daily_rewards", daily), ("season_s1", season)]:
    if name in LEVEL_RULINGS_OWNED and not REGEN_LEVEL_DATA and os.path.exists(os.path.join(OUT, name + ".json")):
        continue  # Level Rulings data: never regressed by regeneration
    if name == "chapter1" and LD_OWNS_CH1:
        continue  # Level Designer's file: never overwritten
    with open(os.path.join(OUT, name + ".json"), "w") as f:
        json.dump(obj, f, indent=1)
# Hint angles are always derived from the target pieces (never the hand-typed HINTS above).
import subprocess, sys
subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__), "derive_hints.py")] + (["--season-only"] if LD_OWNS_CH1 else []), check=True)
print("content generated" + (" (Ch1 skipped: owned by levels/ch1)" if LD_OWNS_CH1 else ""))
