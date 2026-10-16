# Content Roadmap Addendum v1 (answers for Sound Manager and Developer)
Owner: Game designer. Rough direction for audio and planning. Ch2+ names and themes are provisional and may change once the user decides on reference-board parity.

## 1. Chapters 2–5 (islands and themes)
| Ch | Region (island group) | Theme and sound palette | New mechanic | Guardian (L10) |
|---|---|---|---|---|
| 2 | Gearstorm City | Copper-and-steel factory islands: gears, steam, cranes, rail | Moving parts, mechanical weak points | Iron Colossus (mobile factory that builds defences) |
| 3 | Neon District | Glass towers, holograms, energy hum | Energy shields, glass shattering, energy chains | Clockmaker (rotating armour that exposes gears) |
| 4 | Crystal Frontier | Frozen peaks, ice, resonant crystal | Slippery ice, crystal resonance, redirected energy | Crystal King (protective energy redirected through the arena) |
| 5 | Titan's Reach | Storm fortresses, suspended platforms, thunder | Multi-stage structures, reinforced supports | Sky Fortress (destroy the stabilizers, then the core) |
Each chapter has 10 levels on its own floating-island cluster, joined by a bridge or sky-tram gate, the same pattern as Ch1.

## 2. Power-ups
The 6 power-ups are final for launch: Shockwave, Splitstrike, Gravity Well, Chain Reactor, Phase Piercer and Time Fracture. Weekly events can add temporary event-only versions, but no new permanent power-up is planned before Season 2.

## 3. Daily Titan arenas (Emberforge Titan)
Four starting arenas, all in a molten or volcanic setting:
1. Foundry Crater: a ring of smelters around a lava pool
2. Cinder Bridge: a narrow stone bridge over magma
3. Ash Citadel: crumbling fortress walls in falling ash
4. Obsidian Forge: a glassy black arena with heat vents
Each is combined with one of 6 daily rules, giving 24 combinations from server data.

## 4. Season 2 direction (rough)
Season 1 is "Wrecking Frontier" with Magnetic Mayhem, Seismic Surge, Gravity Games and Sonic Rupture.
Season 2 is "Stormfront", built on weather twists: Wind Shear (crosswind on shots), Tidal Pull (rising water lifts debris), Lightning Rod (strikes on steel), Fog Bank (limited visibility, sound-led aiming hints). Its finale is a storm-tower level.

## 5. Reload beat between shots
Yes. After each shot, the next FIRE unlocks only when both of these are true:
- physics has settled (no objective piece moving faster than 0.15 for 0.75s), and
- the reload animation has played for at least 1.0s.
Both run at the same time, so the gap is the longer of the two, usually 1.0–2.5s. Aiming is allowed during reload; only FIRE is locked. Audio cue: start the reload sound at the shot's impact, and play a "ready" click when FIRE unlocks.

## 6. Accessibility (PM approved)
The Reduce screen shake and Reduce motion settings are approved. Reduce motion switches on automatically when Android's Remove animations setting is on, and the player can still turn it off in the game.
