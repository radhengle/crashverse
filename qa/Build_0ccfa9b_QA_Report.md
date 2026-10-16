# CRASHVERSE — Build 0ccfa9b QA Report (code level) — 2026-10-16 IST
Build: `radhengle/crashverse` main @ 0ccfa9b, from `/workspace/crashverse_m1.zip`, unzipped to `/workspace/qa_build_0ccfa9b`. `/workspace/crashverse` untouched. No game code changed.
No APK (Unity licence), no Unity Editor on the box. Every device/visual check stays **OPEN**.

## Summary
- **Developer's 41 logic tests: 41/41 cases passed (417 assertions, 0 failed)**, run with .NET 8 (`~/.dotnet/dotnet`). This is **not** a Unity compile or Unity Test Runner run: there's no Unity on the box, and the logic compiles against the repo's minimal UnityEngine stubs.
  - The documented command (`dotnet run --project Tools/Tests`, BUILD.md:71) **fails as shipped**: `.gitignore` excludes `*.csproj`, so the zip has no project file (BUG-01). QA added a harness-only project, `Tools/Tests/QA_Harness.csproj` in the QA copy (Tests + Stubs + all runtime Scripts except Editor), and ran it.
- **QA logic-level live-ops run: 61 checks, 53 pass, 8 fail.** Harness: `/workspace/qa_harness_0ccfa9b/QATests.cs`. It calls the shipped classes directly (LocalBackendService with the stub test clock `SetTestClock`/`SimulateOffline`, CampaignSave, ComboTracker, CampaignScore, StarRules, StoreValidator, TitanRunJournal).
- Code re-verification: SG-01, SG-20–24 and QA-UI-001–007 have been addressed in code, with the exceptions listed below.
- Top failures:
  1. The Titan journal can be edited and re-signed for a higher ranked score (SEC-01, High).
  2. An offline-queued killed run loses its kill bonus (BUG-04).
  3. Campaign score uses Round instead of floor (BUG-05).
  4. There's no season rollover and no real previous-season board (BUG-06/07).
  5. The hint doesn't appear in events or Titan practice (BUG-08).
  6. The device clock affects the stub (BUG-03).

## 1. Code re-verification
| Item | Result | Evidence |
|---|---|---|
| SG-01 % shots left, per-level thresholds, defaults 0.60/0.30, ≥ | **PASS** | `Meta/StarRules.cs:10-13` defaults; `:31-32` integer `shotsLeft*100 >= pct*total`; per-level `Data/ContentModels.cs:29` star3Pct/star2Pct; `Level/LevelConfig.cs:20-21,32-33` delegates to StarRules (hard-coded 5-shot table removed). Tests stars_budget4–7, S6a/S6b pass |
| SG-24 exact thresholds (3/5) | **PASS** (Low caveat BUG-09) | Integer maths `StarRules.cs:31-32`; test `stars_exact_3_of_5_int` passes. Caveat: thresholds are rounded to whole percent first (`:16`), so 0.625 becomes 63 |
| SG-20 server time only | **PARTIAL / FAIL on stub** | `LocalBackendService.cs:66-73`: ServerNow = device UtcNow + offset unless a fixed test clock is set. Device-clock changes move the stub's day (QA DT-08 FAIL). Header comment `:14` overstates this. Needs a real server |
| SG-21 islandKillY after settle | **PASS** | `Meta/MasteryEvaluator.cs:38-39` strict `y < islandKillY` on the highest role-piece centre; filled after settle `Destruction/DestructionTracker.cs:102-112`, `Core/GameSession.cs:117,159-161`. Repo test mastery_SG21_islandKillY passes |
| SG-22 cutoff 00:15:00.000 inclusive | **PASS** | `LocalBackendService.cs:254-255` (`now > dayStart+1d+15min` rejects). QA: .999 / .000 accepted, .001 refunded as practice |
| SG-23 best-run cap | **PASS** | `LocalBackendService.cs:354-371` (cap at 3, XP only above previous best). QA runs 1,2,2,3 → XP 10/20/20/30, best 3 |
| QA-UI-001 safe area / CanvasScaler | **PASS (code)** | `UI/UIFramework.cs:37-49` SafeArea anchors to Screen.safeArea, re-applied on rotation; `:18-31` DpCanvasScaler. Device check OPEN |
| QA-UI-002 dp sizing | **PASS (code)** | scaleFactor = dpi/160, min 320dp width `UIFramework.cs:15,21-26`; theme minimums `UI/UITheme.cs:20-26` (tap 48dp, icon/tab 48dp, map node 52dp). Test ui_theme_qa_minimums passes. Device check OPEN |
| QA-UI-003 HUD taps don't move the aim | **PASS (code)** | `Weapons/AimController.cs:87,104` uses `EventSystem.IsPointerOverGameObject` for touch and mouse |
| QA-UI-004 Android back | **PASS (code)** | `UIFramework.cs:142` Escape → HandleBack; gameplay back → pause `UI/Screens.cs:314`; results back → map `:376`; map root asks to quit `:100` |
| QA-UI-005 star formula | **PASS** | Same as SG-01 |
| QA-UI-006 unencrypted saves not used for ranked | **PASS with SEC-01** | PlayerPrefs is no longer used. Campaign save is a JSON file (`Meta/CampaignSave.cs:40-53`); ranked state lives in the backend DB. But the ranked journal (`Core/TitanRunJournal.cs:15,29`) is plain JSON and gets re-signed on flush (SEC-01) |
| QA-UI-007 live-ops code present | **PASS** | `Backend/LocalBackendService.cs` (Titan, rewards, season, boards, maintenance), `Data/Clock.cs:9-15` UTC day keys |
| QA-UI-008 orientation | **OPEN** | `ProjectSettings/ProjectSettings.asset:10` is still `defaultScreenOrientation: 4` (auto-rotate) |

