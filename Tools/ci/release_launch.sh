#!/usr/bin/env bash
# QA V2-08: launch the RELEASE variant (no tap probe, OfflineBackendService, dev UI stripped) on the CI emulator and get it past
# the first screen. It has no probe, so it reuses the dev calibration's coordinates (same layout) from $OUT/coords.txt:
# Month -> March (the V2-01 tap) -> Year -> the year the list opens on (now-50, adult) -> Continue. Passes if the app stays alive,
# no logcat fail pattern appears, and the screen after Continue is clearly different from the age gate.
# Usage: release_launch.sh <release.apk> <out_dir>
set -euo pipefail
APK=$1; OUT=$2/release; PKG=com.crashverse.game; mkdir -p "$OUT"
FAIL_PATTERNS=${FAIL_PATTERNS:-$(dirname "$0")/../../qa/ci/logcat_fail_patterns.txt}
PAT_RE=$(grep -vE '^\s*(#|$)' "$FAIL_PATTERNS" | paste -sd'|' -)
while IFS='=' read -r k v; do case "$k" in MONTH|MONTH_OPT|YEAR|YEAR_MID|CONTINUE) export "${k}_X=${v%,*}" "${k}_Y=${v#*,}";; esac; done < "$2/coords.txt"
: "${MONTH_X:?}" "${MONTH_OPT_X:?}" "${YEAR_X:?}" "${YEAR_MID_X:?}" "${CONTINUE_X:?}"
shot(){ adb exec-out screencap -p > "$OUT/$1.png"; }
die(){ shot "FAIL_$1"; adb logcat -d -b all > "$OUT/logcat.txt" 2>&1 || true; adb shell dumpsys activity exit-info $PKG > "$OUT/exit_info.txt" 2>&1 || true; echo "::error::release launch: $2"; exit 1; }
alive(){ adb shell pidof $PKG >/dev/null || die "$1" "app died at $1"; }
adb uninstall $PKG >/dev/null 2>&1 || true   # the dev build is signed by another runner's debug key
adb install -r -g "$APK" > "$OUT/install.txt" 2>&1 || { cat "$OUT/install.txt"; die install "adb install failed"; }
adb logcat -c; adb logcat -b all -v threadtime > "$OUT/logcat_all.txt" 2>&1 & LCPID=$!; trap 'kill $LCPID 2>/dev/null || true' EXIT
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null
sleep 25; alive launch; shot r1_agegate
t(){ adb shell input tap "$1" "$2"; sleep "${3:-2}"; alive "$4"; }
t $MONTH_X $MONTH_Y 2 month; t $MONTH_OPT_X $MONTH_OPT_Y 2 march; t $YEAR_X $YEAR_Y 2 year; t $YEAR_MID_X $YEAR_MID_Y 2 year_pick
shot r2_gate_filled
t $CONTINUE_X $CONTINUE_Y 6 continue; shot r3_after_gate
python3 - "$OUT/r2_gate_filled.png" "$OUT/r3_after_gate.png" <<'PY' || die same_screen "still on the age gate after Continue"
import sys, zlib, struct
def px(p):
    d = open(p, 'rb').read(); i = 8; w = h = 0; idat = b''
    while i < len(d):
        n, = struct.unpack('>I', d[i:i+4]); t = d[i+4:i+8]; c = d[i+8:i+8+n]; i += 12 + n
        if t == b'IHDR': w, h, bd, ct = struct.unpack('>IIBB', c[:10]); bpp = {2: 3, 6: 4}[ct]
        elif t == b'IDAT': idat += c
    raw = zlib.decompress(idat); stride = w * bpp; out = bytearray(); prev = bytearray(stride); o = 0
    for _ in range(h):
        f = raw[o]; line = bytearray(raw[o+1:o+1+stride]); o += 1 + stride
        for x in range(stride):
            a = line[x-bpp] if x >= bpp else 0; b = prev[x]; c = prev[x-bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        out += line; prev = line
    return w, h, bpp, out
w, h, bpp, A = px(sys.argv[1]); _, _, _, B = px(sys.argv[2])
step = 7; diff = tot = 0
for y in range(0, h, step):
    for x in range(0, w, step):
        k = (y * w + x) * bpp; tot += 1
        if sum(abs(A[k+j] - B[k+j]) for j in range(3)) > 60: diff += 1
frac = diff / tot; print(f"release launch: {frac:.1%} of the screen changed after Continue")
sys.exit(0 if frac > 0.15 else 1)
PY
if adb logcat -d -b all | grep -E "$PAT_RE" > "$OUT/err.txt"; then die logcat "fail pattern in logcat (see release/err.txt)"; fi
echo "release launch OK: past the age gate, app alive"
