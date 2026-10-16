# CRASHVERSE — Feature Test Cases v1 (QA)
Source of truth: `design/Design_Pack_v1_Ch1_Powerups_LiveOps.md` (cited as **DP §x**), with M1 rules from `design/M1_Level_Design_Contract_v0.1.md`. Note: the pack header still says "Status: draft for PM approval". PM has stated it's approved; please update the header (SG-00).
Execution status: **all BLOCKED, no APK.** Live-ops cases also need a server/config backend and a test clock.
Global M1 rules (DP intro): win = ≥85% objective mass destroyed after settle. Stars on win only: 3★ ≥60% shots left, 2★ ≥30%, 1★ any win. Retry restores everything.

## A. Star band oracle (DP intro), applied to every level
| Budget | 3★ (shots left) | 2★ | 1★ |
|---|---|---|---|
| 4 (L1–2) | 3–4 (≥2.4) | 2 (≥1.2) | 0–1 |
| 5 (L3–8) | 3–5 (≥3.0) | 2 (≥1.5) | 0–1 |
| 6 (L9) | 4–6 (≥3.6) | 2–3 (≥1.8) | 0–1 |
| 7 (L10) | 5–7 (≥4.2) | 3–4 (≥2.1) | 0–2 |
⚠ For 5 shots, 3 left gives 3★ under the % rule but 2★ under the M1 table (2–3→2★), and the current code follows M1. See SG-01. Cases below use the DP % rule and flag the 3/5 boundary.

## B. Chapter 1 levels (DP §1 table + Ch1 AC 1–5)
| ID | Level | Steps | Expected |
|---|---|---|---|
| CH1-L1-01 | L1 Harbor/Lookout hut, wood | Load L1 | HUD 4/4 shots, only wood pieces, objective DESTROY_STRUCTURE (DP §1, AC2) |
| CH1-L1-02 | L1 | Win with 3 left; win with 2; win with 1 | 3★ / 2★ / 1★ |
| CH1-L1-03 | L1 mastery | Clear in 1 shot | Mastery tick shown; 3★ (3 left) |
| CH1-L1-04 | L1 unlock | Win L1 | L2 unlocks; L3 still locked (AC3 linear) |
| CH1-L2-01 | L2 stilt tower, 4 shots | Load; hit supports | 4 shots. Support hit causes collapse via support graph |
| CH1-L2-02 | L2 stars | Fail at 84% with 0 left / win at exactly 85% after settle | Fail, 0★, Retry / win |
| CH1-L2-03 | L2 mastery | Topple the tank into the sea | Mastery tick (detection rule: SG-04) |
| CH1-L3-01 | L3 bakery, wood+brick, 5 | Load | 5 shots, materials wood+brick only |
| CH1-L3-02 | L3 stars | Win with 3 left | **3★ per DP** (boundary SG-01). Code gives 2★: log a defect if DP is confirmed |
| CH1-L3-03 | L3 mastery | Finish at 94% vs ≥95% | 94% not ticked; 95% ticked |
| CH1-L3-04 | L3→L4 bridge | Win L3 | Mill Isle bridge opens (last node of island) (AC3) |
| CH1-L4-01 | L4 windmill | Load | 5 shots. Sails rotate, hub static. Chain collapse between pieces |
| CH1-L4-02 | L4 mastery | 3★ with zero wall hits vs one wall hit | Ticked vs not ("wall" definition SG-04) |
| CH1-L5-01 | L5 twin cottages | One bridge shot | Both cottages count toward the objective. 85% is computed over the combined objective mass |
| CH1-L5-02 | L5 mastery | Both down in 1 shot | Tick |
| CH1-L6-01 | L6 granary, brick+steel | Hit a steel joint vs a steel face, same shot | Joint takes 2× damage |
| CH1-L6-02 | L6 mastery | Break 2 of 3 joints vs all 3 | Not ticked / ticked |
| CH1-L6-03 | L6→L7 bridge | Win L6 | Clocktower Isle opens |
| CH1-L7-01 | L7 Clockwork Tower | Load | Same as M1 `m1_clockwork_tower` (5 shots, wood/brick/steel). M1 AC-01..12 regress-pass |
| CH1-L7-02 | L7 forced tile | Open briefing | Shockwave Core forced/unlocked in Core slot, first power-up pick (DP §1, §2) |
| CH1-L7-03 | L7 mastery | Crown falls intact | Tick (intact definition SG-04) |
| CH1-L8-01 | L8 market arcade | Briefing | Splitstrike unlocked/forced in Module slot. 5 shots |
| CH1-L8-02 | L8 mastery "3★" | Win with 3 left | 3★ + mastery tick |
| CH1-L9-01 | L9 gauntlet, 6 shots | Load | 6/6 shots |
| CH1-L9-02 | L9 stars | Win with 4 / 3 / 2 / 1 left | 3★ / 2★ / 2★ / 1★ |
| CH1-L9-03 | L9 "No power-up used" | Use any power-up, win with 5 left | 3★ blocked (max 2★) and no mastery tick (DP §2). Without power-up: 3★ |
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
| BOSS-06 | Tell timing | Shutter glows orange **1s** before closing, every closing (measure ±1 frame) |
| BOSS-07 | Shutter cadence | Shielded "every other turn": verify the alternation against the shot count (SG-05) |
| BOSS-08 | Win | 2nd core hit, core destroyed, collapse cinematic, results. Reward: chapter chest (coins), Gravity Well unlocked, Gearstorm bridge open |
| BOSS-09 | Fail | Shots reach 0 with the core alive. Fail screen and Retry |
| BOSS-10 | Retry | Phase resets to 1, joints and shutter reset, 7 shots, charges restored, no leftover debris |
| BOSS-11 | Stars | Win with 5 / 3 / 2 left → 3★ / 2★ / 1★. Mastery "3 shots left" ticked at ≥3? (SG-06) |
| BOSS-12 | Reward once | Re-clear L10 | Gravity Well/bridge not re-granted. Chest repeat rule (SG-07) |
| BOSS-13 | Readability | Observe | No hidden HP bar math needed. Phase rules visible from animation |

