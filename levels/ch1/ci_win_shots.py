#!/usr/bin/env python3
"""BUG-24: known-winning aim for every Ch1 level's best line, in each form CI can consume.
Geometry is the same as Developer's Tools/ContentGen/derive_hints.py (PR #12): Wrecker at x=-20, pivot y=1.5,
spawn 1.95 m along the barrel, +yaw -> +z, structureBase (10,0,0), launchSpeed 28 (wrecker.json), low arc.
On top of that it ray-marches the arc (dt 0.01 s) against every intact box in the layout and reports the FIRST
piece the shot actually hits (sim.py removes broken/collapsed pieces between shots).
Outputs ci_win_shots.json: world target, yaw/elevation (g x1.0 and x1.2), drag from rest in dp and in px at
1080x2340 / 420 dpi, and whether the arc's first hit is the intended piece."""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim
HERE=os.path.dirname(os.path.abspath(__file__))
WX,PY,SA=-20.0,1.5,1.95; BASE=(10.0,0.0,0.0); V=28.0; REST_EL=25.0; YAW_DP=.25; PITCH_DP=.15; DENSITY=420; CX,CY=540,1300
def muzzle(yaw,el):
    y,e=math.radians(yaw),math.radians(el); d=(math.cos(e)*math.cos(y),math.sin(e),math.cos(e)*math.sin(y))
    return tuple(p+SA*c for p,c in zip((WX,PY,0.0),d)),d
def solve(t,g):
    yaw,el=0.0,20.0
    for _ in range(30):
        m,_=muzzle(yaw,el); dx,dy,dz=(a-b for a,b in zip(t,m)); yaw=math.degrees(math.atan2(dz,dx)); r=math.hypot(dx,dz)
        disc=V**4-g*(g*r*r+2*dy*V*V)
        if disc<0: return None
        el=math.degrees(math.atan((V*V-math.sqrt(disc))/(g*r)))
    return yaw,el
SPLIT_DEG=8.0  # wrecker.json splitSpreadDeg; Projectile.Split: centre keeps going, children at +/-8 deg about world up
def _rot_y(v,deg):
    a=math.radians(deg); return [v[0]*math.cos(a)+v[2]*math.sin(a),v[1],-v[0]*math.sin(a)+v[2]*math.cos(a)]
def first_hit(blocks,gone,yaw,el,g,split=False):
    """First piece hit by the shot. split=True (Multi Shot): splits at min(apex, 70% flight) (SplitPlanner) and returns
    the sorted ids hit by the 3 projectiles."""
    p,d=muzzle(yaw,el); v=[V*c for c in d]
    if split:
        t_ap=v[1]/g if v[1]>0 else 1e9; t_fl=_flight(blocks,gone,p,v,g); ts=min(t_ap,0.7*t_fl) if t_fl else t_ap
        p2=[p[0]+v[0]*ts,p[1]+v[1]*ts-0.5*g*ts*ts,p[2]+v[2]*ts]; v2=[v[0],v[1]-g*ts,v[2]]
        return sorted(str(_march(blocks,gone,p2,_rot_y(v2,a),g)) for a in (0.0,SPLIT_DEG,-SPLIT_DEG))
    return _march(blocks,gone,p,v,g)
def _flight(blocks,gone,p,v,g):
    p=list(p); v=list(v); dt=.01
    for k in range(600):
        p=[p[0]+v[0]*dt,p[1]+v[1]*dt-0.5*g*dt*dt,p[2]+v[2]*dt]; v[1]-=g*dt
        if _hit(blocks,gone,p) or p[1]<-6: return (k+1)*dt
    return None
def _hit(blocks,gone,p):
    for b in blocks:
        if b['id'] in gone: continue
        c=[BASE[i]+b['p'][i] for i in range(3)]
        if all(abs(p[i]-c[i])<=b['s'][i]/2+0.225 for i in range(3)): return b['id']
    return None
def _march(blocks,gone,p,v,g):
    p=list(p); v=list(v); dt=.01
    for _ in range(600):
        p=[p[0]+v[0]*dt,p[1]+v[1]*dt-0.5*g*dt*dt,p[2]+v[2]*dt]; v[1]-=g*dt
        for b in blocks:
            if b['id'] in gone: continue
            c=[BASE[i]+b['p'][i] for i in range(3)]
            if all(abs(p[i]-c[i])<=b['s'][i]/2+0.225 for i in range(3)): return b['id']   # projectile radius 0.225 (scale 0.45)
        if p[1]<-6: return None
    return None
