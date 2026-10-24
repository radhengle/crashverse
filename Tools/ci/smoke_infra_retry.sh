#!/usr/bin/env bash
# Runs emulator_smoke.sh; retries ONCE only when the failure carries a known emulator-infrastructure signature, never for app faults:
#   - "Indicates GPU hang" in ApplicationExitInfo (SwiftShader/ANGLE fence stall; runs 40102001893, 40105864952 api34-3)
#   - aim probe saw no yaw at all for a swipe ("yaw none": the injected swipe was dropped by a stalled emulator; 40105864952 api36)
# The first attempt's evidence is kept under $OUT/attempt1 and a ::warning:: names the signature. Any other failure fails the job.
set -u
APK=$1; OUT=${2:-smoke_out}
bash "$(dirname "$0")/emulator_smoke.sh" "$APK" "$OUT" && exit 0
sig=""
grep -q "Indicates GPU hang" "$OUT/exit_info.txt" 2>/dev/null && sig="emulator GPU hang (stuck fence)"
[ -z "$sig" ] && grep -q "yaw none" "$OUT/aim_direction.txt" 2>/dev/null && sig="dropped swipe (no yaw logged)"
if [ -z "$sig" ]; then echo "smoke failed (no infra signature): not retrying"; exit 1; fi
echo "::warning::emulator smoke: $sig on attempt 1; retrying once (evidence in $OUT/attempt1)"
tmp=$(mktemp -d); mv "$OUT" "$tmp/attempt1"; mkdir -p "$OUT"; mv "$tmp/attempt1" "$OUT/attempt1"
adb shell am force-stop com.crashverse.game >/dev/null 2>&1 || true
bash "$(dirname "$0")/emulator_smoke.sh" "$APK" "$OUT"
