# Google Play Console setup: phone-only checklist (CRASHVERSE)
Owner has only an Android phone. Play Console works in the phone's Chrome browser (use "Desktop site" if a page is cramped). Nothing here has been done yet. **[APPROVAL]** marks a step that needs the PM's and owner's go-ahead.

## 1. Developer account
- [ ] Decide the account type: **Personal** or **Organization**. Organization needs a D-U-N-S number and a legal entity, and the privacy notice's `[[COMPANY LEGAL ENTITY]]` is still open. **PARKED for the user at launch prep**, see ACCOUNT_TYPE_COMPARISON.md.
- [ ] Go to play.google.com/console on the phone and sign in with the Google account that will own the app. Use a long-term account with 2-Step Verification on.
- [ ] **[APPROVAL] One-time registration fee of US$25.** Do not pay until approved.
- [ ] Fill in the developer name (it shows publicly on the store listing), contact email, phone and website (if any).

## 2. Identity verification
- [ ] Upload a government ID (taking a photo with the phone camera works) and confirm the legal name and address.
- [ ] Verify the contact email and phone (OTP).
- [ ] Android device verification: confirm through the Play Console app on the owner's phone if asked.
- [ ] Verification can take days, so start it early.

## 3. Personal-account testing requirement
New personal accounts must run a **closed test with at least 12 testers opted in for 14 continuous days** before they can apply for production access. Then they answer the production-access questions.
- [ ] Recruit 12+ testers (aim for 15 or more as a buffer) who have Google accounts and Android phones. Collect their emails or a Google Group.
- [ ] Testers must stay opted in for all 14 days. Plan feedback collection.
- Organization accounts are exempt from this requirement. Verify the current rules when you sign up.

## 4. Create the app
- [ ] All apps → Create app. Name "CRASHVERSE" (see STORE_LISTING_DRAFT.md), default language, **Game**, **Free** (free/paid cannot be changed back to paid later). Accept the declarations.
- [ ] Package name comes from the first upload: `com.crashverse.game` (permanent).

## 5. Play App Signing
- [ ] Enrol (the default for new apps): Google holds the app signing key and you sign uploads with your **upload key** (VERSIONING_AND_SIGNING.md).
- [ ] Note the SHA-1/SHA-256 of both keys from Setup → App signing. The planned Google sign-in or Credential Manager setup will need them.

## 6. Tracks
1. [ ] **Internal testing**: upload the first AAB and add up to 100 testers by email. It's available within minutes, so use it for smoke tests.
2. [ ] **Closed testing**: the 12-tester / 14-day run (§3).
   Internal + closed tracks use the signed `test` flavour (local backend, no ranked, no purchases, labelled).
3. [ ] **Production** (GATE: hosting approved + backend live): apply for access after the closed test. Start with a staged rollout (e.g. 10%). **[APPROVAL]** for every release that goes to review.

## 7. App content (Policy → App content)
- [ ] **Privacy policy URL**: a public HTTPS page. Source: `security/Privacy_Notice_DRAFT.md`, which is still a DRAFT that needs legal review. **OPEN QUESTION:** where it will be hosted.
- [ ] **Ads**: "No, my app does not contain ads". No ad SDK in the repo, Unity Ads disabled, AD_ID removed. Re-check if one gets added.
- [ ] **App access**: if a login is ever needed to reach content, give reviewers instructions. Guest play exists.
- [ ] **Content rating (IARC questionnaire)**: category Game. Cartoon destruction of buildings and objects, with no humans harmed (**OPEN QUESTION: confirm with Design**). No gambling, no user-generated content or chat (**OPEN QUESTION**). Digital purchases: **no** (v1 has earned gems only). Leaderboards show display names.
- [ ] **Target audience**: 13-15, 16-17, 18+. No under-13s (PM decision 2026-10-16, see `security/Play_Data_Safety_DRAFT.md`). Not in the Families programme.
- [ ] **Data safety**: see DATA_SAFETY_DRAFT.md.
- [ ] **Account deletion**: web deletion URL `[[WEB DELETION URL]]` (OPEN, requirements B1–B4).
- [ ] Government apps, financial features, health and news declarations: answer "No".
- [ ] **Store listing**: STORE_LISTING_DRAFT.md.
