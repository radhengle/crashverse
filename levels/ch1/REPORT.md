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

## BUG-24: known-winning CI shots (rev 6)
- `ci_win_shots.py` → `ci_win_shots.json`: for every level's best line it gives the world target, yaw/elevation at gravity ×1.0 and ×1.2, the `hintYaw/hintPitch` that CI's dev `Button_DevAimHint` applies via `SetAim`, and an adb swipe at 1080x2340 / 420 dpi. It ray-marches the real arc (Wrecker at x=−20, spawn 1.95 m along the barrel, 28 m/s) and confirms the first piece hit is the intended one. **All 10 levels are clear.**
- **L1:** target `post`, world (9.4, 2.2, 0). Aim yaw 0°, elevation 11.0° (13.0° once PR #15 gravity ×1.2 lands). Swipe 540,1300 → 540,1549 (×1.2: → 540,1514). Data hint `hintYaw 0 / hintPitch 11`.
- **Orientation fix:** the Wrecker fires along +x, but the layouts were authored front-facing +z. gen_levels.py now turns every layout to face the Wrecker, and sim.py's occlusion is now taken along −x. Without this, the L4, L6, L7, L8 and L10 lines were blocked by side pieces. L10's core moves 0.4 m forward so it sits proud of the torso. The sim results are unchanged.
- Hint angles were re-derived with Developer's derive_hints.py (PR #12) and match ci_win_shots (season event hints updated too).
- **Root cause of the missed win (engine, not data):** logcat shows `Can't add component because class 'BoxCollider' / 'SphereCollider' doesn't exist!`. IL2CPP managed-code stripping removed the collider classes, which are only ever added at runtime. Blocks and projectiles have no colliders, so every shot passes through and 0% is destroyed, whatever the aim. Fix (Developer/Release): `Assets/link.xml` preserving `UnityEngine.PhysicsModule` (BoxCollider, SphereCollider, Rigidbody, FixedJoint), or lower Managed Stripping Level. Secondary: main's L1 hint (yaw 4, pitch 22) was hand-set and doesn't hit main's `post0` either.

## Rev 6 tuning (2026-10-30, branch level-designer/ch1-tuning → integration/v2)
Scope (PM): no new content; tune Ch1 L1–10 and the Daily Boss for fair difficulty, clear goals and fun destruction.
- Evidence used: QA DevBuild7_RunThrough + PR55_Review (L1 hint line wins 100% in 1 shot in PlayMode and on the emulator,
  API 34/36; 3★). No device evidence exists for L2–L10 or the Daily Boss (the smoke covers only L1; no gameplay video).
- sim.py: power-up ids map through powerups.json effects (v2 renamed shockwave → earthquake, splitstrike → multi_shot,
  which had silently switched Multi Shot off in the sim, so L8 read as 1★); the random player now fires a forced power-up;
  SIM_GSCALE env (default 1.2). Power-up lines are shown power-up first.
- ci_win_shots.py: `clear` requires the intended first hit at both g×1.0 and g×1.2; Multi Shot split modelled.
- New: frame_check.py (fixed behind-cannon camera, assumed parameters), sync_to_assets.py, ../daily_boss/daily_boss_sim.py.
- Level changes: L2 (two sills, 5 shots, 0.4/0.2), L5 (narrower), L6 (steel_joint roles, braceM hangs from deck),
  L7 (decor only), L8 (6 shots, narrower, keystones 1.6 m), L9 (5 shots, rock trimmed); readable mastery texts.
- Daily Boss: titan arenas turned to face the cannon (BUG-24 transform, in gen_content.py too), steel legs are joints,
  arena hint pelvis → legL (derived by derive_hints.py, now also covering titan_templates), dailyBoss 6–8 → 5–6 shots.

## Rev 7: building prefabs + L7 spike fix (2026-10-30, PR #57)
- Inventory and mapping in `../prefabs/` (README.md, prefab_inventory.json, prefab_map.json, PREFAB_MAP.md). No Unity prefabs exist;
  3D Modeler ships building assembly JSON + kit FBX (intact + fractured). LevelBuilder spawns cubes only, so no loader code was
  invented: layouts are re-shaped to the buildings and each piece is mapped to its module in the sidecar (code gaps in the README).
- L3 → Bld_Townhouse (0.8 scale) on the same prop/slab graph: random win 57.5% → 48.9%, random 3★ 6.5% → 6.2%; line, 3★ and mastery kept.
- L7 → Bld_Watchtower (~0.55) + spike fix (PM overrode the M1 lock): scaffold gone, ring0 visible and hinted; ground annex/crate stack/
  barrels/poles stay standing (8.5/49 mass), so ring0 alone = 82.7% and ring0 + annex/crate stack = 2-shot 3★. Random win 6.8% → 30.2%,
  random 3★ 0.5% → 2.7%. 5 shots, bands, 3 steel_joint rings (Tests.cs) and crown-intact mastery kept; season finale_e1 hint follows.
- Every level passes at g×1.0 and g×1.2 with identical results; ci_win_shots all clear; frame check all OK.

## Rev 8: realistic modular townhouse on L3 (interim) (2026-10-30)
Input: 3D Modeler's `Models/Realistic/Buildings/Bld_Townhouse/` @ e725121 (11 LD modules, 23 pieces, 69 contacts, 9.49 × 6.84 m with annex).
- **Level chosen: L3 (interim until the 8×6 m bakery lands on Oct 31).** Why:
  - It is the only Ch1 level whose subject is one two-storey shop building. The townhouse's ShopFront (door, window, sign, lantern) reads as
    the bakery, and L3 already carried a townhouse skin in rev 7.
  - It fits the frame at 1:1 (slack 148 px primary / 194 tight; worst piece is the chimney top). L5 would need two of them on one bridge,
    about 14 m wide (or about 0.45 scale), and the cottages hang from the bridge. L1 and L2 are a lookout on one post and a tank on four stilts.
    None of those beats has a ground-floor house in it.
  - With the Base held (indestructible + non-objective never collapses, DestructibleBlock.cs:135), the house on its own can only fall by
    breaking all four ground-wall modules (six pieces at piece level). That is 4 shots, and dropping the Floor alone gives only 49%. So no
    1- or 2-shot beat survives with the house straight on the ground. L3's beat can keep it: a 1.8 m timber cellar (two wood props, two
    brick slabs) sits between the Base and the ground walls. That is the only layer not in the model.
