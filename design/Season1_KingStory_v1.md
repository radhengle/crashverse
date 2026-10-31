# Season 1: The Tyrant King (v1, 31 Oct 2026)

Owner: sole developer (PM-approved scope). Phases: 1 = visual overhaul (this PR), 2 = King fight + map + Daily Boss, 3 = chests, blueprints, characters, cannons.
Visual target: `design/level_concepts_v1/*.jpg`. Real-money purchases stay OFF. Diamonds are earn-only in Season 1.

## 1. Story
**King Grimhold the Iron-Fisted** rules the **Kingdom of Ironvale**, a chain of sunny islands, from his Palace on Crown Rock. He taxes the
harbour folk into ruin and walls off the sea. The player is **Captain Rook**, a dock-worker with an old navy-and-gold cannon, "the Wrecker".
Each level knocks down one royal structure on the road to the palace. Every building carries Ironvale's colours: crimson banners with a gold
iron-fist crest, royal flags on the roof peaks, guard props (spear racks, shields, barrels with the crown stamp).

| # | Level (data id, kept) | Kingdom re-theme | Mechanic / gimmick (unchanged) | Shots |
|---|---|---|---|---|
| 1 | c1_l01 Wooden Lookout Hut | **Royal Watch Post** on the harbour stilts | aim + arc | 4 |
| 2 | c1_l02 Stilt Water Tower | **King's Cistern** (tax-water tower) | find every support | 5 |
| 3 | c1_l03 Brick Bakery | **Royal Bakery** (bread for the castle only) | brick needs support loss | 5 |
| 4 | c1_l04 Windmill | **Crown Mill** (grain levy) | collapse chains | 5 |
| 5 | c1_l05 Twin Cottages | **Tax Collectors' Lodges** on the bridge | one shot, two targets | 5 |
| 6 | c1_l06 Steel-Braced Granary | **Royal Granary** (steel-bound hoard) | steel joints 2x | 5 |
| 7 | m1_clockwork_tower Clockwork Tower | **Curfew Clocktower** | joint sequencing, Earthquake | 5 |
| 8 | c1_l08 Market Arcade | **Royal Market Arcade** (toll hall) | loadout choice, Multi Shot | 6 |
| 9 | c1_l09 Lighthouse Gauntlet | **Sea-Wall Beacon** (blockade lighthouse) | tests everything | 5 |
| 10 | c1_l10 (replaces the Tidewarden target) | **The King's Palace**: keep, towers, gatehouse, throne-room spire | structural destruction + hidden core; King fight moves to Phase 2 | 7 |

The Tidewarden encounter is intentionally retired from Chapter 1 in Phase 1. L10 is now a structural Palace level; the King Grimhold boss encounter is a separate Phase 2 Hard Level. The old Tidewarden boss layout remains in the project for historical/reference use, but its boss logic is not used by the Palace.

## 2. Hard Level: "The Iron King" (map node after L10, not L11)
Crowned-skull node beside the palace. Unlocks after L10 (any stars). Arena = the ruined palace balcony over the sea.
- **King Grimhold** rides a war platform that slides left/right along the balcony rail (2.5 m/s in phase 1).
- **Attacks:** throws a flaming barrel or bomb at the cannon every 4 s (phase 1). The player can shoot it down mid-air; a barrel that lands costs **1 step** (shot).
- **Royal Shield:** raises a gold tower shield for 2.0 s (projectiles bounce), telegraphed by a 0.6 s glow.
- **Stone Guards:** summons 1-3 breakable stone guard walls in front of him (brick HP x1.5).
- **Phases** by HP: 100-66 % (speed 2.5 m/s, throw every 4 s, 1 wall); 66-33 % (3.5 m/s, 3 s, shield every 8 s, 2 walls); 33-0 % (4.5 m/s, 2.2 s, double barrels, 3 walls).
- **Weak point:** the crown jewel, 3x damage; body hits 1x. 12 shots. Stars: 1 = defeat, 2 = defeat with >= 4 shots left, 3 = >= 7 left.
- **Reward:** King's Treasure chest (see 4) on first defeat; a Royal Gold chest on replays (max once a day).

**L11+ = Season 2** ("The Frozen Armada", new storyline): a locked node beyond the palace on the map.

## 3. Hidden Core (every building)
- At level load one block is the **core host**, picked from that layout's candidate blocks (interior, non-roof, non-island, supported pieces)
  with the cross-platform `SimRng(seed = runSeed XOR FNV1a(levelId))`, so ranked replays pick the same one. Nobody can see it at first.
- **Exposure:** when the host itself is damaged to <= 66 % HP, or any block touching it (support or dependent) breaks, the core is exposed:
  a crown-stamped TNT crate appears on the host's cannon-facing side, with a pulsing orange fuse glow and sparks.
- **Detonation:** a direct hit on the exposed host (or the host breaking while exposed) detonates the core: radial damage 400 within 6 m
  (falls off to 50 % at the edge), impulse 1800, plus fireball, smoke column, shockwave ring, camera shake. That usually levels the building.
  If the host breaks before exposure, the core is a dud (small fizzle, no bonus).
