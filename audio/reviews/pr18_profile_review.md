# PR #18 review: ImpactAudioProfile + PHYS_* events (Sound Manager)
PR: https://github.com/radhengle/crashverse/pull/18 "Impact feel: ImpactEvent via AnimEventBus" (base physics/fixed-step-lock, stacked on #6). Reviewed at the PR head, read-only. Nothing was posted to GitHub.
Checked against: audio/mixing_unity.md §"Physics impact audio contract", sfx_list.csv, anim_audio_map.csv.
Files: Physics/Core/ImpactEvent.cs, Physics/ImpactFeel.cs, SimRunner.cs (+Stepped event), tests.

## Payload: matches the contract
The PHYS_IMPACT/CRACK/BREAK/JOINT_SNAP/COLLAPSE_START/COLLAPSE_END event names map one to one from ImpactKind. Every agreed field is present: tick, kind, tier, impulse, materialA/B, position, normal, pieceId, sizeClass, damageStep (1..3 on Crack), cascadeId, cascadeCount, source. They also added sourceId (the power-up id is a separate field, not packed into source) and seq. Ordering within a tick is deterministic, with CollapseStart first and CollapseEnd last, and events flush after the physics step. Audio is presentation-only, as agreed.
Payload notes:
- P1. materialA/B are int MaterialDefinition indices, with -1 meaning ground. Audio needs a stable id→family mapping (wood/brick/steel/terrain). Ask for a MaterialFamily enum on MaterialDefinition, or carry the family in the payload.
- P2. CollapseStart.cascadeCount is the *planned* count and CollapseEnd's is the *final* count. That's fine. We size COLLAPSE_LAND from End. CollapseStart has no material, though, and we need one to choose wood/brick/steel collapse (anim_audio_map COLLAPSE_LAND material param). Ask for a dominant material on CollapseStart/End.
- P3. tier is classified from global thresholds when the event is raised (sim side), so per-material thresholds would have to change classification in Core or be re-tiered on the audio side. Prefer an audio-side re-tier, so the sim never depends on audio data.
- P4. SimRunner.Step calls Physics.Simulate only when Ranked. In non-ranked/FixedUpdate mode, Stepped fires before Unity's automatic physics step, so collision reports land in the *next* tick's flush, stamped with the next tick. That's minor for audio (20 ms), but the per-piece cooldown should allow for it. Please confirm the intent.
- P5. The destruction code doesn't call Report*/BeginCollapse yet (the follow-up PR wires this). Nothing is audible until then.
- Existing anim_audio_map rows (IMPACT_*, BREAK_*, COLLAPSE_LAND cooldown 250 ms, DEBRIS_BURST) still fire alongside PHYS_*. Decide which source wins so nothing plays twice. Plan: PHYS_* for impacts and breaks, the anim events only for camera/VFX-synced cues.

## Profile checklist
| Requirement | Status | In PR |
|---|---|---|
| Tier thresholds per material | MISSING | Only global thresholds (40/150 N·s) |
| Volume & pitch curves per kind | MISSING | One impulseToVolume and one impulseToPitch curve shared by all kinds |
| sizeClass pitch offset S/M/L | MISSING | sizeClass is classified (5/50 kg) but has no audio offset |
| Random pitch/volume variance | PARTIAL | pitchJitter 0.08 only, no volume variance |
| Per-piece cooldown (ticks, default 4) | MISSING | Not in the profile (contract says 4 ticks) |
| Per-material voice cap within 12/24/32 | MISSING | Only the global voicesLow/Mid/High 12/24/32 (present) |
| Rattle window 100 ms | PRESENT | rattleWindowMs=100, rattleMaxVoices=3. Should be in ticks (5) to follow the tick rule |
| Collapse rule (1 COLLAPSE_LAND per cascadeId, count→size) | MISSING | Cascade tracking exists, but there's no audio rule or mapping |
| Min-impulse cutoff | MISSING | Implied only by the volume curve starting at 5 N·s |
| Max distance / attenuation | MISSING | |
| Source priority Cannon>Explosion>PowerUp>Debris | MISSING | The enum order is Cannon0, PowerUp1, Debris2, Explosion3, used only for ordering ties. It isn't a priority |
| Material-pair → bank, mixed pairs resolved | MISSING | No bank mapping |

## Suggested defaults (proposed additions to ImpactAudioProfile)
```
[Serializable] struct MaterialAudio { MaterialFamily family; float mediumMin, heavyMin; int voiceCapLow, voiceCapMid, voiceCapHigh; string bank; int hardness; }
materials:
  wood : medium 30, heavy 120 N·s, caps 4/8/10, bank SFX/Destruction/Wood,  hardness 1
  brick: medium 40, heavy 150 N·s, caps 4/8/10, bank SFX/Destruction/Brick, hardness 2
  steel: medium 60, heavy 200 N·s, caps 3/6/8,  bank SFX/Destruction/Steel, hardness 3
  terrain: bank sfx_impact_terrain, hardness 0
  (the sum of the material caps stays below 12/24/32, which leaves room for cannon/UI/boss)
kindCurves (volume x=impulse 0..300 / pitch multiplier):
  Impact   : vol 0.06→1.0 ease, pitch 1.05→0.85
  Crack    : vol 0.25→0.8, pitch 1.10→0.95 (+0.03 per damageStep)
  Break    : vol 0.40→1.0, pitch 1.00→0.85
  JointSnap: vol 0.50→1.0, pitch 1.00→0.90 (steel only)
sizePitchOffsetSemitones: S +2, M 0, L -3   (sizeVolumeMul S 0.8, M 1.0, L 1.15 capped at 1)
pitchVarianceSemitones ±1.0 (≈ current 0.08 jitter); volumeVarianceDb ±1.5
perPieceCooldownTicks 4 (80 ms @50 Hz, on event.tick)
rattleWindowTicks 5 (100 ms), rattleMaxVoices 3
minImpulseCutoff 5 N·s (Impact only; Crack/Break/JointSnap always pass)
attenuation: Logarithmic, minDistance 5 m, maxDistance 40 m, spatialBlend 0.5 (keep the 2.5D game readable)
sourcePriority (Unity priority, lower = more important): Cannon 32, Explosion 64, PowerUp 96, Debris 192
  voice stealing: lowest priority first, then the quietest
pairResolution: HarderWins (hardness above), and the softer material adds a 30% layer only on Heavy; ground (-1) → the other material's bank, plus a terrain layer
collapse:
  on PHYS_COLLAPSE_START: duck/suppress per-piece PHYS_BREAK of the cascade to 1 voice + rattle
  on PHYS_COLLAPSE_END: play exactly one COLLAPSE_LAND per cascadeId (material = dominant)
  count→size: 1–5 small (sfx_*_collapse vol 0.6, pitch +2 st), 6–20 medium (vol 0.85), 21+ large (vol 1.0, pitch -2 st, + sub layer)
  failsafe: if End hasn't arrived after 150 ticks (3 s), play using the running count
```
## Asks for Physics Developer (to send through the parent, not posted)
1. A material-family mapping (P1) and the dominant material on Collapse events (P2).
2. Confirm the non-ranked tick stamping (P4).
3. Agree that Sound Manager may extend ImpactAudioProfile with the fields above (audio-side only, no sim impact). Sound Manager can author it on a follow-up branch if preferred.
