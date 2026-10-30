# QA review: PR #52 (integration/v1 @ be6ad77), dev-build-6
Reviewer: QA Test Lead. Date: 2026-10-24 (IST). Evidence is in qa/devbuild6/.

## Verdict: APPROVE WITH CHANGES
dev-build-6 can go to the user's phone for the real-device check, mainly sound. It is **not release-ready** (see R-1/R-2).
This verdict is a QA comment only. QA has not merged or approved anything on GitHub.

## CI facts (verified with gh)
- Run 40068871793 "Build Android APK" (push, headSha be6ad77f436e…): success.
  - Release guards + tests (B1): success.
  - GameCI Unity Android (dev): success. Assert arm64 libil2cpp/libunity, apksigner v1/v2/v3, cert pin, versionCode, no testOnly all ran. "Release guard on built output" was SKIPPED, because it only runs on the release profile.
  - Emulator smoke API 34 x86_64 1080x2340: success (8m56s).
  - Emulator smoke API 36 x86_64 1080x2340: success (9m9s).
  - Publish prerelease: SKIPPED. It only runs on workflow_dispatch, so the dev-build-6 release was created outside the gated job. The release assets are byte-identical (sha256) to this run's CI artifact, so the integrity is OK.
- Run 40068871801 "Unity EditMode tests" (push, be6ad77): success.
  - The EditMode + PlayMode step and the "Fail on 0 tests, skips or failures" step both succeeded.
  - **EditMode: 157/157 passed, 0 skipped. PlayMode: 3/3 passed, 0 skipped** (Age_gate_to_L1_by_taps_only, Under_13_goes_to_the_block_screen, InputManager axes).
  - unity-tests-gate: success.
  - The PlayMode log shows that the L1 hint line (yaw 0, pitch 11) gives `state=Won destroyed=100% shots=1`, and the renderer-material (BUG-23) asserts pass inside the age-gate→L1 test.
- PR-event runs: server-ci 40070255974 success (test + emulator jobs). Unity EditMode tests 40070255962 was still pending at review time.
- Hard asserts in the smoke (Tools/ci/emulator_smoke_flow.sh): crash/ANR/Exception check after every step, Continue enabled, map within 30 s, `R1 = win` else exit 1, `R2 = fail` else exit 1, and no_magenta.py at 3% on screens 04–08.
- Magenta: I re-ran no_magenta.py at a 0.01% threshold on all screenshots for both APIs. Every screen shows **0.0000** magenta share.
- Logcat:
  - `CVTAP|RESULT|win` appears in calibration/logcat.txt (API 34 at 06:22:05Z, API 36 at 06:21:54Z, so 11:52 IST).
  - The pass-2 logcat is cleared before Retry, so only `CVTAP|RESULT|fail` appears there. Pass-2 win is evidenced by result_win.txt, which is "win" on both APIs.
  - The #36 stripping/missing-type patterns (qa/ci/logcat_fail_patterns.txt) gave **0 matches** across all 4 logcats. There are also no FATAL, ANR, Fatal signal, BoxCollider/SphereCollider or Exception lines from the app.
  - The only Unity warning is "Setting linear/angular velocity of a kinematic body is not supported" (≈3–6x at L1 start).
- No video was recorded. The smoke still only covers L1.

## APK facts (dev-build-6, both assets)
- package com.crashverse.game, versionCode **47**, versionName 0.1.0, minSdk 24, **targetSdk 34**, compileSdk 34.
- **application-debuggable = true**. That is expected for a dev build and must be false for release.
- CRASHVERSE-dev-arm-only.apk (69.0 MB) has native-code arm64-v8a and armeabi-v7a, with lib/arm64-v8a/libil2cpp.so (50.4 MB) and libunity.so present. CRASHVERSE-dev.apk (98.7 MB) adds x86_64.
- arm64 ELF LOAD alignment is **0x1000 (4 KB)**, so the APK is not 16 KB-page compatible.
- Install: "Performing Streamed Install / Success" on CI emulators API 34 and 36.
- Box emulator: **UNVERIFIED**. AVD qa34 can't boot because the box user has no /dev/kvm permission, and without KVM it's known to be too slow. I relied on the CI evidence instead.
- Permissions: INTERNET, VIBRATE only.

