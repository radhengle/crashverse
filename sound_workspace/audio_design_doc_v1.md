# CRASHVERSE — Audio Design Doc v1 (index)
Based on design/ docs (incl. Gameplay_Feel_Answers_v1, Content_Roadmap_Addendum_v1) and code at commit 0d60e07, and art bible /workspace/art/v1/.

| Doc | Contents |
|---|---|
| [audio_direction.md](audio_direction.md) | "Toybox demolition" identity, adjectives, rules, sourcing & licensing plan |
| [sfx_list.csv](sfx_list.csv) | 107 SFX: material break tiers, cannon, 6 power-ups, combos, stars, win/fail/hint, UI, rewards, Tidewarden, Emberforge Titan |
| [music_plan.md](music_plan.md) | Sunnyville Isles themes (Harbor/Mill/Clocktower/Storm), boss, Titan, Season "The Wrecking Frontier" + 4 events, menu, stingers |
| [mixing_unity.md](mixing_unity.md) | AudioMixer tree, snapshots, ducking, voice limits, pooling, Android import settings, memory budget proposal |
| [haptics.md](haptics.md) | Vibration waveform table + settings toggle |
| [audio_events_for_developer.csv](audio_events_for_developer.csv) | Code hooks (file.method), params, SFX ids, ANIMATOR SYNC flags |
| [anim_audio_map.csv](anim_audio_map.csv) | All Animator events (updated table), each owned by anim OR code (audio_sync_table.md) → sfx ids, frame/ms, code hook, gaps |
| [composer_options_note.md](composer_options_note.md) | Composer/music options with estimated costs + recommendation |
| [licence_register.csv](licence_register.csv) + [licence_rules.md](licence_rules.md) | Licence register header + rules |

## Key decisions folded in
No shot charge (fixed power, drag to aim, tap FIRE); Time Fracture starts on arm; Titan stages at 25/50/75% and the Titan never attacks; no slow-mo on the winning shot; reload sound starts at impact and a ready click plays at FIRE unlock; the 6 power-ups are final; Ch2–5 names are provisional and downloaded later; v1 sourcing is CC0/royalty-free only, with placeholder music.

## Open items
Open Developer hooks: 1.0 s reload gate raising ANIM_WRECKER_READY, Phase Piercer pass-through/third-hit events, 2 Hz tell, 300 ms shutter tween, AnimEventBus. (66 ms fire delay and REARM resolved.)
