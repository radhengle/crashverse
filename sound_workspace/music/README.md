# CRASHVERSE music v2 (in-house, zero spend)
Original music written as code. Background parts (drums, bass, synth pads, taiko, forge) use the numpy synth. All leads and melodies use sampled piano and guitar via FluidSynth + FluidR3_GM (MIT), low-passed at 7.5 kHz (bright) or 6 kHz (dark).
v1 renders are kept in _v1_archive/. Re-render everything with `bash project/render.sh`, which also regenerates this file via gen_docs.py.

| File | Dur | BPM | Key | Structure | Loop (samples) | LUFS-I / dBTP (OGG) |
|---|---|---|---|---|---|---|
| **main_theme.ogg (v1, APPROVED by user 2026-10-16, frozen and byte-identical)** | 77.14 s | 112 | C maj | 4-bar intro + 32-bar body (v1 synth leads) | 378000 → 3402000 | -15.9 / -1.9 |
| main_theme_alt_piano.ogg | 77.14 s | 112 | C maj | 4-bar intro + 32-bar body | 378000 → 3402000 | -15.9 / -1.6 |
| sunnyville_map_loop.ogg | 73.85 s | 104 | C maj | 32 bars | 0 → 3256615 | -16.1 / -1.3 |
| gameplay_loop.ogg | 64.00 s | 120 | G maj | 32 bars | 0 → 2822400 | -16.1 / -1.8 |
| stinger_win.ogg | 4.00 s | 100 | C maj | IV-V-Imaj9 bloom, motif on piano | one-shot | -15.9 / -3.4 |
| stinger_fail.ogg | 3.00 s | 100 | C | motif slides down | one-shot | -15.9 / -2.5 |
| season1_wrecking_frontier_hub.ogg | 66.21 s | 116 | F maj | 32-bar frontier gallop | 0 → 2919724 | -16.0 / -1.5 |
| season2_stormfront_hub.ogg | 76.80 s | 100 | E min | 32 bars rain/thunder | 0 → 3386880 | -16.0 / -2.3 |
| stinger_win_3star.ogg | 4.80 s | 100 | C maj | win + climbing 3-star shimmer | one-shot | -16.0 / -3.6 |
| stinger_boss_defeat.ogg | 5.58 s | 144 | Dm→D | generic boss defeat (v2; bosses now use their own) | one-shot | -15.9 / -3.3 |
| stinger_boss_phase2.ogg | 2.78 s | 144 | D min | v3 Tidewarden phase change: Shepard + tremolo → sub-boom hit at 1.25 s | one-shot | -16.0 / -2.9 |
| stinger_tidewarden_defeat.ogg | 5.58 s | 144 | Dm→D | v3 Tide Call collapses → heroic hook, Dmaj9 | one-shot | -15.8 / -2.2 |
| stinger_titan_defeat.ogg | 5.60 s | 150 | Dm→D | v3 Forge Call hammered → sub-boom → anthem in D | one-shot | -15.8 / -2.9 |
| tidewarden_p1.ogg | 60.00 s | 144 | D min | v3: 4-bar intro + 32-bar arc (drop/call-response/breakdown/rebuild/gate build) | 294000 → 2646000 | -16.1 / -2.0 |
| tidewarden_p2.ogg | 53.33 s | 144 | D min | v3: 32-bar arc, harmony shifted | 0 → 2352000 | -16.1 / -1.9 |
| gameplay_harbor_isle.ogg | 74.18 s | 110 | G maj | 2 + 32 bars | 192436 → 3271418 | -16.0 / -1.4 |
| gameplay_mill_isle.ogg | 70.34 s | 116 | D maj | 2 + 32 bars | 182483 → 3102207 | -16.0 / -1.1 |
| gameplay_clocktower_isle.ogg | 68.00 s | 120 | A min | 2 + 32 bars | 176400 → 2998800 | -16.0 / -1.8 |
| gameplay_storm_isle.ogg | 50.00 s | 96 | D min | 4 + 16 bars | 441000 → 2205000 | -16.0 / -2.3 |
| stinger_chapter_clear.ogg | 5.20 s | 100 | C maj | IV-V-vi-IV-V-Imaj9 | one-shot | -15.9 / -2.9 |
| results_loop.ogg | 19.20 s | 100 | C maj | 8-bar bed | 0 → 846720 | -16.0 / -2.9 |

## Titan (folder titan/), 150 BPM, D minor, 32 bars, sample-aligned
| File | Dur | Samples | LUFS / dBTP alone |
|---|---|---|---|
| titan/titan_foundry_crater.ogg | 51.20 s | 2257920 | -18.3 / -2.5 |
| titan/titan_cinder_bridge.ogg | 51.20 s | 2257920 | -18.2 / -2.4 |
| titan/titan_ash_citadel.ogg | 51.20 s | 2257920 | -17.9 / -2.4 |
| titan/titan_obsidian_forge.ogg | 51.20 s | 2257920 | -18.0 / -2.4 |
| titan/titan_int25.ogg | 51.20 s | 2257920 | -35.6 / -18.8 |
| titan/titan_int50.ogg | 51.20 s | 2257920 | -33.1 / -17.5 |
| titan/titan_int75.ogg | 51.20 s | 2257920 | -27.8 / -12.6 |
| titan/titan_preview_obsidian_full.ogg | 60.00 s | 2646000 | -17.6 / -1.6 |

Shared gain -12.9 dB. Worst true peak over every arena + layer combination, including crossfade midpoints: -1.3 dBTP. Arena + layer mixes measure -18.1 to -17.3 LUFS, peaking at -1.6 dBTP.
Playback rule: one arena mix + one cumulative intensity layer (int25/int50/int75), with a 1 s equal-power crossfade between layers, + a stinger. That is at most 3 music voices (see mixing_unity.md).
