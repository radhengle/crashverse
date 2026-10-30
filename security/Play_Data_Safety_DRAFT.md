> **DRAFT: NEEDS LEGAL REVIEW** before anything is entered into the Play Console.
> - Source: code at `0d60e07` plus `backend/Backend_Plan_v1.md` (backend *planned*).
> - The answers must match the build that ships. Re-check them whenever an SDK is added.

# Google Play Data Safety: proposed answers for CRASHVERSE

## SDK and dependency scan (commit 0d60e07)

**Packages in `Packages/manifest.json`:**
- Unity first-party only: ugui, the built-in modules (including androidjni, unitywebrequest, jsonserialize), URP 14.0.11, TextMeshPro 3.0.6 and Timeline 1.7.6.

**Not found anywhere in the repo:**
- Facebook SDK
- Firebase
- Unity Analytics or Unity Gaming Services
- Unity Ads, AdMob or any other ad SDK
- Crashlytics
- Unity IAP (`com.unity.purchasing`)
- Google Sign-In or Play Games
- Play Integrity
- `Assets/Plugins`: the folder does not exist.

**Planned SDKs that will change the answers:**
- Unity IAP / Play Billing
- Play Integrity API
- Google Credential Manager
- Facebook Login SDK
- an EncryptedSharedPreferences plugin

**Facebook SDK warning:** it collects device and app-event data by default. Turn off auto-logging of app events (`AutoLogAppEventsEnabled=false`) and advertiser ID collection, then re-assess these answers.

**Cannot confirm from the repo:**
- `ProjectSettings/UnityConnectSettings.asset` is not committed. Whether Unity's own analytics, performance reporting or crash reporting is enabled in the release build is unknown. Verify it in the Editor (Services) and disable it, or declare it.
- Whether the release build will add an advertising ID (`com.google.android.gms.permission.AD_ID`). The expected answer is no. Check the merged manifest.

## Top-level answers

**Does the app collect or share any of the required user data types?**
- **Answer:** Yes.

**Is all user data encrypted in transit?**
- **Answer:** Yes. The plan uses HTTPS only, TLS 1.2+, with certificate pinning.

**Do you provide a way for users to request deletion of their data?**
- **Answer:** Yes: in-app deletion plus a web URL [[URL]].
- **Partial deletion:** not offered. Full account deletion only (PM decision). Unlinking a login provider and hiding your name from leaderboards are separate in-app controls.

**Does the app follow the Families Policy?**
- **Answer:** Not applicable. The PM decision (2026-10-16) is that the game is 13+ and does not join the Families programme. In the Play Console, set the target age groups to 13-15, 16-17 and 18+. Do not select any under-13 group. The neutral age gate blocks under-13s. Age bands are under13, 13to15, 16to17 and adult, and the server enforces them.

**Independent security review:**
- **Answer:** No (optional).

## Data types

Every data type below is processed **ephemerally: no**, and shared with third parties: **no**. Under Play's definition, transfers to service providers acting for us (Google Cloud) and transfers the user starts themselves are not "sharing".

**Personal info: Name (display name)**
- **Collected:** Yes
- **Required or optional:** Optional. A default name is generated.
- **Purposes:** App functionality; Account management

**Personal info: Email address**
- **Collected:** Yes, only with Google or Email login.
- **Required or optional:** Optional
- **Purposes:** Account management; App functionality

**Personal info: User IDs** (game ID, provider subject IDs)
- **Collected:** Yes
- **Required or optional:** Required
- **Purposes:**
  - Account management
  - App functionality
  - Fraud prevention, security and compliance

**Financial info: Purchase history**
- **Collected:** Yes
- **Required or optional:** Required for buyers only
- **Purposes:**
  - App functionality
  - Fraud prevention, security and compliance

**Financial info: Payment info**
- **Collected:** No. Google Play handles payments.

**App activity: App interactions / Other actions** (scores, run inputs, progress)
- **Collected:** Yes
- **Required or optional:** Required
- **Purposes:**
  - App functionality
  - Fraud prevention, security and compliance
- **Notes:** run input logs are kept 90 days.

**App activity: In-app search history, installed apps, other user-generated content**
- **Collected:** No

**App info and performance: Crash logs / Diagnostics**
- **Collected:** No for now. [[YES if Unity or Firebase crash reporting is enabled; see the unconfirmed items in the SDK scan.]]

**Device or other IDs** (installation ID for guest; Play Integrity)
- **Collected:** Yes
- **Required or optional:** Required
- **Purposes:**
  - Account management
  - Fraud prevention, security and compliance
- **Notes:** this is a random install ID, not an advertising ID.

**Location, Contacts, Messages, Photos/Videos, Audio, Files, Calendar, Health, Web browsing**
- **Collected:** No

## Notes for the Console entry
- Data collected only on the server, such as server logs, is still declared if it is linked to the user.
- Play Integrity: Google states that the verdict data itself is not collected by the developer as device data. Declaring "Device or other IDs" covers our install ID.
- Re-submit the form whenever we add the Facebook SDK, analytics, crash reporting or ads.
- The privacy policy URL must be [[URL]] and must match `Privacy_Notice_DRAFT.md`.
