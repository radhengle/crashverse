#!/usr/bin/env bash
# QA emulator smoke flow for CRASHVERSE (DEV/CRASHVERSE_DEV APK: needs UITapProbe CVTAP logcat lines).
# Runs inside reactivecircus/android-emulator-runner (matrix API 34/36, x86_64, 1080x2340).
# Usage: emulator_smoke_flow.sh <apk> <outdir>
# HARD ASSERTS: every step waits for the expected CVTAP|BEGIN|<Screen> line; L1 win requires CVTAP|RESULT|win (BUG-24),
# L1 fail requires CVTAP|RESULT|fail. Any crash/ANR/exception/app death/timeout => exit 1.
# Button taps resolve from CVTAP coordinates when a button name regex is given (CV_BTN_*), else fall back to *_X/*_Y env.
# The L1 aim swipes (L1_WIN_* / L1_MISS_*) come from qa/ci/l1_aim.env and MUST be a verified winning line.
set -euo pipefail
APK=$1; OUT=${2:-smoke_out}; PKG=${PKG:-com.crashverse.game}; mkdir -p "$OUT"
SHOTS_L1=${SHOTS_L1:-4}; STEP_TIMEOUT=${STEP_TIMEOUT:-30}
: "${L1_WIN_FROM_X:?set in qa/ci/l1_aim.env (BUG-24)}" "${L1_WIN_FROM_Y:?}" "${L1_WIN_TO_X:?}" "${L1_WIN_TO_Y:?}" "${L1_MISS_TO_X:?}" "${L1_MISS_TO_Y:?}"
step=0
log(){ echo "[smoke] $*"; }
shot(){ adb exec-out screencap -p > "$OUT/$1.png"; }
die(){ shot "FAIL_${1// /_}" || true; adb logcat -d > "$OUT/logcat.txt" || true; echo "::error::SMOKE FAIL: $*"; exit 1; }
check(){
  if adb logcat -d | grep -E "FATAL EXCEPTION|Fatal signal|ANR in $PKG|Unity +: .*Exception" >"$OUT/err_$1.txt"; then die "$1: crash/exception (see err_$1.txt)"; fi
  rm -f "$OUT/err_$1.txt"; adb shell pidof "$PKG" >/dev/null || die "$1: app died"; }
# wait for "CVTAP|<marker>" appearing at least <count> times
wait_log(){ local pat=$1 n=${2:-1} t=0
  until [ "$(adb logcat -d -s Unity:V | grep -c -F "CVTAP|$pat")" -ge "$n" ]; do
    t=$((t+1)); [ $t -gt "$STEP_TIMEOUT" ] && die "timeout waiting for CVTAP|$pat (#$n)"; sleep 1; done; }
# coords of most recent button on <screen> matching <regex>; else fallback vars
btn(){ local scr=$1 re=$2 fx=${3:-} fy=${4:-} l
  if [ -n "$re" ]; then l=$(adb logcat -d -s Unity:V | grep -oE "CVTAP\|$scr\|[^|]*($re)[^|]*\|[0-9]+\|[0-9]+\|1" | tail -1 || true)
    [ -n "$l" ] && { echo "$l" | awk -F'|' '{print $4, $5}'; return; }; fi
  [ -n "$fx" ] && [ -n "$fy" ] || die "no coords for $scr/$re (no CVTAP match, no fallback)"; echo "$fx $fy"; }
tap(){ log "tap $1 $2"; adb shell input tap "$1" "$2"; sleep "${3:-2}"; }
snap(){ step=$((step+1)); shot "$(printf %02d $step)_$1"; }

adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
adb shell settings put global window_animation_scale 0; adb shell settings put global transition_animation_scale 0; adb shell settings put global animator_duration_scale 0
adb shell pm clear "$PKG" >/dev/null 2>&1 || true
adb install -r -g "$APK"; adb logcat -c; adb logcat -G 16M || true
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null
STEP_TIMEOUT=60 wait_log "META|screen"; check launch
adb logcat -d -s Unity:V | grep -F "CVTAP|META" | tail -1 | tee "$OUT/screen_meta.txt"

