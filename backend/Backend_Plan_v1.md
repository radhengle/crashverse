# CRASHVERSE Backend Plan v1

- Owner: Backend Engineer. Status: **draft for PM review**. Nothing is provisioned and no accounts have been created.
- Inputs:
  - `security/SECURITY_REVIEW_v1.md` (C1, C2, H1-H4; M1, M2, M4 where they apply).
  - Client interface at commit `0d60e07`: `Assets/_Project/Scripts/Backend/IBackendService.cs`, `BackendModels.cs` and `LocalBackendService.cs` (LBS).
  - `design/Spec_Gap_Answers_v1.md` (SG-10 to SG-23 and SG-34 to SG-36) and Design Addendum v1.1 §4 (store scope) plus SG-31 to SG-33.
  - The design doc `crashverse_share.txt`: Backend, Security, Daily Titan and Milestones 5-6.
- Line references are to the file at `0d60e07`. They match the security review's "SB:nnn" numbers. For example, `GrantPremiumPassStub` is SB:465.
- Labels: **[verified]** means the price was checked on the vendor page on 2026-10-16 (sources in §12). **[estimate]** means my own calculation from those prices.

---

## 0. Summary of decisions (proposed)

1. **Stack.** Google Cloud Run runs an ASP.NET Core (C#) API. Firestore holds the data. Secret Manager and Cloud KMS hold the keys. Pub/Sub carries Play RTDN. Cloud Scheduler drives resets. A Cloud Run Job runs a headless Unity Linux build as the replay worker.
   - Choosing C# lets the server reuse the client's pure scoring and validation code (`CampaignScore`, `TitanScore`, `WinRule`, `StarRules`, `StoreValidator`) as a shared assembly, so the scoring formulas can't drift between client and server.
2. **Auth.** We issue our own sessions: a 15-minute access JWT signed by KMS and a rotating refresh token. The server verifies each provider token itself.
   - Firebase Auth is optional. It is only worth adding if Developer wants its SDKs. It is not needed for security.
3. **Server owns everything.** The server owns time, Daily Titan config, wallets, entitlements, scores and boards. The client sends only *inputs and claims*, never balances or scores.
4. **Release builds.** `LocalBackendService` becomes dev-only. A release build fails closed unless it uses `HttpBackendService`.
5. **Cost at small scale** (≤ 1k DAU) is about **$0 to $3 per month** [estimate].
   - The owner has to put a billing account (card) on GCP/Firebase Blaze, because Cloud Run, Secret Manager and Pub/Sub all need billing enabled even inside their free tiers.
   - Budget alerts would be set at $5 and $20.
   - Everything is managed from the Cloud Console and Firebase Console mobile web plus GitHub Actions. No PC is needed (§11).

---

## 1. API contract (maps 1:1 to `IBackendService`)

**Conventions**
- Base URL: `https://api.<domain>/v1`. HTTPS only, TLS 1.2+, with a pinned public key and a backup pin (M2).
- JSON field names match the C# models exactly, because `JsonUtility` is case-sensitive.
- Enums are sent as **integers**, the way `JsonUtility` serialises them: `BoardType` 0-3, `BoardPeriod` 0-1, `SubmitStatus` 0-4.
- **Headers on every authenticated call:**
  - `Authorization: Bearer <accessJwt>`
  - `X-Nonce: <uuid>`: the server rejects a repeat within 10 minutes.
  - `X-Client-Time: <unix ms>`: informational only.
  - `X-App-Version`
  - `Idempotency-Key` on every POST that grants something. The server stores the response for 24 hours and replays it on retry.
- **Every response** carries `serverTimeUnixMs` in a header (`X-Server-Time`) and, where the shape allows, in the body. The client can keep its clock offset fresh without extra calls (SG-20, M2).
- **Error envelope** (HTTP 4xx/5xx): `{ "error": { "code": "ATTEMPT_USED", "message": "Ranked attempt already used today", "retryable": false } }`.
  - The client maps any non-2xx response to `BackendException(message)`.
  - A network failure or a 503 maps to `BackendException("offline")`, which is what the interface promises.
  - Proposed error codes: `UNAUTHENTICATED`, `TOKEN_EXPIRED`, `NONCE_REPLAY`, `RATE_LIMITED` (429 with `Retry-After`), `INTEGRITY_FAILED`, `ATTEMPT_USED`, `UNKNOWN_RUN`, `RUN_CLOSED`, `CHECKPOINT_REJECTED`, `ALREADY_CLAIMED`, `TIER_NOT_REACHED`, `PREMIUM_NOT_OWNED`, `BAD_TIER`, `INVALID_PURCHASE`, `PROVIDER_TOKEN_INVALID`, `IDENTITY_IN_USE`, `LAST_LOGIN_METHOD`, `SERVER_TIME_UNAVAILABLE`, `MAINTENANCE`.

### 1.1 Endpoint table

| # | Client method | HTTP | Request body | 200 response (model) | Notes / errors |
|---|---|---|---|---|---|
| 0 | *(new)* session bootstrap | `POST /auth/guest` | `{deviceInstallId, integrityToken?}` | `{accessToken, refreshToken, account: AccountState}` | Creates the server-issued 128-bit guest id (M4). Rate-limited per IP and device. |
| 0b | *(new)* | `POST /auth/refresh` | `{refreshToken}` | `{accessToken, refreshToken}` | Rotating tokens. Reusing an old refresh token revokes the whole token family. |
| 1 | `IsOnline` | `GET /health` *(optional)* | n/a | `{ok:true}` | Client-side property. It can stay as "last call succeeded". |
| 2 | `GetServerTimeUtc()` | `GET /time` | n/a | `{serverTimeUnixMs}` | Unauthenticated, cacheable 0 s. Replaces `DefaultTimeUrl` (google generate_204) per M2. |
| 3 | `GetAccount()` | `GET /account` | n/a | `AccountState {playerId, displayName, linked, provider}` | **Mismatch P1:** `provider` is a single string, but an account can have several links. Proposal: keep `provider` as the primary link and add `providers: string[]`. |
| 4 | `LinkAccount(provider)` | `POST /account/link` | `{provider:"google"\|"facebook"\|"x"\|"email", credential:{idToken?\|accessToken?\|authCode?, codeVerifier?, redirectUri?}, conflictResolution?:"keep_current"\|"switch_to_existing"}` | `AccountState` | **Mismatch P2 (blocking for H4):** the client signature has no credential. Proposal: `LinkAccount(string provider, ProviderCredential cred)`. The 409 `IDENTITY_IN_USE` response returns `{existingAccountSummary}` for the explicit choice UI (review §4). |
| 4b | *(new)* | `POST /account/unlink` | `{provider, recentAuth}` | `AccountState` | Needs a fresh re-authentication. Returns `LAST_LOGIN_METHOD` if it would remove the only login method. |
| 4c | *(new)* | `DELETE /account` + web URL | n/a | `{scheduledDeletionUnixUtc}` | Play policy requires account deletion (review §7). |
| 5 | `GetWallet()` | `GET /wallet` | n/a | `Wallet {coins, shards, extraCharges, cosmetics[], powerUps[]}` | **Mismatch P3:** there is no premium-currency field, but Addendum §4 sells currency packs. Proposal: add `gems:int`. |
| 6 | `ConsumeExtraCharge()` | `POST /wallet/consume-charge` | `{context:{mode, levelId?, runId?}}` | `{consumed:bool, wallet: Wallet}` | The client reads `.consumed` to return a `bool`. Written to the ledger. Idempotency-Key is required, so a retry doesn't double-spend. |
| 7 | `GetInbox()` | `GET /inbox` | n/a | `InboxItem[]` (bare array) | **JsonUtility can't parse a top-level array.** Proposal: the wire format is `{items:[...]}` and the client unwraps it. The same applies to every list response. |
| 8 | `ClaimInbox(itemId)` | `POST /inbox/{itemId}/claim` | n/a | `Wallet` | Idempotent: claiming an already-claimed item returns the current wallet, with no error, matching LBS. |
| 9 | `GetDailyTitan()` | `GET /titan/today` | n/a | `DailyTitanConfig` + `configHash`, `configSig` | Server-built from templates plus server-side overrides only (H3). |
| 10 | `GetRankedStatus()` | `GET /titan/ranked/status` | n/a | `RankedStatus {dayKey, attemptUsed, submitted, score, status}` | `status` is one of `available`, `in_progress`, `submitted`, `submitted_partial`, `refunded_practice` or `pending_validation` (new). |
| 11 | `BeginRankedRun()` | `POST /titan/ranked/start` | `{integrityToken, requestHash}` | `RankedTicket {token, dayKey, issuedUnixUtc, shotBudget}` + **proposed** `seed, configHash, physicsHz, simVersion` | `ATTEMPT_USED` (409), `INTEGRITY_FAILED` (403, or a practice-only downgrade, §6). `token` is an opaque signed run token (§3). |
| 12 | `ReportRankedCheckpoint(cp)` | `POST /titan/ranked/checkpoint` | `RankedCheckpoint` (+ **proposed** `inputs: ShotInput`) | `RankedCheckpoint` with `receipt` filled | Identical retry gets the same receipt (matches SB:274-275). Errors: `CHECKPOINT_REJECTED` (reason in message), `UNKNOWN_RUN`, `RUN_CLOSED`. A server-measured minimum interval between shots applies (H2). |
| 13 | `FinalizeRanked(journal)` | `POST /titan/ranked/finalize` | `RankedJournal {token, final, checkpoints[]}` (+ proposed per-shot `inputs`) | `SubmitResponse {status, message, finalScore}` | Same rules as SB:290-345. The grace window is inclusive at 00:15:00.000 (SG-22). Top-N or outlier runs return `Accepted` with the message "pending validation" and stay off the public board until the replay passes (§3.4). |
| 14 | `GetDailyRewardState()` | `GET /daily-reward` | n/a | `DailyRewardState` | `days` comes from the server catalogue. |
| 15 | `ClaimDailyReward()` | `POST /daily-reward/claim` | n/a | `Wallet` | `ALREADY_CLAIMED` (409). The streak never resets (SG-14). |
| 16 | `GetSeasonState()` | `GET /season` | n/a | `SeasonState` | `premium` comes only from an entitlement record (C2). |
| 17 | `ReportEventStars(levelId, stars)` | `POST /events/{eventLevelId}/stars` | `{stars}` → proposed `{runToken, stars, inputs}` | `SeasonState` | **Mismatch P4:** this has the same weakness as H1. The client asserts the stars. Proposal: event levels use the same attempt-token and input-log flow as the campaign (§3.5). |
| 18 | `SubmitEventScore(levelId, stars)` | `POST /events/{eventLevelId}/score` | `{stars}` | `{weeklyStars:int}` | **Mismatch P5:** this duplicates #17 (SB:462 calls ReportEventStars). Proposal: drop it, or alias it to #17 and return `weeklyStars` in `SeasonState`. |
| 19 | `ClaimSeasonTier(tier, premium)` | `POST /season/tiers/{tier}/claim` | `{premium:bool}` | `SeasonState` | `BAD_TIER`, `TIER_NOT_REACHED`, `PREMIUM_NOT_OWNED`. Idempotent. |
| 20 | `SubmitCampaignResult(report)` | `POST /campaign/results` | `CampaignRunReport` (+ **proposed** `attemptToken, inputs[], simVersion`) | `CampaignSubmitResponse {accepted, message, levelBest, total, firstClear}` | `reportId` is the idempotency key (BUG-14). Today's stats-only form is kept as **interim** under H1 caps (§3.5). |
| 20b | *(new)* | `POST /campaign/attempts` | `{levelId, levelVersion}` | `{attemptToken, seed, expiresUnixUtc}` | Needed for H1. It can also be issued lazily while offline (see Q-D4). |
| 21 | `GetLeaderboard(board, period, friendsOnly)` | `GET /leaderboards/{board}?period=0\|1&friends=true` | n/a | `LeaderboardPage` | Top 100 plus `me`. Guests are hidden (`meIsGuestHidden`) as in SG-19 and SG-36. `submittedTicks` stays as **.NET ticks** for compatibility. Proposal: add `submittedUnixMs`. |
| 22 | *(new, C2)* | `POST /purchases/google/verify` | `{productId, purchaseToken, orderId}` | `{status:"granted"\|"pending"\|"already_granted"\|"invalid", wallet: Wallet, season: SeasonState?}` | §4. The client grants nothing. `INVALID_PURCHASE` (402/409). |
| 22b | *(new)* | `GET /purchases` | n/a | `{purchases:[{productId, orderId, state, grantedUnixUtc}]}` | Restore purchases. |
| 23 | *(new)* | `GET /config/catalog` | n/a | store, daily reward, season and event catalogue + `catalogVersion` | Lets the server change schedules without a build (design doc §10). |

### 1.2 Mismatches and proposals for Developer (summary)
- **P1:** add `providers[]` to `AccountState`.
- **P2:** `LinkAccount` needs a credential parameter. This is blocking for H4.
- **P3:** add `gems` to `Wallet`.
- **P4:** event stars need an attempt token and inputs.
- **P5:** `SubmitEventScore` is redundant.
- **P6:** list responses are wrapped in an object (JsonUtility limitation).
- **P7:** new methods: `PurchaseVerify`, `RestorePurchases`, `AuthGuest`, `Refresh`, `Unlink`, `DeleteAccount`, `StartCampaignAttempt`, `GetCatalog`.
- **P8:** `RankedTicket` gains `seed`, `configHash`, `physicsHz` and `simVersion`.
- **P8b:** `RankedCheckpoint` gains `inputs`, a `ShotInput` object of `{yaw, pitch, power, powerUpId?, tapMs, settleMs}`.
- **P9:** physics step. `Perf/QualityController.cs:27` and `:37` switch `Time.fixedDeltaTime` between 0.02 and 0.0333 depending on quality. **Ranked and campaign runs that will be submitted must run at a fixed 50 Hz**, whatever the quality tier, or replays can't match.
- **P10:** release build fails closed if `BackendLocator.Current is LocalBackendService` (C1). Strip `SetTestClock` and the related hooks (M1), the override path (H3) and `GrantPremiumPassStub` (C2) with `#if DEVELOPMENT_BUILD`. Add a CI grep gate.

---

## 2. Data model (Firestore; collection → document id → fields)

Every write that changes money or a score goes through a server transaction. The client gets no Firestore rules access at all: the rules are `allow read, write: if false`. Only the Cloud Run service account touches the DB.

| Collection | Key | Fields |
|---|---|---|
| `accounts` | `playerId` (128-bit, base32) | `displayName`, `displayNameNorm`, `createdAt`, `status` (active / shadowbanned / deleted_pending), `primaryProvider`, `isGuest`, `riskScore`, `ageGate`, `consent{analytics, marketing, ts}` |
| `identities` | `{provider}:{providerSubject}` (unique by construction) | `playerId`, `linkedAt`, `emailHash?`. **One identity maps to at most one account** (H4). |
| `sessions` | `refreshFamilyId` | `playerId`, `currentRefreshHash`, `rotatedAt`, `revoked`, `deviceInstallId` |
| `wallets` | `playerId` | `coins`, `shards`, `gems`, `extraCharges`, `cosmetics[]`, `powerUps[]`, `version` (optimistic concurrency) |
| `ledger` | auto id (sub-collection `wallets/{id}/ledger`) | `ts`, `delta{currency: n}`, `reason` (daily_reward, inbox, first_clear, tier, purchase, refund_clawback, consume_charge, admin), `refId`, `idempotencyKey`, `balanceAfter`. Append-only. |
| `inventory` | inside `wallets` (small) | if it grows, move it to `inventory/{playerId}/items/{itemId}` with `source` and `grantedAt` |
| `entitlements` | `{playerId}:{sku or seasonId}` | `type` (season_premium, cosmetic_bundle), `purchaseRef`, `state` (active / revoked), `grantedAt` |
| `purchases` | `sha256(purchaseToken)` (**unique**) | `playerId`, `productId`, `orderId`, `state` (pending / granted / consumed / voided), `playResponse` (redacted), `acknowledgedAt`, `voidedAt`, `voidReason` |
| `inbox` | `playerId/items/{itemId}` | `source`, `reward`, `expiresUnixUtc`, `claimed`, `claimedAt` |
| `dailyReward` | `playerId` | `streakIndex`, `lastClaimDayKey` |
| `titanDays` | `dayKey` | `configJson`, `configHash`, `overrideBy?` (admin), `closedProcessed`, `topPercentCutoff` |
| `runs` | `runId` | `playerId`, `mode` (titan / campaign / event), `dayKey` or `levelId`, `levelVersion`, `seed`, `configHash`, `simVersion`, `physicsHz`, `issuedAt`, `shotBudget`, server state `{lastShotIndex, lastDamage, lastShotsLeft, lastKilled, lastShotServerTs}`, `checkpoints[]` (with inputs), `integrityVerdict`, `status`, `serverScore`, `replay{state, replayScore, delta, workerVersion}` |
| `rankedStatus` | `{playerId}:{dayKey}` | mirrors `RankedStatus`. Creating it inside a transaction enforces one attempt per day. |
| `campaignBest` | `playerId/levels/{levelId}` | `best`, `runId`, `firstClearAt`. The total is kept denormalised on the account. |
| `eventBest` | `playerId/{seasonId}/{levelId}` | `stars`, `runId` |
| `seasonProgress` | `{playerId}:{seasonId}` | `xp`, `claimedFree[]`, `claimedPremium[]`, `bonusChests`, `endProcessed` |
| `boards` | `{type}:{periodKey}/entries/{playerId}` | `score`, `reachedAt` (server ts; ties go to the earlier one, BUG-16), `name`, `visible` (false while guest, shadowbanned or pending), `validated` |
| `config` | `season`, `catalog`, `titanTemplates`, `dailyRewards`, `events` | versioned documents, edited via admin endpoints or the console; `catalogVersion` |
| `audit` | auto | link and unlink, merge choices, admin overrides, refunds, bans |

**Leaderboards.**
- Reads are a Firestore query `orderBy(score desc, reachedAt asc).limit(100)`. "My rank" uses a `count()` aggregation of entries with a higher score.
- Each finalised board period gets a materialised snapshot document, which serves Previous-period reads cheaply (SG-18).
- Friends: the provider friend lists are an open question (Q-D6). Until that is settled, the friends filter covers mutual in-game friends only.

**Retention and deletion.**
- Runs and input logs are kept for 90 days. Snapshots of the top 100 runs per board are kept until the board is archived plus 1 year (anti-cheat review).
- Account deletion removes the account, its identities, its board entries and its PII, but keeps anonymised ledger and purchase rows for tax and fraud purposes (review §7). Security Admin needs to confirm this (Q-S3).

---

## 3. Per-attempt tokens and server-side replay scoring (C1, H1, H2)

### 3.1 Run token
- `token` = base64url(`{v:1, kid, runId, playerId, mode, dayKey|levelId, configHash, seed, iat, exp}`) + `.` + signature.
- The signature is Ed25519 or ES256 from **Cloud KMS**, so the private key never leaves KMS.
- The per-run checkpoint receipt is HMAC-SHA256 over `token|shotIndex|damageScore|shotsLeft|killed|inputsHash`. Its key is derived as HKDF(masterKey in Secret Manager, runId) and is never stored per run and never sent to the client. This fixes C1: the per-run keys are no longer in the device DB.
- Server state lives in `runs/{runId}`. The client journal can only add an offline tail that passes the plausibility checks, exactly as SB:290-318 does today.

### 3.2 What the client sends per shot (proposal P8b)
`inputs = {yaw, pitch, power, powerUpId, fireFrame, settleFrames}`
- Angles are sent as fixed-point integers (millidegrees) so encoding is deterministic.
- `fireFrame` and `settleFrames` count fixed-update frames since level load.

The client also sends its own outcome: `damageScore`, `killed` and `shotsLeft`.

### 3.3 Determinism: the honest constraint
CRASHVERSE uses Unity PhysX rigidbodies (Destruction, Projectile, StructuralIntegrity). PhysX is **not bit-exact across CPUs and architectures** (ARM devices vs. an x86 server), so exact replay is not achievable. The plan is **tolerance-based re-simulation**:
1. **Replay worker.** A headless Unity *Linux Dedicated Server* build of the same commit (`simVersion` = git SHA plus content hash). It is built by GameCI in GitHub Actions and run as a **Cloud Run Job**.
2. **Pinned settings.**
   - Ranked and campaign runs use a fixed 50 Hz step (P9), `Physics.simulationMode = Script` with manual `Physics.Simulate(0.02f)`, and fixed solver iterations.
   - `Random` is seeded from `seed`, with no `UnityEngine.Random` calls outside the seeded RNG.
   - Debris pooling must not affect the sim. Visual-only debris goes on a separate layer that does not collide with scoring pieces.
3. **Replay process.** The worker loads the level, applies each shot's inputs at its `fireFrame`, simulates to settle, and computes the per-shot and final `damageScore`, `killed`, `destroyedRatio`, `bestCombo` and mastery.
4. **Tolerances** are initial values. QA must calibrate them on 3+ real devices against the server build (Q-S1, Q-D3).

   | Value | Rule |
   |---|---|
   | Titan per-shot damage | \|client - replay\| ≤ max(150, 5% of replay) |
   | Titan final score | ≤ 3% |
   | `killed`, `won` | must match exactly |
   | `destroyedRatio` | ±0.02 |
   | `bestCombo` | ±1 |
   | Mastery flag | must match, except physics-sensitive goals (L7 crown 80%, L2 kill plane), which take the replay value |
   | Shot count, order, power-ups used | must match exactly |

5. **Score used.** When the run is inside tolerance, the **lower of client and replay** counts. That removes any incentive to inflate. When it is outside tolerance, the score is shadow-boarded and flagged for review, and repeated cases raise `riskScore`.
6. **Cheap checks on every run,** whether or not it is replayed:
   - Server-measured minimum interval between shots, starting at 1.5 s (H2).
   - Shot budget and order.
   - Per-shot damage cap (`offlineShotDamageCap`).
   - Score ceilings per level from design data (`CampaignScoreCeiling`, SB:436).
   - Minimum run duration.
   - Integrity verdict present.

### 3.4 Which runs get replayed (cost control)

| Runs | When |
|---|---|
| Titan top 100 of the day | always |
| Score > p99 or a new personal best jump > 3σ | always |
| Daily random sample | 5% |
| Campaign first clears of levels 8-10 and boss levels | always |
| Campaign board top 100 changes | always |
| Event levels | top 100 per week |

- **Board visibility:** a run is `pending_validation` until its replay passes. Top-10% bonuses (SG-12) are computed after the replays at close + grace (00:15 UTC), and paid within an hour (SG-12 allows that).
- **Worker cost** [estimate]: about 30 s of 1 vCPU and 2 GiB per Titan run.
  - At 1k DAU, roughly 150 replays a day is 4,500 runs a month, or 135k vCPU-s and 270k GiB-s.
  - That fits within the Cloud Run free tier only if the API itself uses little. Budget about $0 to $4 a month.

### 3.5 Campaign and events (H1)
- **Interim (now):** keep `CampaignRunReport` with bounds checks (SB:559-573), plus per-level ceilings, minimum duration, rate limits and shadow-banning. First-clear rewards are granted, but the **board entry stays `validated=false`** until a replay passes.
- **Target:** `POST /campaign/attempts` issues `attemptToken` and `seed`, and the submit carries `inputs[]`. The server recomputes the score with the shared `CampaignScore.Compute` using the **replay's** stats. The client's `destroyedRatio`, `bestCombo` and `mastery` become hints only.
- Offline campaign play remains possible (design doc). See Q-D4 for how offline attempts get tokens. Proposal: the client keeps a small pool of 5 pre-issued attempt tokens.

---

## 4. Google Play purchases, voided purchases and refunds (C2)
1. **Client side.** Unity IAP (Play Billing Library 7+) sets `obfuscatedAccountId = HMAC(serverPepper, playerId)`, returned by `/account` as `obfuscatedAccountId` (proposal). The `OnPurchasePending` handler sends `{productId, purchaseToken, orderId}` to `/purchases/google/verify` and **leaves the transaction pending until the server answers**. It never calls `GrantPremiumPassStub` and never grants anything locally.
2. **Server verification.** The server calls Android Publisher `purchases.products.get` (for one-time products such as gem packs and the season pass). There are no subscriptions at present. It uses a service account that exists only in Secret Manager, or better, workload identity with the Play Console granting the service account access, so no key file is needed. It checks:
   - `purchaseState == 0` (purchased)
   - `packageName` and `productId` are in the server catalogue with `cosmetic == true` or are currency (StoreValidator rule, Addendum §4)
   - `obfuscatedExternalAccountId` matches the caller
   - `purchases/{sha256(token)}` doesn't already exist.
3. **Grant.** In one transaction: create the purchase document, then credit `gems` (ledger reason `purchase`) or create the `entitlements` document (season premium). After that, the server calls `consume` for gems or `acknowledge` for the pass. Both must happen within 3 days, or Play refunds automatically.
4. **RTDN.** Play pushes notifications to a Pub/Sub topic, and a push subscription delivers them to `/internal/rtdn`, authenticated with an OIDC token. The handler deals with `ONE_TIME_PRODUCT_PURCHASED` (a backup for verification the client missed) and `CANCELED`.
5. **Voided purchases.** A Cloud Scheduler job runs every 6 hours, calls `purchases.voidedpurchases.list` (`startTime` = last checkpoint) and, for each voided token:
   - marks the purchase voided
   - **claws back** gems, which may take the balance negative. A negative balance blocks spending, so the player can't buy anything until it is back above zero.
   - or revokes the entitlement. Season premium tiers that were already claimed: cosmetics granted from them get revoked, and that needs PM and Design approval because of the "owned items are never removed" rule (SG-17). This is **Q-PM**.
   - writes an audit entry, and raises `riskScore` on repeat refunds.
6. **Restore.** `GET /purchases` returns what the server knows. On a new device, the client also sends any `queryPurchasesAsync` tokens that are still unacknowledged to `/verify`.
7. **Testing.** Use Play Console license testers and static test product IDs. Everything is done in the Play Console web UI.

---

## 5. Auth and account linking (H4, M4)
- **Guest.** `POST /auth/guest` → the server mints the `playerId` (128-bit) and a refresh token. The client stores it with Android Keystore / EncryptedSharedPreferences through a small Java plugin (Developer).
- **Google.** The client uses Credential Manager / Sign in with Google to get an **ID token** with a `nonce` = sha256(server-issued linkNonce). The server checks the JWKS signature, `iss ∈ {accounts.google.com, https://accounts.google.com}`, `aud` = our web client ID, `exp`, and the nonce. The account is keyed on `sub`.
- **Facebook.** The client uses Facebook Login and gets an access token. The server calls `GET graph.facebook.com/debug_token?input_token=…&access_token={app-id}|{app-secret}` and checks `is_valid`, `app_id` and expiry. The account is keyed on the app-scoped `user_id`. The app secret lives in Secret Manager. Facebook *Limited Login* (OIDC JWT) can be considered later. It applies to iOS only, so it is out of scope.
- **X.** OAuth 2.0 Authorization Code with PKCE. The client opens a Custom Tab, receives the `code` via an App Link to `https://api.<domain>/v1/auth/x/callback`, then hands it to the app. The **server** exchanges the code (the client secret stays server-side) and calls `GET /2/users/me`. The account is keyed on the user `id`. X API access tiers and costs change often, so **verify before committing** (Q-PM). This is flagged as a dependency.
- **Email** (optional). OTP by email through a transactional provider, rate-limited, with a generic response whether or not the account exists.
- **Linking rules** (review §4):
  - A link needs a valid session plus a fresh provider proof.
  - When the identity already exists on another account, the server returns 409 with summaries of both accounts, and the client shows "keep this device's progress" or "switch to existing". A silent merge never happens.
  - SG-19 and SG-36: on link, the guest's entries on **currently open** boards are set `visible=true` with the new name, all campaign bests carry over, and closed boards don't.
  - Unlink needs re-authentication, the last login method can't be unlinked, and both link and unlink write to the audit log and revoke other sessions.
- **Firebase Auth?** It is not required. It would give free Google, Facebook and email sign-in and an ID token our API verifies, but X would still need a custom flow, and it adds a second identity system. Recommendation: **don't use Firebase Auth in v1.** Revisit if Developer prefers its Unity SDK (Q-D1). Firebase Analytics and Crashlytics can still be used independently.
- **Display names:** NFKC normalisation, 3-16 characters, a profanity list, and confusable checks.

---

## 6. Server time, resets and Play Integrity
- **Time.**
  - `GET /time` plus the `X-Server-Time` header on every response. The server uses only its own NTP-synced clock, and Cloud Run's clock is Google-managed.
  - Day key = `yyyy-MM-dd` of UTC (SG-15). Weeks and seasons start Monday 00:00 UTC (SG-18).
  - The ranked grace window is inclusive to 00:15:00.000 UTC (SG-22), judged by the **server receive timestamp** of `/finalize`.
  - An offline-queued ranked journal (SG-13) is judged by the server's `lastShotServerTs` from the checkpoints. Shots with no server timestamp are only accepted if finalize arrives within the grace window.
  - The client rejects a server offset greater than 24 h from its monotonic anchor as tampering (M2).
- **Resets.** Day boundaries are evaluated lazily on every request, the way LBS `Maintenance()` works, so no cron is needed for correctness. **Cloud Scheduler** jobs handle only the batch work:
  - 00:16 UTC: close the board, start top-N replays, then compute the top 10% and fill inboxes by 01:00.
  - Monday 00:16 UTC: weekly and season rollover, sending unclaimed tiers to the inbox (SG-17).
  - Every 6 h: voided purchases.

  Each job is idempotent and guarded by `closedProcessed` / `endProcessed` flags (BUG-15).
- **Play Integrity.**
  - **Standard requests** use `requestHash` = sha256(`runId` or a canonical request body). Classic requests are used on `/titan/ranked/start` and `/purchases/verify` with a server nonce.
  - The server decodes the token via `playintegrity.googleapis.com decodeIntegrityToken` with the service account. It checks `requestDetails` (hash or nonce, timestamp within 5 min), `appIntegrity.appRecognitionVerdict == PLAY_RECOGNIZED`, the package name and certificate digest, `deviceIntegrity` contains `MEETS_DEVICE_INTEGRITY`, and `accountDetails.appLicensingVerdict == LICENSED`.
  - Enforcement is phased:
    1. log only
    2. failing devices get practice-only ranked (`status=RefundedAsPractice`), and boards hide them
    3. block.
  - Quota: **10,000 requests per day by default** [verified, §12]. That is enough for about 3k DAU at 3 calls each. Request an increase in the Play Console before launch.

---

## 7. Secrets and keys (cloud key store)

| Secret | Where | Notes |
|---|---|---|
| Run-token signing key (Ed25519/ES256) | **Cloud KMS** asymmetric key, `kid` in the token | Rotate every 90 days. Public key embedded in the server only. |
| Access-JWT signing key | Cloud KMS | Separate key per purpose. |
| Receipt HMAC master, `obfuscatedAccountId` pepper | Secret Manager | HKDF per run. |
| Play Developer API / Play Integrity access | **Workload identity:** the Cloud Run service account is added in the Play Console. No JSON key file. | If a key is unavoidable, put it in Secret Manager. Never in GitHub. |
| Facebook app secret, X client secret, email-provider API key | Secret Manager | Mounted as env vars at deploy time. |
| GitHub Actions → GCP deploy | **Workload Identity Federation (OIDC)**, so no long-lived key is in GitHub secrets | `permissions: id-token: write, contents: read`; protected `production` environment with a required reviewer (the owner approves from the GitHub mobile app). |

- Secret Manager free tier: 6 active versions and 10,000 accesses per month; after that $0.06 per version per month and $0.03 per 10k accesses [verified].
- The server reads secrets at startup and caches them, so access counts stay tiny.
- KMS asymmetric signing costs about $0.03 per 10k operations plus a small monthly fee per key version. That price is from **[memory, not re-verified today]**, so it needs checking before provisioning.

---

## 8. Security findings: how each is closed

| Finding | Backend / client fix in this plan | Owner |
|---|---|---|
| **C1** plaintext DB and per-run keys on device | All run state, keys, wallet and boards live server-side (Firestore, KMS, Secret Manager). Receipt keys are HKDF-derived server-side. `LocalBackendService` is compiled out of release builds, and release fails closed (P10), with a CI grep gate. | Backend + Developer |
| **C2** premium with no proof | Removed: the premium flag is computed from `entitlements` only. `/purchases/google/verify` runs the Play API checks, a unique token, acknowledge or consume, RTDN and voided-purchase clawback (§4). The stub is stripped. | Backend + Developer |
| **H1** campaign trusts client stats | Attempt token + input log + replay recompute with the shared `CampaignScore` (§3.5). Interim: ceilings, minimum duration, pending-validation board flag, shadow-ban. | Backend + Developer |
| **H2** Titan run plausible but not proven | Server-measured shot interval, Integrity at start, inputs per checkpoint, replay of top 100, p99 and 5% sampled runs with tolerances, lower-of scoring, anomaly scoring (§3.3-3.4). | Backend |
| **H3** override from a device file | Config is built server-side only. Admin overrides go in `titanDays/{dayKey}` and are audited. `configHash` is bound into the run token. The client file path is removed in release builds. | Backend + Developer |
| **H4** fake linking | Provider tokens are verified server-side (Google ID token, FB `debug_token`, X PKCE code exchange). Unique identity mapping, explicit conflict choice, re-auth to unlink, audit log (§5). Needs P2. | Backend + Developer |
| M1 / M2 / M4 (related) | Test clock stripped; time comes from our own API with pinning; 128-bit server-issued id and Keystore refresh token. | Developer + Backend |

Additional server controls: per-account, per-IP and per-device rate limits (auth 5/min; ranked start 1/day; checkpoints ≤ shotBudget+2 per run). For a WAF: Cloud Armor costs money, so v1 uses in-app limits and Cloud Run `max-instances` as a cost cap. Also: nonce replay cache, Idempotency-Keys, and structured logs with no PII.

---

## 9. Hosting options (small scale = ~1,000 DAU, ~30k MAU) [estimates]

Load assumption: 1k DAU × about 60 API calls a day, which is 1.8M requests a month. Each call takes about 50 ms of CPU, so ~90k vCPU-s. Firestore sees about 120 reads and 40 writes per DAU per day, which is 120k reads and 40k writes a day.

| Option | Fit for CRASHVERSE | Free tier [verified] | Est. monthly @ 1k DAU [estimate] | Phone-only manageable? |
|---|---|---|---|---|
| **A. Cloud Run (.NET) + Firestore + Secret Manager/KMS + Pub/Sub + Scheduler (Firebase Blaze project)**, **recommended** | C# code sharing; Play APIs are native GCP (RTDN Pub/Sub, workload identity); Cloud Run Jobs can host the headless Unity replay worker | Cloud Run (request billing): 2M requests, 180k vCPU-s, 360k GiB-s per month. Firestore: 50k reads, 20k writes, 20k deletes per day, 1 GiB. Secret Manager: 6 versions, 10k accesses. | API ≈ $0 (inside the free tier). Firestore ≈ (70k extra reads × $0.03/100k + 20k extra writes × $0.09/100k) × 30 ≈ **$1.20**. Replay worker $0-4. Secrets/KMS < $1. **Total ≈ $1-6.** At 100 DAU: **$0**. | Yes. Cloud Console and Firebase Console both work in mobile browsers, and GitHub Actions deploys via WIF. |
| B. Firebase Cloud Functions (Node/TS) + Firestore | Same data store, but no C# sharing (we'd port the scoring to TS and risk drift); the replay worker still needs Cloud Run | Functions: 2M invocations, 400k GB-s, 200k CPU-s per month on Blaze only (not available on Spark) | ≈ same as A, **$1-6** | Yes |
| C. Supabase (Postgres + Edge Functions, Deno) | Postgres suits boards and ledgers (SQL, unique constraints). The **Free tier pauses after 1 week of inactivity**, which is unacceptable for a live game. No place to run the Unity replay worker (still need Cloud Run). | Free: 500 MB DB, 50k MAU, 500k Edge invocations. Pro: **$25/mo** (8 GB, 100k MAU, 2M invocations; $10 compute credit covers a Micro instance) | **$25** (Pro) + replay worker on GCP $0-4 | Yes (Supabase dashboard is web) |
| D. Cloudflare Workers + D1 | Cheapest at scale, fast. But TS/JS (no C# sharing). Free tier allows only **10 ms CPU per invocation**, too tight for JWT/JWKS verification and integrity decode. No Pub/Sub for RTDN (needs a GCP bridge). Replay worker still on GCP. | Free: 100k requests/day; D1 5M rows read and 100k written per day, 5 GB. Paid: **$5/mo** with 10M requests and 30M CPU-ms | **$5** + GCP bridge and replay worker $0-4 | Yes |

**Recommendation: Option A.**
- Start on Firebase **Blaze** with a budget alert at **$5 and $20**.
- Cloud Run has a `max-instances` setting (start at 3) as a hard cost ceiling. The replay job runs on a schedule with a limited task count.
- Spark alone is not viable, because there is no Functions or Cloud Run and no Pub/Sub on that plan.
- **New GCP billing accounts get $300 of trial credit** [verified on the Firebase pricing page], which covers the first months.
- If the owner refuses to add a card: the fallback is **Cloudflare Workers Free + D1**, but RTDN and Play Integrity decoding still need a GCP project. That is the *same* card requirement, so A is strictly better.

---

## 10. Delivery slices (no provisioning until PM signs off)

| Slice | Contents | Cost |
|---|---|---|
| **S0 (now)** | This plan; `openapi.yaml` derived from §1; .NET solution `CrashVerse.Server` with in-memory store + Firestore emulator; shared `CrashVerse.Rules` assembly (scoring and validation) referenced by Unity via an asmdef/DLL; contract tests that run the same scenarios as the Unity EditMode tests (`LocalBackendService` vs HTTP stub). Runs in GitHub Actions (no PC). | $0 |
| **S1** | Auth (guest, refresh), time, wallet, inbox, daily reward, Titan config/status/start/checkpoint/finalize, leaderboards | $0 |
| **S2** | Play purchase verify, RTDN, voided purchases | needs Play Console |
| **S3** | Google, FB and X linking | |
| **S4** | Replay worker: Unity Linux server build in CI → Artifact Registry → Cloud Run Job | |
| **S5** | Play Integrity enforcement phases, anomaly scoring, admin endpoints | |

Milestone fit: S1-S2 = M5 (Daily Titan); S3-S5 = M6 (accounts, validated boards).

## 11. Phone-only operations
- **Deploy:** push to `main` → GitHub Actions runs build and tests → a `production` environment approval (GitHub mobile app) → `gcloud run deploy` via Workload Identity Federation.
- **Config changes** (Titan override, season, catalogue): an admin JSON PR in the repo, merged from GitHub mobile. CI validates the schema (cosmetic-only store rule) and pushes it to the `config` collection. This gives review and an audit trail. Emergency edits are possible in the Firebase Console Firestore editor (mobile web).
- **Monitoring:** Cloud Monitoring uptime check + alert email, budget alert emails, Error Reporting. The Google Cloud mobile app or console in a browser.
- **Play Console:** RTDN topic setup, service-account permissions and testers are all web.
- **One-time setup** the owner must do on the phone (later, after sign-off): create the GCP/Firebase project, add billing, enable APIs, create the WIF pool, and link Play Console API access. I'll write a step-by-step tap guide when we get there.

## 12. Sources (checked 2026-10-16)
- Cloud Run pricing (free tier 2M requests, 180k vCPU-s, 360k GiB-s on request billing; $0.40 per million requests): https://cloud.google.com/run/pricing
- Firebase pricing (Spark vs Blaze, Firestore 50k reads / 20k writes per day, Functions Blaze-only 2M invocations, $300 credit): https://firebase.google.com/pricing
- Firestore pricing ($0.03 per 100k reads, $0.09 per 100k writes beyond the free tier): https://cloud.google.com/firestore/pricing
- Secret Manager pricing (6 versions, 10k accesses free; $0.06 per version per month, $0.03 per 10k): https://cloud.google.com/secret-manager/pricing
- Supabase pricing (Free pauses after 1 week of inactivity; Pro $25): https://supabase.com/pricing and https://supabase.com/docs/guides/platform/free-project-pausing
- Cloudflare Workers and D1 pricing (Free 100k requests/day, 10 ms CPU; Paid $5): https://developers.cloudflare.com/workers/platform/pricing/ and https://developers.cloudflare.com/d1/platform/pricing/
- Play Integrity quota (10,000 per day default): https://developer.android.com/google/play/integrity/setup
- **Not re-verified today:** Cloud KMS, Cloud Scheduler (I believe 3 free jobs per billing account) and Pub/Sub (I believe 10 GiB free per month) pricing, and X API tier pricing. Verify these before provisioning.

---

## 13. Interface coordination: questions for **Developer** (3baf6ed7…)
- **Q-D1** Will you accept the P1-P10 model and interface changes, especially **P2** (`LinkAccount` with a credential), **P6** (wrapped lists), **P8/P8b** (seed and inputs), **P3** (`gems`)? Would you rather use Newtonsoft JSON (which lifts the JsonUtility top-level array, enum and dictionary limits)? Do you want Firebase Auth SDKs, or our own `/auth` endpoints?
- **Q-D2** Can `CampaignScore`, `TitanScore`, `WinRule`, `StarRules` and `StoreValidator` move into a UnityEngine-free asmdef (`Crashverse.Rules`) that the server compiles too?
- **Q-D3** Determinism. Can ranked, campaign and event runs force 50 Hz fixed step (P9 vs `QualityController`), `Physics.simulationMode = Script`, a seeded RNG only, and scoring pieces separated from visual debris? Is a headless Linux Server build of the gameplay scene feasible, and what would it cost GameCI in licence and build time?
- **Q-D4** Offline campaign: is a pool of pre-issued attempt tokens acceptable, or should offline wins stay "local only" until the next online replay?
- **Q-D5** Secure storage. Who builds the Keystore / EncryptedSharedPreferences plugin for the refresh token? And can the client do certificate pinning with `UnityWebRequest` `CertificateHandler`?
- **Q-D6** Friends filter: source of friends (in-game friend codes vs. provider graphs; FB `user_friends` needs App Review).
- **Q-D7** Should `submittedTicks` (.NET ticks) stay, or can we switch to `submittedUnixMs`?
- **Q-D8** Unity IAP version, and do you agree the client holds purchases *pending* until `/purchases/google/verify` answers?

## 14. Interface coordination: questions for **Security Admin** (ad6d91d5…)
- **Q-S1** Are the replay tolerances (§3.3) and the "lower of client and replay" rule acceptable? Sampling rate 5%?
- **Q-S2** Integrity enforcement: which phase at launch, and is `MEETS_DEVICE_INTEGRITY` + `PLAY_RECOGNIZED` + `LICENSED` the right bar? Emulator players get practice-only.
- **Q-S3** Data retention: 90 days of input logs; anonymised ledger and purchase rows kept after account deletion. Is that OK under GDPR and DPDP? Who is the grievance officer contact?
- **Q-S4** KMS-signed tokens with Ed25519/ES256 vs HMAC-in-Secret-Manager. Is 90-day rotation sufficient?
- **Q-S5** Is it acceptable to skip a paid WAF (Cloud Armor) in v1, relying on in-app rate limits and `max-instances`?
- **Q-S6** Refund clawback when gems are already spent: negative balance and a spending lock, or revoking cosmetics? (This conflicts with SG-17 "owned items never removed" and also needs PM.)
- **Q-S7** Workload identity for the Play APIs (no JSON key) and WIF for GitHub. Please confirm, and review the CI `production` environment design.

## 15. Questions for PM
- Approve Option A, and have the owner add a billing card with budget alerts?
- X login: confirm it is worth the X API dependency and cost (prices not verified).
- Refund policy for cosmetics vs SG-17.

## PM Decisions (2026-10-16)
1. Hosting: Option A is recommended but needs the owner's approval, so nothing gets provisioned until PM confirms.
2. Launch logins: Guest, Google, Facebook and Email. X login stays behind a server feature flag (`auth.x.enabled=false`), so it adds no X API cost.
3. Refunds: a refunded cosmetic is revoked and unspent refunded currency is clawed back, and both are written to the audit log. SG-17's never-remove rule covers legitimately owned items only (Game designer to clarify).
4. P9 (ranked runs locked to 50 Hz) and P2 (LinkAccount takes a provider token) are approved. P1-P10 are still being agreed with Developer.
5. Refunds (2026-10-16): Security's S6 model replaces decision 3 and is now the default. Refunded gems become a negative gem debt, gem spending is locked until it is repaid, items bought with gems are kept, and cosmetics bought directly with cash are revoked. Decision 3 (claw back unspent currency only, stopping at zero) stays available behind the server flag `REFUND_POLICY=floorAtZero`.

## Developer Agreement (2026-10-16)
- Developer accepted P1-P10. P5 is changed: SubmitEventScore is dropped, and event scores go through attempt tokens plus inputs (P4). P10 gating uses `CRASHVERSE_DEV || DEVELOPMENT_BUILD`, with a fail-closed check and a CI grep gate.
- D1: the client uses Newtonsoft JSON and our own login endpoints, not Firebase Auth. D2: shared rules code lives in the `Crashverse.Rules` asmdef (netstandard2.1, no engine references). D3: scored runs use a fixed 0.02 step and seeded System.Random. Replay is checked against a tolerance, not for exact equality.
- D4: up to 20 pre-issued, single-use, expiring campaign tokens for offline play. Ranked play is online-only. D5: the refresh token is stored in the Android Keystore, and certificates are pinned by public key with a backup pin.
- D6: friends are mutual-add friend codes stored on the server. D7: `submittedUnixMs` is set by the server. D8: Unity IAP 4.12+, purchases stay pending until the server verifies them, and only the server grants items.
- Next: Developer writes docs/Server_Interface_v0.md plus the C# interfaces.
- SG-17 updated by Game designer: refund clawback stops at zero and never makes a balance negative. Only unspent currency is clawed back, which matches PM decision 3.
