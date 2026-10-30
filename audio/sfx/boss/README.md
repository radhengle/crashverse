# Boss SFX v3 (original, synthesised; Tidewarden + Emberforge Titan)
42 mono 44.1 kHz OGG files, low-passed at 9 kHz, sample peak -1.5 dBFS, true peak ≤ -1 dBTP (verified). Each sound is layered: impact (sub thump, crack) + body (metal, grind, formant growl, bell) + tail (reverb, debris, splash, hiss), with saturation and pitch drops.
Sync points come from animation/audio_sync_table.md. Per-file measurements and sync checks are in project/sfx_report.json.
To re-render: `/workspace/crashverse/audio/music/project/.venv/bin/python project/boss_sfx.py .` (run from this folder). Then run `project/fight_demos.py`, which writes ../../demos/*_fight_demo.ogg.
Key sync: arms_drop ends at 600 ms (ARMS_LAND). shutter_close thunk at 300 ms. tell pulses at 0/417 ms (2.4 Hz). tw_intro ignite at 2400 ms. tw_defeat impacts at 0/1.2/2.0/2.6 s (4.0 s). titan_intro anvils at 1.0/1.5/2.0 s.
