# CRASHVERSE — Level Design Contract v0.2 (M1 only)

**Status:** v0.2, approved stars rule (SG-01). Supersedes v0.1  
**Owner:** Game designer  
**Audience:** Developer (code against this), Art (moodboard only — numbers are gameplay, not art)  
**Scope:** One greybox test level. Do not treat this as the full campaign contract.

---

## 1. Primary objective (M1 test level)

**Objective type:** `DESTROY_STRUCTURE`

Collapse or dismantle the target structure until the **primary objective mass** is destroyed.

| Field | M1 value |
|---|---|
| Objective ID | `DESTROY_STRUCTURE` |
| Win condition | `destroyedMass / totalObjectiveMass >= 0.85` |
| Target | Single named structure: **Clockwork Tower** (greybox) |
| Non-goals for M1 | No timed bomb, no protect-ally, no multi-structure campaign gates |

**Definition — objective mass:** Sum of mass of all pieces tagged `ObjectivePiece` (supports + floors + target volumes that must fall or break). Decorative props tagged `NonObjective` do not count toward the 85% threshold.

---

## 2. Move limit

| Field | M1 value | Notes |
|---|---|---|
| Move budget | **5 shots** | One shot = one confirmed fire from the Wrecker |
| Aim cancels | Free | Re-aiming does not consume a move |
| Reload / wait | Free | Physics settle time does not consume a move |
| Recommended feel | Level should be solvable in **3** skilled shots; **5** is the soft ceiling so stars can reward efficiency |

---

## 3. Material set (M1 only)

Only these three materials ship in M1. Thresholds are **starting greybox numbers** — tune after first playable, do not invent new materials.

### Wood
| Property | Value |
|---|---|
| Role | Weak supports, early teaching pieces |
| Durability (HP) | `30` |
| Mass | `1.0` |
| Impact resistance | Low — first direct hit from default core should break a standard beam |
| Fracture | Snap + light debris (pooled, max 6 chunks per piece) |
| Notes | Use for outer scaffolds and “obvious” weak points |

### Brick
| Property | Value |
|---|---|
| Role | Mid-weight walls / floors |
| Durability (HP) | `60` |
| Mass | `2.5` |
| Impact resistance | Medium — needs concentrated impact or support loss |
| Fracture | Brittle break; heavy debris (max 4 chunks) |
| Notes | Good for teaching “hit the support, not the wall face” |

### Steel
| Property | Value |
|---|---|
| Role | Critical joints and load-bearing columns |
| Durability (HP) | `120` |
| Mass | `4.0` |
| Impact resistance | High on face; **joints** take `2.0×` damage |
| Fracture | Joint failure preferred over full mesh shatter |
| Notes | M1 puzzle hinges on 2–3 steel joints. Direct face-spam should feel inefficient |

**Out of scope for M1:** glass, rubber, ice, crystal, reinforced concrete, energy barriers.

---

## 4. Win / fail / retry

### Win
1. Objective mass destroyed ≥ **85%**, **and**
2. Physics has settled (no ObjectivePiece rigidbody above velocity threshold `0.15` for `0.75s`), **and**
3. At least **1 move remaining is not required** — 0 moves left is allowed if the last shot already achieved the threshold after settle.

On win → show results (stars + retry/continue stubs). No campaign unlocks required in M1.

### Fail
Any of:
- Moves remaining = **0** after settle **and** destroyed mass &lt; 85%
- Player chooses **Give Up** from pause
- (Optional safety) Structure soft-lock: no ObjectivePiece can take further damage and threshold unmet — treat as fail after settle

### Retry
- Retry returns to **pre-level loadout / aim** state with full move budget restored
- Reset physics to authored greybox state (deterministic; no leftover debris)
- Do **not** force ads, currency, or equipment loss in M1
- Retry should be ≤ one tap from fail / pause

---

## 5. Three-star formula (v0.2: percentage of shots left)

**Formula type:** `STARS_FROM_SHOTS_LEFT_PERCENT`. Same rule as Design Pack v1 for every level.

Each level stores `star3Pct` and `star2Pct` in its level data. Defaults are 0.60 and 0.30.
`ratio = shotsLeft / shotBudget` (exact, no rounding, compared with ≥)

| Stars | Condition (win only) | M1 at 5 shots |
|---|---|---|
| ★★★ | ratio ≥ star3Pct | 3–5 left |
| ★★☆ | ratio ≥ star2Pct | 2 left |
| ★☆☆ | any win | 0–1 left |

- Stars are awarded on a win only. A fail gives 0.
- The best result is kept; a lower retry never overwrites it.
- "Moves" and "shots" mean the same thing (SG-02).
- v0.1's table (2–3 left = 2★) is retired. Code following it (`LevelConfig.cs`) has to change.

## 6. Acceptance criteria (Developer)

Developer can mark M1 level systems **done** when all of the following pass:

1. **Level data** loads a single greybox level with: moveBudget=`5`, objectiveType=`DESTROY_STRUCTURE`, winThreshold=`0.85`, materials ∈ {wood, brick, steel} only.
2. **Shot accounting:** each confirmed fire decrements moves by 1; aim/cancel does not.
3. **Objective tracking:** destroyedMass / totalObjectiveMass updates after each settle; win fires only when ≥ 0.85 and settle complete.
4. **Fail path:** 0 moves + below threshold after settle → fail screen with Retry.
5. **Retry:** restores authored state + full moves; no soft-lock debris.
6. **Stars:** on win, stars computed from shotsLeft/shotBudget against per-level star3Pct/star2Pct (§5); best stars saved locally for this level id.
7. **Materials:** wood/brick/steel pieces respect HP/mass/joint multiplier values in §3 (tunable ScriptableObject / data asset — not hardcoded magic in gameplay logic).
8. **No M1 scope creep:** no Daily Titan, seasons, multi-region map, or extra materials required to pass this contract.

---

## 7. Greybox test level brief — “Clockwork Tower”

**One-page playable intent (gameplay only)**

| Field | Spec |
|---|---|
| Level ID | `m1_clockwork_tower` |
| Fantasy | Small coastal clock tower — vertical puzzle, readable supports |
| Duration target | 30–60s per attempt |
| Move budget | 5 |
| Player teaching goals | (1) Aim at supports, not façades (2) Chain collapse from one good hit (3) Save shots for stars |

### Structure idea
- **Base:** brick plinth (heavy, mostly non-critical) with **two wood scaffold wings** (easy early breaks — teaching shots).
- **Mid:** hollow tower shaft — brick walls + **three steel ring-joints** at 1/3, 1/2, and 2/3 height. Destroying a steel joint should drop the segment above it.
- **Crown:** wood + brick clock house (high objective mass). If mid joints fail in sequence, crown collapses for free.
- **Critical path (intended 3★):** one shot opens a wood wing → exposes lower steel joint → second shot breaks middle steel joint → third shot (or gravity) finishes crown. Extra shots = safety / recovery.

### Layout constraints (greybox)
- Camera: angled 3D, full tower readable on phone without constant orbit
- Weak points visually thicker / different primitive color in greybox (final art later)
- No moving gears required in M1 — static supports only
- Debris budget: keep total simulated rigidbodies modest (mobile)

### Success feel
Player should say: “I dropped the whole top by clipping one joint,” not “I chipped HP off every brick.”

---

## Changelog
- **v0.2** — Stars switched to % shots left with per-level thresholds (PM SG-01).
- **v0.1** — M1 greybox contract: DESTROY_STRUCTURE, 5 moves, wood/brick/steel, win@85%, stars from moves remaining, Clockwork Tower brief.

