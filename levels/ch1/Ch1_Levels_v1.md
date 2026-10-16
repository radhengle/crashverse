# CRASHVERSE — Chapter 1 Sunnyville Isles, Levels v1 (L1–L10)
Rev 2 applies Level_Rulings_v1 (Game designer). Owner: Level Designer · Branch `level-designer/ch1-levels` · Sources: M1 Contract v0.2, Design Pack v1, Spec Gap Answers v1, Gameplay Feel v1, Addendum v1.1, Content Roadmap Addendum v1, Art v2 Decisions v1.

**Data format:** Developer's loader (`ContentModels.cs`: `ChapterData/LevelEntry` in `Resources/Data/chapter1.json`, `LayoutData/BlockData` in `Resources/Data/Layouts/<layout>.json`). Source of truth for this pass is `levels/ch1/gen_levels.py`. It writes `levels/ch1/layouts/*.json` and `levels/ch1/chapter1_levels.json`, and the same files are copied into `Assets/_Project/Resources/Data/`.
**Rules used:** HP wood 30 / brick 60 / steel 120. Mass per piece 1 / 2.5 / 4. A direct hit on a steel joint does 2× damage. A shot does 60 (`wrecker.json`). Win = destroyed objective mass ≥ 0.85. Stars = shotsLeft/shots ≥ star3Pct (0.60) / star2Pct (0.30). For B=4, 3★ needs 1 shot used; for B=5 or 6, 3★ needs ≤2 used.
Stone bases follow ruling 6: `Indestructible` + `NonObjective`, plain grey `8A8A8A`, role `island`, and outside totalObjectiveMass. That covers the L7 plinth and the L10 sea-stack too. A script checked that no stone piece is a hint target or on an optimal line. Stone also stops the one-shot base exploit in Developer's v0 layouts (L4, L6 and L7 were won by 1 shot on the base).
**Debris (ruling 7):** a piece that collapses deals min(60, mass × v) splash to the intact piece it lands on if v > 2 m/s, where v = √(2·g·drop). Pieces that fall together, and pieces landing on static or stone, take no damage (as in the code). Every level was re-checked with debris on.

## Simulator proof (`python3 sim.py` → `sim_results.json`)
Min = the fewest shots to win, found by exhaustive BFS. Need3 = the most shots you can use and still get 3★. Margin = shots − min. Rand3/RandWin = the share of 4,000 random-target runs that 3★ or win at all (a "not trivially 3-starred" check).

| L | Structure | Shots | Stars bands | Min (pw) | Need3 | Optimal line | Final % | Margin | Rand3 | RandWin |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Lookout hut on a single stilt | 4 | .60/.30 | 1 | 1 | post | 100 | 3 | 28% | 85% |
| 2 | Stilt water tower on a wood sill | 4 | .60/.30 | 1 | 1 | sill | 100 | 3 | 11% | 54% |
| 3 | Brick bakery on two wood props | 5 | .60/.30 | 2 | 2 | propL, propR | 100 | 3 | 13% | 68% |
| 4 | Windmill: brick drums on wood ring-beams | 5 | .60/.30 | 2 | 2 | beamL, beamR | 100 | 3 | 3% | 66% |
| 5 | Twin cottages on one bridge | 5 | .60/.30 | 1 | 2 | bridge | 100 | 4 | 39% | 96% |
| 6 | Granary on 2 steel brace joints | 5 | .60/.30 | 2 | 2 | braceL, braceR | 86.4 | 3 | 3% | 65% |
| 7 | Clockwork Tower (M1), Shockwave forced | 5 | .60/.30 | 2 | 2 | scaffold, ring0 | 95.4 (crown 100% unbroken) | 3 | 0.4% | 12% |
| 8 | Market arcade, 4 keystones, Splitstrike forced | 5 | .60/.30 | 4 (2 with Split) | 2 | key0, Split(key1+2+3) | 100 | 3 | 0% | 23% |
| 9 | Gauntlet: lighthouse + seawall | 6 | .60/.30 | 2 | 2 | lhRing, seaDeck | 92.9 | 4 | 4% | 73% |
| 10 | Tidewarden boss (defeat to win) | 8 | **.375/.25** | 5 (spec); current code needs 6 | 5 | shoulderL, shoulderR, core (open), non-shutter target (closed), core (open) | n/a | 3 | n/a | n/a |

