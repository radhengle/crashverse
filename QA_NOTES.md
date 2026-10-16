# CRASHVERSE — QA notes (M1 vertical slice)

**Level id:** `m1_clockwork_tower`  
**Contract:** `design/M1_Level_Design_Contract_v0.1.md`  
**Test cases:** `qa/M1_Test_Cases_v0.2.md` (AC-01…AC-12)

## What to test first

1. Cold launch → HUD shows **Shots 5/5**, Clockwork Tower, DESTROY_STRUCTURE.
2. Drag to aim, release without FIRE → moves still **5** (aim cancel free).
3. FIRE once → moves **4**; projectile flies from booster (+0.2 forward).
4. Wood wing breaks on solid hit (HP 30); brick tougher (60); steel face spongy (120); **steel ring-joints** take **2×** damage.
5. Breaking a steel ring drops the segment above (support graph).
6. Reach ≥**85%** objective mass, wait settle (~0.75s quiet) → **TOWER DOWN** + stars.
7. Exhaust 5 shots under 85% → fail + **RETRY** (full 5, clean structure, no leftover debris).
8. Stars on win only: 0–1 left → 1★; 2–3 → 2★; 4–5 → 3★. Best stars persist across retries.
9. Quality Mid/Low toggle: 60 vs 30 target frame rate; debris capped / fades.
10. Give Up → fail path.

## Known stubs / limitations

| Item | Status |
|---|---|
| uGUI art-directed HUD | IMGUI placeholder (shots / % / stars / fire / retry) |
| ScriptableObject assets on disk | Runtime defaults used; Editor menu can bake `.asset` files |
| URP asset / renderer in scene | Package referenced; full URP pipeline asset may need Hub create on first open |
| Trajectory prediction line | Gizmo in editor only |
| Explosive barrel | Decorative NonObjective (no chain reaction yet) |
| Audio / particles / camera shake | Not in M1 |
| Wrecker modules gameplay | Visual Core/Module/Booster only; default projectile damage |
| Soft-lock auto-fail | Partial — rely on 0 moves fail for M1 |
| Real device perf AC-11/12 | Needs APK + hardware; CI blocked on Unity licence secrets |
| GitHub repo / push | **Not done** — needs user approval |

## Build blockers for QA APK

1. **Unity licence** for GameCI (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).
2. **GitHub repository** + secrets + workflow run (Developer will not push without go-ahead).
3. No Unity Editor on the agent box — cannot produce a local APK here.

## Bug reports

Use the template in `qa/M1_Test_Cases_v0.2.md`. Route all bugs through Project Manager.

## M2: QA tickets addressed

| ID | Fix |
|---|---|
| QA-UI-001/002 | `DpCanvasScaler` (scale = dpi/160, min 320dp width) + `SafeArea`; tap targets ≥48dp and text ≥12sp enforced by UITheme/UIFactory (unit-tested) |
| QA-UI-003 | `AimController` ignores pointers over UI (EventSystem) |
| QA-UI-004 | Android back: closes modal → pause in a level → back a screen → confirm-quit at map root |
| QA-UI-005 / SG-01 / SG-24 | Stars from % shots left with per-level thresholds, integer maths; 4/5/6/7-shot tables tested |
| QA-UI-006 | `campaign_save.json` = campaign progress only; wallet/rewards/ranked/season via `IBackendService` |
| QA-UI-007 | Live-ops screens (Titan, Daily, Season/Event, Leaderboards) |
| SG-20/21/22/23 | Server UTC only; islandKillY; inclusive 15-min grace; event stars = best single run |

## M2: how to test live-ops
Map ▸ ⚙ opens QA Tools: advance the server test clock +1h/+1d/+7d, jump to 23:59:50 UTC, clear the clock, toggle the backend offline.
Times are shown in device local time with a zone label (e.g. IST) and reset at 00:00 UTC (05:30 IST).

## M2: stubbed
- IAP (the store shows validated SKUs; buying shows a toast). Account linking is a local flag. Friends on boards are fake bots.
- Real server: implement `IBackendService` and set `BackendLocator.Current`. Request signing is an FNV checksum, not HMAC.
- Audio/VFX polish, post effects, final fonts and icons (emoji placeholders), map art beyond greybox islands.

