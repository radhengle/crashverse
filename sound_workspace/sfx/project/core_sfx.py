#!/usr/bin/env python3
"""CRASHVERSE core SFX library v1: original, synthesised (numpy/scipy DSP) + tonal accents rendered from
FluidR3_GM piano/nylon/celesta-free palette via FluidSynth (MIT soundfont, SND-001). 'Toybox demolition': warm,
punchy, playful; low-passed at 9.5 kHz; mono 44.1 kHz; true peak <= -1 dBTP; loudness normalised per category/size.
Naming: ImpactAudioProfile -> sfx_<material>_<kind>_<size>_<nn>.ogg (kind impact|crack|break|jointsnap, size s|m|l).
Usage: python core_sfx.py    (writes ../<category>/*.ogg and core_sfx_report.json)"""
import sys,os,json,subprocess,re,tempfile,numpy as np,soundfile as sf,mido
sys.path.insert(0,'/workspace/crashverse/audio/sfx/boss/project')
import boss_sfx as L
from boss_sfx import N,T,flt,noise,env_exp,put,sweep_sine,reverb,drive,thump,crack,metal,rumble,debris,whoosh,grind,hiss,bell,crackle,loopify,meas
SR=44100; OUT='/workspace/crashverse/audio/sfx'; R=np.random.default_rng(777)
# ---------- tonal (sampled) helper ----------
def fluid(notes,dur,rev=.5,room=.6):
    """notes: [(t_s, program, midi, dur_s, vel)] -> mono"""
    mf=mido.MidiFile(ticks_per_beat=960); tr=mido.MidiTrack(); mf.tracks.append(tr); tr.append(mido.MetaMessage('set_tempo',tempo=500000))
    progs=sorted({p for _,p,_,_,_ in notes}); ch={p:i for i,p in enumerate(progs)}; ms=[(0,0,mido.Message('program_change',channel=ch[p],program=p)) for p in progs]
    for t,p,n,d,v in notes:
        ms.append((int(t*1920),1,mido.Message('note_on',channel=ch[p],note=n,velocity=v))); ms.append((int((t+d)*1920),0,mido.Message('note_off',channel=ch[p],note=n,velocity=0)))
    ms.sort(key=lambda x:(x[0],x[1])); now=0
    for t,_,m in ms: tr.append(m.copy(time=t-now)); now=t
    with tempfile.TemporaryDirectory() as td:
        mf.save(f'{td}/a.mid')
        subprocess.run(['fluidsynth','-ni','-q','-C','0','-R','1','-o',f'synth.reverb.room-size={room}','-o',f'synth.reverb.level={rev}','-g','0.6','-r','44100','-F',f'{td}/a.wav','/usr/share/sounds/sf2/FluidR3_GM.sf2',f'{td}/a.mid'],check=True,capture_output=True)
        y,_=sf.read(f'{td}/a.wav')
    y=y.mean(1) if y.ndim>1 else y; y=np.pad(y,(0,max(0,N(dur)-len(y))))[:N(dur)]; y[-N(.03):]*=np.linspace(1,0,N(.03)); return y
PIANO,NYLON,MARIMBA,VIB,KALIMBA,WOODBLK,STEELDR=0,24,12,11,108,115,114
def m(n): return {'C':0,'D':2,'E':4,'F':5,'G':7,'A':9,'B':11}[n[0]]+(1 if '#' in n else 0)+12*(int(n[-1])+1)
# ---------- material models ----------
MAT={  # body modes (Hz), mode decay, noise band, crack band, debris band, character
 'wood': dict(modes=[(310,1),(720,.55),(1180,.3)],k=28,nb=(180,2200),cb=(600,4500),db=(350,3000),thump=(140,60)),
 'brick':dict(modes=[(190,1),(430,.4)],k=40,nb=(120,2600),cb=(400,3800),db=(250,2600),thump=(110,45)),
 'steel':dict(modes=[(260,1),(717,.6),(1060,.45),(1404,.3),(1795,.2)],k=6,nb=(300,4000),cb=(900,6000),db=(600,5000),thump=(120,55)),
 'glass':dict(modes=[(1250,1),(2470,.5),(3720,.25)],k=14,nb=(900,6500),cb=(1500,7500),db=(1500,7000),thump=(160,90)),
 'stone':dict(modes=[(150,1),(340,.45)],k=45,nb=(90,2000),cb=(300,3200),db=(200,2200),thump=(95,38)),
}
SIZE={'s':dict(p=1.3,d=.7,lv=-23),'m':dict(p=1.0,d=1.0,lv=-19.5),'l':dict(p=.75,d=1.45,lv=-16.5)}
def modes(mat,d,pf,k):
    t=T(d); return sum(a*np.sin(2*np.pi*f*pf*R.uniform(.97,1.03)*t+R.uniform(0,6))*np.exp(-t*k*(1+.5*i)/max(pf,.5)) for i,(f,a) in enumerate(MAT[mat]['modes']))*np.minimum(t/.0008,1)
