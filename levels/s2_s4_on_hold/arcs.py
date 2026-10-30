"""Shared shot geometry for sim.py and ci_win_shots.py (world coordinates = layout + structureBase (10,0,0)).
Same constants as Tools/ContentGen/derive_hints.py: Wrecker x=-20, pivot y=1.5, spawn 1.95 m along the barrel, +yaw -> +z,
launchSpeed 28 (wrecker.json), low arc. Projectile radius 0.225 (scale 0.45). Sim tick 0.02 s (PowerUpRulesV2.SimStep).
Moving blocks (spinDps > 0) are posed by rotating about pivot/spinAxis (LevelBuilder: pivotWorld = structureRoot + pivot)."""
import math
WX,PY,SA=-20.0,1.5,1.95; BASE=(10.0,0.0,0.0); V=28.0; RP=0.225; TICK=0.02
G1=9.81; G12=9.81*1.2
def muzzle(yaw,el):
    y,e=math.radians(yaw),math.radians(el); d=(math.cos(e)*math.cos(y),math.sin(e),math.cos(e)*math.sin(y))
    return tuple(p+SA*c for p,c in zip((WX,PY,0.0),d)),d
def solve(t,g,v=V):
    yaw,el=0.0,20.0
    for _ in range(30):
        m,_=muzzle(yaw,el); dx,dy,dz=(a-b for a,b in zip(t,m)); yaw=math.degrees(math.atan2(dz,dx)); r=math.hypot(dx,dz)
        disc=v**4-g*(g*r*r+2*dy*v*v)
        if disc<0: return None
        el=math.degrees(math.atan((v*v-math.sqrt(disc))/(g*r)))
    return yaw,el
def rot(v,axis,deg):
    n=math.sqrt(sum(a*a for a in axis)) or 1.0; k=[a/n for a in axis]; th=math.radians(deg); c,s=math.cos(th),math.sin(th)
    kv=sum(a*b for a,b in zip(k,v)); kx=(k[1]*v[2]-k[2]*v[1],k[2]*v[0]-k[0]*v[2],k[0]*v[1]-k[1]*v[0])
    return tuple(v[i]*c+kx[i]*s+k[i]*kv*(1-c) for i in range(3))
def moving(b): return (b.get('spinDps') or 0)>0 and b.get('spinAxis') and len(b['spinAxis'])==3 and b.get('pivot') and len(b['pivot'])==3
def pose(b,theta,world=True):
    """(centre, axes[3], half) of block b; movers rotated by theta deg about their pivot."""
    off=BASE if world else (0.0,0.0,0.0); h=[s/2 for s in b['s']]
    if not moving(b) or not theta: return tuple(b['p'][i]+off[i] for i in range(3)),((1,0,0),(0,1,0),(0,0,1)),h
    pv=b['pivot']; rel=[b['p'][i]-pv[i] for i in range(3)]; r=rot(rel,b['spinAxis'],theta)
    return tuple(pv[i]+r[i]+off[i] for i in range(3)),tuple(rot(a,b['spinAxis'],theta) for a in ((1,0,0),(0,1,0),(0,0,1))),h
def inside(p,box,pad=RP):
    c,ax,h=box; d=[p[i]-c[i] for i in range(3)]
    return all(abs(sum(d[i]*a[i] for i in range(3)))<=h[k]+pad for k,a in enumerate(ax))
def seg_box(o,d,box,pad):
    """slab test: ray o+s*d (s>=0) vs OBB expanded by pad; returns entry s or None"""
    c,ax,h=box; lo,hi=0.0,1e18
    for k,a in enumerate(ax):
        oc=sum((o[i]-c[i])*a[i] for i in range(3)); dd=sum(d[i]*a[i] for i in range(3)); e=h[k]+pad
        if abs(dd)<1e-12:
            if abs(oc)>e: return None
            continue
        s1,s2=(-e-oc)/dd,(e-oc)/dd
        if s1>s2: s1,s2=s2,s1
        lo,hi=max(lo,s1),min(hi,s2)
        if lo>hi: return None
    return lo
def march(blocks,gone,yaw,el,g,steer=0.0,steer_ticks=60,theta=0.0,dt=0.005,path=False,v=V):
    """Ballistic (optionally yaw-steered, deg/s for the first steer_ticks ticks) flight. Returns (first hit id, points)."""
    p,d=muzzle(yaw,el); vel=[v*c for c in d]; p=list(p); pts=[]; t=0.0
    boxes=[(b['id'],pose(b,theta)) for b in blocks if b['id'] not in gone]
    for _ in range(int(3.0/dt)):
        if steer and t<steer_ticks*TICK:
            h=rot((vel[0],0,vel[2]),(0,1,0),-steer*dt); vel[0],vel[2]=h[0],h[2]   # +steer turns toward +z (AimController: Euler(0,-yaw,0))
        p=[p[0]+vel[0]*dt,p[1]+vel[1]*dt-0.5*g*dt*dt,p[2]+vel[2]*dt]; vel[1]-=g*dt; t+=dt
        if path: pts.append(tuple(p))
        for i,bx in boxes:
            if inside(p,bx): return i,pts,t
        if p[1]<-6: return None,pts,t
    return None,pts,t