## Screenshots reviewed (qa/devbuild6/emulator-smoke-api3x/)
- 02/03 age gate: Month/Year/Continue. Continue turns bright green after March 2006 is picked. The privacy line is cut off ("never your date of").
- 04 map: lit floating island, node 1 (orange) and node 2 (locked), "offline" badge, flat placeholder buttons (Store/Settings/Titan/Daily/Season/Ranks).
- 06 L1 HUD: lit grass, wooden tower, cannon, aim arc, dev HINT button + FIRE, shots 4/4, 0%. No magenta.
- 07 win: "SMASHED!", Destroyed 100% (need 85%), Shots left 3, **Score 0 (best 3550)**. API 34 shows 2 stars with no mastery row, while API 36 shows 3 stars plus "Mastery complete". The win sequence animates (stars, then mastery at 1200 ms, then score count-up from 1500 ms), and the emulator runs about 13x slow, so the capture is most likely mid-animation.
- 08 fail (API 34): **"So close!" at Destroyed 0%**, Map/Retry oversized, and **no Free Hint on the 1st fail**, which is correct. On API 36 the capture was taken before the panel drew ("Settling…").
- Visuals: primitive props and a flat UI. They still don't match Art v2 or the SMASH CITY board.

## Known blockers: fixed?
| ID | Status | Evidence |
|---|---|---|
| BUG-23 / PX-1 magenta | **FIXED** | WorldMapBuilder/ArenaBuilder call RenderMaterials.Apply on every primitive (ce25c5f). BuildScript runs UrpSetup.Ensure(), which assigns URP Low/Mid/High to Graphics and every Quality level at build time (the committed QualitySettings still says customRenderPipeline 0, so it is build-time only). PlayMode AssertNoBrokenRenderers passes, and magenta share is 0.0000 on both APIs. |
| BUG-24 / PX-2 colliders | **FIXED** | Assets/link.xml preserves all of UnityEngine.PhysicsModule (Box/SphereCollider), plus MeshRenderer/MeshFilter/LineRenderer/TrailRenderer, AudioSource/AudioListener and ParticleSystem. The #46 dev aim hook is merged (894125e, Button_DevAimHint under CRASHVERSE_DEV‖DEVELOPMENT_BUILD). levels/ch1/ci_win_shots.json gives L1 yaw 0.0 / hintPitch 11.0 (elevation 10.78), and chapter1.json gives hintYaw 0.0 / hintPitch 11.0, so the two agree. The scripted win gives "win" on both APIs. |
| BT01 HIGH purge | **FIXED** | `Owns()` uses exact id == pid, `pid:` prefix, `|pid` board suffix, or playerId/ownerId == pid. Other players' friends docs only drop the pid (`RemoveAll(o => o == pid)`). Regression tests are in PurgeOwnershipTests (memory + emulator). Note: #1 was already merged to main on 2026-10-17 (12:56 IST) at 628854d; the fix c4ca0a5 is on main. Audit redaction still uses `detail.Contains(pid)`, which is acceptable for random pids. |
| BT-19-1 HIGH premium | **FIXED (server)** | `/v1/store/buy` rejects season_pass with USE_SEASON_UNLOCK. EconomyService.UnlockPremium charges 450 and writes the entitlement in one step, idempotently, and is locked while in gem debt. Covered by FollowUpTests. The client has no premium-unlock UI yet (LocalBackend only), so on-device: UNVERIFIED. |
| BT-10-1 MED gem packs | **FIXED (server)** | The catalog flags/requiresFlag are lifted out, and only the server's ENABLE_GEM_PACKS turns gem packs on; an unknown flag fails closed. Covered by StoreRulesTests. The client catalog still lists gems_small/gems_large (enableGemPacks:false), so they are hidden but present (see C-5). #10 is still OPEN and is not part of #52. |
| BT-19-2 LOW BE03_ST29 | **FIXED** | The fixture is now deco_lighthouse, and the test passes locally. |
| PX-3 audio | **INCLUDED, sound UNVERIFIED** | #32 audio library and #47 SFX wiring are merged. The APK has 382 audio .resource blobs (9.9 MB), matching the 382 clips under Resources/Music + SFX. AudioListenerGuard runs in GameBootstrap. The CI emulator runs with -noaudio, so actual sound needs the phone. |
| Free Hint rule | **FIXED** | `HintRules.ShowHintButton` requires `consecutiveFails >= 2`, is never shown in Titan ranked, and the level must have a hint. The 1st-fail screenshot shows no Free Hint. A 2nd-fail case is not covered by CI. (I couldn't find the rule text in crashverse_share.txt by keyword; I checked against the code and the earlier QA rule instead.) |

## Server tests (run locally on the box, be6ad77)
`dotnet test server/tests/Crashverse.Server.Tests`: **196 passed, 0 failed, 10 skipped**. The skips are Firestore-emulator tests, which CI runs in the server-ci "emulator" job (pass). Output: qa/devbuild6/server_tests.txt.

## Changes requested (none block the dev-build-6 phone test)
| ID | Sev | Evidence / repro |
|---|---|---|
| C-1 | MED | Win screen shows "Score 0 (best 3550)" on both APIs. This is likely a mid-count-up capture (the score step starts at 1500 ms and the emulator is about 13x slow), but the release notes list it as known. Verify on the phone. The smoke should wait for the win sequence to end before screenshot 07 and assert score > 0. |
| C-2 | MED | The #36 logcat stripping/missing-type gate (qa/ci/logcat_fail_patterns.txt) is **not wired into CI**. The smoke checkout is sparse (Tools/ci) and only greps crash/ANR/Exception. A manual run found 0 matches, but wire it in so BUG-24 can't come back silently. |
| C-3 | LOW | API 36 screenshot 08 is taken before the fail panel draws ("Settling…"), and API 34 07 vs API 36 07 show 2 vs 3 stars. Add a settle wait before screenshots 07 and 08. |
| C-4 | LOW (P2, PX-8) | Fail screen title "So close!" shows at 0% destroyed. Map/Retry are oversized. |
| C-5 | LOW | The client store_catalog.json still ships gem packs (hidden by enableGemPacks:false). Economy v1 has no gem store, so drop them from the v1 catalog or confirm with PM. |
| C-6 | LOW | No gameplay video, and the smoke only reaches L1 (Playtester gap). Add screenrecord and an L2 run. |
| C-7 | LOW | Age-gate privacy line is truncated ("never your date of"). |
| C-8 | INFO | "Setting velocity of a kinematic body" warnings at L1 start (≈3–6x). |
| C-9 | INFO | dev-build-6 was published outside the gated publish job (skipped on push). Assets are hash-identical to the CI artifact. Use workflow_dispatch next time. |
| C-10 | P1 (PX-5) | Visuals are still primitive and placeholder versus Art v2 and the SMASH CITY board. Explosions/debris (PX-4) can't be judged from stills: UNVERIFIED. |

## Release blockers (not for the dev build)
- R-1 HIGH, APK-02: targetSdk 34. Google Play requires 35+.
- R-2 HIGH: native libs are 4 KB-aligned (LOAD align 0x1000), so the APK is not 16 KB-page compatible, which Play requires for new apps/updates targeting Android 15+.
- R-3: debuggable=true and the dev HINT button must be gone in release. The "Release guard on built output" job must run and pass on the release profile.

## UNVERIFIED
- Box emulator install and launch: no KVM permission for the box user.
- Real sound, haptics and on-device rendering: need the user's Nothing Phone 3a.
- 2nd-fail Free Hint on device.
- Explosions/debris/damage stages: no video.

---
## Addendum: re-review at 6cf622e (2026-10-24, 1:40 PM IST)
Evidence is in qa/devbuild6b/. New commits since be6ad77: b7702a7, 9444737, 6fdf25c, d559b8a, 5b68ab0, eee598b, c8ad93c, 0524c64, 15adfe4, 6cf622e. That's 16 files, +202/−43.

### Verdict: APPROVE WITH CHANGES (unchanged). The rebuilt dev-build-6 is OK for the phone test. It is not release-ready.

### Runs on 6cf622e
- Build Android APK, run 40074274944 (workflow_dispatch, integration/v1, 6cf622e): all jobs succeeded, and this time Publish ran.
  - B1 guards: success.
  - GameCI build, including the new check_apk.sh (arm64, allowBackup=false, portrait-only): success.
  - Emulator smoke API 34 and API 36: success, both ending in "SMOKE PASS".
  - Publish: success.
  - "Release guard on built output" was skipped again (dev profile).
- Unity EditMode tests, run 40074235319 (pull_request, 6cf622e): **EditMode 157/157, PlayMode 3/3, 0 skipped**, and unity-tests-gate passed.
  - The push-event runs 40074232288 and 40074232294 were cancelled, so their gate correctly went red.
- server-ci, runs 40074235323 and 40074232272: success.

### Claims
| Claim | Result | Evidence |
|---|---|---|
| C-1 score > 0 | **PARTLY MET** | The smoke now hard-asserts score > 0 and stars ≥ 3, using a `CVTAP|RESULTDONE` line the probe writes once the win sequence time has elapsed. Both APIs logged `RESULTDONE|win|score=3550|stars=3|pct=100`. On screen, API 34 07 shows 3 stars, the mastery row and **Score 3550**, but **API 36 07 still shows 2 stars and "Score 0 (best 3550)"**. The assert checks the result data, not the rendered panel, and the screenshot is taken only 0.5 s after RESULTDONE on a CPU-rendered emulator. |
| C-2 fail-pattern gate | **MET** | The smoke job's sparse checkout now includes qa/ci. Both emulator_smoke.sh (calibration) and emulator_smoke_flow.sh build `PAT_RE` from qa/ci/logcat_fail_patterns.txt and use it in every check, and the run fails if the file is missing. My grep of all 4 logcats: **0 matches**. |
| C-3 settled screenshots, matching stars | **PARTLY MET** | Both 08 fail screenshots now show the settled panel. The 07 win screenshots do **not** match: 3 stars on API 34, 2 stars on API 36. |
| C-4 fail copy and buttons | **MET**, with a LOW leftover | At 0% the title is now "Out of shots!"; "So close!" only shows at ≥ 60% of the win threshold. The Map/Retry row no longer stretches and the buttons are normal height (both APIs). Leftover: the fail panel keeps its fixed 470-dp height, so there's a large empty area below the buttons. |
| C-7 age-gate line | **MET** | 02_agegate on both APIs shows "We only keep an age range, never your date of birth." over two lines. |
| C-9 publish only via the gate | **PARTLY MET** | `publish` needs [build, emulator-smoke] and only runs on workflow_dispatch with a tag. It refuses to overwrite a non-prerelease. However:<br>(a) it does not depend on unity-tests-gate or server-ci, which are separate workflows;<br>(b) the `dev-builds` environment has **no protection rules** and main is unprotected, so a manual `gh release upload` is still possible;<br>(c) it used `--clobber` to **replace the already-reviewed dev-build-6 assets in place** with a 6cf622e / versionCode 56 build, while the **dev-build-6 tag still points at be6ad77**. |
| Aim-mirror P0 (new) | **FIXED (logic), phone recheck needed** | This was found in the dev-build-6 phone test: a right swipe aimed left. AimController.Drag now uses `_yaw - dp.x*yawDegPerDp`, because +yaw is screen-left from the run camera. The new `AssertSwipeAimsSameWayOnScreen` (inside Age_gate_to_L1_by_taps_only) projects a point 10 m along the launch velocity through Camera.main and asserts a −120 px drag moves it left and a +120 px drag moves it right. It passed in run 40074235319. Gaps: the test calls `Drag()` directly rather than going through touch input, and the emulator smoke doesn't check swipe direction. |

### New APK / prerelease
- **Yes, a new APK was produced**, but it went into the **existing dev-build-6 prerelease** (assets updated 2026-10-24 08:09Z = 1:39 PM IST). Its notes say 6cf622e, run 40074274944. The release assets are sha256-identical to the run artifact (arm-only f0e4006f…).
- versionCode **56** (was 47), versionName 0.1.0, minSdk 24, **targetSdk 34 (unchanged)**, debuggable=true.
- New in the manifest: allowBackup=false, fullBackupContent=false, dataExtractionRules, portrait orientation.
- arm64 libs are still **0x1000 (4 KB) LOAD alignment (unchanged)**. Unity is 2022.3.50f1.
- Magenta share is 0.0000 on screens 04–08 on both APIs, per the CI log and the artifacts.

### Also changed (for PM's information)
- Age gate (6fdf25c, Security): the year list now opens at the neutral middle of the range (current year − 50), **not** at current year − 18. The tap test asserts that −18 is *not* visible. This reverses QA case V-02, so PM should confirm it and QA will update V-02.

### Still open
| ID | Sev | Item |
|---|---|---|
| C-1b/C-3b | MED | The API 36 win screenshot shows Score 0 and 2 stars, even though RESULTDONE logged 3550 / 3 stars. Wait for a second RESULTDONE repeat (≥ 2 s) before screencap, and/or log the label text. Confirm the score on the phone. |
| C-9b | MED | Publish each build to a new tag (e.g. dev-build-7) instead of clobbering a reviewed prerelease; the tag and assets currently disagree. Make publish depend on unity-tests-gate and server-ci. Add required reviewers to the `dev-builds` environment. |
| C-4b | LOW | The fail panel has a large empty area; size the panel to its content. |
| AIM-1 | LOW | Add a smoke check or input-level test that a right swipe moves the aim right; user to recheck on the phone. |
| C-5 | LOW | The v1 client catalog still ships gem packs (hidden by the flag). |
| C-6 | LOW | Still no gameplay video, and only L1 is covered. |
| C-10 | P1 | Visuals don't match Art v2 / SMASH CITY. Explosions and debris are UNVERIFIED (no video). |
| R-1 | HIGH (release) | targetSdk 34; Play needs 35+ (APK-02). |
| R-2 | HIGH (release) | Native libs are 4 KB-aligned, not 16 KB-page compatible. |
| R-3 | release | debuggable=true and the dev HINT button must go. The built-output release guard must run on the release profile. |

### UNVERIFIED
- Box emulator: no /dev/kvm permission for the box user.
- Real sound, haptics, swipe direction by touch, and the score on device: need the phone.

### V-02 update (PM ruling, 2026-10-24)
V-02 is superseded. The age-gate year list must open at the **current year − 50** (the neutral midpoint, per the Security change in 6fdf25c), not at the current year − 18. Pass criteria: on first open, the selected or visible year is current − 50 (±0), and current − 18 is not preselected. Any year is reachable by scrolling, with no future years.
Publish policy (PM): GitHub Pro isn't bought, so reviewers and protection are enforced by policy. Every build gets a new tag (dev-build-N) and a published build is never overwritten. QA rejects any build whose release files changed after review.
