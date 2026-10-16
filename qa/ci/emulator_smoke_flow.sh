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
cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
APK=$1; OUT=${2:-smoke_out}; mkdir -p "$OUT"
PKG=${PKG:-com.crashverse.game}
STEP_TIMEOUT=${STEP_TIMEOUT:-30}; SHOTS_L1=${SHOTS_L1:-4}
BIRTH_MONTH=${BIRTH_MONTH:-January}; YEAR_OFFSET=${YEAR_OFFSET:-20}; BIRTH_YEAR=${BIRTH_YEAR:-$(( $(date -u +%Y) - YEAR_OFFSET ))}
YEAR_MAX_SWIPES=${YEAR_MAX_SWIPES:-12}; LIST_SWIPE_MS=${LIST_SWIPE_MS:-400}
FAIL_PATTERNS=${FAIL_PATTERNS:-$(dirname "$0")/logcat_fail_patterns.txt}
WIN_SHOTS_JSON=${WIN_SHOTS_JSON:-levels/ch1/ci_win_shots.json}; LEVEL_ID=${LEVEL_ID:-c1_l01}; WIN_GSCALE=${WIN_GSCALE:-1.0}
SCR_W=1080; SCR_H=2340   # updated from CVTAP|META; swipe coords in env/json are authored at 1080x2340
WIN_YAW=${WIN_YAW:-0.0}; WIN_PITCH=${WIN_PITCH:-11.0}; MISS_YAW=${MISS_YAW:-1.0}; MISS_PITCH=${MISS_PITCH:--30}
AIM_MODE=${AIM_MODE:-hook}   # hook (fallback to swipe if hook not acknowledged) | swipe
AIM_FILE=dev_aim_cmd.json; AIM_DIR=${AIM_DIR:-/sdcard/Android/data/$PKG/files}   # Application.persistentDataPath on Android
step=0; aim_id=0

