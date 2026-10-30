# Backend PR #1 review (radhengle/crashverse#1)
Reviewed: PR head **f3fc100** (newer than the e243b15 named in the request; includes e243b15 + fd68cf3). Read-only checkout /workspace/qa_pr1.
Tests: `dotnet test server/` → **140 passed, 6 skipped (Firestore emulator not available on QA box), 0 failed, 146 total** (PM quoted 94/94 at e243b15).

## Rule checks
| Rule | Evidence | Verdict |
|---|---|---|
| Win ≥ 8500 bp, shared helper | CampaignService.cs:70,177 use `Crashverse.Meta.WinRule.Meets` | PASS |
| Per-shot cap 5000 (online+offline) | TitanService.CheckStep:146 applies `offlineShotDamageCap`=5000 to every checkpoint; titan_templates.json:9 | PASS (plausibility only; proof needs replay, SEC-TC-62) |
| Campaign board = sum of validated bests | CampaignService.Total:239 sums **all** `campaignBest.best`, incl. `validated=false` | **QUESTION BE-01** |
| Ties → earlier timestamp | Leaderboards.cs:31 `OrderByDescending(score).ThenBy(reachedUnixMs)` | PASS (BE-04: confirm reachedUnixMs isn't refreshed when total unchanged on replace) |
| Titan grace 00:15:00.000 UTC inclusive | Core.RankedDeadline + TitanService:192 `Now > deadline`; test GraceWindow_InclusiveAt001500000 | PASS |
| Age bands under13/13to15/16to17/adult | ComplianceService:66,75; test Bands_Under13_13to15_16to17_Adult; under13 refused | PASS |
| Public ranked board off until replay | RankedPublicBoardEnabled default off; ignored without replay worker (Program.cs:380); 3 tests | PASS |
| No gameplay for sale / gem packs off | ENABLE_GEM_PACKS default false; GemPacks_OffByDefault test | PASS |

## SEC-TC-60..64
| Case | Result |
|---|---|
| SEC-TC-60 (C1 server-side keys/wallet) | PASS in code (wallet/receipts server-side, receipts carry kid + rotate); KMS in prod BLOCKED (no infra) |
| SEC-TC-61 (C2 Play receipts) | PASS in unit tests (token reuse, foreign/pending, refunds, RTDN auth+dedupe); real Play Developer API BLOCKED |
| SEC-TC-62 (H2 replay/bot) | BLOCKED: StubReplayWorker only; safe-fail confirmed (board stays closed, runs stay pending) |
| SEC-TC-63 (H3 signed overrides) | PASS: Titan override admin-only + audited; shotBudget from server content |
| SEC-TC-64 (H4 linking) | PASS partial: forged/missing token rejected, fresh re-auth required for linked-account deletion; real Google/Facebook verification BLOCKED |

## ST-20..33 (refund/debt)
Covered by Refund_DefaultGemDebt_LocksGemSpending_KeepsGemBoughtItems, Refund_CashCosmeticAndPremiumTierCosmetics_AreRevoked, Economy_SeasonUnlock450_InsufficientAndDebt, Rtdn_…DropsDuplicates (ST-33), TwoRefundsIn90Days. **PASS at unit level** for ST-20,21,22,26,27,30,32,33; ST-23/24/25 (repay order/boundary), ST-29 (coins unaffected), ST-31 (offline sync) not explicitly tested (BE-03). Note: per Economy v1 these flows are dormant until real-money purchases are enabled.

## Tickets
- **BE-01 Med**: campaign total includes unvalidated bests, and a new unvalidated best overwrites the previous validated one. Spec: sum of *validated* bests. Either sum last-validated best, or confirm pending entries are hidden from others and the old best is restored on replay failure (add test).
- **BE-02 Med (cross-PR)**: PR's chapter1.json still has L10 = 7 shots, bands 0.6/0.3, old mastery. Level_Rulings_v1 (8 shots, 0.375/0.25, NO_CLOSED_SHUTTER_HIT, L1 DESTROYED_AT_LEAST 0.95) is on developer-levels 60ba5d1; server shot-count validation will reject 8-shot L10 runs until merged.
- **BE-03 Low**: add tests for ST-23/24/25/29/31.
- **BE-04 Low**: test that tie timestamp is stable on `replace:true` resubmits with equal total.
- **BE-05 Info**: Firestore emulator tests (6) not run by QA; rely on CI.

## Verdict
**Approve with comments**: no blockers for a dev/test backend; BE-01 must be answered before any public campaign board.

---
## Re-check: PR head 9b5574b (16 Oct 2026, 11:45 IST)
Tests: `dotnet test server/` → **168 passed, 7 skipped (Firestore emulator), 0 failed, 175 total**. Backend's "175/175" counts the skips as passes; CI with the emulator wasn't re-run by QA.

| Ticket | Inspection | Verdict |
|---|---|---|
| BE-01 | `Total` = `Where(b => b.validated).Sum(best)` (CampaignService:241). An unvalidated result writes only `provisionalBest/provisionalRunId` (:136) and never touches `best`. Replay promotes only if `ok && finalScore > best` (:209); a failed replay clears the provisional value and keeps the validated best. Tests BE01_TotalsAndBests_CountOnlyValidatedRuns, BE01_UnvalidatedBest_NeverOverwritesValidated_AndFailedReplayKeepsIt | **FIXED** |
| BE-02 | PR chapter1.json is byte-identical to 60ba5d1. Data check vs LevelRulingsTests: LR01 (L10 = 8 shots), LR02 (0.375/0.25), LR04 (NO_CLOSED_SHUTTER_HIT), LR05 (L1 = 4 shots, 0.95) and LR06 (L7 = 5 shots, 0.6/0.3) all PASS. Test BE02_LevelData_MatchesLevelRulings60ba5d1 | **FIXED** |
| BE-03 | New tests BE03_DebtRepayment (ST-23/24/25), BE03_ST29_…, BE03_ST31_… | **FIXED** |
| BE-04 | Submit moves the timestamp only when the score rises (Leaderboards:21-23). Test BE04_TieTimestamp_StableOnEqualReplaceResubmit | **FIXED** |

Contract regression checks are all unchanged and pass:
- win via WinRule.Meets
- per-shot cap 5000 (TitanService:146, titan_templates.json)
- ties go to the earlier reachedUnixMs
- grace inclusive (`Now > deadline`, 15 min)
- the 4 age bands
- the ranked board stays closed without a replay worker
- EnableGemPacks defaults to false

Notes:
- **BE-06 Info:** when a `replace` total goes down, the earlier timestamp is kept. Tie order after a drop is still by the original time, which is acceptable. GD/PM can confirm.
- **QA-H1 (QA debt):** the NUnit harness doesn't compile against current trees (`LocalBackendService` was removed), so I ran the LevelRulings check as a data check instead. QA will update the harness.

**Verdict: APPROVE.** No open blocking tickets. Live SEC-TC-61/62/64 checks are still blocked until the real backend exists. Not posted to GitHub, as instructed.

## Update 2026-10-16 22:50 IST
- Emulator tests: CI now runs all 7 Firestore emulator tests and fails on skips. Run 38914508876 passed (per Backend).
- BE-06 is now a defect per GD ruling (Spec_Gap_Answers_v1, PR #27): when a total drops, the tie-break timestamp must become the server time of the recalculation. The current code keeps the original timestamp, so this FAILS. New test cases: LB-30, where a refund or invalidation lowers the total and the timestamp becomes the recalc time; LB-31, where a player tied after a drop ranks below one who reached that total earlier.
- PR #1 approval is reset (head moved to dc02934) until BE-06 is fixed and the Unity job is green.
