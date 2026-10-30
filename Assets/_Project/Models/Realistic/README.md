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

- **Props:** the layout's `props[]` lists 3 bread crates (BreadCrate1-3, one stacked on 1+2) and 1 × `Prop_Bench.fbx` (indestructible) in front of the window. The crates are the L3 destructible decoys: `Props/Props_Village/Kit_RealBread_Crate.fbx` (LOD0-2, 356 tris) + `Kit_RealBread_Crate_Fractured.fbx` (wood, 8 fragments, 740 tris, 0.9-2.5 kg, JNT break forces blank, ANCHOR Ground; fracture_class Wood). Same mesh and Props_Village UVs as the indestructible `Prop_BreadCrate.fbx`; material M_RealBread / M_RealBread_Inner on T_Props_Village (`texture_set` in `models/HeroBuildings/Kit_RealBread_stats.json`). The kit crate clustering cannot reach 8 pieces on a 0.62 × 0.42 × 0.30 m crate with the 0.15 m min axis, so it is cut on a 4 × 2 grid (one fragment per cell; `Tools/Blender/realistic/bread_crate.py`).
- **Dimensions** (`dimensions_m` in the layout and in `Kit_RealBakery_stats.json`, measured from the exported module tops): eaves 6.30 m, ridge 8.66 m, chimney top 10.17 m.
- **Layout:** `Bld_Bakery.layout.json`. Stats: `models/HeroBuildings/Kit_RealBakery_stats.json`.

## Kit_RealStall + Bld_MarketArch (L8, realistic v1, 2026-10-30)
Built by `Tools/Blender/realistic/market.py`. One material `M_RealStall` (`Kit_RealStall/Textures/T_RealStall_*`, 512, two stripe cells), and the arch uses it too.
- **Kit_RealStall** (`Buildings/Kit_RealStall/`, `Kit_RealStall.layout.json`):
  - Keystn: wooden keystone skid, 1.5 × 1.8 × 0.22 m, Ground, 720 LOD0 tris, pivot exactly on its bottom face (y = 0), deck top at y = 0.22 = the stall pivot. It is a separate prop piece; when it goes, the stall falls.
  - StallA / StallB: 1.6 × 2.17 m overall INCLUDING the awning (frame 1.5 × 1.8), top 2.40 m, 1,124 LOD0 tris each, resting on Keystn. They have identical meshes and the same material; only the awning UVs differ (red Stripe cell vs blue StripeB cell). Place one of them.
- **Bld_MarketArch** (`Buildings/Bld_MarketArch/`, `Bld_MarketArch.layout.json`):
  - Pieces: Pads (granite, indestructible), PostL, PostR (timber + knee brace + steel shoe), Beam (beam + cap board + 4 lantern hooks) and Sign (steel sign box, no text, gold border, bolted to the beam cap on two steel bracket frames: base plates, uprights into the box, strap plates, diagonal stays).
  - Size: the posts' outer faces are 10.4 m apart, with a 9.6 m clear span and 3.5 m clear height. The beam overhangs to 11.1 m and the sign top is at 4.93 m.
  - `stall_slots[]` gives 4 stall positions on a 1.6 m pitch (x = ±0.8, ±2.4); lantern hooks on the beam sit over the slots. Arch span unchanged (10.4 m overall, 9.6 m clear).
  - LOD0 1,324 tris.
- **Stats:** `models/HeroBuildings/Kit_RealStall_stats.json`, shared by both.
- **Pending:** the vertex colour (Col) tint vs damage-mask question is unchanged (Graphics), and KitPathOk is with Developer.

## Bld_StoneTower (realistic v1, L7 `m1_clockwork_tower` / L9 lighthouse)

