# CRASHVERSE – Greybox Arena: Sunnyville Outskirts (M1)
Image: arena_sunnyville_moodboard.png. Chosen over Clockwork Tower because the doc defines Sunnyville as Region 1 (levels 1–10, wooden structures, water towers, bridges, intro to supports/chain reactions); Clockwork Tower is a single "Hard" sample. A clock-tower landmark can still be one hero target in the arena later.

## Hex palette
Sky top #FFC98A → horizon #FFE3A3 (sun) · Sea #2E6F8E · Sand #E6C88F · Grass #6FAE5A · Roofs #2F4A6B (navy, ties to brand) · Wood #C98A4B / dark #8A5A34 / outline #5E3B1F · Brick #B5523B / mortar-dark #8E3F2E / #5A2A20 · Steel #6B7684 / highlight #AEB6C2 · Explosive barrel #E5484D · UI/brand gold #E8B04B, cyan #22D3EE reserved for player/Wrecker energy (never on environment).

## Lighting (late afternoon, ~16:30 golden hour)
- Key: Directional, colour #FFD9A0, intensity 1.3, rotation (35°, -40°, 0) so it comes from back-right; soft shadows, shadow strength 0.75.
- Fill/sky: Gradient ambient — sky #9FC4E8, equator #F2C99A, ground #5E7A4A, intensity 1.0.
- Rim: optional 2nd directional #8FB8FF at 0.3 from front-left to keep silhouettes readable.
- Fog: linear, #F6D7B0, start 60 / end 180. Post: mild bloom (threshold 1.1), slight warm grade, no heavy vignette.

## Material readability rules
| Material | Base colour / value | Edge wear | Break FX colour | Note |
|---|---|---|---|---|
| Wood | Warm orange-brown #C98A4B, mid value (~55%) | Lighter raw-wood edges #E3B07A, visible plank lines | Splinters #E3B07A + dust #CFA779 | Weakest; reads "warm" |
| Brick | Red-terracotta #B5523B, mid-dark (~40%) | Chipped corners show #D98B6E, mortar grid always visible | Red chunks + orange-tan dust #D9A37E | Medium; grid pattern = brick even in greybox |
| Steel | Cool grey-blue #6B7684, mid (~45%), higher smoothness 0.6 | Bright scratched edges #AEB6C2 | Sparks #FFD27A + dark grey bits #3A3F48 | Strongest; only cool-hued material |
Rules: each material owns a hue family (warm brown / red / cool grey) so they differ in hue AND pattern, not just value; damaged state = darker + crack decal, at most 2 steps; weak points get a gold #E8B04B outline/marker; explosives always red #E5484D with white stripe.

## Greybox layout hint
Ground plane 60×30u, Wrecker at x=-20 on a sand pad; targets 25–40u away: wooden water tower (h 10u), brick house (6×5×6u), steel gantry (8u span), 1 explosive barrel between them for chain reaction.
