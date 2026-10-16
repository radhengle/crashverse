# CRASHVERSE Physics Feel & Systems Proposal v1 (for design review)

Owner: Physics & Performance Developer. Status: **proposal**, nothing here is approved design. **Every number is a starting tunable (T)** and lives in data (`MaterialDefinition`, a new `FeelProfile` and `PhysicsTunables` ScriptableObjects), not in code.
Fits: `physics/Physics_Perf_Plan_v1.md` (on `physics/fixed-step-lock`, PR #6), `design/Gameplay_Feel_Answers_v1.md`, `design/Design_Pack_v1_Ch1_Powerups_LiveOps.md`, `Feel/FeelCore.cs` (`AnimEventBus`, `MotionSettings`), `Perf/QualityController.cs`, `Destruction/*`.
Note: `design/Level_Rulings_v1.md` isn't on any branch yet. The debris-damage and collapse rulings below are quoted from Developer's relay of it.

## 0. Ground rules (non-negotiable)
- **Sim layer** (affects score/replay): 50 Hz integer tick from `SimRunner`, `Physics.Simulate(0.02f)` with manual stepping, one seeded `SimRng` (from `AttemptToken.seed`, split into streams), inputs applied by tick. Everything that can change damage, breaks, % destroyed or stars belongs here, keyed by tick, never by `Time.*`.
- **Presentation layer** (feel): hit-stop, shake, particles, dust, decals, sound, camera. It only *listens* to `AnimEventBus` and may use unseeded RNG. **It never writes to the sim.** Hit-stop freezes rendering interpolation and VFX time, never `Physics.Simulate` and never the tick. `MotionSettings` (reduce shake/motion) scales all of it.
- Device tier changes only presentation and *visual* debris count, never sim results (OQ-D2: destroyed mass is counted on the source piece).
- Rulings: debris damage = 0 when relative speed ≤ 2 m/s or resting, else `k·m·v`, cap 60 per collision. Collapse happens only when **all** supports are gone.

## 1. Impact feel tiers
Tier is chosen sim-side from the contact impulse `J` (N·s, the PhysX contact impulse summed per pair per tick), so it's reproducible. The *response* is presentation.

| | Light | Medium | Heavy |
|---|---|---|---|
| Impulse J (T) | < 40 | 40–150 | > 150 (or any break of a piece ≥ 50 kg) |
| Hit-stop (presentation) | 0 ms | 40 ms | 80 ms (max 1 per 0.5 s; off with Reduce motion) |
| Camera shake (amp / freq / dur) | none | 0.08 m / 22 Hz / 0.15 s | 0.2 m / 18 Hz / 0.3 s, Perlin, ×`ShakeScale(tier)` |
| Fragment ejection | none; flinch/scrape decal | 1–3 chunks, eject speed 2–4 m/s | full fracture, 3–6 m/s plus 10% of projectile momentum along the normal |
| Dust | 4 particles puff | 12 particles + small debris sparks | 30 particles + lingering cloud 1.5 s (Low: 10, no cloud) |
| Other | tick sound | crack sound, chromatic pop 0.05 | low boom, FOV kick +2°, light flash |

Ejection velocity is sim-side for simulated chunks (drawn from `SimRng.Debris`), and cosmetic chunks beyond the cap use cosmetic RNG.

**Event payload** (new struct, raised via `AnimEventBus.Raise(FeelEvents.Impact, payload)`):
```csharp
public struct ImpactEvent { public int tick; public byte tier; /*0 L,1 M,2 H*/ public float impulse; public float relSpeed;
  public MaterialId material; public MaterialId otherMaterial; public Vector3 position; public Vector3 normal;
  public int pieceId; public bool broke; public bool isDebris; }
```
Sibling events: `PIECE_CRACK_STAGE {tick, pieceId, stage 0-3, material, position}`, `PIECE_BREAK`, `COLLAPSE_START {tick, groupId, pieceCount}`, existing `COLLAPSE_LAND`, `DEBRIS_FALL_OFF`. Struct payloads should be pooled to avoid GC.

## 2. Material behaviour (starting tunables)
| Material | Density kg/m³ | maxHp | Joint break F / τ (N, N·m) | Restitution | Friction (dyn/stat) | Break style |
|---|---|---|---|---|---|---|
| Wood | 600 | 30 | 450 / 800 | 0.25 | 0.5 / 0.6 | **Splinters**: elongated shards, 8–16 frags, 2–3 crack stages, snapping crack sound, low ejection |
| Brick | 1900 | 60 | 900 / 900 | 0.1 | 0.7 / 0.8 | **Crumbles and chains**: frags break on first heavy impact, mortar dust, neighbours take 25% overflow damage (1 link) for chain rubble |
| Steel | ~200 kg per panel (hollow effective density, agreed with 3D Modeler) | 120 | 2200 / 2200, joint dmg ×2.0 (Contract) | 0.3 | 0.4 / 0.5 | **Dents → bends → tears**: stage 1 dent (decal/blend shape), stage 2 joint softens (break force ×0.6), stage 3 tears at 4–10 frags. Sparks, metal groan |
| Glass (Neon District, not M1) | 2500 | 8 | 150 / 150 | 0.05 | 0.3 / 0.4 | **Shatters**: breaks on any medium hit, max 6 sim frags, rest is cosmetic shard VFX, 0 debris damage (shards are cosmetic) |

Combined restitution uses Minimum and friction uses Average, with bounce threshold 2 m/s (already the PhysicsManager value, which matches the damage ruling). Mass ratio between touching rigidbodies stays ≤ 10:1 to keep the solver stable.

**Debris damage** (sim, per tick, per pair): `if (resting || vRel <= 2) 0 else min(60, kMat · m · (vRel − 2))`, with `k` = 0.5 (T), wood ×1.0, brick ×1.1, steel ×0.8. "Resting" means the pair was in contact on the previous tick with vRel < 0.5 m/s. Each pair counts once per 10 ticks to stop jitter spam. Constants go in `PhysicsTunables`, plus tests (on the existing to-do list).

## 3. Structural collapse feel
- **Rule:** a piece collapses only when all supports (`FixedJoint` + `ANCHOR_` marks) are gone. `StructuralIntegrity` checks on a tick, never in `Update`.
- **Progressive cracking (readable cue):** each piece has crack stages at 33/66/90% damage *or* joint load ≥ 70% of break force held for 5 ticks. Each stage raises `PIECE_CRACK_STAGE`, which drives crack decals, a dust trickle and a creak. Over-stressed joints show a pulsing hairline crack *before* the shot so players can plan (puzzle readability). The visual is presentation; the stage itself is sim state.
- **Staggered chain collapse:** when a group loses its last support, its pieces release in order of distance from the failed support, 3 ticks (60 ms) apart, max 1 group release per tick, with a pre-collapse "sag" of 6 ticks (joint drive softens) and a groan. This is deterministic (ordered by distance then pieceId) and costs little because it spreads the contact spikes.
- `COLLAPSE_LAND` fires when the group's summed vertical impulse peaks, which gives one big heavy-tier hit instead of 20 small ones.

## 4. Projectile & motion physics
Shot power is fixed (OQ-A1). Launch is `v0` along the aim direction, with no charge.

| Projectile | Mass | v0 (m/s) | Gravity scale | Linear drag | Notes |
|---|---|---|---|---|---|
| Standard ball | 8 kg | 28 | 1.0 | 0.02 | baseline |
| Heavy shot | 24 kg | 22 | 1.3 | 0.0 | flatter range, 2.4× momentum |
| Piercing bolt | 4 kg | 40 | 0.6 | 0.0 | near-flat, see §6 |
| Splitstrike child | 4 kg each | inherits | 1.0 | 0.02 | splits at apex or 70% of flight time (OQ-P2) |

- Global gravity is −9.81 × 1.2 (T) for a snappier arc, applied as a per-body extra force on tick (not by changing `Physics.gravity` per level).
- **Projectiles are kinematic-integrated by `SimRunner`** (semi-implicit Euler at 0.02 s with explicit gravity and drag, followed by a sphere-cast per tick) until first contact, and only then become dynamic bodies. **The aim preview runs the exact same integrator function** (`Ballistics.Step(ref state, tunables)`) for up to 150 ticks against a static-geometry snapshot, so the preview matches the sim bit-for-bit on the same device. It stops at the first impact (OQ-A3). The preview never uses PhysX `Simulate` or scene clones (cheap: ~150 sphere-casts per aim change, throttled to 15 Hz).
- `Projectile.Update` life/age moves to ticks (Perf Plan task 3).

## 5. Environment physics concepts
All forces are applied in `SimRunner` per tick, in a fixed order (zones sorted by id), from data. "Det." = deterministic under our contract.

| Concept | How (cheap) | Det. | Perf cost L/M/H | Puzzle use |
|---|---|---|---|---|
| Low/zero-g zone | trigger volume, per-body gravity scale 0.3/0 + drag 0.5 | yes | very low | float debris into targets, slow arcs |
| Wind | constant force × body area proxy; gusts on a tick schedule from `SimRng.Env` | yes | low | bend arcs, push light wood |
| Water/buoyancy | plane at height h: up force = ρ·g·submerged fraction (sphere/box approx) + drag 2.0 | yes | low (≤ 25 bodies Low) | sinking steel vs. floating wood, sea-stack levels |
| Ice | PhysicMaterial friction 0.05 | yes | none | sliding debris, toppling by nudge |
| Magnet/attractor | radial force on steel only, 1/r² clamped, radius 5 m | yes | low-med (overlap query/tick) | steel-only pull, "Magnetic Mayhem" event |
| Conveyor | surface velocity via kinematic body `MovePosition` or tangential force | yes | low | timed delivery of crates/barrels |
| Explosive barrel | on HP 0 → radial impulse (§6 falloff) + 80 dmg at 3 m, delayed 10 ticks, chains | yes | med (spikes) | chain reactions; Chain Reactor power-up |
| Pendulum / wrecking ball | HingeJoint + heavy body, phase from level data | yes (fixed start state) | low | timing shots, release-to-swing |
| Spring/bumper | impulse pad: set normal velocity to 15 m/s | yes | none | redirect projectile/debris |
| Rope (limited) | ≤ 6-segment chain of ConfigurableJoints, max 2 per level; cut = break joint | yes (solver float risk is accepted) | med; **Low: 4 segments** | cut to drop loads |
| Fire spread | grid of wood pieces, ignite neighbours every 25 ticks with p from `SimRng.Env`, 5 dmg/s | yes | low sim; particles costly → Low: decals only | burn supports to trigger collapse |

Time Fracture's slowing of moving parts (OQ-P3) already counts as an environment effect: see §6.

## 6. Power-ups grounded in physics
Existing design names are kept; the physics models extend them and need Game designer sign-off.
- **Shockwave Core:** keep the 40 dmg 1-link rule, and add a radial impulse `J(r) = 600·(1 − r/R)²`, R = 4 m, plus an upward bias of 0.3. Falloff is computed sim-side from the body centre. Presentation: ring distortion and heavy-tier shake.
- **Gravity Well:** pulls loose debris within 4 m with `F = 120·m/max(r,0.5)` for 75 ticks (1.5 s), capped at 8 m/s, then releases as a burst of 60 dmg + outward impulse 300. Implemented as a force zone (§5).
- **Time Fracture in ranked runs:** time is **not** slowed. The *timestep never changes* (0.02 s, same tick count). Instead, "moving parts" (MovingPart, pendulums, conveyors) advance their scripted phase at 0.25× per tick for exactly 200 ticks (4 s at 50 Hz; the "real time" in OQ-P3 is redefined as sim ticks for ranked runs), or until the fired shot's impact tick. Projectile and debris physics run at normal speed. Presentation adds desaturation, audio low-pass and a slowed music tempo. Because the effect is a fixed function of ticks, server replay reproduces it. Variable `Time.timeScale` is forbidden in ranked runs. Cosmetic slow-mo stays off during it (`SlowMoAllowed`).
- **Heavy shot (new, proposed Booster):** projectile ×3 mass (24 kg), ×0.8 v0, damage ×1.5, brick chain overflow 50%.
- **Piercing shot / Phase Piercer:** passes through the first 2 pieces, each costing 15% speed and dealing 0 dmg (design: full damage to the third). Implemented as a sim-side sphere-cast with an ignore list, not by disabling colliders.
- **Ricochet (new):** up to 2 bounces off steel/stone, restitution 0.8, damage ×0.7 per bounce. The preview still shows only the first segment (OQ-A3).
- **Chain lightning for steel (new, Neon "energy chains"):** on a steel hit, it jumps to the nearest steel piece within 3 m, up to 4 jumps, 25 dmg ×0.8 per jump, 2 ticks per jump. Ordered by distance then pieceId. No physics force, so it's cheap and deterministic.
- **Splitstrike, Chain Reactor:** as designed. Splitstrike's children use §4, and Chain Reactor uses the barrel model.

## 7. Sound mapping (for Sound Manager)
Events to emit: `IMPACT` (tiered), `PIECE_CRACK_STAGE`, `PIECE_BREAK`, `COLLAPSE_START`, `COLLAPSE_LAND`, `DEBRIS_RATTLE` (aggregated), `DEBRIS_FALL_OFF`, `PROJ_LAUNCH`/`PROJ_APEX`/`PROJ_WHOOSH`, `PU_*`, `ENV_*` (wind gust, splash, magnet hum, barrel fuse, fire loop).
Parameter mapping (presentation, sound side):
- Volume dB = lerp(−24, 0, log10(J/5)/log10(300/5)), clamped. Pitch = 1.0 ± 0.08 random, minus 0.15 for Heavy and for mass > 100 kg.
- Sample bank = `material × otherMaterial` (wood/brick/steel/glass/ground), with 3–5 round-robin variants per tier. A bank key could look like `SFX_IMPACT_{MAT}_{TIER}`, aligned with `anim_audio_map.csv`.
- Voices: Low 12, Mid 24, High 32. Priority: Heavy > break > collapse > medium > rattle. Steal the quietest voice.
- **Debris rattle throttling:** no per-chunk sounds below Medium. Light debris contacts are aggregated per 100 ms into one `DEBRIS_RATTLE {count, avgMaterial, centroid}` with at most 3 rattle voices. Same material+cell (2 m grid) is deduplicated within 60 ms.
- Distance: 3D blend 0.7, min 2 m / max 40 m. Audio runs during hit-stop (hit-stop doesn't duck audio, except a 40 ms high-shelf dip).

## 8. Perf budgets per tier (Android, 60/60/30 fps targets from QualityController; T)
| Feature | Low | Mid | High |
|---|---|---|---|
| Sim debris rigidbodies | 25 | 60 | 90 |
| Physics step ms (p95) | ≤ 4 | ≤ 3 | ≤ 3 |
| Fragments per break (sim) | wood 3, brick 2 (OQ-D2), rest cosmetic | full | full |
| Dust/particles alive | 150 | 400 | 800 |
| Camera shake | ×0.7 | ×1 | ×1 |
| Hit-stop | on (cheap) | on | on |
| Crack decals | 16 | 48 | 96 |
| Rope segments / ropes | 4 / 1 | 6 / 2 | 6 / 2 |
| Fire spread | decals only, no flames | flames ≤ 8 | ≤ 16 |
| Water | flat plane, no splashes | splash VFX | + ripples |
| Audio voices | 12 | 24 | 32 |
| Aim preview | 10 Hz, 100 ticks | 15 Hz, 150 | 15 Hz, 150 |

Disabled on Low: lingering dust clouds, chromatic pop, FOV kick, debris sparks, ripples, idle fidgets (existing). **The sim content (forces, zones, damage) is identical on every tier.** If a level can't fit Low, cut it in level design rather than tier-switching the sim. GC: 0 bytes per tick (pooled payloads, `Pool<T>`).

## 9. Prioritized roadmap
| # | Item | Feel/effort | Sign-off |
|---|---|---|---|
| 1 | Land PR #6 (50 Hz lock, SimRunner, seeded DebrisPool) | foundation | Developer |
| 2 | Impact tiers + `ImpactEvent` payload + hit-stop/shake/dust (presentation) | **highest** | Game designer (tier thresholds), Animator/VFX |
| 3 | Debris damage rule + all-supports collapse + tests (in progress) | high, required | Game designer (ruling done) |
| 4 | Crack stages + staggered collapse | high, puzzle readability | Game designer, Level Designer |
| 5 | Sound events + rattle throttling + voice limits | high | **Sound Manager** |
| 6 | Ballistics integrator + exact aim preview | high (trust) | Game designer (gravity ×1.2, v0) |
| 7 | Material tuning pass on validation walls (wood/brick/steel) | medium | 3D Modeler, Game designer |
| 8 | Power-up physics models (Shockwave impulse, Gravity Well, Time Fracture tick model) | medium | **Game designer** (Time Fracture ranked change) |
| 9 | Env set A: ice, wind, low-g, spring, explosive barrel | medium, cheap | Game designer, Level Designer |
| 10 | Env set B: water, magnets, conveyors, pendulum, rope, fire; new power-ups (Heavy, Ricochet, Chain lightning); glass | later chapters | Game designer, PM scheduling |

Open questions: (a) Time Fracture as 200 ticks in ranked instead of 4 s real time? (b) Is gravity ×1.2 acceptable for existing levels? (c) Should new power-ups replace or join the six? (d) Should glass go in Neon only?
