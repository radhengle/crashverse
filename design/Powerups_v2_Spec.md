# Power-ups v2 Spec (8 shipping power-ups). Supersedes Design Pack v1 §2
Owner: Game designer · PM approved 2026-10-24 · Data: design/data/powerups_v2.json (drop-in replacement for Resources/Data/powerups.json)

Picker rules from DP §2 are unchanged: one Core, one Module and one Booster per level. Power-ups are earned and never sold, and a retry restores charges. The Booster slot now has 4 options to choose from.

| # | Power-up | Was | Slot | Effect (starting values) | Unlock |
|---|---|---|---|---|---|
| 1 | Earthquake | Shockwave Core | Core | 40 damage to pieces 1 support link away, plus a radial impulse within R = 4 m (Physics PR #15 model) | c1_l07 |
| 2 | Multi Shot | Splitstrike | Module | Splits into 3 at the apex or at 70% of flight time; each does 50% of final damage | c1_l08 |
| 3 | Black Hole | Gravity Well | Module | Pulls debris within 4 m for 75 ticks, then bursts for 60 damage | L10 reward |
| 4 | Mega Bomb | new (takes over Chain Reactor's role) | Core | Radial blast, 70 damage at the centre falling off to 0 at 3.5 m, set off on impact. Also detonates explosive barrels and energy cores within the radius | c2_l03 |
| 5 | Shield | new | Booster | "Protects Steps": if the shielded shot destroys less than 10% of objective mass (after settle), the shot isn't spent. On boss levels the test is less than 5% of boss max HP | c2_l05 |
| 6 | Laser Beam | Phase Piercer | Booster | Straight beam with no gravity. Passes through the first 2 pieces for 0 damage and does full damage to the third (sphere-cast) | c2_l07 |
| 7 | Guided Rocket | new | Booster | After firing, drag to steer for up to 60 ticks at up to 90°/s. Normal damage. Steering input is recorded per tick so it's deterministic for ranked replay. The preview shows only the arc at launch | c3_l03 |
| 8 | Time Freeze | Time Fracture | Booster | Moving parts run at 0.25× for 200 ticks. Starts when armed, can't be disarmed, and ends at the shot's impact tick if that comes sooner | c3_l07 |

## Shield details
- The charge is spent when you fire. If the shot qualifies for the refund, the shot counter goes back up after settle, and a "Step protected" pop shows.
- A refunded shot counts as a shot left for stars, % bands and score. There's nothing to exploit, because a near-miss gains nothing beyond keeping the shot.
- Only one Shield per level (1 charge). It doesn't stack with anything else that refunds shots.
- It counts as using a power-up, so it breaks L9's "No power-up used" mastery goal.

## Acceptance criteria
1. The picker shows the 8 tiles in unlock order, enforces one tile per slot, and loads every value from powerups_v2.json.
2. Each mapped power-up behaves exactly like the old one it replaces (existing tests are renamed, not rewritten).
3. Shield: a shot under 10% (or under 5% of boss HP) refunds the shot after settle; 10% or more doesn't; the charge is spent either way.
4. Mega Bomb detonates barrels and cores within 3.5 m. Chain Reactor is removed.
5. Guided Rocket steering is recorded per tick, and a server replay reproduces the same result.
6. Time Freeze lasts 200 ticks in every mode.

## Notes for Art
Use the icons in /workspace/art/ui_v2/. The slot colour rule (Core cyan, Module violet, Booster orange) still applies through the engine UI ring, not baked into the tiles.
