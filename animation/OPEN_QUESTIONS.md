# Animation Spec v1 — Open Questions / Gaps (Game designer & PM)
No gameplay or art-direction changes were made; the items below need an owner's decision.

## PM / Art
- **OQ-X3 Muzzle flash colour:** spec uses Booster orange + white (muzzle = Booster ring). Confirm, and whether Core inner-tint should colour the trail.
- **OQ-X4 Tidewarden/Titan rigs** don't exist yet — bone counts (≤40) are a proposal for the 3D artist.
- **OQ-X5 Wrecker "win/fail" emotes** vs store "victory animations" (Addendum §4.6): default ones here are free, skins override — confirm.

## Resolved (PM, 2026-10-16)
- ~~OQ-X1 win threshold~~ — Win is ≥85% per contract v0.2 §4; spec text uses 85%. (v2 "Goal 80%/90%" and the "85% destroyed, 0★" fail art are art fixes, not spec.)
- ~~OQ-X2 canonical screens~~ — v1 is canonical for layout and data; v2 is the visual target.
- ~~OQ-X6 auto-enable~~ — Reduce Motion auto-ON when Android animator duration scale = 0; player can turn it off.
- ~~OQ-C2 accessibility~~ — Reduce Motion added (07 § Reduce Motion, default OFF).

## New (PM)


## Resolved by Game designer (`design/Gameplay_Feel_Answers_v1.md`, 2026-10-16)
- ~~OQ-A1~~ No charge, fixed power. ~~OQ-A2~~ Drag to aim + tap FIRE; release never fires. ~~OQ-A3~~ 0.25/0.15 °/dp approved; preview arc to first impact.
- ~~OQ-D1~~ Debris cap 90/60/25 (high/mid/low). ~~OQ-D2~~ Low-tier halving approved.
- ~~OQ-P1~~ Free disarm before FIRE (not Time Fracture). ~~OQ-P2~~ Split at apex or 70% flight time. ~~OQ-P3~~ Time Fracture starts on arm, 4 s real. ~~OQ-P4~~ Not needed in Ch1; Gearstorm asset (PM/Art).
- ~~OQ-B1~~ Crack steps only. ~~OQ-B2~~ Arms = physics debris, non-objective. ~~OQ-B3~~ Stages 25/50/75%, Titan never attacks.
- ~~OQ-C1~~ No win-shot slow-mo. ~~OQ-C3~~ Slow-mo off only in Titan ranked. (OQ-C2 → Reduce Shake + Reduce Motion, both OFF.)

## Still open — PM / Art (OQ-X)
- OQ-X3, OQ-X4, OQ-X5 (above).
- **OQ-X7 (new, from designer):** HUD "power" bar becomes a loadout-effect preview, not a power meter — PM to route to Art; UI motion for it not yet specced.
- **OQ-X8 (new):** Gearstorm explosive crates / energy core assets (from OQ-P4) — PM to schedule with Art.
- **OQ-X9 (new):** Designer notes answers may be revisited after the reference-board parity decision.

## Pending Developer
- ~~**OQ-DEV1 Fire spawn delay**~~ RESOLVED: Developer accepted the 66 ms delay, 2 Hz tell, shutter tweens and Reduce Motion auto-on. (Was: recommend delaying projectile spawn 66 ms after FIRE tap so the breech anticipation plays first (01). Alternative: breech sfx on input with no delay.)

## Resolved by Art (2026-10-16) — rule: v1 canonical for layout/data/states, v2 visual target only
- ~~ART-1~~ v1 orange FIRE + Ready glow kept. ~~ART-2~~ FREE HINT removed from HUD, no video icon, fail screen only from 2nd fail. ~~ART-3~~ Badges x1 (Chain Reactor x2). ~~ART-4~~ 85% win tick from level data, motion specced. ~~ART-5~~ COMBO violet. ~~ART-6~~ Module pod on barrel top per wrecker_brief_v2, slot swap verified. ~~ART-7~~ Mastery tick row on Level Complete. ~~ART-8~~ Map uses v1 states, marker, gate, node order. ~~ART-9~~ Daily Titan: 0–10,000 damage count-up + Ranked/Practice. ~~ART-10~~ Rewards: Design Pack values, Day 4 today, Days 1–3 claimed, Day 7 not highlighted.
