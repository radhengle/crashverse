# Third-party assets (Season 1 Kingdom look, Phase 1)

Every third-party asset here is **CC0 1.0 (public domain)**. Nothing was bought, and nothing needs attribution, but the sources are listed below anyway.
The source files were downloaded at 1k resolution and colour-graded or resized by `Tools/Blender/kingdom/grade_textures.py`.
The graded results ship in `Assets/_Project/Resources/Kingdom/Textures/`.

| Shipped file | Source | Author / site | License |
|---|---|---|---|
| T_K_Sky.jpg | `kloofendal_48d_partly_cloudy_puresky` HDRI (tonemapped JPG, cropped to the upper hemisphere) | Poly Haven (polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky) | CC0 |
| T_K_Sand.jpg | `coast_sand_01` diffuse | Poly Haven | CC0 |
| T_K_Grass.jpg | `rocky_terrain_02` diffuse, regraded green | Poly Haven | CC0 |
| T_K_Rock.jpg | `rocky_terrain_02` / `rock_face_03` diffuse | Poly Haven | CC0 |
| T_K_Planks.jpg | `brown_planks_03` diffuse | Poly Haven | CC0 |
| T_K_Wood.jpg | `weathered_planks` diffuse | Poly Haven | CC0 |
| T_K_Stone.jpg | `castle_wall_varriation` diffuse | Poly Haven | CC0 |
| T_K_Brick.jpg | `red_brick_03` diffuse | Poly Haven | CC0 |
| T_K_Roof.jpg | `clay_roof_tiles_02` diffuse | Poly Haven | CC0 |
| T_K_Bark.jpg | `bark_brown_02` diffuse | Poly Haven | CC0 |

## Original (made for CRASHVERSE, no third party)

- `Resources/Kingdom/Meshes/*.bytes` (the cannon barrel and carriage, rocks, palms, the palace on Crown Rock, horizon isles). These were modelled procedurally in Blender 4.2 by `Tools/Blender/kingdom/build_assets.py` and exported in the CVM1 format by `cvm_export.py`.
- `T_K_Banner.png` (the Ironvale crimson banner with its gold crown) and `T_K_TNT.png` (the hidden-core crate). Both were generated in code by `grade_textures.py`.
- The shaders `VFX/Shaders/CrashverseSkyPano.shader` and `CrashverseWater.shader`.

No paid assets are used. No logos or trademarks are used.

## Repainted building atlases

The cells of `Models/Realistic/Buildings/{Bld_Bakery,Bld_Townhouse,Kit_RealStall,RealCastle}/Textures/T_*_A.png` were repainted by `Tools/Blender/kingdom/repaint_atlases.py`.
The new fills are graded Poly Haven CC0 diffuse textures:

- `red_brick_03`
- `castle_wall_varriation`
- `castle_wall_slates`
- `medieval_blocks_02`
- `brown_planks_03`
- `weathered_planks`
- `clay_roof_tiles_02`
- `rock_face_03`

The UV layouts did not change.

## PBR detail maps (Season 1 look, step 3)
`T_K_<Surface>_N.jpg` (normal, OpenGL) and `T_K_<Surface>_ORM.jpg` (Poly Haven "arm": AO / roughness / metal) in `Resources/Kingdom/Textures/`, 1k, unmodified, CC0 from Poly Haven: Sand `coast_sand_01`, Grass + Rock `rocky_terrain_02`, Planks `brown_planks_03`, Wood `weathered_planks`, Stone `castle_wall_varriation`, Brick `red_brick_03`, Roof `clay_roof_tiles_02`, Bark `bark_brown_02`. Android import max 512.
- `T_K_Metal.jpg` (greyscale remap), `T_K_Metal_N.jpg`, `T_K_Metal_ORM.jpg`: Poly Haven `rusty_metal_02` (CC0), for the worn iron barrel and brass bands.
- `T_K_CastIron.jpg` / `T_K_Bronze.jpg` (recoloured from the diffuse: near-black cast iron with rust only in the pits; dull aged bronze with dark patina) and their `_N` / `_ORM` maps: Poly Haven `rusty_metal_02` (CC0), for the cannon barrel, bands and carriage fittings.
