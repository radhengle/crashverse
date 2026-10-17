# CRASHVERSE — Reference Screen Parity v1 (QA) — 2026-10-16 IST
Target: the QA owner's **own reference board** (`…/attachments/47da2a84…cabb7.png`, 1312×1199, 11 panels + map legend). Per the user it is their original design and the **exact visual target**: layout, elements, styling and polish. **Exception:** social login buttons must use each provider's official logo files, following their brand guidelines. Where the board conflicts with the approved design pack, the conflict is logged for PM (§C) and **not resolved by QA**.
Compared against: Art v1 mockups `/workspace/art/v1/mockups/01–12` (viewed). Build 0ccfa9b screen inventory is noted per panel.
Scoring: each required element counts as **P** (present and matching), **D** (divergent: present but a different layout/style/content) or **M** (missing). **Parity = P / total.** Positions use a 3×3 grid (T/M/B × L/C/R) of the panel.
Panel baselines for visual regression: `/workspace/qa_automation/visual/baselines/` (cropped from the board).

## Scores
| Panel | Art v1 mockup | P | D | M | Parity | Build 0ccfa9b |
|---|---|---|---|---|---|---|
| P1 Login | 01_login | 3 | 2 | 3 | **38%** | no login screen |
| P2 Create profile | — | 0 | 0 | 5 | **0%** | no profile screen |
| P3 World map | 02_world_map | 4 | 3 | 1 | **50%** | MapScreen (3D island) |
| P3b Map features legend | — (UI kit node states only) | 0 | 1 | 5 | **0%** | none |
| P4 Level select | 03_level_start | 4 | 2 | 1 | **57%** | BriefingScreen |
| P5 Gameplay | 04_ingame_hud | 2 | 3 | 0 | **40%** | HudScreen |
| P6 Level complete | 06_win (+07 fail) | 3 | 1 | 0 | **75%** | ResultsScreen |
| P7 Hard level (every 10th) | 12_tidewarden_arena | 2 | 1 | 1 | **50%** | L10 boss level |
| P8 Daily boss | 08_daily_titan | 2 | 0 | 2 | **50%** | TitanScreen (pre-fight only) |
| P9 Power-ups grid | 05_powerup_select | 2 | 2 | 0 | **50%** | inside BriefingScreen |
| P10 Seasons list | 09_season | 1 | 1 | 2 | **25%** | SeasonScreen |
| P11 Progression & rewards | 06/09/10/11 spread | 3 | 1 | 3 | **43%** | DailyRewards/Leaderboard/Season/Store; no hub |
| **Overall** | | **26** | **17** | **23** | **39% (26/66)** | |