def mat_impact(mat,sz):
    M=MAT[mat]; S=SIZE[sz]; d=.45*S['d']; y=np.zeros(N(d))
    put(y,thump(min(d,.4),M['thump'][0]*S['p'],M['thump'][1]*S['p'],14/S['d']),0,.9)
    put(y,modes(mat,d,S['p'],M['k']),0,.55)
    put(y,flt(noise(.05),'band',M['nb'])*env_exp(.05,60,.0005),0,.4)
    return reverb(y,.5,.15)[:N(d)]
def mat_crack(mat,sz):
    M=MAT[mat]; S=SIZE[sz]; d=.35*S['d']; y=np.zeros(N(d))
    for j in range(3+int(2*S['d'])):   # splintering micro-cracks
        put(y,crack(.06,*M['cb'],k=55)*R.uniform(.4,1),j*R.uniform(.008,.025))
    put(y,modes(mat,d,S['p']*1.1,M['k']*1.3),0,.35); put(y,thump(.2,M['thump'][0]*S['p']*1.2,M['thump'][1],20),0,.4)
    return reverb(y,.45,.15)[:N(d)]
def mat_break(mat,sz):
    M=MAT[mat]; S=SIZE[sz]; d=.75*S['d']; y=np.zeros(N(d))
    put(y,mat_impact(mat,sz),0,.8); put(y,mat_crack(mat,sz),.005,.7)
    put(y,debris(d-.05,30*S['d']+(40 if mat=='glass' else 0),*M['db']),.02,.55)
    if mat=='glass':
        for _ in range(int(10*S['d'])): t0=R.uniform(.02,d*.6); put(y,bell(R.uniform(1400,3200),.25,14)*R.uniform(.1,.3),t0)
    return drive(reverb(y,.7,.2)[:N(d)],1.4)
def jointsnap(sz):
    S=SIZE[sz]; d=.6*S['d']; y=np.zeros(N(d)); t=T(d)
    put(y,crack(.04,1200,6000,80),0,.8)                                          # snap
    put(y,metal(300*S['p'],d,7),0,.5)                                            # clank
    tw=np.sin(2*np.pi*np.cumsum(180*S['p']*(1+.6*np.exp(-t*12))*(1+.03*np.sin(2*np.pi*9*t)))/SR)*np.exp(-t*7)
    put(y,tw,.01,.35); put(y,thump(.3,130*S['p'],50,14),0,.6)                    # spring twang + body
    return reverb(y,.6,.18)[:N(d)]
def creak(mat,v):
    d=[.6,.75,.55,.7][v]; y=grind(d,[(14,24),(18,30),(10,20),(16,26)][v],*( (200,1400) if mat=='wood' else (300,2200) ))
    if mat=='steel': t=T(d); y=y*.6+np.sin(2*np.pi*np.cumsum(np.linspace(220,150,len(t)))/SR)*.25*np.sin(np.pi*t/d)
    return reverb(y,.6,.2)[:N(d)]
