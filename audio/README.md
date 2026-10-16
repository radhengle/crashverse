# CRASHVERSE audio: SFX library v1 + launch music

All audio is original (code-synthesised; tonal layers and music leads rendered from the FluidR3_GM SoundFont, MIT). Zero spend. Licences: `audio/licence_register.csv`. Sources and generators live in the audio workspace (`/workspace/crashverse/audio/`, not in this repo).

## Layout
- `Assets/_Project/Resources/Music/<id>.ogg`: 26 launch tracks (ids as used by `MusicPlayer` / `MusicTracks`). **Streaming**, Vorbis (stems 40%, full mixes 50%, stingers 60%), preload off, load in background (set in the .meta files and enforced by `MusicImportRules`).
- `Assets/_Project/Resources/SFX/Destruction/<Material>/`: matches `ImpactAudioProfile.bank` (`SFX/Destruction/Wood|Brick|Steel|Glass|Stone`). Files: `sfx_<material>_<impact|crack|break>_<s|m|l>_<nn>`, `sfx_<material>_collapse_<s|m|l>_<nn>`, `sfx_<material>_debris_<nn>`, `sfx_steel_jointsnap_<s|m|l>_<nn>`, `sfx_creak_<wood|steel>_<nn>`.
- `Assets/_Project/Resources/SFX/{Weapon,PowerUps,Combo,Results,UI,Reward,Boss/Tidewarden,Boss/Titan}/`
- Names are lowercase snake_case and match the ids in `sfx_list.csv`. Variations are `_01.._04`. Loops end in `_loop`, except boss core hum and Titan rule loops (see sfx_list `loop` column).
- Excluded from the APK: Season 2 hub, previews, demos, `main_theme_alt_piano`, archives.
- `main_theme.ogg` is the user-approved v1: sha256 `23c798f8b8122b92f525e0dbcbbcdf3c53c6da687ab130635d3c75d5c80cdb8f` (byte-identical, do not re-encode).

## Import settings (already in the committed .meta files)
| Folder | Load type | Format | Quality | Mono | Preload |
|---|---|---|---|---|---|
| Music | Streaming | Vorbis | 40/50/60% | no | no (load in background) |
| SFX/UI | Decompress On Load | ADPCM | – | yes | yes |
| SFX/Destruction (impact/crack/break/debris/creak/jointsnap), Weapon, PowerUps *_arm, Combo, Results, Reward (single) | Decompress On Load | Vorbis | 50–60% | yes | yes |
| SFX/Destruction *_collapse_*, PowerUps *_fx*, Reward coin_burst/chest_open, Boss/* | Compressed In Memory | Vorbis | 50–55% | yes | yes |
There is no SFX AssetPostprocessor yet. If Developer adds one (like `MusicImportRules`), it should encode the table above.

## Budget (measured)
- Music: 15.71 MB of OGG (budget ≤ 16 MB).
- SFX: 3.18 MB on disk. Resident if every Decompress-On-Load clip is loaded: ≈ 9.7 MB PCM (budget ≤ 12 MB). Compressed in memory: 1.39 MB.

## Music loop points (44.1 kHz samples)
| id | samples | loop start | loop end | BPM | LUFS / dBTP |
|---|---|---|---|---|---|
| gameplay_clocktower_isle | 2998800 | 176400 | 2998800 | 120 | -16.0 / -1.8 |
| gameplay_harbor_isle | 3271418 | 192436 | 3271418 | 110 | -16.0 / -1.4 |
| gameplay_loop | 2822400 | 0 | 2822400 | 120 | -16.1 / -1.8 |
| gameplay_mill_isle | 3102207 | 182483 | 3102207 | 116 | -16.0 / -1.1 |
| gameplay_storm_isle | 2205000 | 441000 | 2205000 | 96 | -16.0 / -2.3 |
| main_theme | 3402000 | 378000 | 3402000 | 112 | -15.9 / -1.9 |
| results_loop | 846720 | 0 | 846720 | 100 | -16.0 / -2.9 |
| season1_wrecking_frontier_hub | 2919724 | 0 | 2919724 | 116 | -16.0 / -1.5 |
| stinger_boss_defeat | 245858 | one-shot | – | 144 | -15.9 / -3.3 |
| stinger_boss_phase2 | 122744 | one-shot | – | 144 | -16.0 / -2.9 |
| stinger_chapter_clear | 229320 | one-shot | – | 100 | -15.9 / -2.9 |
| stinger_fail | 132300 | one-shot | – | 100 | -15.9 / -2.5 |
| stinger_tidewarden_defeat | 245858 | one-shot | – | 144 | -15.8 / -2.2 |
| stinger_titan_defeat | 246960 | one-shot | – | 150 | -15.8 / -2.9 |
| stinger_win | 176400 | one-shot | – | 100 | -15.9 / -3.4 |
| stinger_win_3star | 211680 | one-shot | – | 100 | -16.0 / -3.6 |
| sunnyville_map_loop | 3256615 | 0 | 3256615 | 104 | -16.1 / -1.3 |
| tidewarden_p1 | 2646000 | 294000 | 2646000 | 144 | -16.1 / -2.0 |
| tidewarden_p2 | 2352000 | 0 | 2352000 | 144 | -16.1 / -1.9 |
| titan_ash_citadel | 2257920 | 0 | 2257920 | 128 | -17.9 / -2.4 |
| titan_cinder_bridge | 2257920 | 0 | 2257920 | 128 | -18.2 / -2.4 |
| titan_foundry_crater | 2257920 | 0 | 2257920 | 128 | -18.3 / -2.5 |
| titan_int25 | 2257920 | 0 | 2257920 | 128 | -35.6 / -18.8 |
| titan_int50 | 2257920 | 0 | 2257920 | 128 | -33.1 / -17.5 |
| titan_int75 | 2257920 | 0 | 2257920 | 128 | -27.8 / -12.6 |
| titan_obsidian_forge | 2257920 | 0 | 2257920 | 128 | -18.0 / -2.4 |

Intro+loop tracks: play the intro once, then loop [loop start, loop end). Titan files are sample-aligned (one arena mix + one cumulative int layer; 1 s equal-power crossfade). Detail: `audio/music_loop_points.md`.