- Score: +1 star-progress bonus is not given; the explosion's destruction counts normally toward the win %.

## 4. Star chests (after every level)
Currencies: **Silver** (common), **Gold** (rarer), **Diamonds** (premium, earn-only in S1). Blueprints: shards for a character or cannon.
| Chest | Earned by | Silver | Gold | Diamonds | Blueprint shards | Rarity odds per shard pack (Common / Rare / Epic / Legendary) |
|---|---|---|---|---|---|---|
| Wooden (basic) | 1 star | 80-120 | 0-5 (30 %) | 0 | 1 pack x 2 shards | 80 / 18 / 2 / 0 |
| Silver (medium) | 2 stars | 180-260 | 10-20 | 1 (20 %) | 2 packs x 3 | 65 / 28 / 6 / 1 |
| Royal Gold (high) | 3 stars | 400-550 | 30-50 | 3-5 | 3 packs x 5 | 45 / 38 / 14 / 3 |
| King's Treasure | Hard Level first win | 2,000 | 250 | 40 | 1 guaranteed Epic pack (10) + 2 Rare packs (6) | Legendary chance 10 % |
Replaying a level only pays the chest for stars above the best so far (1 -> 3 stars pays Silver + Royal Gold once).

## 5. Characters (hero beside the cannon)
The hero stands at the cannon. Drag sideways on the lower third: the hero pushes the cannon along a 6 m rail (left/right, +-3 m); drag up/aim and FIRE as now.
One active power per level (button on the HUD).
| Hero | Rarity | Power (once per level) | Unlock |
|---|---|---|---|
| **Captain Rook** (starter) | Common | **Steady Aim**: shows the full trajectory for the next 2 shots | free |
| **Bessa Ironhand** (blacksmith) | Rare | **Hot Shot**: next shot +60 % damage and sets wood on fire (burn 3 s) | 30 shards, or 2,500 Gold, or 150 Diamonds |
| **Pip the Sapper** | Epic | **Find the Core**: reveals the hidden core's host for 3 s | 60 shards, or 6,000 Gold, or 350 Diamonds |
| **Lady Vex, the Exiled Knight** | Legendary | **Second Wind**: +1 step (shot) once per level | 120 shards, or 800 Diamonds |
Hero levels 1-5 via duplicate shards + Silver (500 / 1,200 / 2,500 / 5,000), each level = power +10 %.

## 6. Cannons
| Cannon | Rarity | Power (passive) | Unlock | Upgrade (lv 1-5) |
|---|---|---|---|---|
| **The Wrecker** (navy + gold, starter) | Common | Balanced: 60 damage, standard arc | free | Silver 400 / 900 / 1,800 / 3,600 |
| **Twin Fang** | Rare | Twin barrel: 2 balls side by side, 0.6x damage each | 25 shards or 2,000 Gold | Silver + Gold |
| **Old Thunder** (mortar) | Rare | High arc, lands on roofs: +40 % damage to roof/top pieces | 25 shards or 2,000 Gold | Silver + Gold |
| **Storm Caller** | Epic | Chain lightning: hit arcs to 2 nearby pieces for 35 % | 50 shards or 300 Diamonds | Gold + Diamonds |
| **Dragon's Breath** | Legendary | Fire shot: wood burns 3 s (10 dmg/s), +25 % vs wood | 100 shards or 700 Diamonds | Gold + Diamonds |
Cannon blueprints drop from chests (cannon:character shard ratio 1:1). Upgrades: +8 % damage per level.

## 7. Map (Season 1 = the Kingdom of Ironvale)
Same approved UI style. The path winds from the harbour (L1-3) through the mill farms (L4-6) and the clocktower town (L7-9) up to the
**Palace on Crown Rock (L10)**. The **Hard Level** node (gold crowned skull) sits beside the palace. Beyond it, a fogged, locked **Season 2** node.

## 8. Daily Boss: Ironjaw Golem v2 (redesign)
The King's siege golem: a 9 m stone-and-iron construct with a riveted iron jaw, glowing molten chest core (weak point), banner of Ironvale
on its back, moss-free cut granite blocks, gold trims. Built from fracturable Kit_RealStone/Steel pieces; the chest core uses the hidden-core
glow. Telegraphed slam animation; arena = a quarry shore at sunset. Same rules/data as today; visuals rebuilt to the concept-art standard.

## 9. Phases
- **Phase 1 (this PR):** the concept-art look in game: sky, ocean, island, foliage, kingdom-dressed buildings, big navy/gold cannon on a
  harbour deck, impact/debris/dust/explosion VFX, hidden core (glow + big explosion), L10 = King's Palace, lighting.
- **Phase 2:** the Hard Level (King Grimhold fight), the Ironvale map, Daily Boss v2.
- **Phase 3:** chests, currencies, blueprints, heroes + cannon rail movement, cannon types, upgrade screens.
