# QA review: PR #55 "Integration v2: target UI, power-ups v2, seasons" (integration/v2 @ eb0126f)
QA Test Lead, 2026-10-24 (IST). Evidence is in qa/pr55/ (attempt1/, attempt2/, apk/, unity/).

## Verdict: BLOCK
The blocker is an unexplained app termination at the age gate. It is not a flake (see §1).
Everything else is either APPROVE-able or a change request.

## 1. "API 34 age-gate kill" (attempt 1): root cause
- Build Android APK run 40093293947:
  - **Attempt 1** (17:47 IST, eb0126f): the API 34 smoke failed. API 36 passed.
  - **Attempt 2** (18:07 IST, **same SHA eb0126f**, every job re-run): all jobs passed.
- Log: `MONTH=540,938`, `MONTH_OPT=540,1123`, then `FAIL (discovery): app died at after_tap_540_1123`. That's the tap on "March" in the month list. Attempt 2 used identical coordinates and passed.
- attempt1/api34/calibration/logcat.txt (the harness clears logcat before each tap, so this starts at 12:29:03.6Z):
  - 03.673: the tap ACTION_DOWN at 540,1123.
  - 03.687: app pid 5006 `AudioTrack stop`.
  - 03.698: `PersistentUnitySurface.preserveContent` and `SetWindow 0 0x0`, so the surface was torn down.
  - **03.702: `Unity: onDestroy`.**
  - **03.704: `Process: Sending signal. PID: 5006 SIG: 9`, sent by the app's own main thread** (Unity kills its process when its activity is destroyed).
  - 03.740: `ActivityManager: Process com.crashverse.game (pid 5006) has died: fg TOP` and `Force removing ActivityRecord… app died, no saved state`.
- Ruled out:
  - **Java/native crash:** there's no FATAL EXCEPTION, Fatal signal, tombstone or crash-buffer entry.
  - **ANR:** there's no `ANR in`.
  - **OOM/LMK:** there are no lmkd/am_kill lines, the kill came from the app's own pid, and the app was foreground TOP.
  - **Emulator death:** system_server and GMS kept running and the emulator was terminated afterwards by the harness.
  - **Harness timeout:** the harness's pidof check failed 3 s after the tap, because the process was already gone.
  - **System force-stop:** there's no `Force stopping` from ActivityManager. (That was the cause of the earlier API 34 verifier issue in run 40067356429.)
- **Conclusion:** this was an **app-initiated teardown of the foreground activity (onDestroy → self-SIGKILL) about 30 ms after a tap on the age-gate month list**. The trigger isn't captured, because the pre-tap logcat was cleared.
  - Application.Quit is the only in-code quit path, behind a "Quit CRASHVERSE?" confirm. The age gate's `OnBack()=>true` blocks it, and a ListModal back only pops.
  - The remaining candidates are an activity recreation that Unity turns into a process kill (a configuration change outside the manifest `configChanges=0x40003fff`) or an unknown finish() path.
  - This PR also changes targetSdk 34→35 and Unity 2022.3.50→62f3, both plausible sources of a regression.
- To the user, this looks like the app closing at the age gate, which is a P1 regression risk on Android 14. **It's treated as a BLOCKER, not a flake**, until:
  1. the smoke keeps the full log (`logcat -b all`, including the events buffer with `am_finish_activity`/`am_destroy_activity`/`configuration_changed`) instead of clearing it before each tap;
  2. the API 34 job is re-run at least 10 times on eb0126f's successor with 0 occurrences, or the trigger is found and fixed.

## 2. Release blockers R-1 to R-3 (from the built APKs)
Artifacts are from attempt 1 (same SHA): crashverse-android-apk (dev) and crashverse-android-apk-release-unsigned (release).

