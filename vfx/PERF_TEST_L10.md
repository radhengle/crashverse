# VFX perf test: L10 full-budget collapse (Nothing Phone 3a, Android 16)

## Phone-only flow (no PC, no profiler): use this
1. **Install the dev APK.** On the phone, open the GitHub Actions artifact (`crashverse-dev` APK zip) in the browser, unzip it in Files, tap the `.apk` and allow "Install unknown apps" for Files once. The APK must be a **dev** build from a branch that contains both #31 and #35 (`vfx/perf-l10-integration`).
2. **Prep:** charge to ≥ 80 % and unplug. Brightness 50 %, battery saver off, Do Not Disturb on, close other apps. Let the phone sit idle 5 min (cool).
3. **Open the dev scene** `Scenes/Dev/L10_PerfFullBudget` from the dev build's dev menu/scene picker (needs the Developer hook, see "Open items"). The VFX probe starts by itself in that scene, with no setup.
4. **Watch:** the top-left overlay (kept inside the safe area, clear of the punch-hole) shows tier, live GPU / frame ms, rolling p95 GPU / frame, live and peak particles (incl. the 80/40 dummy power-up lane), draw / SetPass calls. "● REC" shows during the capture window.
5. **Screenshot the summary.** After the collapse (≈ 3 s after it settles, ≤ 15 s), a green (PASS) or red (FAIL) panel shows per-run results, fail reasons, the PERFL10 determinism hash and the build version. It stays until tapped. Take a screenshot (power + volume down) and send it.
6. Repeat 3 times with 1 min rest. Then do one "hot" run after 10 min of play, and say it's hot.
7. If GPU shows **n/a**, the phone's driver doesn't report GPU time. The panel then judges frame time against the frame budget (16.7 ms Mid/High, 33.4 ms Low) and says "(GPU n/a)". That's expected, and the result still counts.

