# CRASHVERSE — Feature Test Cases v1.2 (QA)
Change log: v1.2 rev d (2026-10-16): SC-12 corrected (QA arithmetic error; L2 max with 2 left is 3,450) and re-checked against Art v2: FAIL.
Change log: v1.2 rev c (2026-10-16): SG-34/35/36 closed per `design/Spec_Gap_Answers_v1.md` (cited **GA SG-xx**); added CMB-11/11a, HINT-16/16a, SC-13f/g.
Change log: v1.2 (2026-10-16) adds sections I–L from `design/Design_Addendum_v1.1_Hint_Combo_Score_Store.md` (cited **ADD §x**); PM decisions: store = currency packs + cosmetics only; Free Hint, Combo and Score stay; stars stay on % of shots left. New gaps SG-25…SG-33. 🎨 = re-review against Art painted v2 screens.
Change log: v1.2 rev b (2026-10-16): SG-25…SG-33 closed per ADD "Spec gap answers" (cited **ADD-GA SG-xx**); hint/combo/score/leaderboard/coin boundary cases added; worked examples re-checked (floor, cap 1.0, integer maths); SC-12 answered (formula right, art wrong), kept open vs Art v2.
Change log: v1.1 (2026-10-16) adopts `design/Spec_Gap_Answers_v1.md` (cited **GA SG-xx**) and M1 contract v0.2 §5 (stars = shotsLeft/shotBudget vs per-level star3Pct/star2Pct, defaults 0.60/0.30, compared with ≥, no rounding). Star cases rewritten with exact boundaries. Live-ops dependency note added.
Change log: v1.1 rev b (2026-10-16): SG-21/22/23 closed per GA; L2 mastery cases rewritten (CH1-L2-03a–d); DT-03 grace boundaries at ms precision; SE-03b best-run cap. SG-24 open (Developer).
Source of truth: `design/Design_Pack_v1_Ch1_Powerups_LiveOps.md` (cited as **DP §x**), with M1 rules from `design/M1_Level_Design_Contract_v0.1.md`. Note: the pack header still says "Status: draft for PM approval". PM has stated it's approved; please update the header (SG-00).
Execution status: **all BLOCKED, no APK.** Live-ops cases also need a server/config backend and a test clock.
Global M1 rules (DP intro): win = ≥85% objective mass destroyed after settle. Stars on win only: ratio = shotsLeft/shotBudget; 3★ if ratio ≥ star3Pct (default 0.60), 2★ if ≥ star2Pct (default 0.30), else 1★; fail = 0★ (M1 contract v0.2 §5, GA SG-01). Mastery is a separate tick and never changes stars, except L9 (GA SG-03). Terminology: "shots" (GA SG-02).

