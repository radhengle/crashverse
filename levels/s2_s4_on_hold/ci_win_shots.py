#!/usr/bin/env python3
"""Known-winning aim for every S2-S4 level's sim-proven best line (BUG-24 format, as levels/ch1/ci_win_shots.json), at g x1.0 and
x1.2 (GD ruling #10: x1.2 is canonical). Geometry from arcs.py (= derive_hints.py constants). For each shot:
- normal / mega_bomb / shield / time_freeze: low-arc ballistic solve; the arc is ray-marched against every intact box and the
  FIRST piece hit is reported. Moving blocks are posed at every 5 deg of rotation: open_phases = rotor phases (deg, at impact)
  for which the arc hits the target first; time_freeze shots also give the open window at 1x and at 0.25x.
- laser_beam: straight beam from the barrel to the target (sphere-cast r 0.15); beam_hits lists the pieces in order, the 3rd
  must be the target (first 2 pass for 0).
- guided_rocket: launch yaw/elevation + constant steer rate for 60 ticks found by search; the steered flight must hit first.
Hero slot (ruling #11): the slot volume is assumed to be a 6 x 12 x 10 m box (x, y, z) centred on heroSlot.pos (x, z), on the
ground; every winning-line path and every 'aim at any piece' lane (g x1.0 and x1.2) is checked never to enter it."""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim, arcs
HERE=os.path.dirname(os.path.abspath(__file__))
REST_EL=25.0; YAW_DP=.25; PITCH_DP=.15; DENSITY=420; CX,CY=540,1300
HERO_HALF=(3.0,6.0,5.0)
def hero_box(L):
    p=(L.get('heroSlot') or {}).get('pos') or [9,0,0]
    return (arcs.BASE[0]+p[0],arcs.BASE[1]+p[1]+HERO_HALF[1],arcs.BASE[2]+p[2]),((1,0,0),(0,1,0),(0,0,1)),list(HERO_HALF)
def in_hero(pts,hb): return any(arcs.inside(q,hb,pad=0.0) for q in pts)
def swipe(yaw,el):
    dpx,dpy=yaw/YAW_DP,(el-REST_EL)/PITCH_DP; px=lambda q: round(q*DENSITY/160)
    return [round(dpx,1),round(dpy,1)],[CX,CY,CX+px(dpx),CY-px(dpy)]
def ballistic(lay,gone,t,g,hb):
    b=next(x for x in lay if x['id']==t); w=tuple(arcs.BASE[i]+b['p'][i] for i in range(3)); a=arcs.solve(w,g)
    if a is None: return None
    yaw,el=a; movers=[x for x in lay if arcs.moving(x) and x['id'] not in gone]
    phases=list(range(0,360,5)) if movers else [0]; ok=[]; first=None; pts=[]; ft=0
    for th in phases:
        h,p,f=arcs.march(lay,gone,yaw,el,g,theta=th,path=True)
        if first is None: first=h
        if h==t: ok.append(th); pts=p; ft=f
    d,s=swipe(yaw,el)
    return dict(yaw=round(yaw,2),elevation=round(el,2),hintYaw=round(yaw*2)/2,hintPitch=round(el*2)/2,drag_dp=d,swipe=s,
                first_hit=t if ok else first,open_phases=(ok if movers else None),open_fraction=round(len(ok)/len(phases),3),
                flight_s=round(ft,2),hero_clear=not in_hero(pts,hb)),bool(ok)
def laser(lay,gone,t,hb):
    b=next(x for x in lay if x['id']==t); w=tuple(arcs.BASE[i]+b['p'][i] for i in range(3)); yaw=el=0.0
    for _ in range(10):
        m,_=arcs.muzzle(yaw,el); d=[w[i]-m[i] for i in range(3)]
        yaw=math.degrees(math.atan2(d[2],d[0])); el=math.degrees(math.atan2(d[1],math.hypot(d[0],d[2])))
    m,_=arcs.muzzle(yaw,el); d=[w[i]-m[i] for i in range(3)]; n=math.sqrt(sum(a*a for a in d)); d=[a/n for a in d]
    hits=sorted((e,x['id']) for x in lay if x['id'] not in gone and not arcs.moving(x) for e in [arcs.seg_box(m,d,arcs.pose(x,0),sim.LASER_R)] if e is not None)
    ids=[i for _,i in hits]; end=hits[2][0] if len(hits)>=3 else 60
    pts=[tuple(m[i]+d[i]*s*0.1 for i in range(3)) for s in range(int(end*10)+1)]
    ds,sw=swipe(yaw,el)
    return dict(yaw=round(yaw,2),elevation=round(el,2),drag_dp=ds,swipe=sw,beam_hits=ids[:4],hero_clear=not in_hero(pts,hb)),len(ids)>=3 and ids[2]==t
def rocket(lay,gone,t,g,hb):
    p=sim.rocket_program(lay,gone,t,g)
    if not p: return None,False
    h,pts,_=arcs.march(lay,gone,p['yaw'],p['elevation'],g,steer=p['steerDegPerSec'],dt=0.01,path=True)
    ds,sw=swipe(p['yaw'],p['elevation'])
    return dict(p,drag_dp=ds,swipe=sw,first_hit=h,hero_clear=not in_hero(pts,hb)),h==t