Old linear curve note (superseded by the fun-pass table below): learn by hitting one obvious support (L1–2) → two supports needed (L3–4) → a shared support as a breather (L5) → steel joints (L6) → hidden joint plus sequencing (L7) → 3★ only with the right power-up (L8) → two structures with a tight 3★ (L9) → boss.


## Fun pass: rhythm, twists and "aha" moments (rev 3)
Difficulty follows an easy/medium/hard rhythm with a breather after each spike, not a straight climb. A level's tag comes from its new idea plus the sim numbers: Rand3 (how often random play gets 3★) and the gap between the minimum and the shot budget.

### Difficulty curve
| L | Tag | New idea / twist | Chain-collapse "aha" | Shape | Materials | Rand3 | RandWin |
|---|---|---|---|---|---|---|---|
| 1 | Easy | First shot: one post holds the whole hut | Post snaps → deck, 4 walls and roof all drop | Hut on a single stilt | wood | 28% | 85% |
| 2 | Easy | Hit low, not high: the tank is a decoy target | Sill breaks → 4 stilts and the 3-piece tank topple, and can roll off the edge | Tall, thin tower | wood | 11% | 54% |
| 3 | Medium | Brick won't break cheaply; take away its legs | Two wood props → the whole brick shell and chimney fall | Squat box on stilts | wood + brick | 13% | 68% |
| 4 | **Hard (spike)** | A wall you shouldn't shoot: the wood beams are the key | Beams → drum → cap → top drum → sails spin off (the longest chain so far) | Tall stacked cylinder with sails | wood + brick | 3% | 66% |
| 5 | **Breather** | One shot, two targets | Bridge snaps → both cottages fall together | Wide horizontal span between islands | wood + brick | 39% | 96% |
| 6 | Medium | Steel is tough, but gold joints take 2× | Two braces → the deck, 4 bins, loft and roof all drop | Box on legs with a decoy joint | brick + steel | 3% | 65% |
| 7 | **Hard (spike)** | A hidden weak point: open the scaffold first | Ring0 → the whole 3-tier tower and crown drop as one stack; the crown can land intact | Tallest vertical tower | wood + brick + steel | 0.4% | 12% |
| 8 | **Breather** (with the guided power-up) | A forced power-up: Splitstrike breaks 3 keystones at once | One split shot → 3 market bays fall in a ripple | Long, low arcade | all 3 | 0% random / guided | 23% |
| 9 | Hard | Two structures, exact order, no power-ups for 3★ | Ring → lighthouse topples; deck → seawall and its crest fold | Two-part scene (tall + wide) | all 3 | 4% | 73% |
| 10 | Boss | Phases, timing and a readable tell | Both shoulders → arms fall off and the chest opens; core ×2 → collapse cinematic | Humanoid automaton on a sea-stack | all 3 | n/a | n/a |

The rhythm is E E M **H** B M **H** B H Boss. Breathers follow the L4 and L7 spikes. L8 counts as a breather because the briefing forces Splitstrike and the hint points at the keystones: once the player sees the idea, it's a 2-shot clear. A random player still fails it, so 3★ isn't free. Shapes alternate tall, wide and boxy, and materials step from wood → wood+brick → brick+steel → all 3.

