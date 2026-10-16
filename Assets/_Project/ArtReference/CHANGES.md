# M1 to M1b changes (for Developer)
## Wrecker
- Style: from a 4-wheel armoured platform to a chunky cannon-on-wheels (wooden carriage, navy+gold barrel), following the approved boards.
- Envelope: UNCHANGED at 4.0 × 2.0 × 2.4u. Root at ground centre, unchanged.
- Wheels: 4 per side Ø0.8 → 2 big front wheels Ø1.2 (axle x 0.15, y 0.6) + 2 rear rollers Ø0.7 (axle x -1.35, y 0.35).
- Chassis box 3.6 × 1.8 × 0.6 → wooden wedge carriage about 2.8 × 1.6, 0.55 to 1.35 high.
- Turret trapezoid: REMOVED. The core orb sits at the barrel pivot (0, 1.5, 0), Ø0.9 unchanged.
- Barrel: Ø0.4 × 2.6 → tapered Ø0.84 to 0.68 × 2.3 (from -0.7 to +1.6 off the pivot). Elevation 5–70° and yaw ±60° unchanged.
- Module pod 0.6 × 0.5 × 0.4 → 0.64 × 0.44 × 0.4, still on the barrel top (0.65 along, 0.62 up).
- Booster ring Ø0.6 → Ø0.9, at 1.75 along the barrel. Projectile spawn = booster centre + 0.2 forward (same rule, new position).
- Slot colours UNCHANGED: Core #22D3EE, Module #8B5CF6, Booster #FF7A1A.
- Body colours: charcoal #2B2D33 / steel #5B6472 → barrel navy #3E4C6E, wood carriage #D9965A, gold #E8B04B (same gold). New outline colour #101828.
## Arena
- Setting: flat coastal ground plane 60 × 30u → floating island, top surface 60 × 25u ellipse at y=0, cliff edges, debris falls off the edge (add a kill plane below).
- Time of day: golden hour 16:30 → bright late morning 11:00.
- Key light: #FFD9A0, 1.3, rot (35, -40, 0), shadow 0.75 → #FFF2D6, 1.4, rot (50, -35, 0), shadow 0.6.
- Ambient: sky #9FC4E8 / equator #F2C99A / ground #5E7A4A at 1.0 → #8CC8FF / #DCEFFF / #6FAE5A at 1.15.
- Rim: optional #8FB8FF 0.3 front-left → #FFFFFF 0.35 back-left.
- Fog: #F6D7B0, 60–180 → #CFEAFF, 80–250. Bloom 1.1 → threshold 1.0, intensity 0.6. Added saturation +15 / contrast +10.
- New: glossy smoothness 0.5 on props / 0.7 on metal, optional 0.02u outline.
- Camera: added a starting value of about 15° pitch, FOV 50, low behind the Wrecker.
- Layout distances and target sizes: UNCHANGED.
## Materials (rules unchanged, colours brightened)
- Wood #C98A4B → #E3A766 (#C98A4B is now reserved for the cliff, which is indestructible).
- Brick #B5523B → #D2664C, mortar #8E3F2E → #7A3326.
- Steel #6B7684 → #AEB6C2, smoothness 0.6 → 0.7.
- Weak-point gold ring, red explosive barrel and the 2-step damage rule: unchanged.
- Sky #FFC98A → #2F8CFF/#BDEBFF. Grass #6FAE5A → #7ED957. Roofs #2F4A6B → #4D7BC4. Sea removed (sky surrounds the island).