- **Mapping:** one block per LD module. The README says modules are split into pieces only for the 1,500-tri / 63-char limits. Supports come
  from the module anchor_to, which matches the contacts. Materials follow the fracture class (Stone → brick, Wood → wood, Roof → brick).
  +Z street → authored front → face_wrecker → faces the cannon (−x). Wall_Left/Roof_Left are screen-left. Boxes are cut at the contact
  heights so no cubes overlap. The optional Annex is left out (ground mass that would not fall). The rev 7 shop-awning decoy is kept.
- **Numbers (g×1.0 = g×1.2):** 5 shots, default bands. Best line propL, propR (2 shots, 3★), final 93.0%. Mastery ≥95% via props + awning
  post. Random 3★ 6.5%, random win 55.3% (rev 6: 6.5% / 57.5%; rev 7: 6.2% / 48.9%). CI first hits are clear at both gravities.
  Hint propL yaw +3.0 / pitch 8.0 (the props are now 1.6 m house-left = screen-left). Season event e2 hints follow.
- **Rev 8 PR text (for the user to paste into PR #57; the body was not edited):**
  > **Rev 8:** L3 now uses 3D Modeler's realistic modular townhouse (realistic-v1 @ e725121) at 1:1. That is an interim skin until the 8×6 m
  > bakery lands on Oct 31. It has one block per LD module, with supports from the module anchors and the street facing the cannon. The Base is
  > a held, non-objective plinth. A timber cellar on two props keeps L3's two-prop beat. Kept: 5 shots, 2-shot 3★, ≥95% mastery.
  > Random win is 55.3% and random 3★ is 6.5%. Frame slack is 148/194 px. Results are identical at gravity ×1.0 and ×1.2. L8 waits for the
  > Stall + arch (Oct 31 – Nov 1). New code gaps: a held Base can never fall, and the loader would have to spawn every piece of a module.

## Rev 8.1: sim vs JointBreakPolicy (physics/joint-break-policy @ 3806245) (2026-10-30)
- **The policy (Joint_Break_Policy.md, JointBreakPolicy.cs):**
  - Structural joints are breakForce = breakTorque = Infinity.
  - A joint goes away only through HP damage (steel joint ×2) or when its support breaks, collapses or falls out of bounds (`DetachJointsTo`).
  - SupportRule collapses a piece only when ALL its supports are gone.
  - The old 50%-HP OnJointBreak snap can no longer fire. Debris damage still goes through HP.
- **Diff against sim.py: no behavioural difference.**
  - **Force snap:** the sim never modelled one. There is no joint-force or 50%-HP path; `joint` only drives the ×2 direct-hit rule.
  - **Drag-along:** never modelled. `State.prop` collapses a dependent only when every `sup` is gone (OR semantics), and support state never looks at dependents.
  - **Support falls off vs destroyed:** both count as "gone" (`State.gone` = broken or collapsed), which matches DetachJointsTo on Break / ForceCollapse / out of bounds. A supported piece is kinematic, so only a falling piece can leave the bounds; the sim needs no separate out-of-bounds state.
  - **Held bases:** indestructible + non-objective never collapse in the game (ForceCollapse early return) or in the sim.
  - **Debris:** goes through HP in both (ruling 7a), and can chain-break.
- **Not modelled, and the game is never stricter:**
  - StructuralIntegrity collapse delays: timing only.
  - CollapseRules land-break: a collapsed non-steel piece landing at ≥ 4 m/s breaks. This came in from integration/v2 chunked destruction, not from the policy. It can only add destroyed mass on low pieces that the sim does not credit.
- **Changes:**
  - The sim.py docstring now states the policy. I removed a stale line saying debris was ignored; DEBRIS has been on since rev 5.
  - Added `python3 sim.py --policy-selftest`, which asserts no drag-along, no force snap, collapsed = broken for supports, joint HP only through damage, and held bases.
  - No level data changed.
- **Re-check (g×1.0 and g×1.2):** every L1–L10 result is identical to rev 8; the only difference at g×1.0 is L7's debris-collision count, 28,769 vs 28,770. The Daily Boss, ci_win_shots and frame_check outputs are byte-identical. Every level passes.
- **Local merge of 3806245 onto this branch (not pushed):**
  - It merged clean.
  - Physics EditMode suite (Physics/Tests, engine-free, run through NUnit on .NET 8): 123/123, including JointBreakPolicyTests and ChunkedDestructionTests' L1–10 + boss layout chunk budget on our layouts.
  - CompileCheck builds. Tools/Tests: 5140 passed, 0 failed. Server: 201 passed. Tools/ci/check_joint_break_policy.py: OK. derive_hints --check: clean.
  - Not run: Editor/Tests/Gameplay/JointBreakPolicyPhysicsTests (needs the Unity editor and PhysX; no Unity on the box).

## Rev 9: Bld_Bakery on L3, Kit_RealStall market on L8, integration/v2 merged (2026-10-30)
Input: 3D Modeler's realistic-v1 @ a24f5ec (Bld_Bakery, Kit_RealStall, Bld_MarketArch). The branch has since moved to 8c99d49, which only refits
the MarketArch sign (2.66 × 0.30 × 0.82, bolted to the beam cap). L8 uses that refit. Read with `git fetch` + `git show` only.
- **Merge:** `origin/integration/v2` (fb37202) was merged into `level-designer/ch1-tuning` as a regular merge commit (8363e17). It merged clean, and
  integration/v2 touched none of the level files. The merge brings BlockData `model/rotY/parts/modelScale`, `RealKit.DressParts`, the manifest
  builder, `CannonCameraRules`, `DailyBossData.hints[]/heroSlot`, chunked destruction, CollapseRules land-break and the JointBreakPolicy.
- **L3 → Bld_Bakery (1:1), replacing the interim townhouse.**
  - One block per LD module. Supports come from the module anchor_to. Materials follow the fracture class (Stone/Roof → brick, Wood → wood).
  - Every module block carries `parts` (its Kit_RealBakery piece FBX), `rotY` −90 (shop front faces the cannon, Wall_Left on screen-left) and
    `modelScale` 1. Each box is centred on its module pivot and starts at the pivot height (+1.8 m cellar lift), so the loader puts the parts where
    3D Modeler placed them. Boxes are trimmed at the next module's pivot, with 1–3 mm clearances, so no cubes overlap. The side and back walls sit
    behind the 8 m shop front.
  - The Base (8.64 × 6.84, Kit_RealBakery_Base) is held and non-objective. The timber cellar (two wood props over the Base, two brick slabs
    under the ground walls) stays, so the 2-shot beat survives.
  - The chimney is a 1.5 m stub above the roof with no parts. Kit_RealBakery_Chim's pivot (y 3.30) is inside the Floor, UpperStorey and roof
    boxes, and `parts` can only spawn at the box base. See gap 9 in the prefabs README.
  - Props: crateL and the stacked crateTop (Prop_BreadCrate) are objective wood decoys on the street. They never fall with the house, so
    props alone reach 93% (3★), and the ≥95% mastery needs one crate on top of the props (3 shots, as rev 8's awning post did). crateR and the
    bench stay indestructible dressing, as authored (crateTop also rests on crateR, so it never floats). This replaces the rev 8 awning decoy.
    Props use `parts` at their props[] positions and yaws.
- **L8 → 4 × Kit_RealStall under Bld_MarketArch (1:1).**
  - Each stall's Keystn (1.6 × 0.22 × 1.8, wood) is that stall's keystone (key0–3) and spans two stone quay piers (held). The stall
    (StallA/StallB alternating, wood) stands on it. A broken keystone drops its stall 1.06 m onto the quay, and in game it land-breaks at ≥4 m/s.
  - The arch (Pads held; Post_Left/Right, Beam: non-objective wood; Sign: steel joint, non-objective) frames the row, and the steel sign is the
    trap. The arch is non-objective because otherwise about 7 of 15 objective mass would never fall and the Multi Shot line would break.
    Objective = 4 keys + 4 stalls.
  - **Pitch: 1.6 m, not the 2.2 m stall_slots.**
    - At 2.2 m (keys at ±1.1, ±3.3), ci_win_shots' geometric split at key1 does reach key0/key1/key2: the children land at z −3.07 / +0.78
      around key1 at −1.1, about ±1.95 m.
    - But the sim's Multi Shot window (3 targets pairwise ≤3.5 m) rejects key0–key2 at 4.4 m. The sim's best line becomes key0, key1,
      split(key2+stall2+key3) = 3 shots, so 3★ (2 shots) is impossible at both gravities.
    - At the 2.2 m slots the outer stalls (±3.3 ± 0.85) would also run into the post footprints (Post_Left 1.455 wide at ±4.49, inner face ±3.77).
    - At 1.6 m (±0.8, ±2.4, the rev 6 keystone spacing) the sim and ci agree: multi_shot:key0+key1+key2, key3.
    - Cost: the 1.7 m stall visuals overlap their neighbours by 0.1 m.
  - The stall collider is centred on the module pivot (z 0.187) but only 1.325 m deep. Its front stops 5 cm behind the keystone front, so the
    0.37 m counter/awning overhang is visual only and a shot at the 22 cm keystone meets the keystone first. The piers are 1.0 m deep for the same
    reason. Both are covered by gap 9 (no visual offset).
- **Numbers (g×1.0 = g×1.2, every level passes):**

  | level | best line | final | random 3★ | random win | frame slack (primary/tight) |
  |---|---|---|---|---|---|
  | L3 | propL, propR (2) | 93.0% (mastery: + crate = 96.5%) | 6.5% (rev 8 6.5%) | 51.4% (rev 8 55.3%) | 117/119 px (rev 8 148/194) |
  | L8 | multi_shot:key0+key1+key2, key3 (2; 4 without Multi Shot) | 100% | 2.2% (rev 8 0.8%) | 67.6% (rev 8 43.5%) | 103/83 px (rev 8 115/93) |

  - Budgets, bands, best-line counts and mastery goals are unchanged.
  - The other levels are identical to rev 8.1.
  - Policy self-test: OK.
  - ci_win_shots: all 14 clear at both gravities. L8's Multi Shot at key1 first-hits [key0, key1, key2] at ×1.0 (yaw −1.5, pitch 11.5) and
    ×1.2 (pitch 13.5).
  - frame_check: all OK; the worst level is still L9 at 88/65.
  - Daily Boss: 3★ possible every day for the next 60 days.
  - derive_hints: L3 hint propL moves to yaw 4.0 / pitch 7.5 and L8 hint key1 to pitch 11.5. The season_s1 e2/e4 event hints follow, and
    `--check` is clean.
  - Note: frame_check is still the LD fixed-camera approximation. integration/v2 now has the real `CannonCameraRules`, which fits the pose to the
    structure bounds, so the taller bakery (chimney stub top 11.97 m) is framed by the game, not by a fixed pose.
- **Tests on the merged tree:**
  - Tools/Tests: 5135 passed, 0 failed.
  - Server: 201 passed, 10 skipped.
  - Physics/Tests (NUnit, engine-free): 123/123, including the chunk-budget test on these layouts.
- **For 3D Modeler:**
  1. stall_slots: re-pitch to 1.6 m (x ±0.8, ±2.4) or narrow StallA/StallB to ≤1.6 m. Alternatively, Level Designer + Developer widen the sim
     split window to match the measured ±1.95 m spread; that is a sim rule change across all levels.
  2. Bakery: the layout `dimensions_m` ridge is 8.52, but the README and module tops give 8.66.
  3. The Prop_BreadCrate / Prop_Bench props have no `_Fractured` set and are marked indestructible. L3 uses two crates as destructible decoys,
     so a fractured crate (or a confirmed "cube-break is fine") is needed.
  4. The Keystn pivot is at y −0.0098 (1 cm below its base). Harmless, but every other module pivot is at its base.
  5. realistic-v1 (Kit_RealBakery_*, Kit_RealStall_*, Props_Village) is not in integration/v2. Until it merges, the L3/L8 parts are not found:
     the build logs a `[RealKit] … not found` error per part and the blocks stay cubes.
- **For Developer:** the gap is an optional visual offset on BlockData, e.g. `modelOffset`. Parts spawn at the box base centre, which forces the
  chimney stub and the trimmed stall collider.
- **Rev 9 PR text (for the user to paste into PR #57; the body was not edited):**
  > **Rev 9:** integration/v2 merged in (regular merge, clean). **L3** now uses 3D Modeler's realistic modular Bld_Bakery (realistic-v1 @ a24f5ec)
  > at 1:1, wired through `parts`/`rotY`. It has one block per module, with the shop front facing the cannon and the timber cellar kept, so the
  > 2-shot props line is unchanged. The street bread crates are the mastery decoy. **L8** is four Kit_RealStall stalls, each on its own keystone,
  > under the Bld_MarketArch arch, with the steel sign as the trap. Multi Shot at key1 still takes key0–2 at gravity ×1.0 and ×1.2, with stalls
  > at a 1.6 m pitch instead of the 2.2 m stall_slots (3D Modeler to re-slot). Kept: budgets, star bands, best lines, mastery.
  > L3 random win is 51.4% and 3★ is 6.5%; L8 is 67.6% and 2.2%. Frame slack is 117 px on L3 and 103 px on L8. Tools/Tests 5135/0,
  > server 201, physics 123/123. Visuals need realistic-v1 merged into integration/v2.
