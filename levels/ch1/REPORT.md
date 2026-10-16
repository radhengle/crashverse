# Ch1 Level Pass — Report
See **Ch1_Levels_v1.md** for per-level intent, bands, mastery and the solvability table (it's the PM deliverable).
## Files
- gen_levels.py (layout author), sim.py (solver/sim), sim_results.json, chapter1_levels.json, layouts/*.json (10)
- Installed copies: Assets/_Project/Resources/Data/chapter1.json, Resources/Data/Layouts/*.json
## Rule ambiguities for the Game designer
A1 L10 mastery "Defeat with ≥3 shots left" is impossible at 7 shots: the minimum is 5 under SG-05 (2 shoulders + hit/closed/hit), and 6 under current code. Fix with an 8–9 shot budget, the core taking 1 hit, or the shutter not toggling on the phase-opening shot.
A2 L10 3★ is impossible with the default .60 band (needs ≤2 shots). This pass uses .28/.14; designer must approve.
A3 SG-05 vs code: does the shot that opens phase 2 also toggle the shutter? Code (GameSession calls OnShotResolved after CheckPhase opens it) closes it immediately, which wastes a shot.
A4 M1 says "solvable in 3 skilled shots" but v0.2 stars at 5 shots give 3★ only for ≤2 shots. L7 is tuned to 2. M1 says ring0 sits at 1/3 height; here it sits on the plinth behind the scaffold, and the plinth is indestructible (otherwise 1 shot wins).
A5 Budget-4 levels (L1, L2) give 3★ only for a 1-shot clear, so L1's mastery duplicates 3★.
A6 Structural stone (nonObj indestructible) is used as the base on L2–L9. Confirm this is allowed ("supports are objective pieces", contract §1).
A7 Falling-impact damage (code: up to 60 per collision) can break "intact" pieces, which affects L7 crown mastery and pushes destroyed% up. Not specced.
## Schema / code mismatches
M1 Tools/Tests/Tests.cs:67 asserts star3Pct == 0.6 on every level, so it fails on L10 (.28).
M2 Tools/ContentGen/gen_content.py regenerates chapter1.json and Layouts and would overwrite this pass. It needs to read levels/ch1 or be retired for Ch1.
M3 hintYaw/hintPitch values are Developer's, and hintTargets were renamed (post, sill, slabs, beamL, bridge, braceL, scaffold, key1, lhRing, shoulderL). Aim angles need re-deriving in the editor.
M4 m1_clockwork_tower is also used by season_s1 finale, which now gets the new tower too.
M5 The `sup` list is OR (a piece collapses only when all its supports are gone). It's undocumented in the contract, and the layouts depend on it.
M6 There's no level-data field for an indestructible objective piece's intent. The L7 plinth (objective + indestructible) stays in total mass (2.5) but can never count. That's fine, but it's implicit.
