#!/usr/bin/env python3
"""CRASHVERSE SFX library v1.1: the 33 ids the routing table reported as missing (Wrecker character, UI, rewards,
results accents, hints, explosive crate, kill-plane fall, Titan damage tick, terrain impact S/M/L).
Same style/mastering as core_sfx.py (original DSP + FluidR3_GM tonal layers, LP 9.5 kHz, mono 44.1k, TP <= -1 dBTP).
Usage: python extra_sfx.py   (writes ../<category>/*.ogg and extra_sfx_report.json)"""
import os,json,subprocess,numpy as np,soundfile as sf
import core_sfx as C
from core_sfx import N,T,flt,noise,env_exp,put,reverb,drive,thump,crack,metal,rumble,debris,whoosh,grind,hiss,bell,crackle,loopify,meas,fluid,m,pop,PIANO,NYLON,MARIMBA,VIB,KALIMBA,WOODBLK,SR,OUT,LP,loud
R=np.random.default_rng(4242); C.EV.clear(); ev=C.ev
def loopbuf(y,xf=.12): return loopify(np.concatenate([y,y[:N(xf)]]),xf)
# ---------- Wrecker (cannon character) ----------
def fidget(v):
    d=.8; y=np.zeros(N(d)); put(y,grind(.35,[(18,28),(14,22)][v],250,1600)*.5,0)                   # wheel creak
    put(y,fluid([(.3,VIB,m(['E6','D6'][v])-12,.4,70)],.5,.4),.3,.45); return reverb(y,.5,.15)[:N(d)]   # gold glint ting (octave down)
def aim_hum():
    d=1.0; t=T(d); y=np.sin(2*np.pi*120*t)*.5+np.sin(2*np.pi*240*t)*.2+np.sin(2*np.pi*360*t)*.08          # whole-cycle servo partials (loop-safe)
    y*=1+.08*np.sin(2*np.pi*6*t); y+=flt(noise(d),'band',[200,1200])*.06; return loopbuf(y)
def aim_tick(v): d=.08; y=pop([1500,1400,1600][v],900,d,.45)*.7; put(y,metal(600+40*v,.06,60,.3),0,.25); return y
def aim_clamp(): d=.2; y=np.zeros(N(d)); put(y,thump(.18,160,80,25),0,.8); put(y,metal(300,.15,30,.3),0,.3); put(y,crack(.02,500,2500,150),0,.3); return y
def aim_stretch():
    d=1.0; t=T(d); y=grind(d,(9,13),150,1100)*.6+np.sin(2*np.pi*np.cumsum(180+25*np.sin(2*np.pi*2*t))/SR)*.15   # rubbery band stretch
    return loopbuf(y)
def smoke_tail(v): d=.6; y=whoosh(d,[900,750][v],200,.15)*.8; put(y,flt(noise(d),'low',500)*env_exp(d,6,.01),0,.3); return reverb(y,.6,.2)[:N(d)]
def recoil_land(v):
    d=.5; y=np.zeros(N(d)); put(y,thump(.3,[120,110][v],50,14),0,.9); put(y,flt(noise(.08),'band',[150,1500])*env_exp(.08,50),0,.4)
    put(y,grind(.3,(22,30),200,1400)*.3,.1); return reverb(y,.4,.15)[:N(d)]
def slot_in(v):
    d=.4; y=np.zeros(N(d)); put(y,crack(.025,1000,5000,120),0,.5); put(y,metal(500,.1,40,.4),0,.3)
    put(y,fluid([(0,MARIMBA,m(['C5','E5','G5'][v]),.2,100),(.05,VIB,m(['C5','E5','G5'][v])+12,.25,70)],.35,.25),.02,.6); return y
def slot_out(): d=.2; y=pop(500,900,.12,.3); y=np.pad(y,(0,N(d)-len(y))); put(y,crack(.02,800,4000,150),0,.3); return y
def whoosh_loop(v):
    d=1.5; t=T(d); src=flt(noise(d),'band',[[250,1400],[220,1200]][v]); y=src*(.7+.3*np.sin(2*np.pi*(2/1.5)*t+v))   # round air, whole-cycle wobble
    y+=np.sin(2*np.pi*[90,80][v]*t)*.05; return loopbuf(y)
