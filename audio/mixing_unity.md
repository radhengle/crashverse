# CRASHVERSE — Mixing & Unity Audio Spec v1 (Android, URP)

## AudioMixer group tree (CrashverseMixer.mixer)
```
Master  (exposed: MasterVol)
├─ Music  (exposed: MusicVol)  — Duck Volume receive
│   ├─ Music/Base
│   ├─ Music/Layers   (Rhythm, Lead, Intensity, P2)
│   └─ Music/Stingers
├─ SFX  (exposed: SfxVol)
│   ├─ SFX/Weapon
│   ├─ SFX/Destruction
│   │   ├─ Impacts
│   │   ├─ Breaks
│   │   ├─ Collapse   — Send → Music duck (big breaks)
│   │   └─ Debris
│   ├─ SFX/PowerUps
│   ├─ SFX/Boss       — Send → Music duck
│   └─ SFX/Stingers   (combo, stars, win/fail SFX) — Send → Music duck
├─ UI  (exposed: UiVol) — Send → Music duck (light)
└─ Ambience (island wind/sea beds, Streaming, optional)
```
Exposed params drive Settings sliders (store linear 0–1, set dB = 20*log10(max(v,0.0001))).

## Snapshots
| Snapshot | Use | Changes | Transition |
|---|---|---|---|
| Default | Gameplay | all 0 dB, Music −6 dB | 0.3 s |
| Menu | Map/screens | Music 0 dB, Destruction −80 | 0.5 s |
| Paused | PauseModal | Music LPF 800 Hz −6 dB, SFX −80 | 0.2 s (unscaled) |
| Results | Win/Fail | Music −12 dB, Stingers +0 | 0.25 s |
| BossP2 | Tidewarden P2 | Music Layers +3 dB, Boss +2 | 1 bar |
| SlowMo | CAM_SLOWMO (big collapse / boss phase; never win shot, Titan ranked, Reduce Motion) | LPF 2 kHz on SFX+Music | 0.1 s in / 0.3 s out |
| TimeFracture | Power-up active (starts on ARM) | SFX pitch 0.8, LPF 3 kHz on Destruction | 0.15 s in/out |
| Background | OnApplicationPause | Master −80 | 0 s |

## Ducking (Duck Volume on Music, fed by sends)
| Source | Threshold | Ratio | Attack | Release | Max dip |
|---|---|---|---|---|---|
| Collapse/Boss (big breaks) | −20 dB | 4:1 | 10 ms | 400 ms | −6 dB |
| Stingers (combo tier, stars, win/fail) | −25 dB | 6:1 | 5 ms | 600 ms | −9 dB |
| UI | −30 dB | 2:1 | 5 ms | 150 ms | −3 dB |
Victory/fail stinger also stops level music via code (fade 0.3 s) — ducking alone is not enough.

## Voice limits
- Project Settings ▸ Audio: Max Virtual Voices 64, Max Real Voices 24 (low-tier devices from QualityController: 16).
- Pitch/LPF effects for SlowMo/TimeFracture run on mixer, not Time.timeScale-dependent AudioSource pitch for UI.
- Per category caps (enforced by pool): Debris 6, Breaks 6, Impacts 4, Collapse 2, Weapon 2, PowerUps 2, Boss 3, Stingers 2, UI 2, Music 3 streams (level/arena mix + 1 cumulative layer + stinger; see Music stem-voice rule), Ambience 1.

## AudioSource pooling spec (AudioService, new script e.g. Scripts/Audio/AudioService.cs)
- Pool: 24 pre-warmed AudioSources on a DontDestroyOnLoad root (16 on low tier); 2D sources for UI/stingers, 3D (spatialBlend 0.6, logarithmic, min 5 m, max 60 m) for world SFX — game camera is orbit, keep partly 2D so mobile speaker mix stays even.
- Request: `Play(id, pos, volScale=1, pitch=1)`; random variation without immediate repeat; pitch jitter ±4% (combo_step uses explicit pitch, no jitter).
- Priority stealing: Unity priority 0 = most important, 256 = least. If the category cap or pool is full, steal the voice with the largest priority number (least important), tie-break oldest then quietest. Never steal Music, Stingers or UI. If the new request is less important than everything playing, drop it.
- Cooldowns / spam control:
  - Debris: max 1 per material per 50 ms, max 6 simultaneous, volume scaled by 1/sqrt(n in last 200 ms). DebrisPool.SpawnBurst triggers ONE debris sound per burst, not per chunk.
  - Breaks: same id ≤ 1 per 30 ms; if >8 breaks in 100 ms, play one Collapse instead (batching).
  - Collapse: ≤ 1 per 250 ms.
  - Crack: ≤ 1 per block per 150 ms.
  - Combo step: no cooldown (window is 0.8s by design) but max 1 per frame.
  - UI click: ≤ 1 per 60 ms.

## Android import settings
- Project Settings ▸ Audio: DSP Buffer Size = **Best Latency** (256) default; fall back to Good Latency (512) on QualityController low tier if crackle reported. Sample rate: 48000 (device native) system, assets authored 44.1/22.05 kHz.
| Category | Load Type | Format | Quality | Channels | Rate | Preload |
|---|---|---|---|---|---|---|
| UI clicks (<0.5 s) | Decompress On Load | ADPCM | — | Mono | 22.05 kHz | Yes |
| Short SFX (crack/break/debris/weapon, <1 s) | Decompress On Load | Vorbis | 50% | Mono | 22.05 kHz (weapon/impact 44.1) | Yes |
| Medium SFX (collapse, power-up fx, boss, 1–5 s) | Compressed In Memory | Vorbis | 45–55% | Mono | 44.1 kHz | Yes (scene-bundled) |
| Stingers / stars | Decompress On Load | Vorbis | 60% | Stereo | 44.1 kHz | Yes |
| Music & stems | Streaming | Vorbis | 40% (stems), 50% full mixes | Stereo (Base), Mono (Lead layers OK) | 44.1 kHz | No (load in background) |
| Ambience | Streaming | Vorbis | 35% | Stereo | 22.05 kHz | No |
- Force To Mono on everything except stingers/music. Load In Background on for music.
- Boss/Titan banks loaded only in those scenes (Addressables or Resources groups) and unloaded after.

