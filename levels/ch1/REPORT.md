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
