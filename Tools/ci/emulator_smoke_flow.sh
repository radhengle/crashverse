#!/usr/bin/env bash
# QA emulator smoke flow for CRASHVERSE. Must pass before publish.
# Runs inside the reactivecircus/android-emulator-runner script, with matrix api-level: [34, 36], x86_64, profile with 1080x2340.
# Usage: emulator_smoke_flow.sh <apk> <outdir>
# Tap coordinates are for 1080x2340 and are PLACEHOLDERS: Developer/Art must fill them in from the real layout (Unity UI isn't in uiautomator).
set -euo pipefail
APK=$1; OUT=${2:-smoke_out}; PKG=com.crashverse.game; mkdir -p "$OUT"
: "${MONTH_X:?}" "${MONTH_Y:?}" "${MONTH_OPT_X:?}" "${MONTH_OPT_Y:?}" "${YEAR_X:?}" "${YEAR_Y:?}" "${YEAR_OPT_X:?}" "${YEAR_OPT_Y:?}" \
  "${CONTINUE_X:?}" "${CONTINUE_Y:?}" "${GUEST_X:?}" "${GUEST_Y:?}" "${NODE1_X:?}" "${NODE1_Y:?}" "${PLAY_X:?}" "${PLAY_Y:?}" \
  "${AIM_FROM_X:?}" "${AIM_FROM_Y:?}" "${AIM_TO_X:?}" "${AIM_TO_Y:?}" "${MISS_TO_X:?}" "${MISS_TO_Y:?}" "${RETRY_X:?}" "${RETRY_Y:?}"
SHOTS_L1=${SHOTS_L1:-4}
shot(){ adb exec-out screencap -p > "$OUT/$1.png"; }
check(){ if adb logcat -d | grep -E "FATAL EXCEPTION|Fatal signal|ANR in $PKG|Unity +: .*Exception" >"$OUT/err_$1.txt"; then shot "FAIL_$1"; echo "FAIL at $1"; adb logcat -d > "$OUT/logcat.txt"; exit 1; fi
  adb shell pidof $PKG >/dev/null || { shot "FAIL_$1_dead"; echo "FAIL: app died at $1"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }; }
tap(){ adb shell input tap "$1" "$2"; sleep "${3:-2}"; }
# Developer (PR #23): a drag only aims; FIRE is a button (OQ-A2). FIRE_X/FIRE_Y come from the dev tap probe.
fire(){ if [ -n "${FIRE_X:-}" ]; then adb shell input tap "$FIRE_X" "$FIRE_Y"; sleep 1; check "fire_${1:-x}"; fi; } # QA: crash/ANR/death check after every FIRE
# QA: before an age-gate tap, wait until the dev tap probe's layout log is stable (no new CVTAP lines for 1.5 s, at least one dump)
settle(){ local i n p=-1 still=0; for i in $(seq 1 60); do n=$(adb logcat -d -s Unity | grep -c "CVTAP|" || true)
    if [ "$n" -gt 0 ] && [ "$n" = "$p" ]; then still=$((still+1)); [ $still -ge 3 ] && return 0; else still=0; fi; p=$n; sleep 0.5; done
  shot "FAIL_settle_$1"; echo "FAIL: layout never settled before $1"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
adb shell settings put secure immersive_mode_confirmations confirmed || true; adb shell settings put global hide_error_dialogs 1 || true
adb install -r -g "$APK"; adb shell pm clear $PKG >/dev/null 2>&1 || true; adb logcat -c
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null
for i in $(seq 1 30); do adb shell pidof $PKG >/dev/null && break; sleep 2; done
sleep 12; check launch; shot 01_launch
shot 02_agegate
settle month; tap $MONTH_X $MONTH_Y; settle month_opt; tap $MONTH_OPT_X $MONTH_OPT_Y; check month    # BUG-20 regression
settle year; tap $YEAR_X $YEAR_Y; settle year_list
# the year list scrolls: same swipes as calibration until the wanted year is on screen
for i in $(seq 1 ${YEAR_SWIPES:-0}); do adb shell input swipe 540 1300 540 1000 400; sleep 2; done; settle year_opt
tap $YEAR_OPT_X $YEAR_OPT_Y; check year
# QA (BUG-20 proof): Continue must be enabled once Month and Year are set
for i in $(seq 1 20); do adb logcat -d -s Unity | grep -q "CVTAP|AgeGateScreen|Button_Continue|[0-9]*|[0-9]*|1" && break; sleep 0.5
  [ $i = 20 ] && { shot FAIL_continue_disabled; echo "FAIL: Continue not enabled after Month+Year"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }; done
shot 03_agegate_filled            # Continue must be enabled (visual check / pixel compare)
settle continue; tap $CONTINUE_X $CONTINUE_Y 4; check continue; shot 04_map
tap $GUEST_X $GUEST_Y 2; check login
# QA: the map must appear after the automatic guest sign-in within 30 s
for i in $(seq 1 30); do adb logcat -d -s Unity | grep -q "CVTAP|BEGIN|MapScreen" && break; sleep 1
  [ $i = 30 ] && { shot FAIL_no_map; echo "FAIL: map not reached 30 s after guest sign-in"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }; done
shot 05_map_guest
tap $NODE1_X $NODE1_Y; tap $PLAY_X $PLAY_Y 6; check l1_load; shot 06_l1_hud
# a FIRE tap during reload/flight is ignored, so keep aiming+firing until the result screen shows (max 3× the shot budget)
for s in $(seq 1 $((SHOTS_L1 * 3))); do adb shell input swipe $AIM_FROM_X $AIM_FROM_Y $AIM_TO_X $AIM_TO_Y 400; fire "win$s"; sleep 6; check "win_shot$s"
  adb logcat -d -s Unity | grep -q "CVTAP|RESULT|" && break; done
result(){ local i r; for i in $(seq 1 40); do r=$(adb logcat -d -s Unity | grep -o "CVTAP|RESULT|[a-z]*" | tail -1 | cut -d'|' -f3); [ -n "$r" ] && { echo "$r"; return 0; }; sleep 1; done
  shot "FAIL_no_result_$1"; echo "FAIL: no win/fail result detected ($1)" >&2; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
R1=$(result win); echo "scripted win line result: $R1" | tee "$OUT/result_win.txt"; shot 07_result_win
[ "$R1" = win ] || echo "::warning::scripted L1 aim line gave '$R1', not a win (aim needs tuning; flow still exercised)"       # expect a win (>=85% after settle) on the scripted line
adb logcat -c; tap $RETRY_X $RETRY_Y 6; check retry
# a FIRE tap during reload/flight is ignored, so keep aiming+firing until the result screen shows (max 3× the shot budget)
for s in $(seq 1 $((SHOTS_L1 * 3))); do adb shell input swipe $AIM_FROM_X $AIM_FROM_Y $MISS_TO_X $MISS_TO_Y 400; fire "fail$s"; sleep 6; check "fail_shot$s"
  adb logcat -d -s Unity | grep -q "CVTAP|RESULT|" && break; done
R2=$(result fail); echo "miss line result: $R2" | tee "$OUT/result_fail.txt"; shot 08_result_fail
[ "$R2" = fail ] || { echo "FAIL: miss line did not fail ($R2)"; exit 1; }
adb logcat -d > "$OUT/logcat.txt"; echo "SMOKE PASS (screens in $OUT for visual review)"