def collapse(mat,sz):
    M=MAT[mat]; S=SIZE[sz]; d={'s':1.0,'m':1.8,'l':2.8}[sz]; y=np.zeros(N(d))
    nhit={'s':4,'m':8,'l':14}[sz]
    for j in range(nhit):
        t0=(j/nhit)**1.3*d*.6+R.uniform(0,.05); ss='s' if R.random()<.6 else 'm'
        put(y,mat_break(mat,ss if sz!='l' else ('m' if R.random()<.6 else 'l')),t0,R.uniform(.35,.7)*(1-.4*j/nhit))
    put(y,rumble(d,120)*np.exp(-T(d)*2.5),0,.6*S['d']); put(y,thump(1.0,70,28,4),0,.9)
    put(y,debris(d*.8,35*S['d'],*M['db']),d*.25,.45)
    y*=np.minimum((d-T(d))/.25,1)
    return drive(reverb(y,1.1,.25)[:N(d)],1.3)
def debris_rattle(mat,v):
    M=MAT[mat]; d=[.25,.3,.22,.35][v]; y=debris(d,140,*M['db'])
    put(y,modes(mat,.15,1.4,M['k']*2),0,.15); return y
# ---------- registry ----------
EV={}
def ev(eid,cat,files,lv,desc,trigger,loop=False):
    """files: dict filename -> generator(); lv = target loudness (LUFS, measured on 0.5 s-padded file)"""
    EV[eid]=dict(cat=cat,files=files,lv=lv,desc=desc,trigger=trigger,loop=loop)
for mat in MAT:
    for kind,fn in [('impact',mat_impact),('crack',mat_crack),('break',mat_break)]:
        fs={}
        for sz in 'sml':
            for v in range(3): fs[f'sfx_{mat}_{kind}_{sz}_{v+1:02d}']=(lambda mat=mat,sz=sz,fn=fn: fn(mat,sz),SIZE[sz]['lv']+(1 if kind=='break' else -1 if kind=='crack' else 0))
        ev(f'sfx_{mat}_{kind}','destruction',fs,None,f'{mat} {kind} (ImpactAudioProfile kind={kind.capitalize()}; sizes S/M/L x3 var)','PHYS ImpactEvent kind=%s material=%s'%(kind.capitalize(),mat))
    ev(f'sfx_{mat}_collapse','destruction',{f'sfx_{mat}_collapse_{sz}_{v+1:02d}':(lambda mat=mat,sz=sz: collapse(mat,sz),{'s':-18,'m':-16.5,'l':-15}[sz]) for sz in 'sml' for v in range(2)},None,
       f'{mat} layered collapse S/M/L x2 var (one per cascade)','CollapseStart/COLLAPSE_LAND material=%s, size by cascadeCount'%mat)
    ev(f'sfx_{mat}_debris','destruction',{f'sfx_{mat}_debris_{v+1:02d}':(lambda mat=mat,v=v: debris_rattle(mat,v),-23) for v in range(4)},None,f'{mat} debris rattle (4 var)','Debris ImpactEvents combined every 100 ms')
ev('sfx_steel_joint_snap','destruction',{f'sfx_steel_jointsnap_{sz}_{v+1:02d}':(lambda sz=sz: jointsnap(sz),SIZE[sz]['lv']+1) for sz in 'sml' for v in range(3)},None,'steel joint snap: snap + clank + spring twang (S/M/L x3)','PHYS ImpactEvent kind=JointSnap')
for mat in ('wood','steel'):
    ev(f'sfx_creak_{mat}','destruction',{f'sfx_creak_{mat}_{v+1:02d}':(lambda mat=mat,v=v: creak(mat,v),-22) for v in range(4)},None,f'{mat} stress creak (4 var)','structure stress (sfx_collapse_creak)')
# ---------- weapon ----------
def cannon_fire(v):
    d=.8; y=np.zeros(N(d)); put(y,thump(.6,[95,100,90,105][v],32,6),0,1.0); put(y,flt(noise(.12),'band',[200,3000])*env_exp(.12,30,.0005),0,.8)
    put(y,whoosh(.5,800,250,.1),.01,.35); put(y,flt(noise(.6),'low',600)*env_exp(.6,6,.01),.02,.4)              # pop + air + smoke boom
    put(y,fluid([(0,WOODBLK,m('C5')+[0,2,-1,3][v],.1,90)],.3,.1,.2),0,.25)                                      # toy "pok"
    return drive(reverb(y,.9,.2)[:N(d)],1.6)
