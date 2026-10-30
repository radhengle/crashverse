# CRASHVERSE QA emulator setup (box)

Status 2026-10-16: SDK installed, AVDs created, but NEW emulator instances hang at startup on the box
(qemu sits at ~0% CPU / ~240 MB RSS, adbd never comes up) while two other agents' emulators
(`t` on emulator-5554 offline, `qa34` on emulator-5560) were already running. Suspected cause: box
resources/KVM contention (15 GB RAM, ~5 GB free). Retry once other emulators are stopped.

## Installed
- SDK root: /opt/android-sdk (cmdline-tools/latest, platform-tools, emulator,
  system-images;android-36;google_apis;x86_64)
- AVD `qa36` (API 36, pixel_6). Also `qa34pm` (API 34) from /home/box/android-sdk.
- Build-tools (apksigner/aapt/zipalign): /tmp/bt/android-14 (re-download build-tools_r34 if /tmp was cleared)

## Start
    export ANDROID_SDK_ROOT=/opt/android-sdk
    A=$ANDROID_SDK_ROOT/platform-tools/adb
    adb devices                     # check others' emulators; don't touch them
    rm -f ~/.android/avd/qa36.avd/*.lock
    nohup $ANDROID_SDK_ROOT/emulator/emulator -avd qa36 -port 5580 -no-window -no-audio \
      -no-snapshot -no-boot-anim -gpu swiftshader_indirect -memory 3072 -cores 4 > /tmp/emu.log 2>&1 &
    until [ "$($A -s emulator-5580 shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 5; done
Healthy sign: qemu process at high CPU and multi-GB RSS within 1-2 min. If it stays ~0% CPU, it's hung: kill -9 it.

## Test an APK
    $A -s emulator-5580 install -r app.apk       # prints INSTALL_FAILED_* on failure
    $A -s emulator-5580 shell monkey -p com.crashverse.game 1
    sleep 20; $A -s emulator-5580 exec-out screencap -p > shot.png
    $A -s emulator-5580 shell input tap X Y
Note: APK must contain x86_64 libs (dev-build-4 does; dev-build-3 does not, so it can't install on x86_64 emulators).
