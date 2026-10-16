# Privacy, SDK settings, account deletion and age gate (client)

## Unity Services and Android manifest
- `ProjectSettings/UnityConnectSettings.asset`: Unity Analytics, Cloud Diagnostics crash reporting, performance reporting, Ads and IAP services are all **disabled** (`m_Enabled: 0`). CI (`Tools/ci/check_release_guards.py`) fails if any of them is turned on.
- `Assets/Plugins/Android/AndroidManifest.xml` (custom main manifest) removes `com.google.android.gms.permission.AD_ID` with `tools:node="remove"`, so the merged manifest has no AD_ID even if a library declares it. CI checks that the removal stays in place. To verify a built APK: `aapt2 dump permissions CRASHVERSE.apk | grep AD_ID` should print nothing.

## When the Facebook SDK is added (Security Admin)
Set these in the Facebook SDK settings / `AndroidManifest.xml` **before** the first build that contains it:
- `AutoLogAppEventsEnabled = false` (`<meta-data android:name="com.facebook.sdk.AutoLogAppEventsEnabled" android:value="false"/>`)
- `AdvertiserIDCollectionEnabled = false` (`<meta-data android:name="com.facebook.sdk.AdvertiserIDCollectionEnabled" android:value="false"/>`)
- Keep the AD_ID removal above (the Facebook SDK adds AD_ID by default).
- Implement the data-deletion callback (requirements F3) on the server before enabling Facebook Login.

## Deletion_AgeGate_Requirements.md → implementation map
| Req | Client | Server stub (`LocalBackendService`, dev only) / interface |
|---|---|---|
| A1 | Map → Settings → Delete account (2 taps), for guest and linked accounts | n/a |
| A2 | `DeleteAccountScreen`: copy per privacy notice §6 (deleted / pseudonymised purchases kept / 30 days, 7-day cancel / no refunds); 2 s press-and-hold (`HoldToConfirm`), a single tap never confirms | n/a |
| A3 | Linked accounts re-auth via `AccountLifecycle.ReauthProvider` → `Reauthenticate` | `DeleteAccount`/`UnlinkAccount` throw `REAUTH_REQUIRED` (401) unless re-auth < 5 min |
| A4 | On confirmation: session token cleared, all CRASHVERSE files in `persistentDataPath` (incl. `local_server_db.json`) and PlayerPrefs wiped, `GameBootstrap.RestartFirstRun()` → age gate again | `DeleteAccount` returns `scheduledDeletionUnixUtc` |
| A5 | On connect, a pending deletion offers "Cancel deletion" | 7-day grace, `CancelAccountDeletion`, purge after grace → new account |
| A6 | Offline request is queued (`cv_delete_pending`), shown as "pending until online", sent on next connection; never faked | n/a |
| B | Web deletion page: server/BE (not in client) | — |
| C1 | Guests delete with their session | ✓ |
| C2 | Launch warning with date + "Keep my account" | Warning at day 335, purge at 365 inactive days; any call resets the clock; `KeepAccountActive` |
| C3 | `AgeGate.ShowLinkPrompt` hides link prompts for minors without consent | — |
| D | Deletion job (detach/pseudonymise/audit/backups): **BE**. The stub only does a full wipe incl. all board entries | — |
| E1 | `AgeGateScreen` runs first, before any backend call; month + year pickers with no default, newest year first, no age hint | — |
| E2 | Under 13 → `BlockedScreen` ("CRASHVERSE isn't available."), local `cv_age_blocked` flag, no DOB stored, no server calls | — |
| E3 | Band (`13to17`/`adult`) + locale country → `SetAgeGate` | Most restrictive band kept |
| E4 | Interface `RequestParentalConsent(parentEmail)`; parent UI/web verification **TODO [[LEGAL]]** | consent flags on `AccountState` |
| E5 | Store buy buttons, link buttons hidden for minors without consent; generated name | `LinkAccount` 403 `MINOR_CONSENT_REQUIRED`; leaderboard returns generated name; `VerifyPurchase` 403 `AGE_RESTRICTED` |
| E6 | Buy hidden without purchase consent | `PurchaseAgeRestrictedMarkets` **TODO [[LEGAL]]** |
| F1/F4 | Settings → Account → Linked accounts (unlink with re-auth; last login kept) | `UnlinkAccount` 409 `LAST_LOGIN` |
| F2/F3 | Provider revoke + Facebook callback: **BE** | — |
| F5 | Settings → Privacy → Hide my name on leaderboards | `SetHideName`; API returns generated name for hidden players |
| G | Open items stay placeholders (see requirements §G) | — |

Open (G / [[LEGAL]]): grievance officer, legal entity, tax retention, backup expiry, parental verification mechanism, consent-proof retention, 18+ purchase markets and non-India consent ages, age-signal source (locale used for now), and whether sending the install ID before the gate is acceptable (the client currently sends nothing before the gate).