def breech(v):
    d=.18; y=np.zeros(N(d)); put(y,metal([420,450][v],.15,25,.5),0,.6); put(y,crack(.03,1500,6000,90),0,.5); put(y,thump(.1,200,100,30),0,.5); return y
def reload_loop():
    d=1.0; y=np.zeros(N(d))
    for k in range(4): t0=k*.25; put(y,metal(330+20*(k%2),.12,30,.4),t0,.45); put(y,crack(.02,1000,5000,120),t0,.35)   # ratchet clicks
    y+=grind(d,(10,14),300,1600)*.12; return loopify(np.concatenate([y,y[:N(.12)]]),.12)
def ready(v):
    d=.25; y=np.zeros(N(d)); put(y,metal(520,.15,22,.4),0,.5); put(y,fluid([(0,MARIMBA,m(['G5','A5'][v]),.15,100)],.25,.15),0,.6); return y
ev('sfx_cannon_fire','weapon',{f'sfx_cannon_fire_{v+1:02d}':(lambda v=v: cannon_fire(v),-14) for v in range(4)},None,'cannon pop: sub boom + toy pok + air + smoke','FIRE (+66 ms after ANTICIPATE)')
ev('sfx_wrecker_breech','weapon',{f'sfx_wrecker_breech_{v+1:02d}':(lambda v=v: breech(v),-18) for v in range(2)},None,'breech clack','ANIM_WRECKER_FIRE_ANTICIPATE (FIRE tap)')
ev('sfx_cannon_reload','weapon',{'sfx_cannon_reload_loop':(reload_loop,-22)},None,'reload ratchet loop (1.0 s), plays from impact until FIRE unlocks','impact -> until FIRE unlock',loop=True)
ev('sfx_cannon_ready','weapon',{f'sfx_cannon_ready_{v+1:02d}':(lambda v=v: ready(v),-19) for v in range(2)},None,'ready click + marimba blip','ANIM_WRECKER_READY (FIRE unlock)')
# ---------- power-ups ----------
def arm(root,prog=VIB):  # rising 3-note arpeggio + shimmer, warm
    d=.5; y=fluid([(i*.07,prog,root+o,.3,95) for i,o in enumerate((0,7,12))],d,.35); put(y,whoosh(.4,300,1500,.85)*.15,0); return y
def pu_fx(name):
    if name=='shockwave':
        d=1.5; y=np.zeros(N(d)); put(y,thump(1.2,80,26,3),0,1); put(y,whoosh(1.2,1200,200,.08),0,.5); put(y,rumble(1.2,150)*np.exp(-T(1.2)*3),0,.5); put(y,debris(1.2,25),.1,.25)
        return drive(reverb(y,1.2,.25)[:N(d)],1.4)
    if name=='splitstrike':
        d=1.0; y=np.zeros(N(d)); put(y,whoosh(.35,600,2200,.6),0,.5); put(y,fluid([(.2,MARIMBA,m('E5'),.2,100),(.26,MARIMBA,m('A5'),.2,100),(.32,MARIMBA,m('C6'),.2,100)],.8,.3),0,.7); put(y,crack(.03,1000,5000,90),.2,.4)
        return reverb(y,.8,.2)[:N(d)]
    if name=='gravity_well':
        d=1.5; t=T(d); y=np.sin(2*np.pi*np.cumsum(55+20*np.sin(2*np.pi*(4/1.5)*t))/SR)*.7+flt(noise(d),'band',[100,700])*(.4+.3*np.sin(2*np.pi*(2/1.5)*t))
        return loopify(np.concatenate([y,y[:N(.12)]]),.12)
    if name=='chain_reactor':
        d=1.4; y=np.zeros(N(d))
        for k in range(6): t0=k*.18; put(y,crackle(.12,300)*.8,t0); put(y,thump(.25,150-10*k,60,12),t0,.6)
        put(y,flt(noise(d),'band',[300,2500])*np.exp(-T(d)*2)*.15,0); return drive(reverb(y,.9,.2)[:N(d)],1.5)
    if name=='phase_piercer':
        d=1.0; t=T(d); src=flt(noise(d),'band',[300,4000]); dl=(N(.003)*(1+np.sin(2*np.pi*1.5*t))).astype(int)   # flanger zing
        y=src+np.array([src[i-dl[i]] if i-dl[i]>=0 else 0 for i in range(len(src))]); y*=np.exp(-t*3)*np.minimum(t/.02,1)
        put(y,np.sin(2*np.pi*np.cumsum(np.linspace(300,900,N(.6)))/SR)*np.exp(-T(.6)*4)*.5,0); return reverb(y,.9,.25)[:N(d)]
    if name=='time_fracture':
        d=2.0; y=np.zeros(N(d))
        for k in range(4): put(y,fluid([(0,WOODBLK,m('G4') if k%2 else m('C5'),.1,80)],.4,.2),k*.5,.6)   # slowed tick-tock
        t=T(d); y+=np.sin(2*np.pi*110*t)*.15*(1+.5*np.sin(2*np.pi*.5*t)); return loopify(np.concatenate([y,y[:N(.12)]]),.12)
