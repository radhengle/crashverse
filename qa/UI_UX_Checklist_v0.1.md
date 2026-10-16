# CRASHVERSE — UI/UX Test Checklist v0.1 (QA)
Sources: master design `/workspace/crashverse_share.txt` §4, §5, §12 · Design Pack v1 (`design/Design_Pack_v1_Ch1_Powerups_LiveOps.md`) "Screens Art & UI needs" · code at commit `0be0fa1`.
Status: **No APK, no screen mockups.** Every check below is BLOCKED on a build except §9 (code review). Emulator first pass is OK; hardware validation is still open.

## 0. Global test profile (applies to every screen)
| ID | Check | Pass threshold |
|---|---|---|
| G-01 | Small-phone baseline | Fully usable on 5.0in 720x1280 (xhdpi), 360dp width, and on 320dp width as stress |
| G-02 | Aspect ratios | 16:9, 18:9, 19.5:9, 20:9, 21:9. No clipped or overlapping UI. Letterboxing only where intended |
| G-03 | Safe area | All interactive and critical text sits inside `Screen.safeArea`: punch-hole, notch (top/side), waterfall edges, gesture-nav bar. Test portrait and landscape-left/right |
| G-04 | Text size | Body ≥12sp, labels/captions ≥11sp, numerics on the HUD ≥14sp. Readable at system font scale 1.3 with no truncation |
| G-05 | Contrast | Text vs background ≥4.5:1 (WCAG AA); large text (≥18sp bold) ≥3:1. Gold #E8B04B on navy #14213D and cyan #22D3EE on navy to be measured |
| G-06 | Tap targets | ≥48x48dp, ≥8dp gap between targets, no target inside the 16dp edge-gesture zone |
| G-07 | Android back | System back / back gesture works on every screen: it closes a modal, then goes up one level, and from the map it asks to quit. Never kills a ranked run without confirmation |
| G-08 | No dead ends | Every screen has a visible way out; errors offer Retry + Back |
| G-09 | Play/retry depth | Fail→Retry ≤1 tap. Win→Next ≤1 tap. Map→level in play ≤2 taps (node→Play) |
| G-10 | Loading | Spinner/progress appears within 300ms. A load that runs past 10s shows a timeout with Retry. No frozen frame |
| G-11 | Offline | Campaign is playable offline. Online-only screens (Titan ranked, leaderboard, season sync) show a clear offline state, not a blank screen or crash |
| G-12 | App kill/resume | Kill from recents at each screen, then relaunch. State matches the persistence rules in §8 |
| G-13 | Orientation | ProjectSettings `defaultScreenOrientation: 4` (auto-rotate). Confirm the intended orientation per screen; menus must be portrait-friendly (§12) |
| G-14 | Title/branding | Shows "CRASHVERSE" only. No placeholder or reference game names (e.g. "Smash Quest" from the reference boards) |

## 1. World map (Island map, Design Pack §1 / master §5)
- [ ] Nodes L1–10 are ≥48dp at minimum zoom. Node numbers ≥14sp. Star counts readable at 360dp.
- [ ] Locked, unlocked, completed and boss states can be told apart without relying on colour alone (icon/lock glyph).
- [ ] Zoom/pan limits: the player can't lose the map and gets a recenter control. A pan doesn't trigger a node tap (threshold ≥10dp of movement cancels the tap).
- [ ] Selection feedback appears before navigation (highlight + briefing). Accidental-tap guard.
- [ ] Bridge gates show the unlock requirement. The L10 storm island is visibly a boss destination.
- [ ] The Daily Titan and Event entry points stay reachable at 360dp and don't overlap nodes or the top bar.
- [ ] Top bar (currency/profile/settings) is inside the safe area.
- [ ] Back from the map shows a quit confirmation.
- [ ] Persistence: after kill/relaunch the camera returns to the last-played node and the stars match the save.

## 2. Level briefing / power-up select (Design Pack §2)
- [ ] 3 slots labelled Core / Module / Booster. Each slot ≥48dp. Tile tooltips are readable (≥12sp).
- [ ] Locked tile shows a silhouette plus "Unlocks at …" text that stays within 2 lines at 360dp.
- [ ] Forced/locked slots (L7, L8) are clearly marked as non-editable.
- [ ] Shot budget and star bands are shown with the exact numbers for the level.
- [ ] Play button ≥48dp tall, in the thumb zone, and inside the safe area.
- [ ] Back returns to the map with the selection kept (or discard behaviour is defined).
- [ ] Selected loadout survives app kill and is restored on the next briefing.

## 3. In-level HUD (Design Pack §1/§2, master §12)
- [ ] Shows shots left, % destroyed, active power-up charges, objective and pause.
- [ ] **HUD does not cover the structure**: at 16:9 and 20:9, no HUD element overlaps the target bounding box at the default camera framing. The Wrecker sits in the bottom-left ~20% (wrecker_brief).
- [ ] Fire button ≥48dp, offset from the gesture bar. Aim drags that start on HUD elements are ignored.
- [ ] Pause opens settings and Resume/Retry/Quit. Back = pause (not quit).
- [ ] App backgrounded mid-shot: on resume the game is paused and physics stays coherent.
- [ ] Shot counter updates within 1 frame of fire. The FPS overlay is off in release.

