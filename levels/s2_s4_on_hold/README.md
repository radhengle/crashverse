# CRASHVERSE — Seasons 2–4 level data (Frozen Peaks · Neon City · Primeval Peaks)

Level Designer deliverable for Developer. **Not in any checkout. Not a git repo. No PR, no push.**
(PM rule: nobody edits the shared checkout `/workspace/crashverse`. Developer asked for `design/levels_s2_s4/` inside it; this is that content, staged at `/workspace/levels_s2_s4/` instead.)

Source of truth read with `git show` (read only) from **`integration/v2` at `ded59c8` (2026-10-24 18:39 IST)**: ContentModels.cs, HeroSlotRules.cs, HardLevelRules.cs, MaterialDefinition, powerups_v2, meta_v2.json, chapter1.json + Layouts, derive_hints.py, wrecker.json. `git fetch` failed on the box (no `gh` binary or credentials), so these are **cached refs**. Re-check against the live tip before import.

## How to drop in
Copy `data/chapter2.json`, `data/chapter3.json`, `data/chapter4.json` → `Assets/_Project/Resources/Data/` and `data/Layouts/*.json` → `Assets/_Project/Resources/Data/Layouts/`. The schema is identical to Ch1 v2: same top-level keys, same LevelEntry keys (plus `heroSlot`, already in ContentModels), and the same BlockData fields. The loader's material parser accepts every material used.

## Files
| file | what |
|---|---|
| `data/chapter2.json` / `chapter3.json` / `chapter4.json` | ChapterData: id, name, nextRegion, 4 islands each, 10 LevelEntry each |
| `data/Layouts/cN_lNN_<slug>.json` (30) | layout per level, already rotated to face the Wrecker (+x away from the launcher) |
| `gen_seasons.py` | generator, the single source for all of the above (authored with +z front, then `face_wrecker` maps it to +x) |
| `sim.py` → `sim_results.json` | solver/simulator for every level: min shots, stars, random-player bands, debris log |
| `finalize.py` | sets `hintTarget` = first piece of each level's proven line |
| `_derive/Tools/ContentGen/derive_hints.py` | **unchanged** copy of integration/v2 derive_hints.py, used to write hintYaw/hintPitch |
| `ci_win_shots.py` → `ci_win_shots.json` | BUG-24 format: per-shot aim, drag dp, swipe px, and the arc's first hit at g×1.0 and ×1.2 |
| `level_tags.json` | difficulty tag per level (read by sim.py) |
| `make_readme.py` | regenerates the per-level table below into `_table.md` |
| `ref_*`, `sim_ch1_base.py`, `ci_ch1_base.py`, `derive_hints_v2_original.py`, `wrecker.json` | integration/v2 reference copies |

Rebuild: `python3 gen_seasons.py && python3 sim.py && python3 finalize.py && for c in 2 3 4; do python3 _derive/Tools/ContentGen/derive_hints.py data/chapter$c.json --layouts data/Layouts; done && python3 ci_win_shots.py && python3 make_readme.py`