## 2. Developer logic tests
`dotnet run --project Tools/Tests/QA_Harness.csproj` → **"417 passed, 0 failed (41 cases)"**. All 41 named cases passed (stars ×10, chapter/layout/unlock/loadout, mastery, test clock, Titan, streak, season, leaderboard, combo, score, hint, store, wrecker dims, UI theme, star bands).

## 3. Live-ops / meta cases at logic level (Feature_Test_Cases_v1.2)
| Case | Result | Evidence |
|---|---|---|
| DT-02 config swaps at 00:00 UTC | PASS | 23:59:59.999 → 2026-10-16 (slag_foundry/brittle_brick); 00:00:00.000 → 2026-10-17 (ash_bridge/heated_steel) |
| DT-02b new ranked attempt at reset | PASS | Blocked the same day, available at 00:00 UTC |
| DT-03 grace .999 / .000 / .001 | PASS ×3 | Accepted / Accepted / RefundedAsPractice |
| DT-04 same config per UTC day | PASS | Identical at 10:00 and 22:00 UTC |
| DT-05 one ranked attempt (consumed at start) | PASS | 2nd BeginRankedRun throws |
| DT-06 kill mid-ranked → partial | PASS | Accepted, 3,100 (no bonus) |
| DT-07 reinstall | BLOCKED | Stub DB is on device (`LocalBackendService.cs:15`); needs a real server |
| DT-08 device clock tamper | **FAIL** | Device clock +1 day → new ranked attempt available (BUG-03) |
| DT-10/10b/11 Titan score | PASS ×3 | 8000+3×500 = 9,500; damage capped at 10,000; fail = damage only |
| DT-12 top 10% bonus | PASS (repo test top10_bonus_after_close) | — |
| DT-13 offline ranked disabled | PASS | BackendException |
| DT-13b queued killed run | **FAIL** | Killed run (6000, 3 left) flushed from the journal → 6,000, not 7,500 (BUG-04) |
| SEC-01 edited journal | **FAIL** | Journal damage edited 1200→9999 → Accepted, score 9,999 |
| DR-01/02 calendar + loop | PASS | 7 claims, then index 0 |
| DR-03/04 missed days pause | PASS | After 3 claims + 11 missed days, next = Day 4 |
| DR-05/08 once per UTC day | PASS | 2nd claim throws |
| DR-06 05:29:59 → 05:30:00 IST | PASS | New claim allowed |
| DR offline claim | PASS | Blocked |
| DR-07 clock tamper | FAIL (same root cause as BUG-03) | Stub follows the device clock |
| SE-01 weekly rollover Mon 00:00 UTC | PASS | Sun 23:59:59 = week 0; Mon 00:00 = week 1 (season start 2026-10-12, a Monday) |
| SE-03a tier boundaries (12 pairs) | PASS | 99/100 … 4499/4500 |
| SE-03b best-run cap | PASS | XP 10/20/20/30 |
| SE-04 overflow chests | PASS | 4799→0, 4800→1, 7500→10, 20000→10 |
| SE-07 unclaimed tiers to inbox | **FAIL (code)** | Only free tiers are sent; premium is skipped `LocalBackendService.cs:494` (BUG-07) |
| SE-08 season rollover | **FAIL** | After season end: same id, XP unchanged (30→30), weekIndex 4 (BUG-06) |
| LB-02 out-of-range score | PASS | RejectedInvalid |
| LB-04 ties by earlier submission | PASS | Ordering verified across the top 100 |
| LB-05 top 100 + own rank | PASS | Rank 35 after link |
| LB-06 daily reset + archive | PASS | New board; previous closed, own rank kept |
| LB-06b previous-season archive | **FAIL** | Key hard-coded to `"s0_previous"` (`:401`) |
| LB-08 guest hidden until link | PASS | — |
| LB-09 offline view disabled | PASS | — |
| SC-13 campaign leaderboard | BLOCKED | No campaign board in BoardType/backend; not implemented |
| HINT-01/04/05/10/11(ranked)/12 | PASS ×6 | Per-level streak survives reload; win resets; ranked never shows it |
| HINT-11 events / Titan practice | **FAIL (code)** | Button only for `RunMode.Campaign` `UI/Screens.cs:361`; fails are only recorded for Campaign `Core/GameSession.cs:171-173` (BUG-08) |
| HINT-13/14 Give Up = fail, quit ≠ fail | PASS (code) | `GameSession.cs:138-142` End(false); ExitToMap doesn't call End `GameBootstrap.cs:118` |
| CMB-01/01b/02/05/06/07/08/09 | PASS ×8 | — |
| CMB-02b gap 0.8001 s | **FAIL** | Still chains (x2) because of the `+1e-4f` tolerance, `Meta/ComboTracker.cs:21` (BUG-10) |
| CMB-10 piece once per shot | PASS (code) with BUG-11 | `DestructionTracker.cs:88` HashSet |
| SC-01/02/03/04/04a/04c/05b/06/07 | PASS ×9 | 900 / 2,770 / 3,160 / 4,100 / cap 4,100 / 950 / 873 / 0 / best 2,770 |
| SC-04b floor 0.9999 | **FAIL** | 1,100, expected 1,099 |
| SC-05 floor 0.8739 | **FAIL** | 874, expected 873 |
| ST-01/02 store validator | PASS ×2 | 9 SKUs accepted; cosmetic=false rejected |

