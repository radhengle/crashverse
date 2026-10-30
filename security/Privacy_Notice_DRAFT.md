> **DRAFT: NEEDS LEGAL REVIEW. Not for publication.** Prepared by Security Admin on 2026-10-16 from the code at commit `0d60e07` and `backend/Backend_Plan_v1.md`. Items in **[[double brackets]]** are placeholders that must be filled in. Statements marked *(planned)* describe the backend, which has not been built yet. Check them against the build that actually ships.

# CRASHVERSE Privacy Notice

**Last updated:** [[DATE]]

## 1. Who we are
CRASHVERSE is published by **[[COMPANY LEGAL ENTITY NAME, REGISTERED ADDRESS, REGISTRATION NUMBER]]** ("we", "us").

Under the EU/UK GDPR we are the data controller. Under India's Digital Personal Data Protection Act, 2023 (DPDP Act) we are the Data Fiduciary.

- **Privacy contact:** [[PRIVACY CONTACT EMAIL]]
- **Grievance Officer (India):** [[GRIEVANCE OFFICER NAME / TITLE, EMAIL, POSTAL ADDRESS]]
- **EU/UK representative (if required):** [[EU/UK REPRESENTATIVE, OR "NOT APPLICABLE" PER LEGAL]]

## 2. What we collect and why

**Game account ID**
- **What it is:** a random ID we create for you, plus your display name.
- **Why:** to run your account and save your progress.
- **Legal basis:** contract (providing the game).

**Sign-in data, if you choose to link an account**
- **What it is:**
  - Google: account ID and email.
  - Facebook: app-scoped user ID.
  - X: user ID.
  - Email login: your email address.
- **What we never receive:** your password for any of these providers.
- **Why:** to let you sign in and recover progress on a new device.
- **Legal basis:** contract.

**Guest play**
- **What it is:** a device installation ID and a sign-in token stored securely on your device.
- **Why:** to let you play without linking an account.
- **Legal basis:** contract.

**Gameplay data**
- **What it is:** levels played, stars, scores, season progress, rewards claimed and in-game currency balances.
- **Why:** progress, rewards and leaderboards.
- **Legal basis:** contract.

**Run input logs**
- **What it is:** the aim, power, timing and power-ups for each shot in ranked, campaign and event runs, plus the server timestamps for those runs.
- **Why:** to check that scores are genuine and to prevent cheating.
- **Legal basis:**
  - GDPR: our legitimate interest in fair play.
  - DPDP: lawful use for that stated purpose.

**Leaderboard entries**
- **What it is:** your display name, score and the time you set it.
- **Why:** public leaderboards.
- **Legal basis:** contract.
- **Note:** guest scores are not shown publicly until you link an account.

**Device integrity result (Google Play Integrity API)**
- **What it is:** Google tells us whether the app and device appear genuine. We do not receive device identifiers from this check.
- **Why:** to protect ranked play from cheating and fraud.
- **Legal basis:** legitimate interest (fair play and fraud prevention).

**Purchase records**
- **What it is:** product, order ID, purchase token, purchase status and refunds.
- **What we never see:** your payment card. Google Play processes payments.
- **Why:** to deliver purchases, restore them and handle refunds.
- **Legal basis:**
  - contract
  - legal obligation (tax and accounting).

**Currency ledger**
- **What it is:** a record of in-game currency added and spent.
- **Why:** to keep balances accurate, handle refunds and prevent fraud.
- **Legal basis:** contract and legitimate interest.

**Technical logs**
- **What it is:** request times, error codes and app version. Our logs are designed not to contain personal details.
- **Why:** security and keeping the service running.
- **Legal basis:** legitimate interest.

**Privacy choices**
- **What it is:** your age-gate answer, parental-consent status and consent choices.
- **Why:** to meet the law and respect your choices.
- **Legal basis:** legal obligation.

**What we do not do:**
- We do not collect your precise location, contacts, photos or microphone data.
- We do not sell personal data.
- CRASHVERSE does not show ads. [[CONFIRM AT RELEASE; UPDATE IF ADS OR ANALYTICS SDKs ARE ADDED]]
- The store sells cosmetics and currency only.

## 3. Who we share data with
- **Google:** for Play billing, Play Integrity and cloud hosting (Google Cloud and Firebase, *planned*). Google acts as our processor or as an independent controller, depending on the service.
- **The sign-in provider you choose** (Google, Facebook, X): only to verify your sign-in. [[X login is disabled at launch; remove this mention if it is still disabled at publication]]
- **Email delivery provider:** for login codes. [[PROVIDER NAME WHEN CHOSEN]]
- **Other players:** your display name and scores on leaderboards.
- **Authorities:** when the law requires it.

