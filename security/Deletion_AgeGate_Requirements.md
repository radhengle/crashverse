> **DRAFT: NEEDS LEGAL REVIEW.**
> - Source: Security Review v1, Backend Plan v1 Review (S3, B6) and PM decisions of 2026-10-16.
> - Owners: **Dev** = Developer (Unity client); **BE** = Backend Engineer.
> - Each requirement has acceptance criteria (AC).

# Account deletion, age gate and parental consent: requirements

## A. In-app account deletion (Dev and BE)

**A1.** Settings → Account → "Delete account" is reachable in no more than 3 taps from the main menu, for both linked and guest accounts.
- **AC:** QA reaches it in ≤3 taps from the main menu on both account types.

**A2.** A confirmation screen explains:
- what is deleted
- what is retained (pseudonymised purchase records)
- that the action is irreversible after 30 days, or immediately if it is processed earlier
- that purchases are not refunded and are lost.

The user must type or tap-hold to confirm.
- **AC:** the screen copy matches the privacy notice section 6. The user cannot confirm with a single tap.

**A3.** A guest deletes with their current session only (PM decision). A linked account must re-authenticate with the provider, or with an email OTP, before deletion. The re-auth must be under 5 minutes old.
- **AC:** the server returns 401 `REAUTH_REQUIRED` without fresh re-auth.

**A4.** `DELETE /account` returns `{scheduledDeletionUnixUtc}`. The client then:
- signs out
- deletes local files (`campaign_save.json`, `titan_ranked_journal.json`, cached tokens in Keystore, and `local_server_db.json` if present in a dev build).
- **AC:** after deletion, the app restarts in the first-run state. No CRASHVERSE files remain in `persistentDataPath` or Keystore.

**A5.** A grace period of at most 7 days, inside the 30-day limit. Signing in during the grace period offers "cancel deletion". After the grace period the account cannot be recovered.
- **AC:** cancelling in the grace period restores the account. Signing in after the purge creates a new account.

**A6.** Deletion works while the game is offline-first: the client queues the request and shows "pending until online". It cannot fake completion.
- **AC:** an offline request is sent on the next connection, and the UI states that it is pending.

## B. Web deletion URL (Play requirement) (BE)

**B1.** A public HTTPS page at [[WEB DELETION URL]] is linked in the Play Console and in the privacy notice. It names the app and the developer, lists the steps, and states what is deleted and retained and for how long.
- **AC:** the page loads without the app installed, and the Play Console accepts the URL.

**B2.** Identity verification without the app:
- **Linked accounts:** sign in with the same provider, or with an email OTP. The page must not take a typed-in player ID as proof.
- **Guests:** see section C.
- **AC:** a request cannot be submitted for an account the requester has not proven they control.

**B3.** No partial deletion is offered (PM decision): the page offers full account deletion only. In the Play Console, declare that partial deletion is not available.
- **AC:** the web page and the app offer only full deletion. The unlink and hide-name controls are covered in F4 and F5.

**B4.** The page is rate-limited, protected by CAPTCHA, and audit-logged.

## C. Guest-account path (Dev and BE)

**C1.** In-app deletion works for guests: the session token is proof of control.

**C2.** Guest accounts are auto-deleted after **365 days** of inactivity (PM decision). Inactivity means no authenticated API call.
- At day 335 the server sets `deletionWarningAt`. On the next launch the app shows an in-app warning with the deletion date and a "keep my account" option. Any authenticated activity resets the clock.
- A guest who has uninstalled the app has no credential, so the web page explains that guest data is deleted after 365 days of inactivity, or can be deleted from the app.
- **AC:**
  - A guest inactive for 335 days sees the warning on launch.
  - Activity resets the timer.
  - At 365 days the guest account goes through the same deletion job as section D.
  - The job is tested with the clock moved forward.

**C3.** The app shows guests a "link an account to keep progress and manage your data from any device" prompt. This prompt is never shown to minors without consent (see E5).

