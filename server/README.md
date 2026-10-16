# CRASHVERSE server (v0, local only)

This is the authoritative game server described in `backend/Backend_Plan_v1.md`. It covers the PM Decisions, the Developer Agreement, Security Admin's conditions in `security/Backend_Plan_v1_Review.md`, and PM's refund decision (S6).

It is an ASP.NET Core minimal API on .NET 8. **No hosting is approved, so nothing is provisioned or deployed.** Every external service sits behind an interface, and the dev wiring uses a fake for each one.

## Run it locally (in-memory store)
```bash
cd server
dotnet test                               # unit + API tests; the emulator tests are skipped
dotnet run --project src/Crashverse.Server --urls http://127.0.0.1:8080
curl http://127.0.0.1:8080/v1/time
```

**Environment variables (all optional):**
- `CRASHVERSE_CONTENT`: content folder. The default is `../Assets/_Project/Resources/Data`, found automatically.
- `REFUND_POLICY`: `gemDebt` (the **default**) or `floorAtZero`.
- `INTEGRITY_MODE`: `enforce` (the default) or `logOnly`, used for closed testing.
- `AUTH_X_ENABLED`: `true` turns on X login. It is off by default (PM decision 2).
- `CRASHVERSE_STORE`: `firestore` uses the Firestore emulator (see below). Otherwise the store is in memory.

## Run against the Firestore emulator (no Google account needed)
This uses the `demo-` project prefix, so it never touches a real project. It needs Java 11+ and `npm i -g firebase-tools`.
```bash
cd server
dotnet build
firebase emulators:exec --only firestore --project demo-crashverse "dotnet test --no-build --filter Category=Emulator"
# run the server itself on the emulator
firebase emulators:start --only firestore --project demo-crashverse &
FIRESTORE_EMULATOR_HOST=127.0.0.1:8085 CRASHVERSE_STORE=firestore dotnet run --project src/Crashverse.Server
```

**Firestore rules.** `firestore.rules` denies every client read and write. The emulator tests check this against REST calls made both unauthenticated and as a signed-in Firebase user (Security condition B2).

**Emulator only.** `FirestoreDocStore` refuses to start unless `FIRESTORE_EMULATOR_HOST` is set.

