# CRASHVERSE: Haptics spec (v1)

Owner: Gameplay Developer. Code: `Assets/_Project/Scripts/Feel/Haptics.cs` (`IHaptics`, `HapticBuckets`, `HapticGate`), `AndroidHaptics.cs`, `ShakeDispatcher` in `FeelCore.cs`.
Sources: `animation/02_projectile_impact.md`, `animation/07_camera_shake_slowmo.md`, `animation/audio_sync_table.md`, `ShakeRouter` (#29).

## 1. Model

Haptics are driven by **the same accepted shake events** as the camera: `ShakeDispatcher.Submit` routes the event through `ShakeRouter`, dedupes it per family per frame (the strongest wins), then pulses once.
An event that does not shake (it isn't routed, a light `PHYS_IMPACT`, `PHYS_CRACK`, `PHYS_COLLAPSE_START`) produces **no** haptic.

strength = min(1, max(pos / 0.15 m, rot / 1.5°)) → bucket:

| Bucket | Name | strength | Duration | Android amplitude (1–255) |
|---|---|---|---|---|
| 0 | none | < 0.05 | — | — |
| 1 | `light_tick` | 0.05 – < 0.20 | 12 ms | 60 |
| 2 | `impact` | 0.20 – < 0.45 | 25 ms | 120 |
| 3 | `break_med` | 0.45 – < 0.70 | 40 ms | 180 |
| 4 | `boom` ★ | ≥ 0.70 | 60 ms | 255 |

Each bucket is a single `VibrationEffect.createOneShot(ms, amp)`, built once and cached (API 26+; below 26 the pattern is `vibrate(ms)`).
**★ fallback:** if the device has no amplitude control, only buckets 3–4 vibrate, so light ticks don't become full-strength buzzes.

## 2. Per-event patterns (every ShakeRouter route)

| Event(s) | Shake (07/02) | strength | Haptic |
|---|---|---|---|
| `ANIM_WRECKER_FIRE` | Fire 0.06 m / 0.6° | 0.40 | 2 `impact` 25 ms / 120 |
| `IMPACT_WOOD`, `BREAK_WOOD`, `IMPACT_GLASS`, `BREAK_GLASS` | ImpactWood 0.02 / 0.2° | 0.13 | 1 `light_tick` 12 ms / 60 |
| `IMPACT_BRICK` | BrickHit 0.02 / 0.2° | 0.13 | 1 `light_tick` |
| `IMPACT_STEEL`, `IMPACT_STEEL_JOINT`, `PU_PHASE_HIT` | SteelRing 0.01 / 0.1° | 0.067 | 1 `light_tick` |
| `IMPACT_TERRAIN` | Terrain 0.01 / 0.1° (provisional) | 0.067 | 1 `light_tick` |
| `BREAK_BRICK`, `PHYS_BREAK` (unknown material) | BrickBreak 0.04 / 0.4° | 0.27 | 2 `impact` |
| `BOSS_TIDE_CORE_HIT`, `BOSS_TITAN_HIT` | BrickBreak | 0.27 | 2 `impact` |
| `BREAK_STEEL_JOINT`, `BOSS_TIDE_JOINT_BREAK`, `PHYS_JOINT_SNAP` | JointBreak 0.06 / 0.6° | 0.40 | 2 `impact` |
| `COLLAPSE_LAND`, `PHYS_COLLAPSE_END`, `BOSS_TIDE_ARMS_LAND` | CollapseLand 0.08 / 0.8° | 0.53 | 3 `break_med` 40 ms / 180 |
| `PU_GRAVITY_BURST` | GravityBurst 0.08 / 0.8° | 0.53 | 3 `break_med` |
| `EXPLODE_CRATE` (alias `EXPLODE_BARREL`), `PU_CHAIN_PULSE` | Explosion 0.10 / 1.0° | 0.67 | 3 `break_med` |
| `BOSS_TIDE_PHASE2`, `BOSS_TIDE_DEFEAT`, `BOSS_TITAN_STAGE`, `BOSS_TITAN_DEFEAT` | BossPhase 0.12 / 1.2° | 0.80 | 4 `boom` 60 ms / 255 ★ |
| `PHYS_IMPACT` medium/heavy | by material: wood/glass → ImpactWood, brick → BrickHit, steel → SteelRing, terrain → Terrain | | 1 `light_tick` |
| `PHYS_IMPACT` light, `PHYS_CRACK`, `PHYS_COLLAPSE_START` | none | — | none |
| `PHYS_BREAK` by material | wood/glass → ImpactWood (1), brick → BrickBreak (2), steel → JointBreak (2) | | as listed |

Not routed today, so no haptic: `BREAK_STEEL` (non-joint), UI events (`UI_*`, chest), `PU_TIME_*`, `ANIM_WRECKER_RECOIL_LAND`. A UI-tap haptic is out of scope for v1.

## 3. Toggle rules

Haptics fire only when **both** of these hold (`MotionSettings.HapticsEffective`):

1. **Haptics toggle** is ON (`haptics_enabled` pref, default ON). This is a separate Settings row that the user controls independently.
2. **Reduce motion** is OFF. Reduce motion **overrides** the toggle (PM-approved, 2026-10):
   - explicit Reduce motion ON → no haptics;
   - **Android auto**: with no explicit Reduce motion choice, the OS "Remove animations" setting turns Reduce motion on, and that also suppresses haptics. An explicit Reduce motion OFF beats auto.
   - The toggle's stored value isn't changed. Turning Reduce motion off brings back whatever the user had chosen. Settings shows `Haptics: ON (off while Reduce motion is on)`.
3. **Reduce shake** alone does **not** affect haptics. It only zeroes the camera shake.

Enforced at one point (`ShakeDispatcher.Submit`), so every caller (`CameraShake.Add/AddPhys`, FeelDirector) is covered.

## 4. Rate limiting

- At most **1 pulse per 40 ms**, measured on real (unscaled) time, so hit stop and slow-mo don't stretch it (`HapticGate`, `HapticBuckets.MinGapMs`).
- Exception: a **stronger** bucket may cut in within 40 ms (★ cancels a lower one). Equal or weaker pulses are dropped.
- Per-frame family dedupe runs before the gate (one impact pulse per frame, the strongest).
- Suppressed pulses (toggle/Reduce motion) don't touch the gate.

## 5. Timing alignment with SFX / animation (pending Sound Manager/Animator confirmation)

The pulse fires on the **same frame** as the routed event, i.e. the frame the SFX trigger and the shake start (all `0 / 0` offsets in `audio_sync_table.md` unless noted):

| Event | Offset (frame / ms) | Note |
|---|---|---|
| `ANIM_WRECKER_FIRE` | 0 at launch = +4 f / 66 ms after FIRE tap | pulse on launch with cannon boom, not on tap. *Confirm.* |
| `IMPACT_*` / `BREAK_*` | 0 / 0 | with material hit / snap SFX |
| `EXPLODE_CRATE` | 6 / 100 after swell | pulse at the blast, not swell start. *Confirm the SFX transient is at 100 ms.* |
| `COLLAPSE_LAND` | 0 / 0 per segment contact | multiple segments within 40 ms are merged by the gate. *Confirm that's acceptable vs. per-material thuds.* |
| `PU_GRAVITY_BURST` | 90 / 1500 | pulse on implode+burst. *Confirm the transient sits at the start of BURST.* |
| `PU_CHAIN_PULSE` | 0; links +5 f (80 ms) each | only the PULSE vibrates; `PU_CHAIN_LINK` doesn't. *Should each link tick?* |
| `BOSS_TIDE_ARMS_LAND` | 36 / 600 after ARMS_DROP | |
| `BOSS_TIDE_PHASE2` | 75 / 1250 | |
| `BOSS_TIDE_DEFEAT` | 0 / 0 (clip 4.0 s) | single `boom` at start. *Want a second pulse at the final collapse?* |

Android vibrator latency (~10–30 ms) isn't compensated. Sound Manager: confirm whether the SFX lead/lag needs a haptic offset.
