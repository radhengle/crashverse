# Building prefabs → Ch1 / Daily Titan layouts (rev 7, Level Designer)

Source: `origin/3d-modeler/realistic-v1` @ 9e3f4e3, read with `git show` only. `inventory.py` → `prefab_inventory.json`;
`prefab_map.py` → `prefab_map.json` + `PREFAB_MAP.md` (every layout piece → module, FBX, scale, yaw, fracture class).

## What 3D Modeler has landed
There are **no Unity `.prefab` files** on the branch. A "prefab" is an assembly JSON (`Assets/_Project/Models/Buildings/Bld_*.layout.json`,
format `crashverse.building_layout` v1, Unity metres, Y up) placing kit-module FBX. Every module ships an intact FBX and a
`_Fractured` FBX (fragments with UCX convex hulls, `JNT_A__B` joint empties, `ANCHOR_Ground/Support` empties). Building
`connections` carry contact points, with `break_force_N` / `break_torque_Nm` still blank. The same FBX sets are already in integration/v2.

| Building | Pieces | Size x/y/z m (whole assembly) | Modules (fracture class) | Breakup | Status |
|---|---|---|---|---|---|
| Bld_Townhouse | 19 | 7.2 × 9.3 × 6.1 (house core 5 × 8.8 × 5) | Kit_House GroundDoor/GroundWin (Stone) 4 × 2.8, Floor (Wood) 5 × 0.34 × 5.2, Upper1/Upper2 (Wood) 4.6 × 2.78, Gable (Wood), Roof (Roof) ×2, Chimney (Stone) 5.34 tall; Props Shed, Barrel ×2, Crate, CrateSmall | Stone 7 frags, Wood 12, Roof 8 | stylized modular **landed**; realistic `Models/Realistic/Buildings/Hero/Bld_Townhouse` is **one intact mesh** (not breakable yet) |
| bakery / shop | — | unknown | — | — | **not on the branch**; only the townhouse ground floor ("shop sign, lantern") |
| Bld_Watchtower | 15 | 6.8 × 20.2 × 6.0 | Kit_Tower RingDoor/RingSlit/RingWindow (Stone) ⌀4.16 × 3, Floor ×4 (Wood), Crown (Stone) 1.42, Roof (Roof) 6.8 tall; Shed, props | Stone 7, Wood 12, Roof 9 | stylized **landed**; realistic **waiting** |
| Bld_CastleKeep | 52 | 11.0 × 21.3 × 11.0 | Kit_Keep Wall/WallDoor/WallSlit/WallBanner/WallBalcony (Stone) 4.2 × 3, Floor (Wood) 5.6, Turret/TurretCrown/TurretRoof, Crown | Stone 7, Wood 12, Roof 12 | stylized **landed**; realistic **waiting** |
| Bld_Gatehouse (castle wall) | 31 | 32.4 × 18.3 × 4.9 | Kit_Gate Arch, Portcullis (Steel), Wall 4 × 6, Crown, Tower* | Stone 7, Steel 7, Roof 12 | stylized **landed** (never fits the frame whole; used as wall segments); realistic **waiting** |
| Kit_Real{Brick,Wood,Steel} Block/Wall/Floor/Joint, RealRoof, RealProps | — | Block 1.02 × 1 × 1, Wall 3 × 3 × 0.22–0.27, Floor 3 × 0.18–0.27 × 3, Joint 0.32 × 3 × 0.3 | Brick / Wood / Steel | 7–18 frags, 6–54 joints | **landed** (generic pieces for every other level) |

Pivots: every module is bottom-centre (Blender z-up, z-min 0) except Kit_Tower_Floor and Kit_Keep_Floor (top-centre, the slab hangs
below) and a few meshes that dip 0.06–0.47 m below the pivot (House_Roof eaves, Tower_RingSlit, RealProps_Barrel).
Kit_Real* fragment origins are the fragment's centre of mass. Mass ranges per module are in `prefab_inventory.json`.

## How each level uses them (rev 7)
- **L3 bakery → Bld_Townhouse at 0.8 scale** (geometry changed): ids G_F/G_B/G_L/G_R, Floor1, U_F/U_B/U_L/U_R, Roof_W/Roof_E, Chimney,
  plus the bakery oven. Same props → slabs support graph, same 5 shots, line, 3★ and mastery.
- **L7 tower → Bld_Watchtower at ~0.55 scale** + the spike fix (geometry changed): RingDoor/RingSlit quarters, gold steel rings at the
  storey seams, Floor_4, clock room (RingWindow), Crown, spire Roof, Annex shed, Crate + CrateSmall, two barrels, hoist poles.
- **L9 lighthouse → Watchtower modules; seawall → Gatehouse Wall/Crown** (mapping only: the proportions already match at ~0.5 scale).
- **Daily Titan arenas → Kit_Keep modules** (turret legs/arms, keep-wall body), mapping only.
- **L1, L2, L4, L5, L6, L8, L10 → generic Kit_Real* modules** (mapping only).
- **Waiting:** L8 market stalls on a bakery/shop prefab (no asset, no planned footprint, so the block layout stays);
  L3 on the realistic townhouse split (and a dedicated bakery if one is planned); L7/L9 on a realistic tower/gatehouse;
  Daily Titan on a realistic keep. L10 has no building prefab (boss art).

## Code gaps for Developer (nothing here is consumed at runtime yet; no code was written)
1. `ContentModels.cs:52` **BlockData has no model field** (kit / module / fbx / prefab id) **and no rotation**: side walls need yaw 90°.
   Suggest `string kit; float rotY;`, kept optional so cube fallback still works.
2. `LevelBuilder.cs:35` **always `GameObject.CreatePrimitive(PrimitiveType.Cube)`** scaled to `bd.s`. To use a module it needs to instantiate
   the intact FBX (non-uniform scale from `prefab_map.json`), keep a BoxCollider of size `s` so gameplay and the sim stay identical,
   and on break swap to the `_Fractured` FBX (fragments + UCX hulls) as debris only.
3. **Fracture data is unused**: no runtime reads the `JNT_A__B` / `ANCHOR_*` empties or building `connections`; support stays `BlockData.sup`.
   Break forces in the layouts are blank (3D Modeler).
4. `GameSession.cs:351 EnsureHeroSlot` is **presentation only** (it destroys colliders at line 362), loads from `Resources/HeroBuildings/`
   (no assets there), and supports one keep|gatehouse per level; `DailyBossData` (ContentModels.cs:142) has no heroSlot at all.
   So `heroSlot` cannot place destructible pieces.
5. `CrashverseImportConventions.cs:115 KitPathOk` only accepts `Models/BuildingKit/`; the Kit_Real* sets live under `Models/Realistic/`
   and would fail the validator (CrashverseImportValidator.cs:64) once routed through it.
6. The realistic Bld_Townhouse is a single intact mesh (3D Modeler): it needs splitting into the Kit_House module set before any level can break it.