## D. Server deletion job (BE)

**D1.** A scheduled, idempotent job processes requests whose grace period has expired. It finishes within 30 days of the request (target: under 7 days).
- **AC:** an alert fires if any request is still pending after 25 days.

**D2.** Deleted outright:
- `accounts`, `identities`, `sessions` (revoke all refresh token families first)
- `wallets` and inventory, `inbox`, `dailyReward`, `seasonProgress`
- `campaignBest`, `eventBest`, `rankedStatus`
- board entries on **all** boards, open and archived snapshots alike
- `runs` and input logs
- consent records once they are no longer needed as evidence. [[LEGAL: keep consent proof for X years?]]
- email-provider contact entries.

**D3.** Detached rather than deleted:
- Top-100 snapshot runs kept for anti-cheat have `playerId` and name replaced by a random tombstone ID.
- Exception: when a fraud case is open, the run is kept linked until the case closes, with a reason code.

**D4.** Retained but pseudonymised:
- `purchases` rows: `playerId` → HMAC(deletion pepper, playerId). Keep `orderId`, `productId`, amounts, state and dates. Remove `playResponse` PII.
- Retention: [[TAX RETENTION PERIOD]]. A TTL then deletes the rows.

**D5.** Ledger rows that are not purchases are deleted, or folded into anonymous daily totals.

**D6.** An audit entry `account_deleted` records the time, the pseudonymous ID and the counts per collection. It contains no PII.

**D7.** Backups and PITR expire within [[BACKUP EXPIRY]]. Restore procedures must re-apply deletions using a deleted-ID tombstone list.

**D8.** The job uses the Data Access audit log. Any manual deletion from the console is break-glass only (review B4).

**AC for D2 to D7:** an emulator test creates a fully populated account, deletes it and runs the job. It then asserts:
- no document references the `playerId` or the name
- the purchase rows exist with a hashed ID
- the top-100 run has a tombstone
- the job is idempotent on a second run.

## E. Neutral age gate and parental consent (Dev and BE)

PM decision (2026-10-16): the game is **13+**, with no Google Families programme.

**E1.** The age gate appears at first launch, before any account or sign-in, and before any data beyond the install ID is sent. It asks for year and month of birth with a scroll picker. There is no default age, no hint about the "right" answer, and the screen must not say "you must be 13".
- **AC:** UX review confirms the gate is neutral, and the picker does not default to an eligible age.

**E2.** Under-13s are blocked: the app shows a neutral "CRASHVERSE isn't available" screen.
- No account is created and no server call is made apart from the install ID [[LEGAL: confirm even this is acceptable]].
- The date of birth is not stored. Only a local `blocked` flag is kept, so retrying immediately with a different age doesn't work.
- **AC:**
  - An age under 13 never reaches `/auth/guest`.
  - After the block, relaunching shows the block screen again.
  - Clearing app data is accepted as a residual risk.

**E3.** Age bands are `under13|13to15|16to17|adult` (PM decision). For 13+ players the server stores `ageGate = {band: 13to15|16to17|adult, ts, country}` and never the date of birth.
- Per-market consent ages come from a server config table (`config/ageConsent`) marked **NEEDS LEGAL REVIEW** (`legalReviewed:false`). Until legal signs it off, any unknown or unreviewed market uses the most restrictive value (18).
- `/agegate` returns a short-lived signed age token. Account creation requires that token and refuses `under13`.
- Country comes from the Play store country or the locale. [[LEGAL: which signal is acceptable]]
- Changing the answer to an older band needs parental verification or support.
- The server keeps the most restrictive band for a known install or account.
- **AC:** after a reinstall on the same account, the server returns the most restrictive band.

