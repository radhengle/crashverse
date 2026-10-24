#!/usr/bin/env bash
# CI wrapper (android-emulator-runner runs `script:` line by line, so state lives here).
# Usage: emulator_smoke_ci.sh <apk> <outdir>
set -uo pipefail
APK=$1; OUT=${2:-smoke_out}; mkdir -p "$OUT"
adb wait-for-device; adb shell wm size 1080x2340; adb shell wm density 420
set -a; . qa/ci/l1_aim.env; set +a
( adb shell screenrecord --bit-rate 4000000 --time-limit 180 /sdcard/smoke1.mp4
  adb shell screenrecord --bit-rate 4000000 --time-limit 180 /sdcard/smoke2.mp4 ) &
REC=$!
bash qa/ci/emulator_smoke_flow.sh "$APK" "$OUT"; RC=$?
adb shell pkill -INT screenrecord || true; sleep 4; kill $REC 2>/dev/null || true
adb pull /sdcard/smoke1.mp4 "$OUT/" || echo "::warning::no screenrecord video"
adb pull /sdcard/smoke2.mp4 "$OUT/" 2>/dev/null || true
exit $RC
