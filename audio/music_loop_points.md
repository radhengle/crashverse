# Loop points (44.1 kHz sample offsets; decoded OGG length verified equal to master)
| File | Total samples | Loop start | Loop end | Seam jump / p99 step (OGG) |
|---|---|---|---|---|
| main_theme_alt_piano.ogg | 3402000 | 378000 | 3402000 | 0.00157 / 0.12635 |
| sunnyville_map_loop.ogg | 3256615 | 0 | 3256615 | 0.0427 / 0.15225 |
| gameplay_loop.ogg | 2822400 | 0 | 2822400 | 0.00998 / 0.15677 |
| stinger_win.ogg | 176400 | – | – | one-shot |
| stinger_fail.ogg | 132300 | – | – | one-shot |
| season1_wrecking_frontier_hub.ogg | 2919724 | 0 | 2919724 | 0.00897 / 0.11713 |
| season2_stormfront_hub.ogg | 3386880 | 0 | 3386880 | 0.00619 / 0.08529 |
| stinger_win_3star.ogg | 211680 | – | – | one-shot |
| stinger_boss_defeat.ogg | 245858 | – | – | one-shot |
| stinger_boss_phase2.ogg | 122744 | – | – | one-shot |
| stinger_tidewarden_defeat.ogg | 245858 | – | – | one-shot |
| stinger_titan_defeat.ogg | 246960 | – | – | one-shot |
| tidewarden_p1.ogg | 2646000 | 294000 | 2646000 | 0.01437 / 0.09239 |
| tidewarden_p2.ogg | 2352000 | 0 | 2352000 | 0.0227 / 0.09954 |
| gameplay_harbor_isle.ogg | 3271418 | 192436 | 3271418 | 0.00329 / 0.1431 |
| gameplay_mill_isle.ogg | 3102207 | 182483 | 3102207 | 0.0061 / 0.1578 |
| gameplay_clocktower_isle.ogg | 2998800 | 176400 | 2998800 | 0.00874 / 0.15614 |
| gameplay_storm_isle.ogg | 2205000 | 441000 | 2205000 | 0.00852 / 0.10369 |
| stinger_chapter_clear.ogg | 229320 | – | – | one-shot |
| results_loop.ogg | 846720 | 0 | 846720 | 0.00803 / 0.0496 |
| titan/titan_foundry_crater.ogg | 2257920 | 0 | 2257920 | 0.02436 / 0.07493 |
| titan/titan_cinder_bridge.ogg | 2257920 | 0 | 2257920 | 0.02498 / 0.07418 |
| titan/titan_ash_citadel.ogg | 2257920 | 0 | 2257920 | 0.01896 / 0.07424 |
| titan/titan_obsidian_forge.ogg | 2257920 | 0 | 2257920 | 0.03624 / 0.07386 |
| titan/titan_int25.ogg | 2257920 | 0 | 2257920 | 0.00579 / 0.05289 |
| titan/titan_int50.ogg | 2257920 | 0 | 2257920 | 0.0073 / 0.0529 |
| titan/titan_int75.ogg | 2257920 | 0 | 2257920 | 0.04754 / 0.07594 |

Seams are made by wrapping note and reverb tails past the loop end back onto the loop start (linear, before mastering), and the limiter is memoryless, so the seam stays continuous. A seam passes when its jump is below the 99th-percentile step between samples. Titan layers are also checked in every arena + layer mix (project/master_report_titan_v2.json).
Unity: for intro+loop files, either split them at the loop start or use a loop-region player. Start a new Titan layer with timeSamples equal to the arena mix's timeSamples.
