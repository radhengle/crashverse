# Building prefabs → Ch1 / Daily Titan layouts (rev 8, Level Designer)

Source: `origin/3d-modeler/realistic-v1` @ e725121 (rev 7 used 9e3f4e3), read with `git show` only. `inventory.py` → `prefab_inventory.json`;
`prefab_map.py` → `prefab_map.json` + `PREFAB_MAP.md` (every layout piece → module, FBX, scale, yaw, fracture class).

## What 3D Modeler has landed
There are **no Unity `.prefab` files** on the branch. A "prefab" is an assembly JSON (`Assets/_Project/Models/Buildings/Bld_*.layout.json`,
format `crashverse.building_layout` v1, Unity metres, Y up) placing kit-module FBX. Every module ships an intact FBX and a
`_Fractured` FBX (fragments with UCX convex hulls, `JNT_A__B` joint empties, `ANCHOR_Ground/Support` empties). Building
`connections` carry contact points, with `break_force_N` / `break_torque_Nm` still blank. The same FBX sets are already in integration/v2.

| Building | Pieces | Size x/y/z m (whole assembly) | Modules (fracture class) | Breakup | Status |
|---|---|---|---|---|---|
| Bld_Townhouse (stylized) | 19 | 7.2 × 9.3 × 6.1 (house core 5 × 8.8 × 5) | Kit_House GroundDoor/GroundWin (Stone) 4 × 2.8, Floor (Wood) 5 × 0.34 × 5.2, Upper1/Upper2 (Wood) 4.6 × 2.78, Gable (Wood), Roof (Roof) ×2, Chimney (Stone) 5.34 tall; Props Shed, Barrel ×2, Crate, CrateSmall | Stone 7 frags, Wood 12, Roof 8 | stylized modular **landed** |
| **Bld_Townhouse (realistic, modular)** `Models/Realistic/Buildings/Bld_Townhouse/` | 11 LD modules / 23 pieces / 69 contacts | footprint 9.49 × 6.84 with annex (plinth 7.04 × 6.04); ridge 6.96, chimney top 9.55 | ShopFront (ShopW+ShopD, Stone) 6.41 × 1.28 × 2.86; Wall_Left/Right (Stone) 0.57 × 4.31; Wall_Back (BackD+BackW, Stone); Floor (Wood) 6.42 × 5.08 × 1.0; UpperStorey (UpFL, UpFR, Balc, UpB, UpL, UpR, GabL, GabR; Wood) 6.66 × 6.44 × 4.98; Roof_Left/Right (2 pieces each, Roof) 3.8 × 6.54 × 3.33; Chimney (Stone) 4.0 tall; Base (indestructible) 0.35; Annex (AnnexF+AnnexB, Wood, optional) | Stone 4–7 frags/piece, Wood 9–16, Roof 9–16; pivots = module base-footprint centre, +Z street; fragment pivot = COM | **landed @ e725121**, used by L3 (interim) |
| bakery / shop | — | unknown | — | — | **not on the branch**; only the townhouse ground floor ("shop sign, lantern") |
| Bld_Watchtower | 15 | 6.8 × 20.2 × 6.0 | Kit_Tower RingDoor/RingSlit/RingWindow (Stone) ⌀4.16 × 3, Floor ×4 (Wood), Crown (Stone) 1.42, Roof (Roof) 6.8 tall; Shed, props | Stone 7, Wood 12, Roof 9 | stylized **landed**; realistic **waiting** |
| Bld_CastleKeep | 52 | 11.0 × 21.3 × 11.0 | Kit_Keep Wall/WallDoor/WallSlit/WallBanner/WallBalcony (Stone) 4.2 × 3, Floor (Wood) 5.6, Turret/TurretCrown/TurretRoof, Crown | Stone 7, Wood 12, Roof 12 | stylized **landed**; realistic **waiting** |
| Bld_Gatehouse (castle wall) | 31 | 32.4 × 18.3 × 4.9 | Kit_Gate Arch, Portcullis (Steel), Wall 4 × 6, Crown, Tower* | Stone 7, Steel 7, Roof 12 | stylized **landed** (never fits the frame whole; used as wall segments); realistic **waiting** |
| Kit_Real{Brick,Wood,Steel} Block/Wall/Floor/Joint, RealRoof, RealProps | — | Block 1.02 × 1 × 1, Wall 3 × 3 × 0.22–0.27, Floor 3 × 0.18–0.27 × 3, Joint 0.32 × 3 × 0.3 | Brick / Wood / Steel | 7–18 frags, 6–54 joints | **landed** (generic pieces for every other level) |

Pivots: every module is bottom-centre (Blender z-up, z-min 0) except Kit_Tower_Floor and Kit_Keep_Floor (top-centre, the slab hangs
below) and a few meshes that dip 0.06–0.47 m below the pivot (House_Roof eaves, Tower_RingSlit, RealProps_Barrel).
Kit_Real* fragment origins are the fragment's centre of mass. Mass ranges per module are in `prefab_inventory.json`.

## How each level uses them (rev 7)
- **rev 8: L3 → realistic modular Bld_Townhouse at 1:1 (interim until the 8×6 m bakery, due Oct 31).** One block per LD module, ids = module
  ids, supports = module anchor_to (= the contacts), Base held/non-objective, street facing the cannon; a 1.8 m timber cellar (two props, two
  slabs) between Base and the ground walls keeps the two-prop beat. Why L3: see REPORT.md rev 8. (rev 7 had used the stylized Kit_House at 0.8.)
- **L7 tower → Bld_Watchtower at ~0.55 scale** + the spike fix (geometry changed): RingDoor/RingSlit quarters, gold steel rings at the
  storey seams, Floor_4, clock room (RingWindow), Crown, spire Roof, Annex shed, Crate + CrateSmall, two barrels, hoist poles.
- **L9 lighthouse → Watchtower modules; seawall → Gatehouse Wall/Crown** (mapping only: the proportions already match at ~0.5 scale).
- **Daily Titan arenas → Kit_Keep modules** (turret legs/arms, keep-wall body), mapping only.
- **L1, L2, L4, L5, L6, L8, L10 → generic Kit_Real* modules** (mapping only).
- **Waiting:** L3 moves to the 8×6 m bakery (due Oct 31); L8 on the Stall module + arch with steel sign (due Oct 31 – Nov 1; block layout
  kept); L7/L9 on a realistic tower/gatehouse; Daily Titan on a realistic keep. L10 has no building prefab (boss art).

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
6. ~~Realistic townhouse not modular~~: resolved by 3D Modeler @ e725121.
7. (rev 8) An indestructible + non-objective piece can never fall (`DestructibleBlock.cs:135 ForceCollapse` returns early; sim.py mirrors it),
   so a building's Base can only be ground. Any level that wants a prefab to drop as a whole must put level supports *under* the walls, as L3 does.
8. (rev 8) One block per LD module is a box; the module's piece FBXs (e.g. UpperStorey's 8) would need a loader that spawns all of a module's pieces
   at the module pivot and swaps each for its `_Fractured` set on break (README "Prefab assembly"). Piece-level blocks are possible later, but in the
   sim each piece would become a separate target (the sim occludes per block, so the back walls would show through the gap between front pieces).