log(){ echo "[smoke] $*"; }
ulog(){ adb logcat -d -s Unity:V; }
cv(){ ulog | grep -oE 'CVTAP\|[^[:space:]]+' || true; }                       # CVTAP tokens only
shot(){ adb exec-out screencap -p > "$OUT/$1.png"; }
snap(){ step=$((step+1)); shot "$(printf %02d $step)_$1"; }
die(){ shot "FAIL_$(echo "$1" | tr -c 'A-Za-z0-9_' _ | cut -c1-40)" || true; adb logcat -d > "$OUT/logcat.txt" || true; echo "::error::SMOKE FAIL: $*"; exit 1; }
# logcat gate: crashes, ANR, exceptions, code stripping / missing types (qa/ci/logcat_fail_patterns.txt)
check(){
  if adb logcat -d | grep -E -f <(grep -vE '^\s*(#|$)' "$FAIL_PATTERNS") >"$OUT/err_$1.txt"; then die "$1: logcat gate hit (see err_$1.txt): $(head -1 "$OUT/err_$1.txt" | cut -c1-200)"; fi
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
sx(){ echo $(( $1 * SCR_W / 1080 )); }; sy(){ echo $(( $1 * SCR_H / 2340 )); }   # scale 1080x2340 -> device
# aim_swipe fx fy tx ty  (1080x2340 authoring coords)
aim_swipe(){ local a=$(sx "$1") b=$(sy "$2") c=$(sx "$3") d=$(sy "$4"); log "aim swipe $a,$b -> $c,$d"
  adb shell input swipe "$a" "$b" "$c" "$d" "${SWIPE_MS:-400}"; }
# ordered win candidates (ci_win_shots.json #13 first, then chapter1.json hint only if it disagrees; l1_aim.env if neither)
win_candidates(){ python3 "$(dirname "$0")/win_shots.py" --level "$LEVEL_ID" --out "$OUT" --shots "$WIN_SHOTS_JSON" --gscale "$WIN_GSCALE" \
    ${CHAPTER_JSON:+--chapter "$CHAPTER_JSON"} --swipe "${L1_WIN_FROM_X:-},${L1_WIN_FROM_Y:-},${L1_WIN_TO_X:-},${L1_WIN_TO_Y:-}" --yaw "$WIN_YAW" --pitch "$WIN_PITCH"; }
# wait for a HudScreen BEGIN logged after the latest RESULT (i.e. the retried level is live)
wait_hud_after_result(){ local t=0
  until cv | awk '/^CVTAP\|RESULT\|/{r=NR} /^CVTAP\|BEGIN\|HudScreen$/{h=NR} END{exit !(h>r)}'; do t=$((t+1)); [ $t -gt "$STEP_TIMEOUT" ] && die "retry: HudScreen not reached"; sleep 1; done; }
retry_level(){ tapb ResultsScreen Button_Retry 6; check retry; wait_hud_after_result; sleep 2; }

# play_l1 <label> <want_results> <planfile>: each plan line "yaw pitch fx fy tx ty"; last line repeats for remaining shots
play_l1(){ local lab=$1 want=$2 plan=$3 mode=$AIM_MODE s line yaw pitch fx fy tx ty t=0
  for s in $(seq 1 "$SHOTS_L1"); do
    [ "$(results)" -ge "$want" ] && break
    line=$(sed -n "${s}p" "$plan"); [ -n "$line" ] || line=$(tail -1 "$plan"); read -r yaw pitch fx fy tx ty <<<"$line"
    if [ "$mode" = hook ] && ! aim_hook "$yaw" "$pitch"; then mode=swipe; echo "::warning::$lab: aim hook not acknowledged, using swipe fallback"; fi
    if [ "$mode" = swipe ]; then [ -n "${ty:-}" ] || die "$lab: no swipe fallback coords (BUG-24)"; aim_swipe "$fx" "$fy" "$tx" "$ty"; fi
    sleep 6; check "${lab}_shot$s"; [ "$s" = 1 ] && snap "${lab}_shot1"
  done
  echo "$mode" > "$OUT/aim_mode_$lab.txt"
  until [ "$(results)" -ge "$want" ]; do t=$((t+1)); [ $t -gt 45 ] && die "$lab: no CVTAP|RESULT after $SHOTS_L1 shots"; sleep 1; done; }

# ---- year picker (by name; PR #38 opens the list mid-range; list is newest-first) ----
# latest ListModal snapshot (lines after the last BEGIN|ListModal): "label x y interactable"
list_snapshot(){ cv | awk -F'|' '$2=="BEGIN"{cur=($3=="ListModal"); if(cur) n=0; next} cur && NF==6 && $2=="ListModal" && $3 ~ /^Button_/ {a[++n]=substr($3,8)" "$4" "$5" "$6} END{for(i=1;i<=n;i++) print a[i]}'; }
# Hook point: a future CVTAP debug scroll command (e.g. dev cmd file "scroll_to":"Button_2006") can replace the swipe here.
list_scroll(){ local dir=$1 ys=$2; local x0 ytop ybot; read -r x0 ytop ybot <<<"$ys"
  local from to; if [ "$dir" = down ]; then from=$ybot; to=$ytop; else from=$ytop; to=$ybot; fi   # down = reveal older years (finger moves up)
  log "year list swipe ($dir): $x0,$from -> $x0,$to ${LIST_SWIPE_MS}ms"; adb shell input swipe "$x0" "$from" "$x0" "$to" "$LIST_SWIPE_MS"; sleep 2; }
pick_year(){ local target=$1 tries=0 snap_ yrs seen="" xy
  while :; do
    snap_=$(list_snapshot); yrs=$(echo "$snap_" | awk '$1 ~ /^[0-9]{4}$/ {print $1}' | tr '\n' ' ')
    log "year list visible: ${yrs:-<none>} (target $target)"; seen="$seen [$yrs]"
    xy=$(echo "$snap_" | awk -v y="$target" '$1==y && $4=="1"{print $2, $3}' | tail -1)
    if [ -n "$xy" ]; then log "tap Year $target @ $xy"; adb shell input tap $xy; sleep 2; return; fi
    tries=$((tries+1)); [ $tries -gt "$YEAR_MAX_SWIPES" ] && die "Year $target not reachable after $YEAR_MAX_SWIPES swipes; years seen:$seen"
    [ -n "$yrs" ] || die "year ListModal shows no year buttons"
    # list rect from year button positions: x = column, swipe between inner 20%/80% of visible span
    local geo; geo=$(echo "$snap_" | awk '$1 ~ /^[0-9]{4}$/ {x=$2; if(min==""||$3<min)min=$3; if($3>max)max=$3; if(lo==""||$1<lo)lo=$1; if($1>hi)hi=$1} END{s=max-min; if(s<200)s=200; print x, int(min+s*0.2), int(min+s*0.8), lo, hi}')
    local x0 ytop ybot lo hi; read -r x0 ytop ybot lo hi <<<"$geo"
    if [ "$target" -lt "$lo" ]; then list_scroll down "$x0 $ytop $ybot"; else list_scroll up "$x0 $ytop $ybot"; fi
    shot "year_scroll_$tries" || true
  done; }

# ---- boot + install ----
adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
for k in window_animation_scale transition_animation_scale animator_duration_scale; do adb shell settings put global $k 0; done
adb shell pm clear "$PKG" >/dev/null 2>&1 || true
adb install -r -g "$APK"; adb logcat -G 16M || true; adb logcat -c
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null
t=0; until cv | grep -qE '^CVTAP\|META\|screen\|'; do t=$((t+1)); [ $t -gt 90 ] && die "launch: no CVTAP|META (not a dev APK with UITapProbe?)"; sleep 1; done
check launch; snap launch; cv | grep -E '^CVTAP\|META\|' | tail -1 | tee "$OUT/screen_meta.txt"
read -r SCR_W SCR_H <<<"$(awk -F'|' '{print $4, $5}' "$OUT/screen_meta.txt")"; log "screen ${SCR_W}x${SCR_H} (swipes scaled from 1080x2340)"

# ---- 1. Age gate ----
wait_cv "BEGIN|AgeGateScreen"; sleep 1; check agegate; snap agegate
tapb AgeGateScreen Button_Month; wait_cv "BEGIN|ListModal" 1; snap month_list
tapb ListModal "Button_$BIRTH_MONTH"; wait_cv "BEGIN|AgeGateScreen" 2; check month      # BUG-20 regression
tapb AgeGateScreen Button_Year;  wait_cv "BEGIN|ListModal" 2; snap year_list
pick_year "$BIRTH_YEAR"; wait_cv "BEGIN|AgeGateScreen" 3; check year
snap agegate_filled
tapb AgeGateScreen Button_Continue 3; check continue      # btn() requires interactable=1 => Continue enabled
# ---- 2. Map (no login screen; guest auto-created) ----
wait_cv "BEGIN|MapScreen"; sleep 2; check map; snap map
cv | grep -q '^CVTAP|BEGIN|BlockedScreen$' && die "landed on BlockedScreen (age band)"
tapb MapScreen Button_1 3; wait_cv "BEGIN|BriefingScreen"; snap briefing
tapb BriefingScreen Button_Play 6; check l1_load
# ---- 3. L1 win (BUG-24: must actually win). Try each aim source in order; retry the level between attempts ----
wait_cv "BEGIN|HudScreen" 1; sleep 2; snap l1_hud
NCAND=$(win_candidates); [ "${NCAND:-0}" -ge 1 ] || die "no win aim candidates for $LEVEL_ID"
won=""; tried=""
for c in $(seq 1 "$NCAND"); do
  lab=$(cat "$OUT/win_plan_$c.label"); log "win attempt $c/$NCAND: $lab ($(head -1 "$OUT/win_plan_$c.txt"))"
  [ "$c" -gt 1 ] && retry_level
  want=$(( $(results) + 1 )); play_l1 "win$c" "$want" "$OUT/win_plan_$c.txt"
  sleep 2; snap "result_win_attempt$c"; r=$(last_result); tried="$tried [$lab => $r]"
  if [ "$r" = win ]; then won=$lab; break; fi
  echo "::warning::L1 win attempt '$lab' produced '$r'"
done
[ -n "$won" ] || die "L1: no aim source won (BUG-24):$tried"
echo "$won" > "$OUT/win_source.txt"; log "L1 WIN asserted; CVTAP|RESULT|win produced by: $won"; echo "::notice::L1 win produced by $won"
# ---- 4. L1 fail ----
retry_level
echo "$MISS_YAW $MISS_PITCH ${L1_WIN_FROM_X:-} ${L1_WIN_FROM_Y:-} ${L1_MISS_TO_X:-} ${L1_MISS_TO_Y:-}" > "$OUT/miss_plan.txt"; play_l1 fail $(( $(results) + 1 )) "$OUT/miss_plan.txt"
sleep 2; snap result_fail
[ "$(last_result)" = fail ] || die "L1 miss path produced '$(last_result)'"
check final; adb logcat -d > "$OUT/logcat.txt"; log "SMOKE PASS"
