#!/usr/bin/env python3
"""Set each level's hintTarget = first piece of its sim-proven optimal line (sim_results.json). Run after sim.py, before derive_hints."""
import json
R={r['id']:r for r in json.load(open('sim_results.json'))}
for c in (2,3,4):
    p=f'data/chapter{c}.json'; d=json.load(open(p))
    for L in d['levels']:
        t=R[L['id']]['seq'].split(', ')[0].split(':')[-1]
        L['hintTarget']=t
    json.dump(d,open(p,'w'),indent=1)
    print(p,[ (L['id'],L['hintTarget']) for L in d['levels']])
