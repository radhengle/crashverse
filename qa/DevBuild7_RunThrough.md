# QA run-through: dev-build-7 (main 7d359e6)
QA Test Lead, 2026-10-24 (IST). Evidence is in qa/devbuild7/.
Release: https://github.com/radhengle/crashverse/releases/tag/dev-build-7, published 16:09 IST.
The build is main after #52 (merged 15:03 IST) and #33.

## Decision: GO for the phone test. NO-GO for release.

## 1. Box emulator: did not get a usable run
- /dev/kvm is still `crw-rw---- root:103`, and the box user isn't in that group, so there's **no KVM**.
- I booted AVD qa34 (API 34 x86_64) with `-accel off` and a 15-minute limit. It booted in 282 s.
- Installing the dev-build-7 APK failed: `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. The AVD has an older com.crashverse.game signed with a different key.
- The old copy launched (pid 2761), but there were no CVTAP lines within 60 s. Android showed **"Process system isn't responding"** (box_emu/01_launch.png).
- I stopped the emulator after about 13 minutes.
- **Nothing in this report was checked on the box emulator.** Everything below comes from CI artifacts, CI logs and my own checks of the APK and code.

## 2. CI evidence (all runs on 7d359e6a2acc…)
| Run | Event | Result |
|---|---|---|
| 40083850198 Build Android APK | workflow_dispatch, main | B1 guards ✔, GameCI build ✔, smoke API 34 ✔, smoke API 36 ✔, Publish ✔ |
| 40083443252 Unity EditMode tests | push, main | **EditMode 157/157, PlayMode 3/3, 0 skipped**, unity-tests-gate ✔ |
| 40083443317 server-ci | push, main | test job: 201 pass / 0 fail / 10 skipped (emulator-only); emulator job: **211/211 pass** |

- **Win screenshots:** I opened 07_result_win.png from both API levels. Each shows SMASHED!, 3 stars, Mastery complete, **Score 3550 (best 3550)** and Destroyed 100%. That matches the logged `CVTAP|RESULTDONE|win|score=3550|stars=3|pct=100|label=Score_3550__(best_3550)` on both.
  - The smoke now checks the on-screen score text against the logged score and takes the screenshot 2 s after RESULTDONE.
  - The panel now fits its content.
- **Fail screenshots:** 08_result_fail.png on both shows the finished panel: **"Out of shots!"**, Destroyed 0% (need 85%), Map/Retry at normal size, no Free Hint. RESULTDONE logged fail with score 0.
- **Swipe direction:** this is a real touch test through AimController, with no shortcut hook. aim_direction.txt on both API levels says `swipe right -> yaw -25.0; swipe left -> yaw 25.0`.
  - +yaw is screen-left, so right goes right and left goes left. The smoke hard-asserts right < −1 and left > 1.
  - PlayMode `AssertSwipeAimsSameWayOnScreen` (on-screen projection through Camera.main) passed.
  - The source still has `_yaw - dp.x*…`.
  - There are **no swipe screenshots** in CI; the evidence is the logged yaw.
- **Magenta:** I re-ran no_magenta.py at a 0.01% threshold on all 16 smoke screenshots. Every one is **0.0000**.
- **Logcat:** across all 4 logcats (pass-2 and calibration, both APIs), the qa/ci/logcat_fail_patterns.txt patterns matched **0**. FATAL / Fatal signal / ANR / Unity Exception: **0**.

## 3. Release integrity
- The `dev-build-7` tag is at **7d359e6a2accb1a385b3210dba4d45ea29bb616b**. The release targetCommitish is the same, and so is the commit in the notes (run 40083850198).
- sha256 of the release assets equals the CI artifact:
  - arm-only: aa8bcda23a55…
  - full: 767a2ed0945c…
- **dev-build-6 was not touched again.** Its assets were last updated at 08:09Z (13:39 IST), and their hashes equal what I reviewed at 6cf622e (f0e4006f… / e082c614…). The tag is still at be6ad77, which is the old mismatch and is now frozen.
- The publish job:
  - `needs: [build, emulator-smoke]`, where the emulator-smoke matrix is API 34 and API 36.
  - It runs only on dispatch from main or dev/*.
  - It waits for unity-tests.yml and server-ci.yml to have a **successful run on the same SHA**. The log shows "unity-tests.yml: success on 7d359e6…" and "server-ci.yml: success on 7d359e6…".
  - It refuses an existing tag, requires the dev-build-N format, and checks the tag points at the built commit.
  - Push triggers are limited to main, so there's no integration/** signing anymore.
- Still open, LOW: the `dev-builds` environment has **no protection rules** and main isn't protected. Someone with write access could still publish a release by hand. The Unity/server condition is a "some successful run on this SHA" check inside the job, not a direct job dependency, but that's acceptable.

## 4. APK facts (CRASHVERSE-dev-arm-only.apk)
- versionCode **62** (> 56 ✔), versionName 0.1.0, minSdk 24, **targetSdk 34**, compileSdk 34.
- **debuggable = true** (dev profile).
- arm64-v8a/libil2cpp.so (50.4 MB) and libunity.so are present, plus armeabi-v7a. The full APK adds x86_64.
- **16 KB alignment: NO.** All arm64 .so files have LOAD alignment 0x1000 (4 KB).
- Audio: 382 .resource blobs (9.9 MB).

## 5. Regression against the last review
| Check | Result |
|---|---|
| Age gate: year list opens at current − 50 | ✔ `YearListMiddle = 50`. The PlayMode age-gate test passed. The smoke picks 1974 by name after 0 swipes, which shows the list opened around there. |
| Age gate: privacy line in full | ✔ 02_agegate shows "…never your date of birth." on two lines |
| Free Hint only from the 2nd fail in a row | ✔ `consecutiveFails >= 2`. The 1st-fail screenshot shows no Free Hint. The 2nd-fail case is not exercised by CI. |
| "Out of shots!" at 0% | ✔ on both APIs ("So close!" only at ≥ 60% of the threshold) |
| Audio clips present | ✔ 382 clips in the APK. **Audible sound is still UNVERIFIED** (CI runs -noaudio). |
| BUG-23 still fixed | ✔ RenderMaterials.Apply in WorldMapBuilder (4) and ArenaBuilder (10). BuildScript calls UrpSetup.Ensure. Magenta 0. |
| BUG-24 still fixed | ✔ link.xml keeps PhysicsModule. The PlayMode L1 hint line gives `Won destroyed=100% shots=1`, and the smoke scripted win is "win" on both APIs. |

## Open items
**Release blockers**
- R-1 HIGH: targetSdk 34; Google Play needs 35+ (APK-02). PR #7 is still open.
- R-2 HIGH: native libs are 4 KB-aligned, not 16 KB-page compatible. This likely needs a Unity 2022.3 patch with 16 KB support; the current one is 2022.3.50f1.
- R-3 HIGH: the release profile must be non-debuggable with no dev HINT button. The built-output release guard has never run on a release-profile build. AAB signing (PR #8) is still open.

**Not blocking the phone test**
- LOW: no gameplay video, and only L1 is covered by the smoke.
- LOW: no swipe screenshots; the evidence is the logged yaw.
- LOW: the dev-builds environment and main have no protection.
- LOW: gem packs are still listed (hidden) in the client catalog.
- P1: visuals still don't match Art v2 / SMASH CITY.
- UNVERIFIED (needs the phone): sound, haptics, real-finger swipe feel, explosions and debris.

## PR comment
None posted. dev-build-7 is a main commit (merge of #33 after #52), and there's no open PR for this build.
