# Security Review of Backend Plan v1

- **Reviewer:** Security Admin
- **Plan reviewed:** `backend/Backend_Plan_v1.md` (draft, including the PM Decisions of 2026-10-16)
- **Baseline:** `security/SECURITY_REVIEW_v1.md` (commit `0d60e07`)
- **Date:** 2026-10-16

## Verdict: APPROVE WITH CONDITIONS

The architecture closes C1, C2 and H1-H4 on paper. Each finding is closed once the plan's slice ships and the blocking conditions below are met. Nothing has been provisioned, so these conditions are gates for S1 to S5. They do not block S0.

## 1. Closure check

**C1: plaintext DB and run keys on the device**
- **Plan coverage:** all state lives server-side (Firestore with deny-all rules). The receipt key is derived as HKDF(Secret Manager master, runId). Run tokens are signed in KMS. P10 adds a fail-closed release check and a CI grep gate.
- **Status:** closed once S1 and P10 ship.
- **Conditions:** B1 and B2.

**C2: premium granted with no purchase proof**
- **Plan coverage:** premium comes only from `entitlements`. `/purchases/google/verify` checks state, package, product and `obfuscatedExternalAccountId`, and enforces a unique `sha256(token)`. Then it acknowledges or consumes. RTDN and voided-purchase polling are included, and the stub is stripped.
- **Status:** closed once S2 ships.
- **Conditions:** B5. The client also must hold purchases pending until the server answers (Q-D8).

**H1: campaign trusts client stats**
- **Plan coverage:** attempt token, input log, replay, then the shared `CampaignScore` computed from the replay's stats. In the interim, board entries stay `validated=false`.
- **Status:** partly closed until S4.
- **Conditions:** C1 and C2.

**H2: Titan runs are plausible but not proven**
- **Plan coverage:** server-measured 1.5 s minimum between shots, Integrity check at run start, inputs on each checkpoint, replay of the top 100, p99 runs and a 5% sample, and lower-of scoring.
- **Status:** closed after S4 and S5.
- **Conditions:** S1 changes and B3.

**H3: Titan override read from a device file**
- **Plan coverage:** overrides live in `titanDays/{dayKey}` and are audited. `configHash` is bound into the run token. The client file path is removed.
- **Status:** closed.
- **Conditions:** B4 (console edits).

**H4: account linking is faked**
- **Plan coverage:** the server verifies each provider token. Identities map uniquely, conflicts need an explicit choice, unlinking needs re-auth, and both write to the audit log. P2 is approved by PM.
- **Status:** closed once S3 ships.
- **Condition:** P2 must actually ship. X is behind a feature flag, so its flow must still pass review before the flag is turned on.

M1, M2 and M4 are also addressed (§8 of the plan).

## 2. Gaps found

1. **The nonce cache and rate limits must be shared across instances.** Plan §1 and §8 imply in-process limits. With Cloud Run scaling up to `max-instances=3`, per-instance counters can be bypassed. Store nonces in Firestore with a TTL policy (10 minutes), or use counters per account, IP or device. The once-a-day ranked attempt is already transactional, which is good.
2. **`max-instances=3` doubles as a DoS lever.** A flood of cheap requests causes an outage, not a bill. Order the work so the cheap rejects come first: JWT validation, then rate limiting, then the work itself. Unauthenticated `/auth/guest` and `/time` need the strictest limits (see S5).
3. **Emergency edits in the Firestore console bypass the audit trail** (plan §11). Allow them only for break-glass. Enable Cloud Audit Logs Data Access for Firestore and alert on writes made by any principal other than the service account.
4. **Interim campaign first-clear rewards are granted before validation** (plan §3.5). This is acceptable only while those rewards are soft currency or cosmetics. Cap them per account per day, and claw them back if a later replay fails.
5. **Offline attempt-token pool** (Q-D4). The 5 pre-issued tokens must be single-use and bound to player, level and level version. Expire them within 7 days or less. Runs played on them must always go through validation before they appear on any board.
6. **RTDN push endpoint.** Verify the OIDC token's `aud` and its service-account `email`, and drop duplicate deliveries by `messageId`. Treat RTDN as a trigger only: always re-query the Play API before granting or revoking anything.
7. **Rotation of the HMAC master and pepper.** Run-token keys rotate (§7), but the receipt master and the `obfuscatedAccountId` pepper have no rotation plan. Give the receipt master a key ID (`kid`) in the receipt so it can rotate. Never rotate the pepper: changing it breaks the binding to existing purchases. Version it instead.
8. **Retention after deletion** (see S3). As written, "anonymised" rows would still be keyed by `playerId`, so they are only pseudonymised.
9. **Unverified prices.** The KMS, Scheduler, Pub/Sub and X API prices in plan §12 were not checked. Verify them before provisioning. This has no security impact.
10. **Client offset check** (plan §6): rejecting a server offset over 24 hours as tampering is fine as a hint. All enforcement stays server-side.

## 3. Decisions on the Backend Engineer's questions

