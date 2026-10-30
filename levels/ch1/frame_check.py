#!/usr/bin/env python3
"""Behind-the-cannon frame check (rev 9.1): a faithful Python port of Developer's CannonCameraRules.Solve / ToViewport /
FullyVisible / ViewportRect (Assets/_Project/Scripts/Meta/CannonCameraRules.cs, integration/v2 @ fb37202, Settings defaults).
The game has no fixed camera: OrbitCamera.FrameBehindCannon solves a pose per level and per screen aspect from
  cannonRoot = Wrecker position (WreckerSpec.WreckerX, 0, 0) = (-20, 0, 0), firing direction world +x,
  structure  = union of every block's collider box (GameBootstrap.StructureBounds: structureBase (10,0,0) + p, size s).
Solve: camera `back` m behind the cannon root and `height` m up, on the firing line (yaw 0: +z = screen-left); vertical FOV fits
the cannon's lowest point and the whole structure inside top/bottom margins, also the structure width (sideFill) for the aspect,
clamped to [minFov, maxFov]; pitch puts the cannon's lowest point bottomMargin above the bottom edge, then tilts up if the
structure top would be clipped. Unity conventions: fieldOfView is vertical, viewport (0,0) = bottom-left.
Per level and aspect it reports FOV, pitch (deg down), whether the building / cannon are fully visible, the building's slack
(px to the nearest left/right/top screen edge) and the CannonCameraEditModeTests assertions (centred / bottom / big cannon,
building above and filling the view). It also traces each stored hint aim (hintYaw/hintPitch, derive_hints geometry, g x1.0)
from the muzzle to its hintTarget and checks the whole arc and the target are on screen.
HUD mode (rev 9.2): --hud [--top-margin X] flags any building or hint-arc pixel above the run-HUD line
(safeInsetTop + 104 dp x dpi/160, worst-case inset 130 px; dpi per screen in HUD_DPI) and searches the smallest topMargin
(0.005 steps) that clears it on every layout and screen.
Usage: frame_check.py [--json] | frame_check.py --hud [--top-margin X] [--json]"""
import json, math, os, sys
HERE=os.path.dirname(os.path.abspath(__file__)); DATA=os.path.join(HERE,'../../Assets/_Project/Resources/Data')
BASE=(10.0,0.0,0.0); CANNON_ROOT=(-20.0,0.0,0.0)
# Wrecker v2 envelope (CannonCameraEditModeTests.Cannon): centre (-19.875, 1.3, 0), size (3.75, 2.6, 2.0)
CANNON_BOX=((-19.875-1.875,0.0,-1.0),(-19.875+1.875,2.6,1.0))
SETTINGS=dict(back=14.0,height=4.0,cannonRear=1.8,cannonTop=2.6,topMargin=0.06,bottomMargin=0.03,sideFill=0.92,minFov=24.0,maxFov=68.0)
SCREENS=[('1080x2340',1080,2340),('1080x1920',1080,1920),('1440x2560',1440,2560),('1536x2048 (4:3)',1536,2048)]
TEST_ASPECTS=[1080/2340,1080/2400,1080/2520,9/16,0.5,0.75]   # CannonCameraEditModeTests.Aspects
# hint arc (derive_hints.py / AimController): muzzle 1.95 m along the barrel from the pivot (-20, 1.5, 0), launchSpeed from wrecker.json
WX,PY,SA=-20.0,1.5,1.95
D=math.degrees; R=math.radians
clamp=lambda v,a,b: max(a,min(b,v))

