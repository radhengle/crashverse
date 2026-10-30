# Security Review: PR radhengle/crashverse#1 at commit `e243b15`

- **Reviewer:** Security Admin
- **Date:** 2026-10-16
- **Method:**
  - Read-only local `git fetch` of `refs/pull/1/head`, then extracted the commit to a temp folder.
  - Nothing was posted to GitHub, committed or pushed.
  - A local ref named `pr1` now exists in `/workspace/crashverse`. It is safe to delete.
- **Baseline documents:**
  - `security/Backend_Plan_v1_Review.md` (B1-B7, S1-S7)
  - `SECURITY_REVIEW_v1.md`
- **PM decisions taken into account:**
  - Age bands are `under13|13to15|16to17|adult`, with per-market consent held in a config table that needs legal review.
  - A guest can delete with their session alone. A linked account needs a fresh re-auth.
  - The run summary is accepted in dev only.
  - The ranked board stays off until replay exists, behind `RANKED_PUBLIC_BOARD_ENABLED`. It must default off and fail closed.

Paths below are relative to `server/src/Crashverse.Server/` unless stated otherwise.

## Verdict: REQUEST CHANGES

The PR is a strong v0. These parts are good:
- Server-owned state.
- Signed ES256 tokens with key ids (`kid`).
- HKDF receipts.
- Single-use attempt tokens.
- A shared nonce cache and rate limiter.
- Deny-all Firestore rules.
- Purchase verification with an account-binding check and a unique token.
- The S2 Integrity bar.
- Gem debt (S6).
- The client release-guard CI step.
- Pinned actions.

Four PM or blocker requirements are **not enforced in code**, though:
1. The ranked-board flag does not exist.
2. The run summary is accepted in every environment.
3. Age bands and minor limits are not enforced on the server.
4. The only startup path wires dev fakes, with no prod guard.

None of these is exploitable today, because nothing is deployed. All of them must be fixed before any non-local deployment.

## Blocker and condition status

### Blockers

**B1: release strip of client dev paths**
- **Status:** Met (pending a green CI run).
- **Evidence:** `Tools/ci/check_release_guards.py` checks `CRASHVERSE_DEV` guards, UnityConnect settings disabled and `AD_ID` removed. `android-apk.yml` runs it, and compiles with release defines.

**B2: deny-all rules; shared limits and nonces**
- **Status:** Mostly met.
- **Evidence:** `server/firestore.rules` denies everything. `Infra/Security.cs:160-163` implements a store-backed nonce cache and limiter.
- **Gaps:** M1 (lock lease) and M2 (client IP source).

**B3: no public ranked board until replay**
- **Status:** **Not met.**
- **Evidence:** `RANKED_PUBLIC_BOARD_ENABLED` appears nowhere. Titan scores are written to the board (`Domain/TitanService.cs:220-224`). Entries ranked below the top N are shown publicly as `provisional` (`Domain/Leaderboards.cs:86-98`). See H2.

**B4: admin and console edits audited**
- **Status:** Partly met.
- **Evidence:** The admin override is disabled while `AdminKey` is null (`Program.cs:250`). Console break-glass alerting is ops work, not checked here.

**B5: no client-side grant; Play verification**
- **Status:** Met in logic, **not met for prod**.
- **Evidence:** The logic at `Domain/PurchaseService.cs:46-75` checks state, account binding and token uniqueness. But `Program.Main` wires `FakePlayApi` (C1).

**B6: privacy items (deletion, age gate)**
- **Status:** **Not met.**
- **Evidence:**
  - The deletion request and the guest/linked re-auth split are correct (`Domain/AuthService.cs:340-349`).
  - There is no purge job, so `purgeAt` is never acted on (M3).
  - The age gate is not persisted or enforced (H3).

**B7: CI (workload identity, protected secrets, SHA pins)**
- **Status:** Partly met.
- **Evidence:**
  - Good: SHA pins, `permissions: contents: read`, and `server-ci.yml` uses no credentials.
  - Gap: the Unity secrets are still repository secrets on a job with no `environment:` (`.github/workflows/android-apk.yml:94-96`). See M4.
  - Workload Identity Federation is not applicable until deploy.

### Conditions

**S1: replay tolerances, 100% of top N, 5% sample**
- **Status:** Met in design.
- **Evidence:**
  - Tolerances live in `Domain/Models.cs` (`ServerOptions`).
  - Top-N entries are hidden until replayed (`Domain/Leaderboards.cs:86-91`).
  - Sampling plus 100% replay for high-risk and refund-abuse accounts (`Domain/Leaderboards.cs:126-130`).
  - The worker is a stub that returns null, so top-N entries stay hidden. This is correctly fail-closed.

**S2: Integrity bar**
- **Status:** Met.
- **Evidence:** `Domain/TitanService.cs:60-80` checks PLAY_RECOGNIZED, MEETS_DEVICE_INTEGRITY, package, certificate, request hash and a 5-minute age. LICENSED feeds risk only, and unranked play soft-fails.
- **Note:** `INTEGRITY_MODE=logOnly` (`Program.cs:277`) must be impossible in prod. See C1.

**S3: retention and deletion**
- **Status:** Not met.
- **Evidence:** There is no purge or anonymisation job, no 90-day input-log TTL and no guest-inactivity job (M3).

**S4: KMS signing and rotation**
- **Status:** Partly met.
- **Evidence:** `KeyRing` rotation with overlap and `kid` support (`Infra/Security.cs:30-60`). The only signer is `EcdsaDevSigner`, an in-process key, and there is no KMS signer yet (C1).

**S5: no paid WAF**
- **Status:** Partly met.
- **Evidence:** Shared limits, body caps and Kestrel limits are in place. The rate-limit key uses the socket IP, which is wrong behind the Cloud Run front end (M2).

**S6: refund gem debt**
- **Status:** Met.
- **Evidence:** `RefundPolicy` defaults to `gemDebt` (`Domain/Models.cs`, `Program.cs:276`).

**S7: workload identity, no key files**
- **Status:** N/A yet.
- **Evidence:** No credentials anywhere, which is good. Prod adapters are not written yet.

## Findings

**Counts:** Critical 1, High 3, Medium 4, Low 3.

### Critical

**C1. The only startup path wires dev fakes. Nothing stops them running in prod.**
- **Where:**
  - `Program.cs:279` (`ServerApp.Build(ServerApp.DevContext(opt))`)
  - `Program.cs:16-32` (`DevContext`)
  - `Infra/External.cs:17-25` (`FakeIntegrityVerifier` accepts any `fake:{json}` token)
  - `Infra/External.cs:148-151` (`FakePushAuthVerifier` accepts a constant token that is public in the repo)
  - `Infra/External.cs:134-144` (`FakePlayApi`)
