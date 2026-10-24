# Dev aim hook (BUG-24 automation) — DEV builds only

File-command hook that lets adb set the Wrecker's yaw/pitch and fire. Compiled **only** with `CRASHVERSE_DEV`
(`Assets/_Project/Scripts/DevHooks/`, asmdef `Crashverse.DevHooks` with `defineConstraints: [CRASHVERSE_DEV]`) and inert
unless the app is launched with the opt-in extra. Never runs in Titan ranked; a session that used it is marked non-ranked.

- Package: `com.crashverse.game` · Activity: `com.unity3d.player.UnityPlayerActivity`
- Command file: `Application.persistentDataPath/dev_aim_cmd.json` = `/sdcard/Android/data/com.crashverse.game/files/dev_aim_cmd.json`
- Polled every 0.25 s (map, mid-level, during resolve). Read, then deleted immediately. Applied when the session is
  Aiming and the reload gate is ready (a command that arrives mid-resolve waits, then fires). Once per `id`.
- The dev APK is a Unity Development build (`BuildOptions.Development`) → `android:debuggable=true`, so `run-as` works.
  Release (`CRASHVERSE_BUILD_PROFILE=release`) uses `BuildOptions.None` → not debuggable; the guard also fails if the
  custom manifest forces `android:debuggable`.

## Format (strict)
Exactly these four keys, nothing else: `{"id":1,"yaw":1.0,"pitch":11.0,"fire":true}`
- `id` int ≥ 0 (once per id; reuse is ignored), `yaw` number in [-60, 60], `pitch` (elevation) number in [5, 70], `fire` true/false.
- Out-of-limit values are **rejected, not clamped**; unknown/duplicate/missing keys, strings, nesting, >512 bytes → rejected.

## adb
```sh
PKG=com.crashverse.game
# 1. Launch with opt-in (required; without it the hook never arms)
adb shell am force-stop $PKG
adb shell am start -n $PKG/com.unity3d.player.UnityPlayerActivity --ez cv_dev_aim true
# 2. Enter L1 (c1_l01), then send the hint shot
echo '{"id":1,"yaw":1.0,"pitch":11.0,"fire":true}' > /tmp/dev_aim_cmd.json
adb push /tmp/dev_aim_cmd.json /sdcard/Android/data/$PKG/files/dev_aim_cmd.json
#   fallback if push to Android/data is refused (debuggable dev build):
adb shell "run-as $PKG sh -c 'cat > /sdcard/Android/data/$PKG/files/dev_aim_cmd.json'" < /tmp/dev_aim_cmd.json
# 3. Watch
adb logcat -s Unity | grep -E 'CRASHVERSE_DEV_AIM|CVTAP\|RESULT'
```

## Log lines (logcat tag `Unity`)
- `CRASHVERSE_DEV_AIM armed path=<persistentDataPath>/dev_aim_cmd.json` — run started with opt-in
- `CRASHVERSE_DEV_AIM applied id=N yaw=<v> pitch=<v>` — e.g. `applied id=1 yaw=1.0 pitch=11.0` (format `0.0##`, invariant)
- `CVTAP|RESULT|win` / `CVTAP|RESULT|fail` — when the level ends (settle-gated win/fail in `GameSession.End`; also Give Up)
- `CRASHVERSE_DEV_AIM rejected reason=<pitch_out_of_limits|unknown_key|ranked|...>`, `... ignored id=N reason=duplicate_id`, `... fire_failed id=N`

## Reliable miss / fail case
`pitch -30` is below the 5° minimum elevation and is **rejected**. Use **yaw 60, pitch 5**: at 28 m/s and 5° the
ball lands ~15–17 m out, 60° off-axis, far short of and beside the structure (30 m away). L1 has 4 shots, so a fail
needs 4 misses (ids must differ); after the 4th settles you get `CVTAP|RESULT|fail`:
```sh
for i in 11 12 13 14; do
  echo "{\"id\":$i,\"yaw\":60,\"pitch\":5,\"fire\":true}" > /tmp/dev_aim_cmd.json
  adb push /tmp/dev_aim_cmd.json /sdcard/Android/data/$PKG/files/dev_aim_cmd.json
  adb logcat -d -s Unity | grep -q "applied id=$i" || sleep 1   # hook queues until the reload gate opens
  sleep 4
done
```
(Each command waits for the previous shot to settle before firing; pushing too early just overwrites a not-yet-read file,
so wait for `applied id=N` before pushing the next one.)

## Release guard
`python3 Tools/ci/check_release_guards.py` (already in CI) fails if any hook token (class/method names,
`dev_aim_cmd.json`, `CRASHVERSE_DEV_AIM`, `cv_dev_aim`) appears outside a bare `#if CRASHVERSE_DEV`, or if the
asmdef constraint / file wrapping is missing. Built output: `python3 Tools/ci/check_release_guards.py --built <release.aab|apk>`
scans IL2CPP `global-metadata.dat` for the same tokens (CI hookup after the release build: Developer).
Tests: `python3 Tools/ci/test_check_release_guards.py`, `dotnet run --project Tools/Tests` (`devaim_*`).
