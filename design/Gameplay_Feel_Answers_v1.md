# Gameplay Feel Answers v1 (Animator's open questions)
Owner: Game designer. Answers the Game designer items in animation/OPEN_QUESTIONS.md. Values are tunable data. Reference-board parity changes are on hold until the user decides, so these answers may be revisited then.

- **OQ-A1 Charge:** No charge. Shot power is fixed for every shot, which keeps puzzles deterministic and repeatable. Remove the Charge state (ChargeT). The HUD "power" bar becomes a preview of the loadout's effect, not a power meter. PM to route the HUD change to Art.
- **OQ-A2 Fire input:** Drag anywhere to aim, then tap the FIRE button. Letting go of the drag never fires. Tapping FIRE once fires immediately; there's no confirm step.
- **OQ-A3 Aim values:** Approved as starting values: 0.25°/dp yaw and 0.15°/dp pitch. The trajectory preview shows the arc up to the first impact point, without bounces or the path after impact.
- **OQ-D1 Debris cap:** Approved: 60 simulated debris rigidbodies on mid tier and 25 on low, with 90 on high. When the cap is reached, the oldest settled chunks are turned into visual-only debris or despawned first.
- **OQ-D2 Low-tier halving:** Approved. Wood 3 and brick 2 chunks on low tier, visual only. Destroyed mass is always counted on the source piece, so the device tier never changes the % or stars.
- **OQ-P1 Disarm:** Yes. A power-up can be un-armed for free any time before FIRE, and its charge is only spent when the shot is fired. Time Fracture is the exception (see OQ-P3).
- **OQ-P2 Splitstrike with no apex:** The shot splits at its apex or at 70% of the predicted flight time, whichever comes first, so it always splits before impact. If it hits something before the split point, it doesn't split but still does full single-shot damage, and the charge is spent.
- **OQ-P3 Time Fracture:** It starts when armed, not when fired, and slows moving parts to 25% for 4s of real time. Its charge is spent on arming and it can't be un-armed. If the player fires during the slow, the slow lasts until that shot's impact.
- **OQ-P4 Energy cores:** Not needed in Chapter 1, because Chain Reactor unlocks at Gearstorm L3. Explosive crates and energy cores are a Gearstorm asset need, for PM to schedule with Art.
- **OQ-B1 Tidewarden core:** Show crack steps only (intact, cracked, destroyed). There are no HUD pips, so the boss stays readable from the model.
- **OQ-B2 Tidewarden arms:** They detach as physics debris and fall off the sea-stack. They're not objective pieces and don't count toward score or combo.
- **OQ-B3 Emberforge Titan:** Damage stages at 25%, 50% and 75% of the 10,000 damage maximum, with the defeat at 100%. The Titan never attacks the player. It only plays idle, flinch, stage-break and defeat animations, plus visuals for the day's special rule.
- **OQ-C1 Win-shot slow-mo:** Skip it. There's no final-blow slow-mo, because a win is only known after physics settles. The win camera plays after settle.
- **OQ-C2 Accessibility:** Yes. Add two settings, Reduce screen shake and Reduce motion (no slow-mo, softer camera moves). Both are off by default.
- **OQ-C3 Slow-mo:** Cosmetic slow-mo (combo pops, boss phase breaks) is allowed in weekly events and Titan practice, and is disabled only in Titan ranked.

## Related: OQ-X1 (for PM and Art)
Win threshold is a per-level data field, defaulting to 0.85, and every Chapter 1 level uses 0.85. A goal shown on screen has to match that level's data. A screen showing "85% destroyed" with 0 stars is wrong, because 85% is a win.
- **Update (PM approved):** Reduce motion switches on automatically when Android's Remove animations setting is on (see Content_Roadmap_Addendum_v1 §6).