## 4. International transfers
Data may be processed outside your country, for example on Google Cloud in [[REGION]]. For EU/UK data we rely on [[SCCs / adequacy decision; LEGAL TO CONFIRM]]. Transfers out of India follow the DPDP Act and any restrictions the Government notifies.

## 5. How long we keep data
- **Account and gameplay data:** kept while your account exists.
- **Run input logs:** 90 days.
- **Top leaderboard run records:** kept up to 1 year after the board is archived, for anti-cheat review. They are deleted or de-linked from you when your account is deleted, unless a fraud investigation is open.
- **Purchase records:** kept after account deletion for **[[TAX/ACCOUNTING RETENTION PERIOD, e.g. "X years" PER LEGAL FOR INDIA AND EU]]**, because the law requires it. They are pseudonymised: your game ID is replaced by a one-way code and your profile details are removed. Pseudonymised data is still treated as personal data.
- **Other ledger rows:** deleted or reduced to anonymous totals when your account is deleted.
- **Guest accounts:** deleted automatically after 365 days of inactivity. The game warns you in-app 30 days before.
- **Backups:** expire within [[BACKUP EXPIRY, e.g. 30 days]].

## 6. Deleting your account
You can delete your account in two ways:
- **In the game:** Settings → Account → Delete account.
- **On the web:** at **[[WEB DELETION URL]]**.

Guest players can delete their account from the same in-game screen.

When you ask for deletion:
- We complete it within **30 days**.
- We delete your account, sign-in links, leaderboard entries and profile.
- The exceptions are the pseudonymised records described in section 5.
- Purchases you have deleted cannot be restored.

We offer full account deletion only. Instead of deleting part of your data, you can use these separate controls:
- **Unlink a login provider:** Settings → Account → Linked accounts. You must keep at least one login method.
- **Hide your name from leaderboards:** Settings → Privacy. Your scores still count, but other players see a generated name.

**Facebook deletion requests:** if you ask Facebook to delete the data we received through Facebook Login, we remove the Facebook link and any data that came from Facebook. If Facebook is your account's only login, we delete the whole account.

## 7. Age requirements, children and parental consent
- **CRASHVERSE is for players aged 13 and over.** When you first start the game, we ask your year and month of birth in a neutral way. If you are under 13, you cannot play, and we do not create an account or keep your birth date.
- **Players aged 13 to 17 in India:** the DPDP Act requires verifiable consent from a parent or guardian before we process a child's personal data. Until that consent is given, the game works with these limits:
  - you get a generated name on leaderboards
  - social sign-in is off
  - purchases are off
  - no behavioural tracking or targeted advertising, ever.
  We group players as 13 to 15, 16 to 17, or adult. Whether a parent's consent is needed depends on the age of digital consent in your country. [[LEGAL: per-market table under review]]
- **Purchases:** where a market's law requires it, purchases need you to be 18 or over, or to have a parent's consent. [[LEGAL: list the markets]]
- We do not take part in Google Play's Families programme.
- Parents can review their child's data, withdraw consent or ask for deletion through [[CONTACT]].

## 8. Your rights
**Under the GDPR you can:**
- access your data
- correct it
- have it deleted
- restrict its processing
- receive it in a portable form
- object to processing based on legitimate interest
- withdraw consent at any time
- complain to your data protection authority.

**Under the DPDP Act you can:**
- get a summary of your data and of who it is shared with
- have it corrected, completed or erased
- withdraw consent as easily as you gave it
- nominate another person to act for you
- raise a grievance with our Grievance Officer, and then with the Data Protection Board of India.

**How to make a request:**
- Contact us at [[PRIVACY CONTACT EMAIL]], or use the in-game options.
- We reply within [[1 month (GDPR) / timeline per DPDP Rules]].

## 9. Security
We protect your data in these ways:
- All connections use encryption (TLS).
- Scores and purchases are verified on our servers.
- Signing keys are held in managed key services.
- Access to data is restricted.

If a personal data breach occurs, we notify the regulators and affected users as the law requires.

## 10. Changes
We will notify you in the game about material changes to this notice.

## 11. Language
This notice is available in English. Under the DPDP Act you can ask for it in any language listed in the Eighth Schedule to the Constitution of India. [[LEGAL: confirm translation process]]
