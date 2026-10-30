# CRASHVERSE UI art pack v2 (`/workspace/art/ui_v2/`)
Made 24 Oct 2026 (IST) by Art & UI for Developer. It's a style match for `crashverse/design/TARGET_UI_user_2026-10-24.png`. **All art is original:** Canva made it from text-only prompts. No pixels from the target or from `_reference_DO_NOT_SHIP` were uploaded, cropped, traced or reused, and no reference image was passed to Canva. The brand is CRASHVERSE. No placeholder names and no text are baked into any asset.

## Icons: 256x256 RGBA PNG, transparent, anti-aliased alpha
| File | What | Source |
|---|---|---|
| mega_bomb.png | Black bomb with gold bands and a sparking orange fuse | Canva sheet A |
| multi_shot.png | 3 cyan cannonballs fanning out | Canva sheet A |
| guided_rocket.png | Red/white rocket with a cyan reticle | Canva sheet A |
| time_freeze.png | Stopwatch frozen in ice | Canva sheet A |
| shield.png | Cyan shield in a hex energy dome | Canva sheet B |
| earthquake.png | Rock splitting with orange fissures | Canva sheet B |
| laser_beam.png | Gold/navy emitter firing a red beam | Canva sheet B |
| black_hole.png | Violet vortex pulling in debris | Canva sheet B |
| star_on.png | Glossy gold star | Canva misc sheet |
| star_off.png | Same star, recoloured slate-navy (empty) | **PIL recolour** of the Canva star_on |
| coin.png | Gold coin with an embossed cannon | Canva misc sheet |
| gem.png | Violet-cyan faceted gem (earned-only currency) | Canva misc sheet |
| chest.png | Gold-trim chest with a cyan seam glow | Canva misc sheet (seam holes closed in PIL, see below) |
| lock.png | Gold padlock, navy shackle (shackle hole is transparent) | Canva misc sheet |
| skull_hard_level.png | Cartoon skull, red eyes, flames | Canva misc sheet |
| badge_daily_boss.png | Navy/gold round crest with a horned helmet and a flame crown | Canva misc sheet |
| avatar_01.png | Boy demolition engineer, orange hard hat | Canva avatar sheet |
| avatar_02.png | Girl, teal pigtails, cyan headset | Canva avatar sheet |
| avatar_03.png | Friendly robot, cyan visor | Canva avatar sheet |
| avatar_04.png | Red panda in a violet aviator cap | Canva avatar sheet |
| badge_01_bronze.png / badge_02_silver.png / badge_03_gold.png | Tiered crest, 1/2/3 stars, orange blast emblem. Same scale and a shared ribbon baseline | Canva badge sheet |

All 8 power-ups share one tile: a gold bevel frame around a navy panel with rounded corners. Avatars share a gold ring on navy.

## Backgrounds: 1080x2340 RGB PNG
| File | Content | Source |
|---|---|---|
| bg_world_map.png | Floating-island chain (harbour, windmill, castle/clocktower, volcano, locked cloud island) with an empty winding road. **No nodes, numbers or UI** | Canva |
| bg_season_sunny_shores.png | Beach lagoon, lighthouse, huts, sandstone fort | Canva |
| bg_season_frozen_peaks.png | Ice fortress, alpine village, frozen lake, aurora | Canva |
| bg_season_neon_city.png | Toy-like neon skyline, monorail, twilight (signs are abstract shapes, no lettering) | Canva |
| bg_season_primeval_peaks.png | Prehistoric jungle valley, waterfalls, volcano, stone-and-bamboo fort, distant dinos | Canva (one pterosaur removed with cv2 inpaint, see below) |

Safe areas: the top 158px is sky only and the bottom 72px is calm water, sand, snow, plaza or grass. Canny edge density measured 0.000–0.002 in the top band and 0.000–0.001 in the bottom band, against 0.08–0.14 in the middle. contact_sheet.png shows the bands as cyan guides.

### Season rename
The target mockup's dinosaur-themed season is **Primeval Peaks** here (renamed earlier for trademark reasons). The file is `bg_season_primeval_peaks.png`. Nothing in this pack uses the old name.

## Pipeline (scripts in this folder; re-run order: key.py cuts → fix_avatars_badges.py → fix_chest.py → make_star_off.py → make_bg.py → fix_primeval_top.py → build_contact.py)
- `key.py`: green-screen key for Canva sheets on flat #00FF00. Alpha comes from the colour distance to the sampled background, edges are un-premultiplied (despill), the eroded interior is forced opaque, and per-cell specks are removed. Each item is cut to a 256px canvas.
- `fix_avatars_badges.py`: geometric circle alpha for the avatars (the teal hair was too close to the key colour) and a common scale for the badges.
- `fix_chest.py`: the cyan seam glow keyed partly transparent, so the holes are morphologically closed and despilled toward cyan.
- `make_bg.py`: Canva 896x1776 output is scaled to 2340px tall, centre-cropped to 1080, given a mild unsharp mask, and checked for safe-band detail.
- `fix_primeval_top.py`: inpaints out a large pterosaur that crossed the top safe band.
- `build_contact.py`: builds contact_sheet.png plus `src/qa_icons_on_{navy,white,checker}.png`.
- `src/`: raw Canva renders (`*_sheet.png`, `*_raw.png`) and spare bronze/silver medals (`_medal_*_alt.png`).

Canva media IDs: pu A MAHY5ovOJ5k, pu B MAHY5nfuV8U, misc MAHY5tO3gX0, avatars MAHY5v3548M, badges MAHY5mdy3Nk, map MAHY5hHa9UA, sunny MAHY5j48koY, frozen MAHY5jZQUaE, neon MAHY5sD0Oxw, primeval MAHY5hKdkDk.

## Known weaknesses / second pass
- **Backgrounds are upscaled about 1.32x** from Canva's 896x1776. They're fine behind UI, but fine detail is soft, most of all the Neon City signage. A native-res or upscaler pass would help before store screenshots.
- **star_off** is a PIL recolour, not a separate render. It's clean, but flatter than star_on.
- **chest** seam glow reads slightly teal-green after the hole fix.
- **Power-up names differ from v2 canon.** The 8 names follow Developer's request and the target (Mega Bomb … Black Hole). Art v2 / powerups.json use Shockwave, Splitstrike, Gravity Well, Chain Reactor, Phase Piercer and Time Fracture. PM/GD must confirm which set ships. Also, the v2 slot-colour rule (Core cyan / Module violet / Booster orange) is not encoded in these tiles: all are navy with a gold frame.
- The Daily Boss badge uses a horned helmet emblem. If the Emberforge Titan gets its own face icon, regenerate it.
- No baked text anywhere. Labels such as "Season 1", names and counts must be engine UI.
