# CRASHVERSE Release Plan v1
Release Manager, 16 Oct 2026 IST. Consolidates PLAY_CONSOLE_CHECKLIST, VERSIONING_AND_SIGNING, STORE_LISTING_DRAFT, DATA_SAFETY_DRAFT and RELEASE_CHECKLIST. Nothing has been created, paid for or submitted.

> **Revision 1.1 (16 Oct 2026 IST), PM decisions:** (1) Internal/closed testing use a signed **`test` flavour with the local backend**: no ranked play, no purchases, test-time features clearly labelled. **Public production requires approved hosting**, and that remains a gate. (2) Account type is **parked** for the user at launch prep, see ACCOUNT_TYPE_COMPARISON.md. (3) **Real-money gems are cut from v1**: gems are earned only. IAP returns together with hosting and the billing SDK. Security Admin is drafting the ToS. Art + Game Designer are handling the "Jurassic World" rename.

## 0. Launch blocker: no live server
`server/README.md`: "No hosting is approved, so nothing is provisioned or deployed." `backend/Backend_Plan_v1.md` §9 recommends hosting Option A, but it needs the owner's approval. Consequences:
- The release build **refuses to boot without a server backend** (`ReleaseBuildGuard` / `BackendLocator`). The only working builds today are dev builds on `LocalBackendService`.
- Server-dependent features: Daily Titan (ranked dailies) and leaderboards, season track, 7-day rewards, store/IAP (server-only entitlements), account linking and login, account deletion (server job + web page), age-gate band storage and parental consent.
- **Decision needed (PM/owner):** approve hosting before production, or define an offline-only v1 build (with a different Data Safety answer and guard changes reviewed by Security).
- **Decided (rev 1.1):** internal/closed tracks get a signed `test` flavour on `LocalBackendService`: ranked/leaderboards off, no purchases, and an on-screen "TEST BUILD" label on test-time features (test clock, offline toggle, fake leaderboard). OPEN (Developer/Security): how this flavour gets past `ReleaseBuildGuard`/B1 without weakening the production guard (e.g. a separate `CRASHVERSE_TEST` define, with CI proving the production AAB still fails closed). The test flavour must never be promoted to production. Use a distinct versionCode range or a build label.
- **Production stays gated** on approved hosting + deployed backend.

## 1. Phone-doable Play Console steps
All in Chrome on the phone (details in PLAY_CONSOLE_CHECKLIST.md): account (type PARKED, see ACCOUNT_TYPE_COMPARISON.md) → ID verification → create app (Game, Free) → Play App Signing → internal test → closed test (personal account only: **≥12 testers opted in continuously ≥14 days**) → production access → App content (privacy URL, ads = none, content rating, target audience 13+, Data Safety, account deletion URL) → store listing.
Builds happen in GitHub Actions, not on the phone (BUILD.md). The repo isn't pushed yet, which needs the owner's go-ahead.

## 2. Developer fee
One-time **US$25** registration fee. **Note only. Not paid, needs approval.**

