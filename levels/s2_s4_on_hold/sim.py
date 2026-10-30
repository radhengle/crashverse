#!/usr/bin/env python3
"""CRASHVERSE abstract solver/simulator for Seasons 2-4 (extends levels/ch1/sim.py from integration/v2).
Rules (all Game designer rulings to date, incl. the S2-S4 rulings of 2026-10-24):
- Shot = 60 dmg (wrecker.json) to ONE hittable block; direct hit on a steel joint x2 (only steel has jointDamageMultiplier 2).
- Materials (MaterialDefinition v2): HP wood 30, brick 60, steel 120, glass 12, stone 90; mass 1.0/2.5/4.0/1.5/3.3;
  debris damage TAKEN x1.0/1.1/0.8/1.5/0.9 (rulings 7a, 9). GD S2-S4 #1: ice = ice-tinted stone (same stats, visual only);
  #8: amber = glass (same stats, visual only).
- Ruling 8: a piece collapses only when ALL its supports are gone (sup list = OR of holders).
- Destroyed = broken, or collapsed with base >= 1.0 m.
- Debris (ruling 7a): collapsing piece -> highest intact piece under its footprint: min(60, 0.5*m*(v-2))*taken-mult when v>2,
  v = sqrt(2*g*drop), g = 9.81*1.2 (PR #15). Cap hits logged.
- Explosive blocks: on break, 80 splash (no joint bonus) within 2.5 m centre-to-centre; chains.
- Hittable: the Wrecker fires from -x; a block is hidden when an intact NON-moving block lies fully on its -x side and covers it
  in y and z. Moving blocks (spinDps>0) are handled by TIMING: the aim line (x-ray through the target centre, padded by the
  projectile radius 0.225) is tested against the mover posed at every 1 deg of its rotation. The longest open arc / spinDps
  = the open window in seconds. A window shorter than TAU (0.30 s, assumption: shortest gap a player can reliably time with a
  ~1 s flight) is not hittable by a normal shot; random players hit it with probability = open fraction.
- Power-ups (design/data/powerups_v2.json + GD S2-S4 rulings):
  * mega_bomb (#5): normal projectile damage to the hit piece, PLUS a radial blast at the impact point (centre of the hit
    piece's -x face): 70*(1-d/3.5) to every intact piece (hit piece included) by centre distance d < 3.5 m; explosive pieces with
    d <= 3.5 detonate (PowerUpRulesV2.MegaBombDetonates).
  * laser_beam (#6): straight beam (no gravity) from the barrel (-28.05, 1.5, 0 in layout coords) through the target centre,
    sphere-cast radius 0.15 (assumption); the first 2 intact pieces it meets (any piece, indestructible included) pass for 0
    damage, the 3rd takes full damage (joint x2). Valid only when the aimed piece IS the 3rd.
  * guided_rocket (#6): collides normally; steering (90 deg/s for 60 ticks) can route around cover: a piece hidden from the
    front is a rocket target when its +z or -z side is clear (no intact piece crosses the side ray through its centre). Every
    rocket shot on a proven line is then re-checked by an actual steered flight (arcs.py) that must hit it first.
  * time_freeze: moving parts at 0.25x until the shot's impact (one shot): a timed target whose window/0.25 >= TAU is hittable.
  * shield: the shielded shot is refunded (costs 0) when it destroys < 10% of objective mass (1 charge).
  * earthquake / multi_shot as Ch1.
- Hard levels (HardLevelRules): every 10th level number plays with shots-1 (min 3); stars use that budget.
Stars: shotsLeft/effectiveBudget >= star3Pct / star2Pct (refunded shots count as left)."""
import json, sys, itertools, random, os
from collections import deque
import arcs
HP={'wood':30,'brick':60,'steel':120,'glass':12,'stone':90}
MASS={'wood':1.0,'brick':2.5,'steel':4.0,'glass':1.5,'stone':3.3}
MATF={'wood':1.0,'brick':1.1,'steel':0.8,'glass':1.5,'stone':0.9}
DMG=60; SHOCK=40; SPLIT=0.5; BOMB=70; BOMB_R=3.5; EXPL=80; EXPL_R=2.5
G=9.81*1.2; DEBRIS_MIN_V=2.0; DEBRIS_CAP=60
TAU=0.30; SLOW=0.25; LASER_R=0.15; LASER_O=(arcs.WX+arcs.SA-arcs.BASE[0],arcs.PY,0.0); SHIELD_T=0.10
CAPLOG={'hits':0,'capped':0,'max':0.0}
HERE=os.path.dirname(os.path.abspath(__file__))
def dist(a,b): return sum((x-y)**2 for x,y in zip(a,b))**.5
_TCACHE={}