## Build 0ccfa9b QA report: fixes
| ID | Fix | Test(s) |
|---|---|---|
| SEC-01 | Ranked run reported to the server after every settled shot; server keeps run state and a per-run HMAC-SHA256 key and returns receipts. Journal sent verbatim on relaunch (`FinalizeRanked`); client never signs. Edited/forged entries are ignored and flagged; the offline tail is accepted only step by step within `offlineShotDamageCap` (5000/shot) | SEC01_* (3) |
| BUG-03 / BUG-12 | `NetworkServerClock` (HTTP Date + monotonic Stopwatch); no SystemClock fallback, unsynced = live-ops refused. App countdowns are monotonic too | BUG03_server_time_ignores_device_clock |
| BUG-04 | Kill flag kept through queue/relaunch → kill bonus, Titan clear XP and chest | BUG04_offline_kill_keeps_bonus_xp_chest |
| BUG-05 | Score floors with exact decimal maths | BUG05_score_floor |
| BUG-06 | Season instances roll over every 28 days (`{id}_r{N}`): XP/tier/claims/premium/event bests reset, campaign untouched | BUG06_season_rollover |
| BUG-07 | Previous-period keys are the real archives (yesterday, last week across seasons, last season); no fake pre-season board; unclaimed premium tiers (if owned) to inbox | BUG06_*, BUG07_previous_season_board_is_real |
| BUG-08 | Hint in events and Titan practice (hint data for event levels + Titan arenas; streak via `RecordHintAttempt`), never ranked | BUG08_hint_events_and_practice_not_ranked |
| BUG-09 | Exact fractional thresholds (0.625 stays 0.625) | BUG09_exact_fraction_thresholds |
| BUG-10 | Combo window strictly ≤ 0.8 s (no tolerance; measured in Time.time float seconds) | BUG10_combo_window_strict |
| BUG-11 | `BreakLedger`: destroyed% and combo point tracked separately | BUG11_sagged_piece_scores_on_break |
| BUG-01 | `.gitignore` keeps `Tools/**/*.csproj`; both projects committed | BUG01_test_projects_not_ignored |
| SC-13 | Campaign all-time board (sum of best level scores; best kept; content-derived ceiling); "Campaign" tab | SC13_campaign_leaderboard |

Still needs the real server: true tamper resistance (the stub DB is on device), reinstall-proof state, replay-based validation of offline tails.
The stub's time source is an HTTP HEAD to `LocalBackendService.DefaultTimeUrl` (needs network; otherwise live-ops stay disabled).

## Build 6e1ecac re-test: fixes
| ID | Fix | Test(s) |
|---|---|---|
| BUG-13 | `SubmitCampaignScore(total)` removed. `SubmitCampaignResult(report)` sends raw win stats; the server checks the level, server-side prerequisite clear, shots/ratio/combo bounds; recomputes the score; keeps the best per level; sums the total itself; grants first-clear content rewards (client-sent rewards API `GrantCampaignReward` removed) | BUG13_*, SC13_campaign_leaderboard, first_clear_rewards_once |
| BUG-14 | `CampaignResultQueue`: wins written to disk first, synced in order after each win, at launch and on resume; idempotent `reportId` | BUG14_offline_campaign_win_queued_and_synced |
| BUG-15 | `seasonEndProcessed` set on rollover (no CS0649) | BUG15_season_end_processed_set |
| Online cap | `ReportRankedCheckpoint` uses the same `offlineShotDamageCap` (5000/shot) as the offline tail | online_checkpoint_uses_plausibility_cap |

Residual (needs the real server): reported campaign stats and mastery flags are bounds-checked, not simulated.

## Build 40f5286 re-test: fixes
| ID | Fix | Test |
|---|---|---|
| BUG-16 | Leaderboard timestamp changes only when the player's score/total rises (ties = first to reach) | BUG16_timestamp_only_on_increase |
| BUG-17 | Win = destroyed basis points >= threshold bp (8500), integer compare, no tolerance; same `WinRule` on client and server | BUG17_win_exactly_85pct_bp |
| BUG-18 | Not migrated by decision. Saves from builds before 40f5286 (`local_server_db.json`, `campaign_save.json`, queue/journal files) are **unsupported**: there is no migration. Clear app data (or reinstall) when upgrading test devices. | (doc) |

## Build d109b07: BUG-19
| BUG-19 | Mastery % goals (DESTROYED_AT_LEAST, ROLE_INTACT) use WinRule basis points (integer, no tolerance), the same rule as the win check | BUG19_mastery_bp_no_tolerance |
