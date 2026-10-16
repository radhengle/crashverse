# VFX open questions / conflicts v1 (for PM, Animator, Art, Developer)
Updated 16 Oct 2026 IST.

## Resolved
- **Q-A1** Brick colours: body #D2664C, chipped #E9997C, dust #D9A37E (PM). Applied in spec + VfxCore.
- **Q-A2** Particle budget: one global pool, 150 impacts + 80 active power-up, oldest culled first. `ParticleBudget` in VfxCore.
- **Q-A3** RM Shockwave: one 100 ms ease-out tint per activation, alpha ≤0.5.
- **Q-A4** RM Time Fracture: static 30 % desat under static 25 % tint, tint fades out 300 ms, no crack animation.
- **Q-R1** Shading: Art approved GlossyToon values (threshold 0.45, softness 0.08–0.12, shade #5B6FA8 55–65 %, spec #FFF6E0, rim #FFFFFF 0.35 back-left). Applied.
- **Q-R2** VFX owns crack masks and sprite sheets.
- **Q-R3** Art v2 is never copied into the repo; referenced at `/workspace/art/v2` (lighting: `/workspace/art/m1b/arena_brief_v2.md`).
- **Q-R4** Cyan #22D3EE is Core/Wrecker only and never appears in the environment (Art).
- **Q-D4** Device testing waits for the Unity licence and APK.

## Open
- **Q-A5** Other teams' uncommitted edits exist in the shared checkout; VFX works in its own worktree and never touches them.
- **Q-R5** Inverted-hull outline (0.02 u #101828) is optional: which hero props get it? (adds a pass per prop)
- **Q-D1** Please add `Scripts/VFX/**/*.cs` to `Tools/CompileCheck` and CI (VFX uses `vfx/compile_check` meanwhile).
- **Q-D2** Impact/break events need position + material payload; proposed `Crashverse.VFX.VfxHit`.
- **Q-D3** GameBootstrap/AppServices should spawn `VfxDirector` and give it the `QualityTier`.
- **Q-D5** PU_PHASE_PASS/HIT have no code hook yet.
