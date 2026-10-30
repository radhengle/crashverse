# Chunked destruction (physics/chunked-destruction)

Owner: Physics. Scope: every building in chapter 1 levels 1–10, including the L10 Tidewarden boss (layouts
`c1_l01`…`c1_l06`, `m1_clockwork_tower` (L7), `c1_l08`…`c1_l10_tidewarden`). Titan arenas use the same code path.

## What changed

| Before | Now |
|---|---|
| A broken piece spawned 2–10 random same-size cubes around its centre | It breaks into a **precomputed chunk layout** that tiles the piece exactly: stone/brick blocks, roof tiles, timber beams, steel plates, glass shards |
| All chunks appeared at once | Chunks start **in place, held kinematic**, and release **nearest-to-impact first**, spread over 4–10 ticks |
| Losing the last support dropped every dependent piece in the same tick | **Progressive collapse**: each unsupported piece drops `CollapseRules.DelayTicks` later (3 + material sag + 0–2 seeded jitter), so a tower comes down storey by storey |
| A collapsing piece hit the ground whole | A piece that lost its supports and lands at ≥ 4 m/s **breaks into its chunks** (wood/brick/stone; steel bends, never self-breaks) |
| Debris despawned after 2.5 s | Debris **settles, sleeps, freezes** to static rubble after 25 settled ticks, stays (10/20/30 s), then fades |
| Glass made 10 sim cubes | Glass shatters into **presentation-only shards** (no rigidbody, no collider, not in the sim) |
| Boss defeat collapsed everything at once | Boss pieces are scheduled 2 ticks apart in authored order (+ their collapse delay) |

## Design

Engine-free core: `Assets/_Project/Scripts/Physics/Core/ChunkedDestruction.cs` (`Crashverse.Physics.Core`, no UnityEngine).

