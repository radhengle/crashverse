# CRASHVERSE Security Review v1

- Scope: local checkout `/workspace/crashverse`, commit `0d60e07` (private repo radhengle/crashverse). Nothing was committed or pushed.
- Inputs: the source code, `QA_NOTES.md` and `qa/`. SEC-01 and BUG-03/13/16/17 are treated as fixed and were not reopened.
- Already known before this review: the server trusts client stats and the mastery flag within bounds; the time host is a placeholder; the signing key sits in a local file; premium grant and IAP are stubs. These appear below with ranks and fixes.
- Secret values are never quoted here. They are referenced by location only.
- Line numbers refer to `0d60e07`. SB = `Assets/_Project/Scripts/Backend/LocalBackendService.cs`.

## 1. Threat model

**Assets:** ranked Daily Titan results and the leaderboards (daily, weekly, season, campaign); season XP, the premium track and the wallet (gems, extra charges); purchase entitlements; accounts and linked identities (Google, Facebook, X, Email, guest); PII (email, display name, device IDs); the per-run HMAC keys and any future server keys; the Play signing keystore; the Unity licence and credentials held in GitHub secrets.

**Trust boundaries:**
1. Device to backend. Everything on the device is attacker-controlled: memory, files, clock, network and the APK itself.
2. Backend to Google Play (Developer API, Play Integrity) and to the identity providers.
3. CI (GitHub Actions and GameCI) to the signed artefact.
4. Content files under `Resources/Data`. These ship in the APK and so are readable and patchable.

**Attackers:** casual cheaters (save editors, clock changers, GameGuardian); skilled modders (IL2CPP dump with Il2CppDumper, Frida, repackaged APKs); leaderboard botters who send forged API calls; account thieves (credential stuffing, linking abuse); refund and receipt fraudsters; supply-chain attackers (malicious actions or packages, leaked CI secrets); curious insiders.

**Threats in scope:**
- Score and ranked-run tampering.
- Clock manipulation for Daily Titan, daily rewards and seasons.
- Save and DB editing.
- Memory editing and speedhacks.
- Replayed or forged API calls.
- Account takeover and abuse of account linking or merging.
- IAP receipt fraud and refund abuse.
- Leaked secrets.
- Privacy: Play policy, GDPR, India's DPDP Act 2023.
- Supply chain and CI.

## 2. Code review findings

**Counts:** Critical 2, High 4, Medium 5, Low 4.

### Critical

**C1. The "server" DB, including the per-run HMAC keys, is a plaintext file on the device.**
- Where: SB:29 (`key` field), SB:67 (path `local_server_db.json` in `persistentDataPath`), SB:106 (`Save` writes plain JSON), SB:230-231 (key generated and stored).
- Impact:
  - Anyone with file access (root, backup tools, emulator) can read a run's key and forge valid receipts at SB:239-243. This defeats SEC-01.
  - They can also directly edit the wallet, `premium`, `seasonXp`, boards and `titanClearDays`.
  - The leaderboard is local today. If any of this logic is copied into a client that syncs with a backend, it is fully forgeable.
- Fix:
  - Treat `LocalBackendService` as dev-only. Exclude it from release builds with `#if DEVELOPMENT_BUILD` or an asmdef define, plus a build-time check.
  - The real backend keeps keys, run state and wallet server-side only (section 3).
  - Release builds must fail closed if `BackendLocator.Current` is local.

**C2. Premium is granted with no purchase proof, and IAP is not wired.**
- Where: SB:465 `GrantPremiumPassStub()` sets `premium = true` unconditionally. SB:473-475 then lets premium tiers be claimed. The store button is a stub at `UI/Screens.cs:547`.
- Impact: any client path or modder that reaches this call unlocks paid content. When Play Billing gets wired, there is a high risk of a client-side grant on `OnPurchaseComplete`.
- Fix:
  - Remove the stub from release builds.
  - Entitlements may only be granted by the backend after server-side validation via the Play Developer API (section 5).

### High