## Per-panel checklists (required element · region · states · navigation) and findings
### P1 Login & sign-up
| # | Required (board) | Region | States / nav | Art v1 |
|---|---|---|---|---|
| 1 | Game logo, large, framed by a scenic background | T-C, ~35% height | static | P (CRASHVERSE lockup, C-01) |
| 2 | Tagline under the logo | T-C | — | P |
| 3 | Continue with Google: white pill, official G logo left | M-C | normal/pressed/loading/error | D: "Provider A" placeholder |
| 4 | Continue with Facebook: blue pill, official logo | M-C | same | D: "Provider B" |
| 5 | Continue with Instagram: gradient pill, official logo | M-C | same (C-06) | M |
| 6 | Continue with X: black pill, official logo | B-C | same | M |
| 7 | Create with Email: dark pill, mail icon | B-C | → email form | M |
| 8 | Full-bleed illustrated background, warm sunset | full | — | P (sky + island) |
Measurable: 5 stacked full-width buttons ≥48dp tall with ≥8dp gaps; logos per provider guidelines; back = exit app confirm. Art v1 adds Guest + Create Profile (board has no guest button: C-14).
### P2 Create profile
1 "Create Your Hero" title (T-C) · 2 avatar carousel with ‹ › arrows, 3 visible, selected highlighted (T/M-C) · 3 Player Name field with placeholder (M-C) · 4 "Choose Your Badge" row of 4 badges, selected state (M-C) · 5 green Continue (B-C). Nav: Continue → map; back → login; name validation (empty/too long/profanity) states. **Art v1: none, 0/5.** Build: none.
### P3 World map
1 Season title ribbon "SEASON 1 · <name>" (T-L) — **D** (v1 "SUNNYVILLE ISLES" centre ribbon) · 2 numbered round nodes on a winding road (full) — P · 3 completed nodes with stars, current node highlighted — P · 4 landmark buildings/castles between nodes — P · 5 **Daily Boss** destination on the map with a skull icon and label (T-R) — **D** (v1 uses a side TITAN button) · 6 **Next Season** padlock destination (B-R) — **M** · 7 boss/hard node marked distinctly (red/skull) — P (L10 red node) · 8 chests on the map — **D** (v1 star-chest bar at the bottom). Nav: tap node → level select; tap daily boss → P8; locked node shows its requirement; pan/zoom limits.
### P3b Map features legend (6 rows: icon + title + subtitle)
Regular Levels (1–9) · Hard Level (every 10th) · Daily Boss Level (changes every day) · Season Progress (new theme every week, C-04) · Chests & Rewards (collected on the map) · Special Events (timed challenges, C-11). **Art v1: no legend screen; node icons only in the UI kit (1 D, 5 M).**
### P4 Level selection
1 "Level N" header + 3-star row (T-C) — **D** (v1 orange ribbon; star bands as text, no 3-star icon row) · 2 island diorama preview (M-C) — P · 3 ‹ › arrows to browse levels (M-L/M-R) — **M** · 4 "Select Power Ups" label — P · 5 **4** power-up slots with count badges (B-C) — **D** (v1 has 3 typed slots, C-02) · 6 count badges on slots — P · 7 big green Play (B-C) — P. Nav: back → map; arrows change level; locked level shows its requirement.
### P5 Gameplay
1 "Steps Left N" box (T-L) — P as "SHOTS LEFT" (wording C-03) · 2 % destroyed bar (T-C/R) — P · 3 vertical power-up rail with icons (M-R) — **D** (v1: 2 stacked tiles at the bottom right) · 4 pause button (B-L) — **D** (v1 at T-L, overlapping the label: UX-13) · 5 launcher seen from behind, bottom centre, aiming into the screen (B-C) — **D** (v1 side-on Wrecker at B-L; C-10). Must not cover the structure. Back = pause.
### P6 Level complete
1 three large gold stars (T-C) — P · 2 "Level N Completed!" (M-C) — **D** ("VICTORY!" + level name) · 3 rewards panel with 3 tiles (+coins / +gems / +chest) (M-C) — P (coins/gems/stars; C-09) · 4 green "Next Level" (B-C) — P. Fail variant: board has none; v1 07 provides it (Retry/Map).
### P7 Hard level (every 10th)
1 Steps-left box (T-L) — P (shots) · 2 % bar (T-R) — **D** (v1 phase/joints panel) · 3 dark, burning fortress structure (centre) — P · 4 bottom power-up row, 4 tiles with counts (B-C) — **M**. Conflict C-07 (generic hard level vs Guardian boss).
### P8 Daily boss (in-fight)
1 Steps-left box (T-L) — **M** (v1 08 is the pre-fight screen) · 2 red **Boss HP** bar with skull (T-C) — **M** (C-08) · 3 large boss character, centre — P · 4 fiery arena — P. Daily-change label in the header.
### P9 Power-ups grid
1 4×2 grid of square icon tiles — **D** (v1 2×3 cards) · 2 name + one-line effect under each tile — P · 3 icon art per power-up — P · 4 **8** power-ups — **D** (DP has 6: C-02).
### P10 Seasons
1 header "Seasons (new theme every week)" — P (v1 "THE WRECKING FRONTIER") · 2 vertical list of season banners "Season N + name" with themed art — **M** (v1 shows the 4 weekly events horizontally) · 3 per-season art strip — **M** · 4 locked/current states — **D**.
### P11 Progression & rewards (hub of 6 icons)
Level Rewards — D (only on the win screen) · Star Collection (unlock skins & buildings) — **M** · Season Pass — P (09 premium track) · Leaderboards (compete with friends) — P (10) · Daily Login Rewards — P (11) · Achievements — **M** · the hub grid itself — **M**.

