# L1 v2 "Tide Watchtower" build spec (measured from L01_v2_goal.png)

Sources: `design/level_concepts_v1/L01_v2_goal.png` (1086x1448, 3:4) is the image to match. The concept doc is
`design/level_concepts_v1/L01_v2_TideWatchtower.md`. Nothing that is not in the image or the doc is built.
All screen numbers are % of the goal image (u = from the left edge, v = from the top edge), read from gridded zoom crops.
World numbers come from `/workspace/p1/v2/proj.py` (the same projection the camera uses). They are in metres, with
+x away from the camera, +y up and +z to the screen LEFT. The cannon root is (-20, 0, 0).

## 0. Hierarchy (concept doc §7)
TideWatchtower
- Island_Foundation: rocks_C, rocks_L, rocks_R and the steps
- Stone_Structure: tiers t1 and t2 and the arch
- Wooden_Supports: legs and braces of both archer towers, plus the gallery brackets
- Main_Tower: the gallery deck, the gallery room and the turret
- Roof: main roof halves, dormers and turret roof
- Left/Right_Archer_Platform: top and lower decks, railings, ladder, fire pots and archer figures
- Demon_Banners: banner_C, L_banner and the flag
- Corruption_Crystal: the crystal (an explosive core block)
- Defensive_Cannon: R_cannon
- Decorations: horned skull, braziers, barrel, chain mast and torches
- Destruction_Parts: the physics blocks below. The visual pieces are parented to them.
- Water_Interaction: stone sinks with bubbles, wood floats, splash on entry (phase 2)

## 1. Camera (reproduces the framing)
- Goal frame: 3:4 portrait.
  - Vertical FOV 55.0 deg, horizontal FOV 42.7 deg (tan_h = 0.3905).
  - Pitch 12.3 deg down, which puts the sea horizon at v = 29 %.
- Camera position (-25.8, 4.7, 0.0), on the firing line, no yaw.
  - That is 4.0 m behind the breech ring and 2.7 m above the barrel axis.
  - It is 5.7 m above the sea.
- Sea level for L1: y = -1.0. The cannon deck is 1.0 m above the sea.
- Phones keep the horizontal FOV, so the complex spans the same 95 % of the width.
  - Vertical FOV = 2*atan(0.3905 / aspect): 80.5 deg on 1080x2340.
  - The goal frame becomes the central 61.5 % band of the screen.
  - Above the band is more sky, behind the HUD. Below it is more deck and carriage.
- Check against the goal:
  - The muzzle ring centre projects to v 59.1 (goal 58).
  - The breech ring projects to v 88.3 (goal 89).
  - The waterline at the fortress is v 54.5 at x = -4.5.
- Start aim on L1: elevation 5 deg (the minimum), so the barrel reads nearly level and goes away into the frame, as in the
  goal. The engine rest elevation is 25 deg, which would point the barrel up.