def win_hop():
    d=.8; y=np.zeros(N(d))
    for k,t0 in enumerate((0,.25,.45)):
        tt=T(.2); put(y,np.sin(2*np.pi*np.cumsum(260+120*k+250*np.sin(np.pi*tt/.2))/SR)*env_exp(.2,14),t0,.5); put(y,thump(.15,140,70,20),t0,.5)
    put(y,fluid([(.45,NYLON,m('C5'),.3,90),(.45,NYLON,m('G5'),.3,85)],.35,.4),.0,.5); return reverb(y,.5,.2)[:N(d)]
def fail_droop():
    d=.8; t=T(d); y=np.sin(2*np.pi*np.cumsum(np.linspace(330,160,len(t))*(1+.02*np.sin(2*np.pi*5*t)))/SR)*np.sin(np.pi*t/d)**.6*.6   # toy slide-whistle sigh
    put(y,grind(.3,(10,14),200,900)*.2,.45); put(y,thump(.2,110,60,16),.6,.4); return reverb(y,.6,.2)[:N(d)]
ev('sfx_wrecker_fidget','weapon',{f'sfx_wrecker_fidget_{v+1:02d}':(lambda v=v: fidget(v),-22) for v in range(2)},None,'idle fidget: wheel creak + gold glint ting','ANIM_WRECKER_IDLE_FIDGET')
ev('sfx_wrecker_aim_hum','weapon',{'sfx_wrecker_aim_hum_loop':(aim_hum,-26)},None,'servo hum loop while aiming (layer with aim_stretch)','ANIM_WRECKER_AIM_START..END',loop=True)
ev('sfx_wrecker_aim_tick','weapon',{f'sfx_wrecker_aim_tick_{v+1:02d}':(lambda v=v: aim_tick(v),-24) for v in range(3)},None,'ratchet tick per 5 deg (throttle 60 ms)','ANIM_WRECKER_AIM_TICK')
ev('sfx_wrecker_aim_clamp','weapon',{'sfx_wrecker_aim_clamp':(aim_clamp,-21)},None,'soft stop clunk at aim limit','ANIM_WRECKER_AIM_CLAMP')
ev('sfx_aim_stretch','weapon',{'sfx_aim_stretch_loop':(aim_stretch,-25)},None,'rubbery aim-drag creak loop (pitch follows power in code)','ANIM_WRECKER_AIM_START..END',loop=True)
ev('sfx_wrecker_smoke_tail','weapon',{f'sfx_wrecker_smoke_tail_{v+1:02d}':(lambda v=v: smoke_tail(v),-22) for v in range(2)},None,'air whoosh tail after muzzle','ANIM_WRECKER_MUZZLE_SMOKE')
ev('sfx_wrecker_recoil_land','weapon',{f'sfx_wrecker_recoil_land_{v+1:02d}':(lambda v=v: recoil_land(v),-20) for v in range(2)},None,'carriage thud + wheel roll','ANIM_WRECKER_RECOIL_LAND')
ev('sfx_wrecker_slot_in','weapon',{f'sfx_wrecker_slot_in_{v+1:02d}':(lambda v=v: slot_in(v),-20) for v in range(3)},None,'slot click-in + chime (01 Core C, 02 Module E, 03 Booster G)','ANIM_WRECKER_SLOT_IN')
ev('sfx_wrecker_slot_out','weapon',{'sfx_wrecker_slot_out':(slot_out,-22)},None,'slot pop-out','ANIM_WRECKER_SLOT_OUT')
ev('sfx_projectile_whoosh','weapon',{f'sfx_projectile_whoosh_loop_{v+1:02d}':(lambda v=v: whoosh_loop(v),-24) for v in range(2)},None,'soft round in-flight whoosh loop (stop on impact/despawn)','ANIM_PROJ_LAUNCH..projectile_despawn',loop=True)
ev('sfx_wrecker_win_hop','results',{'sfx_wrecker_win_hop':(win_hop,-19)},None,'Wrecker hop boings + nylon fifth','ANIM_WRECKER_WIN')
ev('sfx_wrecker_fail_droop','results',{'sfx_wrecker_fail_droop':(fail_droop,-20)},None,'Wrecker droop: toy slide-whistle sigh (non-vocal)','ANIM_WRECKER_FAIL')
# ---------- UI ----------
def panel_out(): return whoosh(.3,1400,400,.3)*.6+np.pad(pop(450,300,.08,.15),(0,N(.22)))[:N(.3)]
def score_tick(v): return pop([1300,1200][v],1000,.06,.2)
def mastery_tick(): y=fluid([(0,VIB,m('A5'),.3,90),(0,PIANO,m('A5'),.3,70)],.5,.35); return y
def claim_stamp(): d=.4; y=np.zeros(N(d)); put(y,thump(.3,150,60,16),0,.9); put(y,flt(noise(.05),'band',[200,2000])*env_exp(.05,60),0,.5); return reverb(y,.3,.12)[:N(d)]
def gate_open():
    d=1.2; y=np.zeros(N(d)); put(y,metal(700,.15,30,.4),0,.4); put(y,crack(.02,1500,6000,150),0,.35); put(y,metal(560,.12,30,.4),.06,.35)   # padlock click-clack
    put(y,C.creak('wood',1)[:N(.75)]*.7,.2); put(y,thump(.3,100,50,12),.95,.5); return reverb(y,.6,.2)[:N(d)]
