# CRASHVERSE – The Wrecker v2 (art-bible restyle)
Images: wrecker_v2_hero.png (hero, near-side view on podium island), wrecker_v2_silhouette_sheet.png (silhouette / side ortho / front ortho, 0.5u grid). These are Python concept diagrams, not final art. The approved boards (bright, glossy, casual-mobile stylized 3D) are the style reference.

## Design
A chunky, toy-like cannon on wheels that matches the boards: a glossy navy barrel with gold bands, a warm wooden wedge carriage with gold rivets, one big spoked wooden wheel with a steel rim and gold hub, and a small rear roller. It looks friendly and heavy, like a premium toy, not military. Every part has a thick dark outline and a soft specular highlight. In gameplay the camera sits low behind it (bottom-left framing, like boards 3, 4 and 6).

## Silhouette rules
1. Read it as three shapes: wedge carriage, then the big front wheel, then a fat diagonal barrel at 25° rest elevation.
2. The barrel is the longest line. The muzzle ring overhangs the carriage front by at least 0.5u.
3. The big wheel's diameter (1.2u) is half the total height. Never hide it.
4. Slot parts may change inside their own volume but must not break the outer silhouette.
5. It must still read as a cannon at 64px in black fill (see the sheet).

## Slots (colours unchanged from M1)
| Slot | Shape | Colour | Placement |
|---|---|---|---|
| CORE (impact type) | Glossy glowing orb, gold rim, cyan bloom | #22D3EE, inner #E0FBFF, rim #E8B04B | Barrel pivot, above the big wheel |
| MODULE (secondary effect) | Rounded glossy pod with an icon decal | #8B5CF6 (gradient #B79CFF to #5B21B6) | Barrel top, 0.65u from the pivot |
| BOOSTER (trajectory/force) | Flared ring nozzle | #FF7A1A (gradient #FFB066 to #C2410C) | Muzzle |
Each slot keeps its own shape (sphere / box / ring), so the slots still read in greyscale. Inner glow tints per core type are allowed, but the outer orb stays cyan.

## Colour codes
Barrel navy #3E4C6E to #0F1626 · gold bands #FFE08A to #B9832A · carriage wood #D9965A to #8A5A34, plank lines #6B4226 · wheel wood #C98A4B / #5E3B1F, rim steel #5B6472 · rear roller #5B6472 to #1B1B1F · outline #101828.

## Greybox (Unity units, 1u = 1m). Envelope unchanged at 4.0 × 2.0 × 2.4
- Root at ground centre. Carriage: wedge, about 2.8 long × 1.6 wide, 0.55 to 1.35 high.
- Front wheels: 2 (one per side), Ø1.2, width 0.25, axle at (x 0.15, y 0.6), z ±0.9.
- Rear rollers: 2, Ø0.7, axle at (x -1.35, y 0.35).
- Barrel pivot (0, 1.5, 0). Core sphere Ø0.9 at the pivot.
- Barrel: tapered cylinder, rear Ø0.84 to front Ø0.68, length 2.3 (-0.7 to +1.6 from the pivot). Elevation 5° to 70°, yaw ±60°.
- Module pod 0.64 × 0.44 × 0.4, mounted on the barrel 0.65 along its axis, 0.62 up.
- Booster ring Ø0.9, depth 0.3, centred 1.75 along the barrel. Projectile spawn point = booster centre + 0.2 forward.
