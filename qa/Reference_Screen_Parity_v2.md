# CRASHVERSE: Reference Screen Parity v2 (QA), 16 Oct 2026 IST
v1 is kept unchanged: `Reference_Screen_Parity_v1.md`, Art v1, overall 39%.

**What was compared**
- Art painted v2: `/workspace/art/v2/screens/01–11` (1080×1920). Each screen was viewed, plus the contact sheet `board_v2.png`.
- The target is the user's reference board "SMASH CITY" (`…/attachments/47da2a84…cabb7.png`).

**Two axes**
1. **Board match.** The board is user-owned and the exact visual target, so reusing its style or elements is wanted, not copying. The only exception is social login, which must use each provider's official logo files.
   - The element checklist is the v1 one, 66 elements, so the delta to v1 is like-for-like.
2. **Spec compliance** against DP, M1 contract, GA, ADD and ADD-GA, checking:
   - the 85% goal
   - no power meter
   - the real 6 power-ups and their slots
   - no paid progress or paid shots
   - stars from % of shots left
   - the hint, combo, score and store rules
   - Daily Titan rules

**Scoring:** P = present and matching, D = divergent, M = missing. % = P / total.
**KNOWN** marks issues Art has already reported: the notch/top inset, hand-painted patches (04/07 "85%" re-text and greyed stars, 03 node "4", 05 Shockwave icon), and star rendering.

## Scores
| # | Screen | Board P/D/M | Board % | v1 board % | Δ | Spec P/D/M | Spec % |
|---|---|---|---|---|---|---|---|
| 01 | Login | 2/4/2 | **25%** | 38% | −13 | 3/0/1 | **75%** |
| 02 | Create profile | 1/4/0 | **20%** | 0% | +20 | 4/1/0 | **80%** |
| 03 | World map | 4/2/2 | **50%** | 50% | 0 | 2/4/1 | **29%** |
| 03b | Map legend (board P3b) | 0/0/6 | **0%** | 0% | 0 | n/a | n/a |
| 04 | Level select | 2/3/2 | **29%** | 57% | −28 | 5/0/3 | **63%** |
| 05 | Gameplay | 1/4/0 | **20%** | 40% | −20 | 7/3/0 | **70%** |
| 06 | Level complete | 2/2/0 | **50%** | 75% | −25 | 4/1/0 | **80%** |
| 07 | Hard level (10th) | 1/1/2 | **25%** | 50% | −25 | 3/3/0 | **50%** |
| 08 | Daily boss | 3/0/1 | **75%** | 50% | +25 | 4/1/2 | **57%** |
| 09 | Power-ups | 1/3/0 | **25%** | 50% | −25 | 3/1/1 | **60%** |
| 10 | Seasons | 3/1/0 | **75%** | 25% | +50 | 2/2/2 | **33%** |
| 11 | Progression & rewards | 1/2/4 | **14%** | 43% | −29 | 3/3/1 | **43%** |
| | **Overall** | **21/26/19** | **32% (21/66)** | **39% (26/66)** | **−7 pts** | **40/19/11** | **57% (40/70)** |

