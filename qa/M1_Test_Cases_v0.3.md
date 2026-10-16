# CRASHVERSE — M1 Test Cases v0.3

**Status:** Frozen pending build (updated from Design Contract v0.1)  
**Owner:** QA  
**Source of truth:** `/workspace/crashverse/design/M1_Level_Design_Contract_v0.1.md`  
**Level:** `m1_clockwork_tower`  
**Reference devices:** TBD (hardware validation OPEN). Emulator OK for first pass.

---

## Acceptance criteria (M1 sign-off)

| ID | Criterion | Pass when |
|---|---|---|
| AC-01 | Level load | Loads `m1_clockwork_tower` with moveBudget=5, objectiveType=`DESTROY_STRUCTURE`, winThreshold=0.85, materials ∈ {wood, brick, steel} only |
| AC-02 | Aim / fire | Aim shows readable trajectory; confirmed fire launches; aim/cancel does **not** consume a move |
| AC-03 | Shot accounting | Each confirmed fire decrements moves by 1 |
| AC-04 | Materials | Wood (HP 30), brick (HP 60), steel (HP 120; joints 2.0× damage) behave distinctly; wood weakest, steel face-spam inefficient |
| AC-05 | Structural collapse | Destroying a support / steel ring-joint drops dependent segments (not independent free-fall of every block) |
| AC-06 | Objective tracking | `destroyedMass / totalObjectiveMass` updates after settle; NonObjective props excluded |
| AC-07 | Win | ≥85% objective mass **and** settle (no ObjectivePiece RB velocity > 0.15 for 0.75s); 0 moves remaining allowed if last shot already met threshold after settle |
| AC-08 | Fail | 0 moves after settle and mass < 85% → fail + Retry; Give Up → fail; optional soft-lock → fail |
| AC-09 | Retry | ≤1 tap from fail/pause; restores authored greybox + full 5 moves; no leftover debris; no ads/currency loss |
| AC-10 | Stars (win only) | ratio = shotsLeft/5 vs star3Pct 0.60 / star2Pct 0.30 (≥, no rounding; contract v0.2 §5): 3–5 left → 3★; 2 → 2★; 0–1 → 1★. Fail = 0★. Best persisted; lower retry doesn't overwrite |
| AC-11 | Performance | Mid-range ≥60 FPS during collapse; low-end stable ≥30 FPS with quality fallback; no sustained hitch >200ms |
| AC-12 | Build | Installs/launches on agreed emulator (first pass) and later on agreed low+mid devices |

**Sign-off gate:** All AC pass on agreed matrix; zero open Blockers; Majors fixed or explicitly deferred by PM with owner+date.

---

## Test cases

### Smoke / core

| ID | Title | Steps (summary) | Expected |
|---|---|---|---|
| TC-M1-01 | Cold launch | Launch build → enter `m1_clockwork_tower` | Arena loads; HUD shows moves=5 and DESTROY_STRUCTURE objective |
| TC-M1-02 | Aim free / fire costs | Aim, cancel, re-aim; then fire once | Aim/cancel free; moves → 4 after confirmed fire |
| TC-M1-03 | Wood break | Direct hit default core on wood scaffold wing | Beam breaks on first solid hit; light debris ≤6 chunks |
| TC-M1-04 | Brick vs steel | Same class of hit on brick wall face vs steel column face | Brick takes meaningful damage; steel face resists; joint hit on steel deals ~2× |
| TC-M1-05 | Support chain | Destroy lower steel ring-joint (or wood support that exposes it) | Segment above drops via dependency; crown can cascade |
| TC-M1-06 | Win @ ≥85% | Play until destroyedMass/total ≥ 0.85; wait settle | Win results after settle; stars per contract v0.2 §5 |
| TC-M1-07 | Fail on budget | Exhaust 5 shots without ≥85% after settle | Fail screen with Retry |
| TC-M1-08 | Retry clean | From fail, tap Retry | Full 5 moves; authored state; no leftover debris/soft-lock |
| TC-M1-09 | Background/resume | Mid-level background then resume | No crash; state sane or clean restart |
| TC-M1-10 | Spam / soft-lock | Rapid fire / spam tap; attempt undamageable leftover state | No crash; if soft-lock, fail after settle |
| TC-M1-11 | Thermal load | 10–15 min repeated attempts | FPS stays in band; no OOM |

### Three-star thresholds (win only) — contract v0.2 §5, budget 5, defaults 0.60/0.30

| ID | Title | Setup | Expected stars |
|---|---|---|---|
| TC-M1-S1 | 1★ | Win with **0** shots left (0.00) | ★☆☆ |
| TC-M1-S2 | 1★ just below 2★ | Win with **1** left (0.20 < 0.30) | ★☆☆ |
| TC-M1-S3 | 2★ | Win with **2** left (0.40 ≥ 0.30, < 0.60) — just below 3★ | ★★☆ |
| TC-M1-S4 | 3★ at exact threshold | Win with **3** left (3/5 = 0.60 ≥ 0.60) | ★★★ (was 2★ in v0.2; current `LevelConfig.cs:56` gives 2★ → defect until Developer fix) |
| TC-M1-S5 | 3★ | Win with **4** left (0.80) | ★★★ |
| TC-M1-S6 | 3★ | 5 left (1.00) — unreachable (a win needs ≥1 shot); document N/A | N/A |
| TC-M1-S6a | Per-level override | Set star3Pct 0.80 / star2Pct 0.40 in data: 4 left / 3 left / 2 left / 1 left | ★★★ / ★★☆ / ★★☆ (0.40 exact) / ★☆☆ |
| TC-M1-S6b | Defaults | Remove thresholds from data | 0.60/0.30 applied |
| TC-M1-S7 | Fail = 0★ | Fail level | 0 stars; no star UI as earned |
| TC-M1-S8 | Best persists | Win 1★, then retry and win 3★, then retry and win 1★ | Best stays 3★; last run may show 1★ but persisted best = 3★ |
| TC-M1-S9 | No % in stars | Win at exactly ~85% mass with 2 moves left vs 95% with 2 moves left | Same 2★ both (stars ignore % beyond win gate) |

### Intended skill path (design sanity — not blocker if alternate valid)

| ID | Title | Notes |
|---|---|---|
| TC-M1-P1 | 3-shot clear → 2★ | Intended path: wood wing → lower/mid steel joint → crown/gravity; 2 shots left (0.40) → 2★ |
| TC-M1-P2 | 1–2 shot clear → 3★ (4 or 3 left) | Skillful / exploit check; if impossible, note for design tune |

---

## Bug report template

Title | Severity (Blocker/Major/Minor/Polish) | Build/commit | Device + OS (or emulator ID) | Level ID | Steps | Expected | Actual | FPS/logs if perf | Capture | Blocks M1? Y/N

All bugs report **through Project Manager**. QA does not implement fixes.

---

## Device matrix

| Tier | Target | Status |
|---|---|---|
| Emulator | First-pass smoke/core + stars | OK until devices confirmed |
| Low-end Android (~2–3 GB) | 30 FPS fallback | OPEN |
| Mid-range Android | 60 FPS | OPEN |
| High-end | Headroom only | Optional |

---

## Changelog

- **v0.2** — Aligned to Design Contract v0.1: win ≥85% after settle, 5-move budget, stars from moves remaining, level `m1_clockwork_tower`; added TC-M1-S1–S9.
- **v0.1** — Initial lightweight plan (pre-contract).

- **v0.3** — Stars per contract v0.2 §5 (% shots left, per-level star3Pct/star2Pct, defaults 0.60/0.30, ≥ no rounding). 3 left = 3★. Added S6a/S6b.
