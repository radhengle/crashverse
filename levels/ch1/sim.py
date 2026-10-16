#!/usr/bin/env python3
"""CRASHVERSE Ch1 abstract solver/simulator.
Rules mirrored from Contract v0.2 §3-5, SG-01/05/08, and Developer code (DestructibleBlock/Projectile/DestructionTracker):
- Shot = 60 dmg (wrecker.json defaultDamage) to ONE hittable block; steel joint (joint:true) direct hit x2.
- HP wood 30 / brick 60 / steel 120; mass per piece wood 1 / brick 2.5 / steel 4 (rb.mass = mat.mass).
- Break (HP<=0) -> dependents whose supports are ALL gone collapse (sup list = OR), recursively.
- Destroyed = broken, or collapsed and bottom >= 1.0m above ground (tracker: dropped > max(1m, h/2)).
- Shockwave: +40 splash (no joint bonus) to pieces 1 link deep (supports+dependents) of the hit piece.
- Splitstrike: 3 sub-hits of 30 (x2 on joints) on up to 3 hittable blocks within 3.5m of each other.
- Hittable: not indestructible, not broken/collapsed, and not hidden behind an intact block that is in front
  (-x = Wrecker side; it fires along +x) and overlaps it in y and z. Falling-debris impact damage is IGNORED (conservative).
- Win: destroyed objective mass / total >= winThreshold after the shot. Boss (L10): SG-05 model.
Stars: ratio = shotsLeft/shots >= star3Pct / star2Pct (exact).
"""
import json, sys, itertools, random, os
HP={'wood':30,'brick':60,'steel':120}; MASS={'wood':1.0,'brick':2.5,'steel':4.0}
DMG=60; SHOCK=40; SPLIT=0.5; G=9.81*1.2;  # PR #15 gravity x1.2
MATF={'wood':1.0,'brick':1.1,'steel':0.8}; CAPLOG={'hits':0,'capped':0}; DEBRIS_MIN_V=2.0; DEBRIS_CAP=60
DEBRIS=True
HERE=os.path.dirname(os.path.abspath(__file__))

class State:
    def __init__(s,blocks):
        s.b={x['id']:x for x in blocks}; s.hp={i:HP[x['mat']] for i,x in s.b.items()}
        s.broken=set(); s.coll=set()
        s.dep={i:[] for i in s.b}
        for i,x in s.b.items():
            for p in x.get('sup') or []: s.dep[p].append(i)
    def key(s): return (frozenset(s.broken),frozenset(s.coll),tuple(sorted((k,v) for k,v in s.hp.items() if v<HP[s.b[k]['mat']])))
    def copy(s):
        n=State.__new__(State); n.b=s.b; n.dep=s.dep; n.hp=dict(s.hp); n.broken=set(s.broken); n.coll=set(s.coll); return n
    def gone(s,i): return i in s.broken or i in s.coll
    def damage(s,i,d,direct):
        x=s.b[i]
        if s.gone(i) or x.get('indestructible'): return
        if direct and x.get('joint'): d*=2  # debris/splash never multiplied
        s.hp[i]-=d
        if s.hp[i]<=0: s.broken.add(i); s.prop(i)
    def prop(s,i):
        for d in s.dep[i]:
            if s.gone(d): continue
            sup=s.b[d].get('sup') or []
            if sup and all(s.gone(p) for p in sup):
                if s.b[d].get('indestructible') and s.b[d].get('nonObj'): continue
                s.coll.add(d); s.debris(d); s.prop(d)
    def debris(s,d):
        """Level_Rulings_v1 #7 + DestructibleBlock.OnCollisionEnter: a collapsing (dynamic) piece that lands on the
        highest intact (kinematic) piece under its footprint deals min(60, mass*v) splash to it if v>2 m/s,
        v = sqrt(2*g*drop). The falling piece itself takes nothing (other body kinematic/static); stack-mates fall
        together (relative v ~ 0)."""
        if not DEBRIS: return
        x=s.b[d]; bot=x['p'][1]-x['s'][1]/2; best=None; top=0.0
        for j,y in s.b.items():
            if j==d or s.gone(j): continue
            yt=y['p'][1]+y['s'][1]/2
            if yt>bot+1e-6: continue
            if all(abs(y['p'][k]-x['p'][k])*2 < y['s'][k]+x['s'][k] for k in (0,2)) and yt>=top: best,top=j,yt
        v=(2*G*max(0.0,bot-top))**.5
        if best and v>DEBRIS_MIN_V:
            raw=0.5*MASS[x['mat']]*(v-DEBRIS_MIN_V)   # ruling 7a
            CAPLOG['hits']+=1
            if raw>=DEBRIS_CAP: CAPLOG['capped']+=1
            s.damage(best,min(DEBRIS_CAP,raw)*MATF[s.b[best]['mat']],False)  # factor = struck piece's material (assumed)
    def destroyed(s,i):
        x=s.b[i]
        if i in s.broken: return True
        if i in s.coll: return x['p'][1]-x['s'][1]/2 >= 1.0
        return False
    def ratio(s):
        obj=[i for i,x in s.b.items() if not x.get('nonObj')]
        tot=sum(MASS[s.b[i]['mat']] for i in obj)
        return sum(MASS[s.b[i]['mat']] for i in obj if s.destroyed(i))/tot
    def hittable(s):
        out=[]
        for i,x in s.b.items():
            if s.gone(i) or x.get('indestructible') and x.get('role')!='core': continue
            hid=False
            for j,y in s.b.items():
                if j==i or s.gone(j) or y.get('role')=='shutter': continue
                # Wrecker fires from -x: a block is hidden when an intact block lies fully on its -x side and covers it in y and z
                if y['p'][0]+y['s'][0]/2 <= x['p'][0]-x['s'][0]/2+1e-6 and all(abs(y['p'][k]-x['p'][k])*2 < (y['s'][k]-x['s'][k])+1e-6 for k in (1,2)):
                    hid=True;break   # fully covered in x/y by a block strictly in front
            if not hid: out.append(i)
        return out

