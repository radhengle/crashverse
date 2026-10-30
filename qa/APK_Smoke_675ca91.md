# APK Smoke: CRASHVERSE-dev.apk (675ca91, run 38854570529, dev profile, no audio)
Date: 16 Oct 2026 IST. Tool: androguard 4 (static, no device). File: /workspace/apk/out/CRASHVERSE-dev.apk

| Check | Result | Verdict |
|---|---|---|
| Package / version | com.crashverse.game, versionName 0.1.0, versionCode 1 | OK |
| minSdk / targetSdk | 24 / **34** | **FAIL (APK-02)**: Play requires targetSdk 35 for new apps/updates |
| ABIs | lib/armeabi-v7a only (libil2cpp, libunity, libmain, burst) | **FAIL (APK-01)**: no arm64-v8a; Play rejects. IL2CPP confirmed |
| Permissions | android.permission.INTERNET only | PASS (no AD_ID, nothing unexpected) |
| debuggable | true | OK for dev; **release gate: must be false** (APK-03 tracking) |
| Signing | v2 only (v1/v3 absent), cert CN=Android Debug, SHA-256 205bbc8a…dd7b54 | OK for dev; release must use upload key (Play App Signing) |
| allowBackup / cleartext | not set (defaults: backup allowed; cleartext blocked on 28+) | Note APK-04: set allowBackup=false or backup rules for release (save file/token) |
| Launcher | com.unity3d.player.UnityPlayerActivity, screenOrientation=13 (fullUser) | Check orientation lock intended (APK-05) |
| Size | 24.9 MB compressed (uncompressed libs ~54 MB, v7a only; arm64 will add ~35-40 MB) | OK (<150 MB AAB limit); ship as AAB |

## Tickets (to PM → Developer)
- **APK-01 High**: add ARM64 target architecture (Player Settings › Target Architectures: ARM64; keep v7a optional).
- **APK-02 High**: targetSdk 35 (wt/developer-sdk35 exists, not in this build).
- **APK-03 Med (release gate)**: release profile debuggable=false, release signing, QA tools panel stripped (SEC-02).
- **APK-04 Low**: android:allowBackup=false or dataExtractionRules excluding auth tokens.
- **APK-05 Low**: confirm orientation (fullUser allows portrait if user unlocks rotation).

Device checks: see Phone_Smoke_Checklist_v1.md.