* **ChunkKindRules**: chunk geometry from the material family.
  Glass → GlassShard; Steel → SteelPlate; any roof (layout role `roof`, or fractureClass `Roof`) → RoofTile;
  Wood → TimberBeam; Brick/Stone/Terrain → StoneBlock. fractureClass goes through `FractureClassRules` (Roof → Stone, PR #54), so a
  terracotta roof is RoofTile chunks carrying material **Stone**. A wooden roof is RoofTile chunks carrying **Wood** (shingles).
* **ChunkLayout.Generate** (box / voronoi-lite): integer millimetres, seed = `StableId(blockId)` only (never the run seed), so the
  layout is identical on every run, Retry, device and the server. Grid per kind: stone near-cubic, roof keeps the thin axis whole
  (tiles), timber stays long along the member (≤ 2 pieces along it), steel ≤ 3 plates. Cut planes are jittered ±22 % per row
  (staggered masonry courses), never thinner than 12 cm. Volumes tile the piece exactly (tested).
  Computed **once per level build** (`LevelBuilder.Build` → `DestructibleBlock.PrepareChunks`), never at break time.
* **ChunkLayout.AssignReleaseOrder**: rank by integer squared distance from the impact point (ties by index);
  `delay = rank × spread / (count − 1)`. Spread: stone 8, roof 10, timber 6, steel 4, glass 0 ticks.
  The impact point is deterministic within a tick: a later tick replaces it, script hits (projectile, explosion) beat contacts,
  and among contacts the largest impulse wins, so PhysX callback order never matters.
* **CollapseRules / CollapseSchedule**: SupportRule unchanged (collapse only when ALL supports are gone). `StructuralIntegrity`
  owns a fixed-capacity schedule; due pieces pop in (tick, block index) order at the start of its FixedUpdate.
  Sag: wood 4, brick 2, stone 2, steel 6, glass 0 ticks; jitter = splitmix(runSeed, pieceId) % 3.
* **ChunkLedger**: pool slot state machine, fixed arrays, no allocation.
  `Held → Dynamic → Frozen (rubble) → Fading → Free`. Cap counts every chunk that has a collider (Held + Dynamic + Frozen).
  At cap, the victim is the oldest Frozen rubble, else the oldest settled chunk, else the oldest; victims leave the sim at once
  (collider off) and shrink/fade over 30 ticks. Free slots, else the oldest fading slot, are reused, so nothing is instantiated.

Engine side:
* `DebrisPool` keeps its API (`SpawnBurst` legacy path kept for pieces without a layout) and adds `SpawnChunks`. Prewarms to
  `cap + 16` slots at load. Chunk mass = piece mass × volume fraction (debris damage formula unchanged: `min(60, 0.5·m·(v−2)·mult)`).
  Settled = `IsSleeping()` or < 0.15 m/s and < 0.5 rad/s; freeze sets `isKinematic` (collider kept: static rubble).
* `GlassShardFx` (presentation only): own pool of collider-less cubes, ballistic in `LateUpdate` with render time, own RNG stream
  re-seeded from the pane's piece id. Never touches the sim RNG or the debris caps.
* Dust, micro-debris, sparks: **not spawned by physics**. Physics raises hooks; VFX owns visuals.

### Determinism (ranked)

Unchanged: 50 Hz `Physics.Simulate(0.02f)`, SimRng seeding, 6 solver iterations, sleep threshold 0.08, ranked cap 60 on layer 9.
No `UnityEngine.Random` or `Time` in sim code (`GlassShardFx` and the landing hook are presentation and use neither for the sim).
Layouts: integer math from block id + size. Release/collapse delays: integer ticks. Slot assignment: stable index order.
Ranked always uses Mid budgets. **Note:** collapse timing and chunk physics differ from the previous build, so replays recorded on
the old build will not match this build (same as any sim change: the server must replay with the matching build).

## Per-tier budgets

| | Low | Mid (target, ranked) | High |
|---|---|---|---|
| Colliding debris cap (existing) | 25 | 60 (ranked 60 on every tier) | 90 |
| Sim chunks per broken piece | ≤ 4 | ≤ 8 | ≤ 12 (layouts cap at 6–9 per kind, so ≈ Mid) |
| Layout target per kind | stone 8 · roof 9 · timber 6 · steel 3 | same | same |
| Glass shards per pane / pool (presentation) | 5 / 24 | 10 / 48 | 14 / 72 |
| Freeze after settled | 25 ticks (0.5 s) | 25 | 25 |
| Max time dynamic, then fade | 400 ticks (8 s) | 400 | 400 |
| Frozen rubble lifetime | 500 ticks (10 s) | 1000 (20 s) | 1500 (30 s) |
| Pool objects (prewarmed) | 41 | 76 | 106 |

Why this holds 60 fps on Mid: the solver cost is bounded by **dynamic** bodies, not by the chunk count. At most 60 colliding chunks
exist; settled chunks become kinematic after 0.5 s and cost ~nothing in the solver; held chunks are kinematic until released;
fading chunks have no collider. One break releases ≤ 8 bodies over 4–10 ticks instead of all in one step, which spreads broadphase
spikes. All chunks are pooled cube primitives (one mesh, one material, property-block colour → SRP batcher-friendly, ~1 batch
family), no runtime mesh cutting and no Instantiate during play. Whole-level totals at Mid range 40 (L1) to 113 (L8) chunks if every
piece breaks; the cap keeps the live count at ≤ 60 by fading the oldest rubble first. Profile target stays L10_PerfFullBudget with
Mid forced, p95 ≤ 16.6 ms (Graphics tier PR #56 bar).

## Event hooks for VFX & audio (AnimEventBus)

All raised with a `Crashverse.Sim.ChunkEvent` payload. **One reused instance per event name (no allocation): copy the fields you need
inside the handler; do not keep the reference.** Presentation must never write back into the sim.

| Event | When | Suggested use |
|---|---|---|
| `PHYS_CHUNK_RELEASE` | a chunk starts falling (once per chunk) | crumble puff, small grit burst, crack sfx |
| `PHYS_CHUNK_LAND` | a chunk hits something at ≥ 1.5 m/s (10-tick cooldown per chunk) | **dust plume / impact VFX**, thud; `tier` from impulse |
| `PHYS_CHUNK_SETTLE` | chunk frozen into rubble | settle dust wisp; decals |
| `PHYS_CHUNK_FADE` | chunk leaves (cap or lifetime) | optional dust to hide the shrink |
| `PHYS_GLASS_SHATTER` | pane shattered (`presentationOnly = true`) | glass burst / tinkle; shards are already drawn by physics |
| `PHYS_COLLAPSE_STEP` | a scheduled unsupported piece starts to drop | structural groan, dust from the joint |

Existing `PHYS_BREAK` / `PHYS_COLLAPSE_START` / `PHYS_COLLAPSE_END` / `PHYS_IMPACT` (ImpactEvent payload) are unchanged.

`ChunkEvent` fields: `tick` (sim tick), `pieceId` (StableId of the source block, = PieceIds.Of), `chunkIndex` (-1 for piece-level
events), `chunkCount`, `kind` (ChunkKind: StoneBlock 0, RoofTile 1, TimberBeam 2, GlassShard 3, SteelPlate 4), `material`
(MaterialType: Wood 0, Brick 1, Steel 2, Glass 3, Terrain 4, Stone 5; a terracotta roof is Stone), `tier` (ImpactTier Light/Medium/Heavy,
40 / 150 N·s), `position`, `normal` (SimVec3, world; normal is the contact normal for LAND/SHATTER), `size` (chunk size, m), `speed`
(m/s), `presentationOnly`, `ranked`.

## Fracture kits

`Assets/_Project/Models/BuildingKit/*_Fractured.fbx` (models-v1) are the hero-building modules. The chapter 1 levels are built from
layout JSON greybox blocks, not from the kit prefabs, and `3d-modeler/models-v3` is not on the remote, so this branch generates
layouts from block sizes. Kit-driven chunks (fragment bounds from the fractured FBX as the ChunkSpec list) are the follow-up once
levels use the kit prefabs; `PrepareChunks` already takes the imported `fractureClass`.

## Tests

`Physics/Tests/ChunkedDestructionTests.cs` (EditMode, engine-free): layout determinism + golden hash, exact tiling, kind shapes,
tier budgets, release order, progressive collapse order + golden ticks for a fixed seed, schedule ordering, landing-break thresholds,
cap enforcement at 25/60/90/ranked, victim order, held/release/freeze/rubble/fade/recycle timeouts, material → chunk kind for every
kind including fractureClass Roof, and every destructible block in levels 1–10 (incl. boss) gets a valid layout within budget.