## Memory/size budget (PROPOSAL — to confirm with tech lead)
| Bucket | Budget |
|---|---|
| Resident decompressed SFX (UI+short) | ≤ 12 MB |
| Compressed-in-memory medium SFX | ≤ 4 MB |
| Streaming buffers (4 streams) | ≤ 2 MB |
| Total audio in APK/AAB base | ≤ 25 MB (music ~16 MB, SFX ~7 MB, headroom 2 MB) |
| Ch2–5 music + guardians, Season 2+ | remote download (Addressables/Play Asset Delivery), not in base APK budget |
Check with Profiler ▸ Audio + Memory Profiler on a low-tier device (2–3 GB RAM).

## Animation event routing
Animator raises `AnimEventBus.Raise(name, payload)` (per animation/audio_sync_table.md; class not yet in code at 0d60e07). AudioService subscribes and looks up anim_audio_map.csv (baked to a ScriptableObject). Anim events only trigger visuals/sound, never gameplay. On 30 fps devices events fire on the nearest frame ≥ ms. Where an event has no anim counterpart, code hooks in audio_events_for_developer.csv fire audio directly. Never fire the same sound from both paths: each sfx id has a single owner (anim or code), marked in anim_audio_map.csv.

## Music stem-voice rule (Android, 2026-10-16)
- Max 3 music AudioSources playing: Music/Base (level or titan_<arena> pre-mix), Music/Layers (exactly one cumulative layer: titan_int25/50/75 or P2-style layer), Music/Stingers.
- Layer change = equal-power crossfade over 1.0 s on two Layer sources (A/B). Start the new layer with `timeSamples = base.timeSamples` (or PlayScheduled on a bar boundary) so it stays sample-locked. Stop the old source once the crossfade ends. If a stinger is requested during a crossfade, finish the crossfade or snap it to the end first, so no more than 3 voices ever play.
- Titan: damage 25/50/75% → crossfade to titan_int25/50/75. Never stack layers. Levels already include headroom: arena + any layer ≤ -1.3 dBTP at 0 dB faders.
- Tidewarden: P1 (intro + loop at sample 330750) → stinger_boss_phase2 at the bar boundary → tidewarden_p2 (loop 0 to end). P2 is a separate full mix, not a layer.
- Import: Streaming, Vorbis; loop region via split clips or a custom loop player (sample offsets in music/loop_points.md).

## v2 timing updates (2026-10-16)
- Titan: 150 BPM, loops now 51.2 s / 2257920 samples (all 7 files). Layers are cumulative; crossfade them per the rule above.
- Tidewarden P1: 144 BPM, intro 0–294000, loop 294000 → 2646000. P2 loop 0 → 2352000. Switch at a P1 bar boundary (bar = 73500 samples).
- stinger_boss_phase2 (2.68 s): start it on BOSS_TIDE_ARMS_DROP (t=0). Its hit lands at 1.25 s, matching BOSS_TIDE_PHASE2 (frame 75). Start tidewarden_p2 at the hit, or on the next bar.
- main_theme.ogg is the approved v1 and stays unchanged.

## Physics impact audio contract (agreed with Physics Developer, 2026-10-16)
- ImpactEvent via AnimEventBus: tick, kind (Impact/Crack/Break/JointSnap), tier (light/medium/heavy), impulse, materialA/B, position, normal, pieceId, sizeClass (S/M/L), damageStep (cracks only), cascadeId and cascadeCount (Break during a collapse), source (Cannon/PowerUp+id/Debris/Explosion).
- CollapseStart / CollapseEnd(final count): play one material collapse sound (COLLAPSE_LAND) per cascade, not one per piece.
- Sim at 50 Hz. Rate-limit one sound per piece per 4 ticks (80 ms), measured on the event's tick, not wall time.
- SFX voice limit 12/24/32 (low/mid/high tier); debris combined every 100 ms. Replaces the earlier 16/24 pool and 50 ms gap.
- Curves and tier thresholds live in the ImpactAudioProfile ScriptableObject, owned by Sound Manager.
- Audio is presentation-only and never feeds back into the sim.

## Boss v3 notes (2026-10-16)
- Tidewarden tell is now 2.4 Hz (pulses at 0 and 417 ms = 25 f). Animator and code need the same change (they currently use 2 Hz / 30 f).
- sfx_boss_tw_shutter_close has its thunk at 300 ms. Trigger it at the START of the 300 ms close tween. The anim table currently fires the event at 18 f (the thunk), so either move the event to 0 f or trim the file.
- sfx_boss_tw_arms_drop is a 600 ms pre-roll that ends exactly at ARMS_LAND.
- Core hum (sfx_boss_tw_core_hum) loops at 1.5 s, ~-11 LUFS raw. Play it at about -12 dB under the P2 music. Rule loops (sfx_titan_rule_*) loop at 2.0 s, around -10 dB.
- Boss SFX voice budget: SFX/Boss category cap 3. Roars (3 var) never steal impacts. Use the priority numbers from sfx_list.csv.
- New stingers: stinger_tidewarden_defeat / stinger_titan_defeat replace stinger_boss_defeat for these bosses.
- Titan v3 mixes measure -17.3 to -18.1 LUFS (the headroom goes to the crossfade-midpoint peak rule), so add +1.5 dB on the Titan music snapshot.
