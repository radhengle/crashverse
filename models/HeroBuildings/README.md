# Hero buildings (v1 phase 1, v2 detail pass)

Four original buildings in the style of `design/TARGET_UI_user_2026-10-24.png` (style only: none copies a building from the reference),
plus a shared prop kit. Every building is an assembly of **breakable modules** (#28 class `Kit`, `Kit_*_Fractured` = `KitFractured`)
placed by a layout JSON (`crashverse.building_layout` v1, same format as the v3 buildings).

| Building | Kit | Layout | Script |
|---|---|---|---|
| Bld_CastleKeep | Kit_Keep (+ Kit_Props) | Assets/_Project/Models/Buildings/Bld_CastleKeep.layout.json | Tools/Blender/hero/hero_keep.py |
| Bld_Watchtower | Kit_Tower (+ Kit_Props) | Assets/_Project/Models/Buildings/Bld_Watchtower.layout.json | Tools/Blender/hero/hero_tower.py |
| Bld_Gatehouse | Kit_Gate (+ Kit_Keep turret modules, Kit_Props) | Assets/_Project/Models/Buildings/Bld_Gatehouse.layout.json | Tools/Blender/hero/hero_gate.py |
| Bld_Townhouse | Kit_House (+ Kit_Props) | Assets/_Project/Models/Buildings/Bld_Townhouse.layout.json | Tools/Blender/hero/hero_house.py |
| (props) | Kit_Props: Barrel, Crate, CrateSmall, Shed (lean-to annex used by all four buildings) | – | Tools/Blender/hero/hero_props.py |

Per-module numbers (LOD0/1/2 tris, fragment count, max fragment tris, fractured total, mass range, joints, anchors, fracture class)
are in `Kit_<B>_stats.json` next to this file. `validate_fracture.py` reads the fracture class from there.

## Pipeline (Tools/Blender/hero/hero_lib.py)
* Modules are authored as convex parts (bevelled hulls) tagged with a material class
  (stone, stone_dk, roof, wood, wood_dk, paint, plaster, iron, gold, cloth, cloth_red, glass, leaf, lamp).
  Faces buried inside other parts, and module bottoms/tops that touch the next module, are removed from the intact LODs.
* High-poly is generated per face: stone blocks on world-z courses (round walls get courses that run around the ring),
  roof tile rows, planks, rivets, leaded glass, flowers, plus bevelled copies of everything else.
  It is baked to one atlas per building: `T_<B>_A / _N / _ORM` (2048).
  `T_<B>_Inner_A/N/ORM` is a per-class atlas for the inner (cut) faces of fragments, which use `M_<B>_Inner`.
* Inward faces of hollow walls and rings, downward faces and floors get less UV space, so the outside faces get more texels.
* LOD1/LOD2: small details are dropped, then the mesh is decimated to 50% and 20% of LOD0. `UCX_` hulls: up to 8 per module, ≤255 tris each.
* Fracture: Voronoi cells clip the structural parts. Details stay whole and go to the nearest cell.
  Each fragment gets an exact-match `UCX_<Piece>_frag_##`.
  `JNT_<A>__<B>` sits on shared cut planes (break_force_N / break_torque_Nm blank). `ANCHOR_<frag>` has anchor_to Ground or Support.
  `mass_kg` comes from the closed piece volume × class density. Budgets: fragment <300 tris, set ≤1500, counts in the class range.
* Layout JSON: Unity space (`C=[[-1,0,0],[0,0,1],[0,-1,0]]`), quaternion + ZXY euler. Each piece has anchor_to (Ground or support piece ids),
  plus a connections list and tris_total (LOD0/LOD1/LOD2/Fractured).

Rebuild: `blender -b --python Tools/Blender/hero/hero_<b>.py -- 2048`, then `... -- 2048 layout` (build props first, and keep before gate).
Renders: `blender -b --python Tools/Blender/hero/render_hero.py` writes to models/previews/hero_buildings_v1 and /workspace/crashverse_previews/hero_buildings_v1.

## v2 detail pass
* Stone HP: stones of varied length (0.26–0.95 m); about 30% of short stones are split into two half-height stones.
  Each stone gets a random tilt and protrusion (1.4–5 cm), 3.4 cm mortar joints and rounded 4.2 cm three-segment bevels.
  Round walls get courses that run around the ring.
  Bake rays were lengthened to 6 cm extrusion and 11.5 cm ray length to capture the deeper relief.
* Albedo: per-stone tone spread, about 15% cooler grey stones, about 6% dark stones, subtle worn edges.
  Dirt and moss build up at the base of ground-standing modules; moss patches sit on up-facing ledges.
  Roughness varies per stone and per tile, with moss and dirt rougher.
* Roof tiles are 2.6–3.8 cm thick with random tilt and jitter. Ridge caps run down the gate's pyramid roofs.
  Dormers were added to the keep turret roofs and the gate tower roofs.
* New details: ivy on walls and rings, a timber balcony (keep WallBalcony, house Upper2), flower boxes, beam ends,
  and a lean-to stone annex (Kit_Props_Shed) with chimney, door, shutters and ivy.
* Variety: the keep's back turrets are one storey taller; the watchtower has 4 storeys;
  the gatehouse's left tower is one storey taller; every building has an annex.
* Fragment cut edges are bevelled (3 cm chip) whenever the fragment stays under 300 tris and the set under 1500.
  The per-module count is `cut_edges_bevelled` in the stats JSON.
* `fracture_class` is written as an FBX custom property on every `Kit_*_Fractured` root. It overrides the stats JSON.
* Renders use the 1024 Android cap (LANCZOS-downscaled copies of the atlases).
  `Townhouse_detail_closeup_src2048.png` shows the same frame with the 2048 source maps.
  `maps_1024_check.png` (Tools/Blender/hero/maps_check.py) shows every atlas at 1024, whole and as a 1:1 crop.