## Layout
| Path | What |
|---|---|
| `src/Crashverse.Server/Program.cs` | Endpoints, error envelope, check order (body cap → JWT → rate limit → nonce → work) |
| `Infra/Store.cs` | `IDocStore`: an in-memory store plus a Firestore store that targets the emulator only. Locks work across instances. `ledger` and `audit` are append-only. |
| `Infra/Security.cs` | `ISigner` (ES256 dev key), key rings with a rotation overlap, ES256 compact tokens, receipt HMAC (`kid`, a per-run HKDF key), and a shared-store nonce cache and rate limiter. |
| `Infra/External.cs` | Interfaces and fakes for Play Integrity, Google/Facebook/X verifiers (with real Google/Facebook implementations), email, the Play Developer API, Pub/Sub push auth, and the replay worker (stub). |
| `Domain/*` | Auth/linking/email, wallet + ledger, Titan, campaign/events, season/daily/inbox/friends, purchases/RTDN/refunds, leaderboards + replay |
| *Shared/* (linked) | These client sources are compiled into the server: `ContentModels`, `Clock` (`UtcDay`), `WinRule`, `StarRules`, `ComboTracker` (`CampaignScore`), `StoreValidator`, `BackendModels`, `ServerContracts` (`TitanScoring`). There is no `Crashverse.Rules` folder yet. When Developer's asmdef lands, repoint the `<Compile Include>` links in the csproj at it. |

## Security behaviour
- **Tokens.**
  - Access JWTs last 15 minutes. Refresh tokens rotate, and reusing one revokes the whole session family.
  - Run tokens (ranked) and attempt tokens (campaign and events) are ES256, signed through `ISigner`. A local ECDSA key is used in dev; a KMS implementation comes later.
  - Rotating a key ring keeps the old `kid` valid for an overlap window.
  - Checkpoint receipts are `kid.HMAC(HKDF(master, runId))`.
- **Ranked.**
  - One attempt per UTC day.
  - Integrity requires `MEETS_DEVICE_INTEGRITY`, `PLAY_RECOGNIZED`, a matching package, certificate and request hash, and a verdict no older than 5 minutes. A failed check turns the run into practice, and the attempt is not consumed.
  - A missing `LICENSED` verdict only raises the risk score.
  - With `logOnly` set, failures are logged and the run is not downgraded.
  - The server enforces a 1.5 s minimum between shots and a cap on damage per shot.
  - A run with missing inputs or a `simVersion` mismatch counts as practice.
  - The grace window is inclusive up to 00:15:00.000 UTC.
- **Campaign and events.**
  - Attempt tokens are single-use, expire after 7 days, and are bound to player, mode, level and level version. Each player can hold at most 20 outstanding.
  - Results must carry an input log and pass plausibility checks: one input per shot, the minimum interval, bounds, and a per-level score ceiling.
  - Pre-validation first-clear rewards have a daily cap. Above the cap they are deferred until validation, and they are clawed back if the replay fails.
- **Replay and boards.**
  - An entry with an unreplayed run never appears in the top N (100). Outside the top N it shows with `provisional:true`.
  - Every run is replayed if it is in the Titan top 10%, comes from a high-risk account, or is drawn in a 5% random sample.
  - Out-of-tolerance runs go to the shadow board with an audit entry and no ban. The lower of the client and replay scores counts.
- **Purchases.**
  - The server checks with Play that the purchase is in the purchased state, the product is known, and `obfuscatedExternalAccountId` matches the player. Each purchase token can only be used once.
  - Gem packs are consumed; the pass and cosmetics are acknowledged.
  - RTDN needs push OIDC, drops duplicates by `messageId`, and **always re-queries the Play API** before granting or revoking. The Voided Purchases API is polled.
- **Refunds** (`REFUND_POLICY`):
  - `gemDebt` (**default**, Security S6 adopted by PM): refunded gems are clawed back even if that makes the balance negative, and gem spending is locked until the debt is repaid. Items bought with gems are kept.
  - `floorAtZero` (PM's original decision 3): only unspent gems are clawed back, the balance stops at 0, and the shortfall is written to the ledger note.
  - Both policies revoke cosmetics bought directly with cash and the season pass, including cosmetics claimed from premium tiers.
  - 2 or more refunds in 90 days set the `refundAbuse` flag and raise the risk score. There is no ban.
- **Abuse.**
  - Unauthenticated endpoints have strict per-IP limits (`/auth/guest` 10 per hour per IP and per device, `/auth/*` 5 per minute). The body cap is 64 KB (256 KB for journals).
  - The `X-Nonce` replay cache and the rate limiters both live in the shared store.

## Endpoint map → `IBackendService` (interface agreed with Developer, 2026-10-16; client commit 817aef8)
All paths carry the `/v1` prefix. Bearer token required unless noted. Every list response is `{items:[...]}`. Times on the new account/privacy endpoints are Unix seconds UTC; leaderboard entries carry `submittedUnixMs` (+ `submittedTicks`) and `provisional`.

| `IBackendService` | Server |
|---|---|
| guest / refresh (no bearer) | `POST /auth/guest`, `POST /auth/refresh` |
| `SignIn` (no bearer) | `POST /auth/sign-in` `{provider, token, codeVerifier?, redirectUri?, nonce?, deviceInstallId}` → `AuthResponse` |
| `LinkAccount(LinkCredential)` | `POST /account/link-nonce` (Google), `POST /account/link` `LinkCredential` (+ `conflictResolution`) |
| email register / verify / login / reset | `POST /auth/email/register`, `/auth/email/verify`, `/auth/email/login` (→ `AuthResponse`, no bearer), `/auth/email/reset-request`, `/auth/email/reset` |
| `Reauthenticate` | `POST /auth/reauth {credential}` → `{reauthToken, expiresAt}` (5 min, single use) |
| `DeleteAccount` | `POST /account/delete {reauthToken}` → `{status:"scheduled", purgeAt}` (linked: `401 REAUTH_REQUIRED` without a fresh reauth; guest: `null` allowed, the session is the proof) |
| cancel deletion | `POST /account/delete/cancel` → `{status:"active"}` (allowed while pending) |
| keep active | `POST /account/keep-active` → `{lastActiveAt}` |
| `SubmitAgeGate` (no bearer) | `POST /agegate {birthYear, birthMonth, country}` → `{band: under13\|13to15\|16to17\|adult, requiresParentalConsent}`; nothing stored. Consent ages per market come from `server/config/consent_ages.json` (**NEEDS LEGAL REVIEW**; override with `CONSENT_AGES_PATH`; a missing or invalid file stops startup) |
| `RequestParentalConsent` | `POST /consent/parental/request {parentEmail}` → `{status:"pending", requestId}` via `IParentalConsentMailer` (fake); only a hash of the email is stored |
| `GetParentalConsentStatus` | `GET /consent/parental/status` → `{status: none\|pending\|granted\|denied}`; parent decision: `POST /internal/consent/decide` (push-auth, placeholder for the parent page) |
| `SetProfileVisibility` | `POST /profile/visibility {hideName}` → `{hideName}`; others see `Wrecker #XXXX` on boards and friend lists |
| `BeginAttempt` | `POST /attempts` (also `/campaign/attempts`, `/events/attempts`); `seed` is a long |
| `SubmitScore` | `POST /score` `{attemptToken, inputs[{tick,type,x,y}], clientScoreBp, buildHash}` → `{accepted, status, message, serverScoreBp}`. There is no separate input-log endpoint. |
| `VerifyPurchase` | `POST /purchases/google/verify` → `{status: pending\|verified\|rejected, wallet, season}` |
| restore | `GET /purchases` |
| Titan | `GET /titan/today`, `/titan/ranked/status`, `POST /titan/ranked/start`, `/checkpoint` (that shot's `InputEvent`s), `/finalize` |
| leaderboards / friends | `GET /leaderboards/{0-3}`, `GET/POST /friends`, `POST /friends/{id}/remove` |
| wallet / inventory / season | `GET /wallet`, `/wallet/ledger`, `/inventory`, `/season`, `/inbox`, `/daily-reward`, etc. |
| flags / time | `GET /config/flags`, `GET /time` |

Inputs are Developer's `InputEvent` (fixed point, 1/1000): `aim` (x yaw, y pitch in milli-degrees), `power` (x 0..1000), `powerup` (x slot), `fire`, `settle`; `tick` is the physics tick. Checks: known type, ticks non-decreasing, ranges, ≤64 events per shot, one `fire` per shot used, a `settle` after each fire, and the server minimum shot interval in ticks.

## Flags (PR #1 decisions)
| Env var | Default | Effect |
|---|---|---|
| `ALLOW_CLIENT_RUN_SUMMARY` | `true` in Development, **forced `false` elsewhere** | When false, the client's `won/destroyedRatio/shotsLeft/bestCombo` are ignored: only inputs are checked and stored, the run is queued for replay, and best, boards and rewards change only from the replay result. |
| `RANKED_PUBLIC_BOARD_ENABLED` | `false` | The public Daily Titan board shows other players only if this is `true` **and** the replay worker is configured **and** every top-N entry is replayed. Otherwise it fails closed (`publicBoard:false`, `publicBoardReason`, the viewer sees only their own entry). This covers B3. |
| `REFUND_POLICY`, `INTEGRITY_MODE`, `AUTH_X_ENABLED` | `gemDebt`, `enforce`, off | unchanged |
| `CRASHVERSE_ENV` | missing = `prod` | `dev`/`test` use `DevContext` (fakes, in-memory keys). `prod` uses `ProdContext`, which runs `ProdGuard` first and **refuses to start** with any fake/stub/in-memory adapter, in-process signer or receipt key, `INTEGRITY_MODE` other than `enforce`, the client run summary, or an admin key (C1). Prod adapters are not built yet, so prod cannot start: intended. |
| `IAP_ENABLED` | `false` | Economy v1 has no IAP. Off: `/v1/purchases/google/verify` → 404 `FEATURE_DISABLED`; RTDN is acknowledged and ignored. |
| `ENABLE_GEM_PACKS` | `false` | Served as `enableGemPacks` in `/v1/config/flags`. Off: verify rejects `gems_small` (Pouch, 0.99) and `gems_large` (Chest, 4.99), the ids from client PR #10 `store_catalog.json`, with 403 `GEM_PACKS_DISABLED` before any Play call or grant. |
| `TRUST_X_FORWARDED_FOR` / `TRUSTED_PROXY_HOPS` | `false` / `1` | M2: rate-limit IP from X-Forwarded-For only when on; takes the entry N hops from the right (the one the load balancer appended). |
| `TAX_RETENTION_DAYS` | `2920` | S3: pseudonymised purchase rows kept this long after deletion. **NEEDS LEGAL REVIEW.** |

All boolean flags are strict: only the exact string `true` turns them on (H2).

Consent ages per market: `server/config/consent_ages.json` (**NEEDS LEGAL REVIEW**, `legalReviewed:false`). While unreviewed, unknown markets use 18. Gem grants: `server/config/economy_v1.json`.

## Startup guards (Security re-review f3fc100)

- **N1:** `DevContext` refuses to start when it finds a cloud runtime: `K_SERVICE`, `K_REVISION`, `K_CONFIGURATION`, `CLOUD_RUN_JOB`, `FUNCTION_TARGET`, `FUNCTION_NAME`, `GAE_SERVICE` or `GAE_APPLICATION` is set, or `GOOGLE_CLOUD_PROJECT`/`GCLOUD_PROJECT`/`GCP_PROJECT` is set without a loopback `FIRESTORE_EMULATOR_HOST`. After Kestrel starts, any listen address that isn't localhost or loopback stops the dev server.
- **N2:** the purchase-binding secret (`purchase-binding-pepper`) and the deletion pepper (`deletion-pepper`) come from `ISecretProvider`. In prod that is Secret Manager, with `SECRET_MANAGER_PROJECT` read through the runtime service account. In dev the values are generated in-process. ProdGuard fails if either is missing.
- **Secret versions and rotation:** versions are pinned through config, never `latest`: `SECRET_VERSION_PURCHASE_BINDING`, `SECRET_VERSION_DELETION_PEPPER`, and during a rotation `SECRET_VERSION_PURCHASE_BINDING_PREVIOUS` plus `SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL` (ISO UTC, e.g. `2027-04-30` = end of that UTC day, or `2027-04-30T00:00:00Z`). **Tie the accept-until date to the Play refund window:** rotation date + the longest time Play can still send a refund/void notification or a late verify for a purchase bound with the old secret. Prod refuses to start if `_PREVIOUS` is set without the date, after it has passed, or if the date is more than `SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS` (default 400, hard ceiling 730 even if configured higher) days away; keep that cap at the Play refund window plus a margin. At runtime the previous secret is never accepted without the date (fails closed); after the date the previous secret is no longer accepted at runtime either. Every verification that matches the previous secret is logged (`[security] purchase binding verified with PREVIOUS secret ...`, pseudonymised player id).
- **Used age tokens:** `ageTokenUses` records are purged by the retention job one hour after the token expires; single use is enforced until then.
- **N3:** while holding a lease, every Firestore write runs in a transaction that re-reads the lease. It commits only if we still own it and it hasn't expired; otherwise it throws `LeaseLostException`.
- **N4/N5:** `/v1/agegate` needs `deviceInstallId`. The age token is bound to that install and works once. Accounts without a stored band get 403 `AGE_GATE_REQUIRED` on restricted features until they call `POST /v1/account/agegate {ageToken}`.

## Jobs (Cloud Scheduler → OIDC push)

| Job | What it does |
|---|---|
| `POST /internal/jobs/purge` | Purges accounts past their 7-day grace period (deletion completes within 30 days; an audit alert fires after 25). It deletes or aggregates player-keyed rows (the ledger goes to `ledgerDaily`), pseudonymises purchases, detaches top-100 runs (tombstone id) and redacts audit rows. Idempotent. |
| `POST /internal/jobs/retention` | Clears input logs older than 90 days and deletes purchase rows whose tax retention has ended. |
| `POST /internal/jobs/guest-inactivity` | Warns guests at 335 days of inactivity and schedules deletion at 365. Any authenticated call or keep-active resets the clock. |

Web deletion (B6, no app needed): `POST /v1/web/deletion/request {email, captchaToken}` always returns the same generic answer and emails a 6-digit code to verified emails. `POST /v1/web/deletion/confirm {email, code}` allows 5 attempts and a 15-minute code. `POST /v1/web/deletion/provider {credential, captchaToken}` covers Google, Facebook and X. Rate limits: 5 requests per IP per hour and 3 per email per day.

## CI and Unity secrets (M4)

`server-ci.yml` only builds and tests: actions are pinned to SHAs and it has `permissions: contents: read`. `android-apk.yml`'s build job runs in the GitHub **environment `unity`**. `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` must be stored as secrets of that environment only, not as repo secrets. Set the environment to require reviewers and to deploy from `main` only. The repo owner configures this in Settings → Environments.

## Not done in v0
- No real KMS signer, no Play API client and no Integrity decoder; only the interfaces exist.
- The replay worker is a stub, so queued runs stay pending.
- No admin UI (the override endpoint is break-glass only and audited).
- No deployment, CI deploy job or cloud resources. CI (`.github/workflows/server-ci.yml`) only builds and tests.
- Outside Development, campaign results stay pending until a real replay worker exists (the dev stub never counts as configured). Server-replayed first-clear rewards are not granted yet. `clientScoreBp` is advisory.
- The deletion purge job (requirements D1–D8), the 365-day guest inactivity job (C2) and the parent-facing consent page are not built.