**H1. Campaign score is computed from client-reported stats and the mastery flag.**
- Where: SB:536-546 (comment at 537 admits the residual risk); model at `Backend/BackendModels.cs:45` (`destroyedRatio`, `shotsLeft`, `bestCombo`, `mastery`).
- Impact: a forged report at the bounds' maximum tops the campaign all-time board and collects first-clear rewards (SB:550-551).
- Fix:
  - The server issues a run token per level attempt, carrying a seed and the level version.
  - The client submits the input log (aim vectors, power, timing, power-ups).
  - The server either replays the run deterministically (headless Unity or a fixed-step sim) or samples replays and flags outliers.
  - Interim measures: per-level score ceilings derived from design data, a minimum run duration, and shadow-banning of outliers.

**H2. A ranked Titan run is plausible but not proven.**
- Where: SB:255-264 `CheckStep`, used at SB:276 and SB:310 with `offlineShotDamageCap` (5000 per shot).
- Impact: a bot that reports cap-sized steps every shot gets a near-maximal score that passes every check.
- Fix:
  - Replay validation of the per-shot inputs against the server seed for the top-N and for any score above the 99th percentile.
  - A server-measured minimum time between shots.
  - Play Integrity verdict required to start a ranked run.
  - Statistical anomaly detection per player.

**H3. The Daily Titan override is read from a writable device file.**
- Where: SB:197-206 load `daily_titan_override_{day}.json` from `persistentDataPath`.
- Impact: a user drops a file to change the shot count, arena or rules for their ranked run. The ticket's `shotBudget` comes from this config (SB:228-231).
- Fix: overrides come only from the backend (signed config or server-side). Delete the local file path in release builds.

**H4. Account linking is faked: no provider token is verified.**
- Where: SB:126-136 `LinkAccount(provider)` marks the account linked from a provider string alone.
- Impact: if this is mirrored server-side, anyone can claim any identity or merge into another account.
- Fix: see section 4. The server verifies each provider token, and linking requires proof of both identities.

### Medium

**M1. Device clock and test-clock hooks ship in the runtime class.**
- Where: SB:78-88 (`testFixedUnixMs`, `testOffsetMs`, `SetTestClock`). These values persist in the editable DB, so editing them shifts "server" time for Daily Titan, daily rewards and seasons.
- Fix: compile them out of release builds. The real server uses only its own clock.

**M2. Time comes from a third-party placeholder.**
- Where: SB:56 (`DefaultTimeUrl` = a Google 204 endpoint) and SB:66; `Data/Clock.cs:47-52` reads the HTTP `Date` header.
- Notes: the design is good (monotonic anchor, fails closed). But the source is an unowned host, and a user-installed root CA plus a proxy can spoof it. There is also no sanity bound on the offset.
- Fix: take time from your own backend over TLS, preferably inside an authenticated response. Pin the certificate or public key for the API host. The server stamps every run itself.

**M3. Campaign save is unencrypted and unauthenticated.**
- Where: `Meta/CampaignSave.cs:32-53` (stars, clears, `mastery`, `bestScore`) at line 12.
- Impact: local unlocks can be edited. Low impact while unlocks are offline-only, but these values must never feed boards or rewards.
- Fix: the server is the source of truth for anything competitive or monetised. Optionally add an HMAC with a device-bound Android Keystore key as tamper evidence (this deters casual edits only).

**M4. Guest ID is short and locally generated.**
- Where: SB:71 (`guest_` plus 8 hex characters, about 32 bits).
- Fix: the server issues an opaque 128-bit ID and a refresh token stored in the Android Keystore (EncryptedSharedPreferences).

**M5. CI hardening.**
- Where: `.github/workflows/android-apk.yml`.
  - Third-party actions are pinned by tag, not commit SHA: lines 19, 25, 33, 46.
  - `allowDirtyBuild: true` at line 42.
  - No `permissions:` block, so `GITHUB_TOKEN` gets the default scope.
  - The Unity licence, email and password are passed as `secrets.UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` (lines 35-37) on every push to `main`.
  - The cache key restores across branches.
- Fix: see section 6.

### Low

- **L1.** `FixedTimeEquals` returns early on length mismatch (SB:247-252). This is acceptable because receipts have a fixed length. On the server, use `CryptographicOperations.FixedTimeEquals` on the bytes.
- **L2.** Request signing is described as an FNV checksum (QA_NOTES.md:63). FNV is not a MAC. Do not ship it as a security control.
- **L3.** `Core/TitanRunJournal.cs:15-21` keeps a plaintext journal. This is by design (the server re-verifies), but clear it after finalising and keep it out of backups.
- **L4.** Android settings: minSdk 24 (`ProjectSettings/ProjectSettings.asset:24`); no `allowBackup` override or release minification was found. See section 8.

