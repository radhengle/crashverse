# CRASHVERSE — Music Plan v1
Key family C major / A minor (see audio_direction.md). All tracks: intro (one-shot) + seamless loop body, authored to bar boundaries; Unity plays intro via `PlayScheduled` then loops body (AudioSource.loop on a second source, scheduled at dspTime + intro length). Stems are authored for adaptive layering; ship as max 3 stem groups on Android (Base / Rhythm / Lead) to limit streaming voices.

## Chapter 1 — Sunnyville Isles (4 islands, from Design Pack v1)
| Track | Use | Feel / instruments | BPM | Key | Structure | Stems |
|---|---|---|---|---|---|---|
| mus_map_sunnyville | World map | breezy ukulele, glockenspiel, whistle | 104 | C | 4-bar intro + 32-bar loop | Base, Rhythm, Lead |
| mus_harbor_isle (L1–3) | Gameplay | sea-shanty bounce: accordion, pizz strings, toy drums | 110 | G | 2-bar intro + 32-bar loop | Base, Rhythm, Lead (+Intensity) |
| mus_mill_isle (L4–6) | Gameplay | pastoral folk-pop: fiddle, banjo, claps, wooden percussion | 116 | D | 2+32 | Base, Rhythm, Lead (+Intensity) |
| mus_clocktower_isle (L7–9) | Gameplay | clockwork funk: marimba, tick-tock perc, clavinet, brass stabs | 120 | A min→C | 2+32 | Base, Rhythm, Lead (+Intensity) |
| mus_storm_isle | Storm Isle map/approach | stormy-but-fun: timpani rolls, low brass, rain shaker | 96 | D min | 4+16 | Base, Rhythm |
| mus_boss_tidewarden | L10 boss | P1: heavy toy-march, brass + taiko + bell; P2 adds lantern-bell lead & choir pad (synth, no vocals) | 128 | D min | 4-bar intro, P1 loop 32 bars, transition bar, P2 loop 32 bars | Base, Rhythm, Lead, P2 layer |

Intensity stem: fades in (0.5s) when shots left ≤ 2 or combo ≥ x5; out after shot resolves.

## Chapters 2–5 (PROVISIONAL names/themes per Content_Roadmap_Addendum_v1; download later, NOT in APK)
One theme per chapter (map + gameplay share it; gameplay adds Rhythm/Intensity), plus one Guardian theme each. Same intro+loop structure and 3-stem layout as Ch1.
| Ch | Region (provisional) | Chapter theme palette | BPM / key | Guardian theme (provisional) | Guardian palette |
|---|---|---|---|---|---|
| 2 | Gearstorm City | gears/ratchet perc, steam hiss risers, brass, clanky marimba | 122 / F | mus_boss_iron_colossus | factory-march: anvils, low brass, piston perc |
| 3 | Neon District | glass chimes, energy hum pads, plucky synth bass | 118 / E min | mus_boss_clockmaker | ticking ostinato, music-box lead, rotating-panning arps |
| 4 | Crystal Frontier | resonant crystal bells, icy glass harmonica (synth), airy pads | 108 / B min | mus_boss_crystal_king | bowed-crystal drones, big bell hits, choir-pad (no vocals) |
| 5 | Titan's Reach | storm drums, thunder risers, wind, bold brass | 126 / D min | mus_boss_sky_fortress | two-phase: stabilisers (driving), core (full band + thunder) |

