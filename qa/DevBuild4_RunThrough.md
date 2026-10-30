# Dev-build-4 run-through: QA, 2026-10-16
APK: release dev-build-4, CRASHVERSE-dev-build-4.apk (83.2 MB), /workspace/apk/db4. PR #23 head is now **e95590d**, not bf2d3e5. The commits since then are a58d411, 41c1c24 (tap-test fixes) and e95590d (BUG-21 signing and publish gates).

## Verdict: BLOCKED. The UI flow could not be run on the box emulator. This is not a pass.
| # | Step | Result |
|---|------|--------|
| 0 | SDK install (cmdline-tools, platform-tools, emulator, API 34 google_apis x86_64), AVD qa34 | PASS |
| 1 | Emulator boot with KVM | **BLOCKED**. /dev/kvm exists and `-accel-check` says usable, but qemu sits at 0.3% CPU and ~240 MB RSS with the guest never starting, and adb stays offline for >10 min. Another agent's AVD "t" (pid 386210) on the same box has been stuck the same way for 45+ min. KVM looks unusable here, probably nested virt. |
| 2 | Emulator boot with `-accel off` (TCG) | PASS, slowly: ~10 min to adb, ~20 min to boot_completed. The system then thrashes, with repeated "bg anr" kills of gms, phone and media. |
| 3 | adb install | PASS (`Success`). On x86_64 the first try hit an NPE because the system wasn't ready yet. |
| 4 | Launch | INCONCLUSIVE. IL2CPP JNI_OnLoad and Unity onResume ran, then onPause and onDestroy after ~8 s, then SIG 9 from the system ("app died, no saved state") while the system was overloaded. There's no FATAL EXCEPTION, Fatal signal or managed exception from the app in logcat. Under TCG this can't be told apart from an emulator-load kill. |
| 5–10 | Age gate Month/Year (BUG-20), login, map, L1, win, fail | NOT RUN |

Artefacts in qa/db4_run/: 01_launch.png (home screen after the app died), logcat_01.txt, emulator_tcg.log, emulator_kvm_hang.log.
No new app bugs filed. **BUG-20 is not verified by QA on device or emulator.** It is still unverified until the user's phone, or the CI emulator-smoke job, confirms it.

