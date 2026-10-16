#!/usr/bin/env python3
"""Generates audio/sfx_routing.json + .csv from the design CSVs and the files present in Resources/SFX.
Usage: python3 audio/tools/gen_sfx_routing.py <design_audio_dir>   (run from repo root)"""
import csv, json, os, re, sys, glob, collections
D = sys.argv[1] if len(sys.argv) > 1 else '/workspace/crashverse/audio'
RES = 'Assets/_Project/Resources/'
files = sorted(p[len(RES):-4] for p in glob.glob(RES + 'SFX/**/*.ogg', recursive=True) + glob.glob(RES + 'SFX/**/*.wav', recursive=True))
byid = collections.defaultdict(list)
for f in files: byid[re.sub(r'_\d{2}$', '', f.rsplit('/', 1)[1])].append(f)
ALIAS = {'sfx_cannon_reload': 'sfx_cannon_reload_loop', 'sfx_steel_joint_snap': 'sfx_steel_jointsnap',
         'sfx_pu_gravity_well_fx': 'sfx_pu_gravity_well_fx_loop', 'sfx_pu_time_fracture_fx': 'sfx_pu_time_fracture_fx_loop',
         'sfx_titan_stage_25': 'sfx_titan_stage', 'sfx_titan_stage_50': 'sfx_titan_stage',
         'sfx_titan_stage_75': 'sfx_titan_stage', 'sfx_wrecker_aim_hum': 'sfx_wrecker_aim_hum_loop', 'sfx_aim_stretch': 'sfx_aim_stretch_loop',
         'sfx_projectile_whoosh': 'sfx_projectile_whoosh_loop', 'sfx_hint_pulse': 'sfx_hint_pulse_loop', 'sfx_impact_terrain': 'sfx_terrain_impact'}
STAGEVAR = {'sfx_titan_stage_25': 0, 'sfx_titan_stage_50': 1, 'sfx_titan_stage_75': 2}
MATS = ['wood', 'brick', 'steel', 'glass', 'stone']
PU = ['shockwave', 'splitstrike', 'gravity_well', 'chain_reactor', 'phase_piercer', 'time_fracture']
TITAN_RULES = ['heated', 'magma', 'steam']
GROUP = {'Destruction': 'SFX/Destruction', 'Weapon': 'SFX/Weapon', 'PowerUps': 'SFX/PowerUps', 'Boss': 'SFX/Boss',
         'Reward': 'SFX/Rewards', 'Combo': 'SFX/Rewards', 'Results': 'SFX/Rewards', 'UI': 'UI'}
sfx = {r['id']: r for r in csv.DictReader(open(f'{D}/sfx_list.csv'))}
missing = collections.defaultdict(set)

def resolve(i):
    a = ALIAS.get(i, i)
    v = byid.get(a, [])
    if i in STAGEVAR and v: v = [v[STAGEVAR[i]]] if len(v) > STAGEVAR[i] else v
    if not v:  # size-split banks: id_s/_m/_l
        v = sorted(byid.get(a + '_m', []) + byid.get(a + '_s', []) + byid.get(a + '_l', []))
    return v

def group_of(paths):
    return GROUP.get(paths[0].split('/')[1], 'SFX') if paths else ''

def prio(i):
    p = (sfx.get(i) or {}).get('priority', '')
    return int(p) if p.isdigit() else 128

def expand(tok):
    if '{mat}' in tok: return [tok.replace('{mat}', m) for m in MATS]
    if '<name>' in tok: return [tok.replace('<name>', p) for p in PU]
    if '<heated|magma|steam>' in tok: return [tok.replace('<heated|magma|steam>', r) for r in TITAN_RULES]
    return [tok]