## 2. Element table (goal bbox -> world box on the given vertical plane)
| element | goal bbox (u0,v0)-(u1,v1) | plane | y range | z range | size | notes |
|---|---|---|---|---|---|---|
| turret_roof | (51,2)-(60,5) | x=-2.0 | y 10.09..10.73 | z -1.71..-0.17 | w 1.54 h 0.64 | small pagoda turret roof, red tiles, gold upturned corners, finial+flag |
| turret_body | (52.5,5)-(58,8) | x=-2.0 | y 9.45..10.09 | z -1.38..-0.43 | w 0.95 h 0.64 | open timber lantern room, 4 posts |
| main_roof | (39,8.5)-(71,14) | x=-5.0 | y 7.7..8.76 | z -3.19..1.69 | w 4.88 h 1.06 | 4-sided red tiled hip roof, upturned gold horn corners, 5 tile rows visible |
| dormer_L | (45,8.5)-(50,12) | x=-4.6 | y 8.15..8.83 | z 0.0..0.78 | w 0.78 h 0.68 | small gable dormer with window |
| dormer_R | (61,8)-(65,10.5) | x=-3.6 | y 8.62..9.13 | z -2.43..-1.79 | w 0.64 h 0.51 | small gable dormer |
| gallery_room | (42,14)-(68,18.5) | x=-4.3 | y 6.89..7.8 | z -2.86..1.28 | w 4.14 h 0.91 | timber frame, 3 lit window bays (orange fire glow) |
| gallery_deck | (37.5,18.5)-(70,21.5) | x=-5.2 | y 6.21..6.8 | z -3.07..1.93 | w 5.0 h 0.59 | thick beam balcony with bolted plates, brackets below, railing on top |
| stone_body_top | (41,21.5)-(67,24) | x=-4.5 | y 5.75..6.26 | z -2.72..1.45 | w 4.17 h 0.51 | stone tower, top width |
| stone_body_bot | (37,44)-(71,47) | x=-4.5 | y 0.74..1.42 | z -3.53..2.2 | w 5.73 h 0.68 | stone tower, base width (battered walls) |
| banner_C | (47.5,21.5)-(56.5,37) | x=-4.6 | y 2.98..6.25 | z -1.03..0.41 | w 1.44 h 3.27 | red demon banner, horned skull, frayed gold-fringed tip |
| arch_outer | (40,32.5)-(62,47) | x=-4.9 | y 0.81..3.97 | z -1.93..1.66 | w 3.59 h 3.16 | stone arch frame, 9 voussoirs, block jambs |
| arch_opening | (44.5,34)-(58.5,46) | x=-4.9 | y 1.03..3.65 | z -1.37..0.91 | w 2.28 h 2.62 | dark red interior |
| horned_skull | (45.5,29.5)-(56.5,35) | x=-5.0 | y 3.44..4.6 | z -1.03..0.72 | w 1.75 h 1.16 | bone skull with two curved horns over the keystone |
| crystal | (47.5,35.5)-(54,45.5) | x=-4.0 | y 0.99..3.27 | z -0.67..0.43 | w 1.1 h 2.28 | red emissive crystal cluster: 1 tall + 4 small shards |
| brazier_L | (38,39)-(42,47) | x=-5.5 | y 0.92..2.64 | z 1.27..1.93 | w 0.66 h 1.72 | timber post brazier, iron bowl, fire |
| brazier_R | (59.5,39)-(64,47) | x=-5.5 | y 0.92..2.64 | z -2.22..-1.53 | w 0.69 h 1.72 | same |
| barrel_C | (63,41.5)-(68.5,47.5) | x=-5.6 | y 0.83..2.12 | z -2.93..-2.09 | w 0.84 h 1.29 | orange barrel with red/gold emblem |
| steps | (48,47)-(56,53) | x=-6.2 | y -0.23..1.05 | z -0.93..0.32 | w 1.25 h 1.28 | 5 stone steps down to water |
| rocks_C | (30,46)-(75,56) | x=-6.5 | y -0.8..1.31 | z -3.82..3.13 | w 6.95 h 2.11 | grey rock islet, moss, foam at base |
| flagpole | (42.5,2.5)-(43.5,18) | x=-3.6 | y 7.07..10.23 | z 1.04..1.24 | w 0.2 h 3.16 | pole with red flag |
| flag | (36,3)-(47,11) | x=-3.6 | y 8.52..10.13 | z 0.48..2.28 | w 1.8 h 1.61 | red flag with gold sigil, waving left |
| chain_mast | (38.5,6)-(40,30) | x=-4.0 | y 4.48..9.44 | z 1.58..1.92 | w 0.34 h 4.96 | mast with cross spars, chains to side towers |
| L_top_deck | (15.5,24.5)-(34,27) | x=-4.2 | y 5.13..5.66 | z 2.61..5.66 | w 3.05 h 0.53 | top platform beam |
| L_top_rail | (15.5,22)-(34,24.5) | x=-4.2 | y 5.66..6.18 | z 2.6..5.63 | w 3.03 h 0.52 | railing, 5 posts |
| L_banner | (21.5,27)-(27.5,34.5) | x=-4.2 | y 3.51..5.13 | z 3.69..4.75 | w 1.06 h 1.62 | red skull banner |
| L_low_deck | (7,37)-(35,39.5) | x=-5.2 | y 2.5..3.03 | z 2.4..6.91 | w 4.51 h 0.53 | lower deck beam |
| L_low_rail | (7,35)-(35,37) | x=-5.2 | y 3.03..3.46 | z 2.39..6.88 | w 4.49 h 0.43 | railing |
| L_legs | (13,27)-(33,50) | x=-4.6 | y 0.06..5.12 | z 2.74..6.27 | w 3.53 h 5.06 | 4 square legs + X braces |
| L_ladder | (17,39)-(21,52) | x=-5.6 | y -0.16..2.65 | z 4.57..5.35 | w 0.78 h 2.81 | ladder, 9 rungs, leaning |
| L_fire1 | (9,34)-(14,37) | x=-5.2 | y 3.03..3.67 | z 5.72..6.56 | w 0.84 h 0.64 | fire pot |
| L_fire2 | (25,35)-(31,38) | x=-5.2 | y 2.82..3.46 | z 3.03..4.01 | w 0.98 h 0.64 | fire pot (big) |
| L_archer | (22,15)-(31,24) | x=-3.8 | y 5.78..7.67 | z 3.09..4.65 | w 1.56 h 1.89 | hooded horned archer, drawing bow |
| rocks_L | (0,43)-(30,55) | x=-6.0 | y -0.72..1.86 | z 3.12..8.0 | w 4.88 h 2.58 | rock islet under left tower |
| R_top_deck | (69,26)-(86,29.5) | x=-4.0 | y 4.59..5.35 | z -5.95..-3.16 | w 2.79 h 0.76 | top platform beam |
| R_top_rail | (69,23.5)-(86,26) | x=-4.0 | y 5.35..5.88 | z -5.92..-3.14 | w 2.78 h 0.53 | railing |
| R_low_deck | (76,40)-(97,42) | x=-5.4 | y 1.99..2.42 | z -7.49..-4.16 | w 3.33 h 0.43 | lower deck |
| R_low_rail | (76,37.5)-(97,40) | x=-5.4 | y 2.42..2.94 | z -7.45..-4.15 | w 3.3 h 0.52 | railing |
| R_legs | (71,29)-(95,51) | x=-4.6 | y -0.17..4.7 | z -7.28..-3.57 | w 3.71 h 4.87 | legs + X braces |
| R_scaffold | (63,32)-(71,44) | x=-4.8 | y 1.46..4.07 | z -3.39..-2.15 | w 1.24 h 2.61 | inner scaffold between tower and right deck |
| R_cannon | (82,33.5)-(92,40) | x=-5.2 | y 2.39..3.77 | z -6.67..-5.15 | w 1.52 h 1.38 | black iron cannon on carriage, muzzle toward camera-right |
| R_archer | (74,17)-(87,27) | x=-3.8 | y 5.14..7.26 | z -6.05..-4.01 | w 2.04 h 2.12 | archer |
| rocks_R | (75,45)-(100,53) | x=-5.6 | y -0.38..1.37 | z -7.98..-4.06 | w 3.92 h 1.75 | rock islet |

