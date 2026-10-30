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