def main_unused():
    ch=json.load(open(os.path.join(HERE,'chapter1_levels.json')))['levels']
    res={r['id']:r for r in json.load(open(os.path.join(HERE,'sim_results.json')))}
    out={"_doc":__doc__.strip(),"_assumptions":{"wrecker":[WX,0,0],"pivotY":PY,"spawnAlong":SA,"structureBase":BASE,"launchSpeed":V,
          "restElevation":REST_EL,"yawDegPerDp":YAW_DP,"pitchDegPerDp":PITCH_DP,"screen":"1080x2340 @ 420 dpi, drag from (540,1300); drag right = +yaw, drag UP (smaller y) = +elevation"},"levels":[]}
    for L in ch:
        lay=json.load(open(os.path.join(HERE,'layouts',L['layout']+'.json')))['blocks']; bid={b['id']:b for b in lay}
        r=res[L['id']]
        if L.get('bossId'): targets=['shoulderL','shoulderR','core','head','core']
        else:
            targets=[]
            for part in r['seq'].split(', '):
                if part.startswith('splitstrike:'): targets.append(part.split(':')[1].split('+')[1])  # aim the split at the middle keystone
                else: targets.append(part)
        st=sim.State(lay); shots=[]
        for i,t in enumerate(targets):
            b=bid[t]; w=tuple(BASE[k]+b['p'][k] for k in range(3)); s={}
            for gs in (1.0,1.2):
                a=solve(w,9.81*gs)
                if a is None: s[str(gs)]=None; continue
                yaw,el=a; dpx,dpy=yaw/YAW_DP,(el-REST_EL)/PITCH_DP; px=lambda q: round(q*DENSITY/160)
                gone=st.broken|st.coll
                if L.get('bossId'):   # TidewardenController: shoulders broken -> armour breaks; shutter slides up while open
                    gone=set(targets[:min(i,2)])|({'armorTop','armorBot'} if i>=2 else set())|({'armL','armR'} if i>=2 else set())
                    if t=='core': gone|={'shutter'}
                s[str(gs)]=dict(yaw=round(yaw,2),elevation=round(el,2),hintYaw=round(yaw*2)/2,hintPitch=round(el*2)/2,
                    drag_dp=[round(dpx,1),round(dpy,1)],swipe=[CX,CY,CX+px(dpx),CY-px(dpy)],first_hit=first_hit(lay,gone,yaw,el,9.81*gs))
            pu='splitstrike' if 'splitstrike:' in r['seq'] and i==len(targets)-1 else None
            shots.append(dict(shot=i+1,target=t,powerup=pu,world=[round(x,3) for x in w],aim=s,clear=s['1.0'] and s['1.0']['first_hit']==t))
            if not L.get('bossId'):
                if pu: st=sim.apply(st,('splitstrike',tuple(r['seq'].split(':')[1].split('+'))))
                else: st=sim.apply(st,(None,t))
        out['levels'].append(dict(id=L['id'],layout=L['layout'],line=r['seq'],shots_budget=L['shots'],shots=shots,
            all_clear=all(x['clear'] for x in shots)))
    json.dump(out,open(os.path.join(HERE,'ci_win_shots.json'),'w'),indent=1)
    for l in out['levels']:
        print(l['id'],'OK' if l['all_clear'] else 'BLOCKED',[(s['target'],s['aim']['1.0'] and (s['aim']['1.0']['hintYaw'],s['aim']['1.0']['hintPitch'],s['aim']['1.0']['first_hit'])) for s in l['shots']])

def plan(lay,seq,boss=False):
    """Ordered shots [(target, powerup, triple|None)] from sim's best line."""
    if boss: return [(t,None,None) for t in ['shoulderL','shoulderR','core','head','core']]
    out=[]
    for part in seq.split(', '):
        if ':' in part:
            pu,tr=part.split(':'); tr=tr.split('+'); out.append((tr[1],pu,tuple(tr)))   # aim the split at the middle piece
        else: out.append((part,None,None))
    return out