## Ways to get a real run-through (for PM/Developer)
1. Best: let CI do it. #23 adds `emulator-smoke` (reactivecircus, API 34 x86_64, KVM on GitHub runners). Extend its script to drive the flow and upload the screenshots (adb script below).
2. Fix KVM on the box, or give QA a host with working nested virtualisation. Then the same script runs locally in about 5 min.
3. adb script for the flow (coordinates come from a `uiautomator dump`, but Unity UI isn't in the accessibility tree, so use fixed 1080x2340 coordinates plus screencaps):
   `install -r -g` → `monkey launch` → wait for pidof → screencap 02_agegate → tap Month → tap March → tap Year → tap (year-25) → screencap 03 (Continue enabled) → Continue → login (guest) → 04_map → node 1 → Play → 05_hud → drag shots → 06_win; replay and miss all shots → 07_fail. After each step, `logcat -d | grep -E "FATAL|Fatal signal|Exception|ANR in com.crashverse"` fails the run.
4. AltTester Unity SDK in dev builds only (CRASHVERSE_DEV define, stripped from release by check_release_guards). This lets tests find buttons by name (Button_March, Button_Play …) on device or emulator, so they don't depend on coordinates. It's the recommended long-term approach.

## PR #23 review (read-only, nothing posted)
**Age gate fix (BUG-20):** the root cause is plausible and the fix is right. Active Input Handling = legacy (`activeInputHandler: 0`) with StandaloneInputModule, but there was no InputManager.asset, so the Horizontal/Vertical/Submit/Cancel axes were missing and the module threw every frame, which killed all taps. The PR adds InputManager.asset with the default axes, adds a CI guard (check_release_guards: module matches the handler and the axes exist), adds the age gate error caption, keeps Continue disabled until both values are set and the date isn't in the future, and paints an under-safe-area panel with raycastTarget=false. Good.
Findings:
- M1: the guard checks UIFramework.cs by string match only. Fine for now, but a PlayMode or EditMode assertion (already in the tests) is the real gate.
- M2: `android-apk.yml` publish now allows `workflow_dispatch` from any branch. That means unreviewed PR branches can produce signed, published dev builds. Restrict it to protected branches, or publish dispatch builds as pre-release marked "not QA-run".
- L1: the emulator-smoke job only checks launch and liveness, so it would not have caught BUG-20. Add the age gate taps (script above).
- L2: `UnderSafeArea` is the router's first child. Confirm it doesn't cover the camera or 3D view in gameplay, where the HUD sits over the world.
- Signing (BUG-21): good. The keystore comes from secrets, umask 077, is deleted in an always() step, the cert is pinned, versionCode = run_number, and testOnly is checked.

**Why the PlayMode tap tests fail in batchmode (EventSystem):**
1. In `-batchmode` the editor has no focused Game view. `EventSystem.isFocused` is false and StandaloneInputModule skips processing (`if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus()) return;`). `SendMessage("OnApplicationFocus", true)` is fragile: focus can be reset by the editor in later frames.
2. Faking Touch via `BaseInput` only works if the module takes the touch path. `ProcessTouchEvents` runs only when `input.touchSupported && touchCount>0`; the override does that. But `ShouldActivateModule`/`IsModuleSupported` and `forceModuleActive` matter. Without forceModuleActive, a module that isn't "supported" (no mouse, touch reported unsupported by the editor platform) never activates.
3. With `-nographics`, Screen size can be tiny or default and canvas scale differs, so WorldToScreenPoint can miss the raycast target. The Tap() diagnostics added in a58d411 will show this.
4. PlayMode in batchmode via GameCI runs frames, but `Time.realtimeSinceStartup` waits work. OK.

**Suggested fix (most robust first):**
- Subclass the module for tests: `class TestInputModule : StandaloneInputModule { protected override bool ShouldIgnoreEventsOnNoFocus() => false; }` (or set `forceModuleActive = true` and override). Swap it in during SetUp, and/or
- Skip the input module for click dispatch and use `ExecuteEvents.Execute(btn.gameObject, new PointerEventData(EventSystem.current){position=p}, ExecuteEvents.pointerClickHandler)` after asserting `RaycastAll` at p hits the button first (which proves it's reachable and not covered). Keep one test that goes through the module, which covers BUG-20 via the InputManager axes test (InputManager_has_the_axes…, an EditMode-safe check).
- Run PlayMode in CI without `-nographics`, and set the resolution explicitly (`-screen-width 1080 -screen-height 2340`) so the layout matches a phone.
- Keep the "0 PlayMode tests = fail" gate (good fix for the #9 false green), but note it makes the whole unity-tests job red until the above lands.

**QA verdict on #23:** approve the BUG-20 code fix and BUG-21 signing, with changes requested on M2 (dispatch publish) and on the tap-test harness. BUG-20 itself has **not** been verified by QA on a running build.

---
# Addendum, 2026-10-16 22:15 IST: CI emulator smoke run 38929445231 + PR #23 at 9306946
Artifacts: qa/smoke_38929445231/{api34,api36}. Run head = **289de99**, which is *before* the smoke fixes in c2021bb. Both smoke jobs FAILED and publish was skipped. Each API produced only the calibration pass: 1 screenshot + logcat + coords.txt. Pass 2 (QA flow) never ran.

## Per-API results
| Step | API 34 | API 36 |
|---|---|---|
| Install + launch, Unity boots | PASS (IL2CPP up, focus true, tap probe logging) | PASS (onResume), screen covered by system dialogs |
| Age gate Month opens list | Tap at 540,1170 (coordinate taken before layout settled) opened the **Year** list, not Month. Settled Month = 540,1017 | FAIL: "System UI isn't responding" + "Viewing full screen" bubble over the app. Probe never logged Button_Month |
| Month/Year filled, Continue enabled (BUG-20) | NOT REACHED (Continue logged interactable=0, as expected while empty) | NOT REACHED |
| Map / L1 HUD / win / fail | NOT RUN | NOT RUN |
| App crash / ANR | None from com.crashverse.game. ANRs are gms.persistent only (emulator noise) | None from the app. System UI ANR (emulator) |

Positive evidence: on API 34 a real `adb input tap` opened a list modal, so **touch input reaches the UI on dev-build-4-era code. The BUG-20 symptom (all taps dead) is not present.** Full BUG-20 verification (both fields set → Continue enabled → map) is **still not shown**.

## Visual notes (only 1 in-app screen captured: the Year list modal, API 34)
- V-01 (Med): the age gate list modal is flat placeholder UI (plain navy panel, gold border, system font, rectangular buttons). It doesn't match the user's SMASH CITY board style or Art v2 (01_login/02_profile chunky rounded buttons, display font, gradients). The Art v2 set has no age gate screen, so Art needs to supply one.
- V-02 (Low/UX): the year list starts at 2026 (current year) and needs ~25 swipes to reach an adult year. Start at the current year minus 18, or centre the list there.
- V-03 (Low): the modal floats in a black screen with no background art.
- "Development Build" watermark is expected (dev profile).

## PR #23 @ 9306946 (read-only)
- **M1 fixed**: `input_axes()` now parses InputManager.asset structurally (only m_Name items directly under m_Axes). The module check against UIFramework.cs is still a string match (acceptable, and the PlayMode/EditMode axis test backs it up).
- **M2 fixed**: publish build only runs on `main` or on `workflow_dispatch` from `refs/heads/dev/*`, and the publish job also needs `publish_tag` plus smoke on every API. Suggest protecting `dev/*` branches.
- **L2 fixed**: the single full-screen panel is replaced by `SafeAreaInsets`, 4 strips painted only *outside* Screen.safeArea with raycastTarget=false. It doesn't cover gameplay. 
- **Batchmode tap tests: NOT fixed.** FirstRunTapTests still relies on `SendMessage("OnApplicationFocus")`. There's no focus-ignoring module override, forceModuleActive or ExecuteEvents fallback, and the unity-tests runs for 9306946/cede080 are *cancelled*, so there's no green PlayMode evidence.
- **Script changes:**
  - FIRE tap after each drag: present in both calibration and flow (`fire()` / `ctap $FIRE`). OK.
  - Guest step: not removed. It's replaced with a tap at 540,20 (status bar, no-op) and `check login`, so the crash/death check still runs. OK, but the 04_login shot is now the map, so name it accordingly.
  - Flow pass keeps `check` (FATAL/Fatal signal/ANR in pkg/Unity Exception + pidof) after launch, month, year, continue, login, l1_load, every shot and retry. **05_map screenshot kept.** OK.
  - Gap: the calibration pass (pass 1) has no crash/pidof checks, so a crash there shows up only as a "not found" timeout. Add `check`.
  - Gap: no assertion that Continue is enabled before tapping it (03 is visual only). Assert the probe line `CVTAP|AgeGateScreen|Button_Continue|..|1` so BUG-20 is machine-verified.
  - Gap: no assertion that the result screen is a win vs a fail. Grep for a probe/result log line.
  - Note: the year search `grep CVTAP|ListModal|Button_$YR` reads the whole log, but ctap clears it first. OK.

**Verdict on #23 @ 9306946:** approve with changes. M1, M2 and L2 are resolved. Before merge it needs: (1) the batchmode tap-test fix, or at least a green unity-tests run; (2) a green emulator-smoke run on API 34 + 36 at the new head with the full 01–08 screenshots; (3) Continue-enabled and win/fail assertions in the flow. BUG-20 stays **open / unverified** until (2).
