# CRASHVERSE — Chapter 1 Sunnyville Isles, Levels v1 (rev 9: realistic Bld_Bakery on L3, Kit_RealStall market on L8, integration/v2 merged)
Owner: Level Designer · Branch `level-designer/ch1-tuning` (draft PR into `integration/v2`; supersedes #13, already merged to main) · Rules: Level_Rulings_v1 (#1–10, 7a), gravity ×1.2 (PR #15), powerups v2 ids (earthquake = SHOCKWAVE, multi_shot = SPLIT).

**Pipeline:** `python3 sync_to_assets.py` (gen_levels → Resources layouts + chapter1.json → derive_hints at g×1.0) → `SIM_GSCALE=1.2 python3 sim.py` → `python3 ../daily_boss/daily_boss_sim.py` → `python3 ci_win_shots.py` → `python3 frame_check.py --json > frame_check.json` → `python3 mkdoc.py`.
**Gravity:** sim and ci_win_shots prove every line at g×1.2 (ruling #10 / PR #15). Results are identical at g×1.0. The in-game hint angles in chapter1.json stay at g×1.0 because integration/v2 still runs 9.81 (GameSession `_baseGravity`) and CI runs `derive_hints.py --check` at ×1.0; ci_win_shots.json carries both sets (`aim['1.0']`, `aim['1.2']`).

## Sim table (g×1.2; 4,000 random-target runs per level; the random player fires a forced power-up on its first shot)
| L | Tag | Shots | ★ bands | Min (with power-up) | 3★ needs ≤ | Margin | Best line (= hint, CI) | Final % | Rand 3★ | Rand win | Debris cap hits | CI first-hit ×1.0/×1.2 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Easy | 4 | 0.6/0.3 | 1 | 1 | 3 | post | 100.0 | 22.2% | 84.7% | 0/19750 | ✅ | ✅ |
| 2 | Easy | 5 | 0.4/0.2 | 2 | 3 | 3 | sillL, sillR | 100.0 | 14.0% | 53.9% | 0/22912 | ✅ | ✅ |
| 3 | Medium | 5 | 0.6/0.3 | 2 | 2 | 3 | propL, propR | 93.0 | 6.5% | 51.4% | 0/22049 | ✅ | ✅ |
| 4 | Hard | 5 | 0.6/0.3 | 2 | 2 | 3 | beamL, beamR | 100.0 | 1.7% | 36.6% | 0/26512 | ✅ | ✅ |
| 5 | Breather | 5 | 0.6/0.3 | 1 | 2 | 4 | bridge | 94.4 | 20.3% | 72.4% | 0/11571 | ✅ | ✅ |
| 6 | Medium | 5 | 0.6/0.3 | 2 | 2 | 3 | braceL, braceR | 100.0 | 15.3% | 76.7% | 0/19123 | ✅ | ✅ |
| 7 | Hard | 5 | 0.6/0.3 | 2 | 2 | 3 | ring0, annex | 87.8 | 2.7% | 30.2% | 0/28770 | ✅ | ✅ |
| 8 | Breather | 6 | 0.6/0.3 | 4 (2) | 2 | 4 | multi_shot:key0+key1+key2, key3 | 100.0 | 2.2% | 67.6% | 0/4786 | ✅ | ✅ |
| 9 | Hard | 5 | 0.6/0.3 | 2 | 2 | 3 | lhRing, seaDeck | 93.3 | 2.3% | 42.2% | 0/21800 | ✅ | ✅ |
| 10 | Boss | 8 | 0.375/0.25 | 5 | 5 | 3 | shoulderL, shoulderR, core(open), any(closed), core(open) | defeat | n/a | n/a | n/a | ✅ | ✅ |

Rhythm: Easy, Easy, Medium, Hard, Breather, Medium, Hard, Breather, Hard, Boss. L10 min 5 = spec shutter (Level_Rulings #3); it would be 6 (2★) if the old phase-2 toggle bug came back.
Debris: no collision reaches the 60 cap in any level (ruling 10 check holds).

## Levels
### L1. Wooden Lookout Hut — Seagull Perch: T-shaped cliff lookout on one crooked post [Easy]
- Layout, shots, bands and hint unchanged (the only level with real Unity/emulator evidence; the CI smoke depends on it). Mastery text reworded.
- Materials: wood · Shots 4 · Bands 0.6/0.3 · Teaches: Aim + fire, trajectory arc
- Goal: destroy 85% · Mastery: **Destroy at least 95% of the lookout** · Hint: `post` (yaw 0.0, pitch 11.0 at g×1.0)
- Chain collapse: Post snaps → deck, both side cabins, hut, roof and bell all drop
- Risk/reward: The big roof is a decoy
- Why it's fun: One shot clears the whole cliff-top: the tutorial "aha" (confirmed in Unity: PlayMode + emulator smoke win 100% in 1 shot on the hint line).
- Proof: min 1 via post; 3★ needs ≤1; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 27.68°, pitch 5.14° down, slack 197 px; 1080x1920: FOV 29.0°, pitch 4.52° down, slack 273 px; 1440x2560: FOV 28.62°, pitch 4.7° down, slack 358 px; 1536x2048 (4:3): FOV 27.63°, pitch 5.17° down, slack 345 px; hint arc on screen at all four: yes.
### L2. Stilt Water Tower — Water tower on two sills, plus a pump shed on a catwalk [Easy]
- **Changed:** one sill → two sills; 4 → 5 shots; bands 0.6/0.3 → 0.4/0.2 (3★ = 2–3 shots); hint sill → sillL.
- Materials: wood · Shots 5 · Bands 0.4/0.2 · Teaches: Find every support, not the walls
- Goal: destroy 85% · Mastery: **Knock the water tank off the cliff** · Hint: `sillL` (yaw -2.5, pitch 9.5 at g×1.0)
- Chain collapse: Sill + sill → four stilts → tank, and the catwalk drags the pump shed down (domino)
- Risk/reward: The tank lid is the obvious target and is useless
- Why it's fun: L1 taught "hit the support"; L2 teaches "find every support". 3★ allows one miss.
- Proof: min 2 via sillL, sillR; 3★ needs ≤3; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 32.51°, pitch 2.87° down, slack 40 px; 1080x1920: FOV 30.06°, pitch 4.02° down, slack 94 px; 1440x2560: FOV 29.67°, pitch 4.21° down, slack 116 px; 1536x2048 (4:3): FOV 28.64°, pitch 4.69° down, slack 266 px; hint arc on screen at all four: yes.
### L3. Brick Bakery — Two-storey bakery: 3D Modeler's realistic modular Bld_Bakery (1:1) on a timber cellar with two wood props [Medium]
- **Changed (rev 9):** the interim townhouse is replaced by 3D Modeler's realistic modular Bld_Bakery (realistic-v1 @ a24f5ec) at 1:1: one block per LD module (ShopFront, Wall_Left/Right/Back, Floor, UpperStorey, Roof_Left/Right, Chimney stub; Base = held, non-objective), supports from the module anchors, shop front facing the cannon (parts + rotY −90). The 1.8 m timber cellar (two props, two slabs) stays: the 2-shot line is still propL, propR (93%). The street bread crates replace the awning decoy: crateL + stacked crateTop are objective decoys (≥95% mastery = props + one crate, 3 shots); crateR and the bench are indestructible dressing. 5 shots and default bands kept. Random win 51.4%, random 3★ 6.5% (rev 8: 55.3% / 6.5%).
- Materials: wood + brick · Shots 5 · Bands 0.6/0.3 · Teaches: Brick needs support loss
- Goal: destroy 85% · Mastery: **Destroy at least 95% of the bakery** · Hint: `propL` (yaw 4.0, pitch 7.5 at g×1.0)
- Chain collapse: Two props → cellar slabs → ShopFront, side and back walls → Floor → UpperStorey → both roof halves and the chimney
- Risk/reward: The bread crates on the street never fall with the bakery
- Why it's fun: Stone walls feel useless until the props go and the whole bakery slumps; the crates are the 95% mastery chaser.
- Proof: min 2 via propL, propR; 3★ needs ≤2; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 36.84°, pitch 0.84° down, slack 140 px; 1080x1920: FOV 38.59°, pitch 0.01° down, slack 229 px; 1440x2560: FOV 38.09°, pitch 0.25° down, slack 298 px; 1536x2048 (4:3): FOV 36.77°, pitch 0.87° down, slack 351 px; hint arc on screen at all four: yes.
### L4. Windmill — Windmill with a brick annex [Hard]
- Layout unchanged; mastery text reworded.
- Materials: wood + brick · Shots 5 · Bands 0.6/0.3 · Teaches: Collapse chains between pieces
- Goal: destroy 85% · Mastery: **3 stars without hitting a brick wall** · Hint: `beamL` (yaw -2.5, pitch 13.0 at g×1.0)
- Chain collapse: Beams → drum → cap → top drum → sails; the annex beam drops the annex
- Risk/reward: Drum faces reach 3★ but fail the no-wall mastery
- Why it's fun: The longest chain so far, through mill and annex.
- Proof: min 2 via beamL, beamR; 3★ needs ≤2; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 31.83°, pitch 3.19° down, slack 181 px; 1080x1920: FOV 33.34°, pitch 2.48° down, slack 260 px; 1440x2560: FOV 32.9°, pitch 2.69° down, slack 341 px; 1536x2048 (4:3): FOV 31.76°, pitch 3.23° down, slack 348 px; hint arc on screen at all four: yes.
### L5. Twin Cottages — Twin cottages on a bridge, with a water-wheel and lantern string [Breather]
- **Changed:** recomposed 13 → 10.6 m wide (rocks, piers, bridge, cottages) to fit the fixed camera; same line.
- Materials: wood + brick · Shots 5 · Bands 0.6/0.3 · Teaches: One shot, two targets (bridge)
- Goal: destroy 85% · Mastery: **Drop both cottages with 1 shot** · Hint: `bridge` (yaw 0.0, pitch 12.5 at g×1.0)
- Chain collapse: Bridge → both cottages, the wheel and the lantern string
- Risk/reward: Brick walls are decoys
- Why it's fun: Breather: one clean shot and the whole scene tumbles.
- Proof: min 1 via bridge; 3★ needs ≤2; margin 4.
- Frame (CannonCameraRules port): 1080x2340: FOV 32.73°, pitch 2.77° down, slack 42 px; 1080x1920: FOV 27.1°, pitch 5.42° down, slack 39 px; 1440x2560: FOV 27.1°, pitch 5.42° down, slack 53 px; 1536x2048 (4:3): FOV 24.89°, pitch 6.45° down, slack 184 px; hint arc on screen at all four: yes.
### L6. Steel-Braced Granary — Grain silo on three gold steel joints, with a side hopper [Medium]
- **Changed:** braces role brace → steel_joint (mastery was impossible: it counts role steel_joint, and L6 had none); braceM now hangs under the deck (was on the rock), so the best line drops 100% instead of 87.1%.
- Materials: brick + steel · Shots 5 · Bands 0.6/0.3 · Teaches: Steel joints take 2x damage
- Goal: destroy 85% · Mastery: **Break all 3 gold steel joints** · Hint: `braceL` (yaw -3.0, pitch 14.0 at g×1.0)
- Chain collapse: Two side joints → deck → three drums, steel dome, chute, hopper and the hanging centre joint
- Risk/reward: The steel dome is a 120-HP trap
- Why it's fun: Two gold joints drop a 10 m silo; mastery asks for all 3 joints, which costs the 3rd star.
- Proof: min 2 via braceL, braceR; 3★ needs ≤2; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 33.88°, pitch 2.23° down, slack 218 px; 1080x1920: FOV 35.49°, pitch 1.47° down, slack 289 px; 1440x2560: FOV 35.03°, pitch 1.69° down, slack 380 px; 1536x2048 (4:3): FOV 33.81°, pitch 2.26° down, slack 349 px; hint arc on screen at all four: yes.
### L7. Clockwork Tower — Clockwork Watchtower (Bld_Watchtower, ~0.55 scale; Earthquake forced) [Hard]
- **Changed (rev 7, PM overrode the M1 layout lock):** spike fixed. Scaffold removed so ring0 is visible (hint scaffold → ring0); front storey walls widened to hide the side walls (fewer, clearer targets); ground annex, crate stack, barrels and hoist poles (8.5 of 49 mass) stay standing, so ring0 + annex/crate stack = 2-shot 3★. Random win 6.8% → 30.2%, random 3★ 0.5% → 2.7%. 5 shots, bands, 3 steel rings and the crown-intact mastery kept. Season finale_e1 shares this layout and hint.
- Materials: wood + brick + steel · Shots 5 · Bands 0.6/0.3 · Teaches: Joint sequencing; first power-up pick (Earthquake)
- Goal: destroy 85% · Mastery: **Topple the tower without breaking the crown** · Hint: `ring0` (yaw 0.0, pitch 9.5 at g×1.0)
- Chain collapse: Gold ring0 (in plain sight) → both storeys and the crown drop as one stack; then the annex or the crate stack
- Risk/reward: ring1 drops only the top half; ring0 alone stops at 82.7%; a pole or barrel is too light for 3★
- Why it's fun: Read the gold ring, then pick the heavy ground piece: the crown lands intact.
- Proof: min 2 via ring0, annex; 3★ needs ≤2; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 35.15°, pitch 1.63° down, slack 147 px; 1080x1920: FOV 36.82°, pitch 0.85° down, slack 234 px; 1440x2560: FOV 36.34°, pitch 1.07° down, slack 306 px; 1536x2048 (4:3): FOV 35.08°, pitch 1.66° down, slack 350 px; hint arc on screen at all four: yes.
### L8. Market Arcade — Market row: 4 timber stalls on keystones under a market arch with a steel sign (Multi Shot forced) [Breather]
- **Changed (rev 9):** re-laid out from 3D Modeler's Kit_RealStall + Bld_MarketArch (realistic-v1 @ a24f5ec, sign refit 8c99d49) at 1:1: four stalls (StallA/StallB alternating), each on its own Keystn = key0-3 (wood, spans two stone quay piers), under the arch (Pads, Post_Left/Right, Beam: non-objective) with the steel Sign as the trap. Stalls at a 1.6 m pitch, not the 2.2 m stall_slots, so Multi Shot at key1 still takes key0+key1+key2 at g×1.0 and ×1.2 (sim 3.5 m split window; outer stalls also clear the posts). 6 shots, bands, 2-shot line (Multi Shot key0-2, key3), mastery "Earn 3 stars" and hint key1 kept. Random win 67.6%, random 3★ 2.2% (rev 8: 43.5% / 0.8%).
- Materials: wood (+ steel sign, stone quay) · Shots 6 · Bands 0.6/0.3 · Teaches: Choose loadout deliberately (Multi Shot)
- Goal: destroy 85% · Mastery: **Earn 3 stars** · Hint: `key1` (yaw -1.5, pitch 11.5 at g×1.0)
- Chain collapse: One Multi Shot breaks keystones 0-2 → stalls 0-2 drop to the quay; one more shot for keystone 3
- Risk/reward: The steel sign and the arch look like the target but are not objectives
- Why it's fun: The power-up showcase: one shot does three shots' work.
- Proof: min 2 via multi_shot:key0+key1+key2, key3; 3★ needs ≤2; margin 4.
- Frame (CannonCameraRules port): 1080x2340: FOV 34.57°, pitch 1.9° down, slack 42 px; 1080x1920: FOV 28.65°, pitch 4.69° down, slack 40 px; 1440x2560: FOV 28.65°, pitch 4.69° down, slack 53 px; 1536x2048 (4:3): FOV 25.94°, pitch 5.96° down, slack 174 px; hint arc on screen at all four: yes.
### L9. Lighthouse Gauntlet — Lighthouse + seawall, joined by a catwalk [Hard]
- **Changed:** 6 → 5 shots ("tight budget" was 4 spare); rock trimmed 12 → 10.6 m for the frame.
- Materials: all 3 · Shots 5 · Bands 0.6/0.3 · Teaches: Tests everything; tight budget
- Goal: destroy 85% · Mastery: **Win without using a power-up** · Hint: `lhRing` (yaw -5.5, pitch 14.0 at g×1.0)
- Chain collapse: Ring → lighthouse; deck → seawall and bell tower; the catwalk falls once both ends go
- Risk/reward: The catwalk is a trap
- Why it's fun: The exam: two structures, two exact shots, now on a tight 5-shot budget.
- Proof: min 2 via lhRing, seaDeck; 3★ needs ≤2; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 35.07°, pitch 1.67° down, slack 41 px; 1080x1920: FOV 34.71°, pitch 1.84° down, slack 126 px; 1440x2560: FOV 34.25°, pitch 2.05° down, slack 159 px; 1536x2048 (4:3): FOV 33.06°, pitch 2.61° down, slack 302 px; hint arc on screen at all four: yes.
### L10. The Tidewarden — The Tidewarden on a sea-stack (boss) [Boss]
- Unchanged (Level_Rulings_v1 #1–3: 8 shots, 0.375/0.25, closed-shutter mastery).
- Materials: brick + steel + wood · Shots 8 · Bands 0.375/0.25 · Teaches: Boss phases + weak points
- Goal: destroy 85% (boss: defeat the Tidewarden) · Mastery: **Defeat the Tidewarden without hitting the closed shutter** · Hint: `shoulderL` (yaw -4.0, pitch 19.5 at g×1.0)
- Chain collapse: Shoulders → arms fall and the chest opens; core ×2 → collapse
- Risk/reward: Shooting the closed shutter wastes a shot and fails mastery
- Why it's fun: A boss you read, not grind.
- Proof: min 5 via shoulderL, shoulderR, core(open), any(closed), core(open); 3★ needs ≤5; margin 3.
- Frame (CannonCameraRules port): 1080x2340: FOV 30.71°, pitch 3.72° down, slack 253 px; 1080x1920: FOV 32.17°, pitch 3.03° down, slack 316 px; 1440x2560: FOV 31.75°, pitch 3.23° down, slack 418 px; 1536x2048 (4:3): FOV 30.65°, pitch 3.75° down, slack 347 px; hint arc on screen at all four: yes.

## Daily Boss (meta_v2.json `dailyBoss` + titan layouts)
Config now: baseShots 5, shotSpread 1 (rolls 5–6), winThreshold 0.85. Stars use the LevelEntry defaults 0.6/0.3.

| Arena | Min | Best line (= arena hint) | Pelvis shot alone | 5 shots: rand win / rand 3★ / 3★ possible | 6 shots: rand win / rand 3★ / 3★ possible | Frame slack |
|---|---|---|---|---|---|---|
| titan_ash_bridge | 2 | legL, legR | 84.4% | 86% / 13% / yes | 95% / 13% / yes | 171 px (FOV 33.37°) |
| titan_caldera_rim | 2 | legL, legR | 84.4% | 86% / 13% / yes | 95% / 13% / yes | 171 px (FOV 33.37°) |
| titan_forge_floor | 2 | legL, legR | 77.1% | 86% / 13% / yes | 95% / 13% / yes | 282 px (FOV 33.13°) |
| titan_slag_foundry | 2 | legL, legR | 77.1% | 86% / 13% / yes | 95% / 13% / yes | 304 px (FOV 33.13°) |

Next 60 days (DailyBossRules.ForDate replayed): 3★ possible every day = **True**; shots {'5': 24, '6': 36}; arenas {'titan_ash_bridge': 17, 'titan_caldera_rim': 15, 'titan_forge_floor': 14, 'titan_slag_foundry': 14}.

## Camera frame check (`frame_check.py`, port of CannonCameraRules)
The game has no fixed camera. `OrbitCamera.FrameBehindCannon` solves a locked pose per level and per screen aspect with Developer's `CannonCameraRules.Solve` (integration/v2 @ fb37202, Settings defaults: 14 m behind and 4 m above the cannon root (−20, 0, 0), on the firing line; vertical FOV fits the cannon and the whole layout inside a 6% top / 3% bottom margin, and the layout width at 92% of the screen width, clamped to 24–68°; pitch anchors the cannon's lowest point 3% above the bottom edge and tilts up if the top would clip). `frame_check.py` is a line-by-line Python port (Solve, ToViewport, FullyVisible, ViewportRect) over the union of block boxes (as `GameBootstrap.StructureBounds`).
- Screens: 1080×2340, 1080×1920, 1440×2560 and 1536×2048 (4:3 tablet). Slack = px from the layout to the nearest left/right/top screen edge.
- Every `CannonCameraEditModeTests` assertion (whole building and cannon visible, cannon centred / at the bottom / ≥35% wide / below 60%, building above it and filling the view) is re-checked on the test's six aspects.
- Each stored hint aim (hintYaw/hintPitch, derive_hints geometry, g×1.0) is traced from the muzzle to its hintTarget; the arc and the target must stay on screen.
Flags: none.
Before rev 9.1 this section used an assumed fixed camera (FOV 50, eye (−28, 5, 0), pitch 8°).
