# Audio: music player and budget

## MusicPlayer (`Scripts/Audio/MusicPlayer.cs`, logic in `MusicCore.cs`)
- **Voice rule (mixing_unity.md):** at most 3 audible music streams: Base (level or arena loop), one cumulative Layer, and a Stinger. A layer change uses two Layer sources for a 1.0 s equal-power crossfade (cos/sin). During the crossfade Base + 2 Layers = 3 streams, so a stinger requested then is queued until the crossfade ends (`MusicVoiceState`).
- **Titan:** `titan_<arenaId>` base loop. At damage stages 25/50/75% (`BOSS_TITAN_STAGE`) it crossfades to `titan_int25/50/75`. The new layer starts with `PlayScheduled(dspTime + 50 ms)` and `timeSamples` set to the base's sample position at that DSP time, so it stays sample-locked. Layers never stack.
- **Tidewarden:** on `BOSS_TIDE_PHASE2`, `stinger_boss_phase2` plays exactly at the end of the current P1 bar (128 BPM, 1.875 s bars, measured from P1 start), P1 stops there, and `tidewarden_p2` starts on the next bar line.
- **Intro + loop tracks** (main_theme 378000, tidewarden_p1 330750, gameplay_* island tracks): one streamed clip, with a loop region played as two base sources. The idle source is pre-seeked to `loopStart` and scheduled at the exact wrap DSP time (`loopEnd`). Only one of them is audible at a time. Whole-file loops use `AudioSource.loop`.
- **Streaming:** `Editor/MusicImportRules.cs` imports everything under `Resources/Music/` as Streaming Vorbis (40% stems, 50% full mixes, 60% stingers), with load in background on and preload off. At runtime the player warns if a clip isn't streamed.
- **Wiring:** the map plays `sunnyville_map_loop`. Campaign levels play `gameplay_loop`, the boss level `tidewarden_p1`, and Titan its arena loop. Win/fail stops level music and plays `stinger_win` / `stinger_win_3star` / `stinger_fail`; boss defeats play `stinger_boss_defeat`.
- **Assets:** the OGGs are still in `audio/music/` (audio team) and are **not yet copied into `Assets/_Project/Resources/Music/`**. Until they are, the player is silent (it no-ops on missing clips).

## Audio budget verdict (2026-10-16)
Scope: base APK. Season 2 hub (`season2_stormfront_hub`, 1.05 MB) and `titan_preview_obsidian_full` (0.83 MB) are excluded.

| Bucket | Budget | Measured / estimated | Verdict |
|---|---|---|---|
| Music in APK (streamed OGG, 24 files incl. 6 stingers) | ~12 MB planned | **13.50 MB** measured (source OGG sizes) | ~1.5 MB over the 12 MB plan, but fits the total |
| SFX resident when loaded (Decompress On Load PCM) | ≤ 12 MB | **≈ 5.2 MB** (sfx_list.csv, 108 rows, worst-case lengths × variations) | PASS |
| SFX compressed in memory (medium) | ≤ 4 MB | ≈ 0.9 MB | PASS |
| SFX in APK (compressed) | — | ≈ 1.5 MB estimate | — |
| **Total audio in APK** | **≤ 25 MB** | **≈ 15.0 MB** (13.5 music + ~1.5 SFX) | **PASS** (~10 MB headroom) |
| Streaming buffers | ≤ 2 MB | 5 sources, at most 3 audible (4 only during the sample-exact intro→loop handover) | PASS |

Notes: no SFX assets exist yet, so the SFX figures are estimates from `sfx_list.csv` and must be re-measured once the assets land. Unity re-encodes the music at the import qualities above, so the final music size may differ by ±20%. Check on a low-tier device with Profiler ▸ Audio and the Memory Profiler.
