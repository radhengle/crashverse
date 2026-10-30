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
