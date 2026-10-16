# CRASHVERSE: Build 6e1ecac QA re-test report
Build: origin/main @ 6e1ecac (github.com/radhengle/crashverse), QA copy at /workspace/qa_build_6e1ecac. No game code was modified.
Date: 16 Oct 2026 (IST). Scope: verify the fixes for SEC-01, BUG-01, BUG-03 to BUG-12 and SC-13; write SC-13 tests; regression-check SG-01, SG-21 to SG-24 and QA-UI-001 to 007.

## Overall: CONDITIONAL PASS
All 12 fix tickets from the 0ccfa9b report are verified fixed. SC-13 is **partially met**: one new Medium bug (BUG-13: the server trusts the campaign total the client sends). No regressions.

| Suite | Command | Result |
|---|---|---|
| Developer tests | `dotnet run --project Tools/Tests` (BUILD.md) | **479 assertions passed, 0 failed (55 cases)**. 1 warning: CS0649 (see BUG-15) |
| CompileCheck | `dotnet build Tools/CompileCheck` | 0 errors |
| QA LogicTests (NUnit) | `CV_BUILD_ROOT=/workspace/qa_build_6e1ecac dotnet test -p:BuildRoot=/workspace/qa_build_6e1ecac` in /workspace/qa_automation/LogicTests | **125 tests: 124 passed, 1 failed** (the failure is new bug BUG-13). KnownBug category: **12/12 pass** (was 0/10 on 0ccfa9b). TRX file: /workspace/qa_automation/results_6e1ecac.trx |
| QA harness | `dotnet run` in /workspace/qa_harness_6e1ecac | **61 passed, 0 failed** (was 53/61). Log: /workspace/qa_automation/harness_6e1ecac.txt |
| Unity PlayMode/EditMode, visual compare | — | **Not run.** Needs the Unity Editor or an APK |

Changes to the QA automation:
- 6e1ecac removed `RankedResult` and `LocalBackendService.Sign`, so LogicTests/Support.cs now drive ranked runs through `BeginRankedRun` → `ReportRankedCheckpoint` → `FinalizeRanked`. The default BuildRoot is now 6e1ecac.
- A snapshot of the 0ccfa9b-era suite is kept at /workspace/qa_automation/LogicTests_0ccfa9b_snapshot.
- The harness was copied to /workspace/qa_harness_6e1ecac and ported the same way. Its DT-08, SEC-01 and LB-02 checks were rewritten for the new trust model.
- New tests: SC13Tests.cs (8 SC-13 tests plus a BUG-06 premium-inbox test), offline-cap tests, a forged-receipt test, a receipt-replay test and the BUG-03 network-clock tests.

