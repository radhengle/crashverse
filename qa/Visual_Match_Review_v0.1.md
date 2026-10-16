# CRASHVERSE — Visual Match & Originality Review v0.1 (QA) — updated 2026-10-16 (rev b)
Baseline: master design §4 (Visual identity), §8 (Wrecker), §12 (UI palette). Date 2026-10-16 IST. Reviewer: QA.

## Headline
- **No approved art bible exists.** §4 requires "a single approved art bible" and master references for the Wrecker, avatar, world-map style and material library. Only two briefs exist (`arena_brief.md`, `wrecker_brief.md`), and they're the closest thing to one. So this review is against master §4 plus those briefs, and a full art-bible conformance check is **BLOCKED**.
- **No UI screen mockups exist** for world map, power-up select, HUD, results, Daily Titan, daily rewards, season, or leaderboard. Every screen-level visual check is **BLOCKED** until Art & UI delivers them.
- Images found: 4 unique Art outputs (3 in `art/m1/`, mirrored byte-identically into `Assets/_Project/ArtReference/` by md5) + 1 in `art/m1b/`, plus 6 third-party reference boards in `art/m1b/ref/` and 12 unrelated PNGs in `/workspace/hq/`.
- Metadata: no PNG text chunks or EXIF in any file (checked with PIL), so there's no embedded provenance. `art/m1b/gen.py` draws the Wrecker v2 procedurally with PIL primitives and doesn't open the `ref/` images (no file reads in the script). That is evidence against pixel tracing, but not against design copying.

## Per-asset results
| Asset | Palette | Lighting/style | Title/typography | Originality | Result |
|---|---|---|---|---|---|
| art/m1/arena_sunnyville_moodboard.png (=ArtReference copy) | Matches arena_brief hexes. Navy roofs tie to brand. Environment palette is warm by design | Flat greybox, golden-hour sky. Acceptable as moodboard, not final | "CRASHVERSE" correct. Default PIL font | Original simple shapes | **PASS (greybox)** |
| art/m1/wrecker_hero_34.png | Navy bg, charcoal chassis, gold trim, cyan core, violet module, orange booster: matches §4/brief | Flat, no lighting yet | "CRASHVERSE - THE WRECKER" correct | Original. 4-wheel platform, not a classic cannon | **PASS (greybox)** |
| art/m1/wrecker_silhouette_sheet.png | Slot colours correct | Ortho read sheet | No title issue | Original | **PASS**, see VM-05 |
| art/m1b/wrecker_v2_hero.png | Background is bright royal blue (~#3B5BDB), not deep navy. Wooden-brown carriage dominates | Glossy cartoon | "CRASHVERSE" correct | Redesign toward a classic wooden cannon with a black cannonball, close to the cannon in the reference boards | **FAIL (VM-03 only)**: VM-01/02 closed by PM |
| ~~art/m1b/ref/b1.jpg, b2.jpg~~ (removed rev b; see VM-04) | n/a | n/a | Shows third-party name **"SMASH QUEST"** | Third-party/AI reference boards. Contain Google, Facebook, Instagram, X logos. Unknown origin and licence | **FAIL as an asset / OK as mood reference only**: VM-04 |
| ~~art/m1b/ref/b3–b6.jpg~~ | — | — | — | Removed from workspace (rev b) | **N/A** |
| art/m1b/arena_v2_sunnyville_moodboard.png, wrecker_v2_silhouette_sheet.png (+ Assets copies) | not yet reviewed | — | — | — | **PENDING** next pass |
| /workspace/hq/p1–p5, q1–q5, sheet*.png | — | — | "HAUS QUADRAT" restaurant menu | Unrelated to CRASHVERSE | **N/A**: out of scope, must not enter project |
| UI screens (map, picker, HUD, results, Titan, rewards, season, leaderboard) | — | — | — | — | **BLOCKED**: no files |

## Tickets
- **VM-01 — CLOSED.** Accepted by PM: Wrecker v2 approved, user reference direction supersedes master-doc cannon line. (Original finding: ~~Wrecker silently redesigned. wrecker_v2_hero.png replaces the locked M1 Wrecker (4-wheel charcoal platform, trapezoid turret: wrecker_brief "Silhouette stays fixed all campaign") with a wooden carriage, one big spoked wheel and a cannonball. This breaks §4 "Never silently redesign a previously approved … machine" and wrecker_brief silhouette rules 1/3 (4 visible wheels per side). Expected: a change request through PM, or keep v1. The in-engine `WreckerBuilder.cs` builds v1, so the art and the build are now inconsistent.~~) Follow-up for Developer: WreckerBuilder must move to v2 dims (art/m1b/CHANGES.md).
- **VM-02 — CLOSED.** Accepted by PM: Wrecker v2 approved, user reference direction supersedes master-doc cannon line. (Original: ~~"Cannon" identity. Master §8: the Wrecker "is not simply a traditional cannon". The v2 caption literally says "cannon-on-wheels". It also moves closer to the reference boards' black cannon, which is an originality and differentiation risk.~~)
- **VM-03 (Low) — OPEN, owner Art.** Background palette. The v2 background is saturated royal blue, outside "deep navy/charcoal". Use navy #14213D–#1D2B4F. Re-check rev b: `art/m1b/wrecker_v2_hero.png` was regenerated (md5 043832d6…), but `Assets/_Project/ArtReference/wrecker_v2_hero.png` (md5 ec37602a…) is byte-identical to `art/m1b/wrecker_v2_hero_royalblue_backup.png`, so the **royal-blue version is what sits in Assets**. Art to sync the Assets copy and keep the backup out of Assets.
- **VM-04 (High, process) — PASS (verified rev b).** Evidence:
  - `/workspace/art/_reference_DO_NOT_SHIP/README.md` exists. Heading "REFERENCE ONLY - DO NOT SHIP": never ship in build/store listing, never trace/crop/reuse, social logins must use official provider brand assets. The folder holds only README.md (no images).
  - `art/m1b/ref/` no longer exists (`ls`: No such file or directory). `find / -xdev -name 'b[1-6].jpg'`: no results.
  - Every image under `crashverse/Assets` and `/workspace/art` (13 PNGs, no JPG/WEBP) is a known CRASHVERSE art file (m1 set + v2 arena/wrecker/silhouette + backup). None has the board sizes (900x600 / 900x822). `rg -il "smash ?quest"` over Assets and art: no matches.
  - Residual (Info): the README says the originals live in an agent attachments folder outside /workspace and the project. That's acceptable, but they must never be copied into Assets. QA didn't re-view the v2 images pixel by pixel for logos in this pass, beyond the earlier review of wrecker_v2_hero.
- **VM-05 (Low) Typography.** All art uses PIL's default font. No brand typeface or title lockup is defined. Blocks the typography check in the art bible.
- **VM-06 (Info) Stray files.** `/workspace/hq/` (Haus Quadrat menus) is unrelated. Keep it out of the project.
- I found no Angry Birds or other franchise logos or characters in any CRASHVERSE Art output I viewed.

## Blocked until delivered
1. Approved art bible (palette with hex + usage ratios, lighting, typography/title lockup, icon style, UI kit).
2. Screen mockups for all 8 in-scope screens (Design Pack "Screens Art & UI needs" 1–9).
3. ~~Decision on Wrecker v1 vs v2~~: resolved, v2 approved by PM.
4. ~~Provenance note for refs~~: resolved by the DO_NOT_SHIP README.
