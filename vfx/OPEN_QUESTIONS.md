# VFX open questions / conflicts v1 (for PM, Animator, Art, Developer)

## Animator
- **Q-A1** 02 brick "red chunks": no hex given. VFX uses #C0392B placeholder. Confirm or supply from arena_brief_v2.
- **Q-A2** 04 Perf says Mid ≤80 particles per power-up; 02 says ≤150 live impact particles. Gravity Well + child impacts can coexist: is the 150 cap shared or additive? VFX assumes separate budgets (§4).
- **Q-A3** Reduce motion "flashes ≥100 ms ramps": does this also stretch Shockwave's 1-f link flashes (12×16 ms stagger = 192 ms) or replace them with a single tint? VFX proposes single 100 ms tint.
- **Q-A4** Time Fracture under Reduce motion: is the 30% env desaturation kept with the static 25% tint?
- **Q-A5** Uncommitted local edits exist in animation/05, 07, audio_sync_table.md and design/Spec_Gap_Answers_v1.md on the checkout; VFX did not touch or commit them. Spec built from the working-tree text.

## Art
- **Q-R1** Art v2 is a painted screen target (Canva) with no material/lighting sheet. Confirm toon band softness, shade tint (proposed cool #8C99D9) and spec "candy" highlight vs. a softer PBR look.
- **Q-R2** Crack mask textures (R=step1, G=step2) per material and FX flipbooks (dust, splinter, spark) are not in repo; who authors them (VFX can make originals in Krita/Blender)?
- **Q-R3** Art v2 lives at /workspace/art/v2, not in repo docs/; should a copy/link land in `Assets/_Project/ArtReference/`?
- **Q-R4** Cyan reserved for Core/Wrecker: Chain Reactor arcs and Shockwave ring are Core so stay cyan—confirm no clash with water/sky in Sunnyville.

## Developer / PM
- **Q-D1** `Tools/CompileCheck/CompileCheck.csproj` lists folders explicitly and does not include `Scripts/VFX`; VFX did not edit it (Developer-owned) and added `vfx/compile_check` instead. Please add `Scripts/VFX/**/*.cs` to CompileCheck and CI.
- **Q-D2** Impact/break events currently carry unknown payloads; VFX needs position + material. Proposed `Crashverse.VFX.VfxHit` payload or an agreed struct in FeelCore.
- **Q-D3** VfxDirector must be spawned by GameBootstrap/AppServices (no prefabs). Also needs current `QualityTier` source.
- **Q-D4** URP asset/Volume settings (§3) are Editor-only; requires someone with Unity to apply and validate shaders on device (Mali/Adreno).
- **Q-D5** PU_PHASE_PASS/HIT have no code hook (anim_audio_map) — VFX blocked on same hook.