EXTRA = {'COLLAPSE_CREAK': ['sfx_creak_wood', 'sfx_creak_steel'], 'PU_TIME_END': ['sfx_pu_time_fracture_end']}
DROP = {'COLLAPSE_CREAK': {'sfx_collapse_creak'}}
def ids_in(text):
    out = []
    text = text.replace('sfx_star_1/2/3', 'sfx_star_1 sfx_star_2 sfx_star_3').replace('sfx_combo_tier2..4', 'sfx_combo_tier2 sfx_combo_tier3 sfx_combo_tier4')
    text = text.replace('sfx_titan_rule_<heated|magma|steam>', 'sfx_titan_rule_heated sfx_titan_rule_magma sfx_titan_rule_steam')
    text = text.replace('sfx_titan_rule_loop', 'sfx_titan_rule_heated sfx_titan_rule_magma sfx_titan_rule_steam')
    text = re.sub(r'sfx_titan_stage_25 / _50 / _75', 'sfx_titan_stage_25 sfx_titan_stage_50 sfx_titan_stage_75', text)
    for t in re.findall(r'sfx_[a-z0-9_{}<>|]+', text):
        for u in t.rstrip('_').split('|'):
            if u: out += expand(u)
    return list(dict.fromkeys(out))

def cooldown(notes):
    m = re.search(r'(cooldown|throttle)\s*(\d+)\s*ms', notes or '')
    return max(1, round(int(m.group(2)) / 20)) if m else 0

rows = []
def add(event, owner, ids, params='', cd=0, notes='', key=None):
    clips = []
    for i in ids:
        v = resolve(i)
        if not v: missing[i].add(event)
        clips.append({'sfx_id': i, 'variations': v, 'group': group_of(v), 'priority': prio(i)})
    rows.append({'event': event, 'key': key or {}, 'owner': owner, 'params': params, 'cooldown_ticks': cd, 'notes': notes, 'clips': clips})

for r in csv.DictReader(open(f'{D}/anim_audio_map.csv')):
    own = r[list(r)[3]].strip()
    ids = [i for i in ids_in(r['sfx_ids']) if i not in DROP.get(r['anim_event'], set())] + EXTRA.get(r['anim_event'], [])
    if own == '-' or not ids:
        add(r['anim_event'], 'visual' if 'PHYS' in r['notes'] or own == '-' else 'anim', [], notes=(r['sfx_ids'] + ' ' + r['notes']).strip()); continue
    add(r['anim_event'], 'anim', ids, r['frame_ms_at_60fps'], cooldown(r['notes']), r['notes'])
seen = {x['event'] for x in rows}
for r in csv.DictReader(open(f'{D}/audio_events_for_developer.csv')):
    if r['owner'].strip() != 'C' or r['event_name'].startswith('phys_'): continue
    add(r['event_name'], 'code', ids_in(r['sfx_ids']), r['params'], cooldown(r['notes']), f"trigger: {r['trigger']}; {r['notes']}".strip('; '))

# Supplementary code routes for library clips with no design trigger yet (proposed; Gameplay Dev to confirm).
add('BOSS_TIDE_ARMOR_HIT', 'code', ['sfx_boss_tw_hit_armor'], 'non-joint hit on Tidewarden armour', 4, 'PROPOSED: no anim event yet')
add('UI_COIN_BURST', 'code', ['sfx_reward_coin_burst'], 'reward payout >= 10 coins', 0, 'PROPOSED: play once instead of UI_COIN_LAND singles')
# PHYS_* rules (ImpactAudioProfile): material x kind x size; tier drives volume curve only.
KIND = {'PHYS_IMPACT': 'impact', 'PHYS_CRACK': 'crack', 'PHYS_BREAK': 'break'}
for ev, k in KIND.items():
    for m in MATS:
        for s in 'sml':
            add(ev, 'phys', [f'sfx_{m}_{k}_{s}'], 'tier,impulse,sizeClass,materialA/B,source', 4,
                'harder material wins; soft layer 0.3 Heavy only; minImpulse 5 (Impact only)' if k == 'impact' else ('+0.03 pitch per damageStep' if k == 'crack' else 'in active cascade: 1 voice + rattle'),
                key={'material': m, 'kind': k, 'size': s.upper(), 'tier': 'any'})
