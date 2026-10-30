# APK smoke, dev-build-3 (CRASHVERSE-dev-arm64.apk, 55.0 MB)
- APK-01 arm64: PASS. Native code is arm64-v8a and armeabi-v7a.
- Permissions: PASS. INTERNET only, no AD_ID.
- Package: com.crashverse.game 0.1.0 (code 1).
- debuggable=true: OK for a dev build only; release must be false (APK-03 open).
- Size: 55 MB, up from 24.9 MB because both ABIs are bundled. An AAB will split them per device.
- targetSdk: see the sdk lines below. compileSdk is 34, so APK-02 (target 35) is still OPEN unless the target shows 35.
- Result: targetSdk 34, minSdk 24. APK-02 is still OPEN (PR #7 not in this build).