def marker_hop(v):
    tt=T(.3); y=np.sin(2*np.pi*np.cumsum([300,340][v]+300*np.sin(np.pi*tt/.3))/SR)*env_exp(.3,10); put(y,thump(.1,150,90,30),0,.3); return np.pad(y,(0,N(.1)))
def shot_spend(): return fluid([(0,WOODBLK,m('A5'),.05,80)],.1,.05)
def threshold_hit(): return fluid([(0,NYLON,p,.6,95) for p in ()] + [(i*.06,NYLON,p,.6,95) for i,p in enumerate([m('G4'),m('C5'),m('E5'),m('G5')])]+[(.18,PIANO,m('C5'),.6,85),(.18,PIANO,m('E5'),.6,80)],.8,.5,.7)
def hint_button(): return fluid([(0,VIB,m('E5'),.4,85),(.1,VIB,m('B5'),.4,75)],.6,.45)
def hint_pulse():
    d=1.0; t=T(d); y=fluid([(0,VIB,m('G5')-12,.5,55)],d,.5,.7)*.6; y+=flt(noise(d),'band',[1500,6000])*(.5+.5*np.cos(2*np.pi*t))*.04   # 1 Hz gentle shimmer
    return loopbuf(y,.2)
def node_unlock():
    d=1.0; y=np.zeros(N(d)); put(y,pop(500,900,.12,.3),0,.6); put(y,fluid([(0,MARIMBA,m('C5'),.2,100),(.08,MARIMBA,m('G5'),.3,100)],.4,.3),0,.6)
    put(y,C.creak('wood',3)[:N(.6)]*.5,.3); return reverb(y,.5,.15)[:N(d)]
for k,(fs,lv,desc,tr) in {
    'sfx_ui_panel_out':([panel_out],-23,'panel swoosh down','UI_PANEL_OUT'),
    'sfx_ui_score_tick':([lambda: score_tick(0),lambda: score_tick(1)],-26,'score count-up tick (code: every 50 ms, max 12)','UI_SCORE_TICK'),
    'sfx_ui_claim_stamp':([claim_stamp],-20,'daily claim stamp thunk','UI_CLAIM'),
    'sfx_ui_gate_open':([gate_open],-20,'bridge gate: padlock click + wood creak + thud','UI_GATE_OPEN'),
    'sfx_ui_marker_hop':([lambda: marker_hop(0),lambda: marker_hop(1)],-22,'map marker hop boing','UI_MARKER_HOP'),
    'sfx_ui_shot_spend':([shot_spend],-24,'HUD shot pip spend soft tick','UI_SHOT_SPEND'),
    'sfx_hint_button':([hint_button],-22,'hint offered: soft vibraphone ding','UI_HINT_BUTTON_APPEAR'),
    'sfx_map_node_unlock':([node_unlock],-20,'map node unlock pop + marimba + bridge creak','UI_NODE_UNLOCK')}.items():
    ev(k,'ui',{(k if len(fs)==1 else f'{k}_{i+1:02d}'):(f,lv) for i,f in enumerate(fs)},None,desc,tr)
ev('sfx_hint_pulse','ui',{'sfx_hint_pulse_loop':(hint_pulse,-27)},None,'hint target pulse + ghost arc shimmer loop (1 Hz)','UI_HINT_SHOW..hide',loop=True)
ev('sfx_ui_mastery_tick','results',{'sfx_ui_mastery_tick':(mastery_tick,-20)},None,'mastery gold ping (vib + piano)','UI_MASTERY_TICK')
ev('sfx_ui_threshold_hit','results',{'sfx_ui_threshold_hit':(threshold_hit,-18)},None,'85% win threshold crossed: nylon run + piano third','UI_THRESHOLD_HIT')
# ---------- win / fail accents (layer under music stingers, low level) ----------
def win_accent():
    d=1.5; y=np.zeros(N(d)); put(y,thump(.15,220,120,30),0,.5); put(y,crack(.05,1200,6000,60),0,.35)        # confetti pop
    for k in range(14): put(y,crack(.02,2000,7000,150)*R.uniform(.1,.3),R.uniform(.02,.9))                  # paper flutter
    put(y,fluid([(.05,NYLON,p,1.0,90) for p in (m('C5'),m('E5'),m('G5'))]+[(.05,PIANO,m('C4'),1.1,80)],1.4,.5,.7),0,.7); return y
