# CRASHVERSE — Design Pack v1 (Chapter 1, Power-ups, Live Ops)
Owner: Game designer · Status: draft for PM approval · Compatible with M1 contract v0.1
All names, bosses and content are original to CRASHVERSE. Reference boards inform feel only (floating islands, numbered nodes, cannon vs. towers, power-up tiles, fiery daily boss) — no copied layouts, characters or art.

M1 compatibility: every Ch1 level uses the M1 rules (DESTROY_STRUCTURE ≥85% objective mass, shot budget, stars from shots remaining, clean retry). New levels change only data: shot budget, materials, layout, extra optional mastery goal. Star bands scale with budget: 3★ = ≥ 60% shots left, 2★ = ≥ 30%, 1★ = any win (M1's 5-shot table matches this).

---
## 1. Chapter 1 — Sunnyville Isles (L1–10)
Map: a chain of 4 floating islands linked by rope bridges and a sky-tram. Numbered nodes run along a single winding path; bridges are the region gates. Completed nodes show stars; L10 sits on its own storm-ringed island.

| L | Island | Structure | Materials | Shots | New thing taught | Mastery goal (optional) |
|---|---|---|---|---|---|---|
| 1 | Harbor Isle | Wooden lookout hut | wood | 4 | Aim + fire, trajectory arc | Clear in 1 shot |
| 2 | Harbor Isle | Stilt water tower | wood | 4 | Hit supports, not walls | Topple tank into sea |
| 3 | Harbor Isle | Brick bakery + wood props | wood, brick | 5 | Brick needs support loss | ≥95% destroyed |
| 4 | Mill Isle | Windmill (rotating sails, static hub) | wood, brick | 5 | Collapse chains between pieces | 3★ with no wall hits |
| 5 | Mill Isle | Twin cottages on one bridge | wood, brick | 5 | One shot, two targets (bridge) | Both in 1 shot |
| 6 | Mill Isle | Granary with steel braces | brick, steel | 5 | Steel joints take 2× damage | Break all 3 joints |
| 7 | Clocktower Isle | Clockwork Tower (M1 greybox level) | wood, brick, steel | 5 | Joint sequencing; first power-up pick (Shockwave) | Crown falls intact |
| 8 | Clocktower Isle | Market arcade (long span) | all 3 | 5 | Choose loadout deliberately (Splitstrike) | 3★ |
| 9 | Clocktower Isle | Gauntlet: lighthouse + seawall | all 3 | 6 | Tests everything; tight budget | No power-up used |
| 10 | Storm Isle | Guardian: **The Tidewarden** | all 3 | 7 | Boss phases + weak points | Defeat with 3 shots left |

L7 reuses M1 Clockwork Tower so the greybox ships straight into Ch1.

### L10 Guardian — The Tidewarden (original)
A lighthouse-sized brick-and-steel automaton bolted onto a sea-stack, keeper of Sunnyville's harbor.
- Phase 1 (Shell): wood plank armor over 2 steel shoulder joints. Break both joints → arms drop, chest opens.
- Phase 2 (Lantern core): glowing core in chest, shielded every other turn by a rotating brick shutter (visible tell: shutter glows orange 1s before closing). Hit core 2× when open.
- Win: core destroyed → collapse cinematic. Fail: shots run out.
- Rules readable from animation only; no hidden HP math.
- Reward: Chapter chest (coins + Gravity Well unlock + Gearstorm City bridge opens).

Ch1 acceptance criteria:
1. Chapter defined as data (10 level entries + boss entry), no per-level code.
2. Each level loads with its shot budget/materials from the table; stars by % shots left bands.
3. Nodes unlock linearly; bridge to next island opens after previous island's last node; L10 needs L9 cleared.
4. Boss has 2 phases with visible tell before shutter closes; phase change on both shoulder joints broken.
5. All completed nodes replayable; best stars persist.

---
## 2. Power-up spec v1
Six power-ups, each a tile in the pre-level picker. Charges are per level; power-up use does NOT cost a shot (it modifies the next shot), but using any power-up blocks 3★ on levels with the mastery goal "No power-up used".

| # | Power-up | Slot | Effect | Charges/level | Unlock |
|---|---|---|---|---|---|
| 1 | Shockwave Core | Core | On impact, deals 40 dmg to every piece connected to the hit piece (1 support link deep) | 1 | L7 |
| 2 | Splitstrike | Module | Shot splits into 3 projectiles at apex, each 50% damage | 1 | L8 |
| 3 | Gravity Well | Module | Pulls loose debris within 4m for 1.5s, then releases as a burst (60 dmg) | 1 | L10 reward |
| 4 | Chain Reactor | Core | Detonates explosive crates/energy cores within 3m of impact | 2 | Gearstorm L3 |
| 5 | Phase Piercer | Booster | Passes through first 2 pieces, full damage to third | 1 | Gearstorm L7 |
| 6 | Time Fracture | Booster | Slows moving parts to 25% for 4s for precise aim | 1 | Neon L3 |

Picking before a level:
- Briefing screen shows 3 loadout slots: Core, Module, Booster. Player drops at most one unlocked tile per slot (max 3 power-ups/level).
- Level data may lock a slot or force a tile (teaching levels L7, L8).
- Locked tiles show silhouette + "Unlocks at …".
- Power-ups are earned, never bought for campaign progress. Extra charges may come from daily rewards (max +1 per level).
- Valid combos only: one core, one module, one booster. No two cores.

Acceptance: tiles appear in unlock order; picker enforces 1 per slot; charge decrements on use; effect values come from a data asset; retry restores charges.

---
## 3. Daily boss, daily rewards, seasons, leaderboard

### Daily Titan — "Emberforge Titan"
An original molten-iron colossus; one fixed attempt config per UTC day.
- Daily config (server JSON): archetype, arena, material layout, special rule (e.g. "steel is heated: joints take 3×"), shot budget, mastery goal, rewards.
- Reset: 00:00 UTC (05:30 IST). Same config for every player.
- Attempts: 1 ranked attempt + unlimited practice. Ranked result stored server-side; reinstall cannot reset it.
- Score = damage dealt (0–10,000) + 500 per shot left on kill. Leaderboard uses ranked score only.
- Handcrafted templates (start with 4 arenas × 6 rules), no free random generation.

### Daily rewards
7-day login calendar, loops weekly. Missing a day pauses (doesn't reset) the streak.
| Day | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Reward | 100 coins | 1 extra charge | 150 coins | Cosmetic shard | 200 coins | 1 extra charge | Chest (coins + 3 shards) |
Daily Titan rewards: completion chest; top 10% bonus shards.

### Seasons — The Wrecking Frontier
- Weekly event (7 days) with one rule twist (Magnetic Mayhem, Seismic Surge, Gravity Games, Sonic Rupture) and 5 event levels.
- Season = 4 weekly events + finale level. 30-tier track fed by event stars + Titan clears. Free track always; optional cosmetic premium track — cosmetics only, never power or campaign access.
- Expired seasons never remove owned items or campaign content. All schedules/rewards are server-configurable.

### Leaderboards
- Boards: Daily Titan (resets daily), Weekly Event (resets weekly), Season total (resets per season), Friends filter.
- Show top 100 + player's rank. Ties broken by earlier submission time.
- Only server-validated ranked runs count; guest players can play but must link an account to appear.

Live-ops acceptance: daily config swaps at 00:00 UTC without a build; completed ranked attempt persists across reinstall; streak pauses not resets; season tiers advance from stars; leaderboard rejects unvalidated scores.

---
## Screens Art & UI needs (gameplay requirements, not visual direction)
1. Island world map: numbered nodes, star counts, locked/unlocked states, bridge gates, boss island, Daily Titan + Event entry points.
2. Level briefing with 3-slot power-up picker (Core/Module/Booster), locked tile states, shot budget, star bands.
3. In-level HUD: shots left, % destroyed, active power-up charges.
4. Results: stars, mastery goal tick, retry/next.
5. Tidewarden boss arena + phase tell (shutter glow).
6. Daily Titan screen: today's rule, ranked/practice buttons, timer to reset.
7. Daily reward calendar (7 tiles).
8. Season track (30 tiers, free + premium rows) and weekly event hub.
9. Leaderboard (daily/weekly/season/friends tabs).
