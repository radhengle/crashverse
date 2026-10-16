#!/usr/bin/env bash
# Emulator publish gate (PR #23). Pass 1 calibrates: drives the first-run flow using the dev-only tap probe
# (UITapProbe logs "CVTAP|Screen|Button|x|y|interactable" in `input tap` coordinates) to learn every button centre on this
# device. Pass 2 runs QA's Tools/ci/emulator_smoke_flow.sh unchanged in logic, with those coordinates, from a clean install.
# Usage: emulator_smoke.sh <apk> <outdir>
set -euo pipefail
APK=$1; OUT=${2:-smoke_out}; PKG=com.crashverse.game; mkdir -p "$OUT/calibration"
adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
adb shell wm size 1080x2340; adb shell wm density 420
# no "Viewing full screen" bubble, no ANR/crash dialogs from a slow software-rendered System UI over the app
adb shell settings put secure immersive_mode_confirmations confirmed || true
adb shell settings put global hide_error_dialogs 1 || true
sleep 20 # let System UI settle on the software GPU
if ! adb install -r -g "$APK" > "$OUT/install.txt" 2>&1; then
  code=$(grep -o "INSTALL_[A-Z_]*" "$OUT/install.txt" | head -1); cat "$OUT/install.txt"
  echo "::error::adb install failed on API $(adb shell getprop ro.build.version.sdk | tr -d '\r'): ${code:-unknown}"; exit 1; fi
cat "$OUT/install.txt"; adb shell pm clear $PKG >/dev/null; adb logcat -c
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null

# coord <Screen> <Button> -> "x y" from the newest probe dump of that screen (waits up to 40 s)
coord(){ local i l; for i in $(seq 1 120); do
    l=$(adb logcat -d -s Unity | grep -o "CVTAP|$1|$2|[0-9]*|[0-9]*|[01]" | tail -1 || true)
    if [ -n "$l" ]; then echo "$l" | awk -F'|' '{print $4" "$5}'; return 0; fi; sleep 1; done
  echo "calibration: $1/$2 not found" >&2; adb logcat -d > "$OUT/calibration/logcat.txt"; adb exec-out screencap -p > "$OUT/calibration/missing_$2.png"; exit 1; }
put(){ # $(coord …) runs in a subshell, so its exit can't stop us: fail here with a clear message instead
  if [ -z "${3:-}" ] || [ "${3// /}" = "" ]; then echo "::error::calibration failed: no coordinates for $1 (see calibration/ screenshots + logcat)"; exit 1; fi
  read -r x y <<<"$3"; export "$1_X=$x" "$1_Y=$y"; echo "$1=$x,$y" | tee -a "$OUT/coords.txt"; }
dcheck(){ if adb logcat -d | grep -E "FATAL EXCEPTION|Fatal signal|ANR in $PKG|Unity +: .*Exception" > "$OUT/calibration/err_$1.txt"; then
    adb exec-out screencap -p > "$OUT/calibration/FAIL_$1.png"; adb logcat -d > "$OUT/calibration/logcat.txt"; echo "FAIL (discovery) at $1"; exit 1; fi
  adb shell pidof $PKG >/dev/null || { adb logcat -d > "$OUT/calibration/logcat.txt"; echo "FAIL (discovery): app died at $1"; exit 1; }; }
ctap(){ dcheck "before_tap_$1_$2"; adb logcat -c; adb shell input tap "$1" "$2"; sleep "${3:-3}"; dcheck "after_tap_$1_$2"; } # fresh log for the next coord

M=March; YR=$(( $(date -u +%Y) - 20 )) # adult band, visible when the list opens at now−18 (V-02); no scrolling needed
put MONTH AgeGateScreen "$(coord AgeGateScreen Button_Month)"; ctap $MONTH_X $MONTH_Y
put MONTH_OPT AgeGateScreen "$(coord ListModal Button_$M)";   ctap $MONTH_OPT_X $MONTH_OPT_Y
put YEAR AgeGateScreen "$(coord AgeGateScreen Button_Year)";  ctap $YEAR_X $YEAR_Y
# the year list scrolls: swipe up until the wanted year is logged on screen
YEAR_SWIPES=0; for i in $(seq 1 20); do adb logcat -d -s Unity | grep -q "CVTAP|ListModal|Button_$YR|" && break; adb shell input swipe 540 1300 540 1000 400; sleep 2; YEAR_SWIPES=$i; done; export YEAR_SWIPES
put YEAR_OPT x "$(coord ListModal Button_$YR)"; ctap $YEAR_OPT_X $YEAR_OPT_Y
put CONTINUE x "$(coord AgeGateScreen Button_Continue)"; ctap $CONTINUE_X $CONTINUE_Y 5
# First run has no login screen yet (guest sign-in is automatic in AccountLifecycle): GUEST taps the status bar, a no-op.
export GUEST_X=30 GUEST_Y=1170; echo "GUEST=30,1170 (no login screen in first run: a no-op tap on the map pan surface; 540,20 hit the Store button)" >> "$OUT/coords.txt"
put NODE1 x "$(coord MapScreen Button_1)"; ctap $NODE1_X $NODE1_Y 3
put PLAY x "$(coord BriefingScreen Button_Play)"; ctap $PLAY_X $PLAY_Y 8
put FIRE x "$(coord HudScreen Button_FIRE)"
put HINT x "$(coord HudScreen Button_DevAimHint)"   # BUG-24: dev-only, aims at the level's practice hint (known winning line)
# Aim lines from L1's data hint (yaw 4°, pitch 22° from rest 25°): yaw 0.25°/dp, pitch 0.15°/dp; drag up raises the arc.
D=$(adb shell wm density | grep -o '[0-9]*$' | tail -1); px(){ echo $(( $1 * D / 160 )); }
export AIM_FROM_X=540 AIM_FROM_Y=1300
export AIM_TO_X=$(( 540 + $(px 16) )) AIM_TO_Y=$(( 1300 + $(px 20) ))   # +4° yaw, −3° pitch
export MISS_TO_X=$(( 540 + $(px 240) )) MISS_TO_Y=$(( 1300 + $(px 20) )) # +60° yaw: misses the tower
echo "AIM=$AIM_FROM_X,$AIM_FROM_Y->$AIM_TO_X,$AIM_TO_Y MISS->$MISS_TO_X,$MISS_TO_Y density=$D" | tee -a "$OUT/coords.txt"
SHOTS=$(( ${SHOTS_L1:-4} * 3 ))  # FIRE is ignored while reloading/settling: keep trying until a result
for s in $(seq 1 $SHOTS); do ctap $HINT_X $HINT_Y 1; ctap $FIRE_X $FIRE_Y 6
  adb logcat -d -s Unity | grep -q "CVTAP|RESULT|" && break; done
adb exec-out screencap -p > "$OUT/calibration/result.png"
put RETRY x "$(coord ResultsScreen Button_Retry)"
adb logcat -d > "$OUT/calibration/logcat.txt"

# Pass 2: QA's flow from a clean install.
adb shell am force-stop $PKG
bash "$(dirname "$0")/emulator_smoke_flow.sh" "$APK" "$OUT"

# BUG-23 gate: no magenta (missing shader/material) on map, L1 HUD or results.
python3 "$(dirname "$0")/no_magenta.py" 0.03 "$OUT"/0[4-8]_*.png