## Ticket verdicts
| Ticket | Verdict | Evidence (file:line) | Tests |
|---|---|---|---|
| SEC-01 ranked journal re-signed by client | **FIXED** | The client has no signing API. Per-run 32-byte HMAC key at `Backend/LocalBackendService.cs:232`ff, HMAC-SHA256 receipt around `:250-256` with constant-time compare at `:259`. Server-held run state advances only through validated checkpoints (`:279-294`). `FinalizeRanked` ignores edited or forged receipts and falls back to the server state (`:301-331`). `Core/TitanRunJournal.cs:29-40` does no client-side signing | SEC_01_edited_journal_rejected (edited 1200→9999, scores 1200 and flags "failed verification"), Forged_receipt_ignored, Receipt_not_reusable_across_runs, Client_has_no_signing_api; harness SEC-01 |
| BUG-01 csproj missing | **FIXED** | `.gitignore:15` has the exception `!Tools/**/*.csproj`; Tools/Tests/Tests.csproj and Tools/CompileCheck/CompileCheck.csproj are committed | The documented command runs: 479/0 |
| BUG-03 device clock drives resets | **FIXED (stub; SERVER-DEPENDENT host)** | `Data/Clock.cs:17-54` NetworkServerClock takes an HTTP Date anchor plus a monotonic Stopwatch. `LocalBackendService.cs:64` makes it the default. `:106-111` Online() throws "server time unavailable" while unsynced. Outside tests, `DateTime.UtcNow` appears only in SystemClock (`Clock.cs:7`) and the display offset (`Core/AppServices.cs:38`) | BUG_03_device_clock_tamper_no_new_attempt (unsynced blocks; no second attempt; a monotonic +24 h does allow one), BUG_03_unsynced_blocks_time_gated; harness DT-08 |
| BUG-04 queued kill loses bonus | **FIXED** | `LocalBackendService.cs:318-329` keeps the kill flag from the offline tail. `TitanRunJournal.cs:29-40` flushes the journal unchanged | BUG_04_queued_kill_keeps_bonus (online to 4000, offline kill shot 6000, 3 left → 7500); harness DT-13b |
| BUG-05 score floor | **FIXED** | `Meta/ComboTracker.cs:33-40` CampaignScore.Compute uses `decimal.Floor((decimal)r*1000m)` | BUG_05 ×2 (0.8739→873, 0.9999→1099); SC-04b, SC-05 |
| BUG-06 season never rolls over | **FIXED** | `LocalBackendService.cs:613-633`: on rollover, unclaimed reached free tiers (and premium tiers if owned) go to the inbox for 30 days; XP, bonus chests, premium, claims and event bests reset; campaign is untouched | BUG_06_season_rollover_resets, BUG_06_unclaimed_free_and_premium_to_inbox |
| BUG-07 fake "previous season" archive | **FIXED** | `LocalBackendService.cs:495-497` real PreviousKey; `:573-575` the archive before season 1 is empty and closed (no bots) | BUG_07 (key ≠ s0_previous, closed, 0 entries) |
| BUG-08 hint only in campaign | **FIXED** | `Core/GameSession.cs:194` (Event) and `:205` (TitanPractice) call RecordHintAttempt. `UI/Screens.cs:361` shows the hint button whenever the mode is not ranked. `UI/Screens.cs:137` hides it in ranked | BUG_08; HINT-11 (never in ranked) |
| BUG-09 thresholds rounded to whole % | **FIXED** | `Meta/StarRules.cs:22` Frac = (decimal)float; `:49-51` exact `shotsLeft >= frac×total`. ComputeInt is kept for integer callers | BUG_09 ×2 (0.625 is exact) |
| BUG-10 combo tolerance | **FIXED** | `Meta/ComboTracker.cs:21` strict `<= 0.8` with no epsilon | BUG_10 (gap 0.8001 breaks); CMB-02b |
| BUG-11 sagged piece loses combo point | **FIXED** | `Meta/BreakLedger.cs:15-19`: one combo point per piece when it breaks or falls out of bounds, even if it was counted earlier by sagging. Wired in at `Destruction/DestructionTracker.cs:88-91` | Logic level only (Developer BreakLedger tests pass). Physics behaviour needs Unity: **not run** |
| BUG-12 misleading clock comment | **FIXED** | `LocalBackendService.cs:12-16` now describes NetworkServerClock and the test overrides accurately | Doc review |
| SC-13 campaign leaderboard | **PARTIAL (see BUG-13)** | `LocalBackendService.cs:547-566` SubmitCampaignScore/CampaignScoreCeiling; BoardType.Campaign never closes (`:526-532` IsClosed); `GameSession.cs:191` submits only on a campaign win; `Meta/CampaignSave.cs:71` TotalScore is the sum of best per-level scores | See below |

