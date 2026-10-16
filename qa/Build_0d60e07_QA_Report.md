# CRASHVERSE: Build 0d60e07 QA re-test report (BUG-19)
Build: origin/main @ 0d60e07 (parent d109b07), QA copy at /workspace/qa_build_0d60e07. No game code was modified. Date: 16 Oct 2026 (IST).
PM decision: mastery goals also use basis points (bp), with no tolerance.

## Verdict: PASS (clean)
| Suite | Result |
|---|---|
| Developer tests (`dotnet run --project Tools/Tests`) | **514 assertions passed, 0 failed (62 cases)**. CompileCheck: 0 warnings, 0 errors |
| QA LogicTests (`dotnet test -p:BuildRoot=/workspace/qa_build_0d60e07`) | **188 tests: 187 passed, 0 failed, 1 skipped** (BUG-18, WON'T FIX). That is the previous 163 plus 25 new. TRX: /workspace/qa_automation/results_0d60e07.trx |
| QA harness (/workspace/qa_harness_0d60e07) | **61 passed, 0 failed**. Log: /workspace/qa_automation/harness_0d60e07.txt |
| Unity PlayMode/EditMode, visual compare | **Not run.** Needs the Unity Editor or an APK |

## BUG-19: FIXED
- `Meta/MasteryEvaluator.cs:26`: the DESTROYED_AT_LEAST goal now uses `WinRule.Meets(r.destroyedRatio, g.value)`.
- `Meta/MasteryEvaluator.cs:37`: the ROLE_INTACT goal now uses `WinRule.MeetsFraction(tot - br, tot, g.value <= 0 ? 0.8f : g.value)`.
- `Meta/WinRule.cs:20`: FractionBp, exact integer `part*10000/whole` with no floats (4/5 → 8000, 2/3 → 6666, guarded for ≤ 0).
- `Meta/WinRule.cs:23`: MeetsFraction.
- Both `+1e-6f` tolerances are removed.

New tests (MasteryBpTests.cs, 25 tests):
- **Content goals:** c1_l03 "≥ 95% destroyed" converts to 9500 bp; c1_l07 "Crown falls intact" converts to 8000 bp.
- **DESTROYED_AT_LEAST 95%:**
  - 0.9499 (9499) fails, 0.95 (9500) passes.
  - Float edge cases: 0.9499999f gives 9499 and fails; 0.94999999f (the same float as 0.95f) passes.
  - 0.9501 passes, 1.0 passes, NaN fails.
- **Generic bp−1 / exact sweep** at 0.50, 0.85, 0.90, 0.95 and 0.99: bp−1 fails and exact passes for each.
- **ROLE_INTACT 80%:** 4/5 passes, 3/4 fails, 80/100 passes, 79/100 fails, 7999/10000 fails, 8000/10000 passes, 3/3 passes, 0/0 fails.
- **Default threshold:** a value of 0 means 0.8.
- **Other checks:** a loss never earns mastery; source check that both goal types call WinRule.
- **Regression:** the d109b07 BUG-19 test now passes.

## Sweep for leftover float comparisons and tolerances
Search: `rg -i "0\.849|epsilon|tolerance|1e-N|Mathf.Approximately"`, plus a second search for ratio/pct/percent/threshold names next to comparison operators.

| Hit | Assessment |
|---|---|
| Win rule (`GameSession.cs:124`, `LocalBackendService.cs:569`) and mastery (`MasteryEvaluator.cs:26,37`) | WinRule bp. OK |
| `WinRule.cs:12-13` `ratio <= 0f`, `>= 1f` | Clamping inside the helper. OK |
| `LocalBackendService.cs:568` ratio range check [0, 1] / NaN | Input validation, not a threshold. OK |
| `ComboTracker.cs:38` clamp 0..1 for the score | Clamp; the score uses decimal floor. OK |
| `StarRules.cs:49-51` star thresholds | Exact decimal `shotsLeft >= frac × total`, no tolerance. It doesn't call WinRule, but it's equivalent for every threshold with ≤ 4 decimals (all content: 0.60/0.30 and per-level values). **Info only**; PM may choose to unify it |
| `LocalBackendService.cs:630` Daily Titan top 10%: `Math.Ceiling(count * topPercent)` | int×float is float maths. Verified exact for every multiple of 10 up to 1,000,000 (offline check), plus regression tests at 100, 250, 1000 and 241. OK |
| `Projectile.cs:122`, `DestructibleBlock.cs:222` `+1e-3f` | Geometry radius in metres, not a percentage. OK |
| `WinRule.cs:4,6`, `ComboTracker.cs:21` | Comments only |

**No leftover percentage tolerances.**

## Full regression pass (all PASS)
- **Ranked and Titan:** SEC-01, BUG-03, BUG-04, the 5000 per-shot cap.
- **Scoring, stars, combo and mastery:** BUG-05, BUG-09, BUG-10, BUG-11 (logic only), BUG-17, BUG-19.
- **Season and live-ops:** BUG-06, BUG-07, BUG-15.
- **Campaign and leaderboard:** BUG-13, BUG-14, BUG-16, SC-13.
- **Hint:** BUG-08.
- **Build and docs:** BUG-01, BUG-12.
- **Specs:** SG-01, SG-21 to SG-24, SG-34 to SG-36.
- **UI:** QA-UI-001 to 007 (code).

**New bugs: none.**

## SERVER-DEPENDENT (unchanged)
- Campaign stats and mastery come from the client and are range-checked only.
- The Titan 5000 cap is a plausibility bound.
- Placeholder time host; stubbed premium grant; the server data lives in the local file.
