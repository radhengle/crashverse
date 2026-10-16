# CRASHVERSE: Build d109b07 QA re-test report
Build: origin/main @ d109b07 (parent 4d2ffc7, a doc-only commit), QA copy at /workspace/qa_build_d109b07. No game code was modified. Date: 16 Oct 2026 (IST).
PM decisions applied:
- BUG-17: no tolerance; a win is exactly ≥ 8500 bp.
- BUG-18: closed WON'T FIX, because nothing has been released.

## Verdict: PASS (code level)
BUG-16 and BUG-17 are verified fixed, the doc commit is confirmed, and nothing regressed. There is one new finding: BUG-19 (Low), the mastery percentage goal still uses a float tolerance.

| Suite | Result |
|---|---|
| Developer tests (`dotnet run --project Tools/Tests`) | **507 assertions passed, 0 failed (61 cases)**. CompileCheck: 0 warnings, 0 errors |
| QA LogicTests (`dotnet test -p:BuildRoot=/workspace/qa_build_d109b07`) | **163 tests: 161 passed, 1 failed, 1 skipped.** The failure is the new finding BUG-19. The skipped test is BUG-18, ignored as WON'T FIX. TRX: /workspace/qa_automation/results_d109b07.trx |
| Fix and regression subset (KnownBug, BUG13 to BUG17, OnlineCap, Regression) | 42 of 43 passed. The one failure is BUG-19 |
| QA harness (/workspace/qa_harness_d109b07) | **61 passed, 0 failed**. Log: /workspace/qa_automation/harness_d109b07.txt |
| Unity PlayMode/EditMode, visual compare | **Not run.** Needs the Unity Editor or an APK |

Suite changes from 146 to 163 tests:
- The two NewBug tests from 40f5286 are now Regression tests.
- Added 3 BUG-16 timestamp tests, 7 WinRule boundary cases plus 5 BUG-17 checks, the BUG-19 check, and a skipped BUG-18 placeholder.
- Snapshot of the previous suite: /workspace/qa_automation/LogicTests_40f5286_snapshot.

## Ticket verdicts
| Ticket | Verdict | Evidence | Tests |
|---|---|---|---|
| BUG-16 timestamp refreshed on non-improving wins | **FIXED** | `Backend/LocalBackendService.cs:512`: the timestamp moves only when `score > me.score`. `:513`: a replace board can correct the score down but never touches the timestamp. The campaign total can't fall, because per-level bests only rise | Non_improving_win_keeps_timestamp (lower replay 4 days later: timestamp kept), Equal_total_keeps_timestamp, Higher_total_moves_timestamp (900 → 950 moves it), New_level_raises_total_moves_timestamp |
| BUG-17 84.99% accepted | **FIXED** | `Meta/WinRule.cs:10-17`: ToBp floors in integer basis points, with NaN and ≤ 0 → 0 and ≥ 1 → 10000; Meets is `bp >= thresholdBp`. Client uses it at `Core/GameSession.cs:124` (the old `+1e-5f` is removed). Server uses it at `LocalBackendService.cs:569` (the old `+1e-4f` is removed). The level threshold 0.85f converts to 8500 | Boundaries: 0.8499 → 8499 fail; 0.85 → 8500 pass; **0.8499999f → 8499 fail**; 0.84999996f (the float nearest 0.85) → 8500 pass; 0.8501 → 8501 pass; 1.0 → 10000; 0 → 0; NaN → 0; 1.2 → 10000. Server: 8499 rejected, 8500 accepted. Client and server use the same function |
| BUG-17 "no tolerance anywhere" | **PASS for the win rule** | `rg -i "0\.849|epsilon|tolerance|1e-N"` over Scripts. There are no hits in the win-rule code. The remaining hits are: the WinRule doc comment (`WinRule.cs:5`, it explains 0.84999996f); geometry radii `Weapons/Projectile.cs:122` and `Destruction/DestructibleBlock.cs:222` (+1e-3 in metres, not a ratio rule); the ComboTracker comment ":21 no tolerance"; and **MasteryEvaluator.cs:26,37** (see BUG-19) | No_win_tolerance_left (code only, comments excluded) |
| BUG-18 pre-40f5286 save migration | **CLOSED: WON'T FIX** (PM, 16 Oct 2026; nothing released). Developer documented that these saves are unsupported | Test skipped with an Ignore reason |
| 4d2ffc7 doc commit | **CONFIRMED** | `design/Spec_Gap_Answers_v1.md`, 3 lines added: SG-34 (largest single-shot combo, never summed, failed attempts excluded), SG-35 (device-local counter, resets on reinstall or new device), SG-36 (link to be listed, validated bests carry over, all-time). These match Feature_Test_Cases_v1.2 rev c and the SG34_36 tests (all pass) | — |

## Full regression pass (all PASS)
- **Ranked and Titan:** SEC-01, BUG-03, BUG-04, the online/offline 5000 per-shot cap.
- **Scoring, stars and combo:** BUG-05, BUG-09, BUG-10, BUG-11 (logic only), SG-34.
- **Season and live-ops:** BUG-06, BUG-07, BUG-15, SG-22, SG-23.
- **Campaign:** BUG-13, BUG-14, SC-13 (sum of bests, no reset, earlier timestamp wins ties, practice and events excluded).
- **Hint and accounts:** BUG-08, SG-35, SG-36.
- **Build and docs:** BUG-01, BUG-12.
- **Specs and UI:** SG-01, SG-21, SG-24, and QA-UI-001 to 007 (code; unchanged files).

## SERVER-DEPENDENT (unchanged; logged, not failed)
- Campaign stats and mastery (+1000) come from the client; the server only checks their ranges.
- The 5000 per-shot Titan cap is a plausibility bound, not a simulation.
- Placeholder time host (`LocalBackendService.cs:54`).
- Stubbed premium grant.
- The server data lives in the local file.

## New finding
| ID | Sev | Where | Issue | Expected |
|---|---|---|---|---|
| BUG-19 | Low | `Meta/MasteryEvaluator.cs:26` (DESTROYED_AT_LEAST, `+1e-6f`), `:37` (ROLE_INTACT fraction, `+1e-6f`) | Mastery percentage goals still compare floats with an epsilon. This is outside the win rule and the effect is tiny (1e-6 of a ratio), but it is a tolerance under a strict reading of the PM decision | Either use WinRule.ToBp-style integer bp comparisons, or have PM/GA confirm that the decision covers only the win rule. This does not affect the win rule, the score or ranked play |

## M1 code-level sign-off: RECOMMENDED
Criterion: zero open Blockers or Highs. **Met.** Open code-level items:
- **BUG-19 (Low):** open.
- **BUG-18:** WON'T FIX.
- Every other ticket is fixed and verified.

Open dependencies (not code QA, still required before release sign-off):
1. **APK on device:** QA-UI-001/002 (safe area, dpi), QA-UI-003/004 interaction, HINT-16a (new device), performance.
2. **Unity Editor:** PlayMode/EditMode suites, physics checks (BUG-11 in-scene, SG-21 settle, CMB physics cases), and the visual parity compare against the SMASH CITY reference board and Art v2.
3. **Real backend:** authoritative time host, server-side validation of campaign stats and mastery, Titan simulation instead of the 5000 cap, premium purchase flow, a real leaderboard instead of bots, and the HMAC key held only on the server.
4. **PM:** reference-board conflicts C-01 to C-14 and the VM-04/10/11 proposal from Reference_Screen_Parity_v1 are still waiting for decisions.