def fail_accent(v):
    d=1.2; t=T(d); f=np.linspace([300,280][v],120,len(t)); y=np.sin(2*np.pi*np.cumsum(f)/SR)*(1+.3*np.sin(2*np.pi*7*t))*np.exp(-t*1.5)*.5   # deflate "wah"
    y=flt(y,'band',[150,2500]); put(y,flt(noise(d),'band',[300,1500])*np.exp(-T(d)*2.5)*.12,0); return reverb(y,.6,.2)[:N(d)]
ev('sfx_win_stinger_sfx','results',{'sfx_win_stinger_sfx':(win_accent,-21)},None,'win accent: confetti pop + nylon C triad (layer under stinger_win, -6 dB vs music)','UI_WIN')
ev('sfx_fail_sfx','results',{f'sfx_fail_sfx_{v+1:02d}':(lambda v=v: fail_accent(v),-22) for v in range(2)},None,'cheeky deflate wah (layer under stinger_fail)','UI_FAIL')
# ---------- rewards ----------
def shard(): y=fluid([(0,VIB,m('B5'),.4,85),(.05,VIB,m('F#6')-12,.4,75)],.5,.45); put(y,bell(1900,.3,12)*.08,0); return y
def extra_charge(): y=fluid([(0,KALIMBA,m('A5'),.15,95),(.07,KALIMBA,m('E6')-12,.3,95)],.5,.35); put(y,whoosh(.3,300,1500,.85)*.15,0); return y
def tier_up(): y=fluid([(.08+i*.05,VIB,p,.35,85) for i,p in enumerate([m('C5'),m('E5'),m('G5')])],.5,.4); put(y,whoosh(.3,300,1600,.8)*.3,0); return y
def daily_claim(): return fluid([(i*.08,NYLON,p,1.0,95) for i,p in enumerate([m('C5'),m('E5'),m('G5'),m('D6')])]+[(0,PIANO,m('C4'),1.2,85),(.32,PIANO,m('G4'),1.0,75)],1.5,.55,.75)
for k,(f,lv,desc,tr) in {'sfx_reward_shard':(shard,-19,'cosmetic shard crystal chime (vib + soft bell)','UI_SHARD_GAIN'),
    'sfx_reward_extra_charge':(extra_charge,-19,'power-up charge granted: kalimba up + air','UI_EXTRA_CHARGE'),
    'sfx_reward_season_tier_up':(tier_up,-18,'season tier up whoosh-chime','UI_SEASON_TIER_UP'),
    'sfx_reward_daily_claim':(daily_claim,-17,'daily reward claim: nylon Cadd9 run + piano','UI_CLAIM')}.items():
    ev(k,'reward',{k:(f,lv)},None,desc,tr)
# ---------- destruction / boss ----------
def crate(v):
    d=1.2; y=np.zeros(N(d)); put(y,crack(.04,600,4000,80),0,.6); put(y,thump(1.0,[85,80,90][v],28,4),.02,1)          # wood pop + boom
    put(y,flt(noise(.3),'band',[150,2500])*env_exp(.3,12,.001),.02,.6); put(y,rumble(.9,140)*np.exp(-T(.9)*3),.03,.5)
    put(y,debris(.9,40,350,3000),.1,.4); put(y,fluid([(0,WOODBLK,m('C4')+[0,2,-2][v],.1,100)],.2,.1),0,.2); return drive(reverb(y,1.0,.25)[:N(d)],1.6)
def kill_fall(v):
    d=1.0; t=T(.65); y=np.zeros(N(d)); put(y,np.sin(2*np.pi*np.cumsum(np.linspace([1100,1000,1200][v],350,len(t)))/SR)*np.sin(np.pi*t/.65)*.4,0)   # descending whistle (<=1.2 kHz)
    put(y,flt(noise(.3),'low',1200)*env_exp(.3,10,.01),.65,.45); put(y,thump(.25,90,50,14),.65,.3)                                                  # distant poof
    return reverb(y,1.0,.35)[:N(d)]