def lanes(L,lay,hb):
    """every piece of the untouched layout, aimed at directly (g x1.0 and x1.2): does any arc enter the hero slot?"""
    bad=[]
    for b in lay:
        if b.get('indestructible'): continue
        w=tuple(arcs.BASE[i]+b['p'][i] for i in range(3))
        for g in (arcs.G1,arcs.G12):
            a=arcs.solve(w,g)
            if a is None: continue
            _,pts,_=arcs.march(lay,set(),a[0],a[1],g,path=True,dt=0.01)
            if in_hero(pts,hb): bad.append((b['id'],round(g/9.81,1)))
    return bad
def main():
    res={r['id']:r for r in json.load(open(os.path.join(HERE,'sim_results.json')))}
    out={"_doc":__doc__.strip(),"_assumptions":{"wrecker":[arcs.WX,0,0],"pivotY":arcs.PY,"spawnAlong":arcs.SA,"structureBase":arcs.BASE,
          "launchSpeed":arcs.V,"restElevation":REST_EL,"yawDegPerDp":YAW_DP,"pitchDegPerDp":PITCH_DP,"heroSlotHalfExtents":HERO_HALF,
          "timingTau_s":sim.TAU,"timeFreezeSlow":sim.SLOW,"laserRadius":sim.LASER_R,
          "screen":"1080x2340 @ 420 dpi, drag from (540,1300); drag right = +yaw, drag UP = +elevation"},"levels":[]}
    bad=0
    for c in (2,3,4):
        for L in json.load(open(os.path.join(HERE,f'data/chapter{c}.json')))['levels']:
            lay=json.load(open(os.path.join(HERE,'data/Layouts',L['layout']+'.json')))['blocks']; r=res[L['id']]; hb=hero_box(L)
            st=sim.State(lay); shots=[]
            overlap=[b['id'] for b in lay if any(arcs.inside(q,hb,pad=0) for q in [tuple(arcs.BASE[i]+b['p'][i]+sx*b['s'][i]/2*(i==0)+sz*b['s'][i]/2*(i==2) for i in range(3)) for sx in (-1,1) for sz in (-1,1)])]
            for i,part in enumerate(r['seq'].split(', ')):
                pu,t=(part.split(':') if ':' in part else (None,part)); gone=st.broken|st.coll; aim={}; okg={}
                for gk,g in (('1.0',arcs.G1),('1.2',arcs.G12)):
                    if pu=='laser_beam': aim[gk],okg[gk]=laser(lay,gone,t,hb)
                    elif pu=='guided_rocket': aim[gk],okg[gk]=rocket(lay,gone,t,g,hb)
                    else:
                        x=ballistic(lay,gone,t,g,hb); aim[gk],okg[gk]=(x if x else (None,False))
                tm=st.timing(t)
                sh=dict(shot=i+1,target=t,powerup=pu,world=[round(arcs.BASE[k]+st.b[t]['p'][k],3) for k in range(3)],aim=aim,
                        clear=okg['1.0'],clear_g12=okg['1.2'],hero_clear=all(a and a['hero_clear'] for a in aim.values()))
                if tm: sh['timing']=dict(window_1x_s=round(tm[0],3),window_frozen_s=round(tm[0]/sim.SLOW,3),open_fraction=round(tm[1],3),tau_s=sim.TAU)
                shots.append(sh); st,_=sim.apply(st,(pu,t))
            ln=lanes(L,lay,hb)
            ac=all(s['clear'] and s['clear_g12'] and s['hero_clear'] for s in shots) and not ln and not overlap; bad+=not ac
            out['levels'].append(dict(id=L['id'],number=L['number'],layout=L['layout'],line=r['seq'],shots_data=L['shots'],
                shots_budget=sim.budget(L),forced=L.get('forced',[]),shots=shots,final_ratio=round(st.ratio(),3),
                hero_slot=dict(L.get('heroSlot') or {},assumedHalfExtents=HERO_HALF,lanes_entering=ln,blocks_overlapping=overlap,
                               layout_max_x=round(max(arcs.BASE[0]+b['p'][0]+b['s'][0]/2 for b in lay),2),slot_min_x=round(hb[0][0]-HERO_HALF[0],2)),
                all_clear=ac))
    json.dump(out,open(os.path.join(HERE,'ci_win_shots.json'),'w'),indent=1)
    for l in out['levels']:
        print(l['id'],'OK' if l['all_clear'] else 'BLOCKED',l['final_ratio'],f"hero lanes={len(l['hero_slot']['lanes_entering'])} maxX={l['hero_slot']['layout_max_x']}/slot {l['hero_slot']['slot_min_x']}",
              [(s['target'],s['powerup'],s['clear'],s['clear_g12'],s['hero_clear'],(s['aim']['1.2'] or {}).get('first_hit') or (s['aim']['1.2'] or {}).get('beam_hits')) for s in l['shots']])
    print('ALL CLEAR' if not bad else f'{bad} BLOCKED')
if __name__=='__main__': main()