class State:
    def __init__(s,blocks):
        s.b={x['id']:x for x in blocks}; s.hp={i:HP[x['mat']] for i,x in s.b.items()}
        s.broken=set(); s.coll=set(); s.dep={i:[] for i in s.b}
        s.movers=[i for i,x in s.b.items() if arcs.moving(x)]
        for i,x in s.b.items():
            for p in x.get('sup') or []: s.dep[p].append(i)
    def key(s): return (frozenset(s.broken),frozenset(s.coll),tuple(sorted((k,v) for k,v in s.hp.items() if v<HP[s.b[k]['mat']])))
    def copy(s):
        n=State.__new__(State); n.b=s.b; n.dep=s.dep; n.movers=s.movers; n.hp=dict(s.hp); n.broken=set(s.broken); n.coll=set(s.coll); return n
    def gone(s,i): return i in s.broken or i in s.coll
    def damage(s,i,d,direct):
        x=s.b[i]
        if s.gone(i) or x.get('indestructible'): return
        if direct and x.get('joint') and x['mat']=='steel': d*=2
        s.hp[i]-=d
        if s.hp[i]<=0:
            s.broken.add(i)
            if x.get('explosive'):
                for j,y in s.b.items():
                    if j!=i and not s.gone(j) and dist(x['p'],y['p'])<=EXPL_R+1e-3: s.damage(j,EXPL,False)
            s.prop(i)
    def prop(s,i):
        for d in s.dep[i]:
            if s.gone(d): continue
            sup=s.b[d].get('sup') or []
            if sup and all(s.gone(p) for p in sup):
                if s.b[d].get('indestructible'): continue
                s.coll.add(d); s.debris(d); s.prop(d)
    def debris(s,d):
        x=s.b[d]; bot=x['p'][1]-x['s'][1]/2; best=None; top=0.0
        for j,y in s.b.items():
            if j==d or s.gone(j) or j in s.movers: continue
            yt=y['p'][1]+y['s'][1]/2
            if yt>bot+1e-6: continue
            if all(abs(y['p'][k]-x['p'][k])*2 < y['s'][k]+x['s'][k] for k in (0,2)) and yt>=top: best,top=j,yt
        v=(2*G*max(0.0,bot-top))**.5
        if best and v>DEBRIS_MIN_V:
            raw=0.5*MASS[x['mat']]*(v-DEBRIS_MIN_V); CAPLOG['hits']+=1; CAPLOG['max']=max(CAPLOG['max'],raw)
            if raw>=DEBRIS_CAP: CAPLOG['capped']+=1
            s.damage(best,min(DEBRIS_CAP,raw)*MATF[s.b[best]['mat']],False)
    def destroyed(s,i):
        x=s.b[i]
        if i in s.broken: return True
        if i in s.coll: return x['p'][1]-x['s'][1]/2 >= 1.0
        return False
    def ratio(s):
        obj=[i for i,x in s.b.items() if not x.get('nonObj')]
        tot=sum(MASS[s.b[i]['mat']] for i in obj)
        return sum(MASS[s.b[i]['mat']] for i in obj if s.destroyed(i))/tot
    def front(s,i):
        """intact non-moving blocks fully on the -x side of i that cover it in y and z (occluders)"""
        x=s.b[i]; out=[]
        for j,y in s.b.items():
            if j==i or s.gone(j) or j in s.movers: continue
            if y['p'][0]+y['s'][0]/2 <= x['p'][0]-x['s'][0]/2+1e-6 and all(abs(y['p'][k]-x['p'][k])*2 < (y['s'][k]-x['s'][k])+1e-6 for k in (1,2)):
                out.append(j)
        return out
    def timing(s,i):
        """(window_s, open_fraction) of the moving blocks in front of i along its aim line, or None if no mover ever blocks it"""
        live=tuple(m for m in s.movers if m!=i and not s.gone(m))
        if not live: return None
        k=(i,live)
        if k in _TCACHE: return _TCACHE[k]
        x=s.b[i]; o=(-60.0,x['p'][1],x['p'][2]); fx=x['p'][0]-x['s'][0]/2; groups={}
        for m in live:
            b=s.b[m]; groups.setdefault((tuple(b['pivot']),tuple(b['spinAxis']),b['spinDps']),[]).append(b)
        win=None; frac=1.0
        for (pv,ax,dps),bl in groups.items():
            cov=[]
            for th in range(360):
                c=False
                for b in bl:
                    e=arcs.seg_box(o,(1,0,0),arcs.pose(b,th,world=False),arcs.RP)
                    if e is not None and o[0]+e<=fx: c=True; break
                cov.append(c)
            if not any(cov): continue
            if all(cov): w=0.0; f=0.0
            else:
                best=run=0
                for c in cov+cov:
                    run=0 if c else run+1; best=max(best,run)
                w=min(best,360)/dps; f=cov.count(False)/360
            win=w if win is None else min(win,w); frac*=f
        r=None if win is None else (win,frac); _TCACHE[k]=r; return r
    def candidates(s): return [i for i,x in s.b.items() if not s.gone(i) and not x.get('indestructible') and i not in s.movers]
    def aimable(s): return [i for i in s.candidates() if not s.front(i)]
    def hittable(s):
        out=[]
        for i in s.aimable():
            t=s.timing(i)
            if t is None or t[0]>=TAU: out.append(i)
        return out
    def laser_hits(s,i):
        x=s.b[i]; d=[x['p'][k]-LASER_O[k] for k in range(3)]; n=sum(a*a for a in d)**.5; d=[a/n for a in d]; hits=[]
        for j,y in s.b.items():
            if s.gone(j) or j in s.movers: continue
            e=arcs.seg_box(LASER_O,d,arcs.pose(y,0,world=False),LASER_R)
            if e is not None: hits.append((e,j))
        return [j for e,j in sorted(hits)]
    def side_clear(s,i):
        x=s.b[i]
        for sg in (1,-1):
            o=(x['p'][0],x['p'][1],x['p'][2]); ok=True
            for j,y in s.b.items():
                if j==i or s.gone(j) or j in s.movers: continue
                if (y['p'][2]-x['p'][2])*sg<=0: continue
                if abs(y['p'][0]-x['p'][0])*2<y['s'][0] and abs(y['p'][1]-x['p'][1])*2<y['s'][1]: ok=False; break
            if ok: return True
        return False

