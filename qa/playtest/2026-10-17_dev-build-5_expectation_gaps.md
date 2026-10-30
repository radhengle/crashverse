# dev-build-5: gaps vs user expectations
| Expectation | Current state (evidence) | Gap | Owner |
|---|---|---|---|
| Real-looking, bright, glossy objects | Primitives in flat colours, magenta world (artifacts/dev-build-5/emulator-smoke-api36/06_l1_hud.png) vs art/v2/board_v2.png | Materials, PBR/toon shaders, modelled props (castle, cannon, islands), import pipeline | 3D Modeler, VFX & Tech Artist |
| Explosions | Zero ParticleSystems on main; PR #2 has shaders only | Explosion, dust, spark and glass-shard prefabs; VfxDirector hookup | VFX & Tech Artist |
| Sound | No SFX; music not packaged (checklist #19/#20/#24) | Per-material impacts, collapse layers, UI clicks, music in Resources, logo sound | Sound Manager, Gameplay Developer |
| Destruction feel | 0% destroyed after 4 shots (artifacts/dev-build-5/emulator-smoke-api36/08_result_fail.png); minimal StructuralIntegrity | Collapse/support graph (#11/#12), debris, crack stages, shake, hit-stop and haptics for every impact | Physics Developer, VFX & Tech Artist, Gameplay Developer |
| Satisfying aim | Bare arc, no cannon | Cannon model plus recoil anim, power feedback | 3D Modeler, Animator, Gameplay Developer |
| 10 good levels | Only L1 reached in CI | Merge #12/#13, play and tune all 10 | Level Designer, Game designer |
