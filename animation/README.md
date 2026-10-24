# CRASHVERSE — Animation Spec v1 (index)
Owner: Animator · Date: 2026-10-16 · Target: Android, Unity URP, 60 FPS mid / 30 FPS low (high tier only differs in debris cap).
Frames are @60 fps unless noted; ms is authoritative. Mermaid diagrams render in GitHub/most MD viewers.

## Sources used (latest versions)
- Art: `art/v1/art_bible_v1.md`, `art/v1/ui_system.md`, `art/v1/asset_list.md`, `art/m1b/wrecker_brief_v2.md` + `CHANGES.md` + `arena_brief_v2.md` (Wrecker v2 cannon — approved), `art/v2/PROMPTS_v2.md` + `art/v2/screens/`.
- Design: `M1_Level_Design_Contract_v0.2.md` (supersedes v0.1), `Design_Pack_v1_Ch1_Powerups_LiveOps.md`, `Design_Addendum_v1.1_...md`, `Spec_Gap_Answers_v1.md`.

## Files
| # | File | Covers |
|---|---|---|
| 1 | `01_wrecker_cannon.md` | idle, aim (drag/clamps/smoothing), fire, recoil, cooldown/re-arm, slot swap, muzzle/projectile hooks, Animator |
| 2 | `02_projectile_impact.md` | flight, per-material impacts, hit stop |
| 3 | `03_collapse_debris.md` | collapse beats, debris lifecycle within chunk caps |
| 4 | `04_powerups.md` | all 6 power-ups motion + VFX |
| 5 | `05_bosses.md` | Tidewarden (2 phases, arms drop, shutter tell) & Emberforge Titan |
| 6 | `06_ui_motion.md` | stars, combo x3+, rewards, map node unlock |
| 7 | `07_camera_shake_slowmo.md` | shake table, slow-mo/hit-stop rules, camera states |
| — | `audio_sync_table.md` | every event name, trigger frame/ms, description for Sound Manager |
| — | `OPEN_QUESTIONS.md` | gaps for Game designer / PM |

## Accessibility
- **Reduce Screen Shake** (default OFF): camera shake ×0 only.
- **Reduce Motion** (default OFF, auto-ON when Android "Remove animations" is on, player can turn it off; forces shake off): camera shake ×0, slow-mo disabled, hit stop ≤33 ms, flash intensity ×0.4 / alpha ≤0.5, no full-screen flashes, overshoots removed; gameplay tells kept as colour changes. Full spec: `07_camera_shake_slowmo.md` § Reduce Motion.

## PM decisions applied (2026-10-16)
- Win = ≥85% destroyed (contract v0.2 §4) everywhere.
- v1 (`art/v1/ui_system.md`, mockups) is canonical for layout & data; v2 screens are the visual target.
- Reduce Motion added (above).

## Game designer decisions applied (`design/Gameplay_Feel_Answers_v1.md`, authoritative)
- No charge: fixed shot power; drag to aim, tap FIRE; drag release never fires. Aim 0.25°/dp yaw, 0.15°/dp pitch approved.
- Debris cap 90 high / 60 mid / 25 low; low-tier chunk halving approved.
- Splitstrike splits at apex or 70% flight time (first); Time Fracture starts on arm, 4 s real time.
- Tidewarden core = crack steps only; arms detach as physics debris. Titan stages 25/50/75%, never attacks.
- No win-shot slow-mo; slow-mo disabled only in Titan ranked.
- Accessibility split into Reduce Shake + Reduce Motion (both OFF).

## Screen rule (Art, 2026-10-16)
v1 (`art/v1/`) is canonical for layout, data and states; v2 is only the visual/style target; v1 + design docs win where they differ. `06_ui_motion.md` § Screen motion aligned to v1 layout covers HUD (FIRE + Ready, 85% tick, x1/x2 badges, violet COMBO), fail screen hint, Level Complete mastery row, map states/marker/gate, Daily Titan damage score + Ranked/Practice, daily rewards. ART-1..10 resolved.

## Global rules
- Animation never drives gameplay outcomes (shot spawn, damage, settle, win) — events are VFX/SFX only.
- Gameplay values (radii, durations, HP) are read from data assets so VFX match rules.
- Aim & UI use unscaled time; settle uses scaled sim time.
- Low tier: halve particles, flipbooks instead of meshes, no extra full-screen passes, fidgets off.

## Particle budget (PM decision, via VFX & Tech Artist, 16 Oct)
One global particle budget: 150 live particles for impacts + 80 for the active power-up. When full, the oldest particles are culled first. Brick debris chunk colour: body #D2664C, chipped faces #E9997C, dust #D9A37E (art bible / arena_brief_v2).
