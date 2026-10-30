# Licence register rules (licence_register.csv)
1. Every shipped audio file (SFX, music, stem, stinger, placeholder) has exactly one row before it is imported. No row = asset removed.
2. v1 allowed sources ONLY (no paid contracts):
   a. CC0 assets: Kenney (https://kenney.nl/support), Freesound sounds licensed "Creative Commons 0" (https://freesound.org/help/faq/), OpenGameArt items licensed CC0 (https://opengameart.org/content/faq). CC0 deed: https://creativecommons.org/publicdomain/zero/1.0/
   b. Royalty-free with a free licence checked and logged: Sonniss #GameAudioGDC bundles (https://sonniss.com/gdc-bundle-license/). Not CC0: no standalone redistribution, no AI/ML training.
   c. Original recordings or synthesis made by the team.
3. Forbidden: rips from games, films, TV, YouTube or streaming; CC-BY-NC, CC-ND, Sampling+; CC-BY and CC-BY-SA in v1 (attribution and DRM issues on Android stores, see the OGA FAQ); "free" sites with no clear licence; AI-generated audio; sound-alikes of a recognisable asset; paid packs, subscriptions or commissions (v1).
4. For each row, record the exact item page URL and the licence URL, the licence text as shown on the date of download, and a screenshot or PDF in `audio/licences/` (proof_file_path). Never make up or guess a licence or URL. If you can't verify it, don't use the asset.
5. Placeholder music has `notes=PLACEHOLDER` and must be replaced or re-approved before launch.
6. Attribution is not required for CC0/Sonniss, but keep the credit list in the register anyway (optional Credits screen).
7. Audit before each release build: every file under Assets/_Project/Audio matches a row (a check script is proposed).
