# CRASHVERSE: Build 40f5286 QA re-test report
Build: origin/main @ 40f5286, QA copy at /workspace/qa_build_40f5286. No game code was modified. Date: 16 Oct 2026 (IST).
Scope: verify BUG-13, BUG-14, BUG-15 and the online per-shot cap; regression pass over all earlier fixes (SEC-01, BUG-01, BUG-03 to BUG-12, SC-13, SG-01, SG-21 to SG-24, SG-34 to SG-36, QA-UI-001 to 007).

## Overall: CONDITIONAL PASS
All four claimed fixes are verified and nothing regressed. There are two new findings:
- **BUG-16 (Med):** a later win that doesn't raise the campaign total still moves the player's timestamp, which breaks SC-13's earlier-wins tie rule.
- **BUG-17 (Low):** the server accepts 84.99% as a win.

There is also one migration risk (**BUG-18, Low**).

| Suite | Result |
|---|---|
| Developer tests (`dotnet run --project Tools/Tests`) | **497 assertions passed, 0 failed (59 cases)**; 0 compiler warnings (the CS0649 warning is gone) |
| CompileCheck | 0 errors |
| QA LogicTests (`dotnet test -p:BuildRoot=/workspace/qa_build_40f5286`) | **146 tests: 144 passed, 2 failed.** The 2 failures are the new bugs BUG-16 and BUG-17. The fix categories (KnownBug, BUG13/14/15, OnlineCap) are **25/25**. TRX: /workspace/qa_automation/results_40f5286.trx |
| QA harness (/workspace/qa_harness_40f5286) | **61 passed, 0 failed**. Log: /workspace/qa_automation/harness_40f5286.txt |
| Unity PlayMode/EditMode, visual compare | **Not run.** Needs the Unity Editor or an APK |

How the suite changed from 130 to 146 tests:
- The SC-13 tests were rewritten for the new `SubmitCampaignResult(CampaignRunReport)` API. `SubmitCampaignScore` was removed, so the old tests no longer compile. The old test `Unvalidated_total_rejected` was replaced by BUG-13 tests, which pass.
- New tests were added for BUG-14, BUG-15 and the online cap.
- Snapshot of the previous suite: /workspace/qa_automation/LogicTests_6e1ecac_snapshot.

## Ticket verdicts
| Ticket | Verdict | Evidence (file:line) | Tests |
|---|---|---|---|
| BUG-13 client-sent campaign total | **FIXED** | The client-total API is removed. `Backend/LocalBackendService.cs:538-556` SubmitCampaignResult takes raw run stats. `:558-571` ValidateCampaign checks: level exists, win only, prerequisite cleared on the server, 0 ≤ shotsLeft ≤ shots−1, ratio within [0, 1] and not NaN and ≥ threshold, 0 ≤ combo ≤ pieces. The server recomputes the score with CampaignScore and keeps the best per level (`:545-548`). It sums the total itself (`:575`). First-clear rewards are granted on the server (`:549-550`) and the client-side GrantCampaignReward was removed | No_wins_no_total_api, Unvalidated_reports_rejected (prerequisite, fail, unknown level, ratio, shots, combo; total stays 0), Boundaries_accepted, Sum_of_best_per_level_server_side (1400 + 1950 = 3350), Keeps_best_never_lowers, Idempotent_report_id, First_clear_reward_once_server_side |
| BUG-14 offline campaign win lost | **FIXED** | New file `Core/CampaignResultQueue.cs` (lines 1-48): wins are queued on disk, then flushed oldest first, stopping at the first offline error; reportId makes retries idempotent. Enqueue and flush on a win: `Core/GameSession.cs:193-196`. Flush at launch: `Core/GameBootstrap.cs:34`. Flush on resume: `:42` | Offline_wins_queued_then_synced_in_order (2 queued offline, then total 1800 once online), Retry_after_partial_flush_is_idempotent, Edited_queue_still_validated, Wired_at_launch_resume_and_win |
| BUG-15 seasonEndProcessed never set | **FIXED** | `LocalBackendService.cs:649`, set during season rollover. Compiler warning CS0649 is gone | SeasonEndProcessed_set_on_rollover (false before, true after) |
| Online per-shot cap | **FIXED** | `LocalBackendService.cs:276`: online checkpoints now use `offlineShotDamageCap`, the same as offline (`:310`). Value = **5000** (`Data/ContentModels.cs:77`, `titan_templates.json:9`). Absolute max stays 10,000 | Cap_5000_accepted, Cap_5001_rejected, Gain_cap_is_per_shot_not_absolute (4000 → 9000 accepted, → 14001 rejected), Max_10000_in_two_shots_then_kill_score, Rejected_shot_does_not_advance_state |

