#!/usr/bin/env python3
"""60 s SFX showcase (listening only, NOT for the APK): a mini level per material, then power-ups, combo, UI, rewards."""
import sys,subprocess,numpy as np,soundfile as sf
sys.path.insert(0,'/workspace/crashverse/audio/music/project'); from master import meas,lim
SR=44100; S='/workspace/crashverse/audio/sfx'; M='/workspace/crashverse/audio/music'
def ld(p):
    d=subprocess.run(['ffmpeg','-loglevel','error','-i',p,'-f','f32le','-ac','2','-ar','44100','-'],capture_output=True).stdout; return np.frombuffer(d,np.float32).reshape(-1,2).astype(float)
n=60*SR; y=np.zeros((n,2)); t=0.3
def at(f,tt,g=1.0,pan=0.0):
    x=ld(f'{S}/{f}.ogg' if not f.endswith('.ogg') else f); s=int(tt*SR); e=min(n,s+len(x))
    y[s:e,0]+=x[:e-s,0]*g*np.sqrt((1-pan)/2)*1.41; y[s:e,1]+=x[:e-s,1]*g*np.sqrt((1+pan)/2)*1.41
for i,mat in enumerate(['wood','brick','steel','glass','stone']):   # 9 s per material
    b=.3+i*8.6
    at('weapon/sfx_wrecker_breech_01',b); at(f'weapon/sfx_cannon_fire_0{1+i%4}',b+.066)
    at(f'destruction/sfx_{mat}_impact_m_01',b+.8,1,.2); at(f'destruction/sfx_{mat}_crack_m_02',b+1.3,1,-.3)
    at(f'destruction/sfx_{mat}_break_l_01',b+1.9,1,.1); at(f'destruction/sfx_{mat}_break_s_02',b+2.15,.8,-.4); at(f'destruction/sfx_{mat}_break_m_03',b+2.35,.9,.4)
    if mat in('wood','steel'): at(f'destruction/sfx_creak_{mat}_0{1+i%4}',b+2.9,1,-.2)
    if mat=='steel': at('destruction/sfx_steel_jointsnap_m_01',b+3.5)
    at(f'destruction/sfx_{mat}_collapse_l_0{1+i%2}',b+4.0); [at(f'destruction/sfx_{mat}_debris_0{k+1}',b+4.9+k*.3,1,(-1)**k*.5) for k in range(4)]
    at('weapon/sfx_cannon_reload_loop',b+2.0,.8); at('weapon/sfx_cannon_ready_01',b+7.0)
b=43.5
for k,p in enumerate(['shockwave','splitstrike','gravity_well','chain_reactor','phase_piercer','time_fracture']):
    at(f'powerup/sfx_pu_{p}_arm',b+k*1.15); 
at('powerup/sfx_pu_shockwave_fx',b+.5,.8); at('powerup/sfx_pu_chain_reactor_fx',b+3.9,.8); at('powerup/sfx_pu_time_fracture_end',b+6.2,.8)
b=50.7
for k in range(1,5): at(f'combo/sfx_combo_tier{k}',b+(k-1)*.7)
for k,u in enumerate(['sfx_ui_click_01','sfx_ui_tab','sfx_ui_toggle','sfx_ui_confirm','sfx_ui_back','sfx_ui_error','sfx_ui_charge_spend']): at(f'ui/{u}',b+3.4+k*.28)
b=55.6
for k in range(3): at(f'results/sfx_star_{k+1}',b+k*.45)
at('reward/sfx_reward_coin_burst_01',b+1.5); at('reward/sfx_reward_chest_shake',b+2.6); at('reward/sfx_reward_chest_open',b+3.0)
g=1.0
for it in range(6):
    z=lim(y*g); sf.write('/tmp/_sc.wav',z.astype(np.float32),SR,subtype='FLOAT'); I,P=meas('/tmp/_sc.wav')
    if abs(I+16)<.4 and P<=-1: break
    g*=10**((-16-I)/20)
z[-SR//2:]*=np.linspace(1,0,SR//2)[:,None]; sf.write('/tmp/_sc.wav',z.astype(np.float32),SR,subtype='FLOAT')
subprocess.run(['ffmpeg','-y','-loglevel','error','-i','/tmp/_sc.wav','-c:a','libvorbis','-q:a','5','/workspace/crashverse/audio/demos/sfx_showcase.ogg'],check=True)
print('showcase',meas('/workspace/crashverse/audio/demos/sfx_showcase.ogg'))