### S1. Replay tolerances, lower-score rule and sampling
**Decision:** accept the tolerances and the lower-of rule, with changes to sampling and enforcement.

- **Tolerances.** Accept the plan's §3.3 values (per-shot max(150, 5%), final score 3%, exact match on kill/win, shots and power-ups) as **provisional**. QA calibrates them on at least 3 ARM devices against the x86 worker.
- **Shadow-board only until calibrated.** Until calibration is done, out-of-tolerance runs are shadow-boarded and reviewed, never auto-banned.
- **Recalibrate on every `simVersion` change.**
- **Lower-of rule: accepted.** It removes the incentive to inflate scores.
- **Sampling.** Replay **100% of every top-N leaderboard entry on every board** (Titan daily top 100, campaign top 100, weekly event top 100, season top 100) before the entry becomes visible. Also replay 100% of Titan runs that earn a top-10% bonus (SG-12) and all p99 or 3σ jumps.
- **Random sample.** For everything else, keep a random sample of at least 5%. Raise it to 100% for accounts with an elevated `riskScore`.
- **Inputs are mandatory.** Ranked submissions with no inputs, or with a mismatched `simVersion`, count as practice.

### S2. Play Integrity phase and verdict bar at launch
**Decision:** launch with enforcement for ranked play and a soft fail for unranked play.

**Ranked play (Titan start, any entry on a ranked board):**
- **Required:** `MEETS_DEVICE_INTEGRITY` and `PLAY_RECOGNIZED`, plus a matching package name and certificate digest, plus a request hash or nonce no older than 5 minutes.
- **If the check fails:** the run is downgraded to practice (`RefundedAsPractice`) and the entry is hidden, with a clear message to the player. This is the plan's phase 2, applied to ranked play from day one.
- **`LICENSED`:** log it and add it to `riskScore`, but don't make it a hard requirement at launch. False negatives happen and it adds no anti-cheat value beyond `PLAY_RECOGNIZED`.
- **No `MEETS_STRONG_INTEGRITY` requirement.**

**Unranked play (campaign, practice, store):**
- **Soft fail:** log only and feed `riskScore`.
- **Purchases are not blocked on Integrity.** The Play API receipt check is the authority there.

**Rollout and quota:**
- Run phase 1 (log only) during closed testing to measure the false-positive rate.
- Request a quota increase above 10k/day before launch.
- If the Integrity API is down or out of quota, fail closed to practice for ranked play only.

### S3. 90-day input-log retention and the ledger after deletion
**Decision:** acceptable under GDPR and DPDP with these conditions.

1. **Input logs and run records: 90 days is acceptable.** The legal basis is legitimate interest in fraud prevention (GDPR Art. 6(1)(f)), or a "legitimate use" or contract purpose under DPDP. The purpose and period must appear in the privacy notice. Inputs contain no PII.
2. **Top-100 snapshots** (kept until archive + 1 year): delete or de-link them when an account is deleted. Replace `playerId` and name with a random tombstone ID. Keep them longer only where a fraud case is open.
3. **Purchase rows:** keep them after deletion only as long as tax and accounting law requires (GDPR Art. 17(3)(b); DPDP §8(7) allows retention where law requires). Legal must confirm the period for India (GST and Companies Act) and for the EU. Strip all game PII and replace `playerId` with a one-way hash. Keep `orderId` because it is needed for chargebacks.
4. **Non-purchase ledger rows** (daily rewards, inbox and similar): delete them or aggregate them. They have no legal-retention basis. A truly anonymised aggregate is fine.
5. **Terminology:** call the result "pseudonymised" unless the re-identification link is destroyed. Pseudonymised data is still personal data under GDPR.
6. **Deletion SLA:** complete deletion within 30 days. Cover backups through PITR and backup expiry, and say so in the notice.
7. **DPDP specifics:**
   - Notice in clear language.
   - Consent that can be withdrawn as easily as it was given.
   - Verifiable parental consent and no tracking for users under 18 (the `ageGate` field must drive this).
   - Breach notification to the Data Protection Board and to affected users.
8. **Grievance officer / DPDP contact:** **OPEN ITEM for the user (business decision).** Not assigned in this review. It must be named in the privacy policy and in the Play listing before launch.

### S4. KMS signing or HMAC, and 90-day rotation
**Decision:** use KMS asymmetric signing; 90-day rotation is enough.

- **Run tokens and access JWTs:** sign with Cloud KMS asymmetric keys, preferring ES256 (Ed25519 KMS support should be checked). The private key is non-exportable, and verification uses a cached public key, so KMS is called once per signature only.
- **Separate keys** for each purpose, as the plan already has.
- **Receipt HMAC:** keep it in Secret Manager with an HKDF derivation per run (verification is hot-path and server-only). Add a `kid` (gap 7).
- **Rotation:**
  - Rotate signing keys every **90 days**. Keep the previous key active for verification for at least the maximum token lifetime plus the grace window: 24 hours or more for run tokens, 15 minutes for JWTs.
  - Rotate the HMAC master yearly, or immediately on suspicion of compromise.
  - Write and test an emergency-rotation runbook.