def actions(st,pw):
    hs=st.hittable(); acts=[(None,h) for h in hs]
    if 'earthquake' in pw: acts+=[('earthquake',h) for h in hs]
    if 'mega_bomb' in pw: acts+=[('mega_bomb',h) for h in hs]
    if 'shield' in pw: acts+=[('shield',h) for h in hs]
    if 'time_freeze' in pw:
        for i in st.aimable():
            t=st.timing(i)
            if t is not None and t[0]<TAU and t[0]/SLOW>=TAU: acts.append(('time_freeze',i))
    if 'guided_rocket' in pw: acts+=[('guided_rocket',h) for h in st.candidates() if h not in hs and st.side_clear(h)]
    if 'laser_beam' in pw:
        for h in st.candidates():
            lh=st.laser_hits(h)
            if len(lh)>=3 and lh[2]==h: acts.append(('laser_beam',h))
    if 'multi_shot' in pw:
        for c in itertools.combinations(hs,3):
            P=[st.b[i]['p'] for i in c]
            if all(dist(P[u],P[v])<=3.5 for u in range(3) for v in range(u+1,3)): acts.append(('multi_shot',c))
    return acts

def apply(st,act):
    """returns (new state, shot cost)"""
    n=st.copy(); pu,t=act
    if pu=='multi_shot':
        for i in t: n.damage(i,DMG*SPLIT,True)
        return n,1
    x=n.b[t]; nb=list(x.get('sup') or [])+n.dep[t]; r0=st.ratio()
    n.damage(t,DMG,True)
    if pu=='earthquake':
        for j in nb: n.damage(j,SHOCK,False)
    if pu=='mega_bomb':
        imp=(x['p'][0]-x['s'][0]/2,x['p'][1],x['p'][2])
        for j,y in n.b.items():
            if n.gone(j) or y.get('indestructible'): continue
            d=dist(imp,y['p'])
            if y.get('explosive') and d<=BOMB_R+1e-3: n.damage(j,n.hp[j],False)
            elif d<BOMB_R: n.damage(j,BOMB*(1-d/BOMB_R),False)
    if pu=='shield' and n.ratio()-r0<SHIELD_T: return n,0
    return n,1

