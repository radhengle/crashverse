"""L1 v2 Tide Watchtower physics layout (world boxes from design/L01_v2_spec.md), written relative to the structure base."""
import json
BASE_X = -20.0 + 17.0   # targetDistance 17 -> structure base (-3, 0, 0)
B = []
# PM b25 QA (b26): the goal's towers are TALL (main tower ~2.2x taller than wide); TideWatchtower.cs applies the same map
# (TideWatchtower.StretchY) to the dressing.
# PM b26 QA (b27): the arch stands on a stepped plinth (its base ~7% of the screen above the islet top), the stone tiers are taller
# and the roof/turret a little shorter so the turret tip still clears the HUD. Piecewise map of the authored height y (same numbers
# as TideWatchtower.StretchY): below 0.78 unchanged (rocks, sea); 0.8 -> plinth top P; tier 1 x K1; up to the roof top x K2; turret x K3.
P, K1, K2, K3 = 2.48, 1.42, 1.055, 0.85
YA = P + (3.65 - 0.8) * K1
YR = YA + (9.45 - 3.65) * K2
def Y(y):
    if y < 0.78: return y
    if y <= 3.65: return P + (y - 0.8) * K1
    if y <= 9.45: return YA + (y - 3.65) * K2
    return YR + (y - 9.45) * K3
def box(id, mat, x0, x1, y0, y1, z0, z1, sup=(), role="", obj=True, explosive=False, ind=False, blast=0.0):
    y0, y1 = round(Y(y0), 3), round(Y(y1), 3)
    B.append({"id": id, "mat": mat, "p": [round((x0 + x1) / 2 - BASE_X, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)],
              "s": [round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)], "sup": list(sup), "joint": False, "role": role,
              "nonObj": not obj, "color": "", "explosive": explosive, "indestructible": ind, "spinAxis": [], "pivot": [], "spinDps": 0.0})
    if blast: B[-1]["blastRadius"] = blast
# Island_Foundation (indestructible rock islets; the sea is y = -1.0)
box("rockC", "brick", -6.5, -0.5, -1.6, 0.8, -3.8, 3.1, role="island", obj=False, ind=True)
box("rockL", "brick", -6.0, -2.0, -1.6, 0.0, 3.1, 7.3, role="island", obj=False, ind=True)
box("rockR", "brick", -5.6, -1.6, -1.6, -0.15, -7.3, -3.8, role="island", obj=False, ind=True)
# Corruption_Crystal: breaks on a direct hit (wood HP 30 < blast 36) and detonates (r 7.0, 80 dmg): every stone tier, the gallery,
# room and roofs and both archer decks break (PlayMode b22: with r 2.5 only tier 1 broke and the upper tower came to rest on the
# held tier-1 rubble, 34.5 % after 4 hint shots); the turret (8.2 m away) falls from 9.45 m
box("crystal", "wood", -4.15, -3.25, 0.8, 3.0, -0.86, 0.24, ["rockC"], role="core", obj=False, explosive=True, blast=9.5)  # b26: r 9.5 for the 1.4x taller tower
# Stone_Structure tier 1 (brick HP 60), every centre within 2.5 m of the crystal centre
box("t1L", "brick", -4.9, -1.5, 0.8, 4.0, 0.83, 2.2, ["rockC"], role="wall")
box("t1R", "brick", -4.9, -1.5, 0.8, 4.0, -3.53, -1.49, ["rockC"], role="wall")
box("t1B", "brick", -2.75, -1.5, 0.8, 4.0, -1.49, 0.83, ["rockC"], role="wall")
box("lintel", "brick", -4.9, -2.75, 3.65, 4.0, -1.49, 0.83, ["t1L", "t1R", "t1B"], role="wall")
T1 = ["t1L", "t1R", "t1B", "lintel"]
# tier 2 (y 4.0-6.2), 4 quarters
for n, (x0, x1) in (("F", (-4.75, -3.2)), ("B", (-3.2, -1.65))):
    box(f"t2{n}L", "brick", x0, x1, 4.0, 6.2, -0.6, 1.95, T1, role="wall")
    box(f"t2{n}R", "brick", x0, x1, 4.0, 6.2, -3.15, -0.6, T1, role="wall")
