#!/usr/bin/env python3
"""Generates Ch1_Levels_v1.md (rev 7) from chapter1_levels.json, sim_results.json, ci_win_shots.json, frame_check.json
and ../daily_boss/daily_boss_results.json."""
import json, os
HERE=os.path.dirname(os.path.abspath(__file__)); J=lambda p: json.load(open(os.path.join(HERE,p)))
R={r['id']:r for r in J('sim_results.json')}; L={l['id']:l for l in J('chapter1_levels.json')['levels']}
CI={l['id']:l for l in J('ci_win_shots.json')['levels']}; F=J('frame_check.json'); DB=J('../daily_boss/daily_boss_results.json')
FR={(r['id'],r['layout']):r for r in F['rows']}
info=[
('c1_l01','Seagull Perch: T-shaped cliff lookout on one crooked post','wood','Post snaps → deck, both side cabins, hut, roof and bell all drop','The big roof is a decoy','One shot clears the whole cliff-top: the tutorial "aha" (confirmed in Unity: PlayMode + emulator smoke win 100% in 1 shot on the hint line).'),
('c1_l02','Water tower on two sills, plus a pump shed on a catwalk','wood','Sill + sill → four stilts → tank, and the catwalk drags the pump shed down (domino)','The tank lid is the obvious target and is useless','L1 taught "hit the support"; L2 teaches "find every support". 3★ allows one miss.'),
('c1_l03','Two-storey bakery (3D Modeler Bld_Townhouse, 0.8 scale) on two wood props','wood + brick','Two props → cellar deck → shop walls → Floor1 → timber upper storey → both roof halves and the chimney fold','The awning post holds only the awning','Brick feels useless until the props go and two storeys slump.'),
('c1_l04','Windmill with a brick annex','wood + brick','Beams → drum → cap → top drum → sails; the annex beam drops the annex','Drum faces reach 3★ but fail the no-wall mastery','The longest chain so far, through mill and annex.'),
('c1_l05','Twin cottages on a bridge, with a water-wheel and lantern string','wood + brick','Bridge → both cottages, the wheel and the lantern string','Brick walls are decoys','Breather: one clean shot and the whole scene tumbles.'),
('c1_l06','Grain silo on three gold steel joints, with a side hopper','brick + steel','Two side joints → deck → three drums, steel dome, chute, hopper and the hanging centre joint','The steel dome is a 120-HP trap','Two gold joints drop a 10 m silo; mastery asks for all 3 joints, which costs the 3rd star.'),
('c1_l07','Clockwork Watchtower (Bld_Watchtower, ~0.55 scale; Earthquake forced)','wood + brick + steel','Gold ring0 (in plain sight) → both storeys and the crown drop as one stack; then the annex or the crate stack','ring1 drops only the top half; ring0 alone stops at 82.7%; a pole or barrel is too light for 3★','Read the gold ring, then pick the heavy ground piece: the crown lands intact.'),
('c1_l08','Market arcade with a clock arch (Multi Shot forced)','all 3','One Multi Shot breaks 3 keystones → 3 bays, arch, steel sign and clock ripple down; one more shot for bay 4','The steel sign looks like the target','The power-up showcase: one shot does three shots\' work.'),
('c1_l09','Lighthouse + seawall, joined by a catwalk','all 3','Ring → lighthouse; deck → seawall and bell tower; the catwalk falls once both ends go','The catwalk is a trap','The exam: two structures, two exact shots, now on a tight 5-shot budget.'),
('c1_l10','The Tidewarden on a sea-stack (boss)','brick + steel + wood','Shoulders → arms fall and the chest opens; core ×2 → collapse','Shooting the closed shutter wastes a shot and fails mastery','A boss you read, not grind.')]
CH={'c1_l02':'**Changed:** one sill → two sills; 4 → 5 shots; bands 0.6/0.3 → 0.4/0.2 (3★ = 2–3 shots); hint sill → sillL.',
    'c1_l05':'**Changed:** recomposed 13 → 10.6 m wide (rocks, piers, bridge, cottages) to fit the fixed camera; same line.',
    'c1_l06':'**Changed:** braces role brace → steel_joint (mastery was impossible: it counts role steel_joint, and L6 had none); braceM now hangs under the deck (was on the rock), so the best line drops 100% instead of 87.1%.',
    'c1_l07':'**Changed (rev 7, PM overrode the M1 layout lock):** spike fixed. Scaffold removed so ring0 is visible (hint scaffold → ring0); front storey walls widened to hide the side walls (fewer, clearer targets); ground annex, crate stack, barrels and hoist poles (8.5 of 49 mass) stay standing, so ring0 + annex/crate stack = 2-shot 3★. Random win 6.8% → 30.2%, random 3★ 0.5% → 2.7%. 5 shots, bands, 3 steel rings and the crown-intact mastery kept. Season finale_e1 shares this layout and hint.',
    'c1_l08':'**Changed:** 5 → 6 shots (breather after L7; 3★ still needs Multi Shot); recomposed 14 → 10.4 m wide; keystones 1.6 m apart under their own bays (Multi Shot geometry verified: the split at key1 hits key0+key1+key2 at g×1.0 and ×1.2); hint key1 = the Multi Shot aim.',
    'c1_l09':'**Changed:** 6 → 5 shots ("tight budget" was 4 spare); rock trimmed 12 → 10.6 m for the frame.',
    'c1_l01':'Layout, shots, bands and hint unchanged (the only level with real Unity/emulator evidence; the CI smoke depends on it). Mastery text reworded.','c1_l03':'**Changed (rev 7):** re-laid as the Bld_Townhouse modules (G_F shop front/G_B/G_L/G_R, Floor1 wood, U_F/U_B/U_L/U_R timber, Roof_W/Roof_E, Chimney) on the same props → slabs support graph. Same 5 shots, line, 2-shot 3★ and ≥95% mastery (props + awning post = 100%).','c1_l04':'Layout unchanged; mastery text reworded.',
    'c1_l10':'Unchanged (Level_Rulings_v1 #1–3: 8 shots, 0.375/0.25, closed-shutter mastery).'}
