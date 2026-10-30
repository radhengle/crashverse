#!/usr/bin/env bash
# QA emulator smoke flow for CRASHVERSE. Must pass before publish.
# Runs inside the reactivecircus/android-emulator-runner script, with matrix api-level: [34, 36], x86_64, profile with 1080x2340.
# Usage: emulator_smoke_flow.sh <apk> <outdir>
# Tap coordinates are for 1080x2340 and are PLACEHOLDERS: Developer/Art must fill them in from the real layout (Unity UI isn't in uiautomator).
set -euo pipefail
APK=$1; OUT=${2:-smoke_out}; PKG=com.crashverse.game; mkdir -p "$OUT"
: "${MONTH_X:?}" "${MONTH_Y:?}" "${MONTH_OPT_X:?}" "${MONTH_OPT_Y:?}" "${YEAR_X:?}" "${YEAR_Y:?}" "${YEAR_OPT_X:?}" "${YEAR_OPT_Y:?}" \
  "${CONTINUE_X:?}" "${CONTINUE_Y:?}" "${GUEST_X:?}" "${GUEST_Y:?}" "${NODE1_X:?}" "${NODE1_Y:?}" "${PLAY_X:?}" "${PLAY_Y:?}" \
  "${AIM_FROM_X:?}" "${AIM_FROM_Y:?}" "${AIM_TO_X:?}" "${AIM_TO_Y:?}" "${MISS_TO_X:?}" "${MISS_TO_Y:?}" "${RETRY_X:?}" "${RETRY_Y:?}" \
  "${AVATAR_X:?}" "${AVATAR_Y:?}" "${BADGE_X:?}" "${BADGE_Y:?}" "${PCONT_X:?}" "${PCONT_Y:?}" "${MAP_X:?}" "${MAP_Y:?}" "${NODE2_X:?}" "${NODE2_Y:?}"
