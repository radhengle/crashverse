#!/usr/bin/env python3
"""Boss fight demo mixes (listening only, NOT for the APK): v3 music + boss SFX placed on an animation timeline.
Music ducks ~5 dB under SFX (sidechain envelope 20 ms attack / 300 ms release). Master -16 LUFS / <= -1 dBTP."""
import sys,subprocess,json,numpy as np,soundfile as sf
sys.path.insert(0,'/workspace/crashverse/audio/music/project')
from master import meas,lim
SR=44100; MU='/workspace/crashverse/audio/music'; SX='/workspace/crashverse/audio/sfx/boss'
def load(p):
    d=subprocess.run(['ffmpeg','-loglevel','error','-i',p,'-f','f32le','-ac','2','-ar','44100','-'],capture_output=True).stdout
    return np.frombuffer(d,np.float32).reshape(-1,2).astype(np.float64)
def put(buf,x,t,g=1.0):
    s=int(t*SR); e=min(len(buf),s+len(x)); buf[s:e]+=x[:e-s]*g
def loop(x,start,n,ls=0):
    out=x[:n].copy() if len(x)>=n else None
    if out is None:
        out=np.concatenate([x]+[x[ls:]]*int(np.ceil(n/len(x[ls:]))))[:n]
    return out
def fade(n,a,b,up=False):  # gain curve 1->0 between sample a and b
    g=np.ones(n); g[a:b]=np.linspace(1,0,b-a); g[b:]=0
    return 1-g if up else g
def mix(name,dur,music,sfx,events):
    n=int(dur*SR); M=np.zeros((n,2)); S=np.zeros((n,2))
    for f,t,g,ls,t_end in music:
        x=load(f'{MU}/{f}'); L=n-int(t*SR); y=loop(x,0,L,ls)*g
        if t_end: e=int((t_end-t)*SR); y[e:e+int(.5*SR)]*=np.linspace(1,0,min(int(.5*SR),len(y)-e))[:,None]; y[e+int(.5*SR):]=0
        put(M,y,t)
    for f,t,g in sfx: put(S,load(f'{SX}/{f}.ogg'),t,g)
    env=np.abs(S).max(1); a=np.exp(-1/(.02*SR)); r=np.exp(-1/(.3*SR)); sm=np.zeros(n); v=0
    for i in range(n): x=env[i]; v=a*v+(1-a)*x if x>v else r*v+(1-r)*x; sm[i]=v
    duck=1-.45*np.clip(sm/.3,0,1)
    y=M*.7*duck[:,None]+S*.85; g=1.0
    for it in range(6):
        z=lim(y*g); sf.write('/tmp/_d.wav',z.astype(np.float32),SR,subtype='FLOAT'); I,P=meas('/tmp/_d.wav')
        if abs(I+16)<.3 and P<=-1: break
        g*=10**((-16-I)/20)
    out=f'/workspace/crashverse/audio/demos/{name}.ogg'
    subprocess.run(['ffmpeg','-y','-loglevel','error','-i','/tmp/_d.wav','-c:a','libvorbis','-q:a','5',out],check=True)
    I,P=meas(out); json.dump({'dur_s':dur,'LUFS':I,'TP':P,'timeline':events},open(f'{SX}/project/{name}_timeline.json','w'),indent=1)
    print(name,dur,I,P)
# ---------------- Tidewarden ----------------
tw_sfx=[('sfx_boss_tw_intro',0,1),('sfx_boss_tw_arms_rise',1.0,.8),('sfx_boss_tw_roar_01',3.0,.9),
 ('sfx_boss_tw_joint_hit_01',8.0,1),('sfx_boss_tw_hit_armor_01',9.5,1),('sfx_boss_tw_joint_hit_02',11.0,1),('sfx_boss_tw_roar_02',11.3,.7),
 ('sfx_boss_tw_joint_hit_03',14.0,1),('sfx_boss_tw_shoulder_break_01',15.0,1),('sfx_boss_tw_roar_03',15.2,.8),
 ('sfx_boss_tw_shutter_tell_01',18.0,1),('sfx_boss_tw_shutter_close_01',18.8,1),('sfx_boss_tw_shutter_open_01',21.0,.9),
 ('sfx_boss_tw_shoulder_break_02',22.5,1),
 ('sfx_boss_tw_arms_drop',23.5,1),('sfx_boss_tw_arms_land',24.1,1),('sfx_boss_tw_chest_open',24.2,.9),('sfx_boss_tw_phase2',24.75,1),
 ('sfx_boss_tw_core_hit_01',28.0,1),('sfx_boss_tw_roar_01',28.1,.7),('sfx_boss_tw_shutter_tell_02',31.0,1),('sfx_boss_tw_shutter_close_02',31.8,1),
 ('sfx_boss_tw_shutter_open_02',34.0,.9),('sfx_boss_tw_core_hit_02',36.0,1),('sfx_boss_tw_core_hit_03',39.0,1),('sfx_boss_tw_roar_02',39.1,.8),
 ('sfx_boss_tw_core_hit_01',42.0,1),('sfx_boss_tw_defeat',43.0,1)]
