# CRASHVERSE — Haptics v1 (Android)
API: `VibrationEffect.createWaveform(long[] timings, int[] amplitudes, -1)` (API 26+) via AndroidJavaObject in a `HapticService`; amplitude 1–255. If `Vibrator.hasAmplitudeControl()` is false, fall back to `createOneShot(total ms, DEFAULT_AMPLITUDE)` only for ★ events (fire, break_heavy, boss_hit, defeat, win, reward). API < 26: `Handheld.Vibrate()` only for ★ events. Prefer `HapticFeedbackConstants` (KEYBOARD_TAP / CONFIRM / REJECT on API 30+) for UI ticks.

| haptic id | Used by | timings ms (off,on,…) | amplitudes | Feel |
|---|---|---|---|---|
| light_tick | crack, combo_step, coin_single | 0,12 | 0,60 | tiny tick |
| tick | UI click, toggle, power-up arm | 0,18 | 0,90 | crisp tap |
| tick_double | confirm, reload, node unlock | 0,15,60,15 | 0,100,0,140 | da-dum |
| buzz_short | UI error | 0,40,30,40 | 0,70,0,70 | dull double buzz |
| impact | projectile impact, armour hit | 0,30 | 0,150 | thud |
| fire ★ | cannon fire | 0,25,20,60 | 0,220,0,90 | kick + tail |
| break_med | wood/brick break | 0,25 | 0,120 | |
| break_sharp | steel joint snap | 0,10,10,30 | 0,255,0,100 | snap |
| break_heavy ★ | Tidewarden shoulder | 0,60,40,80 | 0,230,0,140 | |
| collapse_heavy | collapses | 0,40,30,120,30,80 | 0,180,0,120,0,70 | rolling rumble decay |
| boom | explosive crate | 0,80,20,120 | 0,255,0,110 | |
| powerup | power-up fx | 0,30,40,30,40,60 | 0,120,0,160,0,200 | ascending |
| combo_t1..t4 | combo tiers | t1 0,30 /80 · t2 0,30,30,30 /110,140 · t3 three pulses 140,170,200 · t4 four pulses to 255 | | escalates |
| star ★ | each star (synced to anim) | 0,35 | 0,120/170/230 for star 1/2/3 | |
| win ★ | win | 0,40,60,40,60,120 | 0,150,0,180,0,255 | da-da-DAA |
| fail | fail | 0,120,80,200 | 0,90,0,50 | soft deflate |
| reward ★ | chest/claim | 0,20,40,20,40,20,60,100 | 0,90,0,130,0,170,0,230 | |
| rumble/rumble_light | Titan intro / chest shake | 0,300 / 0,150 | 0,90 / 0,50 | |
| phase | boss phase 2 | 0,100,50,200 | 0,200,0,120 | |
| tell_pulse | shutter tell (2 pulses, BOSS_TIDE_TELL 0 & 30 f) | 0,80,420,80 | 0,180,0,180 | 2 Hz warning |
| boss_hit ★ | core hit / Titan hit | 0,50,30,100 | 0,255,0,150 | |
| defeat ★ | boss defeat | 0,80,40,120,40,300 | 0,255,0,200,0,120 | |

Rules: max 1 haptic per 40 ms (except tell/patterns already playing); debris never vibrates; a ★ event cancels a lower one in progress; respect `Time.timeScale` for gameplay patterns but not UI.

## Settings toggle
- Settings ▸ "Vibration" On/Off (default **On**), plus "Intensity" Low/High (Low = amplitudes ×0.6). Save in PlayerPrefs `haptics_enabled`, `haptics_strength`.
- Auto-off if device has no vibrator (`hasVibrator()` false) — hide toggle.
- Respect system: if Android ringer is silent with "vibrate" disabled for touch, still honour our toggle only (document in store listing QA).
- Requires `<uses-permission android:name="android.permission.VIBRATE"/>`.
