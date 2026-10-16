# Ch1 Level Pass — Report (rev 2, Level_Rulings_v1 applied)
See **Ch1_Levels_v1.md** for intent, bands, mastery and the solvability table.
## Files
gen_levels.py, sim.py, sim_results.json, chapter1_levels.json, layouts/*.json (10). Copies go in Assets/_Project/Resources/Data/.
## Rulings applied
1. L10 budget is 8 shots. Mastery is now "Defeat the Tidewarden without hitting the closed shutter" (type NO_CLOSED_SHUTTER_HIT).
2. L10 bands are .375/.25. The 5-shot perfect run gets 3★ (margin 3).
3. The sim follows the spec shutter (open at the start of phase 2, toggles only after phase-2 shots). The code version (min 6 shots) is still computed for Developer as `min_code`.
4. L7 keeps its 2-shot tuning.
5. L1 mastery is now ≥95% destroyed. The L2 text matches SG-21.
6. Stone bases are all nonObj + indestructible + grey 8A8A8A, including the L7 plinth (it was objective before) and the L10 sea-stack. Verified: none is a hint target or on an optimal line.
7. Debris damage is modelled. No level's minimum, stars or win changed. The L7 crown stays 100% unbroken on the 2-shot line, so no crown tuning was needed. L7 final % went 90.2 → 95.4 because the plinth no longer counts.
## Open for Developer
- Bug: TidewardenController/GameSession closes the shutter after the shot that opened phase 2 (ruling 3).
- New mastery type NO_CLOSED_SHUTTER_HIT needs MasteryEvaluator support.
- Already accepted: star-band test becomes a range check, gen_content.py reads levels/ch1, and hint angles are scripted.
## Open for Game designer
- None blocking. The sim is still abstract (no Unity physics). The L2 tank-off-edge and L7 crown masteries need a playtest.

## Fun redesign (rev 4)
Layouts were rebuilt for distinct silhouettes, domino moments and decoy/risk targets.
- L1: T-shaped perch with cabins and a bell.
- L2: adds a catwalk and pump shed.
- L3: now two storeys, with an awning decoy.
- L4: adds a brick annex.
- L5: adds a water-wheel and a lantern string.
- L6: becomes a tall 3-drum silo with a steel dome and hopper.
- L7: unchanged (bound by the M1 contract and ruling 4).
- L8: adds a clock arch over the steel sign.
- L9: adds a bell tower and a catwalk tying both structures together.
- L10: unchanged (bound by the boss rules).

## Rulings 7a / 8 and gravity ×1.2 (rev 5)
All three are modelled. Shot budgets were re-checked: no level's min shots, margin, stars or pass result changed. L7's crown stays 4/4 intact. Repeat the check in Unity once Physics PR #11 and PR #15 land.
Assumption to confirm: the 7a material factor applies to the struck piece.
### Debris cap-hit stats (4,000 random runs per level)
- c1_l01: 0/19750 collisions hit the cap (0.0%)
- c1_l02: 0/23892 collisions hit the cap (0.0%)
- c1_l03: 0/23268 collisions hit the cap (0.0%)
- c1_l04: 0/26512 collisions hit the cap (0.0%)
- c1_l05: 0/15646 collisions hit the cap (0.0%)
- c1_l06: 0/17059 collisions hit the cap (0.0%)
- c1_l07: 0/18357 collisions hit the cap (0.0%)
- c1_l08: 0/31105 collisions hit the cap (0.0%)
- c1_l09: 0/23618 collisions hit the cap (0.0%)
No collision reaches the 60 cap. The worst case is a 4 kg steel piece dropping about 10 m: 0.5 × 4 × (15.4 − 2) ≈ 27. So the cap doesn't bind in Ch1, and debris is supplementary rather than decisive.
## Fun redesign (rev 4)
Layouts were rebuilt for distinct silhouettes, domino moments and decoy/risk targets.
- L1: T-shaped perch with cabins and a bell.
- L2: adds a catwalk and pump shed.
- L3: now two storeys, with an awning decoy.
- L4: adds a brick annex.
- L5: adds a water-wheel and a lantern string.
- L6: becomes a tall 3-drum silo with a steel dome and hopper.
- L7: unchanged (bound by the M1 contract and ruling 4).
- L8: adds a clock arch over the steel sign.
- L9: adds a bell tower and a catwalk tying both structures together.
- L10: unchanged (bound by the boss rules).

## Rulings 7a / 8 and gravity ×1.2 (rev 5)
All three are modelled. Shot budgets were re-checked: no level's min shots, margin, stars or pass result changed. L7's crown stays 4/4 intact. Repeat the check in Unity once Physics PR #11 and PR #15 land.
Assumption to confirm: the 7a material factor applies to the struck piece.
### Debris cap-hit stats (4,000 random runs per level)
- c1_l01: 0/19750 collisions hit the cap (0.0%)
- c1_l02: 0/23892 collisions hit the cap (0.0%)
- c1_l03: 0/23268 collisions hit the cap (0.0%)
- c1_l04: 0/26512 collisions hit the cap (0.0%)
- c1_l05: 0/15646 collisions hit the cap (0.0%)
- c1_l06: 0/17059 collisions hit the cap (0.0%)
- c1_l07: 0/18357 collisions hit the cap (0.0%)
- c1_l08: 0/31105 collisions hit the cap (0.0%)
- c1_l09: 0/23618 collisions hit the cap (0.0%)
No collision reaches the 60 cap. The worst case is a 4 kg steel piece dropping about 10 m: 0.5 × 4 × (15.4 − 2) ≈ 27. So the cap doesn't bind in Ch1, and debris is supplementary rather than decisive.