def solve(cannon_root,bmin,bmax,aspect,s=SETTINGS):
    """CannonCameraRules.Solve(cannonRoot, firingDir=+x, structure, aspect, Default). Returns dict(cam, pitch, fov, ...)."""
    aspect=clamp(aspect,0.3,2.5)
    cam=(cannon_root[0]-s['back'],cannon_root[1]+s['height'],cannon_root[2])   # dir = +x, side = Cross(up, dir) = -z
    near,far,halfW=float('inf'),float('-inf'),0.0
    for i in range(8):
        p=(bmax[0] if i&1 else bmin[0],bmax[1] if i&2 else bmin[1],bmax[2] if i&4 else bmin[2])
        along=p[0]-cam[0]; across=abs(-(p[2]-cam[2]))
        near=min(near,along); far=max(far,along); halfW=max(halfW,across)
    near=max(1.0,near)
    aTop=D(math.atan2(bmax[1]-cam[1],near)); aSB=D(math.atan2(bmin[1]-cam[1],far))
    aCL=D(math.atan2(cannon_root[1]-cam[1],max(0.5,s['back']-s['cannonRear'])))
    hi=max(aTop,aSB); lo=min(aCL,aSB)
    fov_span=(hi-lo)/max(0.5,1-s['topMargin']-s['bottomMargin'])
    needHalfH=math.atan2(halfW/max(0.1,s['sideFill']),near)
    fov_width=2*D(math.atan(math.tan(needHalfH)/aspect))
    raw=max(fov_span,fov_width); fov=clamp(raw,s['minFov'],s['maxFov'])
    pitch=-(lo+fov*(0.5-s['bottomMargin']))
    topEdge=-pitch+fov*(0.5-s['topMargin']*0.5); tilted=0.0
    if topEdge<aTop: tilted=aTop-topEdge; pitch-=tilted
    return dict(cam=cam,pitch=pitch,fov=fov,fov_raw=raw,fov_by='width' if fov_width>fov_span else 'height',
                clamped='max' if raw>s['maxFov'] else 'min' if raw<s['minFov'] else None,tilted_up=tilted)

def to_viewport(pose,aspect,w):
    """CannonCameraRules.ToViewport: rotation = LookRotation(+x, up) * Euler(pitchDown, 0, 0)."""
    p=R(pose['pitch']); d=[w[i]-pose['cam'][i] for i in range(3)]
    fwd=(math.cos(p),-math.sin(p),0.0); up=(math.sin(p),math.cos(p),0.0); right=(0.0,0.0,-1.0)
    lx=sum(a*b for a,b in zip(d,right)); ly=sum(a*b for a,b in zip(d,up)); lz=sum(a*b for a,b in zip(d,fwd))
    if lz<=1e-4: return (-1.0,-1.0,lz)
    t=math.tan(R(pose['fov']*0.5))
    return (0.5+0.5*lx/(lz*t*aspect),0.5+0.5*ly/(lz*t),lz)

def corners(bmin,bmax): return [(bmax[0] if i&1 else bmin[0],bmax[1] if i&2 else bmin[1],bmax[2] if i&4 else bmin[2]) for i in range(8)]
def fully_visible(pose,aspect,bmin,bmax,tol=0.005):
    for c in corners(bmin,bmax):
        v=to_viewport(pose,aspect,c)
        if v[2]<=0 or v[0]<-tol or v[0]>1+tol or v[1]<-tol or v[1]>1+tol: return False
    return True
def vp_rect(pose,aspect,bmin,bmax):
    vs=[to_viewport(pose,aspect,c) for c in corners(bmin,bmax)]
    return (min(v[0] for v in vs),min(v[1] for v in vs),max(v[0] for v in vs),max(v[1] for v in vs))

def bounds(blocks):
    lo=[float('inf')]*3; hi=[float('-inf')]*3
    for b in blocks:
        for i in range(3):
            lo[i]=min(lo[i],BASE[i]+b['p'][i]-b['s'][i]/2); hi[i]=max(hi[i],BASE[i]+b['p'][i]+b['s'][i]/2)
    return tuple(lo),tuple(hi)