hum=[('sfx_boss_tw_core_hum',24.75+k*1.5,.35) for k in range(12)]   # core hum loop while P2 is active (24.75 - 42.75 s)
tw_mus=[('tidewarden_p1.ogg',0,1,294000,23.5),('stinger_boss_phase2.ogg',23.5,1,0,None),('tidewarden_p2.ogg',24.75,1,0,43.0),('stinger_tidewarden_defeat.ogg',44.0,1,0,None)]
mix('tidewarden_fight_demo',50.5,tw_mus,tw_sfx+hum,{'music':tw_mus,'sfx':tw_sfx,'notes':'P1 intro (BOSS_TIDE_INTRO 0 s) -> joint hits -> tell 2.4 Hz + shutter -> phase change: ARMS_DROP 23.5, ARMS_LAND 24.1 (+600 ms), CHEST_OPEN 24.2 (+700 ms), PHASE2 24.75 (+1250 ms) = stinger hit -> P2 + core hum -> core hits -> DEFEAT 43.0 (4.0 s) -> defeat stinger 44.0'})
# ---------------- Titan ----------------
hits=[(t,f'sfx_titan_hit_0{1+i%3}') for i,t in enumerate([6,8.5,11,16,18.5,21,28,30.5,33,40,42.5,45])]
ti_sfx=[('sfx_titan_ranked_start',0,1),('sfx_titan_intro',0.9,1),('sfx_titan_roar_01',3.4,.9)]+[(f,t,1) for t,f in hits]+[
 ('sfx_titan_stage_25',13,1),('sfx_titan_roar_02',13.1,.8),('sfx_titan_stage_50',25,1),('sfx_titan_roar_03',25.1,.85),
 ('sfx_titan_stage_75',37,1),('sfx_titan_roar_01',37.1,.9),('sfx_titan_kill',47,1)]
rule=[('sfx_titan_rule_heated',3.4+k*2.0,.3) for k in range(22)]
# intensity layers crossfade (1 s) at the stage events, all sample-locked to the arena mix that starts at 3.4 s
ti_mus=[('titan/titan_obsidian_forge.ogg',3.4,1,0,47.0)]
mix_layers=[('titan/titan_int25.ogg',13,25),('titan/titan_int50.ogg',25,37),('titan/titan_int75.ogg',37,47)]
def titan_demo():
    dur=54.0; n=int(dur*SR); import numpy as _n
    # build music bed manually for crossfades
    base=load(f'{MU}/titan/titan_obsidian_forge.ogg'); M=np.zeros((n,2)); t0=int(3.4*SR)
    def tiled(x,L): return np.concatenate([x]*int(np.ceil(L/len(x))))[:L]
    M[t0:]+=tiled(base,n-t0)
    F=SR
    for f,a,b in mix_layers:
        x=tiled(load(f'{MU}/{f}'),n-t0); g=np.zeros(n-t0); s=int(a*SR)-t0; e=int(b*SR)-t0
        th=np.linspace(0,np.pi/2,F); g[s:s+F]=np.sin(th); g[s+F:e]=1
        if b<47: g[e:e+F]=np.cos(th)
        else: g[e:]=1
        M[t0:]+=x*g[:,None]
    e=int(47*SR); M[e:e+int(.5*SR)]*=np.linspace(1,0,int(.5*SR))[:,None]; M[e+int(.5*SR):]=0
    put(M,load(f'{MU}/stinger_titan_defeat.ogg'),47.8)
    sf.write(f'{MU}/project/build/_titan_bed.wav',M.astype(np.float32),SR,subtype='FLOAT')
    subprocess.run(['ffmpeg','-y','-loglevel','error','-i',f'{MU}/project/build/_titan_bed.wav','-c:a','libvorbis','-q:a','8','/tmp/_titan_bed.ogg'],check=True)
    import shutil; shutil.copy('/tmp/_titan_bed.ogg',f'{MU}/project/build/_titan_bed.ogg')
titan_demo()
import os; os.makedirs(f'{MU}/project/build',exist_ok=True)
mix('titan_fight_demo',54.0,[('project/build/_titan_bed.ogg',0,1,0,None)],ti_sfx+rule,
    {'music':'titan_obsidian_forge from 3.4 s; layers int25 @13 s, int50 @25 s, int75 @37 s (1 s equal-power crossfades, one layer at a time); out at 47 s; stinger_titan_defeat 47.8 s','sfx':ti_sfx,
     'notes':'RANKED_START 0 -> INTRO 0.9 (anvils 1.9/2.4/2.9) -> RULE loop (heated) -> HIT x12 -> STAGE 25/50/75% at 13/25/37 s (+roar) -> DEFEAT 47 s'})
