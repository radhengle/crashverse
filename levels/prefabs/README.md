# Building prefabs → Ch1 / Daily Titan layouts (rev 9, Level Designer)

Source: `origin/3d-modeler/realistic-v1` @ 8c99d49 (rev 9 asked for a24f5ec; 8c99d49 only refits the MarketArch sign), read with `git show` only. rev 8 used e725121, rev 7 9e3f4e3. `inventory.py` → `prefab_inventory.json`;
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
| **Bld_Bakery (realistic, modular)** `Models/Realistic/Buildings/Bld_Bakery/` | 10 LD modules / 21 pieces | walls 8 × 6 (Base 8.64 × 6.84), eaves 6.30, ridge 8.66 (layout `dimensions_m` says 8.52), chimney top 10.17 | ShopFront (ShopW+ShopD, Stone) 8.0 × 1.6 × 3.16; Wall_Left/Right (Stone) 0.57 × 5.11; Wall_Back (BackD+BackW, Stone); Floor (Wood) 8.02 × 5.88 × 1.0; UpperStorey (UpFL, UpFC, UpFR, UpB, UpL, UpR, GabL, GabR; Wood) 8.26 × 6.70 × 4.88; Roof_Left/Right (2 each, Roof) 4.49 × 7.32 × 2.87; Chimney (Stone) 6.87 tall from y 3.30; Base (indestructible) 0.35; props[]: 3 Prop_BreadCrate (one stacked), 1 Prop_Bench (indestructible, no fractured set) | Stone 6–10, Wood 9–16, Roof 10–16 | **landed @ a24f5ec**, used by L3 (rev 9) |
| **Kit_RealStall + Bld_MarketArch (realistic)** `Models/Realistic/Buildings/Kit_RealStall/`, `Bld_MarketArch/` | stall 2 modules (Keystn 1.6 × 1.8 × 0.225; StallA/StallB 1.7 × 2.17 × 2.18, total 2.40); arch 5 (Pads 10.7 × 0.7 × 0.2 indestructible, Post_Left/Right 1.46 × 0.44 × 3.76 Wood, Beam 11.1 × 0.42 × 0.52 Wood, Sign 2.66 × 0.30 × 0.82 Steel) | arch 11.1 wide, 4.80 tall, clear height 3.5; `stall_slots` x ±1.1, ±3.3 (pitch 2.2) | all Wood except the steel Sign | Wood / Steel fractured sets | **landed @ a24f5ec** (sign refit 8c99d49), used by L8 (rev 9) |
| Bld_Watchtower | 15 | 6.8 × 20.2 × 6.0 | Kit_Tower RingDoor/RingSlit/RingWindow (Stone) ⌀4.16 × 3, Floor ×4 (Wood), Crown (Stone) 1.42, Roof (Roof) 6.8 tall; Shed, props | Stone 7, Wood 12, Roof 9 | stylized **landed**; realistic **waiting** |
| Bld_CastleKeep | 52 | 11.0 × 21.3 × 11.0 | Kit_Keep Wall/WallDoor/WallSlit/WallBanner/WallBalcony (Stone) 4.2 × 3, Floor (Wood) 5.6, Turret/TurretCrown/TurretRoof, Crown | Stone 7, Wood 12, Roof 12 | stylized **landed**; realistic **waiting** |
| Bld_Gatehouse (castle wall) | 31 | 32.4 × 18.3 × 4.9 | Kit_Gate Arch, Portcullis (Steel), Wall 4 × 6, Crown, Tower* | Stone 7, Steel 7, Roof 12 | stylized **landed** (never fits the frame whole; used as wall segments); realistic **waiting** |
| Kit_Real{Brick,Wood,Steel} Block/Wall/Floor/Joint, RealRoof, RealProps | — | Block 1.02 × 1 × 1, Wall 3 × 3 × 0.22–0.27, Floor 3 × 0.18–0.27 × 3, Joint 0.32 × 3 × 0.3 | Brick / Wood / Steel | 7–18 frags, 6–54 joints | **landed** (generic pieces for every other level) |

Pivots: every module is bottom-centre (Blender z-up, z-min 0) except Kit_Tower_Floor and Kit_Keep_Floor (top-centre, the slab hangs
below) and a few meshes that dip 0.06–0.47 m below the pivot (House_Roof eaves, Tower_RingSlit, RealProps_Barrel).
Kit_Real* fragment origins are the fragment's centre of mass. Mass ranges per module are in `prefab_inventory.json`.