## 3. Server-authoritative backend plan

1. **Identity on every call.** A short-lived access JWT (about 15 minutes) plus a rotating refresh token. Every request carries a nonce and timestamp, and the server rejects replays (nonce cache, ±60 s window).

2. **Time.** Server time is the only time. Day keys, resets, the grace window (SB:333) and season indices are computed server-side. The client shows server time only.

3. **Titan start.** `POST /titan/start` checks the once-per-day attempt server-side and requires a Play Integrity verdict (`MEETS_DEVICE_INTEGRITY` and app recognised). It returns:
   - `runId`
   - the server-chosen config, with no client overrides
   - an RNG seed
   - a server timestamp
   - a run token signed with the server-only key (Ed25519 or HMAC in KMS) and bound to player, day and config hash.

4. **Per shot.** The client sends `{runId, shotIndex, inputs, clientDamage}`. The server:
   - checks order, budget and minimum interval
   - keeps the authoritative state
   - returns a receipt.
   The offline tail is accepted only under the existing caps and is flagged for replay.

5. **Validation.** A deterministic fixed-timestep simulation with a pinned physics config and a server replay worker (headless Unity build). Replay every top-100 score and every outlier, plus a random sample. Mismatches go to a shadow board and review.

6. **Campaign.** The same pattern: per-attempt token, input log, server-computed score. Stop accepting `destroyedRatio`, `bestCombo` and `mastery` as-is.

7. **Leaderboards.** Server-written only. Scores go to a pending state until validated. Add per-account and per-device caps, shadow-banning, and a moderation API.

8. **Keys.** Signing and HMAC keys live in a cloud KMS or secret manager, never in the APK or a client-readable file. Rotate them, keep a key ID in each token, and give each service a separate key.

9. **Rate limits.** Per account, IP and device: start at most 1 per day per mode; shots capped per run; auth endpoints 5 per minute with backoff; purchase verification idempotent per `purchaseToken`. Add a WAF in front.

10. **Integrity.** Use the Play Integrity API (Standard requests on hot paths, Classic for ranked start), with nonce binding to `runId`. Enforcement is graduated: soft-flag or practice-only, then block.

## 4. Auth and account linking

- **Google.** Use Credential Manager or Google Sign-In to get an ID token. The server verifies signature, `aud`, `iss` and `exp`, and keys the account on `sub`, not email.
- **Facebook.** The server verifies the access token via `debug_token` with the app ID, then uses the app-scoped user ID.
- **X.** Use OAuth 2.0 with PKCE. Exchange the code on the server and fetch the user ID there. Never put the client secret in the app.
- **Email.** Passwordless magic link or OTP, or a password stored with Argon2id. Require email verification. Add rate limits, breached-password checks and a generic "if the account exists" response.
- **Guest.** A server-issued opaque ID and refresh token in Keystore-backed storage. Warn the user that uninstalling loses progress.
- **Linking.** Requires a valid session on the current account plus a fresh provider token. One provider identity maps to at most one account.
- **Conflicts.** If the identity already belongs to another account, show an explicit choice of which account to keep, with no silent merge. Write an audit log. Re-authenticate before unlinking, and never unlink the last method.
- **Sessions.** Revoke them on unlink or password change. Detect refresh-token reuse.
- **Display names.** Names on boards (SB:131-133) go through a profanity filter, length limits and Unicode normalisation.

## 5. IAP receipt validation (Google Play Billing)

1. Use Play Billing Library 6+ through Unity IAP. Set `obfuscatedAccountId` to an HMAC of the player ID.
2. On purchase, the client sends `purchaseToken`, `productId` and `orderId` to the backend. The client grants nothing.
3. The server calls `purchases.products.get` (or `purchases.subscriptionsv2.get`) with a service-account credential held only server-side. It checks:
   - `purchaseState == PURCHASED`
   - package name and product ID
   - `obfuscatedAccountId` matches the caller
   - the token has not been used before (unique constraint).
