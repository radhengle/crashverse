# CRASHVERSE Style Board v3: Realism direction (DRAFT, pending PM and 3D Modeler check)
Owner: Art & UI · Date: 24 Oct 2026 IST · Board image: `style_board_v3.png` (1920x1080) · Mood sources: `src/`

## 0. What changes and what doesn't
| Changes (v3) | Stays exactly as-is |
|---|---|
| Buildings, props, terrain and trees move from glossy/boxy stylized to **realistic PBR** (CC0 scans OK, after grading) | **The whole UI** (`/workspace/art/v2`, `/workspace/art/ui_v2`, brand wordmark in `/workspace/art/v1/brand`, provider login assets in `/workspace/art/v2/brand`). No reskin. |
| Real material detail: brick bond, mortar depth, sandstone grain, wood grain, brushed steel | **Palette:** navy #0B1530/#14285C/#22408A, charcoal #1C2230/#3A3F48, gold #F5C451/#FFD23F, orange #FF7A1A, cyan #22D3EE, violet #8B5CF6 |
| Shading is physically based (URP Lit), not toon | **Lighting:** sunny 11:00 midday, deep blue sky, bright and saturated |
| Trees: real species shapes instead of lollipop/low-poly | **Wrecker and Guardian shapes** (same silhouettes, slots, gold bands). Only their materials go realistic. |
| | Floating-island world, gameplay camera, cyan = player/Wrecker only |

Target feel: **"bright, sunny, high-end realistic"**. Think a clean, freshly-built miniature town photographed at noon. **Not** gritty, dusty, war-torn or desaturated. Realism is in the *detail and shading*, not in the *mood*.

