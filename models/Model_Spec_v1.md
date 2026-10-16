# CRASHVERSE – Model Spec v1 (3D Modeler, 16 Oct 2026 IST)
The design doc (`crashverse_share.txt`) gives no numeric poly or texture budgets. The budgets below are set by 3D for Android mid-range (URP, Mali-G52 class) and are open for Developer/PM review.
Style sources: `art/v2` (FINAL board, bright glossy toy look) and `art/m1b` (Wrecker v2 dimensions, Sunnyville arena, material colours).

## 1. Budgets per asset class
| Class | Tris (LOD0) | LOD1 | Texture | Materials |
|---|---|---|---|---|
| Hero – Wrecker | ≤ 3,000 total incl. slots | optional ≤ 1,500 | 1× 256–512 palette atlas | 1 (M_Palette) |
| Wrecker modules / slot parts (Core, Module, Booster) | ≤ 400 each | – | shared hero atlas | shared |
| Building kit piece, intact (wall / floor / joint) | ≤ 300 | ≤ 50% of LOD0 | shared 256 palette + 256 inner | 2 (outer + M_<Mat>_Inner) |
| Fracture fragment | < 300 (target ≤ 60), optional _lo < 80 | – | same as parent | same as parent |
| Island (arena) | ≤ 2,500 incl. satellites | ≤ 1,200 | 256–512 palette | 1 |
| Props (barrel, crate, tower parts) | ≤ 500 | ≤ 250 | shared palette | 1 |
| Boss (Guardian / Tidewarden) | ≤ 6,000 | ≤ 3,000 | ≤ 1024 atlas + optional 512 emissive | ≤ 2 |
| Scene on screen | ≤ 60k tris, ≤ 80 draw calls (target) | | ASTC 6x6 (ASTC 4x4 for UI) | |
| Colliders | UCX convex hulls, ≤ 8 per piece, ≤ 255 tris each | | | |

## 2. Export and import contract (agreed with Developer)
- FBX is the Unity deliverable and lives in `Assets/_Project/Models/<Asset>/`. GLB and .blend are source/preview only and live in `models/<Asset>/source/`, never in Assets/.
- FBX export: **Forward -Z, Up Y, Apply Transform ON (bake space transform), scale 1.0, FBX_SCALE_ALL**, 1 unit = 1 m, no embedded media (path mode Relative).
- In Unity the imported transform should read **rotation (0,0,0), scale 1**. No Unity-side scale or rotation fix is needed.
- Root pivot sits at the ground contact point, +Z forward (Unity). Blender authoring convention: forward = -Y, up = +Z, left = +X.
- **Hierarchy depth is 1** (all objects are direct children of the root). Blender 4.2's bake-space-transform exporter mis-rotates objects nested 2+ levels deep, which was verified by a round-trip test. Intended runtime parents are stored in the custom property `attach_to`.
- One material per atlas, named `M_<Atlas>` (`M_Palette`). Fracture inner faces use `M_<Material>_Inner` (Physics spec).
- Textures are PNGs in `<Asset>/textures/`.
- Colliders are `UCX_<Mesh>_##` convex hulls (≤ 8 per piece, ≤ 255 tris each). Each fragment has exactly one `UCX_<FragmentName>` (exact name match).

## 3. Fracture spec (Physics Developer, provisional sign-off until a Unity import test)
1. Fragment smallest bbox axis ≥ 0.15 m and mass_kg ≥ 0.5.
2. Fragment count per object: wood 8–16, brick 12–24, steel 4–10, hard cap 32.
3. `JNT_<FragA>__<FragB>` empties at contacts between adjacent fragments, with custom props `break_force_N` and `break_torque_Nm` left blank (""). `ANCHOR_<Frag>` empties where a fragment touches the ground (`anchor_to`="Ground"). Anchors to other kit pieces are set at level-assembly time.
4. Inner faces use the M_<Material>_Inner slot, each face on its own 0–1 UV island. Outer faces use M_Palette.
5. Intact pieces have `_LOD0` and `_LOD1`. Fragments are < 300 tris. `_frag_##_lo` versions are skipped (approved): every fragment is ≤ 28 tris.
6. Each fragment's pivot is at its own centre of mass. Units are m, +Y up (Unity).
7. `mass_kg` = density × volume, with wood 600, brick 1900, and **steel 800 kg/m³ (hollow effective density, Physics change)**. The custom property `density_kg_m3` is also exported.

## 4. Material colours (from art/m1b CHANGES.md)
Wood #E3A766, Brick #D2664C / mortar #7A3326, Steel #AEB6C2, Barrel navy #3E4C6E, Gold #E8B04B, Core #22D3EE, Module #8B5CF6, Booster #FF7A1A, Outline #101828, Grass #7ED957, Cliff #C98A4B (indestructible), Roof #4D7BC4. Smoothness is 0.5 on props and 0.7 on metal.