Count: 61 checks in the harness (53 pass / 8 fail). Code-review-only results are listed separately above (SE-07, HINT-11 events FAIL; HINT-13/14, CMB-10 PASS).

## New bug tickets
| ID | Sev | File:line | Repro | Expected | Actual |
|---|---|---|---|---|---|
| SEC-01 | **High** | Core/TitanRunJournal.cs:15,28-29 | Start ranked; journal written; edit `titan_ranked_journal.json` damageScore; relaunch (FlushPending) | Tampered result rejected | Client re-signs the edited journal → Accepted at 9,999. The stub signature is computed client-side (FNV, `LocalBackendService.cs:225-232`). Needs server-side validation or HMAC with a server key |
| BUG-01 | Med | .gitignore (`*.csproj`); BUILD.md:71-72 | Unzip / clone; run `dotnet run --project Tools/Tests` | Tests run | No project file, so the command fails. Commit `Tools/Tests/*.csproj` and `Tools/CompileCheck/*.csproj` (add a gitignore exception) |
| BUG-03 | Med (stub) / High for release | Backend/LocalBackendService.cs:66-73 | No test clock; set device clock +1 day | No new Titan attempt or daily claim (GA SG-20) | New attempt available. The stub derives server time from the device clock |
| BUG-04 | Med | Core/TitanRunJournal.cs:28 | Kill the Titan while offline (or app killed after the kill, before submit); relaunch online | Kill bonus kept (6000 + 3×500 = 7,500) | `killed=false` is forced → 6,000; no Titan clear XP or completion chest |
| BUG-05 | Med | Meta/ComboTracker.cs:38-39 (CampaignScore) | Win at 0.8739 / 0.9999 | floor: 873 / 999 (GA SG-31, integer maths) | `Math.Round` on float → 874 / 1,000 |
| BUG-06 | Med | LocalBackendService.cs:312,315 | Advance test clock past season end | New season, XP/tier reset, campaign intact (SE-08) | Same season id, XP kept, weekIndex 4 (out of range). No rollover |
| BUG-07 | Med | LocalBackendService.cs:401, 490-497 | View previous Season board; season ends with unclaimed premium tiers | Real previous-season archive; premium unclaimed tiers to inbox (GA SG-17/18) | Key `"s0_previous"` (an empty bot board); only free tiers are sent |
| BUG-08 | Med | UI/Screens.cs:361; Core/GameSession.cs:171-178 | Fail an event level or Titan practice twice | Hint button (GA SG-26) | Hint only in Campaign; fails not counted in other modes |
| BUG-09 | Low | Meta/StarRules.cs:16 | Level threshold 0.625 | Exact compare, no rounding (GA SG-01) | Rounded to 63% |
| BUG-10 | Low | Meta/ComboTracker.cs:21 | Breaks 0.8001 s apart | Combo ends (>0.8) | Chains (tolerance 1e-4). Use a frame-time-aware rule or document the tolerance |
| BUG-11 | Low | Destruction/DestructionTracker.cs:74-75,88 | Piece sags >½ height (counted as destroyed), then breaks | Break adds a combo point | Already in `_counted`, so no combo point |
| BUG-12 | Info | LocalBackendService.cs:14-15 | — | — | Comment claims device clock has no effect and the result is reinstall-proof. Only true with a fixed test clock / real server |

## Still blocked / open
- APK and device: safe area/cutout rendering, dp sizes on real DPIs, back gesture, FPS 60/30, HUD vs structure at 16:9–20:9, font scale, persistence across real app kill. Unity compile and Unity Test Runner: no licence/Editor.
- Real server: DT-07 reinstall, SG-20 tamper (only the stub exists), signed queue integrity (SEC-01).
- Campaign leaderboard (SC-13*): not implemented in this build.
- Physics-dependent cases (mastery detection in play, combo timing in real frames, settle): logic reviewed only.
- The "map gear → test clock" UI wasn't exercised (no build); the test clock was driven through the `SetTestClock` / `SimulateOffline` APIs.