| Item | Result | Evidence |
|---|---|---|
| Unity 2022.3.62f3 | **PASS** | `unityVersion: 2022.3.62f3` in the workflow, and the libunity.so string is "2022.3.62f3" (dev and release) |
| 16 KB ELF alignment, every .so | **PASS** | readelf LOAD align **0x4000** on all 8 release .so files (arm64-v8a + armeabi-v7a) and all 12 dev .so files; the CI "16 KB report" printed OK for the 4 arm64 libs |
| 16 KB zip alignment of uncompressed libs | **N/A** | libs are **compressed** (`extractNativeLibs=true`; no Stored lib/ entries), so zip alignment doesn't apply. Play accepts this. LOW: uncompressed libs + `zipalign -P 16` are recommended. The PR's dev APK skipped zipalign/re-sign (unsigned PR build). zipalign isn't installed on the box. |
| targetSdk 35 | **PASS** | release and dev: targetSdk 35, compileSdk 35, minSdk 24 |
| debuggable false (release) | **PASS** | release manifest has no `android:debuggable`; badging has no application-debuggable. The dev APK is still debuggable (expected). |
| Dev hooks stripped from release | **PASS** | global-metadata.dat string counts, release vs dev: UITapProbe 0/2, CVTAP 0/1, Button_DevAimHint 0/1, DevAimHook 0/2, LocalBackendService 0/2, CRASHVERSE_DEV 0/2. The CI step "Release build metadata check passed (no dev aim hook symbols)" ran on the release APK. |
| How the smoke still works | — | There are two variants. The **dev** profile (CRASHVERSE_DEV: tap probe, dev HINT, local backend) is what the emulator smoke installs. The **release** profile is built separately and only checked statically (check_apk.sh + metadata guard). **Gap (MED): the release variant is never launched on an emulator**, so release-only paths (OfflineBackendService, stripped dev UI) are untested at runtime. |
| AAB job, signed .aab | **NOT VERIFIED, OPEN** | `.github/workflows/android-aab.yml` exists on the branch but is dispatch-only on main. It's not on the default branch (`gh` gives 404) and has **never run**, so no .aab exists to check. jarsigner/bundletool aren't on the box. |
| Release guard ran on the built APK | **PASS** | It ran in "GameCI Unity Android (release profile, PR guard)". In the dev build job it's still skipped (by design). |
| versionCode > 62 | **PASS** | **65** (dev and release) |

R-1 (targetSdk 35): **closed** for this build. R-2 (16 KB): **closed** (ELF). R-3 (non-debuggable, no dev hooks, signed AAB): **partly closed**. The APK is clean, but no signed AAB has been produced or verified, and the release variant has never been run.

## 3. CI (eb0126f)
- **Build Android APK 40093293947, attempt 2:** B1 ✔, dev build ✔, release build + guard ✔, smoke API 34 ✔, smoke API 36 ✔. Publish was skipped (PR).
- **Smoke artifacts:**
  - attempt2/api34 is from attempt 2. **attempt2/api36 is actually attempt 1's passing API 36 artifact**; attempt 2's re-upload didn't replace it in the listing. Same SHA.
  - Win, both APIs: `RESULTDONE|win|score=3550|stars=3|pct=100|label=Score_3550__(best_3550)`. **I checked both screenshots**: 3 stars, "Level 1 Completed!", Score 3550, Rewards +125 coins / +5 gems, Map / Retry / Next Level. The stars match the log.
  - Fail, both APIs: "Out of shots!" at 0%, Map/Retry, no Free Hint.
  - Swipe, both APIs: right → yaw −25, left → yaw +25 (correct direction).
  - Magenta: 0.0000–0.0001 on all 24 screenshots. The 0.0001 comes from the purple gem art, far below the 3% gate.
  - logcat_fail_patterns: **0** matches in all 4 attempt-2 logcats. No FATAL, ANR, Exception or "has died".
- **Unity EditMode tests 40093293988:** **EditMode 239/239, PlayMode 3/3, 0 skipped** (≥ 160 ✔); unity-tests-gate ✔.
  - New fixtures: FractureClassRules 26, MaterialFractureClass 15, HeroBuildingImport 20, VfxRecipe +15, **MetaV2Tests 3** (the only meta/power-up/season tests).
  - Under_13_goes_to_the_block_screen passed.
- **server-ci 40093293945:** success (201 pass + 10 skipped; emulator job 211/211). **No server changes and no new server tests in this PR.**