- **Impact if this binary is deployed as is:**
  - Anyone passes the Integrity check.
  - Anyone can call `/internal/*` with the constant bearer token: grant parental consent, trigger replay or titan-close jobs, and inject RTDN messages.
  - Signing and HMAC keys are random per process, so tokens break on every restart or scale-out.
  - The in-memory store loses all data.
  - `INTEGRITY_MODE` and `REFUND_POLICY` are free-form environment variables.
- **This answers the PM's question:** yes, dev-only paths *can* run in prod, because they are the only paths.
- **Fix:**
  - Add `CRASHVERSE_ENV` with values `dev|test|prod`. **Missing or unknown means prod.**
  - In prod, `Main` builds a `ProdContext` that uses: Firestore (real), KMS signers, Secret Manager receipt masters, the real Play, Integrity, Google and Facebook adapters, and an OIDC push verifier that checks `aud` and the service-account email.
  - `ProdContext` must refuse to start if any `Fake*`, `Stub*` or `EcdsaDevSigner` type is resolved.
  - Move the fakes into a test-only assembly, or behind `#if DEV`.
  - Reject `INTEGRITY_MODE=logOnly` in prod unless it is explicitly allow-listed for closed testing.
  - Add a startup test asserting that a prod context with missing configuration throws.

### High

**H1. The run summary is accepted in every environment, not dev only.**
- **Where:**
  - `Domain/CampaignService.cs:111`: the score is computed from the client's `destroyedRatio`, `shotsLeft`, `bestCombo` and `mastery`.
  - The validation at lines 69-73 only bounds-checks those values.
  - `server/README.md:115` admits this.
- **Impact:** review finding H1 stays open. Forged at-ceiling scores are accepted and first-clear rewards are paid, up to the daily cap.
- **Fix:**
  - Accept the summary only when `env == dev`, or when `ALLOW_RUN_SUMMARY=true` *and* `env != prod`.
  - In prod, a submit carrying summary fields must either ignore them (score stays pending, no board entry, no reward until replay) or be rejected.
  - Add a test that prod configuration with summary fields gives `validation=pending` and no grant.

**H2. `RANKED_PUBLIC_BOARD_ENABLED` is not implemented, and the Titan board is publicly visible.**
- **Where:**
  - `Domain/TitanService.cs:220-224` submits to `titan:{day}`.
  - `Domain/Leaderboards.cs:86-98` hides only pending entries inside the top N. Entries ranked below 100 are listed to everyone with `provisional=true`, and a player's own entry is always shown.
- **Fix:**
  - Read `RANKED_PUBLIC_BOARD_ENABLED` with **strict parsing**: only the exact string `true` enables it. Missing, empty, `1`, `TRUE` or garbage all mean off.
  - Enabling is allowed only if a real `IReplayWorker` is configured (not `StubReplayWorker`) **and** `TopNReplayCoverage == 100%`. Otherwise refuse and log.
  - In prod the flag comes from deploy config, not a request, and enabling it needs the protected-environment approval.
  - When it is off:
    - `GET /leaderboards/0` returns only the caller's own entry, marked provisional, or a `RANKED_BOARD_DISABLED` status.
    - Weekly and season aggregates exclude Titan scores.
    - Top-10% bonuses wait in `CloseDay`.
  - Add tests for missing, invalid and `true` values with no worker. All three must be off.

**H3. The age gate is not enforced on the server, and the bands don't match the PM decision.**
- **Where:**
  - `Domain/ComplianceService.cs:34` returns `under13|13to15|16plus`. The PM decision is `under13|13to15|16to17|adult`.
  - `Domain/ComplianceService.cs:13-19` hardcodes consent ages in a static dictionary in code, not a config table. An unknown country defaults to 13.
  - The result is never stored. `AccountDoc.ageGate` exists in `Domain/Models.cs` but is never written.
  - `Domain/AuthService.cs:63-67` (`Guest`) creates accounts without any age-gate proof, so the under-13 block is client-only.
  - There are no minor checks on `/account/link`, `/profile` display name, `/purchases/google/verify` or `/store/buy`.
  - Consent can only be decided through `/internal/consent/decide`, because the parent page is not built (`Domain/ComplianceService.cs:61`).
- **Fix:**
  - Return the four bands.
  - Move consent ages to a server config document (`config/ageConsent`) with `legalReviewed:false`. While it is unreviewed, use the most restrictive default (treat unknown countries as 18).
  - `/agegate` returns a short-lived signed `ageToken` holding the band, country and consent requirement. `/auth/guest` and `/auth/sign-in` require it, refuse `under13`, and store `ageGate` without the date of birth.
  - Enforce E5 and E6 on the server: return 403 `AGE_RESTRICTED` or `CONSENT_REQUIRED`, never just hide things in the client.
  - Keep the most restrictive band per account and install.

### Medium

**M1. The Firestore lock lease has no expiry.**
- **Where:** `Infra/Store.cs:144-159`. A crash between creating and deleting the lease leaves the key locked forever, which permanently blocks that player's wallet or run.
- **Fix:** store `expiresAt` in the lease, take over stale leases transactionally, and set a Firestore TTL on `_locks`. Better still, use Firestore transactions.

**M2. Rate-limit keys use the socket IP.**
- **Where:** `Program.cs:58`. Behind Cloud Run or Google's front end, the socket address is the proxy, so all users would share one bucket.
- **Fix:** use the right-most trusted `X-Forwarded-For` hop, and only trust it from Google's front end.

**M3. There is no deletion purge, retention TTL or guest-inactivity job.**
- **Where:** `Domain/AuthService.cs:348` sets `purgeAtUnixMs`, but nothing purges. There is also no 90-day input-log TTL and no 335/365-day guest job.
- **Fix:** add `/internal/jobs/purge` and `/internal/jobs/guest-inactivity` per `Deletion_AgeGate_Requirements.md` D and C2, add Firestore TTL fields, and add an emulator test for D2-D7.

**M4. Unity licence secrets are not in a protected environment.**
- **Where:** `.github/workflows/android-apk.yml:94-96`.
- **Fix:** add `environment: unity-build` with required reviewers, move the secrets there, and restrict the release profile to `main`.

### Low

**L1.** The `X-Admin-Key` comparison is not constant-time (`Program.cs:250`). Use `CryptographicOperations.FixedTimeEquals`, or drop the key and rely on IAM plus OIDC.

**L2.** `/internal/rtdn` reads the raw body without the `Body<T>` size cap (`Program.cs:242`). Kestrel's 256 KB limit applies, but use the 64 KB helper and catch `JsonException`.

**L3.** The client `IBackendService` documentation still describes the old bands (`Assets/_Project/Scripts/Backend/IBackendService.cs:39`, `ServerContracts.cs:67`). Align it with the four bands.

