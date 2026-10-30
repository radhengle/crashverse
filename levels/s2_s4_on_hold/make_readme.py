import json
R={r['id']:r for r in json.load(open('sim_results.json'))}
CI={l['id']:l for l in json.load(open('ci_win_shots.json'))['levels']}
rows=[]
for c in (2,3,4):
    d=json.load(open(f'data/chapter{c}.json'))
    rows.append(f"\n### {d['name']} (`{d['id']}`, levels {d['levels'][0]['number']}–{d['levels'][-1]['number']})\n")
    rows.append("| id | # | name | tag | shots (data→eff) | ★3/★2 | min (no pu / pu) | proven line | final % | rand 3★ | rand win | debris hits/capped/max | hint (target yaw/pitch) | CI g1.0/g1.2 | hero | mastery |")
    rows.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for L in d['levels']:
        r=R[L['id']]; h=L['heroSlot']; ci=CI[L['id']]; m=L['mastery']
        fp=", ".join(f"{f['powerup']}" for f in L.get('forced',[]))
        rows.append(f"| {L['id']} | {L['number']} | {L['name']} | {r['tag']}{' · forced '+fp if fp else ''} | {L['shots']}→{r['budget']} | {L['star3Pct']}/{L['star2Pct']} | {r['min']}/{r['min_pw']} | {r['seq']} | {round(r['final_pct']*100)}% | {r['rand3']:.1%} | {r['randwin']:.0%} | {r['debris_hits']}/{r['debris_capped']}/{r['debris_max']} | {L['hintTarget']} {L['hintYaw']}/{L['hintPitch']} | {'✔' if all(s['clear'] for s in ci['shots']) else '✘'}/{'✔' if all(s['clear_g12'] for s in ci['shots']) else '✘'} | {h['kind']} @ {h['pos']} | {m['type']} {m['value'] if m['value'] else ''}{(' role='+m['role']) if m['role'] else ''} |")
open('_table.md','w').write("\n".join(rows)+"\n")
