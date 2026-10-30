#!/usr/bin/env python3
"""Daily Boss fairness check (meta_v2.json dailyBoss + DailyBossRules.ForDate), using levels/ch1/sim.py rules.
For every arena layout: fewest shots to reach winThreshold, the best line, what the obvious gold-joint shot (pelvis) alone
gives, and random-player win / 3-star rates at every shot count the config can roll. It also replays
DailyBossRules.ForDate (FNV-1a seed) for the next 60 days so every rolled (layout, shots) pair is checked.
Stars: DailyBossRules leaves star bands at the LevelEntry defaults (0.60 / 0.30).
The explosive crate (non-objective) is not modelled; it is treated as a non-target."""
import json, os, sys, datetime
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,os.path.join(HERE,'../ch1'))
import sim
D=sim.DATA
def seed(key):
    h=2166136261
    for ch in key: h^=ord(ch); h=(h*16777619)&0xFFFFFFFF
    return h
def for_date(key,d):
    s=seed('dailyboss:'+key); li=s%len(d['layouts']); ni=(s//7)%len(d['names']); shots=d['baseShots']+(s//131)%max(1,d['shotSpread']+1)
    return d['layouts'][li],d['names'][ni],shots
def main():
    d=json.load(open(os.path.join(D,'meta_v2.json')))['dailyBoss']; win=d.get('winThreshold',.85)
    S3,S2=0.6,0.3; shots_range=range(d['baseShots'],d['baseShots']+d['shotSpread']+1); arenas=[]
    for name in d['layouts']:
        bl=json.load(open(os.path.join(D,'Layouts',name+'.json')))['blocks']
        bl=[dict(b,indestructible=True) if b.get('explosive') else b for b in bl]
        n,seq=sim.min_shots(bl,win,max(shots_range)); p=sim.apply(sim.State(bl),(None,'pelvis')).ratio()
        rates={}
        for S in shots_range:
            rp=sim.random_play(bl,win,S,trials=4000); need3=max(u for u in range(1,S+1) if sim.stars(S,u,S3,S2)==3)
            rates[S]=dict(randwin=round(sum(1 for u in rp if u)/len(rp),3),rand3=round(sum(1 for u in rp if u and u<=need3)/len(rp),3),
                          need3=need3,three_star_possible=n<=need3,margin=S-n)
        arenas.append(dict(layout=name,min=n,seq=', '.join(t for _,t in seq),pelvis_only=round(p,3),by_shots=rates))
    today=datetime.date(2026,10,30); days=[]
    for i in range(60):
        k=(today+datetime.timedelta(days=i)).strftime('%Y%m%d'); lay,nm,S=for_date(k,d); a=next(x for x in arenas if x['layout']==lay)
        days.append(dict(date=k,layout=lay,name=nm,shots=S,three_star_possible=a['by_shots'][S]['three_star_possible']))
    out=dict(config=d,arenas=arenas,next60=dict(all_three_star_possible=all(x['three_star_possible'] for x in days),
             shots_hist={str(S):sum(1 for x in days if x['shots']==S) for S in shots_range},
             layout_hist={L:sum(1 for x in days if x['layout']==L) for L in d['layouts']},days=days))
    json.dump(out,open(os.path.join(HERE,'daily_boss_results.json'),'w'),indent=1)
    for a in arenas: print(a['layout'],'min',a['min'],a['seq'],'pelvis-only',a['pelvis_only'],a['by_shots'])
    print('next 60 days: 3* possible every day =',out['next60']['all_three_star_possible'],out['next60']['shots_hist'],out['next60']['layout_hist'])
if __name__=='__main__': main()
