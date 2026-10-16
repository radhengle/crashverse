#!/usr/bin/env bash
# QA emulator smoke flow for CRASHVERSE (dev APK: CRASHVERSE_DEV/DEVELOPMENT_BUILD, needs UITapProbe from #23).
# Flow: AgeGateScreen -> (ListModal month/year) -> MapScreen (no login: LocalBackendService auto-creates "Guest Wrecker")
#       -> BriefingScreen -> HudScreen -> L1 WIN (assert) -> Retry -> L1 FAIL (assert).
# All taps come from UITapProbe logcat lines (verified against dev/touch-fix UITapProbe.cs / Screens.cs / UIFactory.cs:75):
#   CVTAP|META|screen|<w>|<h>|dpi=<d>    CVTAP|BEGIN|<Screen>    CVTAP|<Screen>|Button_<label>|<x>|<y>|<0/1>  (origin top-left)
#   CVTAP|RESULT|win|fail
# BUG-24 primary aim path: dev aim hook (dev_aim_cmd.json in persistentDataPath, game logs "CRASHVERSE_DEV_AIM applied id=N").
# Fallback: swipe lines from qa/ci/l1_aim.env.  Any crash/ANR/exception/app death/timeout/wrong result => exit 1.
# Usage: emulator_smoke_flow.sh <apk> <outdir>
set -euo pipefail
APK=$1; OUT=${2:-smoke_out}; mkdir -p "$OUT"
PKG=${PKG:-com.crashverse.game}
STEP_TIMEOUT=${STEP_TIMEOUT:-30}; SHOTS_L1=${SHOTS_L1:-4}
BIRTH_MONTH=${BIRTH_MONTH:-January}; BIRTH_YEAR=${BIRTH_YEAR:-2006}   # 13+ band
WIN_YAW=${WIN_YAW:-1.0}; WIN_PITCH=${WIN_PITCH:-11.0}; MISS_YAW=${MISS_YAW:-1.0}; MISS_PITCH=${MISS_PITCH:--30}
AIM_MODE=${AIM_MODE:-hook}   # hook (fallback to swipe if hook not acknowledged) | swipe
AIM_FILE=dev_aim_cmd.json; AIM_DIR=${AIM_DIR:-/sdcard/Android/data/$PKG/files}   # Application.persistentDataPath on Android
step=0; aim_id=0

log(){ echo "[smoke] $*"; }
ulog(){ adb logcat -d -s Unity:V; }
cv(){ ulog | grep -oE 'CVTAP\|[^[:space:]]+' || true; }                       # CVTAP tokens only
shot(){ adb exec-out screencap -p > "$OUT/$1.png"; }
snap(){ step=$((step+1)); shot "$(printf %02d $step)_$1"; }
die(){ shot "FAIL_$(echo "$1" | tr -c 'A-Za-z0-9_' _ | cut -c1-40)" || true; adb logcat -d > "$OUT/logcat.txt" || true; echo "::error::SMOKE FAIL: $*"; exit 1; }
check(){
  if adb logcat -d | grep -E "FATAL EXCEPTION|Fatal signal|ANR in $PKG|Unity +: .*Exception" >"$OUT/err_$1.txt"; then die "$1: crash/exception (see err_$1.txt)"; fi
  rm -f "$OUT/err_$1.txt"; adb shell pidof "$PKG" >/dev/null || die "$1: app died"; }
count(){ cv | grep -cxF "CVTAP|$1" || true; }
# wait until exact token "CVTAP|<tok>" has appeared >= n times
wait_cv(){ local tok=$1 n=${2:-1} t=0
  until [ "$(count "$tok")" -ge "$n" ]; do t=$((t+1)); [ $t -gt "$STEP_TIMEOUT" ] && die "timeout waiting for CVTAP|$tok (#$n)"; sleep 1; done; }
# coords of the latest interactable Button_<label> on <Screen>
btn(){ local scr=$1 name=$2 t=0 l
  while :; do
    l=$(cv | awk -F'|' -v s="$scr" -v b="$name" 'NF==6 && $2==s && $3==b && $6=="1" {x=$4; y=$5} END{if(x!="") print x, y}')
    [ -n "$l" ] && { echo "$l"; return; }
    t=$((t+1)); [ $t -gt "$STEP_TIMEOUT" ] && die "no interactable $scr/$name in CVTAP"; sleep 1; done; }
tapb(){ local xy; xy=$(btn "$1" "$2"); log "tap $1/$2 @ $xy"; adb shell input tap $xy; sleep "${3:-2}"; }
results(){ cv | grep -cE '^CVTAP\|RESULT\|(win|fail)$' || true; }
last_result(){ cv | grep -oE '^CVTAP\|RESULT\|(win|fail)$' | tail -1 | cut -d'|' -f3; }

