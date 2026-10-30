# Realistic assets: realism test v1 (24 Oct 2026)

All build scripts are in `Tools/Blender/realism/` and run headless with Blender 4.2. The order is:
`grade.py` → `atlas.py` → `foliage_tex.py` → `grass_tex.py` → `build_all.py` → `validate_realism.py` → `render_realism.py` → `compose.py`.
CC0 sources are listed in `CREDITS.md`.

## How the materials work (1 material per asset)
- **Tiling-cell atlas.** Each asset has one atlas, `T_<Asset>_A/_N/_ORM`. It is authored at 2048 and Android caps it at 1024. The atlas is split into cells, and each cell holds a whole number of repeats of one graded CC0 surface, with a 16 px wrapped gutter. Geometry is cut on that cell's grid in metres and UV'd per grid square, so a whole building tiles seamlessly from one material (one submesh). Texel density at 1024: plaster, wood and roof 240–248 px/m, sandstone 330, steel/gold/glass 224, granite 120 (granite is a uniform speckle).
- **ORM** is R = AO (strength ≤ 0.5 already applied), G = roughness (remapped into the Art v3 ranges), B = metal. Packing is scripted in `grade.pack_orm` and `pack_orm.py`. `pack_orm.py all-mask` writes Unity mask maps if the layout changes later.
- **Vertex colour `Col`** carries the per-piece tint (tile and stone variation) multiplied by ray-traced AO (strength 0.5, contact hits ignored). URP Lit ignores vertex colour, so Graphics needs a Lit Shader Graph that multiplies Base Map × vertex colour. Trees use `Col` for canopy depth AO.
- **UV2.** Buildings and props carry a lightmap UV2. Trees and grass carry wind data in UV2 (x = sway weight 0–1, y = phase).
- **Foliage material.** Alpha clip at 0.5, Render Face Both, no back-face normal flip (the cards use spherical custom normals). The LOD3 billboard uses its own material, `M_<Tree>_Billboard`.
- **Shared tiling surfaces** live in `Materials/Textures/T_<Surface>_A/_N/_ORM` at 1024, the shipped size. The 2048 graded masters are rebuilt by `grade.py` into `/workspace/crashverse/realism_masters` and are kept out of git.

## Assets
| Asset | Path | Tris LOD0 / 1 / 2 / 3 | Materials (submeshes per LOD) | Textures (authored → Android) |
|---|---|---|---|---|
| Bld_Townhouse (hero) | Buildings/Hero/Bld_Townhouse/ | 19,900 / 8,200 / 3,626 + 6 UCX | 1 (1) | A/N/ORM 2048 → 1024 |
| Oak | Foliage/Oak/ | 3,918 / 1,446 / 408 / 4 (billboard) | 1 per LOD (M_Oak; M_Oak_Billboard on LOD3) | atlas 2048 → 1024; billboard 1024×512 |
| Pine | Foliage/Pine/ | 2,472 / 938 / 176 / 4 | 1 per LOD | 2048 → 1024; billboard 512×1024 |
| Bush | Foliage/Bush/ | 1,150 / 288 / 92 / 4 | 1 per LOD | 2048 → 1024; billboard 1024×512 |
| GrassClump | Foliage/GrassClump/ | 6 / 4 | 1 | 2048 → 1024 |
| Prop_Barrel / Crate / Cart | Props/Props_Village/ | 732/216, 764/476, 2,036/884 | 1 shared (M_Props_Village) | 1024 → 512 |
| Prop_Rock_A/B/C | Props/Props_Village/ | 320 / 80 each | same shared material | same |
| Ground_Patch (44×44 m) | Props/Ground_Patch/ | 5,200 | 1 | 2048 → 1024 |

`realism_stats.json` holds the machine-readable numbers.

## Not done in this test
- The townhouse is a single intact mesh. It is not yet split into #28 breakable modules or fractured sets.
- Moss, damp at the base and dirt streaks were left out because Art v3 §4 bans them on gameplay buildings. Weathering is limited to soft AO, tile and stone tone variation, and dressed-stone edges.

## Realistic v1: L1-10 + Daily Boss pieces (branch 3d-modeler/realistic-v1)

Scope: `models/Realistic_Scope_L1-10.md`. LevelBuilder builds every level from scaled cubes by `mat`, and no hero building is referenced. So the set is
14 break-apart pieces, built most-used first. Each one reuses BuildingKit v3.1 geometry (LOD0-2 + UCX) and its fracture set (fragments with exact
`UCX_<frag>`, `JNT_A__B` with blank break_force_N/torque, `ANCHOR_<frag>` anchor_to Ground/Support, `mass_kg`, pivot = centre of mass).
The pieces are re-surfaced with graded CC0 tiling sets (`Tools/Blender/realistic/resurf.py`). The new `Block` module (1 m solid) uses the same `build_kit2.py` recipe.