## How each level uses them (rev 9)
- **rev 9: L3 → realistic modular Bld_Bakery at 1:1** (replaces the rev 8 interim townhouse). One block per LD module, ids = module ids,
  supports = module anchor_to, Base held/non-objective, shop front facing the cannon (`rotY` −90). The 1.8 m timber cellar (two props,
  two slabs) stays between Base and the ground walls (two-prop beat). The bread crates are the decoy/mastery piece (crateL + stacked
  crateTop objective; crateR and the bench indestructible dressing). Every block carries `parts` (its module's piece FBX), `rotY`, `modelScale`.
- **rev 9: L8 → 4 × Kit_RealStall under Bld_MarketArch.** Each stall's Keystn is that stall's keystone (key0–3) spanning two quay piers;
  the stall sits on it; arch Pads/posts/beam/steel sign are non-objective (sign = the trap). Stalls at a 1.6 m pitch, not the 2.2 m
  `stall_slots` (see REPORT rev 9: Multi Shot key0–2 and post clearance).
- **L7 tower → Bld_Watchtower at ~0.55 scale** (rev 7) + the spike fix: RingDoor/RingSlit quarters, gold steel rings, Floor_4, clock room,
  Crown, spire Roof, Annex shed, crates, barrels, hoist poles.
- **L9 lighthouse → Watchtower modules; seawall → Gatehouse Wall/Crown** (mapping only).
- **Daily Titan arenas → Kit_Keep modules** (mapping only).
- **L1, L2, L4, L5, L6, L10 → generic Kit_Real* modules** (mapping only).
- **Waiting:** realistic-v1 FBX merged into integration/v2 (L3/L8 parts resolve only then; until then the build logs not-found and the cubes stay);
  L7/L9 on a realistic tower/gatehouse; Daily Titan on a realistic keep. L10 has no building prefab (boss art).

## Code gaps for Developer (no code was written by Level Designer)
Status after the integration/v2 merge (fb37202): 1, 2 and 8 are addressed by `BlockData.model/rotY/parts/modelScale` + `RealKit.DressParts`; 4 partly (`DailyBossData.heroSlot` + `hints[]` exist).
1. ~~(resolved in integration/v2: `model`, `rotY`, `parts`, `modelScale`)~~ `ContentModels.cs:52` **BlockData has no model field** (kit / module / fbx / prefab id) **and no rotation**: side walls need yaw 90°.
   Suggest `string kit; float rotY;`, kept optional so cube fallback still works.
2. ~~(resolved in integration/v2: RealKit.Dress / DressParts, collider stays `s`)~~ `LevelBuilder.cs:35` **always `GameObject.CreatePrimitive(PrimitiveType.Cube)`** scaled to `bd.s`. To use a module it needs to instantiate
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
8. ~~(resolved in integration/v2: `parts` spawns every piece at the block's base-footprint centre)~~ (rev 8) One block per LD module is a box; the module's piece FBXs (e.g. UpperStorey's 8) would need a loader that spawns all of a module's pieces
   at the module pivot and swaps each for its `_Fractured` set on break (README "Prefab assembly"). Piece-level blocks are possible later, but in the
   sim each piece would become a separate target (the sim occludes per block, so the back walls would show through the gap between front pieces).
9. (rev 9) **No visual offset**: `DressParts` puts every part at the block's base-footprint centre, so the collider box must be centred on the
   module pivot. The bakery chimney's pivot (y 3.30) is inside the Floor/UpperStorey/roof boxes, so L3's chimney is a stub box above the roof
   with no parts; L8's stall collider is trimmed to 1.325 m deep (centred on the pivot) so the 22 cm keystone stays the first thing a shot meets.
   Suggest an optional `modelOffset` (block-local metres) on BlockData.
10. (rev 9) `RealKitManifestBuilder.Write` logs an Error per missing `parts`/`model` FBX. Until realistic-v1 is merged into integration/v2 the
   L3/L8 parts (Kit_RealBakery_*, Kit_RealStall_*, Props_Village) are missing there, so the APK build logs those errors (the build still succeeds and
   the blocks stay cubes).
