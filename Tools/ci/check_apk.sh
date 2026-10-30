#!/usr/bin/env bash
# Asserts on a BUILT APK (QA APK-03/04/05 + 64-bit). Usage: check_apk.sh <apk> <dev|release>
# Uses aapt2 8.5.0 from Google Maven (pinned by SHA-256) so it works after free-disk-space removed the Android SDK.
set -euo pipefail
APK="$1"; PROFILE="${2:-dev}"
AAPT2_JAR=aapt2-8.5.0-11315950-linux.jar
AAPT2_SHA=0e307c068433efbc645a1864d4380bc79911cf5de311a976f92040cb218739dc
if [ ! -x .aapt2/aapt2 ]; then
  mkdir -p .aapt2
  curl -sSfL "https://dl.google.com/dl/android/maven2/com/android/tools/build/aapt2/8.5.0-11315950/$AAPT2_JAR" -o ".aapt2/$AAPT2_JAR"
  echo "$AAPT2_SHA  .aapt2/$AAPT2_JAR" | sha256sum -c -
  unzip -o -q ".aapt2/$AAPT2_JAR" aapt2 -d .aapt2
fi
fail=0; err() { echo "::error::$*"; fail=1; }
echo "== native libs (unzip -l)"; unzip -l "$APK" | grep -E ' lib/' | tee native_libs.txt
for lib in libil2cpp.so libunity.so; do grep -q "lib/arm64-v8a/$lib" native_libs.txt || err "missing lib/arm64-v8a/$lib"; done
echo "== manifest (aapt2 dump xmltree)"
.aapt2/aapt2 dump xmltree --file AndroidManifest.xml "$APK" > manifest_tree.txt
grep -E 'allowBackup|fullBackupContent|dataExtractionRules|screenOrientation|debuggable' manifest_tree.txt | tee manifest_checks.txt || true
grep -qE 'allowBackup\(0x[0-9a-f]+\)=false' manifest_tree.txt || err "android:allowBackup is not false (APK-04)"
grep -qE 'fullBackupContent\(0x[0-9a-f]+\)=false' manifest_tree.txt || err "android:fullBackupContent is not false (APK-04)"
grep -q 'dataExtractionRules' manifest_tree.txt || err "android:dataExtractionRules missing (APK-04)"
# activity orientation must be portrait (1) only
orients=$(grep -oE 'screenOrientation\(0x[0-9a-f]+\)=[0-9]+' manifest_tree.txt | sed 's/.*=//' | sort -u)
[ "$orients" = "1" ] || err "screenOrientation must be portrait (1) only, got: ${orients:-none} (APK-05)"
if [ "$PROFILE" = "release" ]; then
  grep -qE 'debuggable\(0x[0-9a-f]+\)=true' manifest_tree.txt && err "release APK is debuggable (APK-03)"
fi
# Release blocker (v2): target Android 15 (API 35), Google Play's requirement
grep -qE 'targetSdkVersion\(0x[0-9a-f]+\)=35$' manifest_tree.txt || err "targetSdkVersion is not 35: $(grep -oE 'targetSdkVersion\(0x[0-9a-f]+\)=[0-9]+' manifest_tree.txt)"
# QA V2-01 (release blocker): the Unity activity must handle CONFIG_ASSETS_PATHS (0x80000000) itself, or a Play services / package /
# overlay update relaunches it and Unity kills its own process. AssetsPathsGradleHook adds android:configChanges "assetsPaths".
cc=$(grep -oE 'configChanges\(0x0101001f\)=(\(type 0x11\))?0x[0-9a-f]+' manifest_tree.txt | grep -oE '0x[0-9a-f]+$' | head -1)
echo "configChanges=${cc:-none}" | tee -a manifest_checks.txt
if [ -z "$cc" ] || [ $(( cc & 0x80000000 )) -eq 0 ]; then err "activity configChanges ${cc:-missing} lacks assetsPaths (0x80000000): QA V2-01 relaunch -> SIG 9"; fi
# QA R-2: 16 KB page size: every arm64 .so needs ELF LOAD segments aligned to >= 16 KB (Unity 2022.3.56f1+; project is on 2022.3.62f3).
# Enforced; CV_REQUIRE_16K=0 only reports.
echo "== 16 KB ELF alignment (arm64-v8a)"
rm -rf .so16 && mkdir .so16 && unzip -q -o "$APK" 'lib/arm64-v8a/*.so' -d .so16 || true
python3 - .so16 <<'PY' | tee elf_align.txt
import os, struct, sys
for root, _, files in os.walk(sys.argv[1]):
    for f in sorted(files):
        b = open(os.path.join(root, f), 'rb').read()
        if b[:4] != b'\x7fELF' or b[4] != 2: continue
        phoff, = struct.unpack_from('<Q', b, 0x20); phentsize, phnum = struct.unpack_from('<HH', b, 0x36)
        al = [struct.unpack_from('<Q', b, phoff + i * phentsize + 0x30)[0] for i in range(phnum) if struct.unpack_from('<I', b, phoff + i * phentsize)[0] == 1]
        m = min(al) if al else 0
        print(('OK   ' if m >= 16384 else '4KB  ') + f + ' LOAD align=' + hex(m))
PY
if grep -q '^4KB' elf_align.txt; then
  if [ "${CV_REQUIRE_16K:-1}" = 1 ]; then err "native libs not 16 KB aligned (QA R-2)"; else echo "::warning::native libs are 4 KB aligned"; fi
fi
rm -rf .so16
[ $fail -eq 0 ] && echo "APK checks passed ($PROFILE)"
exit $fail
