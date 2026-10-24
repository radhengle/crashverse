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
[ $fail -eq 0 ] && echo "APK checks passed ($PROFILE)"
exit $fail