o=["# CRASHVERSE — Chapter 1 Sunnyville Isles, Levels v1 (rev 7: building prefabs + L7 spike fix)",
"Owner: Level Designer · Branch `level-designer/ch1-tuning` (draft PR into `integration/v2`; supersedes #13, already merged to main) · Rules: Level_Rulings_v1 (#1–10, 7a), gravity ×1.2 (PR #15), powerups v2 ids (earthquake = SHOCKWAVE, multi_shot = SPLIT).","",
"**Pipeline:** `python3 sync_to_assets.py` (gen_levels → Resources layouts + chapter1.json → derive_hints at g×1.0) → `SIM_GSCALE=1.2 python3 sim.py` → `python3 ../daily_boss/daily_boss_sim.py` → `python3 ci_win_shots.py` → `python3 frame_check.py --json > frame_check.json` → `python3 mkdoc.py`.",
"**Gravity:** sim and ci_win_shots prove every line at g×1.2 (ruling #10 / PR #15). Results are identical at g×1.0. The in-game hint angles in chapter1.json stay at g×1.0 because integration/v2 still runs 9.81 (GameSession `_baseGravity`) and CI runs `derive_hints.py --check` at ×1.0; ci_win_shots.json carries both sets (`aim['1.0']`, `aim['1.2']`).","",
"## Sim table (g×1.2; 4,000 random-target runs per level; the random player fires a forced power-up on its first shot)",
"| L | Tag | Shots | ★ bands | Min (with power-up) | 3★ needs ≤ | Margin | Best line (= hint, CI) | Final % | Rand 3★ | Rand win | Debris cap hits | CI first-hit ×1.0/×1.2 | Pass |","|---|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
for i,(k,*_) in enumerate(info):
    r=R[k]; l=L[k]; mn=f"{r['min']}"+(f" ({r['min_pw']})" if r.get('min_pw') not in (None,r['min']) else "")
    pct=f"{r['final_pct']*100:.1f}" if r.get('final_pct') else 'defeat'; pc=lambda x: 'n/a' if x is None else f"{x*100:.1f}%"
    cap=f"{r['debris_capped']}/{r['debris_hits']}" if 'debris_hits' in r else 'n/a'
    o.append(f"| {i+1} | {r['tag']} | {l['shots']} | {l['star3Pct']}/{l['star2Pct']} | {mn} | {r['need3']} | {r['margin']} | {r['seq']} | {pct} | {pc(r.get('rand3'))} | {pc(r.get('randwin'))} | {cap} | {'✅' if CI[k]['all_clear'] else '❌'} | {'✅' if r['pass'] else '❌'} |")
o+=["","Rhythm: Easy, Easy, Medium, Hard, Breather, Medium, Hard, Breather, Hard, Boss. L10 min 5 = spec shutter (Level_Rulings #3); it would be 6 (2★) if the old phase-2 toggle bug came back.",
"Debris: no collision reaches the 60 cap in any level (ruling 10 check holds).","","## Levels"]
for i,(k,shape,mats,aha,risk,fun) in enumerate(info):
    l=L[k]; r=R[k]; fr=FR[(k,l['layout'])]
    o+=[f"### L{i+1}. {l['name']} — {shape} [{r['tag']}]",f"- {CH[k]}",
        f"- Materials: {mats} · Shots {l['shots']} · Bands {l['star3Pct']}/{l['star2Pct']} · Teaches: {l['teaches']}",
        f"- Goal: destroy {int(l['winThreshold']*100)}%" + (" (boss: defeat the Tidewarden)" if l.get('bossId') else "") + f" · Mastery: **{l['mastery']['text']}** · Hint: `{l['hintTarget']}` (yaw {l['hintYaw']}, pitch {l['hintPitch']} at g×1.0)",
        f"- Chain collapse: {aha}",f"- Risk/reward: {risk}",f"- Why it's fun: {fun}",
        f"- Proof: min {r['min'] if r.get('min_pw') is None else min(r['min'],r['min_pw'])} via {r['seq']}; 3★ needs ≤{r['need3']}; margin {r['margin']}.",
        f"- Frame (assumed fixed camera): x {fr['primary']['x']} px, y {fr['primary']['y']} px; slack {fr['primary']['slack_px']} px (tight cam {fr['tight']['slack_px']} px)."]
o+=["","## Daily Boss (meta_v2.json `dailyBoss` + titan layouts)",
f"Config now: baseShots {DB['config']['baseShots']}, shotSpread {DB['config']['shotSpread']} (rolls {DB['config']['baseShots']}–{DB['config']['baseShots']+DB['config']['shotSpread']}), winThreshold {DB['config']['winThreshold']}. Stars use the LevelEntry defaults 0.6/0.3.","",
"| Arena | Min | Best line (= arena hint) | Pelvis shot alone | " + " | ".join(f"{S} shots: rand win / rand 3★ / 3★ possible" for S in DB['arenas'][0]['by_shots']) + " | Frame slack |",
"|---|---|---|---|" + "---|"*len(DB['arenas'][0]['by_shots']) + "---|"]
for a in DB['arenas']:
    fr=FR[('daily_boss',a['layout'])]
    o.append(f"| {a['layout']} | {a['min']} | {a['seq']} | {a['pelvis_only']*100:.1f}% | " + " | ".join(f"{v['randwin']*100:.0f}% / {v['rand3']*100:.0f}% / {'yes' if v['three_star_possible'] else 'NO'}" for v in a['by_shots'].values()) + f" | {fr['primary']['slack_px']} px |")
o+=["",f"Next 60 days (DailyBossRules.ForDate replayed): 3★ possible every day = **{DB['next60']['all_three_star_possible']}**; shots {DB['next60']['shots_hist']}; arenas {DB['next60']['layout_hist']}.",
"","## Fixed-camera frame check (`frame_check.py`)",
"No fixed behind-the-cannon camera exists yet on integration/v2 or any branch/PR (CameraConfig is still the orbit rig: FOV 50, distance 22, yaw 72, pitch 15). **Assumed** until Developer's values land (edit `CAMERAS`):",
"- primary: vertical FOV 50°, eye at (−28, 5, 0) = 8 m behind / 5 m above the Wrecker, looking +x, pitch 8° down → cannon bottom-centre (~68–98% of screen height).",
"- tight (stress): FOV 50°, eye (−26, 4, 0), pitch 5°.",
"- Viewport 1080×2340 portrait; safe area 6% sides (65 px), top 10% (234 px HUD), bottom 80% (1872 px, cannon/FIRE rail). All 8 corners of every piece are checked.",
"Before rev 6: L8 failed (rock off both edges, −10 px / −40 px), L5 had 4 px slack on the tight camera, and all 4 titan arenas stood side-on to the cannon (arms and legs hidden behind each other)."]
open(os.path.join(HERE,'Ch1_Levels_v1.md'),'w').write("\n".join(o)+"\n")
print('written')