## D. Power-ups & loadout (DP §2)
| ID | Steps | Expected |
|---|---|---|
| PU-01 Shockwave | Hit a piece with 3 connected neighbours + 1 two links away | Neighbours −40 HP each. The 2-link piece is untouched |
| PU-02 Splitstrike | Fire | Splits into 3 at the apex, each 50% of base damage |
| PU-03 Gravity Well | Loose debris at 3.9m / 4.0m / 4.1m | Pulled for 1.5s then burst 60 dmg. 4.0m inclusive? (SG-08). 4.1m not pulled |
| PU-04 Chain Reactor | Explosive crates at 2.9 / 3.0 / 3.1m | Detonate within 3m. 2 charges per level |
| PU-05 Phase Piercer | Shot through 3 aligned pieces | Pieces 1–2 undamaged and passed through. Piece 3 takes full damage |
| PU-06 Time Fracture | Activate | Moving parts at 25% speed for 4s, then normal |
| PU-07 Charges | Use a 1-charge power-up | Charge 1→0, tile disabled. Chain Reactor 2→1→0 |
| PU-08 Shot cost | Use a power-up | Shot counter decrements only for the shot. The power-up itself costs no shot |
| PU-09 Retry | Use a charge, Retry | Charges restored to full |
| PU-10 Extra charge | Day-2/6 reward charge applied | +1 max per level. A second extra charge is rejected |
| SL-01 Slot count | Briefing | Exactly 3 slots: Core/Module/Booster. Max 3 power-ups |
| SL-02 Duplicates | Try 2 cores (Shockwave + Chain Reactor) | Rejected; second replaces or is refused, defined UX |
| SL-03 Wrong slot | Drag Splitstrike into Core | Refused |
| SL-04 Same tile twice | Same tile into 2 slots | Impossible |
| SL-05 Unlock order | Profiles at L6, L7, L8, post-L10 | 0 / Shockwave / +Splitstrike / +Gravity Well. Others show silhouette + "Unlocks at Gearstorm L3/L7, Neon L3" |
| SL-06 Forced/locked slot | L7, L8 | Level data forces the tile; player can't remove it |
| SL-07 Locked tile | Tap a locked tile | Can't equip it; text shown |
| SL-08 Persistence | Set loadout, kill app, reopen | Restored (rule SG-09) |
| SL-09 Data asset | Change damage in the data asset | Effect value changes without code |
| SL-10 No purchase | Shop/IAP | No campaign power-up purchasable |

## E. Daily Titan — Emberforge Titan (DP §3 "Daily Titan")
| ID | Steps | Expected |
|---|---|---|
| DT-01 | Load at 04:00 IST | Today's server config shown: archetype, arena, layout, rule, budget, mastery, rewards |
| DT-02 Reset boundary | Server time 23:59:59 UTC (05:29:59 IST) → 00:00:00 UTC (05:30:00 IST) | Old config until 23:59:59. New config and new ranked attempt at 00:00:00. Countdown hits 0 and refreshes |
| DT-03 Attempt in progress at reset | Start ranked at 05:29:30 IST, finish 05:30:30 | Defined: submitted against the start day or rejected (SG-10) |
| DT-04 Same config | Two accounts, different time zones (IST, UTC-8) | Identical config for the same UTC day |
| DT-05 Ranked limit | Finish ranked | Ranked button disabled until reset. Practice still unlimited |
| DT-06 Kill mid-ranked | Kill app during the ranked attempt, relaunch | Attempt counted as used (no free retry). Outcome rule SG-11 |
| DT-07 Reinstall | Complete ranked, reinstall, log in | Still used (server-side) |
| DT-08 Clock tamper | Set device clock +1 day / −1 day | No new ranked attempt and no new config. Reset uses server time only |
| DT-09 Practice | Practice run with a high score | Not on the leaderboard and doesn't consume ranked |
| DT-10 Score | Kill with 3 shots left, damage 8,000 | Score 8,000 + 1,500 = 9,500. Damage capped at 10,000 |
| DT-11 Score fail | Run out of shots | Damage only, no shot bonus |
| DT-12 Rewards | Complete; top 10% | Completion chest; bonus shards if in top 10% (when evaluated: SG-12) |
| DT-13 Offline | Airplane mode | Ranked disabled. Practice per SG-13 |
| DT-14 Config swap | Change server JSON | Applies at 00:00 UTC without a build |