**Checked and found correct:**
- **Guest and linked deletion** (`Domain/AuthService.cs:340-349`): a guest may pass a null re-auth token. A linked account must present a fresh re-auth token, which is single-use (nonce-burned) and lasts 5 minutes (`Domain/AuthService.cs:332-337`). Guest re-auth requires the current refresh secret. This matches the PM decision.
- **Purchase checks:** account binding and token uniqueness (`Domain/PurchaseService.cs:56`, `:71`).

## Ready-to-post PR review comment

> Not posted. The parent agent or the user must post it. Suggested review type: **Request changes**.

---

**Security review: request changes (commit e243b15)**

Thanks, this is a solid v0. These parts look good:
- Server-owned state, ES256 tokens with `kid`, and HKDF receipts.
- Single-use attempt tokens, and a shared nonce cache and limiter.
- Deny-all Firestore rules.
- Play verification with account binding and token uniqueness.
- The S2 Integrity bar and gem debt (S6).
- The release-guard CI step and SHA-pinned actions.
- The guest/linked deletion re-auth split matches the PM decision.

Four items must be fixed before anything is deployed beyond localhost:

1. **Critical: prod can run dev fakes** (`Program.cs:279`, `Program.cs:16-32`, `Infra/External.cs:17-25,148-151`).
   - `Main` always builds `DevContext`, so the fake Integrity verifier accepts `fake:` tokens, `/internal/*` accepts a constant bearer token, keys are per-process, and the store is in memory.
   - Please add `CRASHVERSE_ENV`, where missing means prod, and a `ProdContext` that refuses to start with any `Fake*`, `Stub*` or `EcdsaDevSigner`.
   - Block `INTEGRITY_MODE=logOnly` in prod.
2. **High: run summary accepted everywhere** (`Domain/CampaignService.cs:111`). Gate it to dev. In prod, the score stays pending with no board entry and no reward until replay.
3. **High: `RANKED_PUBLIC_BOARD_ENABLED` is missing** (`Domain/TitanService.cs:220-224`, `Domain/Leaderboards.cs:86-98`).
   - Entries ranked below 100 are publicly visible today.
   - Add the flag with strict parsing (only `true` enables; missing or invalid means off).
   - Refuse to enable it unless a real replay worker is configured and top-N coverage is 100%.
   - Add tests for missing, invalid, and `true` with no worker.
4. **High: age gate not enforced on the server** (`Domain/ComplianceService.cs:13-19,34`, `Domain/AuthService.cs:63-67`).
   - Use the bands `under13|13to15|16to17|adult`.
   - Move consent ages to a config document marked as needing legal review, with an 18 default while unreviewed.
   - Require a signed age token at account creation, refuse under-13s, store the band, and return 403 for minor limits on link, name, store and purchases.

Also requested (medium):
- Lease TTL on Firestore locks (`Infra/Store.cs:144-159`).
- Trusted `X-Forwarded-For` for rate-limit keys (`Program.cs:58`).
- Purge, retention and guest-inactivity jobs (`Domain/AuthService.cs:348`).
- A protected environment for the Unity secrets (`.github/workflows/android-apk.yml:94-96`).

Low: a constant-time admin key comparison, a body cap on `/internal/rtdn`, and the band docs in `IBackendService.cs:39`.

Full report: `security/PR1_Security_Review.md`.

---

---

# Re-review f3fc100

- **Date:** 2026-10-16
- **Head:** `f3fc100` (PR head, fetched read-only). The diff from `e243b15` touches 30 files: +1,569 / -103.
- **Method:**
  - Every Critical, High and Medium fix was verified in the code itself. The finding-to-fix map in PR comment 6209080875 was not relied on.
  - No .NET SDK is on the review box, so **the tests were read but not run**. A green `server-ci` run on `f3fc100` is a condition of approval.
- Nothing was posted, committed or pushed.

## Verdict: APPROVE WITH CONDITIONS

- C1, H1, H2, H3 and M1-M3 are fixed in code, with tests.
- M4 is fixed in the workflow but needs action from the owner.
- There are no new Critical or High findings.
- The remaining items are Medium or Low, plus the owner and legal setup steps listed below.

## Status of the original findings

**C1: prod must refuse dev wiring — fixed.**
- `CRASHVERSE_ENV` is missing or unknown, so the server runs as prod (`Infra/Environments.cs:16-19`).
- `Main` selects `ProdContext` (`Program.cs:~378`).
- `DevContext` throws in prod (`Program.cs:18`).
- `ProdGuard.Validate` (`Infra/Environments.cs:29-52`) refuses to start if:
  - any component is `[DevOnly]`, `Fake*`, `Stub*` or `InMemory*` (this covers the fake push verifier with its shared secret, the in-memory store, the stub replay worker, and dev signers through the per-ring signer check);
  - the receipt master is ephemeral (`:45`);
  - `INTEGRITY_MODE` is anything other than `enforce` (`:46`);
  - `ALLOW_CLIENT_RUN_SUMMARY` is set (`:48`);
  - an admin key is set (`:49`).
- The OIDC push verifier `GoogleOidcPushAuthVerifier` is wired from config.
- Test: `Prod_RefusesDevContext_AndFakesAndLogOnly`.
- **Note:** prod cannot start at all until the real store, KMS, Secret Manager, Play, Integrity, email and replay adapters exist. That is the intended fail-closed behaviour.

**Token flavor — fixed.**
- `flv` is set from server options only (`Domain/Models.cs:12`; `Infra/Security.cs:86`).
- Prod rejects any token that is not prod flavor (`Infra/Security.cs:108`).
- Test: `ProdServer_RejectsTestFlavorAttemptToken_OnScore`.

**H1: run summary — fixed (dev only).**
- `AllowClientRunSummary` is true only when `ASPNETCORE_ENVIRONMENT=Development` **and** `CRASHVERSE_ENV` is not prod (`Domain/Models.cs:62-64,72`). ProdGuard also rejects it.
- Without the summary, `ServerSummary` (`Domain/CampaignService.cs:105`, `:150-170`) ignores the client stats. It stores the inputs, queues a replay and returns `pending`. Best score, board and rewards stay unchanged until the replay returns.
- Tests: `WithoutClientSummary_*` (2).

**H2: `RANKED_PUBLIC_BOARD_ENABLED` — fixed.**
- Parsing is strict: only the exact string `"true"` enables it (`Domain/Models.cs:65`). The default is false.
- `RankedBlockReason` (`Domain/Leaderboards.cs:70-84`) keeps the board closed unless three things hold: the flag is on, `Replay.Configured` is true, and 100% of the top N are validated. The stub worker reports `Configured => false` (`Infra/External.cs:197`).
- While blocked, the response contains only the caller's own entry with no rank (`Domain/Leaderboards.cs:94-107`).
- Provisional entries are now hidden from others at **every** rank (`Domain/Leaderboards.cs:120-124`).
- Titan XP is left out of season board XP while the board is closed (`Domain/SeasonService.cs:68`).
- Tests: `RankedBoard_*` (3).