## Polish/styling notes vs board (applies to all panels)
Board style: painterly, high-detail 3D renders, dense props, strong rim light, glossy icons with thick outlines. Art v1 is flat/vector with simpler geometry, so even "P" items are lower fidelity. That gap is expected to close with Art's painted v2; re-score then. Visual-regression tolerances are in `qa_automation/visual/panels.json`.

## §C Conflicts with the approved design (for PM — QA has not chosen)
| ID | Board | Approved design | Ref |
|---|---|---|---|
| C-01 | Title "SMASH CITY", tagline "Build. Aim. Smash. Conquer." | CRASHVERSE lockup, "BUILD · AIM · DESTROY" | art_bible_v1 lockup |
| C-02 | 8 power-ups (Mega Bomb, Multi Shot, Guided Rocket, Time Freeze, Shield, Earthquake, Laser Beam, Black Hole); 4 untyped slots | 6 power-ups (Shockwave, Splitstrike, Gravity Well, Chain Reactor, Phase Piercer, Time Fracture); 3 typed slots Core/Module/Booster | DP §2 |
| C-03 | "Steps Left" | "Shots" in UI and data | GA SG-02 |
| C-04 | Seasons: "New Theme Every Week"; list Sunny Shores / Frozen Peaks / Neon City / Jurassic World | Weekly event inside a 4-week season (Magnetic Mayhem, Seismic Surge, Gravity Games, Sonic Rupture); regions Sunnyville / Gearstorm / Neon / Crystal / Titan's Reach | DP §3, master §5 |
| C-05 | Season name "Jurassic World" (renamed to **Primeval Peaks**, design/Art_v2_Decisions_v1.md) | — | **Legal flag:** a third-party film trademark regardless of board ownership; needs PM/legal review |
| C-06 | Login with Google, Facebook, Instagram, X, Email (no guest) | Guest play required; Instagram is a separate feasibility item; official provider assets | master §11, DP |
| C-07 | Every 10th level = "Hard Level" | L10 = Guardian boss (Tidewarden) with phases | DP §1 |
| C-08 | Daily boss shows a Boss HP bar, "Steps Left 8" | Daily Titan: score = damage; 1 ranked + practice; rules readable without hidden HP math (Tidewarden) | DP §1, §3 |
| C-09 | Rewards +coins / +gems / +chest per level | Coins/shards/charges; gems (premium currency) only via store | DP §3, ADD §4 |
| C-10 | Launcher behind-camera view, bottom centre | Wrecker framed bottom-left ~20%, side 3/4 | wrecker_brief, art bible |
| C-11 | Legend "Special Events (Timed Challenges)" | Not in DP (only weekly events) | DP §3 |
| C-12 | Profile "Choose Your Badge" | Not in DP/master profile list (avatar, frames, titles exist) | master §11 |
| C-13 | Map title "Season 1" | Map is per region/chapter; seasons are a separate live-ops track | DP §1, §3 |
| C-14 | No guest button on login | Guest play required | master §11 |

## Proposal: VM-04 / VM-10 / VM-11
The user has stated the reference board is their own original design, not a third-party asset. QA proposes that PM **reopen VM-04, VM-10 and VM-11 and close them as "user-owned design, not a copy"**:
- VM-10 (leaderboard names matching the board) and VM-11 (icons resembling the board's power-ups) are no longer originality issues.
- VM-04: the do-not-ship README (`/workspace/art/_reference_DO_NOT_SHIP/README.md`) says "licence unknown". It should be updated to say user-owned.
- **Caveats to confirm:**
  - The earlier boards in that folder were titled "SMASH QUEST"; the user's board here is "SMASH CITY". PM should confirm both sets are the user's.
  - Social login logos are still the exception: official provider assets only.
  - The "Jurassic World" name (C-05) is outside this ownership.
