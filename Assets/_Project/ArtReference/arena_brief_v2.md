# CRASHVERSE – Sunnyville Floating-Island Arena v2
Image: arena_v2_sunnyville_moodboard.png. A saturated floating-island diorama: grass top, brown cliff underside with crack lines, waterfall off one edge, small satellite islands, puffy outlined clouds and a bright blue sky. The Wrecker sits on a sand pad on the left. Targets are a wooden water tower, a brick house, a steel gantry and an explosive barrel.

## Palette
Sky top #2F8CFF to horizon #BDEBFF · sun glow #FFF6C8 · clouds #FFFFFF, shade #CFE3F7, outline #9FC2E6 · grass #7ED957 (light #A6F07A, dark #4CA83A) · cliff #C98A4B to #4A2E18, cracks #6B4226 · sand pad #FFE7A8 / #E6C88F · waterfall #E6FBFF to #5CC8F0 · trees #7BE36A / #2F7D32 · roofs #4D7BC4 / #1E3A6B · wood #E3A766 · brick #D2664C · steel #AEB6C2 · hazard #E5484D · gold #E8B04B · UI navy #14213D. Cyan #22D3EE is for the Wrecker only and never appears in the environment.

## Lighting (bright late morning, about 11:00)
- Key: Directional, #FFF2D6, intensity 1.4, rotation (50°, -35°, 0), coming from top-right. Soft shadows, strength 0.6.
- Fill/sky: Gradient ambient. Sky #8CC8FF, equator #DCEFFF, ground #6FAE5A, intensity 1.15.
- Rim: Directional #FFFFFF, intensity 0.35, from back-left, for the glossy edge pop.
- Fog: linear #CFEAFF, start 80 / end 250. It fades the satellite islands, not the arena.
- Post: bloom threshold 1.0 / intensity 0.6, saturation +15, contrast +10, no vignette.
- Shading: glossy stylized (smoothness about 0.5 on props, 0.7 on metal and barrels). An inverted-hull outline about 0.02u in #101828 is optional, but the boards support it.

## Material readability (M1 rules kept, colours brightened)
| Material | Base / value | Edge wear | Break FX |
|---|---|---|---|
| Wood | #E3A766 warm orange, about 60% value | Raw edges #F5C994, plank lines #6B4226 | Splinters #F5C994 + dust #CFA779 |
| Brick | #D2664C terracotta, about 45% | Chipped corners #E9997C, mortar grid #7A3326 always visible | Red chunks + dust #D9A37E |
| Steel | #AEB6C2 cool grey, about 65%, smoothness 0.7 | Bright edges #E2E8F0 | Sparks #FFD27A + bits #3A3F48 |
Rules: each material keeps its own colour family. Damage is shown in 2 steps at most (darken + crack decal). Weak points get a gold #E8B04B ring. Explosives are red #E5484D with a white band. The cliff is darker and has no plank pattern, and it is indestructible terrain. Keep destructible wood brighter than the cliff.

## Greybox layout
Island top is an ellipse about 60 × 25u with its surface at y=0. The cliff underside tapers to about 18u deep. Wrecker pad at x=-20. Targets 25–40u away: water tower (h 10u), brick house (6 × 5 × 6u), steel gantry (8u span), and 1 barrel between the house and the gantry. The island edge is a kill plane for debris (it falls off-island for juice). Camera is low behind the Wrecker at about 15° pitch, FOV 50.
