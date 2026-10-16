# Ch1 Level Pass — Report (rev 2, Level_Rulings_v1 applied)
See **Ch1_Levels_v1.md** for intent, bands, mastery and the solvability table.
## Files
gen_levels.py, sim.py, sim_results.json, chapter1_levels.json, layouts/*.json (10). Copies go in Assets/_Project/Resources/Data/.
## Rulings applied
1. L10 budget is 8 shots. Mastery is now "Defeat the Tidewarden without hitting the closed shutter" (type NO_CLOSED_SHUTTER_HIT).
2. L10 bands are .375/.25. The 5-shot perfect run gets 3★ (margin 3).
3. The sim follows the spec shutter (open at the start of phase 2, toggles only after phase-2 shots). The code version (min 6 shots) is still computed for Developer as `min_code`.
4. L7 keeps its 2-shot tuning.
5. L1 mastery is now ≥95% destroyed. The L2 text matches SG-21.
6. Stone bases are all nonObj + indestructible + grey 8A8A8A, including the L7 plinth (it was objective before) and the L10 sea-stack. Verified: none is a hint target or on an optimal line.
7. Debris damage is modelled. No level's minimum, stars or win changed. The L7 crown stays 100% unbroken on the 2-shot line, so no crown tuning was needed. L7 final % went 90.2 → 95.4 because the plinth no longer counts.
## Open for Developer
- Bug: TidewardenController/GameSession closes the shutter after the shot that opened phase 2 (ruling 3).
- New mastery type NO_CLOSED_SHUTTER_HIT needs MasteryEvaluator support.
- Already accepted: star-band test becomes a range check, gen_content.py reads levels/ch1, and hint angles are scripted.
## Open for Game designer
- None blocking. The sim is still abstract (no Unity physics). The L2 tank-off-edge and L7 crown masteries need a playtest.