def test_asserts(pose,aspect,bmin,bmax):
    """CannonCameraEditModeTests.Every_level_and_boss_frames_the_whole_cannon_and_building, assertion by assertion."""
    c=vp_rect(pose,aspect,*CANNON_BOX); s=vp_rect(pose,aspect,bmin,bmax)
    ccx=(c[0]+c[2])/2; ccy=(c[1]+c[3])/2; scy=(s[1]+s[3])/2
    fails=[]
    if not fully_visible(pose,aspect,bmin,bmax): fails.append('building clipped')
    if not fully_visible(pose,aspect,*CANNON_BOX): fails.append('cannon clipped')
    if abs(ccx-0.5)>0.02: fails.append('cannon not centred')
    if not c[1]<0.08: fails.append('cannon not at bottom')
    if not c[2]-c[0]>0.35: fails.append('cannon small')
    if not c[3]<0.6: fails.append('cannon too high')
    if not scy>ccy+0.15: fails.append('building not above cannon')
    if not (s[2]-s[0]>=0.65 or s[3]>=0.7): fails.append('building does not fill')
    return fails,c,s

def hint_arc(pose,aspect,yaw,el,target,g=9.81,v0=28.0):
    """Trace the hinted shot from the muzzle until it passes the target's depth; worst viewport excursion of the arc + target."""
    y,e=R(yaw),R(el); d=(math.cos(e)*math.cos(y),math.sin(e),math.cos(e)*math.sin(y))
    p=[WX+SA*d[0],PY+SA*d[1],SA*d[2]]; v=[v0*c for c in d]; dt=0.01; vs=[]
    for _ in range(1000):
        vs.append(to_viewport(pose,aspect,p))
        if p[0]>=target[0] or p[1]<-2: break
        p=[p[0]+v[0]*dt,p[1]+v[1]*dt-0.5*g*dt*dt,p[2]+v[2]*dt]; v[1]-=g*dt
    tv=to_viewport(pose,aspect,target)
    on=lambda q: q[2]>0 and 0<=q[0]<=1 and 0<=q[1]<=1
    return dict(target_vp=[round(tv[0],3),round(tv[1],3)],target_on=on(tv),arc_on=all(on(q) for q in vs),
                arc_top_vp=round(max(q[1] for q in vs),3),arc_end=[round(p[0]-BASE[0],2),round(p[1],2),round(p[2],2)])

# ---- HUD-aware mode (rev 9.2). UI Developer: HudScreen (Screens.cs:276-287 on main) is anchored to the safe-area top; the opaque
# progress bar ends at 74 dp and the status text at 104 dp; canvas scale = dpi/160. HUD line (px from the screen top) =
# safeInsetTop + 104 dp * dpi/160. Calculated, not measured on a device. Per screen: (dpi, inset px), worst-case inset 130 px.
HUD_DP=104.0; HUD_INSET=130.0
HUD_DPI={'1080x2340':400,'1080x1920':400,'1440x2560':560,'1536x2048 (4:3)':320}
def hud_line_px(name,W,H,inset=HUD_INSET,dpi=None): return inset+HUD_DP*(dpi or HUD_DPI[name])/160.0
def hud_check(bl,tb,hint,settings,v0=28.0):
    """Per screen: px of the building top / hint arc top below the HUD line (negative = under the HUD)."""
    bmin,bmax=bounds(bl); out={}
    tgt=tuple(BASE[i]+tb['p'][i] for i in range(3)) if tb else None
    for nm,W,H in SCREENS:
        a=W/H; pose=solve(CANNON_ROOT,bmin,bmax,a,settings); fails,c,s=test_asserts(pose,a,bmin,bmax)
        line=hud_line_px(nm,W,H); btop=(1-s[3])*H
        e=dict(fov=round(pose['fov'],2),clamped=pose['clamped'],pitch_down=round(pose['pitch'],2),hud_line_px=round(line),
               building_top_px=round(btop),clear_px=round(btop-line),side_slack_px=round(min(s[0],1-s[2])*W),fails=fails)
        if tgt and hint[0] is not None:
            h=hint_arc(pose,a,hint[0],hint[1],tgt,v0=v0); e['hint_on']=h['arc_on'] and h['target_on']
            e['hint_arc_clear_px']=round((1-h['arc_top_vp'])*H-line)
        out[nm]=e
    return out