- **Access:** IAM `signerVerifier` is granted to the Cloud Run service account only, and KMS admin to the owner only.

### S5. Skipping a paid WAF in v1
**Decision:** acceptable for v1 at the planned scale, under these conditions.

1. Rate limits are kept in a shared store (gap 1). Unauthenticated endpoints get strict limits per IP and per `deviceInstallId`:
   - `/auth/guest`: 10 per hour per IP
   - `/time`: cacheable
   - `/auth/*`: 5 per minute.
2. Requests are rejected cheaply before any Firestore read. Request body size is capped (for example 64 KB, larger only for journal or replay inputs with a hard cap). The server has short timeouts.
3. Integrity is required for ranked start (S2), which raises the cost for bots.
4. Uptime, 5xx and 429-spike alerting and the budget alerts are in place (plan §11).
5. **Revisit triggers:** sustained abuse, more than 10k DAU, or any outage caused by traffic. At that point add Cloud Armor (via an external HTTPS load balancer) or a free Cloudflare proxy in front.

### S6. Refund clawback when gems are already spent (SG-17)
**Decision:** use a negative gem balance (a debt) and keep items bought with gems. Flag repeat abusers.

This follows the PM decision of 2026-10-16.

- **Refunded unspent gems:** clawed back.
- **Refunded gems that were already spent:** the balance goes **negative**. It is recorded as a debt in the ledger with reason `refund_clawback`.
- **While the balance is negative:**
  - Gem spending is locked.
  - New gem purchases and earned gems pay the debt off first.
  - Cosmetics bought earlier with gems are **kept**. They were "owned" when bought, which respects SG-17's never-remove rule.
- **Cosmetics bought directly with real money and then refunded:** revoked, and the revocation audited (PM decision 3).
- **Season premium track:** revoke the entitlement. Unclaimed premium tiers are lost. For cosmetics already claimed from premium tiers, apply the PM decision (revoke). The Game Designer should confirm the SG-17 wording.
- **Repeat abusers:**
  - 2 or more refunds in 90 days, or a debt above a threshold, sets a `refundAbuse` flag and raises `riskScore`.
  - The response is to require purchase verification plus Integrity, and the support team may restrict store access.
  - Never ban automatically from refunds alone.
- **Player communication:** tell the player in-game why the balance is negative.

### S7. Workload identity with no key files (Play APIs and GitHub Actions)
**Decision:** confirmed.

**Play APIs:**
- Cloud Run runs as a dedicated service account. That account is granted access in Play Console API access with least-privilege permissions (view financial data and manage orders only).
- The Play Integrity API is enabled on the same project.
- No JSON key is created anywhere. Set the org policy `iam.disableServiceAccountKeyCreation` to enforce this.

**GitHub Actions:**
- Use Workload Identity Federation with OIDC. The provider attribute condition must restrict to `repository == 'radhengle/crashverse'`, `ref == 'refs/heads/main'` and the `production` environment.
- The deploy account has `run.developer` plus `iam.serviceAccountUser` on the runtime account only. It has no Owner or Editor role.
- The workflow sets `permissions: id-token: write, contents: read`.
- The `production` environment requires reviewers (the owner) and allows deploys from `main` only.
- Actions are pinned to commit SHAs (review M5).
- No deploy job runs for pull requests from forks.

**Unity licence:**
- The secrets (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) must move from repository secrets to a protected build environment. This applies to both the client CI and the replay-worker CI.
- They cannot use OIDC, so they stay as GitHub environment secrets.

## 4. Blocking conditions (must be met before the relevant slice goes live)

- **B1 (S1, C1).** P10 must ship: the release build fails closed on `LocalBackendService`, and `SetTestClock`/test-offset hooks, the override file path and `GrantPremiumPassStub` are stripped. A CI grep gate must fail the release build if any of them remain.
- **B2 (S1).** Firestore rules deny all client access, verified by emulator tests. Rate limits and the nonce cache are shared across instances (gaps 1 and 2).
- **B3 (S4/S5, H2).** Before any public ranked board:
  - Integrity is enforced for ranked play (S2).
  - 100% of top-N entries on every board are replayed before they become visible (S1).
  - Until replay is live, boards show provisional entries that are marked as such, with SG-12 bonuses held back.
- **B4 (H3).** Console edits are break-glass only, with alerts on Firestore audit logs (gap 3).
- **B5 (S2, C2).** There is no client-side grant path. The pending-until-verified flow passes Play licence-tester cases (purchase, pending, cancel, refund, replayed token). Voided-purchase clawback follows S6.
- **B6 (launch).** Privacy items:
  - Privacy notice with the retention periods (S3).
  - Account deletion in-app and on the web.
  - Data Safety form.
  - Grievance officer named (**open item for the user**).
  - Age gate and parental-consent flow for users under 18 (DPDP).
- **B7 (CI).** Workload Identity Federation as in S7, with no service-account keys. Unity licence secrets are in a protected environment and actions are pinned by SHA.

Not blocking: price verification (gap 9); X login, which stays behind its flag until its flow is reviewed.
