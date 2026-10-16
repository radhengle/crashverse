# CRASHVERSE Models v1 – for Developer
Branch `3d-modeler/models-v1`. All paths are relative to the repo root. The full contract is in `models/Model_Spec_v1.md`.
**The fracture spec has provisional sign-off from Physics Developer and is final only after Physics runs a Unity import.**

## Layout
- `Assets/_Project/Models/<Asset>/*.fbx` + `textures/*.png` are the Unity deliverables (FBX only).
- `models/<Asset>/source/*.glb|*.blend` are source/preview only (keep out of Assets/).
- `models/previews/*.png` contains one preview per asset plus `sheet.png`.
- `models/BuildingKit/validation/` holds the first 3 validation walls (not for shipping) plus validator output.
- `Tools/Blender/*.py` are headless Blender 4.2.3 build scripts (`blender -b --python <script>`): `cv_lib.py`, `build_walls.py`, `build_hero_island.py`, `render_previews.py`, `validate_fracture.py`, `validate_transforms.py`.

## Import notes (Unity)
- FBX settings: Forward -Z, Up Y, Apply Transform ON, scale 1.0. **After import each transform should read rotation (0,0,0), scale 1**, with 1 unit = 1 m. Root pivot is at ground contact and +Z is forward.
- The hierarchy is 1 level deep on purpose, because of a Blender exporter bug with nested objects. Objects carrying the custom property `attach_to` should be reparented at runtime or in the prefab, keeping their world pose.
- Materials: `M_Palette` (256² palette atlas, point filtering, smoothness about 0.5–0.7) and `M_<Mat>_Inner` (256² inner texture). Remap them to the URP GlossyToon shader (vfx) or URP/Lit. The textures are not embedded.
- Enable "Import custom properties / user properties" to read `mass_kg`, `density_kg_m3`, `attach_to`, `break_force_N`, `break_torque_Nm` and `anchor_to`.
- `UCX_*` meshes are collider hulls. Turn their renderers off and add MeshCollider (convex). `_LOD0`/`_LOD1` suffixes allow auto LODGroup.

## Assets
| Asset | FBX | Tris | Budget | Texture |
|---|---|---|---|---|
| Wrecker_v2 | `Assets/_Project/Models/Wrecker_v2/Wrecker_v2.fbx` | **1,444** (Carriage 228, Barrel 224, Wheel_FL/FR 196 each, Wheel_RL/RR 88 each, Slot_Core 284, Slot_Booster 128, Slot_Module 12) | ≤ 3,000 | T_Palette_256 |
| Island_Sunnyville | `Assets/_Project/Models/Island_Sunnyville/Island_Sunnyville.fbx` | **700** (main 464, satellites 118 + 118) | ≤ 2,500 | T_Palette_256 |
| Kit_<Wood/Brick/Steel>_<Wall/Floor/Joint> | `Assets/_Project/Models/BuildingKit/Kit_<Mat>/` | LOD0 44 / LOD1 12 each | ≤ 300 | T_Palette_256 + T_<Mat>_Inner_256 |
| …_Fractured | same folder | see the table below | frag < 300 | same |

### Wrecker_v2 (m1b dimensions, envelope about 4.0 × 2.0 × 2.4 m)
- Root `Wrecker_v2` is at ground centre. The others sit directly under it.
- `Carriage` is the wooden wedge with gold rivets. Its collider is `UCX_Carriage_01`.
- `Wheel_FL`, `Wheel_FR` are Ø1.2 m with steel rims, at (±0.95, 0.6, +0.15). `Wheel_RL`, `Wheel_RR` are Ø0.7 m rollers at (±0.6, 0.35, -1.35). Each wheel's pivot is its axle centre, so spin on local X. Positions are in Unity coordinates, +X = right.
- `Barrel` pivot is at (0, 1.5, 0), and its **local +Z runs along the muzzle**. Apply yaw (±60°, Y) and pitch (5–70°, X) on Barrel. The exported rest pose is horizontal; m1b rest elevation is 25°, so set that in the prefab. Collider: `UCX_Barrel_01`.
- `Slot_Core` (cyan orb + gold rim), `Slot_Module` (violet pod) and `Slot_Booster` (orange ring at 1.75 m along) have pivots at the barrel pivot, `attach_to=Barrel`.
- `Muzzle_Spawn` is an empty at booster centre + 0.2 m forward, `attach_to=Barrel`.

### Island_Sunnyville
- The top surface is a 60 × 25 m ellipse at y=0, with a grass cap and a stepped brown cliff underside about 14.6 m deep.
- Extras: sand pad and `Spawn_Wrecker` at z = -18 (behind the arena centre), 4 rocks, a waterfall strip on the +X edge, 2 satellite islands.
- Collider `UCX_Island_Sunnyville_Mesh_01` is a flat 1 m slab. `KillPlane_Hint` sits at y = -25.

### Building kit (3 m module grid)
Wall 3×3 m (wood 0.20, brick 0.25, steel 0.16 m thick). Floor 3×3 m (same thicknesses). Joint/column 3 m tall (0.3, 0.4 and 0.3 m square). Intact pivots are at the bottom centre.
| Fractured object | Frags (spec) | Max tris/frag | Total tris | mass_kg range | JNT | ANCHOR |
|---|---|---|---|---|---|---|
| Kit_Wood_Wall | 12 (8–16) | 20 | 192 | 77–116 | 23 | 4 |
| Kit_Wood_Floor | 9 (8–16) | 24 | 140 | 99–145 | 16 | 9 |
| Kit_Wood_Joint | 8 (8–16) | 12 | 96 | 18–25 | 7 | 1 |
| Kit_Brick_Wall | 18 (12–24) | 28 | 300 | 186–317 | 38 | 5 |
| Kit_Brick_Floor | 16 (12–24) | 24 | 264 | 209–315 | 33 | 16 |
| Kit_Brick_Joint | 12 (12–24) | 20 | 168 | 58–93 | 12 | 1 |
| Kit_Steel_Wall | 6 (4–10) | 16 | 88 | 159–230 | 9 | 3 |
| Kit_Steel_Floor | 6 (4–10) | 20 | 88 | 168–238 | 9 | 6 |
| Kit_Steel_Joint | 5 (4–10) | 12 | 60 | 36–49 | 4 | 1 |
Fragment naming is `<Piece>_frag_##`, with collider `UCX_<Piece>_frag_##` (sibling, same pivot = centre of mass). Joints are named `JNT_<FragA>__<FragB>`. Validator outputs are in `models/BuildingKit/*/validation_output.txt` and `models/transform_validation_output.txt` (all PASS).

## Known issues / TODO
- The Core orb (Ø0.9) sits at the barrel pivot as m1b specifies, so it is mostly buried in the barrel (Ø0.84) and reads as a cyan band. The m1b concept art shows it above the barrel. Art to confirm; it is a one-line move.
- Carriage is a simple wedge, with no outline shell (use a shader outline) and no emissive map for the Core yet.
- Floor fragments all touch y=0, so every one gets an ANCHOR. Re-anchor when floors are placed on walls.
- Unity import has not been tested here (no Unity on the box). Round-trip tests ran in Blender only.
- git-lfs is not installed. .blend and .glb files (about 20 MB) are committed as plain git objects.