## Conventions
- **ids:** `cN_lNN` per chapter (c2 = Frozen Peaks, c3 = Neon City, c4 = Primeval Peaks). This matches the power-up unlock ids already in powerups_v2 (c2_l03 mega_bomb, c2_l05 shield, c2_l07 laser_beam, c3_l03 guided_rocket, c3_l07 time_freeze). Those levels have that power-up in `forced`.
- **`number` is global, 11–40,** so `HardLevelRules.IsHard(number % 10 == 0)` fires on 20/30/40, and meta_v2 season firstLevel (11/21/31) lines up. `requires` chains c1_l10 → c2_l01 → … → c4_l10.
- **Hard levels (#20, #30, #40):** data `shots` 7 → effective 6 (HardLevelRules: −1, min 3). Bands are star3 0.5 / star2 0.33, so 3★ = win in 3. Each is built around one exact 3-shot line, and NO_POWERUP is the mastery. They get the dark hard-level theme from code.
- **Rhythm per season:** Easy · Medium · Medium · Hard · Breather · Medium · Hard · Breather · Medium · **Hard (skull)**.
- **Rulings applied:** Level_Rulings_v1; debris 7a (`min(60, 0.5·m·(v−2))·taken-mult`, cap hits logged); ruling 8 (collapse only when ALL supports are gone, so `sup` is an OR list); gravity ×1.2; wood 30 / brick 60 / steel 120, steel joints take 2× direct; win at 85% (`winThreshold 0.85`); approved stone bases (grey indestructible `nonObj` ground/piers, never on a winning line, checked by the sim); every layout faces the launcher along +x.
- **Decoys:** grounded walls, slabs, gondolas and pillars that would block 85% are `nonObj`. They still soak shots and carry `role` for mastery (e.g. "3★ with no wall hits"), but don't count toward the win.

## Hero slot (keep / gatehouse)
Per HeroSlotRules.cs, a level has **at most one** `heroSlot {kind, fbx, pos, rotY}`. It's presentation only: no collider, never an objective, and never read by the sim. Each of the 30 levels has exactly one, alternating keep / gatehouse by island theme (finales #20/#30/#40 are gatehouses). Every slot uses `pos [9,0,0]` (HeroSlotRules.DefaultPos, behind the structure on +x, away from the Wrecker), `rotY 0`, and **`fbx ""`**, because integration/v2 has no `Bld_*` FBX yet. An empty fbx is valid per the rules: Art fills in the `Bld_` prefab name later with no layout change. Since nothing in the slot is physical, it can't change any sim result.

## Seasons
- **Frozen Peaks (S2, accent 9BE7FF, #11–20):** snow-stone (breakable stone, 90 HP, ice/snow tints), frosted-steel joints, pine timber, red frost-gas canisters (explosive), avalanche roofs and icicle rows. No glass, because ruling 9 keeps glass to Ch3+. Teaches Mega Bomb, Shield and Laser Beam. Finale: Frost Citadel (open the timber gate, then both joints).
- **Neon City (S3, accent E879F9, #21–30):** glass (12 HP) skins and atria as decoys, dark neon steel, cyan energy cores (explosive; they clear glass and brick but not steel), and a spinning indestructible hologram ring (decor). Teaches Guided Rocket and Time Freeze. Finale: Neon Spire (shatter the glass curtain, then both joints).
- **Primeval Peaks (S4, accent 84CC16, #31–40):** fossil stone, fern timber, amber crystal (the glass material in amber colour), volatile amber (explosive), and a spinning water wheel (decor). Heavy stone boulders crush on landing (debris 7a). No dinosaur IP: fossils, ferns, nests and bones only. Finale: Primeval Fortress (crack the amber gate, then both timber pins).

## Proof summary
- **sim.py: ALL 30 PASS.** Each level is winnable within its effective budget and 3★-able on the proven line. Random-player 3★ rates sit inside the tag bands (Easy 8–50%, Medium 2–25%, Hard 0.2–6%, Breather 15–60%, Hard-skull 0–6%). No stone or indestructible piece is on any optimal line, and min shots > 1 except on Easy/Breather levels. Forced-power-up levels get a 0% lower bound, because random play never uses power-ups.
- **Hard skull levels:** #20 min 3 (rand 3★ 0.8%), #30 min 3 (0.5%), #40 min 3 (2.6%). All are exactly 3 shots in a 6-shot effective budget.
- **Debris 7a:** **0 cap hits** across all searched states. Max raw debris is 22.2 (#24 / #30), so the 60 cap never binds in S2–S4.
- **Hints:** derived with the unchanged `derive_hints.py`. That tool solves at g×1.0, the same as Ch1.
- **ci_win_shots: ALL CLEAR at g×1.0 and g×1.2.** The ray-marched arc's first hit is the intended piece on every shot. Laser/rocket shots count as clear when the arc's first hit is a piece the power-up is modelled to pass. An earlier pass found 7 levels where the arc clipped the deck/slab edge above a joint, or a glass pane in front. Those joints (and their grey piers) were moved forward to stick out about 0.6 m past the deck, and the c3_l04 glass was lowered. All levels were then re-proven.
- **Mastery feasibility:** every mastery is met by the proven line (CLEAR_IN_SHOTS 1 on breathers with min 1, ALL_ROLE_BROKEN on the joint/brace pair, THREE_STARS_NO_ROLE_HITS where the line avoids the role pieces). c2_l08 was changed from "100% destroyed" (its line reaches 88%) to SHOTS_LEFT_AT_LEAST 3.

## Per-level table
`debris` = debris events / cap hits / max raw value seen during the whole search (not per run). `min (no pu / pu)` = min shots without / with the forced power-up.

### Frozen Peaks (`c2_frozen_peaks`, levels 11–20)

| id | # | name | tag | shots (data→eff) | ★3/★2 | min (no pu / pu) | proven line | final % | rand 3★ | rand win | debris hits/capped/max | hint (target yaw/pitch) | CI g1.0/g1.2 | hero | mastery |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| c2_l01 | 11 | Ski Lodge | Easy | 4→4 | 0.6/0.3 | 1/1 | logpile | 100% | 39.9% | 93% | 15152/0/11.0 | logpile 0.0/9.5 | ✔/✔ | keep @ [9, 0, 0] | DESTROYED_AT_LEAST 0.95 |
| c2_l02 | 12 | Snow-Stone Chalet | Medium | 5→5 | 0.6/0.3 | 2/2 | propL, propR | 100% | 21.5% | 82% | 13014/0/11.5 | propL -3.0/10.0 | ✔/✔ | gatehouse @ [9, 0, 0] | THREE_STARS_NO_ROLE_HITS  role=wall |
| c2_l03 | 13 | Frost Depot | Medium · forced mega_bomb | 5→5 | 0.6/0.3 | 3/2 | deck, mega_bomb:shedPost | 88% | 0.0% | 55% | 10349/0/11.9 | deck 0.0/12.0 | ✔/✔ | keep @ [9, 0, 0] | CLEAR_IN_SHOTS 2.0 |
| c2_l04 | 14 | Ice-Bridge Fort | Hard | 5→5 | 0.6/0.3 | 2/2 | jointL, jointR | 100% | 5.7% | 37% | 10168/0/8.4 | jointL -7.0/13.5 | ✔/✔ | gatehouse @ [9, 0, 0] | THREE_STARS_NO_ROLE_HITS  role=wall |
| c2_l05 | 15 | Snow-Cannon Row | Breather · forced shield | 5→5 | 0.6/0.3 | 1/1 | canister | 100% | 27.6% | 99% | 11189/0/6.9 | canister 0.0/9.5 | ✔/✔ | keep @ [9, 0, 0] | CLEAR_IN_SHOTS 1.0 |
| c2_l06 | 16 | Avalanche Shelter | Medium | 5→5 | 0.6/0.3 | 2/2 | braceL, braceR | 100% | 4.8% | 33% | 5279/0/10.3 | braceL -3.0/13.0 | ✔/✔ | gatehouse @ [9, 0, 0] | ALL_ROLE_BROKEN  role=brace |
| c2_l07 | 17 | Glacier Vault | Hard · forced laser_beam | 5→5 | 0.6/0.3 | None/2 | keyB, laser_beam:keyA | 100% | 0.0% | 0% | 7977/0/9.6 | keyB 3.0/10.5 | ✔/✔ | keep @ [9, 0, 0] | STARS 3.0 |
| c2_l08 | 18 | Icicle Gantry | Breather | 5→5 | 0.6/0.3 | 1/1 | gantry | 88% | 30.8% | 74% | 12114/0/9.6 | gantry 0.0/13.5 | ✔/✔ | gatehouse @ [9, 0, 0] | SHOTS_LEFT_AT_LEAST 3.0 |
| c2_l09 | 19 | Cable-Car Station | Medium | 5→5 | 0.6/0.3 | 2/2 | joint, post | 100% | 16.4% | 79% | 6769/0/10.3 | joint -3.0/13.0 | ✔/✔ | keep @ [9, 0, 0] | NO_POWERUP  |
| c2_l10 | 20 | Frost Citadel | Hard (skull) | 7→6 | 0.5/0.33 | 3/3 | gate, jointL, jointR | 100% | 0.8% | 11% | 5147/0/20.2 | gate 3.5/11.0 | ✔/✔ | gatehouse @ [9, 0, 0] | NO_POWERUP  |

### Neon City (`c3_neon_city`, levels 21–30)

| id | # | name | tag | shots (data→eff) | ★3/★2 | min (no pu / pu) | proven line | final % | rand 3★ | rand win | debris hits/capped/max | hint (target yaw/pitch) | CI g1.0/g1.2 | hero | mastery |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| c3_l01 | 21 | Neon Kiosk | Easy | 4→4 | 0.6/0.3 | 1/1 | pole | 100% | 23.9% | 86% | 10684/0/6.5 | pole 0.0/12.0 | ✔/✔ | keep @ [9, 0, 0] | DESTROYED_AT_LEAST 0.95 |
| c3_l02 | 22 | Glass Atrium | Medium | 5→5 | 0.6/0.3 | 2/2 | colL, colR | 100% | 6.8% | 52% | 8507/0/17.0 | colL -3.5/11.0 | ✔/✔ | gatehouse @ [9, 0, 0] | THREE_STARS_NO_ROLE_HITS  role=wall |
| c3_l03 | 23 | Rooftop Billboard | Medium · forced guided_rocket | 5→5 | 0.6/0.3 | 2/2 | jointA, legB | 86% | 6.8% | 64% | 8188/0/8.5 | jointA -2.5/14.0 | ✔/✔ | keep @ [9, 0, 0] | CLEAR_IN_SHOTS 2.0 |
| c3_l04 | 24 | Energy-Core Tower | Hard | 5→5 | 0.6/0.3 | 2/2 | jointL, jointR | 88% | 2.0% | 57% | 12923/0/22.0 | jointL -3.0/13.0 | ✔/✔ | gatehouse @ [9, 0, 0] | ALL_ROLE_BROKEN  role=steel_joint |
| c3_l05 | 25 | Core Arcade | Breather | 5→5 | 0.6/0.3 | 1/1 | core0 | 100% | 54.2% | 90% | 15965/0/11.0 | core0 -6.0/9.5 | ✔/✔ | keep @ [9, 0, 0] | CLEAR_IN_SHOTS 1.0 |
| c3_l06 | 26 | Sky Bridge | Medium | 5→5 | 0.6/0.3 | 2/2 | legL, legR | 100% | 15.6% | 75% | 13137/0/16.4 | legL -6.0/11.0 | ✔/✔ | gatehouse @ [9, 0, 0] | DESTROYED_AT_LEAST 0.95 |
| c3_l07 | 27 | Spinning Penthouse | Hard · forced time_freeze | 5→5 | 0.6/0.3 | 2/2 | jointL, jointR | 100% | 3.7% | 62% | 9778/0/11.9 | jointL -3.5/13.0 | ✔/✔ | keep @ [9, 0, 0] | STARS 3.0 |
| c3_l08 | 28 | Rooftop Greenhouse | Breather | 5→5 | 0.6/0.3 | 2/2 | propL, propR | 100% | 31.0% | 98% | 9691/0/14.4 | propL -3.0/10.0 | ✔/✔ | gatehouse @ [9, 0, 0] | DESTROYED_AT_LEAST 1.0 |
| c3_l09 | 29 | Data Center | Medium | 5→5 | 0.6/0.3 | 2/2 | joint, leg | 100% | 9.6% | 61% | 8883/0/12.5 | joint -3.5/13.0 | ✔/✔ | keep @ [9, 0, 0] | NO_POWERUP  |
| c3_l10 | 30 | Neon Spire | Hard (skull) | 7→6 | 0.5/0.33 | 3/3 | curtainL, jointL, jointR | 100% | 0.5% | 27% | 13543/0/22.2 | curtainL -3.5/11.0 | ✔/✔ | gatehouse @ [9, 0, 0] | NO_POWERUP  |

### Primeval Peaks (`c4_primeval_peaks`, levels 31–40)

| id | # | name | tag | shots (data→eff) | ★3/★2 | min (no pu / pu) | proven line | final % | rand 3★ | rand win | debris hits/capped/max | hint (target yaw/pitch) | CI g1.0/g1.2 | hero | mastery |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| c4_l01 | 31 | Fern Hut | Easy | 4→4 | 0.6/0.3 | 1/1 | log | 100% | 39.3% | 100% | 11424/0/11.9 | log 0.0/10.0 | ✔/✔ | keep @ [9, 0, 0] | DESTROYED_AT_LEAST 0.95 |
| c4_l02 | 32 | Amber Arch | Medium | 5→5 | 0.6/0.3 | 2/2 | amberL, amberR | 100% | 5.0% | 43% | 6416/0/13.5 | amberL -3.0/11.0 | ✔/✔ | gatehouse @ [9, 0, 0] | THREE_STARS_NO_ROLE_HITS  role=wall |
| c4_l03 | 33 | Boulder Cradle | Medium | 5→5 | 0.6/0.3 | 2/2 | legL, legR | 100% | 18.8% | 73% | 9441/0/11.9 | legL -3.0/10.5 | ✔/✔ | keep @ [9, 0, 0] | DESTROYED_AT_LEAST 0.95 |
| c4_l04 | 34 | Fossil Bridge | Hard | 5→5 | 0.6/0.3 | 2/2 | pinL, pinR | 100% | 3.6% | 43% | 6015/0/12.9 | pinL -3.5/12.5 | ✔/✔ | gatehouse @ [9, 0, 0] | THREE_STARS_NO_ROLE_HITS  role=wall |
| c4_l05 | 35 | Tar-Pit Totems | Breather | 5→5 | 0.6/0.3 | 1/1 | tar0 | 100% | 43.1% | 84% | 12656/0/9.6 | tar0 -4.0/9.5 | ✔/✔ | keep @ [9, 0, 0] | CLEAR_IN_SHOTS 1.0 |
| c4_l06 | 36 | Cliff Dwelling | Medium | 5→5 | 0.6/0.3 | 2/2 | postL, postR | 100% | 18.3% | 90% | 16282/0/13.1 | postL -4.0/10.5 | ✔/✔ | gatehouse @ [9, 0, 0] | DESTROYED_AT_LEAST 0.95 |
| c4_l07 | 37 | Standing-Stone Gate | Hard | 5→5 | 0.6/0.3 | 2/2 | wedgeL, wedgeR | 100% | 4.5% | 43% | 4761/0/12.5 | wedgeL -4.5/15.0 | ✔/✔ | gatehouse @ [9, 0, 0] | THREE_STARS_NO_ROLE_HITS  role=wall |
| c4_l08 | 38 | Nest Tower | Breather | 5→5 | 0.6/0.3 | 1/1 | trunk | 100% | 45.6% | 83% | 14390/0/12.7 | trunk 0.0/11.0 | ✔/✔ | keep @ [9, 0, 0] | CLEAR_IN_SHOTS 1.0 |
| c4_l09 | 39 | Waterfall Mill | Medium | 5→5 | 0.6/0.3 | 2/2 | pier, crystal | 100% | 6.6% | 57% | 7456/0/13.7 | pier -3.0/10.5 | ✔/✔ | keep @ [9, 0, 0] | NO_POWERUP  |
| c4_l10 | 40 | Primeval Fortress | Hard (skull) | 7→6 | 0.5/0.33 | 3/3 | gateCrystal, pinL, pinR | 100% | 2.6% | 27% | 5038/0/11.5 | gateCrystal 3.5/11.0 | ✔/✔ | gatehouse @ [9, 0, 0] | NO_POWERUP  |

## Known soft spots (design, not bugs)
- **c2_l05 Shield and c3_l07 Time Freeze** are forced teaching slots. The sim models both as no-ops, so the level is solvable without using them. The teaching moment depends on Unity behaviour (shield refund, frozen holo ring), which needs a playtest.
- **c3_l03 Guided Rocket:** the proven line (jointA, legB) doesn't need the rocket. The rocket is an alternative, not a requirement. If GD wants it required, I'd hide legB behind the parapet.
- **c2_l07 Glacier Vault** needs the forced Laser to win at all, so random win is 0% without power-ups. That's intended (teaching), but it's the strictest level in S2.

## Open questions for the Game designer (defaults I picked)
1. **Ice material:** Environment Set A approves "ice" as an engine feature, but MaterialDefinition has no ice. *Default:* Frozen Peaks uses breakable `stone` (90 HP) tinted ice/snow, and `steel` tinted frost. Add a real `ice` material (HP, mass, debris factor, slippery?)
2. **Season ↔ roadmap mapping:** meta_v2 seasons are Frozen Peaks / Neon City / Primeval Peaks, while the roadmap's Ch2–4 are Gearstorm / Neon District / Crystal Frontier, and Ch1 L10 gates `gearstorm_city`. *Default:* followed meta_v2 (c2/c3/c4 = S2/S3/S4) with nextRegion "Neon City" / "Primeval Peaks". Which is canonical, and should c1_l10's gate id change?
3. **Global vs per-chapter `number`:** I used global 11–40 so HardLevelRules (number % 10) and meta firstLevel work. Confirm.
4. **Hard-level penalty vs Ch1 L10:** HardLevelRules also hits Ch1 L10 (8 → 7 effective). For S2–S4 I set data 7 → 6 with bands 0.5/0.33 (3★ = 3 shots). Are these the bands you want for hard levels?
5. **Mega Bomb modelling:** assumed a direct 60 hit plus 70×(1−d/3.5) falloff splash to other pieces. Does the target take 60, 70, or both?
6. **Laser Beam / Guided Rocket modelling:** assumed laser ignores ≤2 destructible occluders (passCount 2) and the rocket ignores occlusion. Confirm.
7. **Chain-lightning conduits** (roadmap's Neon District mechanic) aren't implemented, so not used. *Default:* Neon City's mechanic is explosive energy cores (existing `explosive`). Want conduits for a later pass?
8. **Glass as amber in Primeval Peaks (Ch4):** ruling 9 says glass is Ch3+, so Ch4 is allowed. OK to reskin it as amber crystal (F59E0B) there?
9. **Explosive tint per season** (frost canister red E5484D, neon core cyan 22D3EE, volatile amber orange): is block `color` enough, or does Art want a separate prefab per season?
10. **Hints at gravity ×1.2:** derive_hints.py solves at g×1.0 (unchanged). ci_win_shots confirms both 1.0 and 1.2 hit the right piece, but the angles differ by about 0.5–1.5° elevation. Should derive_hints use 9.81×1.2 once PR #15 is in?
11. **Hero slot FBX:** no `Bld_*` models exist on integration/v2, so all 30 slots have `fbx ""` (valid). Which Bld_ keep/gatehouse per season, and is `pos [9,0,0]` right for wide layouts (#20 / #40 are 6–12 m wide)?
12. **Season-finale rewards:** #20 and #30 give coins 500 + a gate to the next season. #40 gives coins 500 only (no next region in meta_v2). Should they also unlock a power-up, like Ch1 L10's black_hole?