def hud_report(tm,v0):
    st=dict(SETTINGS,topMargin=tm); rows=[]
    for lid,name,ht,hy,hp in layouts():
        bl=json.load(open(os.path.join(DATA,'Layouts',name+'.json')))['blocks']
        rows.append(dict(id=lid,layout=name,screens=hud_check(bl,next((b for b in bl if b['id']==ht),None),(hy,hp),st,v0)))
    under=[(r['layout'],n) for r in rows for n,e in r['screens'].items() if e['clear_px']<0 or e.get('hint_arc_clear_px',0)<0]
    return rows,under
def hud_main():
    v0=json.load(open(os.path.join(DATA,'wrecker.json'))).get('launchSpeed',28.0)
    tm=float(sys.argv[sys.argv.index('--top-margin')+1]) if '--top-margin' in sys.argv else SETTINGS['topMargin']
    rows,under=hud_report(tm,v0)
    # smallest topMargin (0.005 steps) that clears the HUD on every layout and screen
    best=None
    for k in range(12,61):
        t=k*0.005; r2,u2=hud_report(t,v0)
        if not u2: best=(t,r2); break
    res=dict(hud=dict(dp=HUD_DP,inset_px=HUD_INSET,dpi=HUD_DPI,line_px={n:round(hud_line_px(n,W,H)) for n,W,H in SCREENS},
                      line_pct={n:round(hud_line_px(n,W,H)/H*100,1) for n,W,H in SCREENS}),
             top_margin=tm,rows=rows,under_hud=under,smallest_clear_top_margin=best[0] if best else None,
             at_smallest=best[1] if best else None)
    if '--json' in sys.argv: print(json.dumps(res,indent=1)); return res
    print('HUD line px (inset %d + %d dp x dpi/160):'%(HUD_INSET,HUD_DP),res['hud']['line_px'],res['hud']['line_pct'],'%')
    for title,rr,t in ((f'topMargin {tm}',rows,tm),)+(((f'smallest clear topMargin {best[0]}',best[1],best[0]),) if best else ()):
        print(f'\n== {title}: layout | per screen: building-top clear px / hint-arc clear px / FOV / side slack')
        for r in rr:
            print(f"{r['layout']:27} "+' | '.join(f"{e['clear_px']:5d} {e.get('hint_arc_clear_px','-'):>5} {e['fov']:5.1f}{'*' if e['clamped']=='max' else ' '} {e['side_slack_px']:4d}{' !' if e['fails'] or not e.get('hint_on',True) else ''}" for e in r['screens'].values()))
    print('\nunder the HUD at topMargin',tm,':',len(under),'layout/screen pairs;',sorted(set(x[0] for x in under if x[1]=='1080x2340')))
    print('smallest clear topMargin:',best[0] if best else 'none <= 0.30')
    return res

def layouts():
    ch=json.load(open(os.path.join(DATA,'chapter1.json')))['levels']
    out=[(L['id'],L['layout'],L.get('hintTarget'),L.get('hintYaw'),L.get('hintPitch')) for L in ch]
    arenas={a['layout']:a for a in json.load(open(os.path.join(DATA,'titan_templates.json'))).get('arenas',[])}
    for n in json.load(open(os.path.join(DATA,'meta_v2.json')))['dailyBoss']['layouts']:
        a=arenas.get(n,{}); out.append(('daily_boss',n,a.get('hintTarget'),a.get('hintYaw'),a.get('hintPitch')))
    return out