Phone-only runs use the defaults: tier = Mid (or the stage's tier), 1 run, power-up lane on. `-perfTier/-perfRuns/-perfNoPowerUpLane` only work when the app is started with arguments (adb), and in that case the probe follows them too.

Owner: VFX & Tech Artist. Status: **plan + probe delivered, not yet run** (no Unity licence/APK on the box; nobody has measured on a device).

## Why L10
Ch1 L10 (mastery) has the largest collapse. At full budget it is the worst case for transparent overdraw (dust/puff layers),
particle simulation and draw calls: impact lane 150 + power-up lane 80 = 230 live particles, 60 debris rigidbodies (Mid), and break FX at the 3 per 100 ms cap.

## Probe: `Assets/_Project/Scripts/VFX/VfxPerfProbe.cs`
- **Overlay (IMGUI):** rebuilt at 4 Hz into a reused StringBuilder, so it allocates one string per 250 ms and nothing per frame. Two cached GUIStyles, font size about 1/26 of the safe-area short side, dark backing.
- **Auto-bootstrap:** `RuntimeInitializeOnLoadMethod` + `sceneLoaded` create the probe in `L10_PerfFullBudget`. The probe finds or creates a `VfxDirector`, which loads its assets from `Resources/VFX/VfxAssetSet`. #35's scene file is not edited.
- **Soft lookups into #35:** `PerfDummyPowerUpLane.LiveParticles` is added to the particle total, and the `PerfL10Stage.Stats.Hash` hash and `runs` value are read. All of it goes through reflection, so #31 compiles without #35.
- **Guards:** compiled only under `CRASHVERSE_DEV || DEVELOPMENT_BUILD`, and the runtime check `Debug.isDebugBuild` disables it otherwise. `check_release_guards.py` passes.
- Dev builds only (`CRASHVERSE_DEV` or Development Build); inert in release.
- Per frame it logs `VFXPERF,frame,t,frame_ms,cpu_ms,gpu_ms,particles,draw,setpass`. Sources:
  - `FrameTimingManager` (CPU/GPU frame time)
  - `VfxDirector.LiveParticleCount()`
  - `ProfilerRecorder` "Draw Calls Count" / "SetPass Calls Count"
- **Capture window:** opens on `PHYS_COLLAPSE_START` (or `COLLAPSE_CREAK`, or `StartCapture()`). It closes 3 s after `PHYS_COLLAPSE_END` / `COLLAPSE_LAND`, or after 15 s at most.
- **End line:** `VFXPERF_SUMMARY,...,result=PASS|FAIL:<reasons>`. Pass/fail logic is in `Core/VfxPerfStats.cs` (EditMode tested).

## Pass/fail targets (`VfxPerfTargets`)
| Metric (capture window) | Mid (60 fps) | High (60 fps) | Low (30 fps) |
|---|---|---|---|
| GPU frame time p95 | **≤ 12 ms** | ≤ 12 ms | ≤ 25 ms |
| CPU frame time p95 | ≤ 12 ms | ≤ 12 ms | ≤ 25 ms |
| Frame time p99 | ≤ 20 ms | ≤ 20 ms | ≤ 40 ms |
| Hitches (> 33.4 ms; Low > 50 ms) | ≤ 2 | ≤ 2 | ≤ 2 |
| Peak live particles | ≤ 230 | ≤ 230 | ≤ 115 |
| Peak draw calls / SetPass | ≤ 150 / 50 | ≤ 200 / 60 | ≤ 120 / 40 |
If GPU timing reads 0, the summary shows `gpu_p95=n/a`, and frame p95 is held to the frame budget (16.7 ms Mid/High, 33.4 ms Low) instead.

## Build setup
1. Player Settings > Other > **Frame Timing Stats: on** (needed for GPU times on Android). Graphics API **Vulkan** first, then repeat on GLES3 if the Vulkan run fails.
2. Development Build on (or `CRASHVERSE_DEV` profile from `android-apk.yml` dev). Turn off Autoconnect Profiler and Deep Profiling, since both distort timings.
3. Scene `L10_PerfFullBudget` (staged by Physics/Developer, see below) contains `VfxDirector` (tier set by QualitySettings) and `VfxPerfProbe` (director assigned, `tier` = active tier).
4. Quality tier forced per run: Mid (the device's expected tier), then High, then Low.

## Steps on the Nothing Phone 3a (Android 16)
1. Charge to ≥ 80 % and unplug the charger during the run (USB debugging over Wi-Fi: `adb tcpip 5555; adb connect <ip>`). Set brightness to 50 %, battery saver off, Do Not Disturb on, and close background apps.
2. Thermal state: start cool (idle 5 min, `adb shell dumpsys thermalservice` shows status 0). Record the status before and after each run.
3. Install: `adb install -r crashverse-dev.apk`.
4. Log: `adb logcat -c && adb logcat -s Unity | grep VFXPERF > l10_<tier>_<run>.csv`.
5. Launch L10 perf scene, fire the scripted shot (or tap FIRE at the scripted aim), wait for `VFXPERF_SUMMARY`.
6. Do 5 runs per tier with 60 s rest between runs. Then do one hot run after 10 min of continuous play, which is reported separately.
7. **Fallback / cross-check:** Android GPU Inspector (AGI) system profile or `adb shell dumpsys gfxinfo <pkg> framestats` on one Mid run. If FrameTimingManager GPU and AGI differ by > 15 %, flag it.
8. Report the median of 5 run summaries per tier, the worst run, the thermal status, the Graphics API, and the build commit.

## If it fails (VFX levers, in order)
Lower puff `maxParticleSize`, then halve smoke/dust layers on Mid, then lower the impact lane to 120, then switch Mid dust to a single flipbook quad. GPU-heavy failures are usually overdraw. Confirm with the Unity Rendering Debugger (Overdraw view) in an editor run of the same scene.

## Needed from Physics Developer to stage the scene
1. A deterministic L10 scene/fixture (`L10_PerfFullBudget`) built from the Ch1 L10 level data (#13/#12) with a scripted shot: fixed aim, power and power-up armed (Chain Reactor for worst case). Seeded `SimRng` (#6), so every run collapses identically.
2. Collapse sized to hit the caps: ≥ 60 debris RBs on Mid, enough breaks to saturate 3 per 100 ms for ≥ 1 s, plus mixed wood/brick/steel/glass.
3. `PHYS_*` ImpactEvents raised through AnimEventBus (#18/#20 merged, or that branch built into the APK), including `PHYS_COLLAPSE_START/END` so the probe window opens/closes, and pieceIds for `DamageFeedback`.
4. An optional debug hook to force the power-up FX lane (80) while power-up FX are still unbuilt, e.g. a dev toggle that spawns 80 dummy particles, so the full 230 is exercised.
5. Confirmation that the dev APK keeps `Frame Timing Stats` on, and that the scene is in the dev build's scene list.

## Open items for the phone-only flow
- **Developer:** the dev APK needs a way to open `Scenes/Dev/L10_PerfFullBudget` on the phone (a dev menu button, or a dev-build launch option). The phone can't pass `-perf*` args without adb, so a dev menu with tier/runs/lane toggles would be ideal.
- **CI:** `android-apk.yml` builds the APK only on `refs/heads/main` (`if: github.ref == 'refs/heads/main'`). A `workflow_dispatch` on `vfx/perf-l10-integration` runs only the guard and test job, with no APK. This needs a CI owner's change (for example, allow dispatch on `vfx/perf-*` / `physics/perf-*` with profile=dev). VFX doesn't edit CI.