## A. Star boundary oracle (M1 v0.2 §5, GA SG-01), default thresholds 0.60 / 0.30
| Budget (levels) | 3★ shots left | 2★ shots left | 1★ shots left | At/just-below 3★ | At/just-below 2★ |
|---|---|---|---|---|---|
| 4 (L1, L2) | 3–4 (0.75, 1.00) | 2 (0.50) | 0–1 (0.00, 0.25) | 3/4=0.75→3★ ; 2/4=0.50→2★ (no exact 0.60) | 2/4=0.50→2★ ; 1/4=0.25→1★ (no exact 0.30) |
| 5 (L3–L8) | 3–5 (0.60, 0.80, 1.00) | 2 (0.40) | 0–1 (0.00, 0.20) | **3/5=0.60→3★ (exact)** ; 2/5=0.40→2★ | 2/5=0.40→2★ ; 1/5=0.20→1★ (no exact 0.30) |
| 6 (L9) | 4–6 (0.667…) | 2–3 (0.333, 0.50) | 0–1 (0.167) | 4/6=0.667→3★ ; 3/6=0.50→2★ | 2/6=0.333→2★ ; 1/6=0.167→1★ |
| 7 (L10) | 5–7 (0.714…) | 3–4 (0.429, 0.571) | 0–2 (0.286) | 5/7=0.714→3★ ; 4/7=0.571→2★ | 3/7=0.429→2★ ; **2/7=0.286→1★** |
Max reachable shots left on a win is budget−1 unless a 0-shot win exists (it can't), so the "budget" column value is N/A in practice. Win with 0 left is valid (M1 v0.2 §4) → 1★.

| ID | Case | Expected |
|---|---|---|
| STAR-01 | Exact-threshold float check: level data override star3Pct=0.60, budget 5, 3 left | 3★. Ratio computed as 3/5 must not fail ≥0.60 because of float/double mismatch (e.g. `(float)3/5` vs `0.6` double) |
| STAR-02 | Override exact 0.30: test level budget 10, star2Pct 0.30, 3 left (3/10) | 2★; 2/10 → 1★ |
| STAR-03 | Per-level override: star3Pct=0.80, star2Pct=0.40, budget 5 | 4 left→3★, 3 left→2★, 2 left (0.40)→2★, 1 left→1★ |
| STAR-04 | Missing thresholds in level data | Defaults 0.60/0.30 applied |
| STAR-05 | Fail at any shots left | 0★, best not overwritten |
| STAR-06 | Code regression | `LevelConfig.cs:56` hardcoded `>=4` replaced by ratio rule (Developer fix per GA SG-01); currently gives 2★ at 3/5 → DEFECT until fixed |

## B. Chapter 1 levels (DP §1 table + Ch1 AC 1–5)
| ID | Level | Steps | Expected |
|---|---|---|---|
| CH1-L1-01 | L1 Harbor/Lookout hut, wood | Load L1 | HUD 4/4 shots, only wood pieces, objective DESTROY_STRUCTURE (DP §1, AC2) |
| CH1-L1-02 | L1 stars (4 shots) | Win with 3 / 2 / 1 / 0 left | 3★ (0.75) / 2★ (0.50, just below 0.60) / 1★ (0.25, just below 0.30) / 1★ (A) |
| CH1-L1-03 | L1 mastery | Clear in 1 shot | Tick shown; 3★ (3/4). Tick never changes stars (GA SG-03) |
| CH1-L1-04 | L1 unlock | Win L1 | L2 unlocks; L3 still locked (AC3 linear) |
| CH1-L2-01 | L2 stilt tower, 4 shots | Load; hit supports | 4 shots. Support hit causes collapse via support graph |
| CH1-L2-02 | L2 stars (4 shots) | Win with 3 / 2 / 1 / 0 left; fail at 84% with 0 left; win at exactly 85% | 3★ / 2★ / 1★ / 1★; fail 0★ + Retry; win |
| CH1-L2-03a | L2 mastery: at kill plane | Tank piece centre settles at exactly y = islandKillY | **No tick** (needs y < islandKillY strictly, GA SG-21) |
| CH1-L2-03b | L2 mastery: just below | Tank centre settles at islandKillY − 0.01 | Tick (GA SG-21) |
| CH1-L2-03c | L2 mastery: waits for settle | Tank is falling past the edge when the win condition is met | No tick evaluated until physics settles; tick is then given once the centre is below islandKillY (GA SG-21, SG-04) |
| CH1-L2-03d | L2 mastery: over the edge and back | If achievable (bounce or rebound), the tank drops below islandKillY mid-flight and is above it at settle | No tick: only the post-settle position counts. Log N/A if it can't be reproduced |
| CH1-L3-01 | L3 bakery, wood+brick, 5 | Load | 5 shots, materials wood+brick only |
| CH1-L3-02 | L3 stars (5 shots) | Win with 4 / 3 / 2 / 1 / 0 left | 3★ / **3★ (3/5=0.60 exact)** / 2★ (0.40, just below 0.60) / 1★ (0.20, just below 0.30) / 1★ |
| CH1-L3-03 | L3 mastery | Destroyed objective mass after settle 0.949 vs 0.950 | Not ticked / ticked (GA SG-04) |
| CH1-L3-04 | L3→L4 bridge | Win L3 | Mill Isle bridge opens (last node of island) (AC3) |
| CH1-L4-01 | L4 windmill | Load | 5 shots. Sails rotate, hub static. Chain collapse between pieces |
| CH1-L4-02 | L4 stars + mastery (5 shots) | 3 left (0.60) → 3★, 2 left → 2★. 3★ with no projectile first-impact on a `Wall`-tagged piece vs one such impact | Tick / no tick (GA SG-04) |
| CH1-L5-01 | L5 twin cottages | One bridge shot | Both cottages count toward the objective. 85% is computed over the combined objective mass |
| CH1-L5-02 | L5 stars + mastery (5 shots) | 3 left→3★, 2 left→2★; both down in 1 shot | Tick (4 left, 3★) |
| CH1-L6-01 | L6 granary, brick+steel | Hit a steel joint vs a steel face, same shot | Joint takes 2× damage |
| CH1-L6-02 | L6 stars + mastery (5 shots) | 3 left→3★, 2 left→2★; break 2 of 3 joints vs all 3 | Not ticked / ticked |
| CH1-L6-03 | L6→L7 bridge | Win L6 | Clocktower Isle opens |
| CH1-L7-01 | L7 Clockwork Tower | Load | Same as M1 `m1_clockwork_tower` (5 shots, wood/brick/steel). M1 AC-01..12 regress-pass |
| CH1-L7-02 | L7 forced tile | Open briefing | Shockwave Core forced/unlocked in Core slot, first power-up pick (DP §1, §2) |
| CH1-L7-03 | L7 stars + mastery (5 shots) | 3 left→3★, 2→2★, 1→1★. Crown pieces with HP>0 at win: 80% vs 79% | Tick / no tick (GA SG-04, ≥80% unbroken, falling allowed) |
| CH1-L8-01 | L8 market arcade | Briefing | Splitstrike unlocked/forced in Module slot. 5 shots |
| CH1-L8-02 | L8 stars + mastery "3★" (5 shots) | Win with 3 left (0.60) / 2 left (0.40) | 3★ + tick / 2★, no tick (GA SG-03) |
| CH1-L9-01 | L9 gauntlet, 6 shots | Load | 6/6 shots |
| CH1-L9-02 | L9 stars (6 shots) | Win with 5 / 4 / 3 / 2 / 1 / 0 left | 3★ / 3★ (0.667) / 2★ (0.50, just below 0.60) / 2★ (0.333) / 1★ (0.167, just below 0.30) / 1★ |
| CH1-L9-03 | L9 "No power-up used" | Use any power-up, win with 5 / 4 / 2 / 1 left | Capped 2★ / 2★ / 2★ / 1★ (cap never raises), no tick (GA SG-03). Without power-up, 4 left: 3★ + tick |
| CH1-L9-04 | L10 gate | Try L10 with L9 not cleared | Locked; tap shows requirement (AC3) |
| CH1-ALL-01 | Data-driven | Inspect chapter asset | 10 level entries + boss entry in data, no per-level code (AC1) |
| CH1-ALL-02 | Replay | Replay a completed node with a lower result | Best stars kept; node stays replayable (AC5) |
| CH1-ALL-03 | Persistence | Kill app after each win | Unlocks and stars survive relaunch |

## C. L10 Guardian — The Tidewarden (DP §1 "L10 Guardian", AC4), budget 7
| ID | Steps | Expected |
|---|---|---|
| BOSS-01 | Load L10 | 7 shots. Phase 1 armour is wood planks over 2 steel shoulder joints |
| BOSS-02 | Break 1 joint | Still phase 1, one arm drops (or partial state). Chest stays closed |
| BOSS-03 | Break both joints | Arms drop, chest opens, transition to phase 2 (AC4) |
| BOSS-04 | Hit the core while the shutter is open | Counts 1 of 2 hits |
| BOSS-05 | Hit the shutter while closed | No core damage, shot consumed |
| BOSS-06 | Tell timing | Orange glow during the last 1s of settle before each close (turn-based, GA SG-05); shot into closed shutter = brick hit, 0 core dmg |
| BOSS-07 | Shutter cadence | Phase 2 starts open; toggles after every shot: open, closed, open… (GA SG-05) |
| BOSS-08 | Win | 2nd core hit, core destroyed, collapse cinematic, results. Reward: chapter chest (coins), Gravity Well unlocked, Gearstorm bridge open |
| BOSS-09 | Fail | Shots reach 0 with the core alive. Fail screen and Retry |
| BOSS-10 | Retry | Phase resets to 1, joints and shutter reset, 7 shots, charges restored, no leftover debris |
| BOSS-11 | Stars (7 shots) | Win with 6 / 5 / 4 / 3 / 2 / 0 left | 3★ / 3★ (0.714) / 2★ (0.571, just below 0.60) / 2★ (0.429) / 1★ (0.286, just below 0.30) / 1★. Mastery: 3 left ticked, 2 left not (≥3, GA SG-06) |
| BOSS-12 | Reward once | Re-clear L10 | Chest, Gravity Well and bridge first clear only; replay gives normal level coins (GA SG-07) |
| BOSS-13 | Readability | Observe | No hidden HP bar math needed. Phase rules visible from animation |

## D. Power-ups & loadout (DP §2)
| ID | Steps | Expected |
|---|---|---|
| PU-01 Shockwave | Hit a steel joint with 3 connected neighbours + 1 two links away | Neighbours −40 each (not ×2 even if joints); direct hit gets ×2; 2-link piece untouched (GA SG-08) |
| PU-02 Splitstrike | Fire with and without a Core modifier | 3 splits at apex, each 50% of final post-Core damage (GA SG-08) |
| PU-03 Gravity Well | Loose debris centre-to-centre at 3.9 / 4.0 / 4.01m | Pulled / pulled (inclusive) / not. 1.5s then 60 dmg burst (GA SG-08) |
| PU-04 Chain Reactor | Crates at 2.9 / 3.0 / 3.01m | Detonate / detonate / not (inclusive, GA SG-08). 2 charges |
| PU-05 Phase Piercer | Shot through 3 aligned pieces | Pieces 1–2 undamaged and passed through. Piece 3 takes full damage |
| PU-06 Time Fracture | Activate | Moving parts at 25% speed for 4s, then normal |
| PU-07 Charges | Use a 1-charge power-up | Charge 1→0, tile disabled. Chain Reactor 2→1→0 |
| PU-08 Shot cost | Use a power-up | Shot counter decrements only for the shot. The power-up itself costs no shot |
| PU-09 Retry | Use a charge, Retry | Charges restored to full |
| PU-10 Extra charge | Day-2/6 reward charge applied | +1 max per level. A second extra charge is rejected |
| SL-01 Slot count | Briefing | Exactly 3 slots: Core/Module/Booster. Max 3 power-ups |
| SL-02 Duplicates | Try 2 cores | Impossible: slots are type-locked; tap filled slot opens picker to swap (GA SG-09) |
| SL-03 Wrong slot | Drag Splitstrike into Core | Refused |
| SL-04 Same tile twice | Same tile into 2 slots | Impossible |
| SL-05 Unlock order | Profiles at L6, L7, L8, post-L10 | 0 / Shockwave / +Splitstrike / +Gravity Well. Others show silhouette + "Unlocks at Gearstorm L3/L7, Neon L3" |
| SL-06 Forced/locked slot | L7, L8 | Level data forces the tile; player can't remove it |
| SL-07 Locked tile | Tap a locked tile | Can't equip it; text shown |
| SL-08 Persistence | Set loadout on L8, kill, reopen L8; first visit to new level; drag tile out | L8 restored (local + profile); new level pre-filled with last-used loadout; drag-out clears slot (GA SG-09) |
| SL-09 Data asset | Change damage in the data asset | Effect value changes without code |
| SL-10 No purchase | Shop/IAP | No campaign power-up purchasable |

> **Live-ops dependency (sections E–H):** all reset/boundary/streak/season/leaderboard-window cases depend on Developer's **server test-clock override** (set server time, e.g. 23:59:59 UTC, without touching device clock). Without it these cases are BLOCKED. Device-clock tamper cases (DT-08, DR-07) must run with the override OFF.

## E. Daily Titan — Emberforge Titan (DP §3 "Daily Titan")
| ID | Steps | Expected |
|---|---|---|
| DT-01 | Load at 04:00 IST | Today's server config shown: archetype, arena, layout, rule, budget, mastery, rewards |
| DT-02 Reset boundary | Server time 23:59:59 UTC (05:29:59 IST) → 00:00:00 UTC (05:30:00 IST) | Old config until 23:59:59. New config and new ranked attempt at 00:00:00. Countdown hits 0 and refreshes |
| DT-03 Straddling reset | Start ranked 05:29:30 IST; server stamps submission at 00:14:59.999 / 00:15:00.000 / 00:15:00.001 UTC | Accepted / **accepted (inclusive)** / rejected and refunded as practice (GA SG-10, SG-22) |
| DT-04 Same config | Two accounts, different time zones (IST, UTC-8) | Identical config for the same UTC day |
| DT-05 Ranked limit | Finish ranked | Ranked button disabled until reset. Practice still unlimited |
| DT-06 Kill mid-ranked | Kill after 2 settled shots + 1 in flight | Submits damage up to last settled shot; attempt used; no resume (GA SG-11) |
| DT-07 Reinstall | Complete ranked, reinstall, log in | Still used (server-side) |
| DT-08 Clock tamper | Device clock +1 day / −1 day / tz change; server unreachable | No new attempt/config/reward; countdown uses server offset synced at launch+resume; with no server time, time-gated actions disabled (GA SG-20) |
| DT-09 Practice | Practice run with a high score | Not on the leaderboard and doesn't consume ranked |
| DT-10 Score | Kill with 3 shots left, damage 8,000 | Score 8,000 + 1,500 = 9,500. Damage capped at 10,000 |
| DT-11 Score fail | Run out of shots | Damage only, no shot bonus |
| DT-12 Rewards | Complete; be in top 10% | Completion chest; top 10% computed at board close (00:00 UTC + 15 min) and shards in inbox within 1h (GA SG-12) |
| DT-13 Offline | Airplane mode | Ranked + leaderboard view disabled; practice works; queued ranked result shows status, signed, retried until accepted or past window (GA SG-13) |
| DT-14 Config swap | Change server JSON | Applies at 00:00 UTC without a build |

## F. Daily rewards streak (DP §3 "Daily rewards")
| ID | Steps | Expected |
|---|---|---|
| DR-01 | Claim days 1–7 consecutively | 100c, 1 charge, 150c, shard, 200c, 1 charge, chest (coins+3 shards) |
| DR-02 Loop | Day 8 | Back to Day 1 reward |
| DR-03 Missed day | Claim D1–D3, skip a day, return | **Paused**: next claim is Day 4 (not reset to 1) |
| DR-04 Long gap / season change | Skip 10 days; skip across a season end | Resumes at next day; never breaks (GA SG-14) |
| DR-05 Once per day | Claim, kill app, relaunch same UTC day | Can't claim again |
| DR-06 Reset boundary | Claim 05:29:59 IST, again 05:30:00 IST | Second allowed (00:00 UTC boundary, GA SG-15); claims need server (GA SG-13) — offline claim blocked |
| DR-07 Clock tamper | Move clock forward | No extra claims |
| DR-08 Double tap | Rapid double tap | One grant |

## G. Seasons — The Wrecking Frontier (DP §3 "Seasons")
| ID | Steps | Expected |
|---|---|---|
| SE-01 | Event structure | 7-day event, one twist, 5 event levels |
| SE-02 | Season structure | 4 weekly events + finale, 30-tier track |
| SE-03 XP values | Earn 1 event star; 1 Titan clear; replay a 3★ event level | +10 XP; +50 XP; no XP beyond 3 stars per event level (GA SG-16) |
| SE-03b Best-run star cap | Same event level: runs of 1★, 2★, 2★, then 3★ | Level contributes 2★ after the third run (not 5, not 3); XP only for stars above the previous best (+10, +10, +0); 3★ run raises it to 3 (+10) (GA SG-23) |
| SE-03a Tier boundaries | Cumulative XP 99/100 (T1), 999/1000 (T10), 1149/1150 (T11), 2499/2500 (T20), 2699/2700 (T21), 4499/4500 (T30) | Tier N−1 / N at each pair (100×10, 150×10, 200×10 = 4,500) |
| SE-04 Overflow | XP 4799/4800 after T30; earn to 7500 and beyond | Bonus chest every 300 XP; chest 1 at 4800; 10th at 7500; no 11th (cap 10/season, GA SG-16) |
| SE-05 Rewards | Reach tier N | Free reward granted. Premium reward only if premium, cosmetic only |
| SE-06 | Premium inspect | No power, power-up or campaign access in premium |
| SE-07 Season end | Cross Monday 00:00 UTC end with unclaimed tiers | Auto-sent to inbox, kept 30 days (day 30 present, day 31 gone); owned items kept (GA SG-17, SG-18) |
| SE-08 Rollover | New season starts | Tier resets to 0. Campaign stars/unlocks unchanged |
| SE-09 Permanence | After expiry | All campaign levels and owned items still accessible |
| SE-10 Server config | Change the schedule remotely | Applies without a build |

## H. Leaderboards (DP §3 "Leaderboards")
| ID | Steps | Expected |
|---|---|---|
| LB-01 Submit | Finish a ranked Titan run | Appears on the Daily board after validation |
| LB-02 Validation | Submit a tampered/unvalidated score (e.g. 99,999 or modified client) | Rejected; not shown |
| LB-03 Practice | Practice score | Never submitted |
| LB-04 Ties | 2 players with equal scores at t1 < t2 | Earlier submitter ranks higher |
| LB-05 Top 100 | 150 entries, player rank 120 | Top 100 + own row "#120" |
| LB-06 Reset | Daily 00:00 UTC; weekly Monday 00:00 UTC; season on Monday boundary | New board; previous day/week/season archived read-only with own final rank + top 100 (GA SG-18) |
| LB-07 Friends | Friends filter | Only linked friends |
| LB-08 Guest | Guest plays ranked, then links | Scores stored; on link, current open-board scores migrate and list; closed-board scores don't (GA SG-19). ⚠ DP says guests must link to appear; consistent |
| LB-09 Offline | Lose connection at submit | Signed local queue, retried until accepted or past GA SG-10 window; visible status; leaderboard view disabled offline (GA SG-13) |

## I. Free Hint (ADD §1)
| ID | Steps | Expected |
|---|---|---|
| HINT-01 🎨 | Fresh level: fail once | Fail screen: **no** hint button (ADD §1 Acceptance) |
| HINT-02 🎨 | Fail a 2nd time in a row on the same level | Hint button shown; one tap, free |
| HINT-03 | Fail a 3rd, 4th… time | Button shown on every fail after the 2nd |
| HINT-04 | Fail, **win**, fail | Counter reset by the clear: no hint on that fail; hint again only after the next 2nd consecutive fail |
| HINT-05 | Fail L3 once, fail L4 ×3, fail L3 again | L3 shows the hint (per-level streak; other levels don't break it). L4 shows from its own 2nd fail (ADD-GA SG-25) |
| HINT-06 🎨 | Use hint → retry | First aim: `hintTarget` piece pulses and a ghost arc shows at `hintAim` yaw/pitch; vanishes when the first shot fires |
| HINT-07 | After HINT-06, fail again without using the hint; retry | No pulse/arc unless the hint is tapped again (next attempt only) |
| HINT-08 | Use hint, win with N shots left | Stars per % shots left exactly as without the hint; mastery and score unchanged (ADD §1, §3) |
| HINT-09 | Inspect the hint flow, offline and online | No ad, currency, timer, purchase or confirmation step |
| HINT-10 | Level without `hintTarget`/`hintAim` data, fail ×3 | Button never shown |
| HINT-11 | Titan ranked fail ×2 / Titan practice fail ×2 / Weekly Event level fail ×2 (with hint data) | Never / shown / shown (ADD-GA SG-26) |
| HINT-12 | Fail ×1, kill app, relaunch, fail ×1 | Hint shown: the streak survives the restart (ADD-GA SG-27) |
| HINT-12a | Fail ×2, kill, relaunch, open fail screen on next fail | Hint still shown (counter 3) |
| HINT-13 | Give Up, then Give Up | 2nd Give Up shows the hint (Give Up = fail, ADD-GA SG-28) |
| HINT-14 | Fail ×1, quit to map mid-level, return, fail ×1 | Hint shown: quitting doesn't count and doesn't reset (counter 1→1→2) |
| HINT-15 | Fail ×1, quit to map ×3, return, win | Counter reset by the clear; next fail shows no hint |
| HINT-16 | Fail ×1, uninstall + reinstall (or new device), fail ×1 | No hint: counter is device-local and restarts at 0 (GA SG-35); not synced to the account |
| HINT-16a | Fail ×2 on device A, sign in on device B, fail ×1 | No hint on B (counter 1) (GA SG-35) |

## J. Combo (ADD §2)
| ID | Steps | Expected |
|---|---|---|
| CMB-01 | Breaks at t=0, 0.80s, 1.60s (gaps exactly 0.80 from the previous break) | Chain continues: x3 ("0.8s or less", ADD-GA SG-29) |
| CMB-01a | Impact at t=0, first break at t=1.2s | Combo starts at the first break (count 1); the window isn't measured from impact |
| CMB-01b | Breaks at t=0, 0.7, 1.4, 2.1 (each 0.7 from the previous, 2.1 from the first) | x4: window is relative to the previous break, not the first |
| CMB-02 | Breaks at t=0, 0.5, 1.301 (gap 0.801) | Combo ends at x2; next break starts a new combo at 1 |
| CMB-03 | Out-of-bounds push (ObjectivePiece leaves bounds) within 0.8s | Counts +1, same as a destroy |
| CMB-04 | NonObjective prop destroyed in a chain | Doesn't count (ObjectivePiece only) |
| CMB-04a | Combo still chaining when settle ends (breaks at settle −0.2s; next break would come at settle +0.3s) | Settle is not extended; combo is final at settle end (ADD-GA SG-29) |
| CMB-05 | Shot 1 ends at x4; shot 2 first break | Starts at 1, never carries over |
| CMB-06 🎨 | Reach x2 | Nothing displayed |
| CMB-07 🎨 | Reach x3, x4 | "COMBO x3", "COMBO x4" shown |
| CMB-08 🎨 | Reach x5, x10, x20 | Bigger pop at each of those values |
| CMB-09 | Same level, same shots left, once with a x8 combo and once with none | Identical stars, mastery, win/fail; only the score differs (50 × highest combo, ADD §3) |
| CMB-10 | A piece reaches HP 0 and then falls out of bounds in the same shot | Counts once (ADD-GA SG-30) |
| CMB-10a | Piece pushed out of bounds, bounces back, leaves again | Counts once per shot |
| CMB-11 | Winning run: shot 1 x4, shot 2 x2, shot 3 x6 | Highest combo = **6** (not 12, not 6+4+2): max over single shots, never summed (GA SG-34); score combo part = 50×6 = 300 |
| CMB-11a | Failed attempt with x9, retry wins with best x3 | Highest combo = 3; failed attempts don't count (GA SG-34) |

## K. Campaign score (ADD §3)
Formula (ADD §3 + ADD-GA SG-31, integer maths): score = floor(1000 × min(settledObjective%, 1.0)) + 500 × shotsLeft + 50 × highestCombo + 1000 × mastery. Highest combo counts even below x3; a shot with no breaks = 0. Worked examples re-checked against this rule.
| ID | Setup | Expected score |
|---|---|---|
| SC-01 | Win at 0.85, 0 left, highest combo 1, no mastery | 850 + 0 + 50 + 0 = **900** |
| SC-02 | L3: 0.92, 3 left, combo 7, no mastery | 920 + 1500 + 350 = **2,770** |
| SC-03 | L3: 0.96, 2 left, combo 4, mastery (≥0.95) | 960 + 1000 + 200 + 1000 = **3,160** |
| SC-04 | 1.00, 3 left, combo 12, mastery | 1000 + 1500 + 600 + 1000 = **4,100** |
| SC-04a | Settled % reported as 1.04 (e.g. out-of-bounds over-count), 3 left, combo 12, mastery | Capped: still **4,100**, not 4,140 |
| SC-04b | 0.9999 destroyed, 0 left, highest combo 2, no mastery | floor(999.9) = 999 + 0 + 100 = **1,099** |
| SC-04d | Win where no shot produced a break chain >0 (edge) | combo term 0 |
| SC-04c | Highest combo x2 (never displayed) | +100 counted (below x3 still counts) |
| SC-05 | 0.873 / 0.8739 / 0.8731 destroyed | 873 / 873 / 873 from the % term (floor, no rounding up) |
| SC-06 | Fail at any % | No score; best not changed |
| SC-07 | Win 2,770 then 900 | Best stays 2,770 |
| SC-08 | Best scores on 3 levels: 900 + 2,770 + 4,100 | Campaign total 7,770 |
| SC-09 | Two runs: score 4,100 with 2★ vs 2,770 with 3★ | Stars come only from % shots left; score never changes stars, unlocks or rewards |
| SC-10 | Bridge/node unlocks | Not gated on score anywhere |
| SC-11 | Daily Titan | Uses the DP v1 formula (damage + 500/shot left), not this one |
| SC-12 🎨 **OPEN vs Art v2 (FAIL)** | Win screen | Formula is right, art is wrong (ADD-GA SC-12). L2 (4 shots, 9 pieces) with 2 shots left: max = 1000 + 1000 + 50×9 + 1000 = **3,450**. Art v2 shows 3,850, which is impossible. *Correction: rev b's "3,850 = 3,500 + x7" wrongly assumed 3 shots left (QA error).* Realistic example: 3,250 (100%, 2 left, x5, mastery). See Reference_Screen_Parity_v2 |
| SC-13 | Campaign leaderboard total | Sum of each level's best score from **server-validated wins only** (ADD-GA SG-32) |
| SC-13a | Practice / Titan / event runs with high scores | Excluded from the campaign total |
| SC-13b | Unvalidated or tampered win | Not counted; previous best kept |
| SC-13c | Two players reach the same total at t1 < t2 (server timestamp) | Earlier player ranks higher |
| SC-13d | Cross a day/week/season boundary | No reset (all-time) |
| SC-13e | View | Top 100 + own rank + friends filter |
| SC-13f | Guest wins levels, opens campaign board | Not listed (own row hidden, "link to appear"), score still stored (GA SG-36, SG-19) |
| SC-13g | Guest with validated bests links an account | Listed with all validated bests carried over (same total, original timestamp); board not reset (GA SG-36) |

## L. Store scope (ADD §4, PM VM-13)
| ID | Steps | Expected |
|---|---|---|
| ST-01 | Dump all SKUs from store data | Every item has `cosmetic=true` or is a premium currency pack |
| ST-02 | Add a test SKU with `cosmetic=false` | Build/validation rejects it |
| ST-03 🎨 | Browse the store | Only: currency packs, Wrecker skins/paint/decals/trails/VFX, avatars/frames/titles, map decorations, season premium track, cosmetic bundles, emotes/victory animations |
| ST-04 | Negative: search store and every purchase flow for power-ups or charges | None for sale (real money or any currency) |
| ST-05 | Negative: shots, extra shots, continues (incl. fail screen) | None |
| ST-06 | Negative: level skips, unlocks, stars, campaign progress | None |
| ST-07 | Negative: Daily Titan attempts, leaderboard advantages | None |
| ST-08 | Negative: equipment upgrades that change gameplay | None |
| ST-09 | Spend premium currency on every item type | Only cosmetics obtainable |
| ST-10 | Spend coins | Coins buy only coin-priced cosmetics (ADD-GA SG-33); gameplay items not purchasable |
| ST-10a | Item priced in premium currency only: try to buy it with coins | Not possible |
| ST-10b | Premium currency on any cosmetic | Allowed; never-sold list unavailable to either currency |
| ST-11 | Buy every cosmetic, then replay L1–L10 and Titan | Shots, charges, stars, unlocks and Titan scores identical to a non-paying account |
| ST-12 | Season premium track purchased | Rewards cosmetic only (DP §3, ADD §4) |
| ST-13 🎨 | Coin/gem "+" buttons (VM-13) | Open the store, which shows only cosmetic/currency items |

### Cases to re-review against Art painted v2 screens
HINT-01/02/06, CMB-06/07/08, SC-12, ST-03, ST-13, plus the earlier star cases on briefing, HUD and results (CH1-L1-02 through BOSS-11, STAR-*), which depend on UX-11/12/14/17/19/20.

## Spec gaps / ambiguities — status v1.1
Closed against `design/Spec_Gap_Answers_v1.md`: **SG-00 … SG-20 all answered (21 closed)**; each answer is cited inline as GA SG-xx. Note SG-00: DP header now reads "v1 approved by PM" (GA SG-00).

Raised by the v1.1 review:
- **SG-21 — CLOSED** (GA SG-21): L2 mastery is "tank off the island edge", y < islandKillY after settle. See CH1-L2-03a–d.
- **SG-22 — CLOSED** (GA SG-22): 00:15:00.000 UTC is inclusive. See DT-03.
- **SG-23 — CLOSED** (GA SG-23): best single run, max 3, never summed. See SE-03b.
- **SG-24 (Low) — OPEN, owner Developer** Float comparison at exact thresholds (3/5 vs 0.60): Developer should compare with integer math or an epsilon; QA covers in STAR-01.
- **SG-25 — CLOSED** (ADD-GA SG-25): per-level streak, only a clear resets it. HINT-05.
- **SG-26 — CLOSED** (ADD-GA SG-26): shown in events + Titan practice, never in Titan ranked. HINT-11.
- **SG-27 — CLOSED** (ADD-GA SG-27): saved locally per level, survives kill. HINT-12/12a.
- **SG-28 — CLOSED** (ADD-GA SG-28): Give Up = fail; quit to map doesn't count. HINT-13–15.
- **SG-29 — CLOSED** (ADD-GA SG-29): window from previous break; no settle extension. CMB-01a/01b/04a.
- **SG-30 — CLOSED** (ADD-GA SG-30): one point per piece per shot. CMB-10/10a.
- **SG-31 — CLOSED** (ADD-GA SG-31): settled %, cap 1.0, floor, integers, combo counts below x3. SC-04a–c, SC-05.
- **SG-32 — CLOSED** (ADD-GA SG-32): validated wins only, all-time, earlier timestamp wins ties. SC-13a–e.
- **SG-33 — CLOSED** (ADD-GA SG-33): coins buy coin-priced cosmetics only. ST-10/10a/10b.
- **SG-34 — CLOSED** (GA SG-34): largest single-shot combo in the winning run; never summed; failed attempts excluded. CMB-11/11a.
- **SG-35 — CLOSED** (GA SG-35): device-local; resets to 0 on reinstall/new device (intended). HINT-16/16a.
- **SG-36 — CLOSED** (GA SG-36): guests must link to be listed; validated bests carry over; all-time, no reset. SC-13f/g.
