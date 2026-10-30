# Versioning and signing

## Current values (ProjectSettings/ProjectSettings.asset)
| Setting | Value |
|---|---|
| applicationIdentifier | `com.crashverse.game` |
| bundleVersion (versionName) | `0.1.0` |
| AndroidBundleVersionCode | `1` |
| Min SDK | 24 (Android 7.0) |
| Target SDK | 34 |
| Scripting backend | `scriptingBackend: Android: 1` = IL2CPP |
| Architectures | `androidTargetArchitectures: 3` = ARMv7 + ARM64 |
| Unity | 2022.3.50f1 |

## versionName / versionCode scheme
- versionName = `MAJOR.MINOR.PATCH` (semver). It's the text players see. 0.x is for testing tracks and 1.0.0 is the first production release.
- versionCode is an integer that **must strictly increase with every upload to any track**, and a code can never be reused. Proposal: `MAJOR*10000 + MINOR*100 + PATCH`, with any rebuild of the same version bumped by hand (or use a monotonically increasing CI counter). Example: 0.1.0 → 100 (first upload), 1.0.0 → 10000. **OPEN QUESTION (PM):** pick a scheme. The value is 1 today, which works for the first upload.
- Bump both values in PlayerSettings in the release commit, tag `vX.Y.Z`, and record it in CHANGELOG.md.

## AAB and API requirements
- Play needs an **Android App Bundle (.aab)** for new apps. The CI (`.github/workflows/android-apk.yml`) only builds and uploads an **APK** (`build/Android/*.apk`). **BLOCKER:** add an AAB release build (GameCI `androidExportType: androidAppBundle` + keystore inputs), run through `workflow_dispatch` profile=`release`.
- 64-bit: ARM64 is enabled ✓ (IL2CPP ✓).
- Target API: new apps and updates must currently target **API 35 (Android 15)**. Google moves this requirement up every year (around August 31). **BLOCKER:** set `AndroidTargetSdkVersion` from 34 to 35 and install the API 35 SDK in the build environment. Check the current requirement on the day you upload. OPEN QUESTION: does Unity 2022.3.50f1 build cleanly with target 35? Test it.
- Size: 200 MB compressed download limit for the base module. Use Play Asset Delivery if content grows.
- Release build: `CRASHVERSE_DEV` off, not a Development Build, a server backend registered (see README `ReleaseBuildGuard`).

## Upload key plan
- The **upload key** is created once (`keytool -genkeypair -v -keystore crashverse-upload.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000`). Play App Signing keeps the real signing key.
- Keep the keystore **where builds happen**. The owner has only a phone, so builds run in GitHub Actions (GameCI). Create the keystore once on a trusted machine or in a one-off job, then store it base64-encoded as a **protected-environment secret** (`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`). This matches Security B7, which requires secrets in a protected environment and actions pinned by SHA.
- **Back it up**: keep 2 offline copies of the .jks and its passwords (e.g. an encrypted file in personal cloud storage plus a password manager). If it's lost, you can ask for an upload key reset through Play support. That's slow but recoverable, because Play App Signing holds the app key.
- **Never commit it.** `.gitignore` has no keystore rule today. Add `*.jks`, `*.keystore` and `*.p12`. Never paste it into chat or tickets.
- **[APPROVAL]** Creating secrets in the repo needs the owner's go-ahead (the repo isn't pushed yet, per BUILD.md).