## Regression pass (all PASS)
| Item | Result |
|---|---|
| SEC-01, BUG-03, BUG-04, BUG-05 to BUG-10, BUG-12 | PASS. All 12 KnownBug tests pass, and harness checks DT-08, SEC-01, DT-13b and DT-03 pass |
| BUG-01 | PASS: the documented command runs |
| BUG-11 | PASS at logic level (BreakLedger). Physics behaviour needs Unity, not run |
| SC-13 | PASS except for BUG-16. Covered: no reset across day, week, season and +400 days; board ordering; practice and event runs never enqueued; only wins enqueued |
| SG-01, SG-21 to SG-24, SG-34 to SG-36 | PASS (star, mastery, grace, best-run, combo-max, hint-local and guest-link tests) |
| QA-UI-001 to 007 | PASS (code); unchanged files. QA-UI-001 and 002 still need a device check |

## SERVER-DEPENDENT (logged, not failed)
1. **Stats and mastery trust.** The server checks that each value in the CampaignRunReport (ratio, shotsLeft, bestCombo, mastery) is within range, but the values themselves come from the client. Mastery (+1000) is taken on trust, with no evidence. Developer's note at `LocalBackendService.cs:533-536` says the same. Fixing this needs server-side replay or simulation.
2. The 5000 per-shot Titan cap is a plausibility bound, not a simulation. It now applies online and offline.
3. Still stubbed from 6e1ecac: the placeholder time host (`:54`), `GrantPremiumPassStub`, and the server data living in the local file.

## New findings
| ID | Sev | Where | Issue | Expected |
|---|---|---|---|---|
| BUG-16 | Med | `LocalBackendService.cs:553` passes `replace: true`; `:512` | Every accepted campaign win rewrites the player's leaderboard timestamp, even when the total doesn't change. In the test, a lower replay 4 days later moved the timestamp, so the player loses tie priority against someone who reached the same total later | SC-13 / GA SG-32: ties go to whoever reached the total first. Only update the timestamp when the total goes up |
| BUG-17 | Low | `LocalBackendService.cs:568` (`+ 1e-4f` tolerance) | The server accepts destroyedRatio 0.8499 (84.99%) as a win; 0.8498 is rejected | The win rule is ≥ 85% (DP intro). Either make the comparison exact, or have PM/GA approve the tolerance as float slack |
| BUG-18 | Low (migration) | `:565` prerequisite check vs. older builds | Builds before 40f5286 recorded `grantedFirstClears` only for levels with a reward, and only c1_l10 has one. A player upgrading from an older build has no server record of L1 to L9, so wins on L2+ are rejected and dropped from the queue until they replay from L1 | Migrate existing clears on upgrade, or accept the client's history once. Only matters if any existing installs or saves have to carry over |

## Still blocked
- Unity PlayMode/EditMode, physics (BUG-11 in-scene, SG-21 settle) and the visual parity compare: **requires the Unity Editor or an APK, not run**.
- Device checks for QA-UI-001/002 and HINT-16a (new device).
- A real backend for: authoritative time, server-side validation of stats and mastery (replay), the Titan simulation instead of the cap, and premium purchases.