def dmg_tick(v): y=pop([1000,1100][v],700,.07,.25); put(y,thump(.07,180,120,40),0,.3); return np.pad(y,(0,N(.05)))
def terrain(sz):
    S=C.SIZE[sz]; d=.3*S['d']+.05; y=np.zeros(N(d)); put(y,thump(d,85*S['p'],35*S['p'],16/S['d']),0,1)               # dull ground thump
    put(y,flt(noise(.06),'band',[100,1500])*env_exp(.06,50,.001),0,.45); put(y,debris(.2,25*S['d'],150,1800),.01,.2)  # dirt puff + grit
    return reverb(y,.4,.1)[:N(d)]
ev('sfx_explosive_crate','destruction',{f'sfx_explosive_crate_{v+1:02d}':(lambda v=v: crate(v),-14) for v in range(3)},None,'explosive crate: wood pop + cartoon boom + debris','EXPLODE_CRATE (alias EXPLODE_BARREL)')
ev('sfx_kill_plane_fall','destruction',{f'sfx_kill_plane_fall_{v+1:02d}':(lambda v=v: kill_fall(v),-22) for v in range(3)},None,'piece falls off island: descending whistle + distant poof','DEBRIS_FALL_OFF')
ev('sfx_impact_terrain','destruction',{f'sfx_terrain_impact_{sz}_{v+1:02d}':(lambda sz=sz: terrain(sz),C.SIZE[sz]['lv']) for sz in 'sml' for v in range(3)},None,'ball hits ground/rock: dull thump + dirt (ImpactAudioProfile Terrain, bank SFX/Destruction/Impacts, S/M/L x3)','PHYS ImpactEvent kind=Impact material=Terrain')
ev('sfx_titan_damage_tick','boss',{f'sfx_titan_damage_tick_{v+1:02d}':(lambda v=v: dmg_tick(v),-24) for v in range(2)},None,'Titan score/damage counter tick','UI_DAMAGE_TICK')
# ---------- master (identical chain to core_sfx.py) ----------
if __name__=='__main__':
    rep={}
    for eid,e in C.EV.items():
        od=f"{OUT}/{e['cat']}"; os.makedirs(od,exist_ok=True)
        for fn,(gen,lv) in e['files'].items():
            y=np.asarray(gen(),float)
            if e['loop']:
                n=len(y); y=flt(np.tile(y,3),'low',LP,4)[n:2*n]
                st=np.abs(np.diff(np.concatenate([y,y[:1]]))); y=np.roll(y,-(int(np.argmin(st[N(.05):-N(.05)]))+N(.05)+1))   # circular: start the loop at its smallest sample step
            else: y=flt(y,'low',LP,4); y[-N(.008):]*=np.linspace(1,0,N(.008))
            y-=np.mean(y); g=10**(lv/20)/max(np.sqrt(np.mean(y**2)),1e-9)
            for it in range(5):
                z=y*g; c=10**(-2.5/20)
                if np.abs(z).max()>c: z=np.where(np.abs(z)<c*.7,z,np.sign(z)*(c*.7+c*.3*np.tanh((np.abs(z)-c*.7)/(c*.3))))
                I=loud(z)
                if abs(I-lv)<.7: break
                g*=10**((lv-I)/20)
            for it in range(6):
                sf.write(f'/tmp/{fn}.wav',z.astype(np.float32),SR,subtype='FLOAT')
                subprocess.run(['ffmpeg','-y','-loglevel','error','-i',f'/tmp/{fn}.wav','-c:a','libvorbis','-q:a','5','-ac','1',f'{od}/{fn}.ogg'],check=True)
                P=meas(f'{od}/{fn}.ogg')[1]
                if P<=-1.1: break
                z=z*10**((-1.3-P)/20)
            r={'id':eid,'cat':e['cat'],'dur_s':round(len(z)/SR,3),'target_LUFS':lv,'LUFS':round(loud(z),1),'TP':P,'loop':e['loop']}
            if e['loop']: r['seam_jump']=round(float(abs(z[-1]-z[0])),5); r['p99_step']=round(float(np.percentile(np.abs(np.diff(z)),99)),5)
            rep[fn]=r
        print(eid,len(e['files']),flush=True)
    json.dump({'events':{k:{x:v[x] for x in ('cat','desc','trigger','loop')}|{'files':list(v['files'])} for k,v in C.EV.items()},'files':rep},open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'extra_sfx_report.json'),'w'),indent=1)