for s in 'sml':  # Terrain (MaterialType.Terrain, bank SFX/Destruction/Impacts): impact only
    add('PHYS_IMPACT', 'phys', [f'sfx_terrain_impact_{s}'], 'tier,impulse,sizeClass,materialA/B,source', 4, 'terrain = softest; used when the other body is Terrain', key={'material': 'terrain', 'kind': 'impact', 'size': s.upper(), 'tier': 'any'})
for s in 'sml':
    add('PHYS_JOINT_SNAP', 'phys', [f'sfx_steel_jointsnap_{s}'], 'impulse,sizeClass', 4, '', key={'material': 'steel', 'kind': 'jointsnap', 'size': s.upper(), 'tier': 'any'})
for m in MATS:
    add('PHYS_RATTLE', 'phys', [f'sfx_{m}_debris'], 'source=Debris aggregated', 5, 'rattleWindowTicks 5, rattleMaxVoices 3', key={'material': m, 'kind': 'rattle'})
    for s, band in zip('sml', ['1-5', '6-20', '21+']):
        add('PHYS_COLLAPSE_END', 'phys', [f'sfx_{m}_collapse_{s}'], 'cascadeId,cascadeCount,dominantMaterial', 0,
            f'one COLLAPSE_LAND per cascadeId; count {band}; fallbackTicks 150', key={'material': m, 'kind': 'collapse', 'size': s.upper(), 'count': band})
add('PHYS_COLLAPSE_START', 'phys', [], 'cascadeId,cascadeCount(planned)', 0, 'opens cascade gate; no sound')
for x in rows:
    if x['event'].startswith('PHYS_'):
        for c in x['clips']: c['priority'] = 32 if 'collapse' in c['sfx_id'] else c['priority']

used = {v for x in rows for c in x['clips'] for v in c['variations']}
out = {'generated_from': ['anim_audio_map.csv', 'audio_events_for_developer.csv', 'sfx_list.csv', 'ImpactAudioProfile (PR #18 patch)'],
       'resources_root': 'Assets/_Project/Resources/', 'path_note': 'Resources.Load<AudioClip>(path), no extension',
       'phys_source_priority': {'Cannon': 32, 'Explosion': 64, 'PowerUp': 96, 'Debris': 192},
       'phys_hardness': ['steel', 'stone', 'brick', 'glass', 'wood', 'terrain'],
       'routes': rows,
       'validation': {'files_in_pr': len(files), 'files_referenced': len(used), 'unreferenced_files': sorted(set(files) - used),
                      'missing_sfx_ids': {k: sorted(v) for k, v in sorted(missing.items())}}}
json.dump(out, open('audio/sfx_routing.json', 'w'), indent=1)
with open('audio/sfx_routing.csv', 'w', newline='') as f:
    w = csv.writer(f, lineterminator='\n'); w.writerow(['event', 'owner', 'key', 'sfx_id', 'variations', 'mixer_group', 'priority', 'cooldown_ticks', 'params', 'notes', 'status'])
    for x in rows:
        key = ';'.join(f'{a}={b}' for a, b in x['key'].items())
        if not x['clips']: w.writerow([x['event'], x['owner'], key, '', '', '', '', x['cooldown_ticks'], x['params'], x['notes'], 'silent'])
        for c in x['clips']:
            w.writerow([x['event'], x['owner'], key, c['sfx_id'], '|'.join(c['variations']), c['group'], c['priority'], x['cooldown_ticks'], x['params'], x['notes'], 'ok' if c['variations'] else 'MISSING'])
print('routes', len(rows), 'files', len(files), 'referenced', len(used), 'unreferenced', len(set(files) - used), 'missing ids', len(missing))
for k, v in sorted(missing.items()): print('  MISSING', k, '<-', ', '.join(sorted(v)))
for u in sorted(set(files) - used): print('  UNREF', u)
