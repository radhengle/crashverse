# CRASHVERSE: Security Test Cases v1 (QA), 16 Oct 2026 IST
**Source:** `security/SECURITY_REVIEW_v1.md` (reviewed at 0d60e07; SB = `Backend/LocalBackendService.cs`). Findings are cited as **SR C1…L4**.
**Scope:**
- Client and CI cases, runnable now.
- Server cases, tracked as **BLOCKED on the real backend**: C1, C2 and H2 to H4 (server side), plus H1/M2 server parts.

**Automation:** `/workspace/qa_automation/SecurityTests` (NUnit, file-level static checks; 36 tests, 31 runnable plus 5 Ignored as BLOCKED). Run with `CV_BUILD_ROOT=/workspace/qa_build_<candidate> dotnet test`; every test is skipped when CV_BUILD_ROOT is unset.
- Builds cleanly.
- **Not run.** Per instruction, it is not run against 0d60e07 or any older build. The "Expected on 0d60e07" column is from code reading only, not a test result.
- The logic-level ranked, clock and campaign cases already live in `qa_automation/LogicTests` (see §5).

Status: **NOT RUN** until a candidate build containing the fixes is delivered.

## 1. Release-build exclusion (debug, test clock, stubs, overrides)
| ID | SR | Case | Method | Pass criteria | Expected on 0d60e07 (code reading) |
|---|---|---|---|---|---|
| SEC-TC-01 | C1 (client) | Local "server" backend not compiled into release | Static: `new LocalBackendService` guarded by `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` or an asmdef define | Guarded; absent from a release IL2CPP dump (APK step) | FAIL: `Core/GameBootstrap.cs:23` falls back unguarded |
| SEC-TC-02 | C1 | Release fails closed with no remote backend | Static, plus APK with no backend config | App shows "service unavailable"; no local DB is created (`local_server_db.json` absent) | FAIL (no fail-closed path) |
| SEC-TC-03 | M1 | Test-clock APIs compiled out: `SetTestClock`, `SetTestOffset`, `AdvanceTestClock`, `ClearTestClock` | Static (4 cases); APK: reflection/IL2CPP dump shows no symbols | All guarded or absent | FAIL: SB:88, :90 public; callers in `UI/Screens.cs:572` |
| SEC-TC-04 | M1 (new detail) | QA debug panel unreachable in release | Static: `new QADebugModal` guarded; APK: no "⚙" on the map | Guarded; no button | FAIL: `UI/Screens.cs:55` "⚙" opens `QADebugModal` (`:555`) unguarded. Its doc comment says "development builds and editor only", but nothing enforces that. **New finding, see SEC-02** |
| SEC-TC-05 | M1 | Persisted `testFixedUnixMs`/`testOffsetMs` in the DB not honoured in release | Static; APK: edit the DB fields, then relaunch | Time unaffected | FAIL (SB:78-88, honoured in `Online()`/ServerNow) |
| SEC-TC-06 | C2 (client) | Premium grant stub off | Static: `GrantPremiumPassStub` guarded or removed; APK: no path grants premium | Absent in release | FAIL: SB:465 public, unguarded (no UI caller found) |
| SEC-TC-07 | C2 | No client-side entitlement grant in purchase callbacks | Static regex on `OnPurchaseComplete/ProcessPurchase` near `premium = true` / `Grant(` | No match | PASS (IAP not wired; store button is a toast stub `UI/Screens.cs:547`) |
| SEC-TC-08 | H3 (client) | Daily Titan override never read from device storage | Static: `daily_titan_override` guarded or absent. APK: drop `daily_titan_override_<today>.json` into persistentDataPath with `baseShots: 99` and start ranked | Ticket `shotBudget` = server config | FAIL: SB:197-206 reads the file; the ticket uses it (SB:228-231) |
| SEC-TC-09 | L2 | No FNV/checksum used as "signing" | Static grep `(?i)fnv` in runtime code; doc check that QA_NOTES.md:63 is reworded | No match | PASS in code (FNV is only in QA_NOTES.md) |
| SEC-TC-10 | §8 | No PlayerPrefs for tokens or progress | Static | No match | PASS |
| SEC-TC-11 | §6 | No hardcoded secrets in tracked C# or JSON | Static regex (api key, client secret, password, private key, `AIza…`) | No match | PASS (per SR §6) |
| SEC-TC-12 | M4 (client) | Guest ID ≥ 128-bit | Static on SB:71 | Not truncated below 32 hex | FAIL (8 hex ≈ 32 bits). The final fix is a server-issued ID (BLOCKED) |
| SEC-TC-13 | L3 | Ranked journal cleared after finalise and excluded from backups | Logic: FlushPending clears the journal (already covered in LogicTests); APK: `adb backup` / Auto Backup doesn't contain it | Cleared; not in backup | PASS for clearing (`Core/TitanRunJournal.cs:18,36`); backup part is in SEC-TC-42 |
| SEC-TC-14 | M3 | Campaign save values never feed boards or rewards | Logic (existing): BUG-13 tests. Server validates and computes; the client save isn't read by the backend | Edited save gives no board or reward change | PASS (LogicTests BUG13). Optional Keystore HMAC: n/a |