# 1. Age gate
wait_log "BEGIN|AgeGateScreen"; sleep 2; check agegate; snap agegate
tap $(btn AgeGateScreen "${CV_BTN_MONTH:-}" "${MONTH_X:-}" "${MONTH_Y:-}"); tap "${MONTH_OPT_X:?}" "${MONTH_OPT_Y:?}"; check month   # BUG-20
tap $(btn AgeGateScreen "${CV_BTN_YEAR:-}"  "${YEAR_X:-}"  "${YEAR_Y:-}");  tap "${YEAR_OPT_X:?}"  "${YEAR_OPT_Y:?}";  check year
snap agegate_filled
tap $(btn AgeGateScreen "${CV_BTN_CONTINUE:-Continue}" "${CONTINUE_X:-}" "${CONTINUE_Y:-}") 4; check continue
# 2. Login
wait_log "BEGIN|${LOGIN_SCREEN:-LoginScreen}"; sleep 1; snap login
tap $(btn "${LOGIN_SCREEN:-LoginScreen}" "${CV_BTN_GUEST:-Guest}" "${GUEST_X:-}" "${GUEST_Y:-}") 5; check login
# 3. Map
wait_log "BEGIN|MapScreen"; sleep 2; check map; snap map
tap $(btn MapScreen "${CV_BTN_NODE1:-}" "${NODE1_X:-}" "${NODE1_Y:-}")
tap $(btn "${BRIEF_SCREEN:-BriefingScreen}" "${CV_BTN_PLAY:-Play}" "${PLAY_X:-}" "${PLAY_Y:-}") 6; check l1_load
# 4. L1 win (BUG-24: must actually win)
wait_log "BEGIN|HudScreen"; sleep 2; snap l1_hud
for s in $(seq 1 "$SHOTS_L1"); do
  adb logcat -d -s Unity:V | grep -qF "CVTAP|RESULT|" && break
  adb shell input swipe "$L1_WIN_FROM_X" "$L1_WIN_FROM_Y" "$L1_WIN_TO_X" "$L1_WIN_TO_Y" "${SWIPE_MS:-400}"; sleep 6; check "win_shot$s"; [ "$s" = 1 ] && snap l1_win_shot1
done
STEP_TIMEOUT=40 wait_log "RESULT|"; sleep 2; snap result_win
r=$(adb logcat -d -s Unity:V | grep -oE "CVTAP\|RESULT\|(win|fail)" | tail -1); [ "$r" = "CVTAP|RESULT|win" ] || die "L1 scripted win line did not win (got '$r') — BUG-24"
log "L1 WIN asserted"
# 5. L1 fail
tap $(btn ResultsScreen "${CV_BTN_RETRY:-Retry}" "${RETRY_X:-}" "${RETRY_Y:-}") 6; check retry
wait_log "BEGIN|HudScreen" 2; sleep 2
for s in $(seq 1 "$SHOTS_L1"); do
  adb shell input swipe "$L1_WIN_FROM_X" "$L1_WIN_FROM_Y" "$L1_MISS_TO_X" "$L1_MISS_TO_Y" "${SWIPE_MS:-400}"; sleep 6; check "fail_shot$s"
  [ "$(adb logcat -d -s Unity:V | grep -cF 'CVTAP|RESULT|')" -ge 2 ] && break
done
STEP_TIMEOUT=40 wait_log "RESULT|" 2; sleep 2; snap result_fail
r=$(adb logcat -d -s Unity:V | grep -oE "CVTAP\|RESULT\|(win|fail)" | tail -1); [ "$r" = "CVTAP|RESULT|fail" ] || die "L1 miss line expected fail, got '$r'"
check final; adb logcat -d > "$OUT/logcat.txt"; log "SMOKE PASS"
