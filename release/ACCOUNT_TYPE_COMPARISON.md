# Play developer account: Personal vs Organization
**Status: PARKED for the user's decision at launch prep (PM 2026-10-16).** Facts were checked against Google's help pages on 16 Oct 2026 IST.

| | Personal | Organization |
|---|---|---|
| One-time fee | US$25 (credit/debit card, no prepaid) [1] | US$25 [1] |
| Needs | Legal name + address (from the Google Payments profile), contact email + phone, developer email, all verified by OTP [2]. A government ID and a card in your legal name may be requested [1] | **D-U-N-S number** (free from Dun & Bradstreet, **can take up to 30 days**), plus organization name, address, phone and website, contact name/email/phone, and developer email **and** phone. The Payments-profile name and address must match D&B [2] |
| Legal entity | Not needed | Required (a registered organization). Ties into the open `[[COMPANY LEGAL ENTITY]]` item |
| Shown publicly on Play | Legal name, country, developer email; full address if you monetize [2] | Legal name, legal address, developer email + phone [2] |
| Testing before production | Accounts created after 13 Nov 2023: **closed test with ≥12 testers opted in continuously for ≥14 days**, then apply for production (review usually ≤7 days) [3] | Not subject to this rule [3] (the rule is stated for personal accounts only) |
| Device verification | Must verify access to an Android device through the Play Console mobile app [1] | Not stated for organizations in [1] |

**Implications for CRASHVERSE:** a personal account adds at least about 3 weeks (14-day test + review), and the owner's legal name and country appear publicly. An organization account needs a legal entity + D-U-N-S number first (up to 30 days), and its address and phone appear publicly. Payment-method verification only matters once we monetize (not in v1) [2].

Sources:
[1] https://support.google.com/googleplay/android-developer/answer/6112435 (Get started with Play Console)
[2] https://support.google.com/googleplay/android-developer/answer/13628312 (Required information to create a developer account)
[3] https://support.google.com/googleplay/android-developer/answer/14151465 (Testing requirements for new personal accounts)