## 2. CI hardening (`.github/workflows/android-apk.yml`)
| ID | SR | Case | Pass criteria | Expected on 0d60e07 |
|---|---|---|---|---|
| SEC-TC-20 | M5 | Least-privilege token | Top-level `permissions: contents: read` | FAIL (no block) |
| SEC-TC-21 | M5 | Actions pinned to full commit SHAs (checkout, cache, unity-builder, upload-artifact), with Dependabot to bump them | Every `uses:` ends `@<40 hex>` | FAIL (lines 19, 25, 33, 46 pinned by tag) |
| SEC-TC-22 | M5 | `allowDirtyBuild` off | Not `true` | FAIL (line 42) |
| SEC-TC-23 | M5/§6 | Secret-using jobs not triggered from forks or `pull_request_target` | No `pull_request_target`; no fork PR trigger with secrets | PASS (push to main and dispatch only) |
| SEC-TC-24 | §6 | Unity licence, email and password only in a protected `release` environment with required reviewers | `environment:` set on the job; repo settings show reviewers (manual check) | FAIL (secrets used on every push to main) |
| SEC-TC-25 | M5 | Cache doesn't restore across branches | No bare `Library-` restore key; key includes the branch | FAIL |
| SEC-TC-26 | §6 | Secrets never echoed | No echo/print of `secrets.*`; log review of one run | PASS (static) |
| SEC-TC-27 | §6 | Keystores ignored and never committed | `.gitignore` has `*.keystore` and `*.jks`; none tracked | FAIL (not in `.gitignore`); none tracked |
| SEC-TC-28 | §6 | No keystore uploaded as an artefact; release signing only from tags | upload-artifact paths exclude keystores; signing job is `on: push: tags` | PASS for artefacts; release-signing job doesn't exist yet |
| SEC-TC-29 | §8 Release | CI release-gate grep fails the build if dev hooks remain (LocalBackendService, SetTestClock, GrantPremiumPassStub, daily_titan_override, QADebugModal) | Step present and failing on a seeded hook | FAIL (no gate) |
| SEC-TC-30 | §6 | Repo hygiene: secret scanning with push protection, Dependabot, branch protection on main, CODEOWNERS for `.github/` | Manual check in repo settings (QA has gh read access) | Not checked (settings; needs admin view) |

## 3. Android hardening (APK and static)
| ID | SR | Case | Pass criteria | Expected on 0d60e07 |
|---|---|---|---|---|
| SEC-TC-40 | L4 | minSdk ≥ 26 if the audience allows (PM to confirm) | `AndroidMinSdkVersion` ≥ 26 | FAIL (24) |
| SEC-TC-41 | §8 | IL2CPP, ARM64 | `scriptingBackend` Android = 1; arm64 in target architectures | Check on candidate |
| SEC-TC-42 | §8/L3 | `allowBackup=false` or `dataExtractionRules` excluding save, journal and tokens | Custom manifest present with either; `adb backup` contains none | FAIL (no custom manifest) |
| SEC-TC-43 | §8 | `usesCleartextTraffic="false"` plus a network security config | Present; an HTTP request fails | FAIL (absent) |
| SEC-TC-44 | §8 | Managed stripping High; R8/minify on | ProjectSettings plus gradle template | Check on candidate |
| SEC-TC-45 | §8 | No exported components beyond the launcher | `aapt dump xmltree` on the APK | APK required |
| SEC-TC-46 | §8 | TLS 1.2+ and pinning for the API host, with a backup pin | MITM proxy with a user CA gets refused | BLOCKED (no API host yet) |
| SEC-TC-47 | §8 | Runtime signals (root, emulator, Frida, repackage) reported, not enforced client-side | Signals reach telemetry | BLOCKED (backend) |