SHOTS_L1=${SHOTS_L1:-4}
shot(){ adb exec-out screencap -p > "$OUT/$1.png"; }
# QA C-2: the #36 logcat gate (crash/ANR/exception + code stripping / missing types), one ERE per line
FAIL_PATTERNS=${FAIL_PATTERNS:-$(dirname "$0")/../../qa/ci/logcat_fail_patterns.txt}; [ -s "$FAIL_PATTERNS" ] || { echo "FAIL: logcat pattern file $FAIL_PATTERNS missing"; exit 1; }
PAT_RE=$(grep -vE '^\s*(#|$)' "$FAIL_PATTERNS" | paste -sd'|' -)
check(){ if adb logcat -d | grep -E "$PAT_RE" >"$OUT/err_$1.txt"; then shot "FAIL_$1"; echo "FAIL at $1"; adb logcat -d > "$OUT/logcat.txt"; exit 1; fi
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
# QA V2-01: flip a framework (target "android") overlay => CONFIG_ASSETS_PATHS (0x80000000). The activity must NOT be relaunched
# and Unity must not kill itself (SIG 9): same pid, no wm_relaunch for our package, no Unity onDestroy.
assets_flip(){ local tag=$1 pid0 pid1 ov
  ov=$(adb shell cmd overlay list android 2>/dev/null | tr -d '\r' | grep -E '^\[ \] ' | awk '{print $3}' \
       | grep -E 'theme\.icon|theme\.font|cutout\.emulation|navbar' | head -1)
  [ -z "$ov" ] && { echo "V2-01 $tag: no disabled framework overlay to flip (skipped)" | tee -a "$OUT/v2_01.txt"; return 0; }
  pid0=$(adb shell pidof $PKG | tr -d '\r'); adb logcat -b events -c; adb logcat -c
  adb shell cmd overlay enable --user 0 "$ov"; sleep 4; adb shell cmd overlay disable --user 0 "$ov"; sleep 5
  pid1=$(adb shell pidof $PKG | tr -d '\r')
  local cfg; cfg=$(adb logcat -d -b events | grep -E 'configuration_changed' | tr -d '\r' | tail -4 | tr '\n' ' ')
  local rel; rel=$(adb logcat -d -b events | grep -E 'wm_relaunch(_resume)?_activity|am_relaunch' | grep "$PKG" || true)
  local des; des=$(adb logcat -d -s Unity | grep -E 'onDestroy|SIG(KILL| 9)' || true)
  echo "V2-01 $tag: overlay=$ov pid $pid0 -> ${pid1:-dead} relaunch='${rel}' unityDestroy='${des}' events='${cfg}'" | tee -a "$OUT/v2_01.txt"
  if [ -z "$pid1" ] || [ "$pid0" != "$pid1" ] || [ -n "$rel" ] || [ -n "$des" ]; then
    shot "FAIL_v2_01_$tag"; adb logcat -d > "$OUT/logcat.txt"; adb logcat -d -b events > "$OUT/events.txt"; echo "FAIL: V2-01 assets-paths change relaunched/killed the game ($tag)"; exit 1; fi
  shot "v2_01_after_$tag"; }
sleep 12; check launch; shot 01_launch
shot 02_agegate
settle month; tap $MONTH_X $MONTH_Y; settle month_opt; tap $MONTH_OPT_X $MONTH_OPT_Y; check month    # BUG-20 regression
settle year; tap $YEAR_X $YEAR_Y; settle year_list
# year by name (PR #36 approach): no replayed swipe count or calibrated coordinate, the list position may differ per run
. "$(dirname "$0")/pick_year.sh"; settle year_opt
pick_year "${BIRTH_YEAR:?}" || { shot FAIL_year; adb logcat -d > "$OUT/logcat.txt"; echo "FAIL: year $BIRTH_YEAR not selectable by name"; exit 1; }
tap $PICKED_X $PICKED_Y; check year
# QA (BUG-20 proof): Continue must be enabled once Month and Year are set
for i in $(seq 1 20); do adb logcat -d -s Unity | grep -q "CVTAP|AgeGateScreen|Button_Continue|[0-9]*|[0-9]*|1" && break; sleep 0.5
  [ $i = 20 ] && { shot FAIL_continue_disabled; echo "FAIL: Continue not enabled after Month+Year"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }; done
shot 03_agegate_filled            # Continue must be enabled (visual check / pixel compare)
begin(){ local i; for i in $(seq 1 30); do adb logcat -d -s Unity | grep -q "CVTAP|BEGIN|$1" && return 0; sleep 1; done
  shot "FAIL_no_$1"; echo "FAIL: $1 not reached within 30 s ($2)"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
settle continue; adb logcat -c; tap $CONTINUE_X $CONTINUE_Y 4; check continue
# v2 target UI: Login (social sign-in shown disabled, "Coming soon") -> guest -> Create Profile -> World Map
begin LoginScreen "after the age gate"; settle login; shot 04_login
for p in Google Facebook Instagram X Email; do adb logcat -d -s Unity | grep -q "CVTAP|LoginScreen|Button_$p|[0-9]*|[0-9]*|0" \
  || { shot FAIL_social_$p; echo "FAIL: $p sign-in must be shown disabled (Coming soon)"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }; done
adb logcat -c; tap $GUEST_X $GUEST_Y 2; check login
begin ProfileScreen "after Play as Guest"; settle profile
tap $AVATAR_X $AVATAR_Y 1; tap $BADGE_X $BADGE_Y 1; check profile; shot 05_profile
adb logcat -c; tap $PCONT_X $PCONT_Y 3; check profile_continue
begin MapScreen "after creating the guest profile"; settle map; shot 05_map_guest
assets_flip map; settle map_v201
adb logcat -c; tap $NODE1_X $NODE1_Y 3; begin LevelSelectScreen "after tapping level 1"; check level_select; shot 06_level_select
tap $PLAY_X $PLAY_Y 6; check l1_load; shot 06_l1_hud
assets_flip l1_hud
# QA LOW + P0 (aim mirrored on dev-build-6): real touch swipes through AimController; +yaw is screen-left from the run camera,
# so a RIGHT swipe must drive the logged target yaw negative and a LEFT swipe positive (no hook, no Drag() shortcut).
D=$(adb shell wm density | grep -o '[0-9]*$' | tail -1); SW=$(( 100 * D / 160 ))
aim_yaw(){ sleep 3; adb logcat -d -s Unity | grep -oE "CVTAP\|AIM\|yaw=-?[0-9.]+" | tail -1 | cut -d= -f2 || true; }  # no line -> empty, reported below
adb logcat -c; adb shell input swipe 540 1300 $(( 540 + SW )) 1300 2000; YR=$(aim_yaw)   # 2 s: several frames even at CPU-GL frame rates
adb logcat -c; adb shell input swipe 540 1300 $(( 540 - 2 * SW )) 1300 2000; YL=$(aim_yaw)
echo "swipe right -> yaw ${YR:-none}; swipe left -> yaw ${YL:-none}" | tee "$OUT/aim_direction.txt"; adb logcat -d -s Unity | grep "CVTAP|BEGIN" | tail -2 >> "$OUT/aim_direction.txt" || true
awk -v r="${YR:-x}" -v l="${YL:-x}" 'BEGIN{ exit !(r+0 < -1 && l+0 > 1 && r != "x" && l != "x") }' || { shot FAIL_aim_direction; echo "FAIL: swipe direction (right must give yaw<0, left yaw>0)"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
check aim_direction
# a FIRE tap during reload/flight is ignored, so keep aiming+firing until the result screen shows
# emulator (swangle = CPU GL) runs the sim ~13x slower than real time: a miss off the island takes ~40 s wall-clock to reach the
# kill plane (run 40067356429), so the budget is wall-clock (SHOT_WALL_S, default 420 s); the RESULT check still exits early.
SHOT_WALL_S=${SHOT_WALL_S:-420}
SECONDS=0; for s in $(seq 1 200); do [ $SECONDS -ge $SHOT_WALL_S ] && break; tap "${HINT_X:?}" "${HINT_Y:?}" 1; fire "win$s"; sleep 6; check "win_shot$s"
  adb logcat -d -s Unity | grep -q "CVTAP|RESULT|" && break; done
result(){ local i r; for i in $(seq 1 120); do r=$(adb logcat -d -s Unity | grep -o "CVTAP|RESULT|[a-z]*" | tail -1 | cut -d'|' -f3); [ -n "$r" ] && { echo "$r"; return 0; }; sleep 1; done
  shot "FAIL_no_result_$1"; echo "FAIL: no win/fail result detected ($1)" >&2; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
# QA C-1/C-3: screenshot only once the results sequence is final (stars popped, score counted up): CVTAP|RESULTDONE
done_line(){ local i l; for i in $(seq 1 60); do l=$(adb logcat -d -s Unity | grep -o "CVTAP|RESULTDONE|$1|[^ ]*" | tail -1); [ -n "$l" ] && { echo "$l"; return 0; }; sleep 1; done
  shot "FAIL_no_resultdone_$1"; echo "FAIL: results sequence never finished ($1)" >&2; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
kv(){ echo "$1" | tr '|' '\n' | grep "^$2=" | cut -d= -f2; }
R1=$(result win); echo "scripted win line result: $R1" | tee "$OUT/result_win.txt"
[ "$R1" = win ] || { shot FAIL_win_line; adb logcat -d > "$OUT/logcat.txt"; echo "FAIL: BUG-24 known winning L1 line (practice hint) gave '$R1'" >&2; exit 1; }
# RESULTDONE = final values in the labels + 10 frames rendered since; 2 s more covers the slow CPU-GL compositor (QA C-1b/C-3b)
D1=$(done_line win); sleep 2; shot 07_result_win; echo "$D1" | tee -a "$OUT/result_win.txt"
# v2 Level Complete: Next Level offered (enabled) after the L1 win
adb logcat -d -s Unity | grep -q "CVTAP|ResultsScreen|Button_NextLevel|[0-9]*|[0-9]*|1" || { echo "FAIL: Level Complete has no enabled Next Level"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
WS=$(kv "$D1" score); WST=$(kv "$D1" stars); WL=$(kv "$D1" label)
[ "${WS:-0}" -gt 0 ] || { echo "FAIL: win screen score is ${WS:-?} (QA C-1)"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
case "$WL" in "Score_${WS}_"*) ;; *) echo "FAIL: win panel label '$WL' does not show score $WS (QA C-1b)"; adb logcat -d > "$OUT/logcat.txt"; exit 1;; esac
[ "${WST:-0}" -ge "${WIN_STARS_MIN:-3}" ] || { echo "FAIL: hint line won with ${WST:-?} stars, expected >= ${WIN_STARS_MIN:-3} (QA C-3)"; adb logcat -d > "$OUT/logcat.txt"; exit 1; }
adb logcat -c; tap $RETRY_X $RETRY_Y 6; check retry
# the probe re-dumps the results screen every 2 s: drop any win line logged before Retry took effect, once the HUD is back
for i in $(seq 1 30); do adb logcat -d -s Unity | grep -q "CVTAP|BEGIN|HudScreen" && break; sleep 1; done; adb logcat -c
# a FIRE tap during reload/flight is ignored, so keep aiming+firing until the result screen shows
SECONDS=0; for s in $(seq 1 200); do [ $SECONDS -ge $SHOT_WALL_S ] && break; adb shell input swipe $AIM_FROM_X $AIM_FROM_Y $MISS_TO_X $MISS_TO_Y 400; fire "fail$s"; sleep 6; check "fail_shot$s"
  adb logcat -d -s Unity | grep -q "CVTAP|RESULT|" && break; done
R2=$(result fail); echo "miss line result: $R2" | tee "$OUT/result_fail.txt"
[ "$R2" = fail ] || { shot 08_result_fail; echo "FAIL: miss line did not fail ($R2)"; exit 1; }
D2=$(done_line fail); sleep 2; shot 08_result_fail; echo "$D2" | tee -a "$OUT/result_fail.txt"   # QA C-3: settled panel
# back to the map: L2 is unlocked by the win; its Level Select opens
# the fail panel has no Next Level row, so Map sits higher than on the calibration win panel: use the live probe position
FM=$(adb logcat -d -s Unity | grep -o "CVTAP|ResultsScreen|Button_Map|[0-9]*|[0-9]*" | tail -1 | cut -d'|' -f4,5)
[ -n "$FM" ] && { MAP_X=${FM%|*}; MAP_Y=${FM#*|}; }; echo "fail-panel Map at $MAP_X,$MAP_Y" | tee -a "$OUT/coords.txt"
adb logcat -c; tap $MAP_X $MAP_Y 4; begin MapScreen "after Map on the results"; settle map2; shot 09_map_after_win
adb logcat -c; tap $NODE2_X $NODE2_Y 3; begin LevelSelectScreen "after tapping level 2"; check level2; shot 09_level2_select
# Daily Boss (unlocked by the L1 win): card + boss arena HUD screenshots for the mockup compare. Screens only, but crash-gated.
adb logcat -c; adb shell input keyevent 4; sleep 3
if adb logcat -d -s Unity | grep -q "CVTAP|BEGIN|MapScreen" || { sleep 3; adb logcat -d -s Unity | grep -q "CVTAP|MapScreen|"; }; then
  settle map3; DB=$(adb logcat -d -s Unity | grep -o "CVTAP|MapScreen|Button_DailyBoss|[0-9]*|[0-9]*" | tail -1 | cut -d'|' -f4,5)
  if [ -n "$DB" ]; then adb logcat -c; tap ${DB%|*} ${DB#*|} 3; check boss_card; shot 10_boss_card
    BF=$(adb logcat -d -s Unity | grep -o "CVTAP|DailyBossScreen|Button_Fight|[0-9]*|[0-9]*" | tail -1 | cut -d'|' -f4,5)
    [ -n "$BF" ] && { tap ${BF%|*} ${BF#*|} 8; check boss_load; shot 11_boss_hud; }
  else echo "boss: no Button_DailyBoss probe" | tee -a "$OUT/coords.txt"; fi
else echo "boss: back did not return to the map (screens skipped)" | tee -a "$OUT/coords.txt"; fi
# L3 / L8 realistic modules, and with ALL_LEVEL_SHOTS=1 every level L1-10 (HUD vs building tops at 1080x2340)
# (dev build only: --es cv_dev_level opens that Level Select; screens only, crash-gated)
LVS="c1_l03 c1_l08"; [ "${ALL_LEVEL_SHOTS:-0}" = 1 ] && LVS="c1_l01 c1_l02 c1_l03 c1_l04 c1_l05 c1_l06 c1_l07 c1_l08 c1_l09 c1_l10"
for LV in $LVS; do
  adb shell am force-stop $PKG; sleep 2; adb logcat -c
  adb shell am start -n $PKG/com.unity3d.player.UnityPlayerActivity --es cv_dev_level $LV >/dev/null 2>&1; sleep 14
  if adb logcat -d -s Unity | grep -q "cv_dev_level opened $LV"; then
    for i in $(seq 1 20); do BP=$(adb logcat -d -s Unity | grep -o "CVTAP|LevelSelectScreen|Button_Play|[0-9]*|[0-9]*" | tail -1 | cut -d'|' -f4,5); [ -n "$BP" ] && break; sleep 1; done
    [ -n "$BP" ] && { tap ${BP%|*} ${BP#*|} 10; check "dev_$LV"; shot "12_${LV}_hud"; adb logcat -d -s Unity | grep -o "\[RealKit\] [^ ]*: [0-9]*/[0-9]* blocks dressed[^\r]*" | tail -1 | tee -a "$OUT/realkit.txt"; }
  else echo "$LV: cv_dev_level not honoured (release build?)" | tee -a "$OUT/realkit.txt"; fi
done
# camera HUD gate: every framed level/boss logs [CAM] ... hudLine H buildingTop T arcTop A; T and A must be <= H (+0.01)
adb logcat -d -s Unity | grep -o "\[CAM\] [^\r]*" | tee "$OUT/cam.txt"
if awk '{for(i=1;i<=NF;i++){if($i=="hudLine")h=$(i+1);if($i=="buildingTop")t=$(i+1);if($i=="arcTop")a=$(i+1)} if(t+0>h+0.01||a+0>h+0.01){print "HUD covers: "$0; bad=1}} END{exit !bad}' "$OUT/cam.txt"; then
  echo "FAIL: building top or hint arc under the HUD (see cam.txt)"; adb logcat -d > "$OUT/logcat.txt"; exit 1; fi
check final   # whole-run logcat gate (QA C-2)
adb logcat -d > "$OUT/logcat.txt"; echo "SMOKE PASS (screens in $OUT for visual review)"