def time_end():
    d=.9; y=np.zeros(N(d)); put(y,crack(.3,1000,7000,15),0,.6); put(y,debris(.8,120,1500,7000),0,.5); put(y,fluid([(0,PIANO,m('C6'),.3,70),(0,PIANO,m('G5'),.3,70)],.9,.4),0,.4)
    return reverb(y,.8,.25)[:N(d)]
PU={'shockwave':m('C5'),'splitstrike':m('D5'),'gravity_well':m('A4'),'chain_reactor':m('E5'),'phase_piercer':m('F5'),'time_fracture':m('G4')}
for p,root in PU.items():
    ev(f'sfx_pu_{p}_arm','powerup',{f'sfx_pu_{p}_arm':(lambda root=root: arm(root),-18)},None,f'{p} armed: rising vibraphone arpeggio','PowerUpRuntime arm')
    loop=p in ('gravity_well','time_fracture')
    ev(f'sfx_pu_{p}_fx','powerup',{f'sfx_pu_{p}_fx'+('_loop' if loop else ''):(lambda p=p: pu_fx(p),-17 if not loop else -20)},None,f'{p} effect'+(' loop' if loop else ''),'PowerUpRuntime effect',loop=loop)
ev('sfx_pu_time_fracture_end','powerup',{'sfx_pu_time_fracture_end':(time_end,-17)},None,'time fracture shatter out','Time Fracture ends')
ev('sfx_pu_disarm','powerup',{'sfx_pu_disarm':(lambda: fluid([(0,VIB,m('G5'),.1,80),(.06,VIB,m('C5'),.15,80)],.3,.25),-21)},None,'disarm: falling 2-note','PU_DISARM')
ev('sfx_pu_chain_link','powerup',{f'sfx_pu_chain_link_{v+1:02d}':(lambda v=v: np.concatenate([crackle(.08,400)*.6,np.zeros(N(.02))])+np.pad(fluid([(0,MARIMBA,m('C5')+[0,4,7][v],.15,90)],.1,.1),(0,0))[:N(.1)],-19) for v in range(3)},None,'chain link zap (pitch up per link)','chain reactor link')
ev('sfx_pu_phase_hit','powerup',{f'sfx_pu_phase_hit_{v+1:02d}':(lambda v=v: reverb(np.sin(2*np.pi*np.cumsum(np.linspace(700+100*v,300,N(.3)))/SR)*np.exp(-T(.3)*8)+crack(.3,800,4000,30)*.3,.4,.2)[:N(.4)],-18) for v in range(2)},None,'phase piercer passes through a block','phase hit')
ev('sfx_pu_time_warn','powerup',{'sfx_pu_time_warn':(lambda: fluid([(0,WOODBLK,m('C5'),.08,100),(.25,WOODBLK,m('C5'),.08,100)],.5,.15),-19)},None,'time fracture ending warning','Time Fracture 1 s left')
# ---------- combo / stars ----------
C_ARP=[m(n) for n in ('C5','E5','G5','C6','E6')]
def combo(tier):
    notes=[(i*.06,NYLON,p,.6,90+5*tier) for i,p in enumerate(C_ARP[:tier+1])]+[(0,PIANO,m('C4'),.6,80),(0,PIANO,m('G4'),.6,70)]
    if tier>=3: notes+=[(.25,PIANO,m('E5'),.6,85),(.25,PIANO,m('G5'),.6,85)]
    d=.8+.2*(tier-1); y=fluid(notes,d,.55,.7); put(y,thump(.3,140,80,16),0,.3*tier/4); return y