## 4. Features vs design: deviations
| ID | Sev | Area | Deviation / evidence |
|---|---|---|---|
| **V2-01** | **HIGH (BLOCKER)** | Stability | The app terminated itself at the age gate on API 34 (§1). |
| V2-02 | HIGH | Target UI / user requirement | The login buttons (Google/Facebook/Instagram/X/Email) are tinted text-only pills with **no official provider logos**, all "Coming soon" (04_login.png). The user requires official logos (UX-29). Use the official SDK/brand buttons. |
| V2-03 | HIGH | Economy v1 | Coins/gems/chests are kept **client-side** in `progress_v2.json` (PlayerProgress.Grant/OpenChest; the comment says "server stays offline"). Economy_v1 says "Gem balances are server-authoritative". It's editable on a rooted device and has to be reconciled when the server goes live. Needs a PM decision; if kept, it must be migrated or discarded server-side. |
| V2-04 | MED | Power-ups v2 / ranked | The Guided Rocket steer events are recorded client-side (`steer` InputEvent), but the **server has no v2 power-up logic** (no GUIDED/SHIELD/TIME_FRACTURE/steer in server/). Spec AC5 ("server replay reproduces the same result") isn't met, so v2 power-ups in a ranked run can't be validated. Either block v2 power-ups in ranked Titan until the server sim supports them, or add them to the server. |
| V2-05 | MED | Target UI | The map header overlaps: the player name renders vertically ("S m a") behind the star counter, and a lock icon sits under the ★0/30 pill (05_map_guest, 09_map_after_win). |
| V2-06 | MED | Target UI / gameplay | The L1 HUD shows a column of **5 empty orange circles** over the tower (power-up rail with no content), which hides the target (06_l1_hud). The arena itself is still primitive (flat grass, wooden boxes) compared with the board's 3D castle. |
| V2-07 | MED | Test coverage | Power-ups v2, seasons, rewards and daily boss are covered by only 3 EditMode tests (MetaV2Tests). There are no tests for the reward rules (first clear, 3★ gems, chest roll), Shield refund, Mega Bomb falloff, Time Freeze 200 ticks, Guided steer clamp, or season open rules, all pure functions that are easy to test. |
| V2-08 | MED | Release | The release variant is never launched on an emulator (§2). |
| V2-09 | LOW | Target UI | The age gate is still the flat v1 style. It's not Art v2 or the board's style (PR #38 is still open). |
| V2-10 | LOW | Target UI | The level-select preview is a flat 2D silhouette, where the board shows a 3D island render. All 4 power-up tiles are locked at L1, which is correct by unlock rules. |
| V2-11 | LOW | Economy | There are no gem sinks in the client yet: gems accumulate with nothing to spend them on, because cosmetics and the 450-gem premium track aren't in this UI. Acceptable for v2, but note it for playtest feedback. |
| V2-12 | MED | Economy | store_catalog.json still defines `allowedTypes: currency_pack` and 2 real-money gem packs: gems_small at 99 for gems:100 and gems_large at 499 for gems:600. The server's PurchaseService.cs still processes and consumes `currency_pack`. **I found no gem-pack gate flag** in Assets/ or server/ (grep for gempack/ENABLE_GEM_PACKS returned nothing), so the claim that they're "server-gated off" is UNVERIFIED. Economy v1 says there are no gem packs in v1, so remove them or add a server-side reject. There's no store UI in the screenshots captured. Carried C-5. |
| V2-13 | INFO | Board vs design | The board's Season 4 says "Jurassic World". The build correctly uses **Primeval Peaks** (meta_v2.json s4_primeval_peaks). There are no "Jurassic" strings in either APK's metadata; in source it appears only in the guard test. |

**Checks that pass:**
- **Power-ups:** no pay-to-win. All 8 unlock by `level_reached`, with no price or currency fields, and the spec says gems never buy power-ups or charges.
- **Free Hint:** shows only from the 2nd fail and never in ranked.
- **Rewards:** +5 gems on the first 3★ is earned only.
- **Seasons:** S1 open, S2–S4 "coming soon".
- **Under-13:** blocked (PlayMode test).

### Board match (design/TARGET_UI_user_2026-10-24.png) vs in-engine screenshots
| Board panel | Est. match | Notes |
|---|---|---|
| 1 Login | ~55% | painted coastal background, tagline, 5 providers in board order, guest button; no logo art and no provider logos (V2-02) |
| 2 Create profile | ~75% | title, avatar row, name field, badge row, Continue; dark panel instead of light card |
| 3 World map | ~65% | painted floating-island map, "Season 1 Sunny Shores" banner, path nodes, Daily Boss badge, locks, coin/gem bar; header overlap (V2-05), no "Next Season" gate |
| 4 Level select | ~55% | Level N with arrows, stars, power-up row, Play; flat preview (V2-10) |
| 5 Gameplay | ~35% | Steps Left badge, % bar, pause, side rail, cannon; primitive arena, empty rail (V2-06) |
| 6 Level complete | ~75% | 3 big stars, "Level 1 Completed!", Rewards coins/gems, Next Level; no chest reward tile |
| 7–11 Hard level, Daily boss, Power-ups, Seasons, Progression | **not captured** | not in the smoke flow: UNVERIFIED |

**Estimate: about 60% across the 6 captured panels (v3 was 38% on art mockups).** That's a large improvement on menus and meta; gameplay is still the gap. It isn't comparable to the full 11-panel figure until 7–11 are captured; please add screenshots of those screens to the smoke.

## Blockers (must fix before approve)
1. **V2-01 HIGH:** unexplained self-termination at the age gate (API 34). Capture the full logs and show 10 or more green API 34 runs, or root-cause and fix it.

## Changes requested (non-blocking for the phone build, blocking for release)
- V2-02 HIGH: official login logos.
- V2-03 HIGH: client-side gem wallet (PM decision).
- V2-04 MED: v2 power-ups in ranked vs server replay.
- V2-05, V2-06, V2-07, V2-08 MED.
- R-3: a signed AAB (run android-aab.yml on main and attach jarsigner/bundletool verification).
- V2-12 MED: remove the gem packs or gate them server-side.
- LOW: V2-09…V2-11.

Comment: https://github.com/radhengle/crashverse/pull/55#issuecomment-6384489623

---

# Re-review @ 603ccc0 (2026-10-30, QA Test Lead)
Evidence is in qa/pr55b/: run logs, per-job smoke logs, 4 smoke artifacts, the release APK, Unity results, and history/ with 15 smoke artifacts and 10 failed-job logs from the intermediate SHAs.
Runs on 603ccc0 (PR merge ref fafeaf3, all started 24 Oct 22:19 IST, **1 attempt each, all green**):
- APK 40113362834
- AAB 40113362825
- Unity 40113362848
- server-ci 40113362850

## Verdict: **NO-GO** (PM merge criterion 1 not met)

## 1. V2-01: age-gate self-kill (API 34)
- **Root cause: identified.** It's not a manifest configChanges gap. With full `-b all` logs (events buffer now captured), all 3 occurrences after 4ab1a1d show `wm_relaunch_resume_activity … com.crashverse.game/…UnityPlayerActivity,80000000`.
  - **0x80000000 = CONFIG_ASSETS_PATHS**: the app's resource/asset paths changed because of a late post-boot overlay or package commit from GMS/setup.
  - Android relaunches the activity: `wm_on_stop_called … handleRelaunchActivity` → `Unity: onDestroy` → `Process: Sending signal. PID: <app> SIG: 9` (self) → `has died: fg TOP`.
  - A manifest can't declare this config. The developer says so too (commit 8b01644).
- Occurrences on 4ab1a1d-based SHAs, from artifacts in history/ (times converted from UTC):
  - 63a7876 api34-2: 24 Oct 19:32:55 IST
  - 3b2ee95 api34-3: 20:33:13 IST
  - 3b2ee95 api34-2: 20:34:19 IST
- **App-side fix: none.** 4ab1a1d adds colorMode/fontWeightAdjustment/grammaticalGender to configChanges, which doesn't cover asset paths. The mitigation is **harness-only**:
  - 8b01644: Play Store disabled, plus a wait for 2 quiet 15 s windows (log: `settle: quiet=2 after 30 s`)
  - 603ccc0: AOSP `target: default` image with no GMS. 603ccc0 logcats contain 0 `com.google.android.gms` lines and 0 crashverse relaunches.
  - So CI no longer exercises the trigger.
  - **Product risk remains:** on a real GMS phone, an overlay, WebView or GMS package update while the game is running relaunches the activity, and Unity then closes the game. This needs a Unity-side mitigation and a device test. **Launch blocker.**
- **Logging: met.** No per-tap `logcat -c`; a single continuous `logcat_all.txt` with 5 buffers (events, kernel, main, radio, system); exit_info.txt and activities.txt dumps are captured.
- **Green API 34 runs on 603ccc0: 3, from 3 matrix jobs (rep 1–3), each with 1 smoke pass. NOT ≥ 10. No repeat loop exists.**
  - `smoke_infra_retry.sh` wasn't triggered in any of them (no attempt1/ folder).
  - Across every SHA since 4ab1a1d there are 10 green API 34 jobs out of 18 non-cancelled (ded59c8 3, 8b01644 2, 9bc2a05 2, 603ccc0 3), but they're on different SHAs, images and harnesses.
- **Kill signatures for com.crashverse.game on 603ccc0** (all 4 jobs, dev + release logcats): onDestroy 0, `has died` 0, FATAL/Fatal signal 0, `ANR in com.crashverse.game` 0.
  - Each api34 `logcat_all` has one `SIG: 9`. It comes from system_server after `Force stopping com.crashverse.game … from pid <shell>` / `am_kill … stop com.crashverse.game`, i.e. the harness force-stopping the dev app before it installs the release APK. That's expected, not a self-kill.
- **Attempt history on 603ccc0:** 1 attempt per workflow, no red or cancelled attempt.
  - **Red on earlier PR SHAs** (flagged):
    - 63a7876: 4/4 smoke red (3 GMS boot-ANR pattern hits, plus 1 self-kill)
    - 02d83b4: cancelled (superseded)
    - 3b2ee95: 3 red (2 self-kills, 1 GPU-hang ANR in com.crashverse.game), API 36 cancelled
    - 8b01644: api34-3 GPU-hang ANR ("stuck fence. Indicates GPU hang") and api36 swipe-direction fail
    - 9bc2a05: attempt 1 smoke skipped; attempt 2 api34-2 red: "Pixel Launcher isn't responding" over the age gate, `calibration failed: no coordinates for MONTH`
  - **Open: GPU-hang ANRs were reported against com.crashverse.game twice** (SwiftShader). They're classified as emulator infra and now retried once automatically. They're UNVERIFIED as app-innocent.

## 2. Gem packs: PASS
- **Correction to my eb0126f V2-12:** the server gate **did exist** (Models.cs `EnableGemPacks = false`, env `ENABLE_GEM_PACKS == "true"`). My grep output was truncated, so "no gate" was wrong.
- Server, off by default:
  - Program.cs:341 throws `403 GEM_PACKS_DISABLED` before any Play call.
  - Test `FollowUpTests.GemPacks_OffByDefault_RejectedAndNothingGranted_ServedInFlags`: flags false; gems_small and gems_large → 403 GEM_PACKS_DISABLED; 0 gems; no purchase docs.
  - server-ci: 201 pass + 10 skip / 211 total, and the emulator job passed 211/211.
  - I didn't run it locally (no dotnet on the box), so I couldn't confirm that test individually.
- Client:
  - catalog `flags.enableGemPacks:false`, both packs `requiresFlag`
  - `StoreValidator` drops gem packs client-side even if the flag is on (`RealMoneyGemPacksInClient=false`)
  - test `store_gem_packs_flagged_off_v1` (B1: 5721 passed / 184 cases)
  - The store screen isn't in the smoke screenshots, so the visual check is UNVERIFIED.

## 3. Social login: PASS
- **Screenshots** (dev `emulator-smoke-api34-1/04_login.png` and release `emulator-smoke-api34-1/release/r3_after_gate.png`): 5 neutral grey text-only pills reading "Continue with Google/Facebook/Instagram/X · Coming soon" and "Create with Email · Coming soon". No logos, no brand colours. Footer: "Online sign-in is coming soon…".
- **Code:**
  - `ProviderColor` is a constant #5B6472.
  - The action is `null` and `interactable = ProfileRules.SocialSignInEnabled(prov)`, which is a constant `false` (PlayerProgress.cs:99), so the buttons can't be clicked.

## 4. Other developer claims
| Claim | Result |
|---|---|
| Map header | **PARTIAL (MED).** The name is now on its own line ("Smasher_5086"), so it's not cut off. **But the top locked level node still renders under the ★ 0/120 pill** (05_map_guest, 09_map_after_win on API 34 and 36). |
| L1 empty orange circles | **FIXED.** 06_l1_hud has no circle strip. The arena is still primitive (carried LOW). |
| Meta tests | **IMPROVED: 3 → 14.** MetaV2 3, MetaRulesEditMode 6 (rewards, stars, season unlock, daily boss, hard-only, chest roll), OfflineMetaEditMode 5 (achievements, daily login rollback, leaderboard, season pass, S2–4 layouts). PlayMode 3 → 6 (+ offline age band final, never relaxed, wipe). **Still no tests for the power-up v2 effects** (Shield/Mega Bomb/Time Freeze/Guided steer), LOW. |
| Release-variant launch | **PASS.** All 4 smoke jobs install `crashverse-android-apk-release-unsigned` (artifact 12567628354, the APK checked in §5; no debuggable flag; no "Development Build" watermark). Log: `release launch: 94.2% of the screen changed after Continue` / `release launch OK: past the age gate, app alive`. Screenshots r1–r3; the release logcat has 0 kill signatures. The release APK now also carries x86_64 (for the emulator), so the shipping build is the AAB. |
| Signed AAB | **NOT MET / UNVERIFIABLE before merge.** The PR job built and guard-checked an **unsigned** AAB (`16 KB check: OK`, `Release build metadata check passed`). "Upload-key secrets present?" and "Sign AAB… (main dispatch only)" were **skipped**. No AAB workflow run on main ever. The AAB artifact expired (1-day retention). jarsigner/bundletool aren't on the box. Launch blocker until a main dispatch shows `jarsigner -verify` against the pinned upload cert. |

## 5. R-1 / R-2 on the 603ccc0 release APK: PASS
- versionCode **75** (> 65), targetSdk **35**, compileSdk 35, no `android:debuggable`, allowBackup=false.
- All 12 .so files (arm64-v8a, armeabi-v7a, x86_64) have LOAD align **0x4000**. Libs are deflated (extractNativeLibs=true), so zip alignment is N/A.
- Unity 2022.3.62f3 (96770f904ca7).
- global-metadata.dat counts: UITapProbe/CVTAP/Button_DevAimHint/DevAimHook/AimHook/LocalBackendService/CRASHVERSE_DEV/TestClock/DevHint all **0**, Jurassic 0.

## 6. Regression: PASS
- **All 4 smoke jobs:**
  - `RESULTDONE|win|score=3550|stars=3`. The 07_result_win screenshot shows 3★, "Level 1 Completed!", Score 3550, +125 coins / +5 gems.
  - Fail: score=0.
  - Swipe right → yaw −25, left → yaw +25 (+24.6 on one job).
  - Magenta max 0.0001 on 9 screens per job.
- **logcat_fail_patterns:** 0 matches in all 12 logcats (dev + logcat_all + release).
- **unity-tests-gate:** ✔ EditMode **267/267**, PlayMode **6/6**, 0 skipped.
- **server-ci:** ✔ (201+10 skip; 211/211).
- **Under-13:** `Under_13_goes_to_the_block_screen` passed.
- **"Jurassic":** only in the guard test (Tools/Tests/Tests.cs); 0 in the APK.

## Blockers
- **Merge (NO-GO):**
  - **V2-01:** < 10 green API 34 runs on 603ccc0 (3). The root cause is known, but the app isn't fixed; it's only masked by removing GMS from the CI image.
  - Need: either a 10× repeat loop on the merge SHA (ideally including at least one GMS image with the settle step), or a Unity-side handling of the asset-path relaunch.
- **Launch (deferred, logged):**
  - V2-01 asset-path relaunch on real phones
  - signed AAB not yet produced
  - server-authoritative gems (V2-03)
  - server validation/replay of the v2 power-ups (V2-04)
- **Non-blocking:**
  - map header lock overlap (MED)
  - power-up v2 effect tests (LOW)
  - GPU-hang ANR classification (LOW, watch)

Comment: https://github.com/radhengle/crashverse/pull/55#issuecomment-6524426044