## 3. Play App Signing and upload key
Google holds the app signing key. Our upload key lives as protected-environment GitHub secrets (Security B7), with 2 offline backups, and is never committed (add `*.jks`/`*.keystore` to .gitignore; there's no rule today).

## 4. Versioning
`com.crashverse.game`, versionName 0.1.0, versionCode 1, min 24, target **34 → must be raised to 35**, IL2CPP, ARMv7+ARM64. Scheme: semver + strictly increasing code (proposal MAJOR*10000+MINOR*100+PATCH).

## 5. First AAB → internal testing
1. CI change: add an AAB release job (currently APK only) with keystore secrets. 2. Build the signed `test` flavour (local backend, no ranked, no purchases, labelled). 3. Upload to Internal testing, add testers, install via the opt-in link on the phone. 4. Smoke test from RELEASE_CHECKLIST.md. 5. Promote to Closed when it passes.

## 6. Store listing and screenshot plan
Copy: STORE_LISTING_DRAFT.md (title "CRASHVERSE: Smash Puzzle", 24 characters). Assets in `/workspace/art/v2` (FINAL visual target, 1080×1920; files starting with `_` are superseded, so do not use them):
| Slot | Art v2 reference | Shows |
|---|---|---|
| 1 | `screens/05_gameplay.png` | aim + FIRE, core loop |
| 2 | `screens/07_tidewarden_boss.png` | Tidewarden boss |
| 3 | `screens/09_powerups.png` | 6 power-ups |
| 4 | `screens/03_world_map.png` | Sunnyville Isles map |
| 5 | `screens/06_level_complete.png` | stars/win |
| 6 | `screens/08_daily_boss.png` | Emberforge Daily Titan (only if the backend is live) |
| 7 | `screens/10_season_track.png` | season (only if live) |
| 8 | `screens/11_rewards.png` | 7-day rewards (only if live) |
No store screenshot may show real-money prices or ranked boards for v1.
Not for the store: `01_login.png` (Google/Facebook brand marks, `brand/`), `02_profile`, `03b_map_legend`, `04_level_select`, `12_fail`, `13_tidewarden_arena` (arena alternative). `board_v2.png` is the 14-panel flow board, not a listing asset.
**Caveat:** these are painted mockups, and text/UI is built live in the engine (OVERLAYS.md). Final screenshots must be captured from a real build to represent the app accurately. Use art v2 only as composition targets. Art & UI to deliver: **512×512 icon, 1024×500 feature graphic** (neither exists), adaptive launcher icon, and in-game captures.

## 7. Content rating
IARC questionnaire: cartoon destruction of structures, no people harmed (confirm), no gambling or loot boxes (catalog items are fixed, not random), **no digital purchases in v1** (gems are earned only), no chat/UGC (confirm), leaderboard names (generated names for minors, F5 "hide my name").

## 8. Pre-launch checklist
### Security Admin blocking conditions B1–B7 (quoted from `security/Backend_Plan_v1_Review.md` §4)
- [ ] **B1 (S1, C1).** "P10 must ship: the release build fails closed on `LocalBackendService`, and `SetTestClock`/test-offset hooks, the override file path and `GrantPremiumPassStub` are stripped. A CI grep gate must fail the release build if any of them remain." Status: README says it's implemented (`check_release_guards.py`, `ReleaseBuildGuard`). Security to sign off.
- [ ] **B2 (S1).** "Firestore rules deny all client access, verified by emulator tests. Rate limits and the nonce cache are shared across instances (gaps 1 and 2)." Status: `server/firestore.rules` + emulator tests exist (server/README). Not deployed.
- [ ] **B3 (S4/S5, H2).** "Before any public ranked board: Integrity is enforced for ranked play (S2). 100% of top-N entries on every board are replayed before they become visible (S1). Until replay is live, boards show provisional entries that are marked as such, with SG-12 bonuses held back."
- [ ] **B4 (H3).** "Console edits are break-glass only, with alerts on Firestore audit logs (gap 3)."
- [ ] *(Deferred: not in v1 since IAP is cut; required before IAP returns)* **B5 (S2, C2).** "There is no client-side grant path. The pending-until-verified flow passes Play licence-tester cases (purchase, pending, cancel, refund, replayed token). Voided-purchase clawback follows S6."
- [ ] **B6 (launch).** "Privacy items: Privacy notice with the retention periods (S3). Account deletion in-app and on the web. Data Safety form. Grievance officer named (**open item for the user**). Age gate and parental-consent flow for users under 18 (DPDP)."
- [ ] **B7 (CI).** "Workload Identity Federation as in S7, with no service-account keys. Unity licence secrets are in a protected environment and actions are pinned by SHA."
- Not blocking (per the same review): price verification (gap 9), and X login, which stays behind its flag.
- Separate items: `security/Deletion_AgeGate_Requirements.md` has its own web-deletion **B1–B4** (public HTTPS page at `[[WEB DELETION URL]]`; identity verification without the app; full deletion only; rate-limited + CAPTCHA + audit-logged). Required for the Play account-deletion declaration.

### Parked legal items (`security/Deletion_AgeGate_Requirements.md` §G, `docs/Privacy_And_Account.md`, `security/Privacy_Notice_DRAFT.md`)
- [ ] Grievance officer (a business decision for the user)
- [ ] Company legal entity (name, address, registration number). This also affects Personal vs Organization Play account
- [ ] Tax and accounting retention period for purchase records (India GST/Companies Act and EU)
- [ ] Backup expiry period
- [ ] Parental verification mechanism under the DPDP Rules
- [ ] Consent-proof retention period
- [ ] Markets that require an 18+ check for purchases, and consent ages for 13- to 17-year-olds outside India
- [ ] Age-signal source (store country or locale)
- [ ] Whether sending the install ID before the age gate is acceptable
- [ ] EU/UK representative (or "not applicable")
- [ ] Privacy Notice and Play Data Safety drafts both say "NEEDS LEGAL REVIEW"
- [ ] Trademark: "Jurassic World" (qa C-05). **Rename in progress: Art + Game Designer**
- [ ] Audio licensing: "Not legal advice; PM to confirm with counsel before release" (audio/audio_direction.md)
- [ ] Login footer "Terms of Service · Privacy Policy": no ToS in the repo. **Security Admin is drafting it**

### Release engineering
- [ ] **GATE (production):** hosting approved and backend deployed (§0)
- [ ] Signed `test` flavour for internal/closed tracks (no ranked, no purchases, labelled)
- [ ] Account type decided by the user (ACCOUNT_TYPE_COMPARISON.md)
- [ ] targetSdk 35, AAB CI job, keystore secrets, .gitignore keystore rule
- [ ] Icon, feature graphic, real screenshots
- [ ] **v1 has no IAP:** remove/disable the real-money items ("Pouch of Gems" 99, "Chest of Gems" 499) in `store_catalog.json`/`gen_content.py`. Store hides buy-with-money. Verify no `com.android.vending.BILLING` permission in the merged manifest. B5 is deferred to the IAP release
- [ ] Privacy policy + deletion pages hosted (public HTTPS)
- [ ] 12 testers × 14 days closed test complete (personal account)
