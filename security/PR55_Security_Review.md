# PR #55 (integration/v2 → main) security review at eb0126f

Reviewed 2026-10-24, about 18:25 IST, by Security Admin. This was a read-only review; nothing is posted on GitHub.
Diff reviewed: merge-base 7d359e6 (main, after #52 and #33) to eb0126f. That covers 18 commits and 111 files (+4181/−307).
Tools: git, gh (read-only API and job logs), gitleaks 8.21.2, and GameCI unity-builder@4423ee7 source (build.sh).

## Verdict: CHANGES REQUESTED

There are 2 blockers, both in the offline release path. Everything else is a condition or a follow-up.
After the two blockers are fixed, this becomes **APPROVE WITH CONDITIONS**: M1–M3 must be done before any upload-key secret is configured.
**Separately, for the user (account-level, not a #55 code issue):** the repository is **public**. See H-1.

---

## Blockers

### B-55-1 (High): account deletion never deletes anything in the release build
- **Where:**
  - `Assets/_Project/Scripts/Core/GameBootstrap.cs:55`: release now uses `OfflineBackendService`.
  - `Assets/_Project/Scripts/Core/AccountDeletion.cs:37-44`: `Flush` calls `backend.DeleteAccount`, which always throws `BackendException("offline")` (`OfflineBackendService.cs:28`). It returns `PendingOffline`, and `WipeLocal()` is reached only after the server confirms.
  - `Screens.cs:808-812`: the UI says "Deletion is pending until you're online".
- **Effect:** in this build the server is never online. A guest's profile (name, avatar, badge), wallet, campaign save and `progress_v2.json` therefore stay on the device indefinitely. The user is told deletion is pending, which can never complete.
- **Requirements broken:**
  - Deletion_AgeGate_Requirements §A: guest data deletes with the session.
  - Play's in-app deletion requirement.
  - The PM decision "guests delete with their session".
- **Fix:**
  - If there is no server account (`backend is OfflineBackendService`, or `!backend.IsOnline` and no server account was ever created), `DeleteAccountScreen.Confirm` / `AccountDeletion.Request` should:
    - call `WipeLocal()` immediately;
    - not set `cv_delete_pending`;
    - call `GameBootstrap.RestartFirstRun()`.
  - Keep the existing queue-until-online path only for a real server backend.
  - Show offline-specific copy: "This deletes your profile and progress from this device. It can't be undone." Remove the purchase, pseudonymised-records, 30-day and 7-day-cancel text, which doesn't apply offline (`Screens.cs:772-777`).
  - Add a test: with an OfflineBackendService, hold-to-delete must remove `campaign_save.json`, `progress_v2.json`(.tmp), `campaign_sync_queue.json` and all prefs, and end in the first-run state.
  - No partial deletion: keep it all-or-nothing, as `WipeLocal` already is.

### B-55-2 (Medium, blocking because it's an explicit requirement and a small fix): the age band doesn't persist offline, so the gate is asked again on every launch and the band can be relaxed
- **Where:**
  - `Screens.cs:842`: `Needed` is true while `cv_age_submitted_v` is 0 and `PendingDob` is null.
  - `cv_age_submitted_v` is set only in `OnServerBand` (`Screens.cs:849`), which needs `SubmitAgeGate` to succeed. That never happens with `OfflineBackendService` (`OfflineBackendService.cs:31`).
  - After every cold start, `GameBootstrap.cs:43` shows the gate again.
  - `Confirm` (`Screens.cs:913`) overwrites the stored band with whatever is entered, so a 13–17 user can become "adult" on the next launch. The under-13 block (`cv_age_blocked`, `Screens.cs:912`, `GameBootstrap.cs:42`) does persist correctly.
- **Effect:** repeated prompting invites the user to enter a different age, and the band isn't stable. Offline, the band only gates Store buy buttons (stubs), but a band that resets is still non-compliant with E1/E3. It would also carry over as-is when the server returns.
- **Fix:**
  - When no server backend is configured (release: `BackendLocator.Current == null`), treat the locally classified band as final for this install. Either:
    - add a `cv_age_local_v` flag set in `Confirm` and have `Needed` skip the re-ask when it's set and there is no server; or
    - choose the backend before the gate and check `Backend.IsOnline`.
  - `Confirm` must never relax an existing stored band: take the most restrictive, as `OnServerBand` already does at `:848`.
  - When the real server ships, the N5 re-gate (`AGE_GATE_REQUIRED` → `POST /account/agegate`) handles the upgrade.
  - Add a test: offline backend, gate passed as 13–17, then relaunch. Expect no gate, and the band is still 13–17.

---

## Findings by severity

### High
- **H-1 (account-level, for the user via PM): the repo `radhengle/crashverse` is PUBLIC.** This contradicts the PM decision "the repo stays private".
  - `gh api repos/radhengle/crashverse` → `"visibility":"public"`, and an unauthenticated API GET returns 200.
  - The PublicEvent is by radhengle on 2026-10-16 at 08:53 IST, the same second as repo creation.
  - Exposed today:
    - all source and history;
    - every Actions log, including Unity's echoed command line (see M-1);
    - artifacts: dev-signed APKs, emulator logcat and screenshots, and the future signed AAB;
    - the `dev-build-N` prereleases.
  - Anyone can now open fork PRs.
  - gitleaks over **all 279 commits (`--all`): no leaks**. The 4 DEV_* and 3 UNITY_* secrets are repo secrets, not in code.
  - The user must decide (account-level):
    - **Make it private again** (Settings → General → Danger Zone). On the Free plan, environment protection rules are then unavailable, so the M-2 fix needs a paid plan, which was declined.
    - **Or keep it public on purpose.** Environment protections are then free: do M-2, and set Settings → Actions → "Require approval for all external contributors" for fork PR workflows.
  - I changed nothing.

### Medium
- **M-1: upload-key passwords go on the Unity command line and are echoed into the (public) job log.**
  - `android-aab.yml:92-96` passes `androidKeystorePass` / `androidKeyaliasPass` to GameCI. GameCI's `build.sh:136-139` (at 4423ee7) puts them on the `unity-editor` command line.
  - Unity prints `COMMAND LINE ARGUMENTS:` to stdout. Run 40093293947 (release job) log lines 3185-3215 show this, and dev-signed dispatch runs 40083850198, 40082473407 and 40074274944 show the dev passwords there as `***`.
  - The only protection is GitHub's secret masking, and the values are also visible in the container's process list.
  - This doesn't meet "passwords via env, not on the command line".
  - Fix:
    - Build the AAB **unsigned** in GameCI: drop the four `androidKeystore*` inputs and `androidKeystoreBase64`. Let `BuildScript.cs:70-71` allow an unsigned release AAB only when `CRASHVERSE_SIGN_AFTER=1`.
    - Then sign on the runner with `jarsigner -keystore "$KS_FILE" -storepass:env KS_PASS -keypass:env KA_PASS "$AAB" "$KA_NAME"`, using step-scoped `env:`.
    - Verify with `jarsigner -verify -certs` and pin the upload cert SHA-256 (new `Tools/ci/upload_cert_sha256.txt`). That also gives a cert check stronger than the dev-key name denylist.
    - It also removes the duplicate keystore decode: the step at `:67-73` plus GameCI's own `androidKeystoreBase64` decode write the same file.
    - The dev APK job (`android-apk.yml:160-163`) has the same pattern. It's lower impact (dev key), but fix it the same way.
- **M-2: the `release` environment doesn't exist, and existing environments have no protection.**
  - `gh api …/environments`: `dev-builds`, `dev-signing` and `unity` all have `protection_rules: []` and `deployment_branch_policy: null`. `release` will be auto-created unprotected on the first run.
  - `if: github.ref == 'refs/heads/main'` (`:20,43`) only exists in the workflow file. A writer can dispatch a modified copy from another branch, so it isn't an access control.
  - Fix (user, account-level), before the upload-key secrets are ever added:
    - create the `release` environment;
    - set deployment branches to "Selected: main";
    - set a required reviewer (the user);
    - store the 4 `ANDROID_*` secrets **only** as `release` environment secrets, never repo secrets.
  - This is free while the repo is public.
- **M-3: the signed AAB is uploaded as an artifact with 14-day retention** (`android-aab.yml:130-135`). Public-repo artifacts can be downloaded by any signed-in GitHub user. This is a pre-release leak, not a key leak. It's the same retention condition I set on #8 and it's still open. Fix: `retention-days: 1`, or no artifact; later, push straight to the Play internal track with workload identity / OIDC (Release Manager checklist).

### Low
- **L-55-1: the Store shows stub/debug copy in release.**
  - `Screens.cs:635` says "Purchases are stubbed in this build."
  - `Screens.cs:644` shows the toast "Purchase stub: <sku> (IAP not wired)".
  - No IAP path is live: there's no billing/purchasing package in Packages/manifest.json, and `gems_small`/`gems_large` (`priceType: real`) are hidden by `enableGemPacks: false` in `store_catalog.json:3` and `StoreValidator.cs:23`.
  - Fix: when the backend is offline, show "Coming soon" and hide the buy buttons.
- **L-55-2: misleading sync/leaderboard claims offline.**
  - `GameSession.cs:295` says "result saved and will sync to the leaderboard later". There's no leaderboard in this build, and `campaign_sync_queue.json` grows with every win.
  - Settings shows "Hide my name on leaderboards" and "Linked accounts" (`Screens.cs:680-682,712`); both only toast or say "need to be online".
  - Fix:
    - When `!Backend.IsOnline` and there's no server client, don't queue or show the note, or cap the queue.
    - Hide those two Settings rows, or label them "Coming soon".
- **L-55-3: the AAB assertion doesn't check backups.** `android-aab.yml:112-113` checks debuggable and targetSdk only. Add `allowBackup="false"`, `fullBackupContent="false"` and `dataExtractionRules` checks on the bundletool manifest dump, as `check_apk.sh:20-22` does for APKs.
- **L-55-4: bundletool.jar is downloaded without a hash pin** (`android-aab.yml:110`), while build-tools is pinned (`:108`). Pin its SHA-256. The `bt.zip` download in the AAB job is unused (`$BT` is never referenced), so drop it.
- **L-55-5: the `--built` token list is narrow.** `check_release_guards.py:27` covers only the hook tokens plus `Button_DevAimHint`, `UITapProbe` and `QADebugModal`. Also scan for `LocalBackendService`, `DevOverride` and the test-clock type names, so a guard regression shows up in the binary too.
- **L-55-6 (carried over): unity-tests.yml isn't fork-gated** (`unity-tests.yml:5-8,61-70`). Now that the repo is public, fork PRs run it with empty Unity secrets and fail noisily. GitHub doesn't pass secrets to `pull_request` from forks, so nothing leaks. Add `if: github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository` on the Unity steps or job.

### Info / notes for when the server returns
- **The release guard is weaker:** `BuildScript.cs:125-127` ("no real backend") is now satisfied by `OfflineBackendService`. That's acceptable for v2. When the real client ships, assert that it, not the Offline one, is wired in release.
- **Client-side economy:** coins, gems and chests are granted locally (`GameSession.cs:289,333`, `PlayerProgress.cs`), and the Daily Boss uses the device-local date (`ScreensMap.cs:59,327`), so it can be farmed by changing the clock. That's fine offline, but **the server must never import or trust the local wallet or progress** at migration.
- **The custom player name isn't consent-gated** (`ScreensProfile.cs` / `PlayerProgress.CreateProfile`). It stays on the device, so that's fine offline. Apply `AgeGate.CanCustomName` before any name leaves the device.
- **Instagram** appears as a disabled provider (`PlayerProgress.cs:90`) but isn't in `AuthProvider` or the privacy notice. That's fine while disabled, but update the Privacy and Data Safety drafts before enabling it.
- No PII logging was added: the only new runtime log is `PlayerProgress.cs` "corrupt save" with the exception message, which has no data.

---

## Checklist results

### 1. OfflineBackendService in release
| Check | Result |
|---|---|
| No ranked or authoritative UI | **OK.** All 37 calls fail with `BackendException("offline")`; nothing is granted, validated or stored. `IsOnline=false` gates Daily Titan, Daily Rewards, Season Pass and Leaderboards in More (`ScreensMap.cs:140-147`). Progression tiles say "Coming soon" (`ScreensMeta.cs:103-105`). The ranked button lives only in TitanScreen, which is unreachable offline. There's no "verified" state, and the ranked board stays off. |
| No leftover URLs, keys or dev hooks | **OK.** No new URLs in Assets. The only HttpClient (`Clock.cs:49`) is used only by LocalBackendService, which is `#if CRASHVERSE_DEV` (`LocalBackendService.cs:8`). QADebugModal and UITapProbe are guarded by `CRASHVERSE_DEV \|\| DEVELOPMENT_BUILD`, and the `--built` scan of the release APK passed. |
| Local saves covered by backup exclusions | **OK.** `progress_v2.json` is in `persistentDataPath` (the files domain). `data_extraction_rules.xml` excludes root, file, database, sharedpref and external for cloud-backup and device-transfer, and `allowBackup` and `fullBackupContent` are false. The release APK in run 40093293947 shows allowBackup=false and dataExtractionRules=@0x7f060000. |
| Deletion/reset offline; guest deletes with session; no partial deletion | **FAIL: B-55-1.** `progress_v2.json`(.tmp) was correctly added to `AccountDeletion.LocalFiles:15`, but the wipe is never reached offline. There's no separate partial reset (OK). |
| Age gate and block persist | Block: **OK.** Band: **FAIL, B-55-2.** |
| No personal data logged | **OK.** |
| No IAP paths live | **OK.** There's no purchasing package, real-money SKUs are flag-hidden, and `VerifyPurchase` fails offline. Copy issue: L-55-1. |

### 2. AAB signing job (#8, `android-aab.yml`)
| Check | Result |
|---|---|
| Keystore decoded from secrets into a temp path, removed afterwards, never echoed or uploaded | **Mostly OK.** It's decoded with `umask 077` to `build-secrets/upload.keystore` (`:67-73`, `printf … \| base64 -d`, no echo). It's removed `if: always()` (`:97-99`) before the asserts and the upload. The artifact path is `build/Android/*.aab` only. It's in the workspace rather than `$RUNNER_TEMP`, and decoded twice (see M-1). |
| Passwords via env, not the command line | **FAIL: M-1.** They go via GameCI inputs onto Unity's argv, and are echoed but masked in the log. |
| `--built` on the AAB fails the job | **OK.** `:128` runs `check_release_guards.py --built <aab>`. `scan_built` reads any zip member ending in `global-metadata.dat`, which covers `base/assets/bin/Data/Managed/Metadata/`, and returns errors that end in exit 1. Not exercised yet, because the job is skipped until the secrets exist. |
| Runs only on protected refs, never on fork PRs; permissions | **Partly.** `workflow_dispatch` only, plus `if: github.ref == 'refs/heads/main'`. There's no `pull_request` trigger. Permissions are `contents: read` at the top level and in the job. But the ref check isn't enforced (M-2). |
| Release profile | **OK.** `-crashverseProfile release`. BuildScript refuses an AAB that isn't release or has no custom keystore (`BuildScript.cs:70-71`), and refuses the dev key (`:66-68`). Non-debuggable, targetSdk 35, arm64 and 16 KB are asserted. |

### 3. Signing triggers
- **The `integration/**` secrets trigger is removed.** android-apk.yml:17-18 is now `push: branches: [ main ]` ("Security L-52-1"). Signing runs only from a push to main or a dispatch on main or `dev/*` (`:81,88`). PRs build unsigned with no DEV_* secrets.
- **L-52-1 is fixed.** The publish job (`android-apk.yml:372-392`) allows only `dev-build-N`, resolves `next` to max+1, refuses an existing tag or release (never overwrites), creates it with `--target "$GITHUB_SHA"`, and asserts the tag resolves to `$GITHUB_SHA`. `TAG_IN` is passed via env, so there's no injection.
- **New PR job `build-release`** (`android-apk.yml:228-295`): PRs only, unsigned, no signing secrets, `contents: read`. It runs `check_apk.sh … release` and `--built`. Good.

### 4. Disabled social sign-in stubs
| Check | Result |
|---|---|
| Can't be enabled at runtime | **OK.** `ProfileRules.SocialSignInEnabled` is a constant `false` (`PlayerProgress.cs:91`); there's no flag or remote config. Buttons have no onClick (`ScreensProfile.cs:27-28`, `null` action, `interactable=false`). The smoke test asserts all 5 are disabled (`emulator_smoke_flow.sh:48-49`). LinkedAccountsModal needs `GetAccount`, which fails offline. |
| No app IDs or secrets in the client | **OK.** No Facebook, Google or X app or client IDs anywhere in Assets, Packages or ProjectSettings. |
| No SDK that changes Data Safety | **OK.** Packages/manifest.json has no Facebook, Google Sign-In, Firebase, ads or analytics package. Data Safety is unchanged. The locally stored name and avatar never leave the device, so they aren't "collected". |
| No misleading UI | **OK** for the login screen: "Continue with X · Coming soon" and "Online sign-in is coming soon. Your progress is saved on this device." (`ScreensProfile.cs:27,31`). Elsewhere, see L-55-1 and L-55-2. |

### 5. Full-diff scan (7d359e6..eb0126f)
- **gitleaks:** range of 16 non-merge commits, no leaks. Full history `--all` (279 commits), no leaks.
- **Dev defines:** no new `#if` that ORs `CRASHVERSE_DEV` with a non-editor symbol. The source guard passes in CI. `Data/Dev/PerfL10Fixture.cs` only changes a constant.
- **New network endpoints:** none in the client. In CI there are two downloads in the AAB job: build-tools (pinned) and bundletool (unpinned, L-55-4).
- **Workflow injection and permissions:** no `${{ github.event.* }}` inside `run:`. `customParameters` uses `env.CRASHVERSE_BUILD_PROFILE`, which comes from a `choice` input, so it's low risk. All jobs are least-privilege; only publish has `contents: write`. All actions are SHA-pinned. `persist-credentials: false` everywhere.
- **unity-tests.yml:** only the Unity version changed. The gating follow-up isn't done (L-55-6).
- **Unity bump:** 2022.3.50f1 → 2022.3.62f3, needed for 16 KB alignment, which is now enforced.

### CI at eb0126f (2026-10-24, started 17:47 IST)
- **All green:**
  - Build Android APK 40093293947 (pull_request): guards, release-guard self-tests, compile checks, logic tests, the dev GameCI build, the **release-profile build** (`[BuildScript] profile release`), `check_apk.sh release` (non-debuggable, targetSdk 35, allowBackup=false, 16 KB OK on all 4 arm64 libs), **`--built` release guard: "Release build metadata check passed"**, and emulator smoke on API 34 and 36. Publish was skipped, as expected.
  - Unity EditMode tests: 40093293988 (PR) and 40093290397 (push).
  - server-ci: 40093293945.
- AAB `--built` didn't run (no dispatch, no secrets). That's expected.

---

## Follow-up status
| Follow-up | Status at eb0126f |
|---|---|
| Remove the `integration/**` secrets trigger | **Done** (android-apk.yml:18). |
| L-52-1: tag at the built commit / new tag each time | **Done** (android-apk.yml:372-392). |
| Gate the Unity secrets in unity-tests.yml | **Open** (L-55-6). |
| Wire `--built` into #8's AAB job | **Done** (android-aab.yml:128); not yet exercised. |
| #33 | **Merged** (main 7d359e6). |
| targetSdk 35 | **Done:** ProjectSettings.asset AndroidTargetSdkVersion 35, `check_release_guards.py` MIN_TARGET_SDK=35, `check_apk.sh`, and the AAB assert. |
| CertDigests from env with a startup check | **Open.** `server/src/Crashverse.Server/Domain/Models.cs:15` is still `{ "dev-cert-sha256" }`, used at TitanService.cs:69. Load it from `CRASHVERSE_CERT_DIGESTS` and have ProdGuard refuse the placeholder. Needs the Play upload/app-signing cert. |
| #3 install ID and Reset | **Open.** There's no InstallId on main (`git grep` finds none). #3 is still changes requested. When it lands, use noBackupFilesDir and wire `Reset()` into `AccountDeletion.WipeLocal`. |
| 4 age bands | **Open.** `Meta/AgeGate.cs:11` is still `Unknown/Under13/Minor13to17/Adult`, and `:29-30` maps "13to17" (the server sends `13to15\|16to17`). |
| Server age token / AGE_GATE_REQUIRED (client) | **Open.** The server side exists (ComplianceService.cs:72, AuthService.cs:75,210, README N4/N5). The client `AgeGateResponse` (ServerContracts.cs:67) has no `ageToken`, nothing sends `deviceInstallId`, and there's no AGE_GATE_REQUIRED handling (IBackendService.cs:39-42 is docs only). Not reachable while offline. |

## Reply text for Developer
See the parent report. Same content as the blockers and conditions above.

---

## Re-review at 603ccc0 (2026-10-30, IST) — scoped: B-55-1, B-55-2, signing secrets
**Verdict: APPROVE.** One condition must be met before the ANDROID_* upload-key secrets are added (C-55-1 below). It doesn't block the merge, because no ANDROID_* secret exists yet.

| Item | Result |
|---|---|
| **B-55-1** offline delete | **Fixed.** `BackendLocator.IsLocalOnly` (IBackendService.cs:94) is true for OfflineBackendService/null. `AccountDeletion.Request` (AccountDeletion.cs:28) wipes at once and never sets `cv_delete_pending`. `Flush` (:38) also completes a request queued by an older build locally. `WipeLocal` (:56-62) deletes all LocalFiles (save, progress_v2.json(.tmp), which holds the profile and wallet, journal, queue), every other `*.json`, and `PlayerPrefs.DeleteAll`. The UI (Screens.cs:808-812) then calls `RestartFirstRun` and shows the toast "Your data was deleted from this device." The offline copy (Screens.cs:788,797) reads "This deletes your profile and progress from this device. It can't be undone." It has no purchase, 30-day or 7-day text. The DeleteAll also clears the under-13 block flag, but a blocked install never reaches Delete (GameBootstrap.cs:43), so that's OK. Tests: Tools/Tests/Tests.cs:1061 (files, prefs, no pending flag, copy words) and PlayMode `Offline_delete_wipes_every_file_and_pref` (FirstRunTapTests.cs:231, first-run age gate after the wipe). |
| **B-55-2** offline age band | **Fixed.** Confirm stores `MostRestrictive(Current, band)` plus `cv_age_local_v` (Screens.cs:934-935; AgeGate.cs:30). `Needed` (Screens.cs:861-863) isn't re-asked when the band is local-final and `!ServerConfigured`. That holds in release before boot (IBackendService.cs:96-100, Current null → false). `OnServerBand` also never relaxes. The under-13 block still persists (Screens.cs:933, GameBootstrap.cs:43). Tests: Tests.cs:1081 and PlayMode `Offline_age_band_is_final_across_relaunch` (:207, a RestartFirstRun relaunch) and `Offline_band_is_never_relaxed` (:222). All 3 PlayMode tests passed in run 40113362848. |
| **Signing secrets vs PR/fork runs** | **OK in the workflow code.** There's no `pull_request_target`, `workflow_run` or `issue_comment` in any workflow. android-aab.yml: the ANDROID_* secrets are only in `secrets-check` (:27) and `sign` (:136), and both need `workflow_dispatch` and `refs/heads/main`. The PR `aab` job (:51) is same-repo only, unsigned, and has Unity secrets only. android-apk.yml: DEV_* are only in `sign` (:161, `event != pull_request`, main or dev/*). The PR `build`/`build-release` jobs (:83,:252) skip fork PRs. unity-tests.yml: editmode skips fork PRs (:22). server-ci: no secrets. build-mixer: dispatch or a push to audio/library-v1 only. Fork PRs need approval for all external contributors (verified via the API), and they get no secrets. |
| **C-55-1 (condition, before the ANDROID_* secrets)** | All secrets are **repo-level** (DEV_*, UNITY_*). The environments `unity`, `dev-signing` and `dev-builds` have **no protection rules or branch policy**, and `release` **doesn't exist**. A dispatch would auto-create it unprotected. So anyone with write access can read repo secrets from any branch or same-repo PR by editing a workflow. Fix (free on a public repo): create `release` with the deployment branch policy `main` only plus a required reviewer, and add ANDROID_* **only as `release` environment secrets**. Move DEV_* into `dev-signing` (branches main and `dev/*`). |
| M-1 | **Fixed.** `-crashverseSignAfter` means Unity gets no keystore or password; BuildScript refuses it if one is passed. The AAB is signed with `jarsigner -storepass:env/-keypass:env` in `$RUNNER_TEMP` and pinned to Tools/ci/upload_cert_sha256.txt, with exactly one signer. The dev APK uses `apksigner --ks-pass env:`. |
| M-3 | **Fixed.** The unsigned and signed AAB artifacts both have `retention-days: 1` (android-aab.yml:130,179). |
| L-55-6 | **Fixed.** unity-tests.yml:22 skips fork PRs. The gate (:137-140) fails them with a clear message. |
| CI | All 4 workflows passed on 603ccc0 (pull_request, 2026-10-24 ~22:20 IST): AAB 40113362825 (unsigned PR check), APK 40113362834 (dev + release-profile builds, smoke 34×3/36), Unity tests 40113362848 (273/273, 6 PlayMode), server-ci 40113362850. The sign, secrets-check and publish jobs were skipped, as expected. |
