# Data Safety draft (Release Manager)
Builds on Security Admin's `security/Play_Data_Safety_DRAFT.md` (written at commit 0d60e07), checked against the current tree. **Not for entry until Security Admin and legal review it.**

## Facts found in the current tree
- **Manifest** (`Assets/Plugins/Android/AndroidManifest.xml`) declares **no permissions** and only removes `com.google.android.gms.permission.AD_ID` (`tools:node="remove"`). Unity adds INTERNET by default (it's needed for `unitywebrequest`). OPEN: run `aapt2 dump permissions` on the release AAB/APK and paste the result here.
- **Packages:** only Unity first-party (URP 14.0.11, TMP, Timeline, uGUI, Newtonsoft JSON 3.2.1, built-in modules). No Firebase, Facebook, ads, IAP, Play Integrity or analytics SDK.
- **UnityConnectSettings.asset** is now committed, and all 6 `m_Enabled` flags are 0 (Analytics, crash and perf reporting, Ads, IAP disabled). This **resolves** the "cannot confirm" note in Security's draft. `Assets/Plugins/Android` now exists too, which also updates that draft.
- **Backend:** `server/` (ASP.NET Core, Firestore) is **not deployed** ("No hosting is approved"). The release build refuses to boot without a server backend.

## Proposed answers (if the backend ships as planned)
Same as Security's draft: data is collected (yes), encrypted in transit (yes, HTTPS/TLS 1.2+), deletion in-app + web `[[URL]]`, no partial deletion, no sharing. Data types: display name (optional), email (optional, Google/Email login only), user IDs (required), app interactions/scores/run inputs (required), plus the rest of that draft. No location, contacts, photos, or advertising ID.

**If the launch has no backend (offline-only)**, collection could drop to "No data collected". That would need a different build, because the release guard blocks it.

## Open questions for Security Admin
1. **Rev 1.1: v1 has no IAP** (real-money gems cut), so drop "Financial info: Purchase history" from v1 answers and the purchase-row retention/S6 items until IAP returns. Which other SDKs ship in v1.0: Play Integrity, Credential Manager, Facebook Login? Each one changes the answers. Real-money items in `store_catalog.json` must be removed for v1.
2. Device or other IDs: is the install ID declared under "Device or other IDs"?
3. Does crash/diagnostic data go to our server? Then declare "Crash logs/Diagnostics".
4. Are run input logs kept 90 days (Backend review) and stated in the privacy notice?
5. Web deletion URL and privacy policy URL: hosting and owner?
6. Age band and country: declare them under "Other personal info"?
7. Confirm the merged manifest from a real release build (AD_ID absent, permission list).

## Testing flavour (rev 1.1)
Internal/closed builds use the local backend (data stays on the device, nothing is sent to a server). The Data Safety form covers the production build, so answer it for the hosted backend.
