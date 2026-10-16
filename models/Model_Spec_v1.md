# CRASHVERSE – Model Spec v1 (3D Modeler, 16 Oct 2026 IST)

> **v3 budgets (PM, 2026-10-16):** Wrecker 12k, island 10k, kit pieces 500–1.5k, fragments < 400 tris. These match PR #28's validation report (hero 12,000, environment 10,000). Tidewarden 6k + 2k LOD1 unchanged.
The design doc (`crashverse_share.txt`) gives no numeric poly or texture budgets. The budgets below are set by 3D for Android mid-range (URP, Mali-G52 class) and are open for Developer/PM review.
Style sources: `art/v2` (FINAL board, bright glossy toy look) and `art/m1b` (Wrecker v2 dimensions, Sunnyville arena, material colours).

## 1. Budgets per asset class (APPROVED by PM, 16 Oct 2026)
| Class | Tris (LOD0) | LOD1 | Texture | Materials |
|---|---|---|---|---|
| Hero: Wrecker (incl. wheels + slot parts) | **≤ 12,000** (v3) | LOD1 + LOD2 required (per-file) | 2048 set (Albedo/ORM/Emissive) target ~256 px/m | 2 (M_Wrecker + M_Wrecker_Emissive) |
| Wrecker slot parts (Core, Module, Booster) | inside hero 3k (guide ≤ 400 each) | – | shared | shared |
| Building kit piece, intact (wall / floor / joint) | **500–1,500** (v3) | ≤ 50% of LOD0 | 512 tiling sheet per material (Albedo/Normal/ORM, 256 px/m) + 256 inner | M_<Mat> (+ M_<Mat>_Inner on fragments) |
| Fracture fragment | **< 400** (v3; optional _lo skipped) | – | same as parent | same as parent |
| Island (arena, incl. satellites, trees, houses, VFX placeholder) | **≤ 10,000** (v3) | – | unique 1024 macro + 256 tiling grass/rock/sand detail | M_Island (+ M_VFX_Placeholder) |
| Props | ≤ 500 (guide) | ≤ 250 | shared palette | 1 |
| Boss: Tidewarden / Guardians | **≤ 6,000** | **≤ 2,000** | ≤ 1024 atlas (+ optional 512 emissive, later) | ≤ 2 |
| Colliders | UCX convex, ≤ 8 per piece, ≤ 255 tris each | | | |
| Scene guide (not a PM-approved figure) | ≤ 60k tris, ≤ 80 draw calls | | ASTC 6x6 | |

All hard edges get a 1–2 segment bevel (Art v2), counted inside these budgets.

## 2. Export and import contract (agreed with Developer)
- FBX is the Unity deliverable and lives in `Assets/_Project/Models/<Asset>/`. GLB is source/preview only and lives in `models/<Asset>/source/`. **.blend files are not in git** (no LFS): they live in `/workspace/crashverse/models_src/<Asset>/`, and `*.blend` is in .gitignore.
- FBX export: **Forward -Z, Up Y, Apply Transform ON (bake space transform), scale 1.0, FBX_SCALE_ALL**, 1 unit = 1 m, no embedded media (path mode Relative).
- In Unity the imported transform should read **rotation (0,0,0), scale 1**. No Unity-side scale or rotation fix is needed.
- Root pivot sits at the ground contact point, +Z forward (Unity). Blender authoring convention: forward = -Y, up = +Z, left = +X.
- **Hierarchy depth is 1** (all objects are direct children of the root). Blender 4.2's bake-space-transform exporter mis-rotates objects nested 2+ levels deep, which was verified by a round-trip test. Intended runtime parents are stored in the custom property `attach_to`.
- One material per texture set, named `M_<Set>` (M_Wrecker, M_Wrecker_Emissive, M_Island, M_Wood, M_Brick, M_Steel, M_<Mat>_Inner). See section 5 for v3 shader/maps. Fracture inner faces use `M_<Material>_Inner` (Physics spec).
- Textures are PNGs in `<Asset>/textures/`.
- Colliders are `UCX_<Mesh>_##` convex hulls (≤ 8 per piece, ≤ 255 tris each). Each fragment has exactly one `UCX_<FragmentName>` (exact name match).

