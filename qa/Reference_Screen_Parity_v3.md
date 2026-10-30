# CRASHVERSE — Reference Screen Parity v3 (QA) — Art v2 FINAL re-check
Date: 2026-10-16 IST. Inputs: /workspace/art/v2 (board_v2.png, 14 screens, OVERLAYS.md, README). Target: user-owned board (exact visual target). Specs: DP, ADD v1.1, Spec_Gap_Answers_v1, **Art_v2_Decisions_v1 (GD, applied here)**. v2 kept unchanged.
Method same as v2: P present/matching, D divergent, M missing; % = P/total. All screens viewed.

## Scores
| # | Screen | Board P/D/M | Board % | v2 | Spec P/D/M | Spec % | v2 |
|---|---|---|---|---|---|---|---|
| 01 | Login | 3/1/4 | 38% | 25% | 4/0/0 | 100% | 75% |
| 02 | Profile | 1/4/0 | 20% | 20% | 4/1/0 | 80% | 80% |
| 03 | World map | 6/1/1 | 75% | 50% | 7/0/0 | 100% | 29% |
| 03b | Map guide | 3/0/3 | 50% | 0% | n/a | n/a | n/a |
| 04 | Level select | 2/3/2 | 29% | 29% | 8/0/0 | 100% | 63% |
| 05 | Gameplay | 1/4/0 | 20% | 20% | 9/1/0 | 90% | 70% |
| 06 | Level complete | 2/2/0 | 50% | 50% | 5/1/1 | 71% | 80% |
| 07+13 | L10 boss / arena (board P7) | 1/2/1 | 25% | 25% | 6/0/0 | 100% | 50% |
| 08 | Daily Titan | 3/0/1 | 75% | 75% | 5/1/1 | 71% | 57% |
| 09 | Power-ups | 1/3/0 | 25% | 25% | 4/0/1 | 80% | 60% |
| 10 | Season track | 1/3/0 | 25% | 75% | 5/1/0 | 83% | 33% |
| 11 | Rewards | 1/2/4 | 14% | 14% | 7/0/0 | 100% | 43% |
| | **Overall** | **25/25/16** | **38% (25/66)** | 32% | **64/5/3** | **89% (64/72)** | 57% (40/70) |
Extra screens (not in v2 totals): 12 Fail 6/6 spec, 13 Arena 5/5 spec → incl. these spec = **92% (75/83)**.
Board match stays low by design: v2 follows the spec where the board conflicts (C-01…C-14 still awaiting the user). 10 dropped vs board because it's now a DP season track, not the board's season-banner list (C-04).

## Claimed fixes — verification
| Item | Result |
|---|---|
| UX-27 07 L10 Tidewarden, SHOTS 7, GOAL defeat the guardian | **PASS** |
| UX-28 / SC-12 06 score 3,250, 2 left, 2★ (2/4=0.50) | **PASS** |
| UX-29 01 official Google "Sign in with Google" + Meta "f" on #0866FF; X removed (Instagram absent) | **PASS** (use SDK buttons in-app) |
| UX-30 09 Splitstrike violet (Module), Chain Reactor teal (Core) | **PASS** |
| UX-31 05 FREE HINT removed from HUD; 12 fail shows FREE HINT, no ad icon | **PASS** (conditional display = engine, ART-06) |
| UX-32 11 days 100/+1 charge/150/shard/200/+1 charge/chest; no gems; Tier 21 2,700/4,500; pause-never-resets note | **PASS** (Low N-02: Day 7 label truncated "CHEST +3") |
| UX-33 08 rules plate, 6 shots, 1 ranked + unlimited practice, RANKED/PRACTICE | **PASS** |
| UX-34 03 10 nodes in order, 1–3 3★/4 2★/5 1★, 6 current ring, 7–9 locked, gate, BOSS node 10 | **PASS** |
| UX-35 04 bands 3 LEFT/2 LEFT/ANY WIN, SHOTS 5, mastery line (L7: 3/5=0.60, 2/5=0.40≥0.30) | **PASS** |
| UX-36 10 season track, free+premium cosmetic, ends in 21 days, weekly event | **PASS** (tick+lock caveat, see ART-05) |
| 03b map guide; 13 Tidewarden arena (phase, core hits, shots) | **PASS** |
| UX-15 charges ⚡x1, +1 CHARGE | **PASS** (13 arena badges engine-built) |
| UX-23 shard vs gem distinct, no gem rewards | **PASS** |

## GD decisions graded (Art_v2_Decisions_v1)
| Item | Result |
|---|---|
| 06 mastery row (tick/empty box, never affects stars) + "Level N Complete" (UX-38) | **FAIL (Low, engine-built per OVERLAYS)**: target shows neither the row nor the level number. Art should add a placeholder row/title or the engine spec (ART-01) governs. |
| C-08 HP bar = 10,000 − damage, marks 75/50/25% | **FAIL (Low)**: bar present ("HP 100%") but no 75/50/25 marks. |
| UX-37 no live campaign score | **FAIL (Med)**: 05 still shows SCORE 1,240 in the HUD. Must be removed (13 arena correctly has none). |
| SG-37 milestones 10/20/30 per chapter | **PASS** (chests; no gems). Reward contents engine/data (ART-04). |

## Known placeholders (not failed)
Flat PIL patches (engine-built UI). **Season premium tiers 1–3 show tick + padlock together → ART-05: engine must NOT copy** (test added).

## New issues (v3)
| ID | Sev | Screen | Issue |
|---|---|---|---|
| UX-40 | Med | 05 | Live SCORE box must go (UX-37 decision). |
| UX-41 | Med | 08 | Reward chest / top-10% bonus no longer shown (was P in v2) — regression; restore or confirm engine-built. |
| UX-42 | Low | 08 | HP bar lacks 75/50/25 marks (C-08 decision). |
| UX-43 | Low | 06 | No mastery row placeholder, no "Level N" (ART-01, UX-38). |
| UX-44 | Low | 11 | Day 7 label "CHEST +3" truncated (chest: 300 coins + 3 shards). |
| UX-45 | Low | 03, 05 | Visible inpaint smudges (path near nodes 6/8; dark patch bottom-left of 05). |
Carried open: UX-01/02/04/06 (measure on device), UX-39 effect line on 09 (engine), C-01…C-14 and VM-04/10/11 awaiting user/PM.

## Automation
- Feature_Test_Cases_v1.3: §M ART-01…06 (incl. ART-05 tick+padlock prohibition), §L2 refund/debt ST-20…33 (revised SG-17).
- qa_automation/visual: baselines_art_v2/ + art_v2_targets.json (secondary painted-art baselines, engine UI masked). Primary baselines (user board) unchanged. Not runnable until APK.