## 1. Buildings: real material reads
- Every surface must read as its real material at a glance: brick shows running bond + recessed mortar; sandstone shows cut blocks, soft grain and chipped arrises; wood shows planks/grain and end-grain on cut edges; steel shows brushed panels, rivets and sharp reflections; granite shows a fine speckle and polished faces.
- Detail scale must be **legible at phone size**: brick courses and stone blocks are big enough to count from the gameplay camera. Micro-detail (pores, hairline cracks) goes in normal maps only, never in albedo contrast.
- Proportions stay game-friendly: slightly chunky walls, clear roof shapes, flags/pointy roofs on targets. Real materials on readable shapes, not a real-world-accurate building.
- Damage: real fracture surfaces (raw brick interior #E9997C, fresh sandstone face lighter than weathered face, splintered wood #F5C994). Crack stages stay at **2 visible steps** (Physics 1 → 1, 2–3 → 2).

## 2. Locked material targets (albedo, sRGB, measured on the **graded** base colour, no lighting)
| Material | Albedo target | Allowed range (avg) | Metal / Rough | Notes |
|---|---|---|---|---|
| Brick | **#D2664C** | ±6% value, hue 8–16° | 0 / 0.75–0.85 | Mortar **#7A3326**, recessed, always visible. Chips/interior #E9997C |
| Breakable stone (sandstone) | **≈#C9B48A** | #BFA97D–#D6C29A | 0 / 0.7–0.85 | Warm, light, visible block lines, chipped edges. Hit FX palette = warm sandstone (as sent for PR #53) |
| Indestructible base (granite) | **#4A4F5C** | #42474F–#545A67 | 0 / 0.35–0.5 | Dark, cool, one solid mass, **gold trim band #F5C451** (metal 1, rough 0.25). **Never cracks**, never chips |
| Steel | **#AEB6C2** (base colour = F0 tint) | #A4ACB8–#BAC2CE | **1 / 0.3** | **Reflection probe required in every arena** (or bright sky fallback). Min-brightness floor so it never renders dark. Sparks #FFD27A |
| Wood | **≈#E3A766** | #D99A58–#EBB476 | 0 / 0.6–0.75 | Warm honey, plank lines #6B4226, raw edges #F5C994. Weakest material, lightest-feeling |
| Roof (terracotta) | ≈#D9703F | slightly brighter/more orange than brick | 0 / 0.6–0.7 | Must not merge with brick. Wooden-shingle roofs use Wood |
| Glass | #9FD8EE body / #E6FAFF edge | n/a | 0 / 0.05 | Paler than cyan Core, **no glow** |
| Grass / foliage | #5FBF3A–#3E9A2B | see §5 | 0 / 0.6–0.8 | Lush, saturated |
| Cliff underside | #C98A4B → #4A2E18 | | 0 / 0.85 | Indestructible terrain, darker than wood |

Exact hex is the **average** albedo of the texture. Scan variation around it is fine (see §3).

## 3. Albedo value ranges (keep scans bright)
- Non-metal albedo, sRGB: **min 50, max 240** on any pixel; **average value (HSV V) per material ≥ 0.55**, except granite bases (V ≈ 0.36) and cliff shadow side.
- Max in-texture variation: ±12% value around the material average for brick/stone/wood; ±6% for granite (it should read as one solid mass).
- No baked lighting, shadows or AO in albedo. De-light every scan (Agisoft De-Lighter / Substance / manual) before grading.
- Check with a Unity albedo validator (Scene view > Validate Albedo) or `albedo_check.py` in this folder; fail anything that drops below the floor.

## 4. Saturation and grading rules
- **Grade scans up toward the palette**: shift hue to the target, then raise saturation until the average matches the locked hex. Raw CC0 scans are almost always too grey and too dark.
- Saturation floor (HSV S, average): brick ≥ 0.55, wood ≥ 0.50, sandstone 0.28–0.40, grass/foliage ≥ 0.55. Granite and steel are the only low-saturation surfaces.
- **AO/dirt budget is light**: AO map strength ≤ 0.5 in-engine, dirt/grime overlays ≤ 10% coverage and only in crevices; no soot, moss streaks, rust runs, graffiti or wet stains on gameplay buildings.
- Weathering reads as "lived-in and clean": soft edge wear only.

## 5. Trees and foliage
- Real species silhouettes: broad oak (rounded mass), conifer/pine (tall cone), birch (slender, white bark), palm on Sunny Shores, flowering accent tree (pink/white), round shrubs. Each island uses 2–3 species max for clarity.
- **Lush saturated greens**: lit leaves #7BD34A–#5FBF3A, mid #3E9A2B, shade tinted toward #2E6E3A / blue, never olive, brown or grey. No dead leaves, no autumn colours outside a themed season.
- Canopy reads as clustered masses (alpha cards grouped into clumps), not noise. Silhouette must read at phone size.
- Wind: gentle sway only (vertex shader), off when Reduce motion is on.
- Trees are scenery unless the level marks them destructible; destructible trees use Wood hit FX.

## 6. Lighting and post
- Sun: 11:00 midday, warm white key ≈#FFF4E0, high angle (≈55–60°), soft-ish shadows. Sky gradient #4FB8FF → #FFE3A3 at horizon. Ambient: gradient or skybox, bright.
- Shade tint stays cool and light (#5B6FA8 influence); shadows must never go below sRGB ~45 on lit-material albedo.
- **Tonemap: Neutral (locked)**, on all tiers (Graphics Developer, 2026-10-24). Post exposure 0, contrast +8, saturation +8. Bloom threshold 1.1 HDR (Core/FX only). Sun #FFF4E0 at 55–60°. Sky gradient #4FB8FF → #FFE3A3.
- **Bloom only on the Core and FX**: threshold ≥ 1.1 (HDR) so lit sandstone/steel/sky never bloom. Emissive allowed only on cyan Core, Booster/Module glow, explosions, sparks, Guardian weak points.
- No heavy vignette, no film grain, no chromatic aberration, no DOF in gameplay.
- Reflection probes: one per arena (baked), required for steel and gold.

## 7. Readability rules (gameplay first)
- **Breakable vs indestructible differ by value**: breakable materials are light/mid (brick, sandstone, wood), indestructible bases are dark (granite #4A4F5C) with a gold band. **Must stay distinct in greyscale**; check every arena with a greyscale screenshot.
- **Cyan #22D3EE stays unique** to the Core/player/Wrecker energy. No cyan in environment albedo, sky reflections on steel are fine, glass stays paler (#9FD8EE) and never glows.
- Silhouettes read at phone size (gameplay camera, 1080x1920 on a ~6" screen): roof shapes, towers, weak-point gold rings and explosive crates must be identifiable from shape alone.
- Detail density lower than real life: max ~2 detail frequencies per surface (block/course + subtle grain).
- Weak points: gold ring #E8B04B, unchanged. Explosives: #E5484D + white band, unchanged.

## 8. Mobile budgets (SUPERSEDED, see "Mobile budgets: LOCKED" at the end of this doc)
| Item | Suggested |
|---|---|
| Texel density | ~256 px/m for buildings at gameplay distance (512 px/m only for hero close-ups) |
| Building material sets | 1024² per tiling material (albedo, normal, packed mask: metal/AO/smooth). 512² for small props (as M_Brick 512 today) |
| Trees | 1024² atlas per species group, alpha-tested, ≤ 2 materials per tree |
| Compression | ASTC 6x6 (albedo), ASTC 5x5 or 4x4 (normals) |
| Tris (LOD0) | Hero building ≤ 15–20k, standard building ≤ 5–8k, tree ≤ 3–5k, Wrecker ≤ 15k |
| LODs | 3 levels (LOD0/1/2 ≈ 100% / 50% / 20%), cull far trees to billboards |
| Draw calls | Keep shared materials + SRP Batcher; target ≤ 150 batches in gameplay |
| Fracture pieces | Pre-fractured chunks reuse the parent material set; inner faces use a tiling "interior" material |
All numbers are starting points for discussion; **Graphics/Tech Art to confirm against device targets**.

## 9. Wrecker and Guardians
- **Same shapes, realistic materials.** Wrecker v2 stays a cannon on wheels with visible Core (cyan orb at pivot), Module (violet pod at breech), Booster (orange ring at muzzle) and gold bands.
- Wrecker materials: barrel navy-painted metal #3E4C6E (metal 0.6, rough 0.35, light edge wear), carriage oiled wood #D9965A, gold bands #E8B04B (metal 1, rough 0.25), Core emissive cyan (the only blooming element on it).
- Guardians (Tidewarden and others): keep silhouettes and colour blocking; skin/armour become realistic (wet stone, coral, metal) but keep the red/orange boss accents and emissive weak points.

## 10. Do / Don't
**Do**
- De-light and grade every scan up to the locked hex before import.
- Keep noon sun, blue sky, bright saturated surfaces.
- Keep breakable light, indestructible dark + gold band; test in greyscale.
- Keep cyan for the player only; keep bloom on Core/FX only.
- Use real species and real material detail at a readable scale.
- Reuse material sets to keep batching.

**Don't**
- Don't ship raw grey/brown scans, wet/dirty/war-zone looks, heavy AO or soot.
- Don't use cyan or glowing glass in the environment.
- Don't let lit stone, steel or sky bloom.
- Don't change the UI, wordmark or palette.
- Don't change Wrecker or Guardian shapes.
- Don't use photo textures with baked shadows, people, signage, logos or text.
- Don't use anything from `/workspace/art/_reference_DO_NOT_SHIP/`.

## 11. Open questions / to confirm
- Tonemapper: Neutral (locked by Graphics Developer).
- Budgets: locked by Graphics Developer (see the end of this doc).
- CC0 sources and licence log: 3D Modeler (CC0 only, e.g. Poly Haven / ambientCG; keep a source list).
- 3D Modeler realism test renders: none found in `/workspace/crashverse_previews/` at time of writing (latest `hero_buildings_v2` is still stylized). Review on arrival against §2–§4 and §7.
- Brick #D2664C and roof #D9703F have almost the same greyscale value. That's fine because both are breakable, but roofs must separate from walls by hue and shape (and the roof hit FX palette), not by value.

## 12. Board and sources
- `style_board_v3.png`: built by `build_board_v3.py` (PIL). Panels: town mood, material detail row, locked albedo chips (with greyscale corner chips), gameplay-camera mood with a greyscale inset, trees mood, palette strip, v2 login inset (`/workspace/art/v2/screens/01_login.png`, unchanged), key rules.
- `src/mood_01_town.png` (Canva media MAHY7cl7EEQ), `src/mood_02_trees.png` (MAHY7VnjtLM), `src/mood_03_swatches.png` (MAHY7drb-18), `src/mood_04_gameplay.png` (MAHY7bPxoDI). All original text-to-image generations, no reference image uploaded; nothing from `_reference_DO_NOT_SHIP`.
- Mood images show the **direction**, not exact values: the granite swatch in `mood_03` is darker and more speckled than #4A4F5C, and the town mood has more grime in the cliff than allowed. **The hex table in §2 always wins.**
- `albedo_check.py`: quick PIL check of a graded base-colour texture against §2–§4.

## LOCKED 2026-10-24 (PM approved)
- Wrecker body: navy painted metal, metallic 0.6, roughness 0.35.
- Wrecker gold bands: metallic 1.0, roughness 0.25.
- Graphics Developer locked the Neutral tonemapper. Mobile budgets are locked; see the section below.

## Mobile budgets: LOCKED by Graphics Developer, 2026-10-24 (replaces the guidance budgets above)
- Texel density: about 256 px per metre.
- Android texture sizes (PM cap): tiling materials max 1024, props max 512.
- Compression: ASTC 6x6 for albedo, ASTC 4x4 to 5x5 for normal maps.
- Packed maps use the _ORM layout: R = AO, G = roughness, B = metallic.
- Triangles: hero buildings 15–20k, standard buildings 5–8k.
- Trees: LOD0 max 5k, LOD1 2.5k, LOD2 800, LOD3 impostor/billboard. Bushes are half of these.
- Batches: 150 or fewer on Mid/High, 100 or fewer on Low.
- Bloom threshold: 1.1 HDR (Core and FX only).
- These are enforced by import rules and tests in the integration-branch PR.