def step(): return fluid([(0,MARIMBA,m('C5'),.1,100)],.12,.1)
for t_ in range(1,5): ev(f'sfx_combo_tier{t_}','combo',{f'sfx_combo_tier{t_}':(lambda t_=t_: combo(t_),-19+t_)},None,f'combo tier {t_}: nylon arpeggio + piano (bigger per tier)','ComboTracker tier up')
ev('sfx_combo_step','combo',{'sfx_combo_step':(step,-20)},None,'combo step blip (pitch-shift +1 semitone/step in code, wrap +1 oct, cap x20)','combo +1')
def star(k):
    p=[m('E5'),m('G5'),m('C6')][k]; y=fluid([(0,PIANO,p,.5,95),(0,NYLON,p-12,.5,85),(.03,VIB,p,.4,70)],.7,.6,.75); put(y,thump(.15,200,120,25),0,.25); return y
for k in range(3): ev(f'sfx_star_{k+1}','results',{f'sfx_star_{k+1}':(lambda k=k: star(k),-18+k)},None,f'star {k+1} pop (rising pitch)','results star reveal')
# ---------- UI ----------
def pop(f0,f1,d=.08,bright=.3):
    t=T(d); y=np.sin(2*np.pi*np.cumsum(f1+(f0-f1)*np.exp(-t*60))/SR)*np.exp(-t*45)
    return y+flt(noise(d),'band',[800,4000])*np.exp(-t*200)*bright
UI={'sfx_ui_click':[lambda: pop(900,500),lambda: pop(950,520),lambda: pop(850,480)],
    'sfx_ui_confirm':[lambda: fluid([(0,MARIMBA,m('C5'),.15,100),(.07,MARIMBA,m('G5'),.25,100)],.35,.2)],
    'sfx_ui_back':[lambda: fluid([(0,MARIMBA,m('G4'),.12,90),(.06,MARIMBA,m('D4'),.2,90)],.3,.15)],
    'sfx_ui_error':[lambda: fluid([(0,MARIMBA,m('D4'),.12,95),(.12,MARIMBA,m('C#4'),.2,95)],.35,.1)+pop(200,150,.35,0)*.3],
    'sfx_ui_tab':[lambda: pop(700,450,.07,.2)],
    'sfx_ui_toggle':[lambda: np.concatenate([pop(1100,700,.05,.25),pop(800,600,.05,.2)])],
    'sfx_ui_charge_spend':[lambda: fluid([(0,KALIMBA,m('A5'),.08,100)],.1,.05)],
    'sfx_ui_modal_open':[lambda: whoosh(.3,400,1400,.7)*.6+np.pad(pop(600,400,.1,.2),(0,N(.2)))[:N(.3)]]}
for k,fs in UI.items():
    ev(k,'ui',{(k if len(fs)==1 else f'{k}_{i+1:02d}'):(f,-22) for i,f in enumerate(fs)},None,k.replace('sfx_ui_','UI ')+' (toy pop / marimba)','UI_BTN_PRESS/UI events')
# ---------- rewards ----------
def coin(v):
    p=[m('E6'),m('G6'),m('D6')][v]-12; y=fluid([(0,VIB,p,.3,90),(.04,VIB,p+5,.35,80)],.5,.4); put(y,metal(1800,.15,30,.2)*.15,0); return y
def coin_burst(v):
    d=1.5; y=np.zeros(N(d))
    for k in range(10): put(y,coin(k%3)[:N(.4)],k*.1+R.uniform(0,.03),.6)
    return y
