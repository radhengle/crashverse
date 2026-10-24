# CRASHVERSE · VFX & Shader Spec v1
Owner: VFX & Technical Artist · 16 Oct 2026 IST · Unity 2022.3.50f1 / URP 14 / Android
Sources: Art v2 FINAL visual target (`/workspace/art/v2`, board_v2.png, PROMPTS_v2.md: "glossy materials, volumetric lighting, navy + gold, cyan Core / violet Module / orange Booster"), animation/02, 03, 04, 07 (Reduce motion), audio/anim_audio_map.csv (authoritative sync), audio_sync_table.md, Gameplay_Feel_Answers_v1, README "Look is data" (`VisualConfig`).
**Rule:** VFX are listeners only (`AnimEventBus`). They never change gameplay, input timing or audio. Effect values (radii, durations) are read from gameplay data, not hard-coded.

**PR scope v1:** this spec, the glossy material shader, impact + dust effects (shader + pooled trigger component). Power-ups are **spec only** (§6).

## 1 · Look (Art-approved 16 Oct 2026; lighting ref `/workspace/art/m1b/arena_brief_v2.md`)
Art v2 is referenced by path only (`/workspace/art/v2`), never copied into the repo.
| Parameter | Value |
|---|---|
| Toon threshold / softness | 0.45 / 0.08–0.12 (hero props 0.15) |
| Shade tint | #5B6FA8 at 55–65 % (default 0.6), multiplied into albedo; wood/brick may drift toward #7A4A6A |
| Spec | stepped, colour #FFF6E0, band cut 0.5, gloss 0.75–0.8; metal intensity 0.9 (steel smoothness 0.7); props 0.5 (smoothness 0.4–0.5) |
| Rim | #FFFFFF at 0.35 from back-left (`_RimDirWS`) |
| Outline (optional) | inverted hull 0.02 u #101828, not in v1 shader (extra pass; enable per hero prop later) |
| Core cyan | #22D3EE, white-hot centre #E0FBFF, emissive ≈2.5 with bloom, gold #E8B04B rim, short dark outline on trails. **Cyan never in the environment.** |
| Power-up colours | Module #8B5CF6, Booster #FF7A1A, weak-point ring #E8B04B |
| Cracks | chunky wedge lines in the material's dark colour: wood #6B4226, mortar #7A3326, steel #3A3F48. Step 1 sparse edge cracks; step 2 dense + ≈20 % darkening (`_DarkenPerStep` 0.1) |
| Effects | toon puffs with 2–3 value steps; no realistic smoke. White flashes capped (shader clamps flash at 0.85; ×0.4 under Reduce motion) |
| Debris | wood splinters #F5C994 + dust #CFA779; brick body #D2664C, chipped faces #E9997C, dust #D9A37E; steel sparks #FFD27A + bits #3A3F48 |

## 2 · Shaders (delivered: `Assets/_Project/VFX/Shaders/`)
| Shader | Use | Notes |
|---|---|---|
| `Crashverse/GlossyPBR` (**primary**, v1.1) | all opaque gameplay meshes on High/Mid; Low via SubShader LOD 150 | URP GGX BRDF (`InitializeBRDFData`, `DirectBRDFSpecular`, `GlossyEnvironmentReflection`, plain probe sample, no box projection or blending), normal map, ORM, soft shadows, baked AO from ORM.r only (no SSAO), lightmaps (UV2), emissive, stylised terminator, shade tint, back-left rim, damage cracks (× vertex R), hit flash, flex (× vertex A), `_InnerFace` for M_<Mat>_Inner. Passes: ForwardLit, ShadowCaster, DepthOnly (DepthNormals removed with SSAO). |
| `Crashverse/GlossyToon` | Low-tier/fallback, stylised previews | Toon band (threshold/softness), shade tint, stepped Blinn spec (gloss + band cut), rim, emission, SH ambient. **Damage 2 steps**: `_DamageStep` 0/1/2 darkens and blends crack mask (R=step1, G=step2). `_HitFlash` lerp. ShadowCaster + DepthOnly passes. |
| `Crashverse/VfxParticle` | impact sprites, dust, sparks, smoke | Unlit, blend via material (alpha or additive), vertex-colour fade, alpha-clip floor to cut invisible overdraw, `_Intensity` = flash cap hook. No depth texture needed. |
| Dissolve/crack + power-up fresnel | — | **Deferred to v2 PR** (PM scope). Planned as variants of the above using the same CBUFFER layout. |

