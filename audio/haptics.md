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
| `IMPACT_TERRAIN` | Terrain 0.01 / 0.1°, 80 ms (final) | 0.067 | **none** (shake only) ✅ Animator |
| `BREAK_STEEL` (non-joint) | SteelBreak 0.03 / 0.3°, 150 ms, Break family | 0.20 | 1 `light_tick` (override) ✅ Animator |
| `BREAK_BRICK`, `PHYS_BREAK` (unknown material) | BrickBreak 0.04 / 0.4° | 0.27 | 2 `impact` |
| `BOSS_TIDE_CORE_HIT`, `BOSS_TITAN_HIT` | BrickBreak | 0.27 | 2 `impact` |
| `BREAK_STEEL_JOINT`, `BOSS_TIDE_JOINT_BREAK`, `PHYS_JOINT_SNAP` | JointBreak 0.06 / 0.6° | 0.40 | 2 `impact` |
| `COLLAPSE_LAND`, `PHYS_COLLAPSE_END`, `BOSS_TIDE_ARMS_LAND` | CollapseLand 0.08 / 0.8° | 0.53 | merged per 40 ms window (§4): wood/glass 2, brick/stone/unknown 3 `break_med`, steel 4. ✅ Sound Manager |
| `PU_GRAVITY_BURST` | GravityBurst 0.08 / 0.8° | 0.53 | 1 `light_tick` at implode start (1500 ms) + 3 `break_med` at burst-ring peak (+100 ms, minus lead → +80 ms) ✅ Animator |
| `EXPLODE_CRATE` (alias `EXPLODE_BARREL`) | Explosion 0.10 / 1.0° | 0.67 | 3 `break_med` |
| `PU_CHAIN_PULSE` | Explosion 0.10 / 1.0° | 0.67 | 2 `impact` (medium override) ✅ Animator |
| `PU_CHAIN_LINK` | none (haptic only) | — | 1 `light_tick` per link, 80 ms stagger, max 5 per activation (count resets on PULSE) ✅ Animator |
| `BOSS_TIDE_DEFEAT_COLLAPSE` (new, +210 f / 3500 ms) | none (haptic only) | — | 2 `impact` (medium) ✅ Animator. Until the rig clip lands, **both** `BOSS_TIDE_DEFEAT` and this event are raised from code-driven `TidewardenController.Defeat()`: DEFEAT immediately (once per defeat, `TideDefeatEvent`), COLLAPSE via `TideDefeatCollapseTimer` (+3500 ms unscaled, once, cancelled on exit/reset). Then both move to the clip (frame 0 / frame 210) |
| `BOSS_TIDE_PHASE2`, `BOSS_TIDE_DEFEAT`, `BOSS_TITAN_STAGE`, `BOSS_TITAN_DEFEAT` | BossPhase 0.12 / 1.2° | 0.80 | 4 `boom` 60 ms / 255 ★ |
| `PHYS_IMPACT` medium/heavy | by material: wood/glass → ImpactWood, brick → BrickHit, steel → SteelRing, terrain → Terrain | | 1 `light_tick` (terrain: none) |
| `PHYS_IMPACT` light, `PHYS_CRACK`, `PHYS_COLLAPSE_START` | none | — | none |
| `PHYS_BREAK` by material | wood/glass → ImpactWood (1), brick → BrickBreak (2), steel → JointBreak (2) | | as listed |

Not routed, so no haptic: UI events (`UI_*`, chest), `PU_TIME_*`, `ANIM_WRECKER_RECOIL_LAND`. A UI-tap haptic is out of scope for v1.

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
- **COLLAPSE_LAND merge** ✅ *confirmed by Sound Manager*: the haptic for every landing that arrives within 40 ms of the first one is merged into **one** pulse, sent when the window closes (≤ 40 ms latency, `ShakeDispatcher.Tick`). Its strength comes from the strongest material in the window: **steel > stone > brick > glass > wood** (`CollapseHapticMerge`). The `material` param travels on the bus payload, and if it's missing the default is bucket 3. Camera shake is still deduped per frame as before.

## 5. Timing alignment with SFX / animation

Status: **all rows confirmed.** Rows 1/2/3/7 by Sound Manager; rows 4/5/6/8 by Animator (also recorded in `animation/07` 'Haptics timing').

The pulse fires on the **same frame** as the routed event, which is the SFX transient (all `0 / 0` offsets in `audio_sync_table.md` unless noted).

| # | Event | Offset (frame / ms) | Rule | Status |
|---|---|---|---|---|
| 1 | `ANIM_WRECKER_FIRE` | at launch = +4 f / 66 ms after FIRE tap | pulse at launch with the cannon boom, **not** on the tap (raised on WreckerState.Recoil) | ✅ Sound Manager |
| 2 | `EXPLODE_CRATE` | 6 / 100 after swell start | the event is raised at the peak, so the pulse lands on the 100 ms peak, not at swell start | ✅ Sound Manager |
| 3 | `COLLAPSE_LAND` | 0 / 0 per segment contact | landings within 40 ms → one pulse, strongest material (§4) | ✅ Sound Manager |
| 4 | `PU_GRAVITY_BURST` | 90 / 1500; peak 1600 | light tick at 1500 ms (implode start), `break_med` at 1600 ms burst ring (scheduled, lead −20 → 1580 ms) | ✅ Animator |
| 5 | `PU_CHAIN_PULSE` / `PU_CHAIN_LINK` | 0; links +5 f (80 ms) each | medium on PULSE; light tick per LINK, max 5 per activation | ✅ Animator |
| 6 | `BOSS_TIDE_DEFEAT` / `BOSS_TIDE_DEFEAT_COLLAPSE` | 0 / 0; +210 f / 3500 ms (clip 4.0 s) | strong `boom` at start, medium at the new DEFEAT_COLLAPSE event. Until the rig clip lands, both events are raised from code-driven `Defeat()` (DEFEAT once via `TideDefeatEvent`, COLLAPSE via the 3500 ms unscaled timer) | ✅ Animator |
| 7 | Haptic lead offset | `HapticTiming.LeadMs`, default **−20 ms** | see below | ✅ Sound Manager |
| 8 | `BREAK_STEEL`; `IMPACT_TERRAIN` | 0 / 0 | BREAK_STEEL routed: shake 0.03 m / 0.3° 150 ms + light tick. IMPACT_TERRAIN final 0.01 m 80 ms, no haptic | ✅ Animator |
| — | `BOSS_TIDE_ARMS_LAND` 36 / 600, `BOSS_TIDE_PHASE2` 75 / 1250, `IMPACT_*`/`BREAK_*` 0 / 0 | | on the event | as routed |

**Lead offset (row 7):** config field `HapticTiming.LeadMs` (default −20 ms, negative = before the audio peak). Scheduled time = `eventMs + max(0, peakDelayMs + LeadMs)`, so a pulse **never fires before its triggering event**. Every routed event is raised at its own transient today (peakDelay 0), so the −20 ms lead clamps to 0 and pulses stay synchronous. The lead only shifts a pulse if an event later carries a known delay to its peak, e.g. 100 ms → fires at +80 ms. Android vibrator latency (~10–30 ms) is otherwise not compensated.