def main():
    v0=json.load(open(os.path.join(DATA,'wrecker.json'))).get('launchSpeed',28.0); rows=[]
    for lid,name,ht,hy,hp in layouts():
        bl=json.load(open(os.path.join(DATA,'Layouts',name+'.json')))['blocks']; bmin,bmax=bounds(bl)
        r=dict(id=lid,layout=name,bounds=[[round(x,2) for x in bmin],[round(x,2) for x in bmax]],screens={},test_aspects={})
        tb=next((b for b in bl if b['id']==ht),None)
        tgt=tuple(BASE[i]+tb['p'][i] for i in range(3)) if tb else None
        for nm,W,H in SCREENS:
            a=W/H; pose=solve(CANNON_ROOT,bmin,bmax,a); fails,c,s=test_asserts(pose,a,bmin,bmax)
            e=dict(fov=round(pose['fov'],2),fov_by=pose['fov_by'],clamped=pose['clamped'],pitch_down=round(pose['pitch'],2),
                   tilted_up=round(pose['tilted_up'],2),building_vp=[round(x,3) for x in s],cannon_vp=[round(x,3) for x in c],
                   slack_px=dict(left=round(s[0]*W),right=round((1-s[2])*W),top=round((1-s[3])*H)),fails=fails)
            e['slack_min_px']=min(e['slack_px'].values())
            if tgt and hy is not None: e['hint']=hint_arc(pose,a,hy,hp,tgt,v0=v0)
            r['screens'][nm]=e
        for a in TEST_ASPECTS:
            pose=solve(CANNON_ROOT,bmin,bmax,a); r['test_aspects'][f'{a:.3f}']=dict(fov=round(pose['fov'],2),fails=test_asserts(pose,a,bmin,bmax)[0])
        r['hint']=dict(target=ht,yaw=hy,pitch=hp)
        rows.append(r)
    flags=[f"{r['id']} {r['layout']} @ {k}: "+', '.join((['FOV clamped at '+str(e['fov'])] if e['clamped']=='max' else [])+e['fails']
           +([] if 'hint' not in e or (e['hint']['arc_on'] and e['hint']['target_on']) else ['hint arc/target off screen']))
           for r in rows for k,e in r['screens'].items()
           if e['clamped']=='max' or e['fails'] or ('hint' in e and not (e['hint']['arc_on'] and e['hint']['target_on']))]
    flags+=[f"{r['id']} {r['layout']} @ test aspect {k}: "+', '.join(t['fails']) for r in rows for k,t in r['test_aspects'].items() if t['fails']]
    if '--json' in sys.argv:
        print(json.dumps(dict(source='CannonCameraRules.cs @ fb37202 (port)',settings=SETTINGS,cannon_root=CANNON_ROOT,cannon_box=CANNON_BOX,
                              screens=SCREENS,test_aspects=TEST_ASPECTS,rows=rows,flags=flags),indent=1)); return rows
    print(f"{'level':11} {'layout':27} " + ' | '.join(f"{n.split(' ')[0]:>22}" for n,_,_ in SCREENS))
    print(f"{'':39}" + ' | '.join(f"{'fov  pitch  slack  hint':>22}" for _ in SCREENS))
    for r in rows:
        cells=[]
        for n,_,_ in SCREENS:
            e=r['screens'][n]; h=e.get('hint'); hk='-' if not h else ('ok' if h['arc_on'] and h['target_on'] else 'OFF')
            cells.append(f"{e['fov']:5.1f}{'*' if e['clamped'] else ' '}{e['pitch_down']:6.1f} {e['slack_min_px']:5d}{' !' if e['fails'] else '  '}{hk:>4}")
        print(f"{r['id']:11} {r['layout']:27} "+' | '.join(f"{c:>22}" for c in cells))
    print('slack = px from the building to the nearest left/right/top screen edge; * = FOV clamped; ! = a CannonCameraEditModeTests assertion fails')
    print('FLAGS:' if flags else 'FLAGS: none (no 68 deg clamp, nothing clipped, every EditMode-test assertion holds on all 6 test aspects, every hint arc on screen)')
    for f in flags: print('  '+f)
    return rows
if __name__=='__main__': hud_main() if '--hud' in sys.argv else main()