Foreground (on the cannon deck y = 0). The rail plane is x = -17.55; the rail top is y 1.30, its bottom y 0.81, and the
post tops y 1.69.
- Rail posts L/R: goal u 17-25 and 75-83, v 63-80; world z = +/-2.0, 0.42 m square, 1.7 m tall, bolted iron plate.
  The rail runs full width as two thick beams (y 0.81-1.30).
- Lantern: goal (0.5-12, 55-69). Brass hexagonal lantern on the rail top at z 3.0, about 0.7 m wide and 1.3 m tall.
  Glowing warm yellow (#FEF03C core).
- Barrel L: goal (0-11, 70-84). Deck point x -18.2, z 2.9. Orange-brown staves #D57321 with 3 steel hoops; 0.9 m diameter,
  1.1 m tall.
- Cannonball crate: goal (11-27, 74-84). Deck point x -18.2, z 2.0. Wooden crate 1.0 x 0.45 m with 5 black balls (#312227,
  highlights #7E6266), 3 at the bottom and 2 on top.
- Rope coil on a barrel, right: goal (89-100, 67-84). Deck point x -18.2, z -2.95. A barrel with a coiled rope (#91532F),
  a rope loop hanging down the side, and a cannonball on top at v 63-67.
- Red cloth: draped on the rail between the left post and the cannon, goal (25-37, 70-75), #791314.
- Gold/yellow cloth: on the rail, goal (70.5-74.5, 67-70).
- Corner banners: red cloth on gold-capped poles at both bottom corners, goal (0-8, 87-100) and (92-100, 87-100).
  Deck point x -20.2, z +/-2.34.
- Deck: warm planks (#E28D55 lit, #8A503C mid, #351912 gaps) running away from the camera; plank width about 0.35 m.

## 3. Player cannon (render-only scale k = 1.35; the pivots and ballistics are unchanged)
- Barrel: dark navy #212548 (lit #3C394E), satin. It reads long; the breech ring diameter is about 1.25 m and the muzzle
  ring about 0.8 m.
- Gold rings #C08050 (shade #764E39), thick and raised, at goal v:
  - muzzle lip 56-60
  - ring 64-67
  - wide reinforce ring 72-75
  - breech ring 87-91, the widest, spanning u 30-70
- Sun emblem: gold #F8E474 / #EC9114, on top of the barrel between v 74.5 and 86, centred u 50.
  - 16 rays: 8 long, 8 short.
  - The disc is about 35 % of the barrel width.
- Breech knob (cascabel): gold ball at the bottom centre, v 95-100.
- Carriage: heavy dark-brown timber cheeks #714130 (shade #160C0A).
  - Each cheek is stepped, 3 steps, on both sides of the barrel: left u 28-38, right u 62-72, v 75-97.
  - Iron bolts on the cheeks; the trunnion caps are dark iron.
  - Front blocks at the bottom corners: u 25-40 and 62-77, v 95-100.
  - Wheels are not visible in the goal; they are hidden by the frame.

## 4. Central fortress (world sizes, from the table)
### Rocks
- Grey stone #7C6E5F, dark #393933, highlights #E7D1A8, with moss tufts.
- Front edge x -6.5. Top at y 0.8 under the arch.
- Steps: 5 stone steps, 1.25 m wide, down from the arch platform to the water at the centre.

### Stone tower (two tiers)
- Battered (tapered) walls: 5.6 m wide at the base (y 0.8) to 4.2 m at the top (y 6.2); depth 4.4 m (x -4.9..-0.5).
- Masonry, so the wall reads as stone blocks: rows of blocks about 0.45 m tall and 0.6-0.9 m long, stagger bonded.
  - Light sandstone-grey, #866263 lit with #402C25 mortar; the right face is lit warm.
  - Moss and green streaks.
  - About 12 courses from y 0.8 to 6.2.
- Arch: outer 3.6 x 3.2 m, opening 2.3 x 2.6 m.
  - Large dressed block jambs: 4 blocks per side, alternating long and short.
  - 9 voussoirs and a keystone, #C48A6F.
- Horned skull: over the keystone, 1.75 m wide including the horns, 1.16 m tall, bone #F7DEB1 / #C67B48. Glowing red eyes.
- Crystal: red emissive #E9172D, core #F9A99D, dark facets #840822.
  - One tall 6-sided shard (2.0 m) and 4 small shards around it.
  - Sits on a stone plinth 0.5 m behind the arch face.
  - Red glow on the arch interior (#44101E, cracks #8C2630).
- Central banner: hangs from under the gallery, centred u 52.
  - 1.44 m wide, 3.3 m long; red #8C140E with darker folds.
  - Pale horned-skull emblem; gold fringe; split (swallowtail) bottom.
- Braziers L/R: square timber post 0.3 m, iron bowl 0.66 m, fire. At z +1.6 and -1.9, x -5.5.
- Barrel: orange #AE5E30 with a red diamond and a gold sun emblem, at z -2.5.

### Gallery and roof
- Gallery deck (balcony): 5.0 m wide, 0.6 m thick beams, y 6.2-6.8; projects 0.35 m beyond the stone.
  - Bolted iron plates. 7 corbel brackets under the front edge.
  - Railing: posts every 0.7 m, 0.8 m tall, with a top rail.
- Gallery room: 4.1 m wide, y 6.9-7.8.
  - Timber posts with 3 lit window bays (fire-orange #FFA040 glow) on the front, and the same on the sides.
- Main roof: red clay tiles #E15A43 / #5C1A15, gold trim and ridge caps.
  - Pagoda-style hip roof with concave slopes; eaves 4.9 m wide at y 7.7.
  - 4 upturned gold horn corners.
  - 5 tile rows on the front slope.
  - 2 small gable dormers (left front, right side).
- Turret: open lantern room 0.95 m wide (4 posts, warm light), y 9.45-10.1.
  - Small pagoda roof 1.5 m wide with gold upturned corners.
  - Finial spike at y 10.7.
- Flag: pole at z +1.1 from y 7.1 to 10.2. Red flag 1.8 x 1.6 m with a gold sigil, waving toward the screen left.
- Chain mast: thin pole with 3 cross spars at z +1.7, with iron chains hanging from it to the left tower.

## 5. Archer towers
Dark timber #361C14 / lit #AF612C, iron bolts.
### Left
- 4 square legs (0.3 m) with X braces. Top deck 3.0 m wide at y 5.1-5.7, railing 0.5 m.
- Lower deck 4.5 m wide at y 2.5-3.0, railing 0.45 m. The lower deck is wider and extends to the left.
- Ladder: 9 rungs, from the water rocks to the lower deck, at z 4.6-5.3, leaning about 8 deg.
- Banner: 1.06 x 1.6 m, under the top deck.
- Fire pots: one on the lower deck left (z 6.1) and one big one at z 3.5.
- Torch: one on the top deck rail.
- Archer: hooded horned figure 1.9 m tall, drawing a bow toward the camera.

### Right
- Same build: top deck 2.8 m at y 4.6-5.35, lower deck 3.3 m at y 2.0-2.4.
- Inner scaffold between the stone tower and the right deck.
- Enemy cannon: black iron, 1.5 m, on the lower deck at z -5.9, aimed toward the camera-right.
- Torch on the top deck; archer figure.

## 6. Background
- Sky: deep blue #0063E2 at the top, lighter toward the horizon; puffy white clouds #C8D5FC, mostly upper-left and right.
- Sea: open #0477C2 / #25A8E5; near lagoon turquoise #0FC4C8 / #0A6B8A; white foam rings around the rocks.
- Left cliffs: goal (0-25, 14-29), green tops, rock #3B607E in haze, with a beach at the waterline.
- Right cliff: goal (72-100, 6-28), green tops, with a waterfall at u 85-88, v 11-27.
- Castle: white walls with blue spires, on the right cliff top, goal (80-93, 0-8).
- Seagulls: 2, at the upper left (u 3-12, v 13-16) (optional).

## 7. Destruction (concept doc §4-5; physics blocks, layout c1_l01_tide_watchtower)
- Crystal: wood HP 30 (a direct hit breaks it). It is explosive, so it detonates with r 2.5 and 80 damage.
  - Every stone block of tier 1 is brick, HP 60, with its centre within 2.5 m of the crystal, so the hint shot breaks the
    whole base.
  - Tier 2, the gallery, the roof and the turret are supported only by tier 1, so they collapse and fall (S3).
- Objective pieces: the central structure (17) and the two archer top decks (2).
  - One hint shot collapses the centre, which is 17/19 = 89 %, at least 0.85. That is a 3-star one-shot win.
- Archer tower legs, braces, ladders and railings are breakable non-objective pieces.
- Phase 2:
  - stone chunks sink with bubbles; wood floats
  - the crystal cracks and flashes, then shatters into glowing shards
  - banners tear; torches fall
  - S1 cracks (existing damage feedback), S2 lean

## 8. Verification
- `/workspace/p1/v2/overlay.py` projects every layout block with the camera above and draws its outline on the goal image.
  This checks alignment before every build.
- After each build: side-by-sides, a strip, and element crops (goal vs in-game) for the fortress, both towers, the cannon
  and the foreground.