def chest_shake():
    d=.5; y=np.zeros(N(d))
    for k in range(4): put(y,flt(noise(.06),'band',[200,1500])*env_exp(.06,40,.002),k*.1,.8); put(y,metal(300,.1,30,.3)*.3,k*.1)
    return y
def chest_open():
    d=1.5; y=np.zeros(N(d)); put(y,grind(.3,(30,50),200,1500)*.4,0); put(y,thump(.4,120,60,10),.28,.6)
    put(y,fluid([(.3+i*.07,NYLON,p,1.0,95) for i,p in enumerate([m('C5'),m('E5'),m('G5'),m('B5'),m('D6')])]+[(.3,PIANO,m('C4'),1.1,80),(.3,PIANO,m('E4'),1.1,70)],1.2,.6,.8),.0,.8)
    return y
ev('sfx_reward_coin_single','reward',{f'sfx_reward_coin_single_{v+1:02d}':(lambda v=v: coin(v),-20) for v in range(3)},None,'coin: warm vibraphone ding','coin +1')
ev('sfx_reward_coin_burst','reward',{f'sfx_reward_coin_burst_{v+1:02d}':(lambda v=v: coin_burst(v),-17) for v in range(3)},None,'coin burst (10 coins)','coin burst')
ev('sfx_reward_chest_shake','reward',{'sfx_reward_chest_shake':(chest_shake,-19)},None,'chest wobble rattle','chest shake')
ev('sfx_reward_chest_open','reward',{'sfx_reward_chest_open':(chest_open,-16)},None,'chest open: creak + thump + Cmaj9 nylon bloom','chest open')
# ---------- master ----------
LP=9500
def loud(y):
    p=np.pad(y,(0,max(0,N(.5)-len(y)))); sf.write('/tmp/_l.wav',p.astype(np.float32),SR,subtype='FLOAT'); return meas('/tmp/_l.wav')[0]
if __name__=='__main__':
    rep={}
    for eid,e in EV.items():
        od=f"{OUT}/{e['cat']}"; os.makedirs(od,exist_ok=True)
        for fn,(gen,lv) in e['files'].items():
            y=gen(); y=np.asarray(y,float)
            if e['loop']: n=len(y); y=flt(np.tile(y,3),'low',LP,4)[n:2*n]
            else: y=flt(y,'low',LP,4); y[-N(.008):]*=np.linspace(1,0,N(.008))
            y-=np.mean(y); g=10**(lv/20)/max(np.sqrt(np.mean(y**2)),1e-9)
            for it in range(5):
                z=y*g; pk=np.abs(z).max()
                c=10**(-2.5/20)
                if pk>c: z=np.where(np.abs(z)<c*.7,z,np.sign(z)*(c*.7+c*.3*np.tanh((np.abs(z)-c*.7)/(c*.3))))   # soft knee limiter, ceiling -2.5 dBFS
                I=loud(z)
                if abs(I-lv)<.7: break
                g*=10**((lv-I)/20)
            for it in range(6):   # encode, verify true peak on the OGG, back off if needed
                sf.write(f'/tmp/{fn}.wav',z.astype(np.float32),SR,subtype='FLOAT')
                subprocess.run(['ffmpeg','-y','-loglevel','error','-i',f'/tmp/{fn}.wav','-c:a','libvorbis','-q:a','5','-ac','1',f'{od}/{fn}.ogg'],check=True)
                P=meas(f'{od}/{fn}.ogg')[1]
                if P<=-1.1: break
                z=z*10**((-1.3-P)/20)
            r={'id':eid,'cat':e['cat'],'dur_s':round(len(z)/SR,3),'target_LUFS':lv,'LUFS':round(loud(z),1),'TP':P,'loop':e['loop']}
            if e['loop']: r['seam_jump']=round(float(abs(z[-1]-z[0])),5); r['p99_step']=round(float(np.percentile(np.abs(np.diff(z)),99)),5)
            rep[fn]=r
        print(eid,len(e['files']),flush=True)
    json.dump({'events':{k:{x:v[x] for x in ('cat','desc','trigger','loop')}|{'files':list(v['files'])} for k,v in EV.items()},'files':rep},open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'core_sfx_report.json'),'w'),indent=1)
