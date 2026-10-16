# Design Addendum v1.1: Free Hint, Combo, Campaign Score, Store scope
Owner: Game designer · Extends Design Pack v1 and M1 contract v0.2 · Values are tunable data

## 1. Free Hint (fail screen)
- When it appears: on the fail screen of any campaign level, after the 2nd fail in a row on that level and on every fail after that. It resets when the level is cleared. It's never shown on Daily Titan ranked runs.
- Cost: free. No ads, currency or timers. One tap.
- What it shows: on the next attempt's first aim, the best weak point (the level data's `hintTarget` piece) pulses and a ghost aim arc is drawn for the suggested first shot. It stays up until the first shot is fired, then disappears.
- Effect on scoring: none. Stars, mastery and score work as normal. It doesn't change the 3★ rules.
- Data: each level stores `hintTarget` and `hintAim` (yaw and pitch). A level with no hint data never shows the button.
- Acceptance: the button appears only from the 2nd fail in a row; using it shows the pulse and arc on the next attempt only; no ad or purchase step exists; stars are unaffected.

## 2. COMBO xN
- What counts: each ObjectivePiece that is destroyed (HP reaches 0) or pushed out of bounds adds +1 to the combo, as long as it happens within 0.8s of the previous one. The combo starts at the first break of a shot. If 0.8s pass with no break, it ends. The counter resets with each new shot, so combos never carry across shots.
- Display: "COMBO xN" shows from N ≥ 3, and the pop gets bigger at 5, 10 and 20.
- Rule: combos are feedback only, plus the campaign-score bonus below. They never change stars, mastery, win or fail.
- Acceptance: breaks 0.8s or less apart chain together, and a gap over 0.8s ends the combo; nothing shows below x3; stars are identical with or without combos.

## 3. Campaign score (display and leaderboard only)
Score on a win = 1,000 × % destroyed (0–1) + 500 × shots left + 50 × highest combo + 1,000 if mastery is ticked.
- A fail gives no score. The best score per level is kept. Total campaign score is the sum of the best scores of all levels, and it's used for profile display and an optional campaign leaderboard.
- Stars still come only from the % of shots left (SG-01). Score never affects stars, unlocks or rewards.
- The Daily Titan keeps its own score formula from Design Pack v1.

## 4. Store scope (VM-13)
Allowed item types:
1. Premium currency packs. These spend on cosmetics only.
2. Wrecker cosmetics: skins, paint and decals, projectile trails, muzzle and impact VFX skins.
3. Player avatars, frames and titles.
4. Map and island decorations (cosmetic).
5. The season premium track (cosmetic rewards only) and cosmetic bundles.
6. Optional emotes and victory animations.

Never sold, for real money or any currency:
- Power-ups or power-up charges
- Shots, extra shots or continues
- Level skips, unlocks, stars or campaign progress
- Daily Titan attempts or leaderboard advantages
- Equipment upgrades that change gameplay

Power-ups and charges only come from campaign unlocks and daily rewards. Coins can't buy gameplay. Every store item has to be flagged `cosmetic=true` in data, and the build rejects any item that isn't.
- Acceptance: every SKU is cosmetic or currency; no purchase anywhere changes shots, charges, stars or unlocks.

## Spec gap answers (QA v1.2, SG-25 to SG-33, SC-12)
- **SG-25** The fail streak is counted per level. Fails on other levels in between don't break it. Only clearing that level resets it.
- **SG-26** The hint shows on Weekly Event levels and in Daily Titan practice if the level has hint data. It never shows in Titan ranked runs.
- **SG-27** Yes. The counter is saved per level locally and survives an app kill or relaunch.
- **SG-28** Give Up counts as a fail. Quitting to the map in the middle of a level does not, and the counter stays where it was.
- **SG-29** The 0.8s window is measured from the previous break, and the first break of a shot starts the combo. Combos never extend the settle: whatever is still running when settle ends is final.
- **SG-30** One point. Each piece counts at most once per shot.
- **SG-31** Use the settled objective destroyed %, capped at 1.0. Score = floor(1000 × %) + 500 × shots left + 50 × highest combo + 1000 for mastery, all as integers. The highest combo counts even below x3 (when it isn't displayed), and a shot with no breaks counts as 0.
- **SG-32** The campaign leaderboard takes the sum of each level's best score from server-validated wins only. Practice runs and modes other than the campaign don't count. It's all-time with no reset. Ties go to whoever reached the total first (server timestamp). It shows the top 100, the player's own rank, and a friends filter.
- **SG-33** Coins buy cosmetics from the store's coin-priced items (some skins, trails, decor) and nothing else. Premium currency buys every cosmetic. Neither currency can buy anything from the never-sold list.
- **SC-12** The formula is correct and the art is wrong. On L2 (4 shots) the most a win can score is 1000 + 1500 + 50 × combo + 1000, which is about 3,500 plus the combo bonus, so 48,750 isn't possible. Art should use a realistic placeholder, for example 3,850.