## Live ops
| Track | Use | Feel | BPM | Structure | Stems |
|---|---|---|---|---|---|
| mus_daily_titan | Emberforge Titan fight (Titan never attacks: music stays heroic, not threatening) | molten industrial-playful: anvil perc, distorted bass, big brass, fire whoosh risers | 132 | 4-bar intro + 32 loop; ranked adds Lead; stage-break hit at 25/50/75% (sync to BOSS_TITAN_STAGE, code-driven music) | Base, Rhythm, Lead + **Arena layer** |
| mus_titan_arena_{foundry_crater, cinder_bridge, ash_citadel, obsidian_forge} | arena variation layer (one streamed mono stem, 32 bars, locked to Titan loop) | Foundry Crater: smelter clanks; Cinder Bridge: magma bubbles + low drone; Ash Citadel: crumbling stone perc + wind; Obsidian Forge: glassy vent hiss + bell | 132 | 32-bar loop | 1 stem each |
| mus_daily_titan_screen | Titan lobby | low simmering pulse of the Titan theme | 132 | 16-bar loop | Base |
| mus_season_frontier_hub | Season "The Wrecking Frontier" hub/track | frontier-western-pop: twangy guitar, whistle, stomp-clap | 112 | 4+32 | Base, Rhythm, Lead |
| mus_event_magnetic_mayhem | Weekly event | Frontier theme re-voiced: synth "magnet" wobble lead | 112 | 2+32 | shared Base + event Lead |
| mus_event_seismic_surge | Weekly event | rumbling low drums, banjo | 112 | 2+32 | shared Base + event Lead |
| mus_event_gravity_games | Weekly event | floaty pads, half-time feel | 112 (half-time) | 2+32 | shared Base + event Lead |
| mus_event_sonic_rupture | Weekly event | chiptune-ish square lead, bitcrush fx | 112 | 2+32 | shared Base + event Lead |
| mus_season_finale | Season finale level | full-band Frontier theme, boss energy | 124 | 4+32 | Base, Rhythm, Lead |
| **Season 2 "Stormfront" (download later)** | | | | | |
| mus_season_stormfront_hub | S2 hub | weather-adventure: wind pads, rain shakers, bold brass | 116 | 4+32 | Base, Rhythm, Lead |
| mus_event_wind_shear / tidal_pull / lightning_rod / fog_bank | S2 weekly events | Lead swaps: whistling wind lead / sloshing marimba + water / crackling synth + thunder hits / muted, sparse (Fog Bank leaves room for sound-led aiming hints) | 116 | 2+32 | shared Base + event Lead |
| mus_season_stormfront_finale | storm-tower finale | full band + thunder | 126 | 4+32 | Base, Rhythm, Lead |
Season approach: one shared Base stem per season + per-event Lead swap keeps APK small; future seasons downloadable via remote content (Addressables), not in base APK.

## Menu & stingers (one-shots, no loop)
| ID | Use | Length | Notes |
|---|---|---|---|
| mus_menu_main | Title/login | 4+32 loop, 100 BPM, C | glossy pop with main motif (C–E–G–A "CRASH-VERSE" hook) |
| mus_stinger_victory | Win | 3–4 s | motif in brass+glock, ends on tonic; ducks/stops level music |
| mus_stinger_victory_3star | 3★ win | 4–5 s | extended, extra sparkle layer |
| mus_stinger_fail | Fail | 2–3 s | descending toy-horn "wah-wah", cheeky, ends unresolved (iv) |
| mus_stinger_boss_defeat | Tidewarden/Titan defeated | 6 s | big fanfare, flows into victory loop |
| mus_stinger_phase2 | Tidewarden phase change | 2 s | covers music transition bar |
| mus_stinger_chapter_clear | Chapter chest | 5 s | |
| mus_results_loop | Results screen bed | 8-bar loop, 100 BPM | low-energy after stinger |

## Flags
- Ch1 islands are canon (Design Pack v1). Ch2–5 regions and guardians are **PROVISIONAL** (Content_Roadmap_Addendum_v1) and may change after the reference-board decision.
- APK contains only menu, Ch1, Tidewarden, Titan (+4 arena layers), stingers and Season 1 hub. Ch2–5, guardian themes 2–5 and Season 2 ship as downloadable content.
- No final-blow slow-mo on the winning shot (Gameplay_Feel_Answers OQ-C1); victory stinger plays after physics settles.
- All v1 music is **placeholder** (CC0 / royalty-free, logged) until a composer decision is made - see composer_options_note.md.