def shots_for(lay,steps,boss=False):
    bid={b['id']:b for b in lay}; st=sim.State(lay); shots=[]
    for i,(t,pu,tr) in enumerate(steps):
        b=bid[t]; w=tuple(BASE[k]+b['p'][k] for k in range(3)); s={}
        for gs in (1.0,1.2):
            g=9.81*gs; a=solve(w,g)
            if a is None: s[str(gs)]=None; continue
            yaw,el=a; dpx,dpy=yaw/YAW_DP,(el-REST_EL)/PITCH_DP; px=lambda q: round(q*DENSITY/160)
            gone=st.broken|st.coll
            if boss:   # TidewardenController: shoulders broken -> armour + arms off; shutter open on the core turns
                gone=set(x for x,_,_ in steps[:min(i,2)])|({'armorTop','armorBot','armL','armR'} if i>=2 else set())
                if t=='core': gone|={'shutter'}
            fh=first_hit(lay,gone,yaw,el,g,split=bool(tr))
            ok=(sorted(tr)==fh) if tr else (fh==t)
            s[str(gs)]=dict(yaw=round(yaw,2),elevation=round(el,2),hintYaw=round(yaw*2)/2,hintPitch=round(el*2)/2,
                drag_dp=[round(dpx,1),round(dpy,1)],swipe=[CX,CY,CX+px(dpx),CY-px(dpy)],first_hit=fh,clear=ok)
        shots.append(dict(shot=i+1,target=t,powerup=pu,split_targets=list(tr) if tr else None,world=[round(x,3) for x in w],aim=s,
                          clear=all(s[k] and s[k]['clear'] for k in ('1.0','1.2'))))
        if not boss: st=sim.apply(st,(sim.EFFECT.get(pu,pu),tr) if tr else (None,t))
    return shots

def main():
    ch=json.load(open(os.path.join(HERE,'chapter1_levels.json')))['levels']
    res={r['id']:r for r in json.load(open(os.path.join(HERE,'sim_results.json')))}
    out={"_doc":__doc__.strip()+"\nrev 6: 'clear' now requires the first hit to be the intended piece at BOTH gravity x1.0 (live build, CI --gscale 1.0) "
         "and x1.2 (PR #15, each with its own aim); Multi Shot is modelled (split at min(apex, 70% flight), children +/-8 deg).",
         "_assumptions":{"wrecker":[WX,0,0],"pivotY":PY,"spawnAlong":SA,"structureBase":BASE,"launchSpeed":V,
          "restElevation":REST_EL,"yawDegPerDp":YAW_DP,"pitchDegPerDp":PITCH_DP,"splitSpreadDeg":SPLIT_DEG,
          "screen":"1080x2340 @ 420 dpi, drag from (540,1300); drag right = +yaw, drag UP (smaller y) = +elevation"},"levels":[],"daily_boss":[]}
    for L in ch:
        lay=json.load(open(os.path.join(HERE,'layouts',L['layout']+'.json')))['blocks']; r=res[L['id']]
        boss=bool(L.get('bossId')); shots=shots_for(lay,plan(lay,r['seq'],boss),boss)
        out['levels'].append(dict(id=L['id'],layout=L['layout'],line=r['seq'],shots_budget=L['shots'],shots=shots,all_clear=all(x['clear'] for x in shots)))
    dbp=os.path.join(HERE,'../daily_boss/daily_boss_results.json')
    if os.path.exists(dbp):
        for r in json.load(open(dbp))['arenas']:
            lay=json.load(open(os.path.join(sim.DATA,'Layouts',r['layout']+'.json')))['blocks']
            shots=shots_for(lay,plan(lay,r['seq']))
            out['daily_boss'].append(dict(layout=r['layout'],line=r['seq'],shots=shots,all_clear=all(x['clear'] for x in shots)))
    json.dump(out,open(os.path.join(HERE,'ci_win_shots.json'),'w'),indent=1)
    for l in out['levels']+out['daily_boss']:
        print(l.get('id',l['layout']),'OK' if l['all_clear'] else 'BLOCKED',[(s['target'],*(s['aim'][g] and (s['aim'][g]['hintYaw'],s['aim'][g]['hintPitch'],s['aim'][g]['first_hit']) for g in ('1.0','1.2'))) for s in l['shots']])
if __name__=='__main__': main()