## 4. BLOCKED on the real backend (tracked, not runnable)
| ID | SR | Case (server-side) | Unblock when |
|---|---|---|---|
| SEC-TC-60 | **C1** | Run keys, run state and wallet exist only server-side (KMS); with full device-file access, a valid receipt can't be forged; editing the wallet/premium/seasonXp on the device has no effect | Backend with KMS-held keys |
| SEC-TC-61 | **C2** | Play receipt validation: fake, reused, other-account, wrong-package and refunded tokens are rejected or revoked; acknowledgement within 3 days; RTDN and Voided Purchases clawback; restore from server records | Backend plus Play Developer API sandbox |
| SEC-TC-62 | **H2** | Ranked Titan: cap-sized bot steps get flagged by replay (top-N and >p99); server minimum interval between shots; Play Integrity is required to start | Replay worker plus Integrity |
| SEC-TC-63 | **H3** | Overrides come only from signed server config; tampering with the config hash is rejected; ticket `shotBudget` = server value | Backend config service |
| SEC-TC-64 | **H4** | Linking verifies provider tokens (Google `aud/iss/exp/sub`, Facebook `debug_token`, X PKCE server-side); one identity maps to one account; conflict UI with no silent merge; re-auth to unlink; never unlink the last method; sessions revoked | Auth service |
| (also) H1 | H1 | Campaign: per-attempt token plus input-log replay; server-computed score | Replay worker |
| (also) M2 | M2 | Time from our own API over TLS with pinning; offset sanity bound | API host |
| (also) §3 | — | Nonce/timestamp replay rejection (±60 s); rate limits (auth 5 per minute; 1 start per day per mode); JWT expiry and refresh-token reuse detection | Backend |

## 5. Mapping: existing QA tickets ↔ review findings
| Existing ticket | Status | Review finding | Relationship |
|---|---|---|---|
| SEC-01 (client re-signed journal) | Fixed 6e1ecac | **C1** | SEC-01 moved signing server-side, but in the stub the "server" key sits in a device file, so C1 re-opens the same threat until there's a real backend (SEC-TC-60). Logic tests (`SEC_01_*`, receipts) stay as the regression |
| BUG-03 (device clock) / BUG-12 | Fixed | **M2**, **M1** | The clock source is a placeholder (M2, SERVER-DEPENDENT); the test-clock hooks ship (M1 → SEC-TC-03/05) |
| BUG-04, online/offline 5000 cap | Fixed 6e1ecac / 40f5286 | **H2** | The cap is plausibility only (SERVER-DEPENDENT since 6e1ecac); proof needs replay (SEC-TC-62) |
| BUG-13 / BUG-16 / SC-13 | Fixed 40f5286 / d109b07 | **H1**, **M3** | The server computes from client stats within bounds. "Stats and mastery trust" is already logged SERVER-DEPENDENT. H1 is the same item, ranked High |
| BUG-14 (campaign queue) / L3 journal | Fixed | **L3** | Queue and journal are plaintext by design; the server re-validates. Backup exclusion is in SEC-TC-42 |
| Stubbed premium grant (SERVER-DEPENDENT since 6e1ecac) | Open dependency | **C2** | Same item; SEC-TC-06 (client) and SEC-TC-61 (server) |
| QA-UI-006 (unencrypted saves not used for ranked) | Pass | **M3** | Consistent; SEC-TC-14 |
| SG-19/SG-36 guest/link | Pass (logic) | **H4**, **M4** | Linking logic passes, but there's no token proof (H4) and the guest ID is 32-bit (M4) |
| BUG-01 (csproj) / CI | Fixed | **M5** | Unrelated to BUG-01; M5 is new (SEC-TC-20 to 29) |
| *(none)* | — | **H3**, L1, L2, L4, §7 privacy | New for QA. H3 → SEC-TC-08/63; L1 server-only (byte FixedTimeEquals, BLOCKED); L2 → SEC-TC-09; L4 → SEC-TC-40 to 45; §7 (Data Safety, account deletion, DPDP/GDPR) → separate privacy checklist, owner PM/legal |

## New finding from writing these cases
| ID | Sev | Where | Issue |
|---|---|---|---|
| SEC-02 | Med | `UI/Screens.cs:55` (map "⚙" button), `:555` QADebugModal | The QA tools panel (time travel +1h/+1d/+7d, "Jump to 23:59:50 UTC", offline toggle) is reachable from the map in every build. Its doc comment says dev/editor only, but there's no `#if` or define. In release it would let any user move "server" time for Daily Titan, rewards and seasons (with the local backend). Part of SR M1/C1; fix with the same release-gate define and add it to the CI grep (SEC-TC-29) |