**H3: age gate — fixed.**
- Four bands (`Domain/ComplianceService.cs:66`).
- The consent table lives in `server/config/consent_ages.json`, marked `"NEEDS LEGAL REVIEW"` with `legalReviewed:false`. Unknown markets get 18 while it is unreviewed (`Domain/ComplianceService.cs:12-13`). Bad files fail startup (`:24-27`).
- `/agegate` issues a signed age token, and none for under-13s (`:69-70`).
- Guest and sign-in require that token and refuse under-13s (`Domain/AuthService.cs:63-65`, `:197-199`; `Domain/ComplianceService.cs:79-85`).
- The band and country are stored without the date of birth. The most restrictive band is kept per account and per install (`Domain/ComplianceService.cs:88-100`).
- `Enforce` returns 403 on:
  - link (`Program.cs:222`, `Domain/AuthService.cs:148`)
  - email register (`Program.cs:224`)
  - name change (`Domain/AuthService.cs:383`)
  - friends (`Program.cs:305-306`)
  - store, premium unlock and purchase verification (`Program.cs:265`, `:274`, `:315`).
- Tests: `AgeGate_*`, `Minor_LimitsEnforcedOnServer`, `ConsentTable_*` (2).

**M1: lock lease — fixed.**
- A transactional lease with an owner token and `expireAt` (TTL 15 s). Expired leases are taken over, and only the owner's own lease is released (`Infra/Store.cs:167-198`).
- An emulator test was added.
- See residual N3.

**M2: forwarded IP — fixed.**
- `X-Forwarded-For` is used only when `TRUST_X_FORWARDED_FOR=true`.
- The address is taken counting `TrustedProxyHops` (1-5, default 1) from the **right**, so entries a client prepends are ignored. The value must parse as an IP, otherwise the socket address is used (`Program.cs:104-113`).
- It cannot be spoofed when the hop count matches the real proxy chain. If the count is wrong, every client lands in one rate-limit bucket. This is an owner item.
- Test: `ClientIp_HonoursXffOnlyWhenTrusted`.

**M3: purge, retention and guest jobs — fixed.**
- The jobs are `/internal/jobs/purge`, `/internal/jobs/retention` and `/internal/jobs/guest-inactivity`, all behind OIDC (`Program.cs`, internal section).
- `DataLifecycleService`:
  - Purge deletes the player's rows and board entries.
  - Top-100 runs are kept but detached under a tombstone ID.
  - Non-purchase ledger rows are folded into daily totals, then deleted.
  - Purchases are pseudonymised, with `TaxRetentionDays` (default 8 years, needs legal review).
  - PII is stripped from audit rows.
  - An alert fires for deletions overdue past `DeletionAlertDays` (`:20-97`).
  - Retention clears input logs after 90 days and expires purchase rows (`:110-128`).
  - Guests are warned at day 335 and deleted at day 365; activity resets the clock (`:137-155`).
- The web deletion endpoints have captcha, rate limits and generic answers.
- Tests: `Purge_*` (2), `Retention_*`, `GuestInactivity_*`, `WebDeletion_*` (2).

**M4: Unity secrets environment — fixed in code; owner action needed.**
- The build job now declares `environment: unity` (`.github/workflows/android-apk.yml:60`).
- The owner must create the environment, add reviewers, move the three secrets into it, and delete the repository-level copies.

**Lows:**
- L1 is fixed: `FixedTimeEquals` (`Program.cs`, admin endpoint).
- L2 is fixed: the RTDN body uses the 64 KB `Body<T>` helper.
- L3 is fixed: `IBackendService.cs` and `ServerContracts.cs` are updated.

## New findings introduced by, or visible after, the fixes

**N1 (Medium): prod safety rests on a single environment variable.**
- Deploying with `CRASHVERSE_ENV=dev` (or `test`) runs `DevContext` in the cloud, with fake Integrity, the public push token `fake-pubsub-oidc`, fake Play, and in-process keys (`Program.cs:18-33`; `Infra/External.cs` `FakePushAuthVerifier`).
- **Fix:** `DevContext` should also refuse when it detects Cloud Run (the `K_SERVICE` or `K_REVISION` variable), or whenever it is bound to a non-loopback address. Add a test.

**N2 (Medium, latent while IAP is off): the obfuscation pepper is random per process.**
- `ObfuscationPepper` is set from `RandomNumberGenerator.GetBytes(32)` at startup (`Domain/Models.cs:74`), and ProdGuard does not check it.
- Every restart or new instance changes `obfuscatedAccountId`, which breaks the purchase-to-account binding (`Domain/PurchaseService.cs:56`).
- **Fix:** load the pepper and its version from Secret Manager in `ProdAdapters`, and have ProdGuard refuse a random pepper. It must never rotate; version it instead.

**N3 (Low): the lease has no fencing.**
- If a lock body runs longer than 15 s (a GC pause or a slow Firestore call), a second holder can take over (`Infra/Store.cs:168`).
- **Fix:** renew the lease while the body runs, or check the owner token inside the write transaction. Better still, move wallet and run writes to real Firestore transactions.

**N4 (Low): age tokens can be reused.**
- A token lasts 30 minutes, can be used more than once, and is not bound to the install (`Domain/ComplianceService.cs:69`).
- **Fix:** bind it to `deviceInstallId`, add a `jti` burned at account creation, and cut the lifetime to about 10 minutes.

**N5 (Low): accounts with no age record skip enforcement.**
- `Enforce` returns early when `ageGate` is null (`Domain/ComplianceService.cs:107`). Only accounts created before this change are affected, because creation now requires the token. They rely on the client re-gating them.
- **Fix:** force those accounts to re-gate on the server: return `AGE_GATE_REQUIRED` on restricted scopes until a band is stored.

**N6 (Info): unreviewed table values still apply to listed markets.**
- Markets in the table use the draft values (for example US 13, DK 13) even while `legalReviewed:false`. Only unknown markets get 18. This matches the PM decision, but Legal must sign the table off before launch.

## Blocker and condition status at f3fc100

### Blockers
- **B1:** Met (pending a green CI run).
- **B2:** Met. Lease and forwarded-IP gaps are fixed; residual N3.
- **B3:** **Met.** The board fails closed on the flag, the worker check and 100% top-N coverage. Provisional entries are hidden.
- **B4:** Met. The admin key is refused in prod. Console alerting is an owner and ops item.
- **B5:** Met for v1. IAP is behind `IAP_ENABLED`, off by default, and the logic was verified earlier. N2 must be fixed before IAP is enabled.
- **B6:** Met in code: deletion (app and web), purge, retention, guest inactivity, age gate and minor limits. Policy text and legal review are still open.
- **B7:** Met in the workflow. The owner must create the `unity` environment and move the secrets.

