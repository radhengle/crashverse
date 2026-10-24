#!/usr/bin/env bash
# Sourced by emulator_smoke.sh + emulator_smoke_flow.sh. Picks a birth year in the open year ListModal BY NAME
# (Test Automation's PR #36 approach): read the visible Button_<year> items from the dev tap probe (CVTAP lines,
# viewport-filtered), tap the target if visible, otherwise swipe toward it (list is newest-first) and re-read.
# The list opens neutral mid-range (now-50, Security PR #52), so nothing here depends on an age-threshold anchor.
# Usage: pick_year <year> [max_swipes]; sets PICKED_X/PICKED_Y. Returns 1 if not reachable.
pick_year(){ local target=$1 max=${2:-15} t lines xy lo hi
  for t in $(seq 0 "$max"); do
    sleep 1.5
    lines=$(adb logcat -d -s Unity | grep -oE "CVTAP\|ListModal\|Button_[0-9]{4}\|[0-9]+\|[0-9]+\|[01]" || true)
    xy=$(echo "$lines" | grep "|Button_$target|" | tail -1 | awk -F'|' '{print $4" "$5}')
    if [ -n "$xy" ]; then read -r PICKED_X PICKED_Y <<<"$xy"; export PICKED_X PICKED_Y; echo "year $target @ $xy after $t swipes"; return 0; fi
    [ -n "$lines" ] || { echo "year list: no year buttons visible" >&2; return 1; }
    lo=$(echo "$lines" | awk -F'|' '{sub("Button_","",$3); print $3}' | sort -n | head -1)
    hi=$(echo "$lines" | awk -F'|' '{sub("Button_","",$3); print $3}' | sort -n | tail -1)
    echo "year list shows $lo..$hi, target $target" >&2
    adb logcat -c   # next read sees only the post-swipe probe dump
    if [ "$target" -lt "$lo" ]; then adb shell input swipe 540 1500 540 1100 400   # older years are below: finger up
    else adb shell input swipe 540 1100 540 1500 400; fi
  done
  echo "year $target not reachable after $max swipes" >&2; return 1; }
