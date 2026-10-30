# CRASHVERSE — Audio Direction v1
Matches art bible v1 (/workspace/art/v1/art_bible_v1.md): glossy, bright, chunky toy-like, saturated, round & friendly player side vs. tall stacked targets.

## Sonic identity
"Toybox demolition." Every break is satisfying and *cartoon-weighty*, never violent or gritty. Materials read like oversized toys: wood = hollow toy-block knock, brick = chunky clay clunk, steel = bright metallic clang/boing with a musical ring. Player cannon (the Wrecker) is round and bouncy: a punchy "thoomp" with a springy tail, not a gunshot.

## Reference adjectives
bright · bouncy · chunky · glossy · punchy · playful · tactile · sparkly (rewards) · tumbling · musical · warm · cheeky (fail) · triumphant-not-epic (wins)

## Rules
- Original or licensed sounds only. Reference games/films inform *feel* only; no rips, no sample matching, no sound-alike recreations of a recognisable asset.
- Musical key centre: **C major / A minor** family so stingers, combos and coins never clash with music (combo ladder pitched in C major pentatonic).
- Low end controlled: phone speakers. Put weight at 120–250 Hz with a 2–4 kHz "click" transient so breaks read on a speaker; sub only as garnish.
- Every gameplay sound must be readable at 40% volume on a phone speaker, and in mono.
- No harsh/realistic screams, gunfire, or glass-shard pain. Fail is cheeky ("wah-wah" toy horn), never punishing.
- Bosses (Tidewarden, Emberforge Titan) are big but toy-like: hydraulic hiss, clockwork, bell-like lantern core; Titan = molten "forge" timbre (anvil + sizzle).
- Seasons (The Wrecking Frontier) can recolour the palette (new lead instruments), never change the core identity.

## Sourcing & licensing plan (v1: no paid contracts)
Only CC0 or royalty-free assets whose licence was checked and logged, plus placeholder music. No commissions, subscriptions or paid packs in v1 (see composer_options_note.md for later options).

| Source | Licence (checked 16 Oct 2026) | Licence URL | Use | Notes |
|---|---|---|---|---|
| Kenney (kenney.nl) audio packs, e.g. "Music Jingles", UI/impact packs | CC0 (public domain) per Kenney support page | https://kenney.nl/support ; CC0 deed https://creativecommons.org/publicdomain/zero/1.0/ | UI clicks, impacts, jingles as placeholder stingers | No attribution required (credit "Kenney" optional; don't use their logo). Keep the License.txt from each pack. |
| Freesound, filtered to **Creative Commons 0** only | CC0 per sound (Freesound also hosts CC-BY, CC-BY-NC and old Sampling+ - **excluded**) | https://freesound.org/help/faq/ ; https://creativecommons.org/publicdomain/zero/1.0/ | foley layers (wood, clay, metal, coins) | Record the sound page URL + licence shown on that page at download time; screenshot it. Skip anything tagged GenAI. |
| OpenGameArt, filtered to CC0 only | CC0 (site also hosts CC-BY/SA, OGA-BY, GPL - **excluded**) | https://opengameart.org/content/faq | placeholder music loops, SFX | Check each item's licence field; previews may be all-rights-reserved, use only the download files. |
| Sonniss #GameAudioGDC bundles | **Not CC0** - Sonniss custom royalty-free licence: commercial use, modification, no attribution; may not resell/redistribute the sounds standalone; AI/ML training prohibited | https://sonniss.com/gdc-bundle-license/ (v2.0, effective 27 Aug 2026; older terms at https://sonniss.com/gdc-bundle-license/previous-versions/) | heavy layers: collapses, explosions, metal | Log the bundle year and the licence version in force when downloaded. Fine inside a compiled game. |
| Own recordings / synthesis | Original, owned by the studio | n/a (internal record) | anything | Log date, recordist, and session notes. |

Rules: original or CC0/royalty-free only; never rips; every file gets a licence_register.csv row (with the licence URL above) before it is imported; `source`/`licence` in sfx_list.csv stay "TBD - original or licensed only" until that row exists. Prefer layering and processing several sources so no single stock file ships unmodified.
Not legal advice; PM to confirm with counsel before release.