### Why it's fun (one line each)
1. A single shot drops the whole hut, so you get a big payoff in the first second.
2. The tank looks like the obvious target, but a low hit sends it toppling, maybe off the cliff.
3. Shooting brick feels useless until you knock the props out and it all slumps at once.
4. Following the chain down the windmill and watching the sails fly off is the chapter's first "whoa".
5. A relief level: one clean bridge shot clears two houses.
6. The gold joints teach you to spot weak points; steel columns that won't break make the joints feel clever.
7. A puzzle within a puzzle: open the scaffold, find the hidden ring and drop the whole tower, with a bonus for keeping the crown intact.
8. A power-up showcase where one split shot does three shots' work.
9. A real exam: two structures, two exact shots, and no crutches for 3★.
10. A boss you read rather than grind: break the shoulders, time the shutter and land two core hits.

## Per-level intent and mastery
1. **Lookout hut** (Harbor). Teaches aim and fire. One centre post holds the deck, walls and roof. Mastery (ruling 5): ≥95% destroyed. The post line gives 100%.
2. **Stilt water tower**. Teaches hitting supports, not walls. Shooting the sill drops the stilts and the tank. Mastery: "Topple the tank off the island edge" (SG-21: the tank falls below islandKillY −2); in the real physics, check that the tank rolls off the edge.
3. **Brick bakery**. Teaches that brick needs support loss. The brick shell sits on two half-slabs, each on a wood prop. Shooting brick faces costs one shot per piece. Mastery: ≥95%; the optimal line gives 100%.
4. **Windmill**. Teaches collapse chains. Breaking both wood beams chains through drum1 → cap → drum2 → hub, sails and roof. Mastery: 3★ with no Wall hit. The beams carry role `support` and the drums carry role `wall`. The drum1F+drum1B route also reaches 3★ but hits walls.
5. **Twin cottages**. Teaches one shot, two targets. Breaking the shared wood bridge drops both cottages. Mastery: both in 1 shot, which is achievable.
6. **Granary**. Teaches that steel joints take 2×. Each gold brace breaks in one shot (60×2 ≥ 120). Mastery: break all 3 joints; the decor joint braceM is the 3rd shot, so it's achievable with 3★ impossible (mastery is a separate tick, SG-03).
7. **Clockwork Tower**. Teaches joint sequencing. The wood scaffold hides ring0; opening it and breaking ring0 drops the whole shaft and crown. Shooting ring1 first only gets 58%. Shockwave is forced but doesn't shorten the line. Mastery: crown ≥80% unbroken. With debris on, the 2-shot line keeps 4/4 crown pieces unbroken (the crown falls as one stack onto stone or ground, which does no damage), so it's achievable. A playtest still needs to check tumble collisions. The 2-shot tuning is approved (ruling 4).
8. **Market arcade**. Teaches choosing a loadout. The four bays each hang from a wood keystone, 1.5 m apart. One Splitstrike (3×30 dmg) breaks three keystones. Without it, a win takes 4 shots and gives 1★. Mastery: the 3★ tick.
9. **Lighthouse gauntlet**. Tests everything. The steel ring (joint) drops the lighthouse, and the brick sea-deck drops the seawall. Only exactly 2 correct shots get 3★. Mastery: no power-up (using one caps the run at 2★).
10. **Tidewarden**. Boss phases, 8 shots. Phase 1: both steel shoulders, 1 shot each. Phase 2 (ruling 3): the shutter is open when phase 2 starts, and the shot that ended phase 1 doesn't toggle it. It toggles after each phase-2 shot, and the core needs 2 direct hits while it's open. The win is the defeat, not %. Bands are .375/.25: a perfect 5-shot run gets 3★ (3/8) and 6 shots gets 2★. Mastery: "Defeat the Tidewarden without hitting the closed shutter", so the closed-turn shot has to go somewhere else (for example the head).

## Sim assumptions (approximations)
Camera is on the +z side. A piece is hittable unless an intact piece in front fully covers it in x and y. Debris follows the ruling-7 model above (first landing only). A collapsed piece counts as destroyed if its base is ≥1.0 m up (the tracker's "dropped >1m" rule). Splitstrike sub-hits must be within 3.5 m of each other. Shockwave does 40 to neighbours 1 link deep. The Unity physics still needs a playtest, especially joint break forces and the L2/L7 masteries.