def actions(st,pw):
    hs=st.hittable(); acts=[(None,h) for h in hs]
    if 'shockwave' in pw: acts+=[('shockwave',h) for h in hs]
    if 'splitstrike' in pw:
        for c in itertools.combinations(hs,3):
            P=[st.b[i]['p'] for i in c]
            if all(sum((a-b)**2 for a,b in zip(P[u],P[v]))**.5<=3.5 for u in range(3) for v in range(u+1,3)): acts.append(('splitstrike',c))
    return acts

def apply(st,act):
    n=st.copy(); pu,t=act
    if pu=='splitstrike':
        for i in t: n.damage(i,DMG*SPLIT,True)
    else:
        nb=[j for j in (n.b[t].get('sup') or [])]+n.dep[t]
        n.damage(t,DMG,True)
        if pu=='shockwave':
            for j in nb: n.damage(j,SHOCK,False)
    return n

def min_shots(blocks,win,budget,powerups=(),boss=False):
    """BFS: fewest shots to win. powerups: each usable once. Returns (n, sequence)."""
    start=(State(blocks),frozenset(powerups),())
    frontier=[start]; seen=set()
    for depth in range(1,budget+1):
        nxt=[]
        for st,pw,seq in frontier:
            for a in actions(st,pw):
                n=apply(st,a); npw=pw-{a[0]} if a[0] else pw
                if n.ratio()>=win: return depth,seq+(a,)
                k=(n.key(),npw)
                if k in seen: continue
                seen.add(k); nxt.append((n,npw,seq+(a,)))
        frontier=nxt
        if len(frontier)>60000: frontier=frontier[:60000]
    return None,None