## Production status (in-house, zero spend — updated 2026-10-16)
| Track | File | Status |
|---|---|---|
| Main theme | music/main_theme.ogg | **APPROVED by user 2026-10-16 (v1, frozen, byte-identical; never re-render over it).** Alt: main_theme_alt_piano.ogg (v2 leads) for comparison only |
| mus_map_sunnyville | music/sunnyville_map_loop.ogg | DEMO v1 (73.8 s seamless loop; no separate intro yet; single file, stems not split) |
| Gameplay (generic, Harbor-style) | music/gameplay_loop.ogg | DEMO v1 (64 s seamless, G maj 120 BPM — differs from per-island BPMs above; island variants TODO) |
| mus_stinger_victory | music/stinger_win.ogg | DEMO v1 (3.8 s) |
| mus_stinger_fail | music/stinger_fail.ogg | DEMO v1 (3.0 s; chromatic slide-down instead of iv ending) |
| Titan theme ×4 arenas (pre-mixed) + cumulative int25/50/75 | music/titan/*.ogg | DEMO v3: 60 s loops, 128 BPM, D minor; one arena + one intensity layer (crossfaded); preview titan_preview_obsidian_full.ogg |
| mus_boss_tidewarden P1 / P2 | music/tidewarden_p1.ogg, tidewarden_p2.ogg | DEMO (P1 4-bar intro + 32-bar loop; P2 32-bar loop; phase2 stinger covers the transition) |
| mus_harbor / mill / clocktower / storm isle gameplay | music/gameplay_*_isle.ogg | DEMO (110 G / 116 D / 120 Am / 96 Dm; intros per plan). Clocktower stays in A minor; the A min→C shift isn't done |
| mus_stinger_chapter_clear / mus_results_loop | music/stinger_chapter_clear.ogg, results_loop.ogg | DEMO (5.1 s / 8-bar 19.2 s loop) |
| Season 1 'The Wrecking Frontier' hub | music/season1_wrecking_frontier_hub.ogg | DEMO v2 (66.2 s loop, 116 BPM F maj) |
| Season 2 'Stormfront' hub | music/season2_stormfront_hub.ogg | DEMO v2 (76.8 s loop, 100 BPM E min) — DLC per plan |
| mus_stinger_victory_3star / boss_defeat / phase2 | music/stinger_*.ogg | DEMO v2 (4.2 / 6.2 / 2.5 s) |
| Ch2–5 themes, guardians, menu | — | NOT STARTED |
Method: original code-synthesised music (music/project/), no soundfonts/samples. Instrument palette is synth approximations (no real accordion/brass yet).

### Android stem-voice rule (decided 2026-10-16)
At most **3 music voices** at any time: [level/arena music] + [at most 1 layer] + [stinger]. Arena layers are pre-mixed into the base (titan_<arena>.ogg). Intensity layers are cumulative (int50 contains int25, int75 contains int25+int50), so only one plays at a time: on a damage stage change, crossfade the old layer to the new one (equal-power, 1 s). During the crossfade there are briefly 3 voices, so a stinger requested in that second waits until the crossfade ends or replaces it. The Base/Rhythm/Lead 3-group layout in the tables above now means that no more than 3 voices may be authored as simultaneous files. In practice, ship 1 file + 1 cumulative layer per track.

## v2 re-render (2026-10-16): user feedback applied
- **Leads:** every lead and melody voice (glockenspiel, whistle, bells, brass, sparkles, high plucks, metal ticks) is now sampled piano or guitar from FluidR3_GM (MIT). Background drums, bass, pads, taiko and forge are unchanged synth.
  - Bright tracks: acoustic grand piano, nylon guitar, steel guitar.
  - Dark tracks: piano capped at C5, muted guitar, palm-muted overdriven guitar, staccato strings.
  - Leads fold down by octaves when they would sit above G5 (bright) or C5 (dark; the boss anthem goes up to G5), with a 4th-order low-pass at 7.5 kHz (bright) or 6 kHz (dark).
- **Archive:** the v1 renders are in music/_v1_archive/.
- **Tempos and loop points:** the boss family is redesigned and faster. Titan is now 150 BPM, so the loops are 51.2 s. Tidewarden is 144 BPM in a 12/8 roll. See loop_points.md for the new offsets.

### Design intent per track
| Track | Intent |
|---|---|
| main_theme | Title card for the "toybox demolition" world: the motif stated clearly on warm piano, with nylon-guitar answers. Inviting, curious, sets up the hook every other track reuses. |
| sunnyville_map_loop | Breezy island-hopping on the world map: strummed uke-like bed, the motif on piano and guitar. Relaxed, so map browsing never tires. |
| gameplay_loop (generic) | Puzzle focus: drums, bass and low pad carry it. The motif appears only every 8 bars on soft high piano, leaving the 1–5 kHz midrange free for break SFX. |
| gameplay_harbor_isle | Seaside shanty bounce: oom-pah guitar off-beats, a slow sea-swell pad every 2 bars (waves), G major. |
| gameplay_mill_isle | Rustic folk: claps, fingerpicked nylon rolls, wooden feel matching the mills and timber blocks, D major. |
| gameplay_clocktower_isle | Ticking clockwork: muted-guitar tick-tock eighths, guitar stabs, minor motif. Rhythmic and precise like the gears, A minor. |
| gameplay_storm_isle | Stormy but fun: rain-shaker 16ths, taiko thunder rolls, low piano thunder, D minor. |
| tidewarden_p1 | Ocean boss: a rolling 12/8 tide made of triplet palm-muted chugs, swells on every bar, and a deep sub-heavy bass. The intro rises with the boss (BOSS_TIDE_INTRO, lantern ignites) through a riser. The heroic boss anthem (the motif turned minor and extended) arrives on piano + low piano octaves at bar 5 of each 8. |
| tidewarden_p2 | Core exposed: the same tide gets more urgent with triplet hats, a staccato string ostinato, and the anthem every 4 bars. A riser every 8 bars builds tension. |
| stinger_boss_phase2 | Phase change: a riser and snare build climax exactly at 1.25 s, matching BOSS_TIDE_PHASE2 (core reveal flare, frame 75). An anthem-head hit then lands into P2. |
| titan_<arena> | Molten forge, relentless: 150 BPM 8th kicks, 16th palm-muted overdriven riff, anvil on every downbeat, anthem on piano. Arena colours: Foundry Crater hammer and anvil, Cinder Bridge staccato strings, Ash Citadel low drone and piano tolls, Obsidian Forge taiko/tom ensemble. |
| titan_int25/50/75 | Damage stages raise the tension: 16th hats, then a counter string ostinato, then snare builds + the anthem an octave up + a riser every 8 bars (cumulative layers). |
| stinger_boss_defeat | Triumph: the minor anthem over chugs and taiko, then the motif in D major over IV–V–I, blooming into Dmaj9 with lush reverb. |
| stinger_win / stinger_win_3star | Magical reward: the motif on warm piano over a IV–V pad swell, resolving to a rolled Cmaj9 nylon arpeggio with lush reverb. 3-star adds a climbing shimmer (kept at or below G5). |
| stinger_chapter_clear | Journey complete: IV–V–vi–IV–V–I with fingerpicked arpeggios and a big Cmaj9 bloom. |
| results_loop | Satisfied afterglow: fingerpicked maj7/add9 colours over the unchanged soft bed, with a quiet piano motif. |
| stinger_fail | Cheeky deflate: the motif starts, then slides down chromatically on nylon guitar and piano. Never harsh. |
| season1 / season2 hubs | Wrecking Frontier gallop (F major) and Stormfront rain and thunder (E minor), leads now on guitar and piano. |

### Main theme reference (user-approved)
music/main_theme.ogg (v1) is APPROVED and frozen. Its arrangement is the reference for what works: a clear 4-bar statement of the motif, uke-strum bed, A A' B A'' form, and a bright melody kept sparse over a steady groove. master.py writes any re-render only to main_theme_alt_piano.ogg.

### Known sync issue (v2)
The Tidewarden tell pulses at exactly 2 Hz (120 BPM quarter notes), but the boss music is now 144 BPM, so the tell drifts against the beat. That is fine if the tell is meant to stand out. If Animator or Game designer want it locked, the options are: keep the drift, drop the boss to 120 BPM (loses the requested pace), or retime the tell to 2.4 Hz (144 BPM quarter notes). Decision needed.

## Boss music v3 (2026-10-16): each boss has its own identity. v2 is in music/_v2_archive/
**Shared 32-bar dynamic arc:**
- DROP (bars 0–7): boss motif ↔ heroic hook, call and response.
- CALL/RESPONSE (8–15): low-piano/chug call answered by the piano hook, with m2 + tritone stabs.
- FILTERED BREAKDOWN (16–19): low-passed to 420 Hz, heartbeat sub-pulse doubles, lonely motif.
- REBUILD (20–27): tremolo strings crescendo, piano octave tremolo, a 4-bar Shepard riser from bar 24.
- GATE/STUTTER BUILD (28–31): trance gate, stutters, and a reversed swell that sucks into the DROP at the loop point.

Sub-booms land every 8 bars. Leads are sampled piano, guitars and strings, with no harsh highs. Stem and voice rules are unchanged.

| Track | Design intent |
|---|---|
| tidewarden_p1 / p2 (144 BPM, 12/8) | **Ocean/tide.** The "Tide Call" motif (A–D–F–A swell leaning on the G# tritone) rolls like waves over triplet palm-muted chugs and a deep sub. The heroic hook (C–F–A–C) answers. Intro: heartbeat, rising tide, Shepard riser, reversed swell into the fight. P2 shifts the harmony (Bb–A–Dm–C) and adds triplet hats and string ostinato, so it sounds less stable. |
| stinger_boss_phase2 | Shepard riser + tremolo + reversed swell build to a sub-boom hit at exactly 1.25 s (BOSS_TIDE_PHASE2), then the Tide Call head with m2 chugs. |
| stinger_tidewarden_defeat | The Tide Call is stated one last time and collapses, then the heroic hook turns to D major, with Gadd9 → A → Dmaj9 and a nylon bloom. |
| titan_<arena> + int25/50/75 (150 BPM) | **Molten forge, relentless.** The "Forge Call" motif hammers repeated Ds and leans on Ab, answered by the boss anthem. 16th overdriven riff, anvil on every downbeat, tritone anvil answer in the breakdown. Intensity layers: int25 adds hats + double heartbeat, int50 adds the string ostinato + m2 stabs, int75 adds snare builds, the anthem an octave up, tremolo, and a Shepard riser every 8 bars. |
| stinger_titan_defeat | Forge Call over chugs and a riser, sub-boom meltdown, then the anthem in D major (Dmaj9). |

### Guardians (Ch2–5, later), music and SFX direction from Content_Roadmap_Addendum_v1
| Guardian | Music identity | Signature SFX |
|---|---|---|
| Iron Colossus (Gearstorm City) | factory march, piston ostinato, ratchet perc, low brass-like overdriven chugs; "Gear Call" motif in 7/8 bursts | piston slams, steam vents, defence-building crane whirs |
| Clockmaker (Neon District) | ticking ostinato, rotating panned arps, music-box piano motif turned sinister | gear tick-tock tells, armour rotation grind, glass/energy shield shatter |
| Crystal King (Crystal Frontier) | resonant crystal piano clusters, slow tremolo strings, icy reversed swells | crystal resonance hum (tell), redirected-energy zaps, ice cracks |
| Sky Fortress (Titan's Reach) | storm anthem, thunder taiko, Shepard risers between stabilizer stages | stabilizer breaks, wind gusts, thunder hits, core overload |