def budget(L):
    n=L['shots']
    return max(min(3,n),n-1) if L['number']%10==0 else n

def min_shots(blocks,win,maxc,powerups=(),cap=400000):
    """0-1 BFS over shot cost (shield refunds cost 0). Returns (cost, seq, state) or (None,None,None); sets LAST_TRUNC."""
    global LAST_TRUNC; LAST_TRUNC=False
    s0=State(blocks); k0=(s0.key(),frozenset(powerups)); distc={k0:0}; dq=deque([(0,s0,frozenset(powerups),())])
    while dq:
        c,st,pw,seq=dq.popleft()
        if distc.get((st.key(),pw),99)<c: continue
        if seq and st.ratio()>=win: return c,seq,st
        for a in actions(st,pw):
            n,cost=apply(st,a); nc=c+cost
            if nc>maxc: continue
            npw=pw-{a[0]} if a[0] else pw; k=(n.key(),npw)
            if k in distc and distc[k]<=nc: continue
            if len(distc)>=cap: LAST_TRUNC=True; continue
            distc[k]=nc
            (dq.appendleft if cost==0 else dq.append)((nc,n,npw,seq+(a,)))
    return None,None,None

def stars(B,used,s3,s2):
    r=(B-used)/B
    return 3 if r>=s3 else 2 if r>=s2 else 1

def random_play(blocks,win,B,trials=4000,seed=1):
    """no power-ups; picks any front-visible piece; timed pieces are hit with probability = open fraction (else the shot is lost)"""
    rnd=random.Random(seed); res=[]
    for _ in range(trials):
        st=State(blocks); used=None
        for k in range(1,B+1):
            h=st.aimable()
            if not h: break
            t=rnd.choice(h); tm=st.timing(t)
            if tm is not None and rnd.random()>=tm[1]: continue
            st,_=apply(st,(None,t))
            if st.ratio()>=win: used=k;break
        res.append(used)
    return res

BANDS={'Easy':(.08,.50),'Medium':(.02,.25),'Hard':(.002,.06),'Breather':(.15,.60),'Hard (skull)':(0.0,.06)}
def fmt(q): return ', '.join((f'{a}:' if a else '')+(t if isinstance(t,str) else '+'.join(t)) for a,t in q) if q else '-'

def rocket_program(lay,gone,t,g=arcs.G12):
    """search a launch (yaw offset, elevation tweak) + constant steer rate (|s|<=90 deg/s, 60 ticks) whose flight hits t first"""
    b=next(x for x in lay if x['id']==t); w=tuple(arcs.BASE[i]+b['p'][i] for i in range(3)); a=arcs.solve(w,g)
    if a is None: return None
    y0,e0=a
    for dy in [d*s for d in range(4,42,2) for s in (1,-1)]:
        for sr in range(10,100,10):
            for de in (0,.5,1,1.5,2,2.5,3,-.5,-1):
                h,_,ft=arcs.march(lay,gone,y0+dy,e0+de,g,steer=-sr if dy>0 else sr,dt=0.01)
                if h==t: return dict(yaw=round(y0+dy,2),elevation=round(e0+de,2),steerDegPerSec=(-sr if dy>0 else sr),steerTicks=60,flight_s=round(ft,2))
    return None

