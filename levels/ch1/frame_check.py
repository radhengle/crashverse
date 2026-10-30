#!/usr/bin/env python3
"""Fixed behind-the-cannon camera frame check (Developer request, 2026-10-30).
Projects all 8 corners of every piece (structureBase + p +/- s/2) of every Ch1 layout and Daily Boss layout into a
portrait 1080x2340 viewport and checks a safe area. Unity convention: fieldOfView is VERTICAL; camera looks along +x
(yaw 0), pitched down by `pitch`; +z is screen-LEFT (P0 aim fix: +yaw aims screen-left from the run camera).

ASSUMED camera (no fixed-camera code exists yet on integration/v2 or any branch/PR as of 2026-10-30; CameraConfig there
is still the orbit rig: fov 50, distance 22, yaw 72, pitch 15). Edit CAMERAS when Developer's real values land:
  primary : FOV 50 (CameraConfig.fov kept), eye 8 m behind and 5 m above the Wrecker (x=-28, y=5, z=0), pitch 8 deg down
            -> cannon sits bottom-centre (~68-98% of screen height)
  tight   : stress case, FOV 50, eye 6 m behind / 4 m up (x=-26, y=4), pitch 5 deg (narrower view of the structure)
Safe area: 6% side margins (65 px), top 10% (234 px, HUD bar), bottom 80% (1872 px, cannon + FIRE/power-up rail).
Usage: frame_check.py [--json]"""
import json, math, os, sys, glob
HERE=os.path.dirname(os.path.abspath(__file__)); DATA=os.path.join(HERE,'../../Assets/_Project/Resources/Data')
W,H=1080,2340; BASE=(10.0,0.0,0.0)
CAMERAS={'primary':dict(fov=50.0,eye=(-28.0,5.0,0.0),pitch=8.0),'tight':dict(fov=50.0,eye=(-26.0,4.0,0.0),pitch=5.0)}
SAFE=dict(left=0.06*W,right=0.94*W,top=0.10*H,bottom=0.80*H)
def project(pt,c):
    x,y,z=(pt[i]-c['eye'][i] for i in range(3)); p=math.radians(c['pitch'])
    fwd=x*math.cos(p)-y*math.sin(p); up=x*math.sin(p)+y*math.cos(p); right=-z
    t=math.tan(math.radians(c['fov']/2)); nx=right/fwd/(t*W/H); ny=up/fwd/t
    return (nx+1)/2*W,(1-ny)/2*H
def check(blocks,c):
    xs=[];ys=[];worst=None
    for b in blocks:
        for dx in (-1,1):
            for dy in (-1,1):
                for dz in (-1,1):
                    pt=[BASE[i]+b['p'][i]+(dx,dy,dz)[i]*b['s'][i]/2 for i in range(3)]
                    u,v=project(pt,c); xs.append(u); ys.append(v)
                    over=max(SAFE['left']-u,u-SAFE['right'],SAFE['top']-v,v-SAFE['bottom'])
                    if worst is None or over>worst[0]: worst=(over,b['id'])
    return dict(x=[round(min(xs)),round(max(xs))],y=[round(min(ys)),round(max(ys))],fits=worst[0]<=0,
                worst_piece=worst[1],slack_px=round(-worst[0]))
def layouts():
    ch=json.load(open(os.path.join(HERE,'chapter1_levels.json')))['levels']
    out=[(L['id'],L['layout'],os.path.join(HERE,'layouts',L['layout']+'.json')) for L in ch]
    for n in json.load(open(os.path.join(DATA,'meta_v2.json')))['dailyBoss']['layouts']:
        out.append(('daily_boss',n,os.path.join(DATA,'Layouts',n+'.json')))
    return out
def main():
    rows=[]
    for lid,name,path in layouts():
        bl=json.load(open(path))['blocks']; r=dict(id=lid,layout=name)
        for k,c in CAMERAS.items(): r[k]=check(bl,c)
        rows.append(r)
    if '--json' in sys.argv: print(json.dumps(dict(cameras=CAMERAS,safe=SAFE,rows=rows),indent=1)); return rows
    print(f"{'level':11} {'layout':28} {'primary x/y px':26} {'slack':>6} {'tight slack':>11}  worst piece")
    for r in rows:
        p,t=r['primary'],r['tight']
        print(f"{r['id']:11} {r['layout']:28} x{p['x']} y{p['y']}".ljust(67)+f"{p['slack_px']:>6} {t['slack_px']:>11}  {p['worst_piece']}/{t['worst_piece']} {'OK' if p['fits'] and t['fits'] else ('OK(primary)' if p['fits'] else 'FAIL')}")
    return rows
if __name__=='__main__': main()