# BUG-24 dev aim hook: push {"yaw","pitch","fire","id"}; adb push, run-as fallback. Returns 0 if acknowledged.
aim_hook(){ local yaw=$1 pitch=$2 t=0; aim_id=$((aim_id+1))
  printf '{"yaw":%s,"pitch":%s,"fire":true,"id":%d}\n' "$yaw" "$pitch" "$aim_id" > "$OUT/$AIM_FILE"
  if ! adb push "$OUT/$AIM_FILE" "$AIM_DIR/$AIM_FILE" >/dev/null 2>&1; then
    adb push "$OUT/$AIM_FILE" "/data/local/tmp/$AIM_FILE" >/dev/null &&
    adb shell "run-as $PKG sh -c 'mkdir -p files && cat /data/local/tmp/$AIM_FILE > files/$AIM_FILE'" || { log "aim hook: push failed"; return 1; }
  fi
  until adb logcat -d -s Unity:V | grep -qE "CRASHVERSE_DEV_AIM applied id=$aim_id\b"; do
    t=$((t+1)); [ $t -gt 15 ] && { log "aim hook: no ack for id=$aim_id"; return 1; }; sleep 1; done
  log "aim hook applied id=$aim_id yaw=$yaw pitch=$pitch"; }
aim_swipe(){ : "${L1_WIN_FROM_X:?l1_aim.env swipe fallback not set (BUG-24)}" "${L1_WIN_FROM_Y:?}" "${2:?}" "${3:?}"
  adb shell input swipe "$L1_WIN_FROM_X" "$L1_WIN_FROM_Y" "$2" "$3" "${SWIPE_MS:-400}"; }
# play L1 until the n-th RESULT; $1 label, $2/$3 yaw/pitch, $4/$5 swipe target
play_l1(){ local lab=$1 yaw=$2 pitch=$3 sx=${4:-} sy=${5:-} want=$6 mode=$AIM_MODE s
  for s in $(seq 1 "$SHOTS_L1"); do
    [ "$(results)" -ge "$want" ] && break
    if [ "$mode" = hook ] && ! aim_hook "$yaw" "$pitch"; then log "falling back to swipe"; mode=swipe; echo "::warning::$lab: aim hook not acknowledged, using swipe fallback"; fi
    [ "$mode" = swipe ] && aim_swipe x "$sx" "$sy"
    sleep 6; check "${lab}_shot$s"; [ "$s" = 1 ] && snap "${lab}_shot1"
  done
  echo "$mode" > "$OUT/aim_mode_$lab.txt"
  local t=0
  until [ "$(results)" -ge "$want" ]; do t=$((t+1)); [ $t -gt 45 ] && die "$lab: no CVTAP|RESULT after $SHOTS_L1 shots"; sleep 1; done; }

# ---- boot + install ----
adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
for k in window_animation_scale transition_animation_scale animator_duration_scale; do adb shell settings put global $k 0; done
adb shell pm clear "$PKG" >/dev/null 2>&1 || true
adb install -r -g "$APK"; adb logcat -G 16M || true; adb logcat -c
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null
t=0; until cv | grep -qE '^CVTAP\|META\|screen\|'; do t=$((t+1)); [ $t -gt 90 ] && die "launch: no CVTAP|META (not a dev APK with UITapProbe?)"; sleep 1; done
check launch; cv | grep -E '^CVTAP\|META\|' | tail -1 | tee "$OUT/screen_meta.txt"

# ---- 1. Age gate ----
wait_cv "BEGIN|AgeGateScreen"; sleep 1; check agegate; snap agegate
tapb AgeGateScreen Button_Month; wait_cv "BEGIN|ListModal" 1; snap month_list
tapb ListModal "Button_$BIRTH_MONTH"; wait_cv "BEGIN|AgeGateScreen" 2; check month      # BUG-20 regression
tapb AgeGateScreen Button_Year;  wait_cv "BEGIN|ListModal" 2; snap year_list
tapb ListModal "Button_$BIRTH_YEAR"; wait_cv "BEGIN|AgeGateScreen" 3; check year
snap agegate_filled
tapb AgeGateScreen Button_Continue 3; check continue      # btn() requires interactable=1 => Continue enabled
# ---- 2. Map (no login screen; guest auto-created) ----
wait_cv "BEGIN|MapScreen"; sleep 2; check map; snap map
cv | grep -q '^CVTAP|BEGIN|BlockedScreen$' && die "landed on BlockedScreen (age band)"
tapb MapScreen Button_1 3; wait_cv "BEGIN|BriefingScreen"; snap briefing
tapb BriefingScreen Button_Play 6; check l1_load
# ---- 3. L1 win (BUG-24: must actually win) ----
wait_cv "BEGIN|HudScreen" 1; sleep 2; snap l1_hud
play_l1 win "$WIN_YAW" "$WIN_PITCH" "${L1_WIN_TO_X:-}" "${L1_WIN_TO_Y:-}" 1
sleep 2; snap result_win
[ "$(last_result)" = win ] || die "L1 win path produced '$(last_result)' (BUG-24)"
log "L1 WIN asserted"
# ---- 4. L1 fail ----
tapb ResultsScreen Button_Retry 6; check retry
wait_cv "BEGIN|HudScreen" 2; sleep 2
play_l1 fail "$MISS_YAW" "$MISS_PITCH" "${L1_MISS_TO_X:-}" "${L1_MISS_TO_Y:-}" 2
sleep 2; snap result_fail
[ "$(last_result)" = fail ] || die "L1 miss path produced '$(last_result)'"
check final; adb logcat -d > "$OUT/logcat.txt"; log "SMOKE PASS"