def run_chapter(chfile,laydir):
    ch=json.load(open(chfile)); rows=[]
    for L in ch['levels']:
        lay=json.load(open(os.path.join(laydir,L['layout']+'.json')))['blocks']
        B=budget(L); s3,s2=L['star3Pct'],L['star2Pct']; tag=TAGS.get(L['id'],'?')
        need3=max([u for u in range(0,B+1) if stars(B,u,s3,s2)==3] or [0])
        pw=[f['powerup'] for f in L.get('forced',[])]
        CAPLOG.update(hits=0,capped=0,max=0.0)
        n,seq,fin=min_shots(lay,L['winThreshold'],B)
        npw,seqp,finp=min_shots(lay,L['winThreshold'],B,pw) if pw else (n,seq,fin)
        best,bseq,bfin=(npw,seqp,finp) if (npw if npw is not None else 99)<=(n if n is not None else 99) else (n,seq,fin)
        nopu_ext=trunc=None
        if pw:   # teaching level: how many shots does the best NO-power-up solve need? (searched to budget+3)
            nopu_ext,_,_=min_shots(lay,L['winThreshold'],B+3); trunc=LAST_TRUNC
        rp=random_play(lay,L['winThreshold'],B)
        r3=sum(1 for u in rp if u is not None and u<=need3)/len(rp); rw=sum(1 for u in rp if u)/len(rp)
        stone_bad=[t for a,t in (bseq or []) for t in ([t] if isinstance(t,str) else t) if bid(lay,t).get('indestructible')]
        lo,hi=BANDS[tag]
        if pw and (n is None or n>need3): lo=0.0   # 3-star needs the forced power-up: random play (no power-ups) can't 3-star
        st=stars(B,best,s3,s2) if best is not None else 0
        # need-the-power-up test: no-power-up min over the budget (Shield: over the 3-star shot count, see README)
        if pw: needs_pu=(nopu_ext is None or nopu_ext>B) if pw!=['shield'] else (nopu_ext is None or nopu_ext>need3)
        else: needs_pu=None
        used_pu=[a for a,t in (bseq or []) if a]
        rockets=[]
        if bseq:
            s_=State(lay)
            for a,t in bseq:
                if a=='guided_rocket': rockets.append((t,rocket_program(lay,s_.broken|s_.coll,t)))
                s_,_=apply(s_,(a,t))
        ok=(best is not None and st==3 and lo<=r3<=hi and not stone_bad and (best>1 or tag in ('Easy','Breather') or pw)
            and (needs_pu is not False) and (not pw or set(pw)<=set(used_pu)) and all(p for _,p in rockets))
        rows.append(dict(id=L['id'],number=L['number'],name=L['name'],tag=tag,shots=L['shots'],budget=B,star3Pct=s3,star2Pct=s2,
            min=n,min_pw=npw,nopu_min_ext=nopu_ext,nopu_search_to=B+3 if pw else None,nopu_truncated=trunc,needs_pu=needs_pu,
            forced=pw,need3=need3,stars=st,margin=B-(best if best is not None else B),final_pct=round(bfin.ratio(),3) if bfin else None,
            seq=fmt(bseq),rand3=round(r3,3),randwin=round(rw,3),debris_hits=CAPLOG['hits'],debris_capped=CAPLOG['capped'],
            debris_max=round(CAPLOG['max'],1),rocket_programs={t:p for t,p in rockets},hero=L.get('heroSlot'),pass_=ok))
    return rows
def bid(lay,t): return next(b for b in lay if b['id']==t)
TAGS={k:v['tag'] for k,v in json.load(open(os.path.join(HERE,'level_tags.json'))).items()}
def main():
    out=[]
    for c in ('chapter2','chapter3','chapter4'):
        rows=run_chapter(os.path.join(HERE,'data',c+'.json'),os.path.join(HERE,'data','Layouts'))
        for r in rows:
            npu='' if not r['forced'] else f" NO-PU min={r['nopu_min_ext'] if r['nopu_min_ext'] is not None else '>'+str(r['nopu_search_to'])}{' (trunc)' if r['nopu_truncated'] else ''} needsPU={r['needs_pu']}"
            print(r['id'],r['tag'],'PASS' if r['pass_'] else 'FAIL',f"B{r['budget']} min{r['min']}/{r['min_pw']} need3<={r['need3']} r3={r['rand3']} rw={r['randwin']} pct={r['final_pct']} cap={r['debris_capped']}/{r['debris_hits']} max={r['debris_max']}{npu} :: {r['seq']}")
        out+=rows
    json.dump(out,open(os.path.join(HERE,'sim_results.json'),'w'),indent=1)
    print('ALL PASS' if all(r['pass_'] for r in out) else 'SOME FAIL')
if __name__=='__main__': main()