Reading the scores:
- **The board-match drop is mostly by design.** v2 follows the approved spec where the board conflicts with it (C-02 power-ups and slots, C-03/C-10 launcher, C-07 hard level, C-04/C-13 seasons, C-06/C-14 login).
- **Excluding conflict-bound elements** (9 elements: P1#5, P3#1, P4#5, P5#5, P7#2/#4, P9#1/#4, P10#1), board match is **37% (21/57)**.
- **Painted fidelity is far closer to the board than v1:** glossy 3D, gold-trim navy panels, floating islands, rim light. The remaining gaps are mostly layout and composition (e.g. the board's level-select arrows, the vertical power-up rail, the 6-icon progression hub, the map legend).
- There is no v1 baseline for spec compliance; this is its first scoring.

## Per-screen checklists
### 01 Login (`01_login.png`)
**Board (8):**
- P: logo framed by a scenic background (C-01 lockup); illustrated background.
- D: Google, Facebook and X are all identical white pills with **no logo**. The board has a white Google pill with the G mark, a blue Facebook pill and a black X pill, each with its logo.
- D: Email is a blue pill without a mail icon.
- M: tagline; Instagram (C-06).

**Spec (4):**
- P: Guest present (C-14 conflict with the board, awaiting the user); Instagram correctly not shown; ToS and Privacy links.
- **M: official provider logos** (QA-VIS: Google "G", Facebook "f", X logo, each per its brand guidelines). This is the one required exception to board reuse.

KNOWN: art shifted down 70 px for the notch.

### 02 Create profile
**Board (5):**
- P: name field.
- D: "CREATE PROFILE" instead of "Create Your Hero"; a 6-avatar grid instead of a carousel with ‹ › and 3 visible; a single "ROOKIE" badge instead of a row of 4 choices (C-12); orange Continue instead of green.

**Spec (5):**
- P: avatar choice; name field; back button; coin and gem pills (currency packs are allowed in the store).
- D: the badge isn't in DP (C-12).
- Name validation states are not mocked (empty, too long, profanity).

### 03 World map
**Board (8):**
- P: numbered nodes on a winding road; landmarks; Daily Boss destination; Next Season padlock island.
- D: "SUNNYVILLE" sign at the bottom instead of a "SEASON 1" ribbon top-left (C-13); every node shows 3★ and no current node is highlighted.
- M: hard or boss node marked distinctly (node 10 looks like the others); chests on the map (only the nav-bar chest).

**Spec (7):**
- P: Daily Titan entry; region name Sunnyville (DP).
- **D: 12 nodes, but Chapter 1 has 10 levels** (`chapter1.json`).
- **D: node order is not readable.** The patched "4" (KNOWN patch) sits beside 7–8, after 6, so the path reads 1-2-3-5-6-4-7.
- D: all nodes are 3★, including the yet-to-play 11–12 (KNOWN stars).
- D: a "Next Season" island on the chapter map (C-13).
- **M: L10 Guardian boss node not distinct** (DP §1).

### 03b Map features legend
No v2 screen, so all 6 legend rows are M.

### 04 Level select
**Board (7):**
- P: "LEVEL 7" header with the 3-star row; diorama preview.
- D: no "Select Power Ups" label (slot labels CORE/MODULE/BOOSTER instead); 3 typed slots instead of 4 (C-02); orange PLAY instead of green.
- M: ‹ › level arrows; count badges.

**Spec (8):**
- P: Goal 85%; 3 typed slots Core/Module/Booster; empty stars before play; close X; PLAY.
- **M: star bands (shots left needed for 3★/2★); the level's shot budget; the mastery goal** (DP briefing; old UX-12 now just missing).

KNOWN: stars greyed and "85%" re-texted (hand patch).

### 05 Gameplay
**Board (5):**
- P: % bar.
- D: "SHOTS 3" box at top-right of centre, with SCORE at top-left (board: Steps Left box top-left, C-03); bottom row of 3 round power-ups instead of a vertical rail; pause top-right instead of bottom-left; side-on Wrecker bottom-left instead of a behind-camera launcher (C-10).

**Spec (10):**
- P:
  - no power meter (drag to aim, dotted arc)
  - shots-left counter
  - % bar without star markers (old UX-14 closed)
  - "COMBO x3" (shown from x3)
  - pause
  - Wrecker bottom-left per the art bible
  - no crates or energy cores in Ch1
- **D: "FREE HINT" carries a video/film icon,** which implies a rewarded ad. ADD-GA: the hint is free, offered from the 2nd consecutive fail, and never in ranked. It's shown mid-run in the HUD with no fail-state context.
- **D: a live SCORE (1,240) mid-run.** ADD §3: campaign score is win-only, computed at the end. Needs GD to confirm a live score is allowed.
- **D: every power-up shows a "2" charge badge.** DP §2 gives 1 charge per level (Chain Reactor 2); same as old UX-15.

KNOWN: HUD top row at y≈85 px is inside the notch zone; Shockwave icon hand-patched.

### 06 Level complete
**Board (4):**
- P: 3 large stars (2 lit); Next button.
- D: "LEVEL COMPLETE!" without the level number (board: "Level N Completed!"); rewards panel shows score, shots left and +250 coins instead of 3 reward tiles (C-09).

**Spec (5):**
- P: 2★ with 2 shots left (2/4 = 0.50, so 2★: old UX-17 closed); shots left shown; NEXT and MAP; no paid continue.
- **D: SC-12, see below.**

### 07 Hard level (every 10th)
**Board (4):**
- P: dark fortress with red windows.
- D: pre-fight modal (Shots/Goal) instead of the in-fight Steps Left box.
- M: % bar; bottom power-up row.

**Spec (6):**
- P: close; PLAY; empty stars.
- **D: "HARD LEVEL 10" shows a generic fortress,** but L10 is the Tidewarden Guardian boss with phases (C-07, awaiting the user).
- **D: "SHOTS: 5", but L10 has 7 shots** (`chapter1.json` c1_l10).
- **D: "GOAL: 85%", but the boss win is defeating the guardian,** not 85% (`GameSession.cs:124`, `Boss.Defeated`; `IsBoss`).

KNOWN: "85%" re-text patch.

### 08 Daily boss
**Board (4):**
- P: red HP bar; large boss; fiery arena.
- M: steps-left box (v2 is the pre-fight screen; the board is in-fight).

**Spec (7):**
- P: back; reset countdown; reward chest (top-10% bonus); FIGHT.
- D: HP bar (C-08: score = damage; awaiting the user).
- **M: "1 ranked attempt + practice" state and its choice. This regresses Art v1 08, which had it.**
- **M: shot budget and rules line** (DP §3 config).

KNOWN: back button patched down for the notch.

### 09 Power-ups
**Board (4):**
- P: icon art.
- D: 2×3 grid instead of 4×2; names only, with no one-line effect; 6 power-ups instead of 8 (C-02).

**Spec (5):**
- P: the real 6 DP power-ups; no prices or shop; back.
- **D: slot colour tags are wrong for 2 of 6.** Splitstrike is drawn cyan (Core) but is **Module**. Chain Reactor is drawn violet (Module) but is **Core** (`powerups.json`).
- M: effect line and unlock source ("earned by playing"; Art v1 had it).

### 10 Seasons
**Board (4):**
- P: vertical list of season banners; per-season themed art; locked and current states.
- D: header has no "new theme every week" subtitle (C-04).

**Spec (6):**
- P: back; locked states.
- **D: seasons are drawn as playable regions with PLAY.** DP: a season is a 4-week live-ops track with weekly events, and regions are chapters (C-04/C-13).
- D: names Clockwork, Frostpeak and Tidewarden are neither DP regions (Gearstorm/Neon/Crystal/Titan's Reach) nor seasons; Tidewarden is the L10 boss.
- M: tier, XP and premium (cosmetic) track; season end date and time.

**C-05 resolved for art:** no "Jurassic World" name is used.

### 11 Progression & rewards
**Board (7):**
- P: daily login rewards.
- D: star milestones (chests, not "unlock skins & buildings"); a single rewards panel instead of the 6-icon hub.
- M: Level Rewards; Season Pass; Leaderboards; Achievements tiles.

**Spec (7):**
- P: Day 1–7 track; close; CLAIM.
- **D: rewards don't match DP §3 / `daily_rewards.json`.**
  - Art shows 500 coins, **10 gems**, chest, 1000, **20 gems**, chest, chest.
  - DP has 100 coins, +1 charge, 150, shard, 200, +1 charge, chest (coins 300 + 3 shards).
  - **Gems are premium currency, sold in the store only** (C-09, ADD §4). This regresses Art v1 11, which was correct.
- D: "LV 12, XP 2560/4000" doesn't match the season XP model (tiers 100/150/200, 4,500 total).
- D: star milestones at 30/60/90, but Chapter 1 maxes at 30★ (spec gap, needs GD: SG-37).
- M: the "a missed day pauses, never resets" note (GA SG-14; Art v1 had it).

## SC-12 re-check (score placeholder): FAIL, reopened
- 06 shows **SCORE 3,850** with 2 shots left and 2★. The PROMPTS note says it's the L2 win.
- L2 has 4 shots and 9 pieces (`Layouts/c1_l02_stilt_tower.json`), so the maximum is 1000 (100%) + 500×2 + 50×9 + 1000 (mastery) = **3,450**. That makes 3,850 impossible for L2.
- With 2 shots left, 3,850 is reachable only on c1_l07 (25 pieces, max 4,250).
- **QA error, owned:** Feature_Test_Cases_v1.2 SC-12 suggested "3,850 = 3,500 + x7", which assumed 3 shots left. With 2 left the base is 3,000, not 3,500. I'll correct that row.
- **Suggested value:** L2, 100%, 2 left, combo x5, mastery gives 1000 + 1000 + 250 + 1000 = **3,250**. Without mastery it's **2,250**. Alternatively, keep 3,850 but label the screen L7.

## Art v1 UX tickets: re-check on v2
| Ticket | v2 status |
|---|---|
| UX-01 text size | Mostly fixed: large text throughout. Still small: login footer "Terms of Service · Privacy Policy" ≈30 px (10 sp < 12 sp) and the 11 day-labels. **Partly open** |
| UX-02 tap targets ≥48 dp (144 px) | Needs measurement on the final layout. Visually: map nodes ≈128 px, close X ≈128 px, nav-bar icons ≈150 px. **Open (measure)** |
| UX-03 safe area | **KNOWN** (Art top-shift patches). Recheck on a 20:9 cutout template |
| UX-04 contrast | Improved: dark strokes on button labels. White on orange PLAY/CONTINUE, measured visually, is borderline. **Open (measure)** |
| UX-05 back/close everywhere | **Closed**: present on 02, 04, 07, 08, 09, 10, 11 |
| UX-06 extra aspect ratios | **Open** (9:16 only) |
| UX-07 watermark/footer | **Closed** (no watermark) |
| UX-08 map label overlap | **Closed** |
| UX-09 star gate | **Closed** (no gate shown) |
| UX-10 island names | **Changed**: map shows Sunnyville (DP ✔); seasons screen uses non-DP names (see 10) |
| UX-11 L8 data | n/a (no L8 shown) |
| UX-12 star bands | Wrong bands are gone, but bands are now **missing** (04) |
| UX-13 pause overlap | **Closed** |
| UX-14 star markers on % bar | **Closed** |
| UX-15 charge counts | **Open** (every tile "2") |
| UX-16 card overflow | **Closed** |
| UX-17 3★ with 2 left | **Closed** (2★) |
| UX-18 / SC-12 score | **Open** (3,850 impossible for L2) |
| UX-19/20 fail screen 80%, 1★ | **Unverified**: fail screen 12 not generated (PROMPTS spec looks right) |
| UX-21 Titan overflow | **Closed**, but ranked/practice was lost (08) |
| UX-22 event icons | n/a (no event hub screen) |
| UX-23 shard vs gem icon | **Open/worse**: rewards show gems where DP has shards |
| UX-24 streak count | n/a |
| UX-25/26 Tidewarden arena | **Unverified** (no arena screen in v2) |

## New issues (v2)
| ID | Sev | Screen | Issue |
|---|---|---|---|
| UX-27 | High | 07 | L10 shown as a generic "Hard Level" with 5 shots and an 85% goal. Data: Tidewarden boss, 7 shots, win = guardian defeated (conflict C-07 also pending) |
| UX-28 | High | 06 | SC-12: 3,850 impossible for L2 (max 3,450) |
| UX-29 | High | 01 | No official provider logos on Google, Facebook and X (the required exception) |
| UX-30 | Med | 09 | Wrong slot colour for Splitstrike (Module) and Chain Reactor (Core) |
| UX-31 | Med | 05 | FREE HINT shows a video/ad icon and appears mid-run; the hint is free and offered from the 2nd consecutive fail |
| UX-32 | Med | 11 | Daily rewards amounts and gems don't match DP (gems are store-only); XP model mismatch; "pauses, never resets" note missing |
| UX-33 | Med | 08 | Ranked (1 attempt) and practice choice, shot budget and rules missing (regression from v1) |
| UX-34 | Med | 03 | 12 nodes vs 10 levels; L10 not distinct; node order unreadable after the "4" patch; all 3★ / no current node |
| UX-35 | Med | 04 | Star bands, shot budget and mastery goal missing |
| UX-36 | Med | 10 | Seasons drawn as playable regions with non-DP names; no tiers, premium (cosmetic) track or end date (C-04/C-13 pending) |
| UX-37 | Low | 05 | Live score mid-run; GD to confirm (ADD §3 win-only) |
| UX-38 | Low | 06 | No level number on the complete screen |
| UX-39 | Low | 09 | Effect line and "earned by playing" note missing |
| SG-37 | Gap | 11 | Star milestones 30/60/90 vs a 30★ Chapter 1 maximum: define the milestones |

## §C Board vs spec conflicts: still awaiting the user (QA has not chosen)
**C-01 to C-14 are unchanged from v1 §C and still open.** v2 follows the approved spec on each one:

| ID | v2 follows |
|---|---|
| C-01 | CRASHVERSE lockup |
| C-02 | 6 power-ups / 3 typed slots |
| C-03 | "SHOTS" |
| C-04 / C-13 | Seasons rendered as regions, which matches neither the board's weekly themes nor DP's live-ops season (see UX-36) |
| C-06 / C-14 | Guest present, no Instagram |
| C-07 | Generic "Hard Level", which matches the board but conflicts with DP's Tidewarden (UX-27) |
| C-08 | HP bar, which matches the board and still conflicts with DP's score = damage |
| C-09 | Coins only on the complete screen; but 11 adds gems as daily rewards |
| C-10 | Side-on Wrecker |
| C-11 | No Special Events legend |
| C-12 | Badge present (board-aligned) |

C-05: v2 uses no "Jurassic World" name; the legal flag remains for any board-name adoption.
VM-04/10/11 proposal from v1: still awaiting PM.
