# VFX perf test: L10 full-budget collapse (Nothing Phone 3a, Android 16)
Owner: VFX & Tech Artist. Status: **plan + probe delivered, not yet run** (no Unity licence/APK on the box; nobody has measured on a device).

## Why L10
Ch1 L10 (mastery) has the largest collapse. At full budget it is the worst case for transparent overdraw (dust/puff layers),
particle simulation and draw calls: impact lane 150 + power-up lane 80 = 230 live particles, 60 debris rigidbodies (Mid), and break FX at the 3 per 100 ms cap.

## Probe: `Assets/_Project/Scripts/VFX/VfxPerfProbe.cs`
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
If no GPU timing comes back, the summary shows `NO_GPU_TIMING`. That means the run is invalid, not that the build failed. In that case use the fallback below.

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