T2 = ["t2FL", "t2FR", "t2BL", "t2BR"]
# Main_Tower: gallery deck (balcony), gallery room
box("galleryL", "wood", -5.2, -1.3, 6.2, 6.8, -0.57, 1.93, T2, role="floor")
box("galleryR", "wood", -5.2, -1.3, 6.2, 6.8, -3.07, -0.57, T2, role="floor")
box("roomL", "wood", -4.3, -2.1, 6.8, 7.7, -0.79, 1.28, ["galleryL", "galleryR"], role="wall")
box("roomR", "wood", -4.3, -2.1, 6.8, 7.7, -2.86, -0.79, ["galleryL", "galleryR"], role="wall")
# Roof (pagoda hip roof, two halves) and the turret
box("roofL", "wood", -5.0, -1.4, 7.7, 9.45, -0.75, 1.69, ["roomL", "roomR"], role="roof")
box("roofR", "wood", -5.0, -1.4, 7.7, 9.45, -3.19, -0.75, ["roomL", "roomR"], role="roof")
box("turret", "wood", -3.7, -2.7, 9.45, 10.75, -1.4, -0.4, ["roofL", "roofR"], role="roof")
# Decorations on the rock (breakable, non-objective)
box("brazierL", "wood", -5.68, -5.32, 0.8, 2.6, 1.30, 1.66, ["rockC"], role="prop", obj=False)
box("brazierR", "wood", -5.68, -5.32, 0.8, 2.6, -2.35, -1.99, ["rockC"], role="prop", obj=False)
box("fortBarrel", "wood", -6.0, -5.2, 0.8, 2.1, -3.67, -2.83, ["rockC"], role="prop", obj=False)
# Left_Archer_Platform (dark timber): legs + lower deck + top deck (objective)
LX = (-5.15, -2.45)
for i, x in enumerate(LX):
    box(f"LlegI{i}", "wood", x - 0.15, x + 0.15, 0.8, 5.1, 2.6, 2.9, ["rockC"], role="support", obj=False)
    box(f"LlegO{i}", "wood", x - 0.15, x + 0.15, 0.0, 5.1, 5.35, 5.65, ["rockL"], role="support", obj=False)
LL = ["LlegI0", "LlegI1", "LlegO0", "LlegO1"]
box("Llow", "wood", -5.0, -2.6, 2.5, 3.0, 2.4, 6.9, LL, role="floor", obj=False)
box("Ltop", "wood", -5.0, -2.6, 5.1, 5.65, 2.6, 5.66, LL, role="floor")
# Right_Archer_Platform
RX = (-5.0, -2.35)
for i, x in enumerate(RX):
    box(f"RlegI{i}", "wood", x - 0.15, x + 0.15, 0.8, 4.6, -3.85, -3.55, ["rockC"], role="support", obj=False)
    box(f"RlegO{i}", "wood", x - 0.15, x + 0.15, -0.15, 4.6, -7.0, -6.7, ["rockR"], role="support", obj=False)
RL = ["RlegI0", "RlegI1", "RlegO0", "RlegO1"]
box("Rlow", "wood", -4.85, -2.5, 2.0, 2.42, -7.3, -4.16, RL, role="floor", obj=False)
box("Rtop", "wood", -4.85, -2.5, 4.6, 5.35, -5.95, -3.16, RL, role="floor")
box("Rscaffold", "wood", -5.18, -4.92, 1.46, 4.07, -3.39, -2.15, ["rockC"], role="support", obj=False)
json.dump({"id": "c1_l01_tide_watchtower", "blocks": B}, open("/workspace/cv_p1/Assets/_Project/Resources/Data/Layouts/c1_l01_tide_watchtower.json", "w"), indent=1)
obj = [b["id"] for b in B if not b["nonObj"]]
print(len(B), "blocks;", len(obj), "objective:", obj)