- **Folder:** `Buildings/Bld_StoneTower/` (`Kit_RealTower_<piece>.fbx` + `_Fractured.fbx`, `Bld_StoneTower.layout.json`). Builder: `Tools/Blender/realistic/tower.py` on the shared castle library `castle_lib.py`.
- **Look:** square sandstone watchtower, 2 stone storeys with quoins, putlog holes, cross arrow slits and a round-arched oak door (dressed voussoirs, iron straps and studs, granite steps, own indestructible piece), iron-bound oak ring beams between storeys, 2 red/gold banners on iron hooks over storey 2, a corbelled stone crown, timber hoarding with loopholes, a crenellated parapet and a terracotta pyramid roof on a plank deck with hip caps, eaves plate and finial.
- **Atlas:** shared castle atlas `Buildings/RealCastle/Textures/T_RealCastle_A/N/ORM` (1024, `cells.json` beside it; graded CC0 sandstone, wood, terracotta, rock + procedural granite, steel, gold and a Banner cell). Material `M_RealTower` / `M_RealTower_Inner`; `models/HeroBuildings/Kit_RealTower_stats.json` carries `"texture_set":"RealCastle"`, which `validate_realism.py` now reads. The keep, gate and wall kits use the same atlas.
- **Level fit:** module ids = the level block ids, sizes = block sizes. Place the root at the plinth top (Unity 0, 1.2, 0) with no rotation and every module lands on its block (door = wallF1 at Unity -X). Total height 10.2 m above the root = crownRoof top 11.4. For L9 use the same modules with scale (0.85, 0.89, 0.85) on lhBase (see `level_fit` in the layout).
- **Topple:** each storey rests on the module below (fragment ANCHOR `Support`), so a hit on storey 1 or a ring brings down everything above.
- **Tris:** LOD0 15,476 (hero budget 15-20k), LOD1 9,808, LOD2 3,576, fractured 12,912. Every FBX ≤ 1,500 LOD0.
- **Breakage:** stone walls, crown and parapet → Stone chunks (4-10); rings and hoarding → Wood (8-16); roof → Roof (tiles and deck/timber, 16 each half).

| Module (level id) | Pieces | Footprint m | Height m | Top m | LOD0 | Fracture | Rests on |
|---|---|---|---|---|---|---|---|
| doorSteps | Steps | 0.43 × 1.20 | 0.50 | 0.50 | 88 | Indestructible  | Ground |
| ring0 | R0 | 2.63 × 2.63 | 0.50 | 0.50 | 1,068 | Wood 16 | Ground |
| wallF1 | W1F | 0.45 × 2.62 | 2.30 | 2.76 | 1,114 | Stone 10 | ring0 |
| wallB1 | W1B | 0.39 × 2.62 | 2.26 | 2.75 | 784 | Stone 10 | ring0 |
| wallL1 | W1L | 1.91 × 0.38 | 2.26 | 2.75 | 344 | Stone 4 | ring0 |
| wallR1 | W1R | 1.91 × 0.38 | 2.26 | 2.75 | 388 | Stone 4 | ring0 |
| ring1 | R1 | 2.63 × 2.63 | 0.50 | 3.25 | 1,088 | Wood 16 | wallB1, wallF1, wallL1, wallR1 |
| wallF2 | W2F | 0.48 × 2.62 | 2.25 | 5.50 | 872 | Stone 10 | ring1 |
| wallB2 | W2B | 0.39 × 2.62 | 2.25 | 5.50 | 634 | Stone 10 | ring1 |
| wallL2 | W2L | 1.91 × 0.38 | 2.26 | 5.51 | 406 | Stone 4 | ring1 |
| wallR2 | W2R | 1.90 × 0.38 | 2.26 | 5.50 | 414 | Stone 4 | ring1 |
| ring2 | R2 | 2.63 × 2.63 | 0.50 | 6.00 | 988 | Wood 16 | wallB2, wallF2, wallL2, wallR2 |
| crownFloor | CrFl | 3.20 × 3.20 | 0.40 | 6.40 | 1,164 | Stone 10 | ring2 |
| crownWood | CrWdA, CrWdB | 2.83 × 2.83 | 1.40 | 7.80 | 2,364 | Wood 11+11 | crownFloor |
| crownBrick | CrBk | 2.41 × 2.41 | 0.81 | 8.61 | 912 | Stone 6 | crownWood |
| crownRoof | CrRfA, CrRfB | 2.71 × 2.71 | 2.18 | 10.65 | 2,848 | Roof 16+16 | crownBrick |