### SC-13 tests (QA LogicTests, category SC13)
| Requirement | Test | Result |
|---|---|---|
| Sum of best per level (repeat runs and fails don't add) | Sum_of_best_per_level (2000/1500 on L1 and 1800 on L2 → 3800) | PASS |
| Best total kept, never lowered | Keeps_best_total_never_lowers | PASS |
| Ceiling and negative totals rejected | Above_ceiling_rejected | PASS |
| No reset at day, week, season or +400 days | No_reset_daily_weekly_season (same key, open, score kept) | PASS |
| Ties go to the earlier timestamp | Tie_goes_to_earlier_submission (an equal bot score ranks above the player's later submission; whole board ordering checked) | PASS |
| Practice and events excluded | Practice_and_events_do_not_submit (single call site, inside `case RunMode.Campaign`, `GameSession.cs:191`) | PASS (source-level) |
| Wins only | Only_wins_submit | PASS (source-level) |
| **Validated** wins only | Unvalidated_total_rejected | **FAIL (BUG-13)**: the server accepts any total up to the ceiling with no validated wins |

## SERVER-DEPENDENT (logged, not failed)
1. **Offline shot plausibility is only a 5000-damage-per-shot cap.** The cap itself is verified as enforced:
   - Value is 5000: `Data/ContentModels.cs:77` and `Resources/Data/titan_templates.json:9`.
   - Enforced at `LocalBackendService.cs:321`.
   - Tests: Offline_cap_value_is_5000; Offline_tail_5000_per_shot_accepted (5000, then +5000 with a kill → 10000 + bonus); Offline_tail_5001_rejected (scores 0, flagged); Offline_tail_after_signed_capped (signed 1000, unsigned +6000 → 1000).
   - Note: online checkpoints use `maxGain = maxDamageScore` (`:288`), so online the server trusts the client's per-shot numbers up to 10,000. Only a real server-side simulation or replay can fix that.
2. **Placeholder NTP host**: `DefaultTimeUrl = https://www.google.com/generate_204` (`LocalBackendService.cs:54`) uses the HTTP Date header, which has 1-second resolution. It needs to be the real backend's time.
3. **Stubbed premium grant**: `GrantPremiumPassStub()` (`LocalBackendService.cs:476`) has no purchase flow.
4. Also stub-bound: the per-run HMAC key and the run state live in the local "server DB" file on the device. This is only tamper-proof once the backend is real.

## New bugs
| ID | Sev | Where | Issue | Expected |
|---|---|---|---|---|
| BUG-13 | Med | `Backend/LocalBackendService.cs:547-556`, `Core/GameSession.cs:191` | SubmitCampaignScore(int totalScore) takes a total computed by the client from the editable local campaign save. The only check is the content ceiling, so an edited save or a forged call can post up to the ceiling (QA posted CampaignScoreCeiling() with zero wins and it was accepted). | Per SC-13, the server should hold the best validated win per level and sum it itself; the client should not submit a total |
| BUG-14 | Low | `Core/GameSession.cs:191,212-215` | If a campaign win happens offline, SubmitCampaignScore throws and the score isn't queued. The board stays stale until the next online win | Queue and retry, as the ranked journal does, or resync on launch |
| BUG-15 | Info | `Backend/LocalBackendService.cs:48` | CS0649: `ServerDb.seasonEndProcessed` is never assigned (dead field) | Remove it, or use it |

## Regression check (all PASS, nothing regressed)
| Item | Verdict | Evidence |
|---|---|---|
| SG-01 / QA-UI-005 stars | PASS | `Meta/StarRules.cs:12-13` defaults 0.60/0.30; `:49-51` `>=` with exact fractions; per-level thresholds `Data/ContentModels.cs` star3Pct/star2Pct. Star tests pass |
| SG-21 islandKillY | PASS | `Meta/MasteryEvaluator.cs:38-39` strict `<` after settle |
| SG-22 grace window | PASS | `LocalBackendService.cs:338-339` inclusive to 00:15:00.000; tests .999 and .000 accepted, .001 refunded (LogicTests and harness DT-03) |
| SG-23 best-run cap | PASS | `LocalBackendService.cs:447-452` cap and best-delta XP; Best_run_cap_1_2_2_3 |
| SG-24 exact thresholds | PASS | 3/5 exact, and the BUG-09 caveat is now gone |
| QA-UI-001 safe area | PASS (code) | `UI/UIFramework.cs:35-44`. Device check OPEN |
| QA-UI-002 dp sizing | PASS (code) | `UI/UIFramework.cs:9,31`. Device check OPEN |
| QA-UI-003 HUD taps don't aim | PASS (code) | `Weapons/AimController.cs:87,104` |
| QA-UI-004 Android back | PASS (code) | `UI/UIFramework.cs:142` |
| QA-UI-006 saves not used for ranked | PASS | Ranked runs now use server receipts (SEC-01 fixed); no PlayerPrefs. The campaign save is still plain JSON, which leads to BUG-13 |
| QA-UI-007 live-ops present | PASS | LocalBackendService and Clock.cs |

## Still blocked
- Unity PlayMode/EditMode suites, physics checks (BUG-11 in-scene, SG-21 settle) and the visual parity compare: **requires the Unity Editor or an APK, not run**.
- Device checks for QA-UI-001/002 (notch, dpi).
- A real backend for: authoritative time, a server-side shot simulation instead of the cap, and the purchase flow for the premium grant.
- SG-34 to SG-36 remain open with GA.
