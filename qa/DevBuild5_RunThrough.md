# dev-build-5: QA run-through (CI emulator-smoke, run 38945003682, PR #23 @ 7c6c48f)
Artifacts: qa/smoke_38945003682/api34, api36 (screenshots 01-08, logcat, coords, calibration). Emulator: x86_64, 1080x2340, swiftshader. Reviewed 2026-10-17 00:30 IST.

| Step | API 34 | API 36 | Notes |
|---|---|---|---|
| 01 launch | PASS | PASS | No crash/ANR in logcat |
| 02 age gate | PASS | PASS | Placeholder UI (flat navy, system font); Art PR #24 pending |
| 03 Month/Year filled | PASS | PASS | March / 2006 picked; **Continue turns bright green (enabled)**, so BUG-20 is fixed on a real build |
| 04/05 map | PASS (reached) | PASS (reached) | **Islands render magenta** (BUG-23) |
| 06 L1 HUD | PASS (loads) | PASS (loads) | **About 51% of the screen is magenta** (ground/sea/island); FIRE button and aim line visible |
| 07 "win" line | **NOT TESTED** | **NOT TESTED** | The scripted win aim produced a fail ("So close!", Destroyed 0%); the script only warns (BUG-24) |
| 08 fail + retry | PASS | PASS | Fail screen shows Map/Retry; RESULT|fail logged |

## Findings
- **BUG-20: VERIFIED FIXED** on API 34 and 36 (screenshot 03: Continue enabled after Month+Year; 04: map reached). Still to confirm on the user's Nothing Phone 3a.
- **BUG-23 (High): magenta islands and arena. Not emulator-only.** Root cause in code: `WorldMapBuilder.cs:34/38` and `ArenaBuilder` add `MeshRenderer`s to procedural meshes (`ProceduralMesh.IslandTop/Cone`) without ever assigning a material, then only set a MaterialPropertyBlock colour. A renderer with no material draws magenta on every GPU and API. Cubes and primitives render fine because `CreatePrimitive` brings Default-Material. Expect the same on device. Fix: give those renderers a shared material that's in the build (e.g. a Resources material on URP/Lit or Standard, matching what the primitives use), and add an EditMode/Play test asserting no Renderer has a null sharedMaterial after building the map and L1. Side note: URP 14 is in the manifest but no pipeline asset is assigned (built-in is active), and `Assets/_Project/Settings.meta` has no folder. Settle which pipeline is intended before #2/#31 shaders land (they're URP-only).
- **BUG-24 (Medium, blocks "fully gating"): the win path isn't tested.** The scripted win line ends in a fail and is only a warning. Needs a known win line for L1 (deterministic, from the level data/sim, 50 Hz lock in #6) and a hard assert `CVTAP|RESULT|win`.
- Board/Art v2 comparison: the age gate, map and HUD are all procedural placeholders (navy boxes, system font, flat-coloured islands). Nothing matches the board's navy/gold styling or Art v2 screens 01/03/05/06/12 yet. Expected until Art is integrated (#24, UI skin). The fail screen offers a "Free Hint" button. Check against the GD hint rule (no free hint on the HUD; results screen allowed?).

Verdict for dev-build-5: **OK to test on the phone for flow (age gate, map, L1 load, fail/retry)**. Not visually acceptable (magenta), and the win path isn't verified.
