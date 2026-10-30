#!/usr/bin/env bash
# Re-render all CRASHVERSE demo music: score+synth -> float WAV -> -16 LUFS master -> OGG q5 + verification
set -e; cd "$(dirname "$0")"
[ -d .venv ] || { python3 -m venv .venv && .venv/bin/pip install -q mido numpy soundfile; }
mkdir -p build
.venv/bin/python crashverse_music.py build
.venv/bin/python master.py          # writes ../*.ogg and master_report.json
cp build/*.mid .
# batch 2 (Titan stems, season hubs, boss/3-star stingers)
.venv/bin/python crashverse_music_b2.py build
.venv/bin/python master_b2.py       # writes ../*.ogg, ../titan_stems/*.ogg, master_report_b2.json
cp build/*.mid .
# Titan v2 (pre-mixed arenas + cumulative intensity, replaces batch-2 separate stems) + preview
.venv/bin/python master_titan_v2.py   # writes ../titan/*.ogg, master_report_titan_v2.json
# batch 3 (Tidewarden P1/P2, island gameplay, chapter clear, results loop)
.venv/bin/python crashverse_music_b3.py build
.venv/bin/python master_b2.py render_info_b3.json master_report_b3.json
cp build/*.mid .
cd "$(dirname "$0")" && .venv/bin/python gen_docs.py
/workspace/crashverse/audio/music/project/.venv/bin/python /workspace/crashverse/audio/sfx/boss/project/boss_sfx.py /workspace/crashverse/audio/sfx/boss && /workspace/crashverse/audio/music/project/.venv/bin/python /workspace/crashverse/audio/sfx/boss/project/fight_demos.py