### Conditions
- **S1:** Met (no change).
- **S2:** Met. `logOnly` is now refused in prod.
- **S3:** **Met in code.** The tax period is a placeholder pending legal review.
- **S4:** Partly met. The ring and `kid` design and the flavor check are in place, but there is no KMS signer yet, and prod refuses to start without one.
- **S5:** Met. The forwarded IP is handled correctly; the hop count must be verified at deploy.
- **S6:** Met.
- **S7:** Pending deployment. OIDC verification for internal endpoints is now implemented. Workload Identity Federation and the Play access setup belong to the owner.

## Conditions of approval
1. `server-ci` is green on `f3fc100`. The tests were not run in this review.
2. Fix N1 and N2 before the first cloud deployment. N2 can wait for IAP if IAP stays off.
3. Complete the owner and legal items below before prod.

## Owner-only items
- Set `CRASHVERSE_ENV=prod` on every cloud deployment, and never `dev` or `test`. N1 adds a code guard on top of this.
- Create the GitHub environment `unity` with required reviewers, restricted to `main` and tags. Move `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` into it and delete the repository-level secrets.
- Create the `production` environment and the Workload Identity Federation pool, limited to `radhengle/crashverse`, `main` and `production`.
- On GCP:
  - set the org policy that disables service-account key creation;
  - grant the Cloud Run service account least-privilege access in Play Console API access;
  - enable the Play Integrity API and request a quota increase.
- Provision the real adapters. Until they exist, prod refuses to start, by design:
  - Firestore store
  - KMS signers (ES256)
  - Secret Manager receipt master and obfuscation pepper
  - Play and Integrity
  - email and consent mailer
  - captcha
  - replay worker.
- Configure the Pub/Sub and Scheduler OIDC audience and service accounts (`INTERNAL_OIDC_AUDIENCE`, `INTERNAL_OIDC_SERVICE_ACCOUNTS`).
- Schedule the purge, retention, guest-inactivity, replay and titan-close jobs.
- Set a Firestore TTL policy on `_locks.expireAt`.
- Set `TRUST_X_FORWARDED_FOR=true` and the correct `TRUSTED_PROXY_HOPS` after checking the real `X-Forwarded-For` chain in staging.
- Get Legal sign-off on `server/config/consent_ages.json` (including `purchaseAdultMarkets`) and on `TAX_RETENTION_DAYS`.
- Add the grievance officer, legal entity and contact details to the policy drafts.
- Keep `RANKED_PUBLIC_BOARD_ENABLED`, `IAP_ENABLED` and `ENABLE_GEM_PACKS` unset until their prerequisites are met.

## Ready-to-post PR review comment (f3fc100)

> Not posted. The parent agent or the user must post it. Suggested review type: **Approve** with the conditions below, or **Comment** if you want to wait for CI.

---

**Security re-review f3fc100: approve with conditions**

I checked every Critical, High and Medium fix in the code itself:
- **C1:** prod refuses dev wiring. ProdGuard rejects fakes, stubs, in-memory store, dev signers, an ephemeral receipt key, `logOnly`, the client run summary and an admin key, and a missing `CRASHVERSE_ENV` means prod. Token flavor is set by the server, and prod rejects test tokens.
- **H1:** the run summary is dev only. Prod scores come from server replay alone.
- **H2:** `RANKED_PUBLIC_BOARD_ENABLED` is strict, only `"true"`, and the board also needs a configured worker and 100% top-N replay. Provisional entries are hidden at every rank.
- **H3:** four bands, a signed age token required at account creation, under-13s refused, the band stored without the date of birth, the consent table in config marked as needing legal review (18 for unknown markets), and minor limits enforced on the server.
- **M1-M3:** lease with TTL, right-most trusted `X-Forwarded-For`, and the purge, retention and guest-inactivity jobs. Tests are present for all of these.
- **M4:** the workflow uses `environment: unity`. The owner still has to create the environment and move the secrets.

Conditions:
1. Green `server-ci` on f3fc100. I could not run the tests locally.
2. **N1** (`Program.cs:18-33`): `CRASHVERSE_ENV=dev` on Cloud Run would run the fakes and the public push token. Please also refuse `DevContext` when `K_SERVICE` is set, or when bound to a non-loopback address.
3. **N2** (`Domain/Models.cs:74`): the obfuscation pepper is random per process. Load it from Secret Manager and add it to ProdGuard before IAP is enabled.

Low, can be follow-ups:
- **N3:** lease fencing (`Infra/Store.cs:168`).
- **N4:** age tokens are reusable for 30 minutes and not bound to the install (`Domain/ComplianceService.cs:69`).
- **N5:** accounts with no stored band skip `Enforce` (`Domain/ComplianceService.cs:107`).

The owner and legal setup items are listed in `security/PR1_Security_Review.md` under "Re-review f3fc100".

---

---

# Re-review 9b5574b

- **Date:** 2026-10-16
- **Head:** `9b5574be5bb696cfd2c5f01af0b33102a8620cd8` (read-only fetch)
- **Diff from `f3fc100`:** 23 files, +554 / -152
- **CI:** read with `gh pr checks` and `gh run view` (read-only)
- **Actions:** nothing posted, committed or pushed

## Final verdict: APPROVE WITH CONDITIONS. Do not merge until CI is green.

All the items I asked for are fixed in the code. There are no new Critical, High or Medium findings. One required check is red. The cause is a false positive in the release-guard scanner, not a security regression, but the PR must not merge on red.

## CI on 9b5574b

**`server-ci` → `test`: pass.** Two runs, both for head `9b5574b`:
- 38859267940 (45 s)
- 38859273798 (57 s)

**`Unity EditMode tests` → "Unity compile + EditMode tests": FAIL.** Run 38859273776.
- `Tools/ci/check_release_guards.py` reports:
  `Assets/_Project/Scripts/Backend/ServerContracts.cs:75: 'LocalBackendService' outside CRASHVERSE_DEV guard`
- **Cause:** line 75 (unchanged since `f3fc100`) has an inline block comment, `/** Legacy local-stub band only (LocalBackendService) ... */`, on the same line as code. The scanner only treats lines starting with `//`, `///` or `*` as comments, and it strips only `//` tails. So it is a false positive. There is no dev code in the release build.
- **Fix (Developer):**
  - Move that remark onto its own `///` line.
  - Optionally, teach the scanner to strip `/* ... */` spans as well. Don't weaken the symbol list.
- **Related:** the client-side `AgeBands.Minor = "13to17"` legacy value and `IsMinor` still accept it. Remove it once `LocalBackendService` migrates to the four bands (Low).

## Verification of the requested items

