# CRASHVERSE – The Wrecker (M1 locked concept, greybox-level)
Images: wrecker_hero_34.png (3/4 hero), wrecker_silhouette_sheet.png (silhouette + side/front ortho, 0.5u grid). Placeholder concept renders, not final art.

## Design
A compact 4-wheel armoured demolition platform: low charcoal chassis, trapezoid steel turret, one long angled launch barrel. Reads as "friendly heavy machinery", not a military tank: rounded edges, gold trim, oversized glowing parts. Silhouette stays fixed all campaign; only the three slot parts swap.

## Silhouette rules
1. Three-mass read: flat chassis base -> turret block -> long diagonal barrel (rests at ~25° elevation).
2. Barrel always longest line; muzzle must overhang the chassis front by >=0.5u.
3. Wheels: 4 visible per side, never hidden by skirts.
4. Slot parts may change *inside* their bounding volume but never break the outer silhouette (keeps it recognisable at 120px on phone).
5. Black-fill test: must read at 64px as "launcher", rather than a tank or a truck.

## Slot read (shape / colour / placement)
| Slot | Shape | Colour | Placement | Gameplay read |
|---|---|---|---|---|
| CORE (impact type) | Sphere/orb, emissive center | Electric cyan #22D3EE, inner #E0FBFF; per-core tint allowed (Shockwave = cyan, Chain Reactor = orange-red) only in the inner glow | Center of turret, barrel pivot | "Heart" of the machine; biggest glow |
| MODULE (secondary effect) | Box/pod, rounded corners | Violet #8B5CF6 | Barrel shoulder, top-side, rides with the barrel | Add-on box; icon decal shows effect (split, gravity, pierce) |
| BOOSTER (trajectory/force) | Ring/nozzle at muzzle | Energetic orange #FF7A1A | Barrel tip | Changes the muzzle shape: precision = narrow, force = flared |
Rule: one shape language per slot (sphere / box / ring) so the slots read in greyscale too.

## Colour codes
Chassis charcoal #2B2D33 · shadow #1D1F24 · turret steel #5B6472 · highlight #AEB6C2 · trim warm gold #E8B04B · Core cyan #22D3EE · Module violet #8B5CF6 · Booster orange #FF7A1A · wheels #1B1B1F.

## Greybox dimensions (Unity units, 1u = 1m)
- Overall: L 4.0 × W 2.0 × H 2.4 (barrel at rest elevation).
- Chassis: 3.6 × 1.8 × 0.6 box, top at y=1.0. Wheels: 4 per side, Ø0.8, width 0.3, axle y=0.4.
- Turret: trapezoid 1.6 (base) / 1.0 (top) × 1.4 × 0.7, centred x=-0.2.
- Core sphere: Ø0.9, centred on barrel pivot (y≈1.5).
- Barrel: cylinder Ø0.4, length 2.6, pivot at core, elevation range 5°–70°, yaw ±60°.
- Module pod: 0.6 × 0.5 × 0.4, mounted 0.8 along barrel.
- Booster ring: Ø0.6, depth 0.3, at barrel tip. Projectile spawn point = booster centre + 0.2 forward.
- Pivot/root at ground centre of chassis. Camera framing: Wrecker occupies bottom-left ~20% of screen.
