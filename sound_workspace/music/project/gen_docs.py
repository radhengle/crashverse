#!/usr/bin/env python3
"""Regenerate music/README.md + music/loop_points.md tables from master_report*.json (single source of truth)."""
import json
META={'main_theme':('C maj','4-bar intro + 32-bar body'),'sunnyville_map_loop':('C maj','32 bars'),'gameplay_loop':('G maj','32 bars'),
'stinger_win':('C maj','IV-V-Imaj9 bloom, motif on piano'),'stinger_fail':('C','motif slides down'),
'season1_wrecking_frontier_hub':('F maj','32-bar frontier gallop'),'season2_stormfront_hub':('E min','32 bars rain/thunder'),
'stinger_win_3star':('C maj','win + climbing 3-star shimmer'),'stinger_boss_defeat':('Dm→D','generic boss defeat (v2; bosses now use their own)'),
'stinger_boss_phase2':('D min','v3 Tidewarden phase change: Shepard + tremolo → sub-boom hit at 1.25 s'),'stinger_tidewarden_defeat':('Dm→D','v3 Tide Call collapses → heroic hook, Dmaj9'),'stinger_titan_defeat':('Dm→D','v3 Forge Call hammered → sub-boom → anthem in D'),
'tidewarden_p1':('D min','v3: 4-bar intro + 32-bar arc (drop/call-response/breakdown/rebuild/gate build)'),'tidewarden_p2':('D min','v3: 32-bar arc, harmony shifted'),
'gameplay_harbor_isle':('G maj','2 + 32 bars'),'gameplay_mill_isle':('D maj','2 + 32 bars'),'gameplay_clocktower_isle':('A min','2 + 32 bars'),
'gameplay_storm_isle':('D min','4 + 16 bars'),'stinger_chapter_clear':('C maj','IV-V-vi-IV-V-Imaj9'),'results_loop':('C maj','8-bar bed')}
rows=[]
for f,pre in [('master_report.json',''),('master_report_b2.json',''),('master_report_b3.json','')]:
    for k,v in json.load(open(f)).items():
        if k.startswith('_') or k not in META: continue
        rows.append(((k+'_alt_piano' if k=='main_theme' else k)+'.ogg',v))
T=json.load(open('master_report_titan_v2.json'))
out=["# CRASHVERSE music v2 (in-house, zero spend)",
"Original music written as code. Background parts (drums, bass, synth pads, taiko, forge) use the numpy synth. All leads and melodies use sampled piano and guitar via FluidSynth + FluidR3_GM (MIT), low-passed at 7.5 kHz (bright) or 6 kHz (dark).",
"v1 renders are kept in _v1_archive/. Re-render everything with `bash project/render.sh`, which also regenerates this file via gen_docs.py.","",
"| File | Dur | BPM | Key | Structure | Loop (samples) | LUFS-I / dBTP (OGG) |","|---|---|---|---|---|---|---|"]
A=json.load(open('../_v1_archive/master_report.json'))['main_theme']
out.append(f"| **main_theme.ogg (v1, APPROVED by user 2026-10-16, frozen and byte-identical)** | {A['dur_s']:.2f} s | 112 | C maj | 4-bar intro + 32-bar body (v1 synth leads) | {A['loop_start']} → {A['loop_end']} | {A['LUFS_ogg']} / {A['TP_ogg']} |")
for n,v in rows:
    k=n[:-4].replace('_alt_piano',''); lp=f"{v['loop_start']} → {v['loop_end']}" if v.get('loop_start') is not None else 'one-shot'
    out.append(f"| {n} | {v['dur_s']:.2f} s | {v['bpm']} | {META[k][0]} | {META[k][1]} | {lp} | {v['LUFS_ogg']} / {v['TP_ogg']} |")
out+=["","## Titan (folder titan/), 150 BPM, D minor, 32 bars, sample-aligned","| File | Dur | Samples | LUFS / dBTP alone |","|---|---|---|---|"]
for k,v in T.items():
    if k.startswith('_'): continue
    out.append(f"| titan/{k}.ogg | {v['dur_s']:.2f} s | {v.get('samples_ogg',v['dur_s']*44100):.0f} | {v['LUFS_ogg']} / {v['TP_ogg']} |")
mx=T['_mixes_decoded']; L=[v['LUFS'] for v in mx.values()]; P=[v['TP'] for v in mx.values()]
out+=["",f"Shared gain {T['_shared_gain_db']} dB. Worst true peak over every arena + layer combination, including crossfade midpoints: {T['_worst_TP_any_combo']} dBTP. Arena + layer mixes measure {min(L)} to {max(L)} LUFS, peaking at {max(P)} dBTP.",
"Playback rule: one arena mix + one cumulative intensity layer (int25/int50/int75), with a 1 s equal-power crossfade between layers, + a stinger. That is at most 3 music voices (see mixing_unity.md)."]
open('../README.md','w').write("\n".join(out)+"\n")
lp=["# Loop points (44.1 kHz sample offsets; decoded OGG length verified equal to master)","| File | Total samples | Loop start | Loop end | Seam jump / p99 step (OGG) |","|---|---|---|---|---|"]
for n,v in rows:
    if v.get('loop_start') is None: lp.append(f"| {n} | {v['samples_ogg']} | – | – | one-shot |"); continue
    lp.append(f"| {n} | {v['samples_ogg']} | {v['loop_start']} | {v['loop_end']} | {v['boundary_jump_ogg']} / {v['p99_step_ogg']} |")
for k,v in T.items():
    if k.startswith('_') or 'preview' in k: continue
    lp.append(f"| titan/{k}.ogg | {v['samples_ogg']} | 0 | {v['loop_end']} | {v['boundary_jump_ogg']} / {v['p99_step_ogg']} |")
lp+=["","Seams are made by wrapping note and reverb tails past the loop end back onto the loop start (linear, before mastering), and the limiter is memoryless, so the seam stays continuous. A seam passes when its jump is below the 99th-percentile step between samples. Titan layers are also checked in every arena + layer mix (project/master_report_titan_v2.json).",
"Unity: for intro+loop files, either split them at the loop start or use a loop-region player. Start a new Titan layer with timeSamples equal to the arena mix's timeSamples."]
open('../loop_points.md','w').write("\n".join(lp)+"\n")
print('docs ok',len(rows),'tracks')
