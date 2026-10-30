# CRASHVERSE — Master Completeness Checklist (10-level scope)
Audited 2026-10-16 22:45 IST against `origin/main` @ 22162b5 (25537db is #3 head) (radhengle/crashverse), open PRs, worktrees and team docs.
Status: **D** = Done on main · **P** = Partial (code/docs exist, or only in an unmerged PR / uncommitted worktree, or not verified on a device) · **M** = Missing.
Rule: nothing counts as Done until it is on main AND seen working in the CI emulator tap-through (age gate → L1 win/fail).

## P0 — Gameplay feel & visible quality (first)
| # | Requirement (share §) | Status | Evidence | Owner |
|---|---|---|---|---|
| 1 | Aim + drag-to-aim input on device (§3) | P | `Weapons/AimController.cs` on main; touch was dead until PR #23 (unmerged, not tap-verified) | Developer |
| 2 | Cannon fire + projectile ballistics (§3, §8) | P | `FireController.cs`, `Projectile.cs` on main; tick-converted version in PR #14 (open) | Physics Developer |
| 3 | Destruction: blocks, support graph, collapse (§7) | P | `Destruction/DestructibleBlock.cs`, `StructuralIntegrity.cs` (31 lines, minimal); collapse predicate PR #11/#12 open | Physics Developer |
| 4 | Debris pooling + debris damage (§7) | P | `DebrisPool.cs` on main; damage rule PR #11, deterministic pool PR #6 open | Physics Developer |
| 5 | Material set: wood/brick/steel/glass/rubber/ice/crystal/concrete/energy (§7) | P | `MaterialDefinition.cs`: wood/brick/steel only; Ch1 needs at least glass + stone | Physics Developer |
| 6 | Material-specific fracture (pre-fractured variants) (§7) | M | no fracture meshes in repo or models_src | 3D Modeler |
| 7 | Deterministic retry / 50 Hz lock (§7) | P | PR #6 open | Physics Developer |
| 8 | Damage feedback (hit flash, cracks, damage stages) (§7) | M | no crack/damage-stage material or code | VFX & Tech Artist |
| 9 | Impact event wiring → feel/VFX/SFX (§7) | P | PR #18 + #20 open (stacked) | Physics Developer |
| 10 | Camera shake / hit-stop / slow-mo (§7, juice) | P | `Feel/FeelDirector.cs` ShakeStack, HitStop on main; only one impact type wired (`ImpactWood`) | Developer |
| 11 | VFX explosions, dust, sparks, glass shards (§4, §7) | M on main / P in PR | zero ParticleSystem on main; PR #2 has shaders + VfxDirector only, no particle prefabs | VFX & Tech Artist |
| 12 | Toon/PBR shaders applied in game (§4) | P | PR #2 shaders; not on main, not on any material | VFX & Tech Artist |
| 13 | Real 3D Wrecker (≤12k tris, PBR, baked normals) replacing greybox | P | `WreckerBuilder.cs` builds primitives; textures only, uncommitted, in `/workspace/wt/3d-modeler-v3/Assets/_Project/Models/Wrecker_v2/` (no mesh exported); .blend in `models_src/Wrecker_v2` | 3D Modeler |
| 14 | Island environment Sunnyville (≤10k) | P | `models_src/Island_Sunnyville/*.blend`; nothing in repo; `ArenaBuilder.cs` is procedural | 3D Modeler |
| 15 | Building kit wood/brick/steel (500–1.5k each) | P | `models_src/Kit_Wood|Kit_Brick|Kit_Steel` .blend; not exported/imported | 3D Modeler |
| 16 | Import pipeline: FBX→prefab, LODs, colliders, materials | M | no .fbx/.prefab/.mat on main | VFX & Tech Artist |
| 17 | Wrecker fire/recoil animation (§8) | P | spec `animation/01_wrecker_cannon.md`; no Animator assets | Animator |
| 18 | Collapse/debris + powerup + UI motion animation | P | specs `animation/02–07`; not implemented | Animator |
| 19 | SFX: per-material impacts, creaks, layered collapse, UI clicks (§13) | M | `audio/sfx/` has only `boss/`; no SFX code (no AudioSource besides music) | Sound Manager |
| 20 | Music hookup in build (§13) | P | `MusicPlayer.cs` loads `Resources/Music/<id>`; no .ogg in repo — music won't play in APK. Tracks exist in `/workspace/crashverse/audio/music/` | Developer + Sound Manager |
| 21 | main_theme.ogg kept unchanged | D | `/workspace/crashverse/audio/music/main_theme.ogg` (DO NOT EDIT) | Sound Manager |
| 22 | Piano/guitar re-render | P | `main_theme_alt_piano.ogg` only; re-render unfinished | Sound Manager |
| 23 | Haptics (§13) | P | `audio/haptics.md` spec; no code | Developer |
| 24 | Logo sound / region ambience (§13) | M | not in music or sfx | Sound Manager |
| 25 | Gameplay video capture per feature for user | M | process not set up | QA |

## P1 — Playable 10 levels & flow
| # | Requirement | Status | Evidence | Owner |
|---|---|---|---|---|
| 26 | All 10 Ch1 levels authored | P | all 10 present (L07 = m1_clockwork_tower.json via chapter1.json; see levels/ch1/LEVEL_INDEX.md); PR #13 needs #12 test updates + #23 touch fix to merge/play | Level Designer |
| 27 | Levels tuned / solvability sim | P | PR #13 sim, PR #12 pipeline; not played on device | Level Designer |
| 28 | L10 Tidewarden boss | P | `Boss/TidewardenController.cs`, music exists; boss art/VFX missing | Developer |
| 29 | Win ≥85% + three-star scoring | P | `Meta/WinRule.cs`, `StarRules.cs`, `MasteryEvaluator.cs`; CampaignScore split PR #16 | Game designer |
| 30 | Results / fail-retry screens | P | `ResultsScreen` in `UI/Screens.cs`; not tap-verified | Developer |
| 31 | UI flow age gate → map → briefing → L1 → results → map | P | screens exist; touch fix PR #23 open; CI smoke only checks launch | Developer |
| 32 | Age gate (tap pickers, under-13 block, Continue disabled) | P | PR #23 + Art PR #24 open | Developer / Art & UI |
| 33 | Final UI art matching reference (navy/gold, original) | P | `/workspace/art/v2/screens`; game UI still procedural `UIFactory` | Art & UI |
| 34 | Official logo (transparent) | M | stand-in title only | Art & UI |
| 35 | Splash / loading / error / maintenance states (§12) | M | no splash screen class | Developer |
| 36 | Name/avatar creation, profile, achievements (§11–12) | M | not in `Screens.cs` | Developer |
| 37 | Wrecker workshop/upgrades, equipment select/inventory (§8, §12) | P | `Meta/Loadout.cs`, `wrecker.json`, `powerups.json`; no workshop screen | Developer |
| 38 | Power-ups runtime (§8) | P | `PowerUps/PowerUpRuntime.cs`; no VFX/SFX | Developer |
| 39 | Tutorial / first-time onboarding for L1 | M | none found | Game designer |
| 40 | Combo / hint rules (no free hint on HUD) | P | `ComboTracker.cs`, PR #16 | Game designer |
| 41 | Daily Titan, weekly events, season, leaderboards, daily rewards | P | screens + JSON exist; offline only; out of 10-level must-have | Game designer |

## P2 — Economy, settings, accessibility, performance
| # | Requirement | Status | Evidence | Owner |
|---|---|---|---|---|
| 42 | Economy v1: earned-only gems, no IAP | P | PR #19 open; PR #10 gem-pack flag off | Developer |
| 43 | Store screen | P | `StoreScreen`; `store_catalog.json` | Developer |
| 44 | Reduce shake / Reduce motion (+auto from Android) | D | `SettingsModal` in `Screens.cs` L570-589; `MotionSettings` | Developer |
| 45 | Music / SFX volume, vibration, graphics quality toggles (§12) | M | not in SettingsModal | Developer |
| 46 | Safe area / notch / aspect ratios | P | `art/v2/safe_area.py`, PR #23 safe area; QA L2 open | Art & UI |
| 47 | Quality tiers / destruction quality setting (§7, §16) | P | `Perf/QualityController.cs`; no user toggle; not profiled | Developer |
| 48 | Perf on mid phones (60/30 fps, memory) (§16) | M | no profiling run on any device | QA |
| 49 | Install on Android 16 / Nothing 3a (dev-build-4 rejected) | M | root cause unknown; dev-build-5 pending | Release Manager |
| 50 | Fixed signing key for all dev builds | P | PR #23 (BUG-21) | Release Manager |
| 51 | targetSdk 35 | P | PR #7 open | Release Manager |
| 52 | CI emulator tap-through (age gate→L1 win/fail + screenshots) | P | PR #23 smoke = launch only; QA script not in CI | QA |
| 53 | PlayMode tap tests green | M | failing in batchmode (PR #23) | Developer |
| 54 | APK hardening asserts | P | PR #21 | Release Manager |
| 55 | Signed AAB for Play | P | PR #8 (needs upload keystore — user) | Release Manager |

## P3 — Accounts, backend, legal
| # | Requirement | Status | Evidence | Owner |
|---|---|---|---|---|
| 56 | Guest account, offline local backend | D | `Backend/LocalBackendService.cs` | Backend Engineer |
| 57 | Server v0 | P | PR #1 approved, unmerged; scanner false positive ServerContracts.cs:75 | Backend Engineer |
| 58 | Account deletion, 365-day guest purge | P | `Core/AccountDeletion.cs`, `AccountLifecycle.cs`; server side in PR #1 | Backend Engineer |
| 59 | Age-band data minimisation (store band only) | P | spec decided; Security wording pending | Security Admin |
| 60 | Privacy policy / ToS / Data Safety | P | drafts in `/workspace/crashverse/security/`; legal entity, grievance officer pending (user) | Security Admin |
| 61 | Anti-cheat / fair play (§15) | P | `StoreValidator.cs`, `CampaignResultQueue.cs`; server validation in PR #1 | Security Admin |
| 62 | Other regions 2–5 | M (out of 10-level scope) | only titan layouts | Game designer |

## Open PRs (20) — all unmerged
#1 Backend server v0 · #2 VFX v1 foundation · #3 Developer WIP (music v2, age gate, /v1) · #5 Rules asmdef · #6 50 Hz lock · #7 targetSdk 35 · #8 signed AAB · #10 gem-pack flag · #11 debris damage · #12 level pipeline · #13 Ch1 L1-10 data · #14 physics tick conversion · #15 physics feel proposal · #16 combo split · #18 impact feel · #19 economy v1 · #20 impact wiring · #21 APK hardening · #22 CONTRIBUTING approvals · #23 BUG-20 touch fix · #24 Art age gate v2.
Merge order suggestion: #23 → #5 → #6 → #14 → #11 → #18 → #20 → #2 → #12/#13 → #16 → #19/#10 → #7/#21/#8 → #1 → #22/#3/#24.

## Unfinished bot tasks
- 3D Modeler: v3 HQ pass — Wrecker textures only, uncommitted in `wt/3d-modeler-v3`; no FBX, island + kit not exported.
- VFX & Tech Artist: PR #2 has shaders only; no explosion/dust/spark/shard particle prefabs; damage-stage shader missing.
- Sound Manager: piano/guitar re-render; entire SFX library (sfx dir empty except boss/); music clips not in repo Resources.
- Animator: specs 01–07 written, zero animation assets.
- Level Designer: merge #12 then #13; on-device tuning from CI video.
- Physics Developer: 7 stacked PRs (#5 #6 #11 #14 #15/#16 #18 #20) waiting review/merge; glass/stone materials.
- QA: dev-build-4/5 emulator run-through; 8 PR reviews; CI tap-through script.
- Developer: PR #23 fixes (tap tests, M1/M2, L2), dev-build-5, merge backlog.
- Art & UI: logo, PR #24, in-game UI skin.
- Release Manager: Android 16 install failure root cause.
