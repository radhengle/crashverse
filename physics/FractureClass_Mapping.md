# fractureClass → physics material (Physics-owned)

`ImportedPhysicsHints.fractureClass` (PR #53) is a free-text string from `models/HeroBuildings/Kit_<B>_stats.json` `modules[*].frac`, or from an FBX `fracture_class` property. Physics maps it with `Crashverse.Sim.FractureClassRules` (engine-free) and `MaterialDefinition.FromFractureClass` (MaterialKind).

| fractureClass | MaterialType (PHYS_* events, audio) | MaterialKind (HP, mass, joints, debris) |
|---|---|---|
| Wood | Wood (0) | Wood (0) |
| Brick | Brick (1) | Brick (1) |
| Steel | Steel (2) | Steel (2) |
| Glass | Glass (3) | Glass (3) |
| Stone | Stone (5) | Stone (4) |
| **Roof** | **Stone (5)** | **Stone (4)** |
| empty / null / whitespace | caller fallback (default Wood), no warning: not authored | same |
| anything else | caller fallback (default Wood), **one warning per distinct value** | same |

## Rules
- Trim with `string.Trim()`, then compare with `StringComparison.OrdinalIgnoreCase` against a fixed table. No `ToLower`/`ToUpper` and no current culture, so `"ROOF"` maps the same on a `tr-TR` device as in CI.
- The default fallback is Wood, the same default `LevelBuilder` uses for an unknown `mat`. Callers can pass their own.
- No new enum values. `MaterialType` and `MaterialKind` are serialized and append-only. Their counts are pinned by tests.

## Why Roof → Stone
- **The hero data.** Every `Roof` module in the five kits is a red terracotta-tile roof sitting on masonry:
  - Kit_Keep `TurretRoof`: red conical roof, 294–3168 kg.
  - Kit_Tower `Roof`: tall conical roof, 573–14844 kg.
  - Kit_Gate `TowerRoof`: stepped red pyramid roof.
  - Kit_House `Roof`: roof slope with dormer.

  Timber parts are tagged `Wood` separately, and the VFX spec says wooden-shingle roofs are tagged `Wood`. So `Roof` means heavy tiled roofing, not timber, and Wood would make multi-tonne roofs the weakest pieces on the building.
- **One material family per piece.** The PM ruled that Roof uses Stone FX with a terracotta dust tint, and that tint applies only while the piece's material is Stone (`DamageFeedback.dustOverride`). The PHYS_* event material comes from `MaterialKind` (`ImpactReport.TypeOf`). With Roof → Stone, the impact and break events, the audio bank (`SFX/Destruction/Stone`), the feel scales, the VFX and the tint all agree. Brick (fired clay) would fit terracotta's density slightly better, but it would send Brick events and Brick VFX to a piece whose DamageFeedback says Stone, and the tint would never show.
- **Tuning.** Stone is the tougher choice: HP 90, debris taken ×0.9, joint break 1500/1300. If Game design wants roofs more fragile than walls, use per-module `mass` and `break_force_N` hints, or add an appended `Roof` kind later behind a ranked determinism review. Don't remap Roof.

## Ranked / existing content
Nothing in the sim calls the mapping yet, and no shipped level uses `fractureClass` (hero kits are only on `3d-modeler/models-v3`). Ranked sim behavior and serialized data are unchanged. Hooking hero modules up to `DestructibleBlock.material` is a follow-up once the kits land.
