# Store listing draft (en-US)
Based on README, chapter1.json, powerups.json, store_catalog.json and season_s1.json. Final copy needs PM sign-off.

## Title (max 30)
`CRASHVERSE: Smash Puzzle` (24 characters). Alternative: `CRASHVERSE` (10).

## Short description (max 80)
`Aim, fire and topple wild 3D structures. Smash smart, earn stars, beat bosses!` (78)

## Full description (max 4000), draft
```
Line up the shot. Hit FIRE. Watch it all come crashing down.

CRASHVERSE is a 3D physics destruction puzzle game. Drag to aim your Wrecker cannon, fire, and bring down every structure with as few shots as you can. Fewer shots mean more stars.

SUNNYVILLE ISLES – CHAPTER 1
Sail across Harbor Isle, Mill Isle, Clocktower Isle and Storm Isle, then wreck 10 handcrafted levels: the Wooden Lookout Hut, Stilt Water Tower, Brick Bakery, Windmill, Twin Cottages, Steel-Braced Granary, Clockwork Tower, Market Arcade and Lighthouse Gauntlet. Then face the boss, The Tidewarden.

6 POWER-UPS
Shockwave Core, Splitstrike, Gravity Well, Chain Reactor, Phase Piercer and Time Fracture. Pick the right one and turn a tough tower into a chain-reaction collapse.

REAL PHYSICS, REAL CHAOS
Wood, brick and steel break differently. Every collapse is simulated, so no two wrecks look the same.

EMBERFORGE DAILY TITAN
A new Titan challenge every day. Deal as much damage as you can and climb the leaderboard.

REWARDS AND SEASONS
7-day login rewards, a 4-week season track and cosmetic Wreckers and trails you unlock with coins and gems earned in play.

COMFORT SETTINGS
Reduce shake and Reduce motion options.

CRASHVERSE is free to play, with no ads and no in-app purchases.
```
OPEN QUESTIONS: the Daily Titan leaderboard, season, store and IAP all need the live backend, which is offline (see Release_Plan_v1.md). Remove those lines if they aren't live at launch. Does a "Gearstorm City" teaser (`nextRegion`) belong in the listing? Confirm the "no ads / no in-app purchases" claims with the PM.

## Graphic assets
| Asset | Spec | Source / need from Art & UI |
|---|---|---|
| App icon | 512×512 PNG, 32-bit, ≤1 MB, full square (Play masks it) | **Missing.** Needed: Wrecker or CRASHVERSE lockup icon. Also the Android adaptive launcher icon (foreground/background layers) in Unity PlayerSettings |
| Feature graphic | 1024×500 JPG/24-bit PNG, no alpha | **Missing.** Can be built from `art/v2` key art (Sunnyville arena + Wrecker). Keep text away from the edges |
| Phone screenshots | 2–8, PNG/JPEG, 16:9 or 9:16, each side 320–3840 px. 4+ at ≥1080 px are recommended for promotion | `/workspace/art/v2/screens/*.png` are 1080×1920 (9:16). **These are painted visual targets, not in-game captures.** Play policy requires screenshots that represent the real app, so final screenshots should come from a device build. See the plan in Release_Plan_v1.md |
| Video (optional) | YouTube URL | Optional |
| 7" / 10" tablet screenshots | Optional | Optional |

## Rev 1.1 (PM)
v1 has no real-money purchases, so do **not** tick "Contains in-app purchases" and set Pricing = Free. Gems are earned only. Leaderboard/Daily Titan ranking lines depend on hosting being live at production.