## 3. Fracture spec (Physics Developer, provisional sign-off until a Unity import test)
1. Fragment smallest bbox axis ≥ 0.15 m and mass_kg ≥ 0.5.
2. Fragment count per object: wood 8–16, brick 12–24, steel 4–10, hard cap 32.
3. `JNT_<FragA>__<FragB>` empties at contacts between adjacent fragments, with custom props `break_force_N` and `break_torque_Nm` left blank (""). `ANCHOR_<Frag>` empties where a fragment touches the ground (`anchor_to`="Ground"). Anchors to other kit pieces are set at level-assembly time.
4. Fragment breakup edges get a 1-segment 0.025 m bevel. Inner and bevel faces use the M_<Material>_Inner slot (a slightly darker shade of the same material), each face on its own 0–1 UV island. Outer faces use M_Palette.
5. Intact kit pieces (and Kit_Base_Indestructible) require in-file `_LOD0`, `_LOD1` and `_LOD2`; the Wrecker and island use per-file `<Asset>_LOD0|1|2.fbx`. Fractured sets are LOD0 only (LOD-exempt in #28 @723ccb3). Fragments < 400 tris (#28); we keep them < 300. Each fractured set totals ≤ 1,500 tris. No `_frag_##_lo` versions.
6. Each fragment's pivot is at its own centre of mass. Units are m, +Y up (Unity).
7. `mass_kg` = density × volume, with wood 600, brick 1900, and **steel 800 kg/m³ (hollow effective density, Physics change)**. The custom property `density_kg_m3` is also exported.

## 4. Material colours (from art/m1b CHANGES.md)
Wood #E3A766, Brick #D2664C / mortar #7A3326, Steel #AEB6C2, Barrel navy #3E4C6E, Gold #E8B04B, Core #22D3EE, Module #8B5CF6, Booster #FF7A1A, Outline #101828, Grass #7ED957, Cliff #C98A4B (indestructible), Roof #4D7BC4. Smoothness is 0.5 on props and 0.7 on metal.

## 5. v3 shader, maps and mesh data (VFX final spec, 16 Oct 2026)
- **Shader:** CrashverseGlossyPBR, roughness workflow. Maps: Albedo (sRGB), tangent-space **Normal OpenGL Y+** (Unity "Normal map", no green flip), **ORM** linear with R = AO, G = roughness, B = metallic. Emissive (sRGB) on M_Wrecker_Emissive only.
- **Steel:** metallic 1.0, roughness ≈0.3, albedo mean #ADB5C1 (target #AEB6C2). It needs the reflection probe to read bright. This supersedes Art's earlier approval of metallic 0 / smoothness 0.7, so Art should re-confirm.
- **Gold** (bands, hub, rivets, cradle): metallic 1.0, roughness 0.3.
- **Brick:** face pattern lives in the M_Brick tiling sheet (0.5 × 0.25 m bricks, 0.04 m mortar groove + normal). It is **not** in a shared palette atlas any more (VFX tiling-sheet rule). It batches by sharing one M_Brick material across all brick pieces (SRP Batcher).
- **Texel density targets:** Wrecker ≈256 px/m on one 1024/2048 set. Kit 128–256 px/m via tiling sheets. Island: unique 1024 macro + tiling grass/rock/sand detail.
- **Vertex colour (Col, linear):** R = damage mask (default 0), G = B = 0, A = wind/flex weight (1 on tree canopies and bushes, 0 elsewhere).
- **UVs:** UV0 = texture UVs. **UV2 lightmap** (second channel, `UV2_Lightmap`, non-overlapping) on static meshes: island meshes and intact kit `_LOD0/_LOD1/_LOD2`. Fragments are dynamic and have none.
- **Wrecker sockets** (empties flat under root, `attach_to`): Socket_Muzzle, Socket_Core, Socket_Module, Socket_Booster (attach_to=Barrel), Socket_WheelDust (attach_to=Wrecker_v2, on the ground under the front axle). They replace the old Muzzle_Spawn.
- **Emissive:** Core/Module/Booster glow faces use material slot M_Wrecker_Emissive with T_Wrecker_Emissive_2048.
- **VFX_Waterfall_Placeholder:** 1.5 m ribbon + splash puff with its own M_VFX_Placeholder; V runs down the fall. VFX replaces it.
- **Preview lighting:** sun warm white from the upper back-left with soft shadows, gradient ambient (#8CC8FF / #DCEFFF / #6FAE5A), Filmic exposure 0 (Blender has no built-in ACES), bloom threshold 1.0, raytraced AO/reflections. Gameplay views have no DoF; beauty shots use light DoF.

## v3 import contract (PR #28 @723ccb3, vfx/IMPORT_PIPELINE.md)
- Kit files only under `Assets/_Project/Models/BuildingKit/Kit_<Mat>/`. The validation walls are the kit walls (`Kit_<Mat>_Wall[_Fractured].fbx`) for Wood, Brick, Steel, Glass and Stone.
- Textures `T_<X>_A|_N|_ORM|_E`. `M_<Mat>_Inner` reads `T_<Mat>_Inner_*` (`T_<Mat>_Inner_A.png` shipped).
- Hulls: `UCX_<Piece>_frag_##` exact per fragment; other `UCX_<Name>_##` resolves to `<Name>`. Joints `JNT_<FragA>__<FragB>[_##]` with `break_force_N`/`break_torque_Nm` (blank = Physics default). Anchors `ANCHOR_<Frag>` with `anchor_to="Ground"`.
- `Kit_Base_Indestructible` (Art #10): intact only, no fracture set, no `_Crack` map; M_Granite + M_Gold. Named with the `Kit_` prefix so #28 classifies it as Kit (a bare `Base_` prefix is an unknown class).