| Folder | Material | Tiling set (1024, ORM) | tile_m |
|---|---|---|---|
| Buildings/Kit_RealBrick | M_RealBrick / _Inner | T_RealBrick (brick_wall_001), T_RealBrick_Inner (#E9997C) | 1.75 |
| Buildings/Kit_RealWood | M_RealWood / _Inner | T_RealWood (worn_planks), T_RealWood_Inner (#EBC28E) | 1.4 |
| Buildings/Kit_RealSteel | M_RealSteel / _Inner | T_RealSteel (metal_plate, metal 1, rough 0.25-0.35) | 1.5 |
| Buildings/Kit_RealRoof | M_RealRoof / _Inner | T_RealRoof (clay_roof_tiles) | 2.0 |
| Props/Kit_RealProps | M_RealProps / _Inner | T_RealProps 512 atlas (wood half, steel half for hoops) | 1.0 |

* One material per piece, plus `_Inner` on cut faces (kit convention). There is one shared material per kit, so all pieces of a kit batch together.
* UV0 is a metric box projection. After non-uniform per-block scaling, rebuild UV0 from the scaled positions (`uv = pos / tile_m` on the dominant axis),
  so texel density stays constant. `render_sheet.py` `assemble()` does exactly this for L3 and the Daily Boss.
* Vertex colour `Col` is unchanged from the kit (GlossyPBR: R = damage mask, A = flex).
* `fracture_class` is on every `_Fractured` root and fragment. The stats are in `models/HeroBuildings/Kit_Real<M>_stats.json`. Validator logs are in `models/validation_realistic/`.
* Importer note (VFX): `ImportConventions.KitPathOk` only allows `Kit_*` under `Models/BuildingKit/`. It needs to also accept `Kit_Real*` under `Models/Realistic/`,
  as the local `validate_colliders.py` now does. Until then the Unity validator reports a path error for these files.

## Bld_Townhouse, modular + destructible (realistic v1, 2026-10-30)
Folder: `Buildings/Bld_Townhouse/`. Built by `Tools/Blender/realistic/house_modular.py` from the approved realism-test townhouse
(`Tools/Blender/realism/townhouse.py`) and its graded CC0 atlas, with added balcony, flower boxes, lantern and hanging shop sign (icon only, no text).
- **Intact:** 19,692 LOD0 tris (LOD1 9,980, LOD2 4,982). Materials: `M_RealHouse` (T_RealHouse_A/N/ORM, 1024) on the intact pieces, plus `M_RealHouse_Inner` (T_RealHouse_Inner_*, 1024, fresh stone or splintered wood) on fracture faces.
- **Front:** the street front (shop door) faces Unity **+Z**. Left and right are as seen from the street; Annex and Roof_Left are on Unity +X.
- **Level Designer modules** (11). Pivot = centre of the module's base footprint, +Z = front. Every piece in a module shares that pivot, so placing all of a module's pieces at the module position assembles it.

| Module | Pieces (`Kit_RealHouse_<code>.fbx` + `_Fractured.fbx`) | Footprint X × Z (m) | Height (m) | Anchor |
|---|---|---|---|---|
| ShopFront | ShopW (window, flower box), ShopD (door, lantern, sign) | 6.41 × 1.28 | 2.86 | Base |
| Wall_Left | WallL | 0.57 × 4.31 | 2.86 | Base |
| Wall_Right | WallR | 0.57 × 4.31 | 2.86 | Base |
| Wall_Back | BackD, BackW | 6.41 × 0.57 | 2.86 | Base |
| Floor | Floor (slab, jetty boards, joists, brackets) | 6.42 × 5.08 | 1.01 | ground walls |
| UpperStorey | UpFL, UpFR, Balc, UpB, UpL, UpR, GabL, GabR | 6.66 × 6.44 | 4.98 | Floor |
| Roof_Left | RoofLF (street slope), RoofLB (back slope + ridge) | 3.80 × 6.54 | 3.33 | UpperStorey |
| Roof_Right | RoofRF, RoofRB | 3.87 × 6.54 | 3.33 | UpperStorey |
| Chimney | Chim | 0.87 × 0.72 | 4.00 | Roof_Right, UpperStorey |
| Base | Base (granite plinth, indestructible, no fractured set) | 7.04 × 6.04 | 0.35 | Ground |
| Annex (optional) | AnnexF, AnnexB | 2.25 × 3.81 | 3.14 | Ground |

Whole building footprint: 9.49 × 6.84 m with the annex (7.04 × 6.04 m plinth).

- **Layout:** `Bld_Townhouse.layout.json` (`crashverse.building_layout` v1). `modules[]` gives id, label, pivot, footprint_m, height_m, pieces, anchor_to and tris. `pieces[]` gives intact/fractured paths, position (= module pivot), rotation, anchor_to, fracture_class and tris. `connections[]` gives piece contacts, with break_force_N/break_torque_Nm left blank for Physics.
- **Prefab assembly:** create an empty `Bld_Townhouse` root, then one child empty per module at `modules[i].pivot`, with that module's piece FBXs at local (0,0,0) under it. At runtime, swap a piece for its `_Fractured` prefab, keeping the transform. For fragments, `ANCHOR_` with anchor_to=Support means "held while the module's `anchor_to` modules stand".
- **Fracture (#28/#52):** classes are Stone for ground walls and chimney (4–10 fragments), Wood for floor, upper walls, gables, balcony and annex (8–16), and Roof (8–16). Fragments are `<piece>_frag_##`, each with an exact convex `UCX_`, plus `JNT_<a>__<b>` (force/torque blank), `ANCHOR_` (Ground/Support), `mass_kg`, `density_kg_m3` and `fracture_class`. Pivot = volume centre of mass. Fragments are at least 0.15 m on every axis, under 300 tris each and at most 1,500 tris per set. Stats: `models/HeroBuildings/Kit_RealHouse_stats.json`.
- **Why a module has several pieces:** the #28 Kit budget is 1,500 LOD0 tris per FBX, and Blender/FBX names are capped at 63 characters, so `JNT_<fragA>__<fragB>` forces piece codes of 6 characters or fewer.
- **Pending:** the KitPathOk path exception (Models/Realistic) is owned by Developer. `CrashverseGlossyPBR` reads vertex colour R as the damage mask, but these meshes carry Col as a tint (as the realism test did), so a shader decision is needed.

## Bld_Bakery, modular + destructible (L3, realistic v1, 2026-10-30)
Folder: `Buildings/Bld_Bakery/`. Built by `Tools/Blender/realistic/bakery.py`, which uses the townhouse pipeline (`house_modular.py` building parts + `modkit.py` export).
- **Size:** walls 8 m (X) × 6 m (Z), 2 storeys. Eaves (wall top) 6.30 m, ridge 8.66 m, oven chimney top 10.17 m.
- **Colourway:** `T_RealBakery_*` (1024) is derived from the graded T_RealHouse set by `variant_atlas.py`, with no new sources. It has darker timber, cream plaster and a red/cream striped awning cell. The display window is 3.0 m with 4 panes and loaves on a display shelf. Roof pantiles are larger. Materials: `M_RealBakery`, plus `M_RealBakery_Inner`, which points at the shared `Bld_Townhouse/Textures/T_RealHouse_Inner_*`.
- **Facing:** the shop front faces Unity **+Z**; Level Designer rotates the root to face -X. Every module pivot is the centre of the module's base footprint. All pieces of a module share it.
- **Totals:** LOD0 18,216 tris, LOD1 9,964, LOD2 4,990; fractured sets total 18,538.

| Module | Pieces | Footprint X × Z (m) | Height (m) | Top (m) | LOD0 tris | Rests on |
|---|---|---|---|---|---|---|
| ShopFront | ShopW (display window + awning), ShopD (door, lantern, loaf sign) | 8.00 × 1.60 | 3.16 | 3.50 | 2,444 | Base |
| Wall_Left | WallL | 0.57 × 5.11 | 3.16 | 3.50 | 566 | Base |
| Wall_Right | WallR | 0.57 × 5.11 | 3.16 | 3.50 | 566 | Base |
| Wall_Back | BackD, BackW | 8.01 × 0.57 | 3.16 | 3.50 | 1,102 | Base |
| Floor | Floor (slab + jetty joists) | 8.02 × 5.88 | 1.01 | 3.54 | 1,376 | the 4 ground walls |
| UpperStorey | UpFL, UpFC, UpFR, UpB, UpL, UpR, GabL, GabR | 8.26 × 6.70 | 4.88 | 8.38 | 6,682 | Floor (falls when the ground walls go) |
| Roof_Left | RfLF, RfLB | 4.49 × 7.32 | 2.88 | 8.66 | 2,284 | UpperStorey |
| Roof_Right | RfRF, RfRB | 4.49 × 7.32 | 2.87 | 8.66 | 2,500 | UpperStorey |
| Chimney | Chim (oven flue at the back) | 1.24 × 0.94 | 6.87 | 10.17 | 288 | Floor, Roof_Left, UpperStorey |
| Base | Base (granite plinth, indestructible) | 8.64 × 6.84 | 0.35 | 0.35 | 408 | Ground |

- **Props:** the layout's `props[]` lists 3 × `Props/Props_Village/Prop_BreadCrate.fbx` (one stacked) and 1 × `Prop_Bench.fbx` in front of the window. They are new indestructible props on the existing Props_Village atlas.
- **Layout:** `Bld_Bakery.layout.json`. Stats: `models/HeroBuildings/Kit_RealBakery_stats.json`.