def boss_min(blocks,budget,toggle_on_phase_shot):
    """SG-05: phase1 break both steel shoulders (joint 2x). Phase2 shutter OPEN, toggles after every shot.
    Core needs 2 direct hits while open. toggle_on_phase_shot=True mirrors GameSession (shutter closes right
    after the shot that opened phase 2)."""
    sh=[x for x in blocks if x.get('role')=='shoulder']
    p1=sum(-(-HP['steel']//(DMG*2)) for _ in sh)
    # phase2: if opening shot also toggles, first P2 shot sees closed shutter
    seq_open = [False,True] if toggle_on_phase_shot else [True,False]
    hits=0;shots=p1;i=0
    while hits<2 and shots<budget+5:
        is_open=seq_open[i%2]; shots+=1; i+=1
        if is_open: hits+=1
    return shots

def stars(budget,used,s3,s2):
    r=(budget-used)/budget
    return 3 if r>=s3 else 2 if r>=s2 else 1

def random_play(blocks,win,budget,trials=4000,seed=1):
    """Naive player: random hittable target each shot. Returns (win rate, 3-star-capable rate by shots used)."""
    rnd=random.Random(seed); res=[]
    for _ in range(trials):
        st=State(blocks); used=None
        for k in range(1,budget+1):
            h=st.hittable()
            if not h: break
            st=apply(st,(None,rnd.choice(h)))
            if st.ratio()>=win: used=k;break
        res.append(used)
    return res

def lay_ids(seq):
    out=[]
    for a,t in seq or []: out+= [t] if isinstance(t,str) else list(t)
    return out

def main():
    ch=json.load(open(os.path.join(HERE,'chapter1_levels.json')))
    rows=[]
    for L in ch['levels']:
        lay=json.load(open(os.path.join(HERE,'layouts',L['layout']+'.json')))['blocks']
        B,s3,s2=L['shots'],L['star3Pct'],L['star2Pct']
        need3=max(u for u in range(1,B+1) if stars(B,u,s3,s2)==3)
        if L.get('bossId'):
            a=boss_min(lay,B,False); b=boss_min(lay,B,True)  # a = spec (Level_Rulings #3), b = current code bug
            rows.append(dict(id=L['id'],shots=B,min=a,min_code=b,need3=need3,stars=stars(B,a,s3,s2) if a<=B else 0,
                 stars_code=stars(B,b,s3,s2) if b<=B else 0,margin=B-a,rand3=None,seq='shoulderL, shoulderR, core(open), any(closed), core(open)'))
            continue
        pw=[f['powerup'] for f in L.get('forced',[])]
        n,seq=min_shots(lay,L['winThreshold'],B)
        npw,seqp=min_shots(lay,L['winThreshold'],B,pw) if pw else (n,seq)
        CAPLOG['hits']=CAPLOG['capped']=0
        rp=random_play(lay,L['winThreshold'],B)
        cap=(CAPLOG['hits'],CAPLOG['capped'])
        r3=sum(1 for u in rp if u is not None and u<=need3)/len(rp); rw=sum(1 for u in rp if u)/len(rp)
        fmt=lambda q:', '.join((f'{a}:' if a else '')+(t if isinstance(t,str) else '+'.join(t)) for a,t in q) if q else '-'
        fin=State(lay)
        for a in (seqp if npw<=n else seq): fin=apply(fin,a)
        crown=[i for i,x in fin.b.items() if x.get('role')=='crown']
        intact=(sum(1 for i in crown if i not in fin.broken)/len(crown)) if crown else None
        stone_on_weak=[i for i in lay_ids(seqp if npw<=n else seq) if fin.b[i].get('indestructible')]
        rows.append(dict(id=L['id'],debris_hits=cap[0],debris_capped=cap[1],cap_rate=round(cap[1]/cap[0],3) if cap[0] else 0.0,shots=B,crown_intact=intact,stone_on_weak=stone_on_weak,final_pct=round(fin.ratio(),3),min=n,min_pw=npw,need3=need3,stars=stars(B,min(n,npw),s3,s2),
            margin=B-min(n,npw),rand3=round(r3,3),randwin=round(rw,3),seq=fmt(seqp if npw<=n else seq),one_shot=(n==1)))
    TAG={'c1_l01':('Easy',.15,.50),'c1_l02':('Easy',.08,.50),'c1_l03':('Medium',.05,.25),'c1_l04':('Hard',.005,.06),
         'c1_l05':('Breather',.20,.60),'c1_l06':('Medium',.02,.25),'c1_l07':('Hard',.002,.06),'c1_l08':('Breather',0,.60),
         'c1_l09':('Hard',.005,.06),'c1_l10':('Boss',None,None)}
    for r in rows:
        tag,lo,hi=TAG[r['id']]; r['tag']=tag
        ok=r['min'] is not None and (r.get('margin',0)>=0) and r['stars']==3
        if lo is not None: ok = ok and lo<=r['rand3']<=hi
        r['pass']=ok
    json.dump(rows,open(os.path.join(HERE,'sim_results.json'),'w'),indent=1)
    for r in rows: print(r)
if __name__=='__main__': main()