**N1: cloud markers and the loopback check — fixed.**
- `DevGuard.CheckNotCloud` (`Infra/Environments.cs:276-283`) refuses dev wiring if any of these is set: `K_SERVICE`, `K_REVISION`, `K_CONFIGURATION`, `CLOUD_RUN_JOB`, `FUNCTION_TARGET`, `FUNCTION_NAME`, `GAE_SERVICE`, `GAE_APPLICATION`.
- It also refuses `GOOGLE_CLOUD_PROJECT`, `GCLOUD_PROJECT` or `GCP_PROJECT`, unless `FIRESTORE_EMULATOR_HOST` points to loopback.
- `DevContext` calls it first (`Program.cs:445`).
- `CheckLoopbackOnly` (`Infra/Environments.cs:286-293`) rejects `*`, `+`, `0.0.0.0` and LAN or public IPs. It runs on the **actually bound** `app.Urls` after Kestrel resolves its configuration, and stops the app on failure (`Program.cs:522-528`).
- **Residual (Low, N7):** the check runs after `app.Start()`. A misconfigured dev server is reachable on a public interface for a few milliseconds before it stops itself. That is acceptable, because the cloud-marker check runs before startup. A pre-start check of the configured URLs would close the gap.

**N2: purchase-binding secret and deletion pepper — fixed.**
- `ObfuscationPepper` and `DeletionPepper` no longer have random defaults (`Domain/Models.cs:206`, `:217`).
- Prod loads both through `ISecretProvider` → `SecretManagerSecretProvider` (Secret Manager `versions/latest:access`, using the runtime service account's credentials, no key file), wired from `SECRET_MANAGER_PROJECT` (`Program.cs:473`, `:484-489`; `Infra/External.cs:329-343`). A failed load sets the value to null.
- `ProdGuard` refuses to start when:
  - `secrets` is missing or dev-only (`Infra/Environments.cs:259`), or
  - either pepper is missing or shorter than 32 bytes (`:260-261`).
- Only dev uses `DevSecretProvider` (`[DevOnly]`).
- **Note:** `versions/latest` means a new secret version would silently change the binding. Pin the version, or treat these two secrets as never-rotate (owner).

**N3: lease checked inside each write — fixed.**
- `FirestoreDocStore.Fenced` (`Infra/Store.cs:362-376`) runs every `Put`, `TryCreate`, `Delete` and `LifecyclePurge` (`:382-420`) in a transaction.
- That transaction re-reads every lease held by the current async flow (`HeldLeases`, an `AsyncLocal` set in `Atomic`, `:428-429`). It aborts with `LeaseLostException` if a lease is gone, now has another owner, or has expired.
- So work that outlives the 15 s TTL cannot commit after another holder takes over.
- `ReReviewTests` and `EmulatorTests` add coverage.

**N4: age tokens bound to the install and single-use — fixed.**
- `/agegate` requires `deviceInstallId`, and the token carries `ins` = sha256(install) plus a `jti` (`Domain/ComplianceService.cs:115-116`).
- `VerifyAgeToken` rejects a mismatched install with `AGE_GATE_REQUIRED` (`:129`).
- `ConsumeAgeToken` burns the `jti` with `TryCreate("ageTokenUses")`. Reuse returns `AGE_TOKEN_USED` (`:137-142`).
- Guest, sign-in (after the provider check) and the new re-gate endpoint all consume it (`Domain/AuthService.cs:65-66`, `:200-203`; `Program.cs:498-504`).
- **Low notes:**
  - The install ID is chosen by the client, so the binding stops casual sharing, not a determined user. Single use is the real control.
  - `ageTokenUses` has no TTL or cleanup. Add a Firestore TTL on the expiry field, or sweep it in `Retention()`.

**N5: missing band → `AGE_GATE_REQUIRED` — fixed.**
- `Enforce` now fails closed when `ageGate.band` is null (`Domain/ComplianceService.cs:150-152`).
- The new `POST /v1/account/agegate` lets an account without a band re-gate with a token from its own install (`Program.cs:498-504`).

**Tests:** `ReReviewTests.cs` (219 lines) is new and covers N1-N5. `server-ci` passes, so they ran.

## Other changes in the diff (no new issues)

- **BE-01** (`Domain/CampaignService.cs:132-147`, `:187-203`, `:238`): pending campaign scores now go to `provisionalBest`, and `Total()` counts only **validated** bests. A failed replay no longer wipes the validated best. This tightens H1.
- **BE-02**: event stars and campaign stars use the per-level star bands (`CampaignService.cs:253`, `EconomyService.cs:71`).
- **Content data:** changes to `chapter1.json` and `season_s1.json` are design data only.

## Status at 9b5574b

**Blockers**

| Item | Status |
|---|---|
| B1 | **Not met on this head.** The release-guard CI step is red (a false positive, see the CI section). |
| B2 | Met (lease fencing done). |
| B3 | Met. |
| B4 | Met. |
| B5 | Met for v1. Purchases stay off. N2 is done. |
| B6 | Met in code. |
| B7 | Met in the workflow. Owner setup still needed. |

**Conditions**

| Item | Status |
|---|---|
| S1 | Met. |
| S2 | Met. |
| S3 | Met. Retention periods still need legal input. |
| S4 | Partly met. There is no KMS signer yet, so prod refuses to start. |
| S5 | Met. The proxy hop count must be confirmed at deploy. |
| S6 | Met. |
| S7 | Pending deployment. Secret Manager uses the runtime service account. |

## Left to do

1. **Merge gate (Developer):** fix the scanner false positive at `Assets/_Project/Scripts/Backend/ServerContracts.cs:75`, then get "Unity compile + EditMode tests" green on the new head.
2. **Low follow-ups:**
   - N7: check the configured URLs before startup.
   - Add a TTL to `ageTokenUses`.
   - Pin the pepper secret versions.
   - Remove the legacy `13to17` client band.
3. **Owner and legal items:** unchanged from the f3fc100 re-review. Add one more: create the Secret Manager secrets `purchase-binding-pepper` and `deletion-pepper`, each at least 32 random bytes, never rotated, readable only by the runtime service account. Set `SECRET_MANAGER_PROJECT`.

## Ready-to-post PR review comment (9b5574b)

> Not posted. The parent agent or the user must post it. Suggested review type: **Comment** now. Switch to **Approve** once CI is green.

---

**Security re-review 9b5574b: approve with conditions, blocked on CI**

I verified each item in the code itself:
- **N1:** dev wiring refuses cloud markers (`K_SERVICE`, `K_REVISION`, Cloud Run Job, Functions, App Engine, and project variables outside the emulator), and checks that the bound URLs are loopback only.
- **N2:** both peppers come from Secret Manager. ProdGuard refuses to start without the provider or with a secret under 32 bytes, and there are no random defaults.
- **N3:** every Firestore write re-checks the held lease inside its transaction.
- **N4:** age tokens are bound to the install hash and single-use (`jti` burned).
- **N5:** an account with no stored band gets `AGE_GATE_REQUIRED`, and there is a re-gate endpoint.

BE-01 (validated-only campaign totals) is a nice tightening. No new security issues.

**Blocking:** "Unity compile + EditMode tests" fails on 9b5574b (run 38859273776). `check_release_guards.py` flags `Assets/_Project/Scripts/Backend/ServerContracts.cs:75`, because the inline `/** ... (LocalBackendService) ... */` comment sits on a code line. It's a false positive, but please move the remark to its own `///` line, and optionally strip `/* */` spans in the scanner. I'll approve once it's green. `server-ci` passes.

Low follow-ups:
- check the configured URLs before `Start()`;
- add a TTL to `ageTokenUses`;
- pin the pepper secret versions, or document them as never-rotate;
- drop the legacy `AgeBands.Minor = "13to17"`.

---

---

## Re-review at 4a83daf (2026-10-16, ~23:10 IST)
Scope: diff 515ee12..4a83daf (2b872b8, 9958e17, 4a83daf). Read-only; nothing posted, committed or pushed.

**Verdict: Approved with conditions.** Every check on 4a83daf is green, but Unity ran 0 EditMode tests (see CI).

**CI at 4a83daf**
- server-ci `test`: success. `emulator`: success.
- **Unity compile + EditMode tests: success** (run 38940558948).
- **"EditMode test results" is neutral, 0/0 tests.** The green comes from compile, the release guard and logic tests; no EditMode test executed. Once #23 merges, its fail-on-0-tests gate will turn this red unless EditMode tests exist.
- The release-guard false positive (ServerContracts.cs:75) is fixed. I re-ran the guard locally at 515ee12 and it passed. CI is green at 4a83daf.

**Low-1 bind check: verified.**
- `DevGuard.ConfiguredUrls` reads `urls` (`--urls`, ASPNETCORE_URLS, DOTNET_URLS), `Kestrel:Endpoints:*:Url` and `http_ports`/`https_ports`. The ports count as wildcards and are refused.
- `CheckConfiguredLoopbackOnly` runs before `app.Start()` (Program.cs:407).
- The post-bind `CheckLoopbackOnly(app.Urls)` backstop is kept, and it calls StopAsync on failure.
- Programmatic listens can only be caught by the backstop. That is acceptable.

**Low-2 ageTokenUses: verified, with a nit.**
- `expiresUnixMs = exp*1000` is set (ComplianceService.cs:94).
- `Retention()` purges expired rows (DataLifecycleService.cs:130-136), and `/internal/jobs/retention` exposes it (Program.cs:361).
- Nit: the token verifies while `exp >= now` (Security.cs:109), but the sweep deletes at `exp*1000 <= nowMs`. That leaves a replay window of under a second, which widens with clock skew between instances. Fix: purge at `exp + 1h`.
- The retention job also needs a Cloud Scheduler entry at provisioning.

**Low-3 pinned secret versions: verified, with one condition.**
- `SecretManagerSecretProvider` refuses an unpinned or `latest` version and access calls without a version (External.cs). `IsPinned` requires a numeric value that isn't 0.
- ProdGuard refuses to start when:
  - SECRET_VERSION_PURCHASE_BINDING or SECRET_VERSION_DELETION_PEPPER is unpinned, or
  - _PREVIOUS is unpinned, fails to load, or equals the current version (Environments.cs:45-52).
- A load failure leaves the pepper null, and ProdGuard's 32-byte check catches it. The tests cover `latest`, a missing previous version and the pinned case (LowFollowUpTests.cs).
- Only the current version plus one previous version can match, because the binding id carries a `v<N>_` prefix (AuthService.cs:35-42). Retiring the previous version therefore just means unsetting the env var.
- **Condition: _PREVIOUS has no sunset.** A retired secret stays valid for as long as the variable is left set. Add `SECRET_PREVIOUS_ACCEPT_UNTIL`, with ProdGuard failing once it's past, or a runbook step tied to the Play refund and RTDN window. Log every match on the previous version so you can see when it's safe to remove.
- The prefix changed from `p1` to `v<N>`. That's fine because there are no prod bindings yet.

**Data revert and gen_content guard: verified.**
- chapter1.json and season_s1.json at 4a83daf are identical to dc02934 and Level Rulings 60ba5d1 (empty diff).
- gen_content.py skips both files unless `--regenerate-level-data` is passed.
- Tools/Tests now expects L10 = 8 shots with bands 0.375/0.25, which matches. Server BE02 and H1 now pass.

**Other changes**
- BE-06 tie-break reset on a score drop (Leaderboards.cs) has tests and is fine.
- PurchaseService uses `MatchesObfuscatedId` for verify and RTDN owner lookup, which is consistent with the rotation design.

**Still open:**
- The `CertDigests` placeholder (Models.cs:15) must be the real Play app-signing digest in prod.
- The LocalBackendService "13to17" dev stub has to migrate together with #3.
- Merge order: after #21 and #8. #1's Resources/Data changes overlap #12 and #13.

---

# PR #52 (integration/v1) re-review at 6cf622e (2026-10-24, ~13:45 IST)

The earlier review was at be6ad77 and asked for changes; that record is the #52 row in PR_Approvals.md. There's no separate #52 file, so this section goes here. Read-only review. Nothing was posted on GitHub.

## Verdict: APPROVE

Both blockers are fixed. I found no new Critical, High or Medium issue in be6ad77..6cf622e (10 commits, 16 files, +202/−43). There's one new Low issue and the earlier non-blocking follow-ups are still open (below).

## Blocker 1: #21 backup exclusions. FIXED (b7702a7, 9444737)
- Assets/Plugins/Android/AndroidManifest.xml:9-12 sets `android:allowBackup="false"`, `android:fullBackupContent="false"` and `android:dataExtractionRules="@xml/data_extraction_rules"`. It also has `tools:replace="android:allowBackup"`, so a library can't turn backup back on.
- Assets/Plugins/Android/CrashverseRes.androidlib/res/xml/data_extraction_rules.xml:4-17. Both `<cloud-backup>` and `<device-transfer>` exclude every domain (root, file, database, sharedpref, external, path "."). That covers the save, the session and token PlayerPrefs, and any future install ID, without listing each file.
- The .androidlib is set up so Unity packages it: project.properties:1 has `android.library=true`, and AndroidManifest.xml:2 has `package="com.crashverse.res"`. The @xml name `data_extraction_rules` matches the file name.
- The built APK proves it. In run 40074274944, Tools/ci/check_apk.sh (android-apk.yml:138-139) used pinned aapt2 8.5.0 (SHA-256 checked) and dumped the merged manifest: `allowBackup=false`, `fullBackupContent=false`, `dataExtractionRules=@0x7f060000` (the resource resolved, so aapt2 link found the XML), `screenOrientation=1`. Result: "APK checks passed (dev)". check_apk.sh:20-22 now fails any future build that loses these attributes.
- Side effect, a good one: the under-13 `cv_age_blocked` flag no longer moves to a new device through backup or transfer. That does no harm, because E2 already accepts clearing app data as a residual risk.

## Blocker 2: age gate not neutral. FIXED (6fdf25c, d559b8a)
- Screens.cs:888 `YearListMiddle = 50`. Screens.cs:895 opens `new ListModal("Year of birth", items, _year > 0 ? now - _year : YearListMiddle)`. ListModal (Screens.cs:241-249) scrolls so that start index is the top row. The list is newest-first (Screens.cs:893), so it opens with now−50 (1976) at the top and only shows years 50 or more back. The 13 and 18 boundaries are not in view. When reopened, it shows the year already picked.
- Runtime evidence: the API 34 emulator smoke logged "year 1974 @ 540 1112 after 0 swipes" (now−52 visible without scrolling). The PlayMode test asserts that the mid-range year is visible and that now−18 is not visible (FirstRunTapTests.cs:76-84). Unity tests run 40074235319 had 160/160 passed, 0 skipped, 3 of them PlayMode.
- No cut-off is revealed anywhere:
  - Nothing is preselected on the month or year picker (Screens.cs:881-885, 889-895).
  - The text is neutral: "Please enter your date of birth." and "We only keep an age range, never your date of birth."
  - Validate (Screens.cs:899-906) checks only for a missing value or a future date, so Continue behaves the same at every age.
  - The block screen says only "CRASHVERSE isn't available." (Screens.cs:928), with no back or retry.
  - Ages 13 to 17 and adults get the same path at the gate.
  - The smoke scripts pick the year by name (Tools/ci/pick_year.sh), so tests no longer depend on a now−18 anchor.
- Persisted block (E2): Confirm sets `cv_age_blocked=1` and saves it before the callback (Screens.cs:914), and no date of birth is stored. GameBootstrap.cs:41 checks Blocked first on every launch and shows BlockedScreen. `Needed` requires `!Blocked` (Screens.cs:844), so a version bump doesn't bring the gate back. A blocked under-13 can't enter a new year without clearing data or reinstalling, which E2 accepts. FirstRunTapTests.cs:162-163 asserts this.

## Rest of the diff (be6ad77..6cf622e)
- Secrets: gitleaks 8.30.1 on the 10 commits found no leaks. No new secret is referenced. The aapt2 download is from dl.google.com and pinned by SHA-256, and `.aapt2/` is gitignored.
- Debug and test code in release: UITapProbe is still bare-guarded with `#if CRASHVERSE_DEV || DEVELOPMENT_BUILD` (UITapProbe.cs:1). Its new RESULTDONE line logs only win/fail, score, stars and %, so no PII. The new ResultsScreen getters (Score, Stars, Ratio, SequenceDone, FailTitle) are read-only UI values. `AimController.Drag` changed from private to internal and the yaw sign was fixed. That affects input direction only, the server replays the inputs, and the change adds no new trust. The release guard passes locally at 6cf622e (118 files) and its self-tests pass. In CI the source guard passed (118 files). The `--built` IL2CPP scan was skipped because this was a dev-profile run (expected).
- Network and PII: there are no new endpoints or HTTP calls in game code. The only new network use is CI downloading aapt2.
- Orientation: portrait only (BuildScript.cs:40-43, ProjectSettings.asset, activity `screenOrientation`), asserted by check_apk.sh. No security effect.
- Workflows:
  - android-apk.yml: the build job now runs check_apk.sh. Emulator smoke now uses a sparse checkout of qa/ci for the #36 logcat fail patterns, plus a final whole-run check.
  - The publish job can now update an existing prerelease:
    - It runs only on dispatch with publish_tag set, in the `dev-builds` environment, with `contents: write`.
    - TAG is passed through env, not interpolated into the script (no injection).
    - It refuses to touch a release that isn't a prerelease.
  - unity-tests.yml has no changes.
- CI: run 40074274944 (workflow_dispatch on integration/v1, dev profile, head 6cf622e) succeeded in every job: guards, GameCI build including check_apk and the cert pin, emulator smoke API 34 and 36, and publish (it updated dev-build-6). server-ci and Unity tests are green on 6cf622e. The push-triggered runs were cancelled because the dispatch superseded them.

## New finding
- **L-52-1 (Low), prerelease provenance mismatch.** Updating an existing prerelease only uploads new assets with `--clobber` and edits the notes (android-apk.yml:274-277). It does not move the tag. Right now `dev-build-6` still points to **be6ad77**, but its APKs (uploaded 2026-10-24 08:09 UTC = 13:39 IST) and its notes say they were built from **6cf622e**. Anyone who checks out the tag to reproduce or audit the build gets the wrong source. The same path also lets a dev/* dispatch replace the APKs of a prerelease built from another branch. Fix: use a new tag for each publish and refuse an existing tag. Or, if you keep updating, delete the release and tag and recreate them at `$GITHUB_SHA`, and record the replaced asset hashes in the notes. Not blocking, because these are dev-signed QA builds only.
- Info: check_apk.sh:22 only checks that `dataExtractionRules` is present, not what the rules say. Optional hardening: dump `res/xml/data_extraction_rules.xml` from the APK and assert that both sections exclude the sharedpref and file domains.

## Earlier non-blocking follow-ups: status at 6cf622e (none changed)
- (a) The `integration/**` push trigger and the integration/ build condition with signing secrets are still at android-apk.yml:18 and :69. Drop them after merge. This run shows the exposure: an integration-branch dispatch published a signed prerelease.
- (b) Unity secrets in unity-tests.yml are still not gated. The file is unchanged: secrets at lines 66-68 and 85-87, githubToken at 73 and 92.
- (c) `--built` exists only on the release APK step (android-apk.yml:194-198). There's still no AAB job (#8), so it never runs on a real release artifact.
- (d) #33 is still OPEN and not in main or #52.
- (e) `CertDigests = { "dev-cert-sha256" }` is still a placeholder with no env override or startup check (server Models.cs:15). It fails closed.
- (f) targetSdk is still 34 (ProjectSettings.asset:30, the line moved from 25). It needs to be 35.
- (g) #3's install ID and Reset are still absent. There's no InstallId, no noBackupFilesDir file and no deletion wiring. The new manifest rules lower the backup risk but don't replace this.
- (h) The 4 age bands are still unchanged (AgeGate.cs not in the diff).
- (i) The server age token is still unchanged: SubmitAgeGate has no deviceInstallId or ageToken, and there's no AGE_GATE_REQUIRED re-gate (IBackendService.cs, AccountLifecycle.cs not in the diff).