## F. Daily rewards streak (DP §3 "Daily rewards")
| ID | Steps | Expected |
|---|---|---|
| DR-01 | Claim days 1–7 consecutively | 100c, 1 charge, 150c, shard, 200c, 1 charge, chest (coins+3 shards) |
| DR-02 Loop | Day 8 | Back to Day 1 reward |
| DR-03 Missed day | Claim D1–D3, skip a day, return | **Paused**: next claim is Day 4 (not reset to 1) |
| DR-04 Long gap | Skip 10 days | Still resumes at the next day (no break rule exists: SG-14) |
| DR-05 Once per day | Claim, kill app, relaunch same UTC day | Can't claim again |
| DR-06 Reset boundary | Claim 05:29 IST and again 05:30 IST | Second claim allowed if the day boundary is UTC (SG-15) |
| DR-07 Clock tamper | Move clock forward | No extra claims |
| DR-08 Double tap | Rapid double tap | One grant |

## G. Seasons — The Wrecking Frontier (DP §3 "Seasons")
| ID | Steps | Expected |
|---|---|---|
| SE-01 | Event structure | 7-day event, one twist, 5 event levels |
| SE-02 | Season structure | 4 weekly events + finale, 30-tier track |
| SE-03 XP | Earn event stars / Titan clear | Tier XP increases. **Thresholds not specified** (SG-16): test each tier boundary at threshold−1 / threshold once defined |
| SE-04 | Reach tier 30, keep earning | No tier 31. Overflow rule SG-16 |
| SE-05 Rewards | Reach tier N | Free reward granted. Premium reward only if premium, cosmetic only |
| SE-06 | Premium inspect | No power, power-up or campaign access in premium |
| SE-07 Season end | Cross the end time | Track closes; unclaimed rewards rule SG-17. Owned items kept |
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
| LB-06 Reset | Daily at 00:00 UTC, Weekly at event end, Season at season end | Boards empty/new; old ones archived (SG-18) |
| LB-07 Friends | Friends filter | Only linked friends |
| LB-08 Guest | Guest plays ranked | Not listed; link prompt. After linking: rule for earlier scores (SG-19) |
| LB-09 Offline | Finish ranked offline / lose connection on submit | Queued and retried or blocked, never lost silently (SG-13) |

## Spec gaps / ambiguities (for Game designer via PM)
- **SG-00** Pack header says "draft for PM approval"; mark it approved/versioned.
- **SG-01 (High)** Star rule conflict at 5 shots: DP % rule gives 3 left → 3★; M1 contract and `LevelConfig.cs:56` give 2★. Also confirm rounding (≥ exact %, no rounding is assumed above).
- **SG-02** "Shots" vs M1 "moves" naming; confirm they're identical.
- **SG-03** Can mastery goals change stars (L8 "3★", L9 blocks 3★) or are they a separate tick only? Only the L9 "No power-up" coupling is defined.
- **SG-04** Detection definitions missing: "into sea" (L2), "wall hit" (L4), "crown intact" (L7), 95% measured after settle (L3)?
- **SG-05** Tidewarden "every other turn": is a turn = a shot? Which state does phase 2 start in? Does the shutter tell depend on real time (1s) or turns?
- **SG-06** "Defeat with 3 shots left": exactly 3 or ≥3?
- **SG-07** Chapter chest on replay: once only?
- **SG-08** Radii inclusive (4m, 3m)? Splitstrike 50% of what (base or core-modified)? Shockwave stacking with the 2× joint multiplier?
- **SG-09** Loadout persistence: per level, global, or reset each briefing? Duplicates across slots and swap UX.
- **SG-10** Ranked attempt that straddles 00:00 UTC.
- **SG-11** App killed mid-ranked: is it counted as a fail, the partial damage, or resumable?
- **SG-12** When the top 10% is computed and paid out.
- **SG-13** Offline behaviour for Titan practice/ranked, reward claim, leaderboard submit queue.
- **SG-14** Streak "break" condition: the pack only says a missed day pauses. Is there ever a break/reset (e.g. after N days, or at season end)?
- **SG-15** Daily reward day boundary: UTC or local?
- **SG-16** Season XP: per-tier thresholds, XP per star/Titan clear, overflow after tier 30.
- **SG-17** Unclaimed tier rewards at season end; whether event stars are capped.
- **SG-18** Weekly/season reset exact times (UTC?) and archive visibility.
- **SG-19** Guest→linked account: do pre-link ranked scores count?
- **SG-20** Clock-tamper policy and the server time source for the client countdown.