**E4.** Parental consent flow for players below their market's consent age, taken from the config table: under 18 in India (DPDP), so both 13to15 and 16to17. Other markets per [[LEGAL: reviewed config table]].
- The minor enters the parent's email, and the parent receives a consent link.
- On the web, the parent completes a **verifiable** check. [[LEGAL: choose the mechanism that complies with the DPDP Rules]]
- The parent grants or denies each item: social sign-in, custom leaderboard name, purchases.
- Consent can be withdrawn at any time from the same link or by email.
- **AC:**
  - Consent is recorded with method, time and scope.
  - Withdrawal returns the account to minor limits within 24 hours.
  - The link expires in 7 days.

**E5.** Limits for 13- to 17-year-olds without consent, and DPDP rules that apply to all under-18s even with consent:
- Never, at any age under 18: behavioural tracking, analytics profiling or targeted ads (DPDP §9).
- Until consent: a generated name on leaderboards, no social sign-in prompts, no friends features and no purchases.
- Ranked play is allowed with a generated name.
- **AC:**
  - The server enforces each limit, not just the client UI.
  - With `band=13to15` or `band=16to17` and no consent, `/account/link`, display-name change and `/purchases/google/verify` return 403.

**E6.** 18+ check for purchases where a market requires it [[LEGAL: list of markets]]:
- In those markets, purchases need `band=adult` or parental consent covering purchases.
- Play's own payment and family controls act as a secondary check.
- **AC:** with a configured market and `band=13to15` or `band=16to17` without purchase consent, purchase verification returns 403 `AGE_RESTRICTED`, and the client hides the store's buy buttons.

## F. Revoking social links (Dev and BE)

**F1.** Unlinking (`POST /account/unlink`) needs re-auth. The last login method cannot be removed. Unlinking:
- deletes the `identities/{provider}:{sub}` document
- revokes all other sessions
- writes an audit entry.
- **AC:** after unlinking, sign-in with that provider creates a new, separate account.

**F2.** On account deletion and on unlink, call the provider's revoke endpoint where one exists, best-effort and logged:
- Google: revoke the OAuth grant if one was obtained.
- Facebook: `DELETE /{user-id}/permissions`.
- X: token revoke.
- Email: remove the address from the email provider.

**F3.** Facebook data deletion callback (required for Facebook Login). PM decision:
- A request removes the Facebook link (`identities/facebook:*`) and all data that came from Facebook: the app-scoped ID, plus any name or profile field that was imported.
- If Facebook is the account's **only** login, the whole account is deleted through the section D job.
- The callback returns the confirmation code and a status URL.
- **AC:**
  - Facebook's callback tester passes.
  - An account with another login keeps its progress but loses the Facebook link and data.
  - A Facebook-only account is scheduled for full deletion.

**F4.** Users can see and manage their links under Settings → Account → Linked accounts. This is a separate control from deletion.

**F5.** Hide-name control: Settings → Privacy → "Hide my name on leaderboards".
- Entries on every board, including archived snapshots, show a generated name. Scores still count.
- **AC:** the setting takes effect on all boards within 1 minute, and the API never returns the real name for a hidden player.

## G. Open items

These are parked as placeholders for the user and Legal.

- [[Grievance officer]]: business decision for the user.
- [[Company legal entity]].
- [[Tax and accounting retention period]] for purchase records (India and EU).
- [[Backup expiry period]].
- [[Parental verification mechanism]] under the DPDP Rules.
- [[Consent-proof retention period]].
- [[Markets that require an 18+ check for purchases]], and consent ages for 13- to 17-year-olds outside India.
- [[Age-signal source]] (store country or locale).
- [[Per-market consent-age config table]]: legal review of `config/ageConsent`.
- Confirm that Unity Services analytics and crash reporting are off in the release build.

Resolved by PM on 2026-10-16:
- Under-13 policy: the game is 13+ with no Families programme.
- Guest inactivity: 365 days, with a warning 30 days before.
- Partial deletion: not offered.
- Facebook callback scope: see F3.
