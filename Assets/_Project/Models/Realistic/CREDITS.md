# Realistic assets: sources and licences

Everything under `Assets/_Project/Models/Realistic/` uses original geometry and original procedural maps. The only third-party inputs are the CC0 scans below. Each scan was de-lit, then graded to the locked v3 albedo targets (`Tools/Blender/realism/grade.py`) and re-packed (ORM: R=AO at strength ≤0.5, G=roughness, B=metal). Nothing comes from `_reference_DO_NOT_SHIP`.

All of these sources are **CC0 1.0 (public domain)**, so no attribution is required. They are listed here so we can trace them.

| Used for | Source asset | Site / author | Licence | Downloaded |
|---|---|---|---|---|
| Sandstone wall (Townhouse ground floor, chimney) | Rustic Stone Wall (`rustic_stone_wall`) | Poly Haven, Dimitrios Savva | CC0 | 2026-10-24, 2k (diffuse, nor_gl, rough, AO, disp) |
| Plaster infill | White Rough Plaster (`white_rough_plaster`) | Poly Haven, Rob Tuytel | CC0 | 2026-10-24 |
| Timber beams, cart frame, crate battens | Rough Wood (`rough_wood`) | Poly Haven, Rob Tuytel | CC0 | 2026-10-24 |
| Planks (doors, shutters, deck, barrel, crates, cart bed) | Worn Planks (`worn_planks`) | Poly Haven, Dimitrios Savva | CC0 | 2026-10-24 |
| Roof tiles, ridge, chimney pots | Clay Floor 001 (`clay_floor_001`) | Poly Haven, Dimitrios Savva / Rico Cilliers | CC0 | 2026-10-24 |
| Dressed sandstone (quoins, sills, lintels) + rocks | Mossy Rock (`mossy_rock`), graded to sandstone/grey, moss graded out | Poly Haven, Rob Tuytel | CC0 | 2026-10-24 |
| Ground grass | Leafy Grass (`leafy_grass`) | Poly Haven, Charlotte Baglioni | CC0 | 2026-10-24 |
| Ground dirt | Brown Mud Leaves 01 (`brown_mud_leaves_01`) | Poly Haven, Rob Tuytel | CC0 | 2026-10-24 |
| Cobble path | Cobblestone Floor 08 (`cobblestone_floor_08`) | Poly Haven, Rob Tuytel | CC0 | 2026-10-24 |
| Oak and Bush bark | Jolcham Oak Bark 01 (`jolcham_oak_bark_01`) | Poly Haven, Charlotte Baglioni | CC0 | 2026-10-24 |
| Pine bark | Pine Bark (`pine_bark`) | Poly Haven, Dimitrios Savva | CC0 | 2026-10-24 |
| Oak and Bush leaves (composited into cluster cards) | Leaf Set 016 (`LeafSet016`, 2K-PNG) | ambientCG (Lennart Demes) | CC0 | 2026-10-24 |
| Pine needles (composited into branch cards) | Leaf Set 019 (`LeafSet019`, 2K-PNG) | ambientCG (Lennart Demes) | CC0 | 2026-10-24 |
| Preview lighting only (not shipped) | Kloofendal 48d Partly Cloudy (Pure Sky), 4k EXR | Poly Haven, Greg Zaal (sky edits: Jarod Guest) | CC0 | 2026-10-24 |
| Brick (Kit_RealBrick: L1-10 + Daily Boss blocks, walls, slabs, pillars) | Brick Wall 001 (`brick_wall_001`) | Poly Haven, Rob Tuytel / Dimitrios Savva | CC0 | 2026-10-30, 2k |
| Steel plate (Kit_RealSteel, prop hoops/straps) | Metal Plate (`metal_plate`) | Poly Haven, Rob Tuytel | CC0 | 2026-10-30, 2k (incl. metal) |
| Clay roof tiles (Kit_RealRoof) | Clay Roof Tiles (`clay_roof_tiles`) | Poly Haven, Amal Kumar | CC0 | 2026-10-30, 2k |
| Brick / roof fracture interior (T_*_Inner) | Clay Floor 001 (`clay_floor_001`), re-graded to #E9997C | Poly Haven | CC0 | 2026-10-24 |
| Wood fracture interior (splinter, T_RealWood_Inner) | Rough Wood (`rough_wood`), re-graded to #EBC28E | Poly Haven, Rob Tuytel | CC0 | 2026-10-24 |

Downloaded but **not used**: `rusty_metal_02` (Poly Haven) and `LeafSet026` (ambientCG). Rust is banned by style board §4, so the steel and gold are procedural.

Original (procedural, no third-party input): granite, steel, gold, window glass, end grain, grass blade cards, and all geometry.

Source pages: https://polyhaven.com/license and https://docs.ambientcg.com/license/ (both CC0 1.0).