4. Grant the entitlement in a database transaction. Then acknowledge (`acknowledge`, or consume for gem packs) within 3 days.
5. Subscribe to Real-time Developer Notifications (Pub/Sub). Poll the Voided Purchases API and revoke or claw back on refunds and chargebacks.
6. Restore purchases from server records. Log `orderId` for support.
7. Re-run `StoreValidator` (cosmetics only, `Meta/StoreValidator.cs:13-42`) on the server catalogue so the store stays cosmetics-only.

## 6. Secrets and CI

- No hardcoded API keys or credentials were found in tracked C# or JSON. The only key material is the per-run HMAC key generated at runtime and stored in the local DB (C1).
- **Unity licence.** Keep `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` as GitHub Environment secrets on a protected `release` environment, with required reviewers. Use a dedicated Unity account with 2FA where GameCI supports it, or a Unity Build Server licence. Never echo secrets in logs, and rotate the password if it was ever shared in chat or docs.
- **Triggers.** Do not trigger secret-using jobs from `pull_request_target` or from forks.
- **Workflow file.** Add `permissions: contents: read`. Pin `actions/checkout`, `actions/cache`, `game-ci/unity-builder` and `actions/upload-artifact` to full commit SHAs and let Dependabot bump them. Set `allowDirtyBuild: false`.
- **Signing.** Use Play App Signing. Keep the upload keystore and its passwords only in a protected environment's secrets, decoded to a temp file at build time. Never commit the keystore (add `*.keystore` and `*.jks` to `.gitignore`).
- **Repo hygiene.** Enable secret scanning with push protection, Dependabot, branch protection on `main` and CODEOWNERS for `.github/`.
- **Dependencies.** Review `Packages/packages-lock.json` changes, and use only the Unity registry or verified scoped registries.
- **Artefacts.** Release builds are only signed in CI from tags. Keep the 14-day retention for debug APKs and do not upload release keystores as artefacts.

## 7. Privacy and Play policy

- **Data Safety form.** It must declare account identifiers, email, device or other IDs (Play Integrity, advertising ID if used), purchase history, gameplay data and crash logs, and whether data is encrypted in transit and can be deleted.
- **Account deletion.** Play requires both an in-app path and a web URL that deletes the account and its data, including leaderboard entries. Linked social IDs are removed as well.
- **Families policy.** If the target audience includes under-13s, Families policy applies. Restrict social login and display names, and use no personalised ads.
- **GDPR.** Set lawful bases (contract for gameplay, consent for analytics and marketing) with a consent UI for EEA and UK users. Support access, rectification and deletion within 30 days. Sign DPAs with processors and use SCCs for transfers.
- **India's DPDP Act 2023.**
  - Clear notice in English or the 22 scheduled languages on request.
  - Consent that is free, specific and withdrawable.
  - Verifiable parental consent for under-18s, with no behavioural tracking or targeted ads for children.
  - A grievance officer contact.
  - Breach notification to the Data Protection Board and affected users.
  - Erasure when the purpose is served.
- **Minimisation.** Request only the `openid`/`email` scopes from providers. Show public display names, not emails, on boards. Keep anti-cheat telemetry for a limited, defined period.

## 8. Android hardening

- **Build settings.** IL2CPP, ARM64, release builds with stripping (High managed stripping) and R8/minify for Java. Use an obfuscator for C# symbol names (for example Beebyte) where cost allows. These raise the effort needed but do not replace server authority.
- **Manifest.**
  - `android:allowBackup="false"` or `dataExtractionRules` that exclude the save, journal and tokens.
  - `usesCleartextTraffic="false"` with a network security config.
  - No exported components unless required.
  - Raise minSdk to 26+ if the audience allows.
- **Secure storage.** Store tokens in EncryptedSharedPreferences or the Android Keystore, never in PlayerPrefs (none were found in the code, which is good).
- **Networking.** TLS 1.2+ with certificate or public-key pinning for the API host, plus a backup pin.
- **Runtime checks.** Repackaging, debugger, root, emulator and Frida detection, used only as soft signals feeding server risk scoring. Play Integrity is the authoritative check.
- **Release.** Strip the test hooks (M1), the local backend (C1), the override path (H3) and the stubs (C2) from release builds, and verify with a CI grep that fails the build if any remain.

## Priority order before any public ranked or IAP launch

1. C1, C2: real backend plus server-side receipt validation.
2. H3, H4, H1, H2: config, linking and score authority.
3. M1-M5.
4. Lows.
