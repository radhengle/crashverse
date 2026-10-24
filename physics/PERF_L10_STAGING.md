# L10 full-budget perf staging (Physics → VFX)

This answers the "Needed from Physics Developer" list in `vfx/PERF_TEST_L10.md` on `vfx/particles-damage` (3c56e26).

## Scene
The scene is `Assets/_Project/Scenes/Dev/L10_PerfFullBudget.unity`. It's dev-only and holds one `PerfL10Stage` plus a camera.
- **Fixture:** `Scripts/Data/Dev/PerfL10Fixture.cs` is engine-free and dev-guarded. It starts from Ch1 `c1_l10`, copies the base layout's 13 blocks (ids prefixed `l10_`), and puts a 2×10×8 curtain in front of the boss. Curtain rows, bottom to top, are wood (explosive fuse), glass, brick, glass, wood, steel, glass, brick, glass, wood.
  - That gives 173 blocks, 160 of them breakable. Potential Mid debris is far above the 60 cap.
  - Boss rules are removed and the win threshold is unreachable, so the collapse decides the run, not boss phases or a win screen.
- **Seed:** fixed `0x4C31305045524631` ("L10PERF1"), passed through `RunContext.seed` → `SimRunner.BeginRun` → `SimRng` / `DebrisPool`.
- **Scripted inputs (InputEvents):** `arm` slot 0 (Chain Reactor) at tick 25, then `fire` at tick 50 with yaw 0° and elevation 8.000°. Power (launch speed) is pinned at 28 m/s.
- **Mode:** a Campaign (non-scored) run, so tier caps are as shipped: Low 25, Mid 60, High 90.
- **Glass:** glass is Neon (ch3+) only. Here it exists only in this dev fixture, generated in code with a `dev_` layout id and never written to `Resources/Data/Layouts`. `CONTENT_chapter1_has_no_glass` excludes dev ids and asserts that no `dev_` layout is shipped.

## PHYS_* events
`GameplayDirector.StartRun` now adds an `ImpactEventEmitter` after `Session.Begin`. Before this change nothing created one, so no PHYS_* events fired at all.
- It raises PHYS_IMPACT, CRACK, BREAK, JOINT_SNAP and COLLAPSE_START/END, all with pieceId and cascadeId.
- It does this in every build, dev and release, because it's presentation only and audio depends on it.

## Output (logcat `-s Unity | grep PERFL10`)
- **`PERFL10_RUN_START,...`:** printed when a run begins.
- **`PERFL10_SUMMARY,run,seed,tier,hash,events,breaks,saturated_ms,peak_debris,collapse_start,collapse_end,mats`:** one per run.
  - `hash` is an FNV-1a hash of (tick, kind, pieceId, cascadeId) over all PHYS_* events.
  - `saturated_ms` is the longest run of consecutive 100 ms windows with at least 3 breaks. The target is ≥ 1000.
  - `peak_debris` is the most active debris rigidbodies seen at once. The target is ≥ 60 on Mid.
- **`PERFL10_DETERMINISM,runs,result=MATCH|MISMATCH`:** printed when `-perfRuns` is greater than 1. Runs retry with the same seed and inputs.
- **Run window:** a run closes 150 ticks (3 s) after the last COLLAPSE_END, or 750 ticks (15 s) after the shot, matching the VfxPerfProbe window.

Command line: `-perfTier Low|Mid|High`, `-perfRuns N`, `-perfFillPowerUpLane` (on by default) or `-perfNoPowerUpLane`.

## Power-up lane (stub)
`Scripts/Dev/Perf/PerfDummyPowerUpLane.cs` holds 80 (Low 40) long-lived particles that use `Sprites/Default` (alpha). There's no power-up lane API on `vfx/particles-damage` yet, so it's a standalone ParticleSystem and **VfxDirector doesn't count it**.

## Build
- `BuildScript.BuildAndroid` turns on `PlayerSettings.enableFrameTimingStats` only for the dev profile, and turns it off for release.
- The dev scene list is the EditorBuildSettings scenes plus `BuildScript.DevScenes`. Release uses EditorBuildSettings only, with any `Scenes/Dev/` path filtered out.
- `ReleaseBuildGuard` fails a release build that lists a dev scene or has Frame Timing Stats on.
- `Tools/ci/check_release_guards.py` checks three things: no `Scenes/Dev/` entry in EditorBuildSettings, `enableFrameTimingStats` isn't 1 in ProjectSettings, and perf symbols only appear under the dev guard.

## Not verified (no Unity on the box)
- **Shot target:** the 8° aim is computed on paper, aiming at curtain row 0–1. Check it hits in an editor run.
- **Targets:** check that `saturated_ms` ≥ 1000 and `peak_debris` ≥ 60 on Mid. If they fall short, tune `PerfL10Fixture` (Rows, RowMats, AimElevationDeg).
- **Cross-run determinism:** non-scored runs use Unity auto-simulation. `PERFL10_DETERMINISM` reports whether repeat runs match on a given device. Bit-identical results across devices aren't promised.

## VFX to do
1. Add `VfxDirector` and `VfxPerfProbe` to the scene (or have the probe find them at runtime). They aren't on this branch.
2. Add `PerfDummyPowerUpLane.LiveParticles` to the probe's particle total, or replace the stub with a VfxDirector power-up lane API (`VfxRules.PowerUpLaneBudget`).
3. Build the dev APK from a branch that has both this PR and `vfx/particles-damage`.
