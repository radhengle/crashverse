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
adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
adb shell pm clear $PKG >/dev/null 2>&1 || true
adb install -r -g "$APK"; adb logcat -c
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null
for i in $(seq 1 30); do adb shell pidof $PKG >/dev/null && break; sleep 2; done
sleep 12; check launch; shot 01_launch
shot 02_agegate
tap $MONTH_X $MONTH_Y; tap $MONTH_OPT_X $MONTH_OPT_Y; check month    # BUG-20 regression
tap $YEAR_X $YEAR_Y;   tap $YEAR_OPT_X $YEAR_OPT_Y;   check year
shot 03_agegate_filled            # Continue must be enabled (visual check / pixel compare)
tap $CONTINUE_X $CONTINUE_Y 4; check continue; shot 04_login
tap $GUEST_X $GUEST_Y 5; check login; shot 05_map
tap $NODE1_X $NODE1_Y; tap $PLAY_X $PLAY_Y 6; check l1_load; shot 06_l1_hud
for s in $(seq 1 $SHOTS_L1); do adb shell input swipe $AIM_FROM_X $AIM_FROM_Y $AIM_TO_X $AIM_TO_Y 400; sleep 6; check "win_shot$s"; done
sleep 4; shot 07_result_win       # expect a win (>=85% after settle) on the scripted line
tap $RETRY_X $RETRY_Y 6; check retry
for s in $(seq 1 $SHOTS_L1); do adb shell input swipe $AIM_FROM_X $AIM_FROM_Y $MISS_TO_X $MISS_TO_Y 400; sleep 6; check "fail_shot$s"; done
sleep 4; shot 08_result_fail
adb logcat -d > "$OUT/logcat.txt"; echo "SMOKE PASS (screens in $OUT for visual review)"
