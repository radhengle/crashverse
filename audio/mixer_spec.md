# CRASHVERSE AudioMixer spec (v1): create in Unity as `Assets/_Project/Audio/CrashverseMixer.mixer`
A hand-written .mixer YAML isn't shipped, because it needs Unity-generated GUIDs/fileIDs for every group, effect and snapshot and can't be checked without the Editor. Create it in Unity (Window > Audio > Audio Mixer) exactly as below. Unity will write the .mixer and .meta.

## Group tree (Master > ...)
| Group | Parent | Default vol | Effects (in order) | Who routes here |
|---|---|---|---|---|
| Master | — | 0 dB | Attenuation | — |
| Music | Master | -3 dB | **Duck Volume** (receives sidechain), Attenuation | — |
| Music/Base | Music | 0 dB | Attenuation | MusicPlayer.baseGroup |
| Music/Layers | Music | 0 dB | Attenuation | MusicPlayer.layerGroup (Titan intensity, arena layers) |
| Music/Stingers | Music | 0 dB | Attenuation, **Send → Music Duck** | MusicPlayer.stingerGroup |
| SFX | Master | 0 dB | Attenuation | — |
| SFX/Destruction | SFX | -2 dB | Attenuation | routing.json group SFX/Destruction (all PHYS_*, debris, creak) |
| SFX/Weapon | SFX | 0 dB | Attenuation, **Send → Music Duck** | cannon fire/breech/reload/ready |
| SFX/PowerUps | SFX | -1 dB | Attenuation, Lowpass Simple (cutoff 22000 Hz, exposed) | sfx_pu_* |
| SFX/Boss | SFX | 0 dB | Attenuation, **Send → Music Duck** | Tidewarden / Titan |
| SFX/Rewards | SFX | -2 dB | Attenuation | reward, combo, results stars |
| UI | Master | -4 dB | Attenuation | sfx_ui_* (never ducked, never lowpassed) |

Stingers duck the music *base and layers*. Put Duck Volume on Music/Base and Music/Layers rather than on Music, so a stinger doesn't duck itself. (So: Music has only Attenuation; Base and Layers each get a Duck Volume receiving the sends.)

## Ducking (Duck Volume on Music/Base and Music/Layers)
Sidechain sources: Send effects on **SFX/Weapon, SFX/Boss, Music/Stingers** (send level 0 dB).
Threshold -25 dB · Ratio 4:1 · **Make-up/target reduction ≈ -5 dB** (set Ratio/Threshold so a nominal cannon hit gives -5 dB; Unity's Duck Volume has no direct "depth", so tune Threshold until the meter reads -5) · **Attack 10 ms · Release 300 ms** · Knee 10 dB · Sidechain mix 100%.

## Exposed parameters (names are the contract for code and Settings)
| Name | Target | Range | Default |
|---|---|---|---|
| `MasterVol` | Master attenuation | -80..0 dB | 0 |
| `MusicVol` | Music attenuation | -80..0 | -3 |
| `SfxVol` | SFX attenuation | -80..0 | 0 |
| `UiVol` | UI attenuation | -80..0 | -4 |
| `PowerUpLPF` | SFX/PowerUps Lowpass cutoff | 500..22000 Hz | 22000 |
Settings sliders map 0..1 → dB as `20*log10(max(v,0.0001))`. Mute = -80.

## Snapshots
| Snapshot | Changes from Default | Trigger |
|---|---|---|
| **Default** | as above | boot, CAM_SLOWMO_END, PU_TIME_END |
| **SlowMo** | Music -4 dB, SFX/Destruction lowpass not available → SFX -1 dB, PowerUpLPF 2500 | CAM_SLOWMO_START (0.15 s transition) |
| **TimeFracture** | Music -6 dB, PowerUpLPF 1800, SFX/Destruction -3 dB | PU_TIME_START (0.1 s), back to Default on PU_TIME_END (0.3 s) |
| **Paused** | Music -10 dB, SFX -80, UI 0 | app pause / modal pause menu (0.2 s) |
| **Results** | SFX/Destruction -12 dB, Music 0 | win/fail screen in (0.4 s) |
| **Muted** | Master -80 | app focus lost (Android) |

## Voice / priority
Unity voice limits are set by code (AudioSettings, real voices 32 / virtual 64). The pool caps per device tier 12/24/32 come from ImpactAudioProfile. AudioSource.priority per clip is in audio/sfx_routing.json.