SRP Batcher: every material property is in `UnityPerMaterial`; no per-object MaterialPropertyBlocks. Damage step and hit flash use **3 shared material instances per material type** (step 0/1/2) + one flash material swapped for the flash duration, so batching survives. Variants: GlossyToon = `_MAIN_LIGHT_SHADOWS` × fog × instancing (≤8 per pass); VfxParticle = fog only. Strip unused URP variants via URP asset (additional lights off, no SSAO, no decals keyword); exact GlossyPBR counts in §4a.

## 3 · Lighting & post (URP 14 mobile), v1.1 "real, top-quality objects"
Reference: `/workspace/art/m1b/arena_brief_v2.md` (key #FFF2D6 1.4 at (50°, -35°, 0); gradient ambient sky #8CC8FF / equator #DCEFFF / ground #6FAE5A ×1.15; fog linear #CFEAFF 80–250). Values live in `VisualConfig`.

### URP asset per tier (3 assets: `URP_High`, `URP_Mid`, `URP_Low`, switched by QualitySettings)
| Setting | High | Mid | Low |
|---|---|---|---|
| Render scale / MSAA / HDR | 1.0 / 2× / on (R11G11B10) | 0.85 / off / on | 0.7 / off / off |
| Main light shadows | 2 cascades (split 25%), distance 40 m, 2048 | 1 cascade, 35 m, 1024 | hard, 1 cascade, 25 m, 512 (or off → blob under Wrecker) |
| Soft shadows | on | on | off |
| Additional lights | off (per-vertex for FX glows if needed) | off | off |
| SSAO (Renderer Feature) | **off** (QA #2: AO = baked ORM.r only; shader has no SSAO keyword) | **off** | off |
| Reflection probes | **one baked probe per zone, no blending, no box projection** (URP asset: Probe Blending off, Box Projection off) | same | off (SH + `_MetalReflectFloor`; stepped spec) |
| Depth texture / Opaque texture | off / off | off / off | off / off |
| Shader LOD | 300 | 300 | 150 |

### Reflection probes per arena
One baked probe per zone, non-blended (each renderer takes the single most important probe), box projection off: the play diorama probe (bounds = island top + 6 m up, placed at diorama centre ~2 m up), resolution 256 (High) / 128 (Mid), HDR; one probe for the sky/satellite islands at 64. Zones do not overlap. Re-bake when arena layout changes. Wrecker gets its own anchor override to the arena probe. No realtime probes.

**Rule (Art, steel metallic 1 "never renders dark"):** every arena scene ships (a) its baked probes above, baked before merge, and (b) Environment Reflections = Custom `Assets/_Project/VFX/Lighting/T_SkyFallback_Cube.png`, a bright gradient-sky cubemap (#8CC8FF / #DCEFFF / #6FAE5A, generated, import notes in that folder's README). It is never the black default skybox. As a backstop, CrashverseGlossyPBR applies `_MetalReflectFloor` (default #9AA3AF) as `max()` on the environment specular, weighted by metallic, on both High/Mid and Low. Modeler keeps steel albedo ≥ #9AA3AF without a probe.

### Post (one global Volume per tier)
- Tonemapping **ACES**, post-exposure **0** (Modeler preview rig matches). Colour grade that keeps the bright palette: Colour Adjustments saturation +15, contrast +10 (compensates ACES desaturation), White Balance temperature +5; Lift/Gamma/Gain neutral; Shadows-Midtones-Highlights: shadows tinted slightly to #5B6FA8 (≤5%).
- Bloom: threshold 1.0, intensity 0.4 (High; Mid same, low-quality filtering), scatter 0.6; Core emissive ≈2.5 so only Core/power-ups bloom.
- No vignette, no DoF in gameplay, no motion blur, no chromatic aberration. Low: no post; ACES look approximated by authored albedo.
- Preview rig (Modeler, agreed): sun ≈1.2 from upper back-left, gradient ambient, one reflection probe, ACES exposure 0, bloom threshold 1 / intensity 0.4, SSAO off (AO from ORM), no DoF.

## 4 · Mobile budgets per quality tier (`QualityTier` Low/Mid/High)
| Budget | Low | Mid | High |
|---|---|---|---|
| Particles: **one global pool** = 150 impacts + 80 active power-up, oldest culled first (`ParticleBudget`) | 115 (×0.5) | 230 | 230 |
| Debris RBs (03, approved) | 25 | 60 | 90 |
| Break-FX spawns | 3 / 100 ms, queue ≤200 ms (all tiers) | | |
| Transparent overdraw target (avg / worst) | 1.5× / 3× | 2× / 4× | 2.5× / 5× |
| Full-screen passes | 0 (TF tint via UI image) | 1 | 1 |
| FX texture size | 256, atlas | 512 atlas | 512 atlas (1024 for TF crack) |
| Prop textures (albedo/normal/ORM) | 512 (mip-bias import) ASTC 8×8, normal/ORM not sampled | 1024 ASTC 6×6 (normal ASTC 5×5) | 1024 ASTC 6×6 (normal 5×5) |
| Shader variants | GlossyPBR: see §4a (124 declared, 32 shipped in the APK) | | |
| Draw calls / SetPass gameplay | ≤120 / ≤40 | ≤150 / ≤50 | ≤200 / ≤60 |
Debris fade = scale to 0 (no alpha) per 03. Dust = 1 flipbook quad on Low.

## 4a · GlossyPBR variant count (QA #2, after removing SSAO / box projection / probe blending)
Keyword sets per pass (`_` = keyword off). Fog `multi_compile_fog` = {_, FOG_LINEAR, FOG_EXP, FOG_EXP2}; instancing = {_, INSTANCING_ON}.
| SubShader / pass | Keyword sets | Declared | Shipped (per tier) |
|---|---|---|---|
| LOD 300 ForwardLit | shadows {_, _MAIN_LIGHT_SHADOWS, _MAIN_LIGHT_SHADOWS_CASCADE} × soft {_, _SHADOWS_SOFT} × {_, LIGHTMAP_ON} × fog 4 × inst 2 | 3×2×2×4×2 = **96** | High: {CASCADE} × {SOFT} × LM 2 × fog {_, LINEAR} 2 × inst 2 = **8**; Mid: {_MAIN_LIGHT_SHADOWS} × {SOFT} × 2 × 2 × 2 = **8** |
| LOD 300 ShadowCaster | {_, _CASTING_PUNCTUAL_LIGHT_SHADOW} × inst 2 | **4** | punctual stripped (no additional lights): **2** |
| LOD 300 DepthOnly | inst 2 | **2** | **2** |
| LOD 150 ForwardLitLow | {_, _MAIN_LIGHT_SHADOWS} × fog 4 × inst 2 | **16** | Low: {_, _MAIN_LIGHT_SHADOWS} (shadows can be off on Low) × fog 2 × inst 2 = **8** |
| LOD 150 ShadowCaster | {_, _CASTING_PUNCTUAL_LIGHT_SHADOW} × inst 2 | **4** | **2** |
| LOD 150 DepthOnly | inst 2 | **2** | **2** |
| **Total** | | **124** (was 798: ForwardLit 768 with SSAO ×2, box projection ×2, blending ×2, plus DepthNormals 2) | **APK (union of the three URP assets): ForwardLit 16 + ShadowCaster 2 + DepthOnly 2 + ForwardLitLow 8 + ShadowCaster 2 + DepthOnly 2 = 32**. A single tier uses 8 + 2 + 2 (High or Mid) or 8 + 2 + 2 (Low). |
Shipped counts assume Graphics settings fog = Linear only (strips FOG_EXP/EXP2), additional lights off in all three URP assets, and High = 2 cascades + soft, Mid = 1 cascade + soft, Low = hard or off. These are pre-stage counts (vertex/fragment programs are deduplicated further for `multi_compile_fragment`/`_vertex`).

**Stripping note (IPreprocessShaders):** URP's own stripper removes features no URP asset in the build enables (SSAO, additional lights, decals). For our own guarantee, an editor `IPreprocessShaders` (Developer/VFX, `Scripts/VFX/Editor`, `callbackOrder` after URP's) should, for `Crashverse/GlossyPBR` only: drop `FOG_EXP`/`FOG_EXP2`; drop `_CASTING_PUNCTUAL_LIGHT_SHADOW`; in LOD 300 passes keep `_SHADOWS_SOFT` on whenever main light shadows are on (drop shadows-without-soft), and drop the shadows-off ForwardLit variant (High/Mid always have shadows); log the final count and fail the build if GlossyPBR > 32. Not added in this PR (needs a Unity build to verify; tracked as Q-D8).

## 4b · Texture & mesh authoring conventions (agreed with 3D Modeler)
- **Per asset:** 1024² albedo (sRGB), normal (**OpenGL Y+**, linear, Unity "Normal map" import), ORM (linear: **R occlusion, G roughness, B metallic**). Low tier gets the same files with Max Size 512 via platform override.
- **Texel density (3D Modeler, final numbers in the models README on branch `3d-modeler/models-v3`):** Wrecker_v2 ≈256 px/m on a 1024–2048 texture set; building kit 128–256 px/m on tiling wood, brick and steel sheets; island = unique 1024 macro map + tiling detail. Fragments share their parent's texture.
- **Material values:** steel metallic 1, roughness 0.3; wood/brick metallic 0, roughness 0.55–0.75; gold trim metallic 1, roughness 0.25. Albedo stays bright (sRGB 50–240), no baked lighting in albedo except AO in ORM.R.
- **Vertex colour:** R = damage mask (cracks/darken only where R>0), A = flex weight (hit wobble). G/B free.
- **UVs:** UV0 unique, non-overlapping; UV2 lightmap UVs on static pieces (Unity "Generate Lightmap UVs" allowed).
- **Slots:** separate emissive slot `M_Wrecker_Emissive` (Core/Module/Booster glow; HDR emission ≈2.5). Fragments carry an `M_<Mat>_Inner` slot → same shader with `_InnerFace = 1`, `_InnerColor` (the material's dark colour), roughness 0.9.
- **Wrecker sockets (empties):** `Muzzle`, `Core`, `Module`, `Booster`, `WheelDust` (VFX attach points).
- **Crack masks:** VFX authors per material (R step1, G step2) on UV0, 512².
- **Naming:** `T_<Asset>_A`, `T_<Asset>_N`, `T_<Asset>_ORM`, `M_<Mat>`, `M_<Mat>_Inner`.

## 4c · v3 hero render checklist (sign-off before an asset ships)
1. Rendered in the preview rig (above) on URP_High and URP_Low.
2. Albedo-only view: no baked shadows/highlights; values in range.
3. Normal map: green up (OpenGL); no seams across UV islands; bevels read at gameplay distance (≈18 m).
4. ORM: AO not crushed (min ≥0.35), steel reads metallic with sharp sky reflection from the probe, wood/brick read rough.
5. Silhouette and toon terminator read in the shade tint (#5B6FA8), rim visible from back-left.
6. Damage steps 1/2 look right with vertex R mask; inner faces show `M_<Mat>_Inner`.
7. Core emissive blooms, environment never cyan.
8. Low tier: still readable without normal/ORM; triangle and texture budgets met.
9. Turntable + gameplay-camera screenshot attached to the PR.

## 5 · Impacts, materials, dust (delivered: `Scripts/VFX/VfxCore.cs`, `VfxDirector.cs`)
Timings in ms (frames @60). Event column = audio/anim_audio_map.csv; VFX fire on the same event at frame 0 unless stated.
| Event (sfx) | VFX | Particles (Mid; Low ×0.5) | Life | Flash | Reduce motion |
|---|---|---|---|---|---|
| IMPACT_WOOD / BREAK_WOOD (sfx_wood_break) | splinters #F5C994 + dust #CFA779, squash 0.92 80 ms | 12 | 400 | warm white 2 f | flash ×0.4, ≥100 ms ramp; counts unchanged |
| IMPACT_BRICK / BREAK_BRICK | chunks body #D2664C, chipped faces #E9997C, dust #D9A37E; crack step decal | 16 | 600 | 2 f; hit stop 33 ms on break (Feel) | flash ×0.4; hit stop ≤33 ms/0.3 (Feel) |
| IMPACT_STEEL / BREAK_STEEL | sparks #FFD27A (additive) + bits #3A3F48; BREAK_STEEL 24 sparks | 10 / 24 | 300 | white 2 f | flash ×0.4 |
| IMPACT_STEEL_JOINT / BREAK_STEEL_JOINT | gold ring #E8B04B decal + 24 spark burst; joint pop 1→1.15→0 150 ms | 24 | 300 | gold ring 4 f | ring alpha ≤0.5, ×0.4 |
| IMPACT_TERRAIN | dust puff #C98A4B | 6 | 400 | none | unchanged |
| EXPLODE_CRATE (alias EXPLODE_BARREL) @ 6 f/100 ms after swell | fireball red/white 400 ms + smoke 1.2 s | 20 + 8 smoke | 400/1200 | fireball core | core ×0.4, no strobe |
| DAMAGE_STEP (material, step) | shader `_DamageStep` swap, no particles | 0 | — | — | unchanged |
| DEBRIS_BURST (material, count) | small dust puff at burst | 6 | 800 | — | unchanged |
| COLLAPSE_CREAK 0–150 ms | 1 dust puff at seam (off on Low) | 4 | 600 | — | unchanged |
| COLLAPSE_LAND (wood/brick/steel) | ground dust ring tinted by material | 18 (Low: flipbook) | 1200 | — | dust unchanged, **no shake** |
| DEBRIS_FALL_OFF | none (despawn kill plane +2 s) | — | — | — | — |
Debris lifecycle (03): active ≤2.5 s → rest 1.5 s → scale-fade 500 ms → pool. Projectile trail: cyan #22D3EE 0.35 s, width 0.25→0 (Low: 8-segment line).
`VfxDirector` pools per material (no Instantiate after warm-up; oldest recycled), rate-limits break FX (3/100 ms), uses unscaled time so hit stop/slow-mo/Time Fracture never stretch FX lifetimes beyond spec. Payload `VfxHit(position, material)` optional (see OPEN_QUESTIONS Q-D2).

## 5b · Particle effects & damage feedback (delivered in vfx/particles-damage)
Code-built: `VfxRecipes` (data) → `VfxParticleBuilder` (ParticleSystem) at runtime in `VfxDirector` pools, or **Crashverse/VFX/Build Effect Prefabs** (editor) → `Assets/_Project/VFX/Prefabs/VFX_<Id>.prefab`. Textures generated by `vfx/tools/gen_vfx_textures.py` (original). Bursts only, world space, unscaled time, maxParticleSize 0.5, Fixed-mode gradients = flat toon steps.
| Effect id | Trigger | Layers (Mid count) | Colours | Life | Low | Reduce motion |
|---|---|---|---|---|---|---|
| Explosion | EXPLODE_CRATE / PHYS_* Explosive | core flash 1, fireball puffs 10 (3 steps), shock ring 1, chunks 8, smoke 8 (+150 ms) | #FFE27A/#FF7A1A/#E0402A, ring #FFF6E0 | 0.4 / 1.2 s | ×0.5 | flash+ring ×0.4, α≤0.5, ≥100 ms |
| Impact_Wood | IMPACT/BREAK_WOOD, PHYS wood | hit 1, splinters 6, dust 6 | #F5C994, dust #CFA779 | 0.4 s | ×0.5 | flash ×0.4 |
| Impact_Brick | IMPACT/BREAK_BRICK | hit 1, chunks 8, dust 8 | #D2664C/#E9997C, dust #D9A37E | 0.6 s | ×0.5 | flash ×0.4 |
| Sparks_Steel | IMPACT/BREAK_STEEL | hit 1, sparks 7 (stretched, additive), bits 3 | #FFD27A, bits #3A3F48 | 0.3 s | ×0.5 | flash ×0.4 |
| Sparks_Joint | PHYS_JOINT_SNAP, *_STEEL_JOINT | gold ring 1 (4 f), sparks 24 | #E8B04B, #FFD27A | 0.3 s | ×0.5 | ring α≤0.5 |
| Glass_Shards | PHYS_* Glass | hit 1, shards 10 (glossy atlas row 1, white glint), glint 4 | #9FD8EE/#E6FAFF | 0.55 s | ×0.5 | flash ×0.4 |
| Impact_Stone | PHYS_* Stone (Sim MaterialType 5), MaterialKind.Stone, fractureClass "Stone" or "Roof", IMPACT/BREAK_STONE (Animator confirmed) | hit 1 (white, 2 f), chips 8 (toon chunks), dust 8 (3 steps) | chips #C9B48A/#A08A62, dust #D4C49E (steps #E8DCC0/#D4C49E/#B09C74), **Art ruling #10 sandstone** | dust 0.6 s | ×0.5 | flash ×0.4, α≤0.5, ≥100 ms |
| Dust_Wood/Brick/Terrain/Glass/Stone | debris impacts, DEBRIS_BURST, COLLAPSE_START | puffs 6 | #CFA779 / #D9A37E / #C98A4B / white / #D4C49E | 0.4–0.8 s (stone 0.6 s) | ×0.5 | unchanged |
| Collapse_Dust | COLLAPSE_LAND / PHYS_COLLAPSE_END | ring puffs 18 tinted by dominant material | DustTint | 1.2 s | ×0.5 | unchanged, no shake |
| Muzzle_Wrecker | ANIM_WRECKER_FIRE f0 at muzzle_socket | flash quad 3 f 0.6→1.3, sparks 6, smoke 3 @2 f | Core cyan #22D3EE, centre #E0FBFF, rim #E8B04B | 50 ms / 0.5 s | sparks off | ×0.4, no scale pop |
Budget: impacts lane 150 (Low 75) + power-up lane 80 (Low 40) = global 230, oldest culled first; break/debris spawns ≤3 per 100 ms. Light debris rattles spawn no particles.
**Damage feedback** (`DamageFeedback` on the piece; PHYS_IMPACT → hit flash, PHYS_CRACK → step): `_HitFlash` peak 0.5/0.7/0.85 by tier (cap 0.85), 2 f (joint 4 f), RM ×0.4 with ≥100 ms ease-out; `_DamageStep` 0/1/2 from physics crack step 1→1, 2–3→2, never decreases; `_CrackMap` = `T_CrackMask_Wedge` (R sparse, G dense); `_DarkenPerStep` 0.10 (20 % at step 2; glass 0.05); `_CrackColor` wood #6B4226, brick #7A3326, steel #3A3F48, glass #E6FAFF, stone #5E4A30 (Art ruling #10). `VfxMaterial`: Wood 0, Brick 1, Steel 2, SteelJoint 3, Terrain 4, Explosive 5, Glass 6, **Stone 7** (append only; Stone = 7 and IMPACT_STONE / BREAK_STONE confirmed by Animator). A piece whose `ImportedPhysicsHints.fractureClass` maps to a VFX material (Wood/Brick/Steel/Glass/Stone, and Roof → Stone) gets it as its `DamageFeedback` default; an explicit assignment in code still wins.

**Roof (PM decision):** there is no Roof `VfxMaterial` (and no Roof value in the Physics/Gameplay enums, which Physics owns). Terracotta-tile roofs (`fractureClass "Roof"`) use **Stone** FX with a per-piece dust colour: `DamageFeedback.dustOverride` = #D9A28C (set from the hints), and `VfxDirector` recolours only the `dust` layer of that piece's Stone effects with `VfxIds.DustSteps(#D9A28C)` = #EBC8B4 / #D9A28C / #B48167 (lit/shade derived per channel the same way as the Art stone steps). Chips, flash and cracks stay Stone. The override applies only while the piece's material is Stone, so a non-Stone material assigned in code never gets the tint. Collapse dust (no piece id) stays sandstone. **Wooden-shingle roofs are tagged `Wood`** and use the Wood palette.
### Glass, Stone, Roof (material tables)
| Material | Albedo | Smoothness | Metallic | Debris | Crack |
|---|---|---|---|---|---|
| Glass | #CFF3FF, α via GlossyPBR opaque tint (no transparency on Low) | 0.9 | 0 | shards #9FD8EE + glint #FFFFFF | #E6FAFF lines |
| Stone (Art ruling #10, warm sandstone) | #C9B48A, seams #8A7350 (hero kit atlas) | — | 0 | chips #C9B48A/#A08A62, dust #D4C49E | #5E4A30 |
| Roof, terracotta tiles (PM: uses Stone) | tile #C8553D (hero kit atlas) | — | 0 | Stone FX; dust layer recoloured #D9A28C (steps #EBC8B4/#D9A28C/#B48167) | Stone #5E4A30 |

## 6 · Power-ups (spec only in v1)
Values from 04_powerups.md; sync from anim_audio_map.csv. Common arm: slot part pulses in slot colour on `PU_ARM_<NAME>`; `PU_DISARM` = 150 ms fade-out (not Time Fracture).
| Power-up | Timeline (ms / f@60) | Sync events | Shader/tech | Low tier | Reduce motion |
|---|---|---|---|---|---|
| Shockwave (Core cyan) | 0: cyan flash 3 f · 0–400/24 f ring 0→connected bounds, alpha 1→0 (ease-out cubic) · link flashes 1 f staggered 16 ms (max 12) | PU_SHOCKWAVE_IMPACT (sound listens to IMPACT only), _RING | ring mesh + fresnel energy shader | single mesh ring, link flashes off | ring alpha ≤0.5, flash ×0.4 ≥100 ms; link flashes replaced by **one 100 ms ease-out tint per activation, alpha ≤0.5** |
| Splitstrike (Module violet) | split point: 66/4 f violet pop, scale 1→1.3→split (ease-out-back) · 3 violet trails · child impacts at 0.8 scale | PU_SPLIT (not raised if hit before split) | additive pop sprite | trails 8 seg | pop ×0.4, no overshoot scale (1→1.1) |
| Gravity Well (Module violet) | 0–150/9 f vortex disc 0→4 m (ease-out-back) · 150–1500 swirl loop · 1500/90 f 100 ms implode (ease-in expo) + 300 ms burst ring | PU_GRAVITY_OPEN, LOOP @9 f, BURST @90 f | UV-swirl disc, fresnel | 1 flipbook quad, no debris trails | swirl speed ×0.5, burst no shake, alpha ≤0.5 |
| Chain Reactor (Core cyan) | 3 m sphere pulse 200 ms (ease-out) · arc line to each target +80 ms by distance · targets use EXPLODE_CRATE | PU_CHAIN_PULSE, PU_CHAIN_LINK +5 f/80 ms per link | line renderer arcs, sphere fresnel | arcs 4 segments | arcs alpha ≤0.5, no flicker >3 Hz (static arcs) |
| Phase Piercer (Booster orange) | projectile ghost: orange fresnel 60% opacity · ripple decal 150 ms on pieces 1–2 · 3rd: solidify 1 f + normal impact | PU_PHASE_PASS ×2, PU_PHASE_HIT (DEV hook needed) | fresnel transparent (1 extra transparent draw) | ripple off, ghost kept | solidify as 100 ms ramp |
| Time Fracture (Booster orange) | on **arm**: 0–200/12 f crack overlay from Booster ring + env desat 30% · 4000/240 f · warn last 500 ms crack pulse 2 Hz (skipped if extended) · end 200 ms shatter-out | PU_TIME_START @0 on arm, PU_TIME_WARN 210 f/3500, PU_TIME_END 240 f/4000 or at that shot's impact | 1 full-screen pass (crack + desat) | UI-image tint, no full-screen pass | static 30 % env desaturation under a static 25 % tint; tint fades **out** over 300 ms at end; no crack animation; warn = colour change only |
Time Fracture slows moving parts only; FX on those parts use their local speed, global FX use unscaled time.

## 7 · Reduce shake / Reduce motion (animation/07, `MotionSettings`)
- VFX never add camera shake; shake is FeelDirector's (Reduce shake **or** Reduce motion ⇒ 0).
- Reduce motion ON (read live from `MotionSettings.Current`, no per-system toggle): flashes ×0.4 / alpha ≤0.5 / ≥100 ms ramps (`VfxRules.FlashPeak/FlashSeconds`); no full-screen flashes; Time Fracture static tint; repeating pulses ≤3 Hz as colour change, amplitude ×0.5; particle counts and dust **unchanged** (02/03).
- Gameplay tells (shutter tell, armed pulses) stay visible as colour changes.

## 8 · Delivered files
`vfx/VFX_Shader_Spec_v1.md`, `vfx/OPEN_QUESTIONS.md`, `vfx/compile_check/VfxCompileCheck.csproj`, `Assets/_Project/VFX/Shaders/CrashverseGlossyPBR.shader`, `.../CrashverseGlossyToon.shader` (fallback), `.../CrashverseVfxParticle.shader`, `Assets/_Project/Scripts/VFX/VfxCore.cs`, `VfxDirector.cs`. Compile check: `dotnet build vfx/compile_check` (stubs; not a Unity compile; shaders need a Unity import to validate).

Device testing (Mali/Adreno) waits for the Unity licence and the CI APK. VFX owns crack masks and FX sprite sheets (original, free tools: Krita/Blender).