## 4. Results: win / fail / stars (Design Pack §1, M1 contract)
- [ ] Stars render 1–3 with filled/empty states told apart by shape, not only colour.
- [ ] Mastery goal tick shown.
- [ ] Retry ≤1 tap, Next ≤1 tap, Map ≤1 tap. Back = map.
- [ ] Best stars persist across retry, kill and reboot. A lower result never overwrites a higher one.
- [ ] Fail screen shows % reached vs 85% needed.

## 5. Daily Titan (Design Pack §3)
- [ ] Shows today's rule text (≤3 lines at 360dp), Ranked and Practice buttons (clearly labelled), and a reset countdown in local time that computes the 00:00 UTC target (05:30 IST).
- [ ] Ranked button disabled after use, with the reason shown. Practice stays available.
- [ ] Offline: ranked is disabled with an "online required" state. Practice: defined behaviour (spec gap).
- [ ] Back during a ranked attempt asks for confirmation and states that the attempt is consumed.

## 6. Daily rewards calendar
- [ ] 7 tiles readable at 360dp. Today, claimed and paused states are distinct. Claim button ≥48dp.
- [ ] Claim state survives kill. Double-tap doesn't double-claim.

## 7. Season track / Weekly event hub / Leaderboard
- [ ] Season: 30 tiers scroll smoothly. Free and premium rows are distinct. Premium is labelled cosmetic-only. Current tier auto-scrolls into view. Time-remaining shown.
- [ ] Event hub: 5 event levels and the rule twist explained.
- [ ] Leaderboard: tabs Daily/Weekly/Season/Friends ≥48dp. Top 100 plus a pinned own-rank row. Long names truncate with an ellipsis. Guest sees a "link account to appear" CTA. Offline shows a cached/empty state with a timestamp.

## 8. State persistence matrix (each item: kill app, reboot, airplane mode, reinstall where noted)
| Item | Kill/resume | Reinstall | Notes |
|---|---|---|---|
| Best stars per level | persist | persist only if account-synced | currently local PlayerPrefs only |
| Unlocked nodes / bridges | persist | synced | |
| Selected loadout | persist | n/a | |
| Power-up charges mid-level | restore on retry (spec) | — | |
| Daily Titan ranked used | persist | **must persist** (server) | |
| Daily reward streak/claim | persist | synced | |
| Daily reset | 00:00 UTC, not device-local midnight | — | test at IST 05:29:59→05:30:00 |
| Season tier/XP | persist | synced | |

## 9. Code-level risks (review of commit 0be0fa1; no build run)
| Ticket | File:line | Sev | Expected | Actual |
|---|---|---|---|---|
| QA-UI-001 | Assets/_Project/Scripts/UI/HudController.cs:52,67-68,75,81,99 | High (ship blocker for UI) | HUD respects `Screen.safeArea` and scales by DPI (CanvasScaler, Scale With Screen Size, reference 1080x1920 or similar) | IMGUI with fixed pixel rects (340x150 box, 160x80 fire, 200x48 quality, 140x56 Give Up) at absolute screen edges. No safe-area or DPI handling, so it gets clipped by notches/gesture bar. Sizes in px are ~27dp at xxxhdpi for a 48px button (below 48dp). Known placeholder per QA_NOTES |
| QA-UI-002 | Assets/_Project/Scripts/UI/HudController.cs:33,39,42 | Med | Text ≥12sp-equivalent at every density | Fixed fontSize 20–28 px, not density-scaled: ~10sp body at xxxhdpi (4x). Too small on high-dpi small phones |
| QA-UI-003 | Assets/_Project/Scripts/Weapons/AimController.cs:98-102 | Med | Taps on any HUD button don't start an aim drag | Exclusion zone is only the bottom-right 28%x28% (percentage). Quality button (top-right), Give Up (bottom-left) and Retry panel aren't excluded, so tapping them also rotates aim. The zone also doesn't match the fixed-px Fire rect at line 68 |
| QA-UI-004 | Assets/_Project/Scripts/ (no match for Escape/back) | Med | Android back = pause/close modal | No back-key handling anywhere in Scripts. Back behaviour is undefined (Unity default may exit the activity) |
| QA-UI-005 | Assets/_Project/Scripts/Level/LevelConfig.cs:52-59 | High (for Ch1) | Stars from % shots left (Design Pack: 3★ ≥60%, 2★ ≥30%) per level budget 4/5/6/7 | Hardcoded 5-shot table (≥4→3★, ≥2→2★). Wrong for L1-2 (4 shots), L9 (6), L10 (7). Also conflicts with the pack at 3/5 left (pack→3★, M1 table→2★); see spec gap SG-01 |
| QA-UI-006 | Assets/_Project/Scripts/Level/StarProgressStore.cs:12,21-22 | Med | Progress survives reinstall/device change (account sync) and tamper resistance for anything competitive | Plain PlayerPrefs (unencrypted shared_prefs XML). Fine for local M1 best stars. Must not be reused for ranked Titan or rewards |
| QA-UI-007 | Assets/_Project/Scripts (no DateTime/UtcNow usage) | Info / Blocked | Daily reset uses server time at 00:00 UTC | No daily reset, streak, season or leaderboard code exists yet. Cannot test; re-review when implemented (watch for `DateTime.Now` / local midnight) |
| QA-UI-008 | ProjectSettings/ProjectSettings.asset:10 | Low | Deliberate gameplay orientation, portrait-friendly menus (§12) | `defaultScreenOrientation: 4` (AutoRotation). Orientation isn't locked; the HUD layout is untested under rotation |
