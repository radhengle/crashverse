#!/usr/bin/env python3
"""CRASHVERSE boss action SFX (original, synthesised; no samples). Tidewarden + Emberforge Titan.
Layered impact + body + tail, with reverb, saturation, pitch drops and debris tails. Mono 44.1 kHz.
Every file: low-passed at 9 kHz (no harsh highs), sample peak -1.5 dBFS, then OGG q5; true peak verified <= -1 dBTP.
Hit points (ms) follow /workspace/crashverse/animation/audio_sync_table.md. Usage: python boss_sfx.py <outdir>"""
import sys,os,json,subprocess,re,numpy as np,soundfile as sf
from scipy.signal import butter,sosfilt,fftconvolve
SR=44100; R=np.random.default_rng(1234)
def N(d): return int(d*SR)
def T(d): return np.arange(N(d))/SR
def flt(x,kind,f,o=2): return sosfilt(butter(o,f,kind,fs=SR,output='sos'),x)
def noise(d): return R.uniform(-1,1,N(d))
def env_exp(d,k,a=.002): t=T(d); return np.minimum(t/a,1)*np.exp(-t*k)
def put(buf,x,t0,g=1.0):
    s=N(t0); e=min(len(buf),s+len(x)); buf[s:e]+=x[:e-s]*g; return buf
def sweep_sine(d,f0,f1,k=3):
    t=T(d); f=f1+(f0-f1)*np.exp(-t*k); return np.sin(2*np.pi*np.cumsum(f)/SR)
def reverb(x,rt=1.2,wet=.3,tone=4500,pre=.012):
    t=T(rt*1.5); ir=R.standard_normal(len(t))*np.exp(-t*6.9/rt); ir=flt(ir,'low',tone); ir[:N(pre)]=0
    ir/=np.sqrt(np.sum(ir**2)); w=fftconvolve(x,ir)[:len(x)+len(ir)]
    y=np.zeros(len(w)); y[:len(x)]=x; return y*(1-wet*.5)+w*wet
def drive(x,a=3): return np.tanh(a*x)/np.tanh(a)
# ---------- layers ----------
def thump(d=.6,f0=90,f1=35,k=8): return sweep_sine(d,f0,f1,6)*env_exp(d,k)
def crack(d=.25,lo=800,hi=5000,k=30): return flt(noise(d),'band',[lo,hi])*env_exp(d,k,.0005)
def metal(f=180,d=1.0,k=4,bright=1.0):
    t=T(d); y=sum(a*np.sin(2*np.pi*f*r*t+R.uniform(0,6))*np.exp(-t*k*(1+.4*i)) for i,(r,a) in enumerate([(1,1),(2.76,.6*bright),(4.07,.4*bright),(5.4,.25*bright),(6.9,.15*bright)]))
    return y*np.minimum(t/.001,1)
def rumble(d,lp=140,rise=False):
    t=T(d); y=flt(noise(d),'low',lp,4)*6; e=(t/d)**1.5 if rise else np.ones(len(t)); return y*e
def splash(d=1.2,k=3.5):
    y=flt(noise(d),'band',[250,3500])*env_exp(d,k,.02)
    for _ in range(int(d*25)):                                  # droplets: short upward chirps, kept < 1.6 kHz
        t0=R.uniform(.05,d*.8); dd=R.uniform(.02,.05); f=R.uniform(500,1500)
        put(y,np.sin(2*np.pi*np.cumsum(np.linspace(f,f*1.6,N(dd)))/SR)*np.hanning(N(dd))*R.uniform(.1,.3)*np.exp(-t0*2),t0)
    return y
def debris(d=1.5,density=40,lo=400,hi=3500):
    y=np.zeros(N(d))
    for _ in range(int(density*d)):
        t0=R.exponential(d/4)
        if t0>d-.05: continue
        dd=R.uniform(.01,.04); put(y,flt(noise(dd),'band',[lo,hi])*np.hanning(N(dd))*R.uniform(.2,1)*np.exp(-t0*2.5),t0)
    return y
def whoosh(d,f0=300,f1=1800,peak=.8):
    t=T(d); x=noise(d); y=np.zeros(len(x)); blk=512
    for s in range(0,len(x),blk):
        fc=f0*(f1/f0)**(min(s/len(x)/peak,1)); y[s:s+blk]=flt(x[s:s+blk+256],'band',[fc*.6,fc*1.6])[:len(y[s:s+blk])]
    e=np.where(t<d*peak,(t/(d*peak))**2,np.exp(-(t-d*peak)*12)); return y*e
def grind(d,rate=(35,70),lo=200,hi=2200):   # stick-slip friction: jittered pulse train + band noise
    t=T(d); f=np.linspace(*rate,len(t))*(1+.2*np.sin(2*np.pi*3*t)); ph=np.cumsum(f)/SR
    pulses=(np.diff(np.floor(ph),prepend=0)>0).astype(float)*R.uniform(.5,1,len(t))
    y=flt(pulses,'band',[lo,hi])*4+flt(noise(d),'band',[lo,hi])*.3
    return y*np.minimum(t/.03,1)*np.minimum((d-t)/.08,1)
def hiss(d,lo=1500,hi=7000,k=2): return flt(noise(d),'band',[lo,hi])*env_exp(d,k,.01)
def growl(d,f0=70,f1=45,formants=((520,360,1.0),(1100,800,.6),(2400,2200,.25)),rough=28,wet=False):
    """creature vocalisation: jittered sawtooth + noise source -> moving formant band-passes, AM roughness"""
    t=T(d); f=f1+(f0-f1)*np.exp(-t*1.5)+f0*.04*np.sin(2*np.pi*5*t)+R.standard_normal(len(t)).cumsum()*.002
    ph=np.cumsum(f)/SR; src=2*(ph%1)-1+.35*noise(d)
    src*=1+.6*np.sin(2*np.pi*rough*t)*(.5+.5*np.sin(2*np.pi*.7*t))
    y=np.zeros(len(t))
    for a,b,g in formants:
        fc=np.linspace(a,b,len(t)); blk=1024
        for s in range(0,len(t),blk):
            c=fc[s]; seg=src[s:s+blk+512]; y[s:s+blk]+=g*flt(seg,'band',[c*.75,c*1.3])[:len(y[s:s+blk])]
    if wet: y*=1+.5*np.sin(2*np.pi*R.uniform(6,11)*t)         # bubbling/watery throat
    e=np.minimum(t/.12,1)*np.minimum((d-t)/.35,1)**1.5
    return drive(y*e*1.5,2.5)
def bell(f=196,d=1.6,k=2.2): return flt(metal(f,d,k,.6),'low',4000)
def lava_bubbles(d,rate=8):
    y=np.zeros(N(d))
    for _ in range(int(rate*d)):
        t0=R.uniform(0,d-.15); f=R.uniform(70,160); dd=R.uniform(.06,.14)
        put(y,np.sin(2*np.pi*np.cumsum(np.linspace(f,f*2.2,N(dd)))/SR)*np.hanning(N(dd))*R.uniform(.3,.8),t0)
    return y
def crackle(d,rate=60):
    y=np.zeros(N(d))
    for _ in range(int(rate*d)):
        t0=R.uniform(0,d-.01); dd=R.uniform(.002,.008); put(y,flt(noise(dd),'band',[1500,6000])*R.uniform(.2,1),t0)
    return y
def loopify(x,xf=.12):  # seamless loop: equal-power crossfade of the tail into the head
    n=N(xf); a=x[:-n].copy(); th=np.linspace(0,np.pi/2,n); a[:n]=a[:n]*np.sin(th)+x[-n:]*np.cos(th); return a
# ---------- events ----------
EV={}
def ev(name,var=1,hit_ms=0,trigger='',desc='',loop=False,sync='hit',hits=None):
    """sync: 'hit' = transient at hit_ms (+ any extra hits list); 'body' = sound starts (-20 dB) within 40 ms of t=0"""
    def deco(f): EV[name]=dict(f=f,var=var,hit_ms=hit_ms,trigger=trigger,desc=desc,loop=loop,sync=sync,hits=hits or [hit_ms]); return f
    return deco
# Tidewarden ----------------------------------------------------------
@ev('sfx_boss_tw_intro',1,2400,'BOSS_TIDE_INTRO','Tidewarden rises from the sea: rising rumble + water surge, lantern ignites at 2.4 s')
def tw_intro(v):
    d=3.0; y=np.zeros(N(d)); put(y,rumble(2.6,120,True),0,.9); put(y,splash(2.6,1.2)*np.linspace(0,1,N(2.6))**2,0,.7)
    put(y,whoosh(1.0,200,1500,.95),1.45,.6); put(y,thump(.8,70,40),2.4,.9); put(y,bell(147,1.0,3),2.4,.35)
    put(y,flt(noise(.6),'band',[300,2000])*env_exp(.6,6,.01),2.4,.4)   # flame bloom
    return reverb(y,1.6,.35)[:N(d)]
@ev('sfx_boss_tw_arms_rise',1,0,'BOSS_TIDE_INTRO +1.0 s (code; no ARMS_RISE anim event yet)','arms lift: hydraulic creak + water streaming off',sync='body')
def tw_arms_rise(v):
    d=1.5; y=grind(1.4,(25,45),150,1500)*.6; y=np.pad(y,(0,N(d)-len(y))); put(y,splash(1.4,1.5)*.5,.05); put(y,hiss(1.0,1200,5000,2)*.25,.3)
    return reverb(y,1.0,.3)[:N(d)]
@ev('sfx_boss_tw_arms_drop',1,600,'BOSS_TIDE_ARMS_DROP (t=0); whoosh peaks into ARMS_LAND at 600 ms','arms fall: heavy air whoosh, cut at the 600 ms land',sync='end')
def tw_arms_drop(v):
    d=.62; y=whoosh(d,150,900,.97)*.9+rumble(d,90,True)*.5; y[N(.6):N(.608)]*=np.linspace(1,0,N(.608)-N(.6)); y[N(.608):]=0; return y
@ev('sfx_boss_tw_arms_land',1,0,'BOSS_TIDE_ARMS_LAND (36 f / 600 ms)','arms crash onto sea-stack/water: sub thump + stone crack + big splash + debris')
def tw_arms_land(v):
    d=1.6; y=np.zeros(N(d)); put(y,thump(1.0,80,30,4),0,1.1); put(y,crack(.3,500,4000,18),0,.6); put(y,splash(1.5,2.5),.01,.8); put(y,debris(1.5,45),.03,.5)
    return drive(reverb(y,1.4,.3)[:N(d)],1.5)
@ev('sfx_boss_tw_chest_open',1,0,'BOSS_TIDE_CHEST_OPEN (42 f / 700 ms)','chest plates grind open with a pressure hiss',sync='body')
def tw_chest(v):
    d=1.0; y=grind(.95,(30,55),180,1800); y=np.pad(y,(0,N(d)-len(y))); put(y,hiss(.7,1200,5000,3)*.35,.25); put(y,metal(110,.8,4,.5)*.3,.85)
    return reverb(y,1.0,.3)[:N(d)]
@ev('sfx_boss_tw_phase2',1,0,'BOSS_TIDE_PHASE2 (75 f / 1250 ms)','core flare + transform: sub-boom, lantern bell, steam blast, roar')
def tw_phase2(v):
    d=2.0; y=np.zeros(N(d)); put(y,thump(1.5,70,26,2.5),0,1.0); put(y,bell(131,1.8,1.8),0,.5); put(y,hiss(1.2,800,5000,2.5),0,.4)
    put(y,growl(1.4,85,50,wet=True),.15,.5); put(y,debris(1.2,25),.05,.3)
    return reverb(y,1.8,.35)[:N(d)]
@ev('sfx_boss_tw_core_hum',1,0,'starts at BOSS_TIDE_PHASE2, loops while Phase 2 active','lantern core hum loop (seamless 1.5 s)',loop=True)
def tw_hum(v):
    d=1.62; t=T(d); y=sum(a*np.sin(2*np.pi*f*t) for f,a in [(110,1),(165,.4),(220,.25),(331,.1)])*(1+.25*np.sin(2*np.pi*(4/1.5)*t))
    y+=flt(noise(d),'band',[100,600])*.15; return loopify(y,.12)
@ev('sfx_boss_tw_shutter_tell',2,0,'BOSS_TIDE_TELL: pulses at 0 and 417 ms (2.4 Hz)','warning hum x2 at 2.4 Hz: low pulse + muted glow ping',sync='body')
def tw_tell(v):
    d=.9; y=np.zeros(N(d)); f=[98,104][v]
    for t0 in (0,.417):
        t=T(.4); p=(np.sin(2*np.pi*f*t)+.4*np.sin(2*np.pi*2*f*t))*np.sin(np.pi*np.minimum(t/.4,1))**2
        put(y,p,t0,.8); put(y,bell(f*4,.35,9)*.25,t0)
    return reverb(y,.8,.25)[:N(d)]
@ev('sfx_boss_tw_shutter_close',2,300,'BOSS_TIDE_SHUTTER_CLOSE: scrape over the 300 ms tween, thunk at 300 ms','heavy brick shutter slam')
def tw_close(v):
    d=.9; y=np.zeros(N(d)); put(y,grind(.3,(50,90),250,2000)*np.linspace(.3,1,N(.3)),0,.6)
    put(y,thump(.5,[100,90][v],40,10),.3,1.0); put(y,crack(.12,400,3000,40),.3,.6); put(y,debris(.5,30,300,2500),.31,.35)
    return reverb(y,.9,.25)[:N(d)]
@ev('sfx_boss_tw_shutter_open',2,0,'BOSS_TIDE_SHUTTER_OPEN','shutter grinds open',sync='body')
def tw_open(v):
    d=.6; y=grind(.58,[(40,70),(45,80)][v],200,2000); y=np.pad(y,(0,N(d)-len(y))); put(y,metal(140,.3,8,.4)*.3,.5); return reverb(y,.7,.25)[:N(d)]
@ev('sfx_boss_tw_joint_hit',3,0,'BOSS_TIDE_JOINT_HIT','shoulder joint clang (not broken)')
def tw_joint(v):
    d=.6; y=np.zeros(N(d)); put(y,metal([160,175,150][v],.6,6),0,.6); put(y,thump(.3,120,60,14),0,.7); put(y,crack(.08,1000,5000,50),0,.3)
    return reverb(y,.8,.25)[:N(d)]
@ev('sfx_boss_tw_shoulder_break',2,0,'BOSS_TIDE_JOINT_BREAK','joint breaks: big clank + metal snap + hydraulic hiss + debris')
def tw_shoulder(v):
    d=1.0; y=np.zeros(N(d)); put(y,metal([120,135][v],.9,3.5),0,.7); put(y,thump(.5,100,35,7),0,.9); put(y,crack(.2,600,4500,20),0,.6)
    put(y,hiss(.8,1500,6000,3),.05,.3); put(y,debris(.9,40),.02,.45); return drive(reverb(y,1.0,.3)[:N(d)],1.3)
@ev('sfx_boss_tw_hit_armor',2,0,'DestructibleBlock.Damaged role=armor','armour plank hit: wood knock + metal band')
def tw_armor(v):
    d=.8; y=np.zeros(N(d)); put(y,flt(noise(.15),'band',[200,1200])*env_exp(.15,30),0,.8); put(y,metal([210,230][v],.6,7,.5),0,.4); put(y,thump(.3,130,70,15),0,.6)
    return reverb(y,.8,.25)[:N(d)]
@ev('sfx_boss_tw_core_hit',3,0,'BOSS_TIDE_CORE_HIT','core hit: low lantern bell + glass crack + pained roar')
def tw_core(v):
    d=1.0; y=np.zeros(N(d)); put(y,bell([175,185,165][v],1.0,3),0,.6); put(y,crack(.25,900,6000,20),0,.5); put(y,thump(.4,110,50,10),0,.7)
    put(y,growl(.8,95,60,wet=True)*.5,.08); return reverb(y,1.2,.3)[:N(d)]
@ev('sfx_boss_tw_roar',3,0,'with BOSS_TIDE_CORE_HIT / JOINT_BREAK (code picks), intro end','watery sea-creature roar: formant growl with bubbling throat',sync='body')
def tw_roar(v):
    d=1.5; f0,f1=[(78,48),(90,55),(70,42)][v]
    y=growl(1.45,f0,f1,((560,380,1.0),(1150,820,.55),(2300,2000,.2)),wet=True); y=np.pad(y,(0,N(d)-len(y)))+rumble(d,100)*.2*np.exp(-T(d)*2)
    return reverb(y,1.5,.35)[:N(d)]
@ev('sfx_boss_tw_defeat',1,0,'BOSS_TIDE_DEFEAT (clip 4.0 s)','defeat: core burst, dying roar pitch-drop, collapse impacts at 1.2/2.0/2.6 s, huge splash 2.8 s, debris + water tail',hits=[0,1200,2000,2600])
def tw_defeat(v):
    d=4.0; y=np.zeros(N(d)); put(y,thump(2.0,70,22,1.5),0,1.0); put(y,bell(147,2.5,1.5),0,.45); put(y,crack(.4,600,5000,12),0,.5)
    put(y,growl(2.2,80,30,wet=True)*np.exp(-T(2.2)*.9),.2,.5)
    for t0,g in [(1.2,.8),(2.0,.9),(2.6,1.0)]: put(y,thump(.7,90,35,6),t0,g); put(y,crack(.2,400,4000,25),t0,.8*g); put(y,debris(1.0,35),t0,.4)
    put(y,splash(1.2,1.5),2.8,.9); put(y,debris(1.2,30),2.8,.3)
    y*=np.minimum((d-T(d))/.3,1); return drive(reverb(y,1.8,.3)[:N(d)],1.3)
# Emberforge Titan -----------------------------------------------------
@ev('sfx_titan_ranked_start',1,0,'BOSS_TITAN_RANKED_START (before intro)','ranked warning: double anvil tick + sub boom + rising ember')
def ti_ranked(v):
    d=1.0; y=np.zeros(N(d)); put(y,metal(220,.35,10,.5),0,.6); put(y,metal(220,.35,10,.5),.18,.6); put(y,thump(.8,60,30,4),0,.8); put(y,whoosh(.6,300,1500,.9)*.3,.3)
    return reverb(y,1.0,.3)[:N(d)]
@ev('sfx_titan_intro',1,1000,'BOSS_TITAN_INTRO','Titan reveal: lava rise + bubbles, anvil hits at 1.0/1.5/2.0 s (Titan never attacks)',hits=[1000,1500,2000])
def ti_intro(v):
    d=2.5; y=np.zeros(N(d)); put(y,rumble(2.5,130,True),0,.6); put(y,lava_bubbles(2.4,10),0,.4)
    for t0,f in [(1.0,147),(1.5,147),(2.0,110)]: put(y,metal(f,.6,6),t0,.7); put(y,thump(.4,90,45,10),t0,.9); put(y,crack(.1,600,4000,40),t0,.6)
    return drive(reverb(y,1.5,.3)[:N(d)],1.4)
for i,(nm,desc) in enumerate([('heated','"steel is heated" rule: sizzle + hot metal tick'),('magma','magma rule: slow lava bubbling'),('steam','steam vents rule: pulsing hiss')]):
    def _mk(i=i):
        def f(v):
            d=2.12
            if i==0: y=crackle(d,70)*.6+flt(noise(d),'band',[2000,6000])*.12+rumble(d,80)*.3
            elif i==1: y=lava_bubbles(d,9)+rumble(d,120)*.5
            else: t=T(d); y=hiss(d,1200,6000,0)*(0.55+.45*np.sin(2*np.pi*(2/2.0)*t))+rumble(d,90)*.3
            return loopify(y,.12)
        return f
    ev(f'sfx_titan_rule_{nm}',1,0,'BOSS_TITAN_RULE (loop, variant by day rule)',desc+' (loop 2.0 s)',loop=True)(_mk())
@ev('sfx_titan_hit',3,0,'BOSS_TITAN_HIT','damage hit: anvil clang + ember spray')
def ti_hit(v):
    d=.7; y=np.zeros(N(d)); put(y,metal([150,165,140][v],.7,5),0,.6); put(y,thump(.3,110,55,14),0,.7); put(y,crackle(.4,90)*np.exp(-T(.4)*6),.01,.5)
    return reverb(y,.9,.25)[:N(d)]
for i,st in enumerate([25,50,75]):
    def _mk(i=i):
        def f(v):
            d=[1.2,1.4,1.6][i]; y=np.zeros(N(d)); put(y,crack(.35,500,4500,10+0*i),0,.6+.1*i); put(y,thump(.9,85-10*i,28,5-i),0,.8+.1*i)
            put(y,grind(.8,(20,35),120,1200)*.5,.1)             # mechanical stagger groan
            put(y,debris(d-.1,30+15*i,300,3000),.02,.4+.1*i); put(y,lava_bubbles(d-.2,6+4*i)*.4,.1)
            return drive(reverb(y,1.3,.3)[:N(d)],1.3+.3*i)
        return f
    ev(f'sfx_titan_stage_{st}',1,0,f'BOSS_TITAN_STAGE at {st}%','stage break %d%%: armour crack + stagger groan + debris (escalating)'%st)(_mk())
@ev('sfx_titan_roar',3,0,'with BOSS_TITAN_STAGE (layer) and BOSS_TITAN_INTRO end','molten roar: deep distorted growl + sizzle + lava bubbles (not an attack)',sync='body')
def ti_roar(v):
    d=1.7; f0,f1=[(62,38),(70,42),(56,34)][v]
    y=growl(1.65,f0,f1,((450,300,1.0),(950,700,.6),(2100,1800,.2)),rough=34); y=np.pad(y,(0,N(d)-len(y)))
    y=drive(y,3)*.8+crackle(d,50)*.25*np.exp(-T(d)*1.5)+lava_bubbles(d,5)*.3
    return reverb(y,1.4,.3)[:N(d)]
@ev('sfx_titan_kill',1,0,'BOSS_TITAN_DEFEAT','meltdown: sub-boom, falling roar, iron collapse, steam hiss tail')
def ti_kill(v):
    d=2.5; y=np.zeros(N(d)); put(y,thump(1.8,65,22,1.8),0,1.0); put(y,growl(1.6,60,25),.1,.5)
    for t0 in (.6,1.1,1.5): put(y,metal(110,.8,4),t0,.4); put(y,debris(.8,40),t0,.4)
    put(y,hiss(1.3,1000,6000,2),1.1,.4); y*=np.minimum((d-T(d))/.25,1)
    return drive(reverb(y,1.6,.3)[:N(d)],1.4)
# ---------- master ----------
def meas(p):
    r=subprocess.run(['ffmpeg','-hide_banner','-nostats','-i',p,'-af','ebur128=peak=true','-f','null','-'],capture_output=True,text=True).stderr
    s=r[r.rfind('Summary:'):]; I=re.search(r'I:\s+(-?[\d.]+|-inf)',s).group(1); P=re.search(r'Peak:\s+(-?[\d.]+|-inf)',s).group(1); return float(I),float(P)
def hit_ok(y,h):
    a=N(h/1000); w=N(.03); pre=np.sqrt(np.mean(y[max(a-w,0):a]**2)) if a>0 else 0; post=np.sqrt(np.mean(y[a:a+w]**2))
    return bool(post>2*pre+1e-6)
def body_ms(y):
    w=int(.005*SR); e=np.array([np.sqrt(np.mean(y[i:i+w]**2)) for i in range(0,len(y)-w,w)]); return int(np.argmax(e>=.1*e.max())*5)
def onsets(y):
    w=int(.005*SR); e=np.array([np.sqrt(np.mean(y[i:i+w]**2)) for i in range(0,len(y)-w,w)])
    return int(np.argmax(e>=.5*e.max())*5), int(np.argmax(np.diff(e))*5+5)   # first >=50% of max, biggest energy rise
if __name__=='__main__':
    out=sys.argv[1]; os.makedirs(out,exist_ok=True); rep={}
    for name,e in EV.items():
        for v in range(e['var']):
            y=e['f'](v)
            if e['loop']: n=len(y); y=flt(np.tile(y,3),'low',9000,4)[n:2*n]   # circular filtering keeps the seam
            else: y=flt(y,'low',9000,4)
            y=y-np.mean(y)
            if not e['loop']: y[-N(.01):]*=np.linspace(1,0,N(.01))
            g=10**(-1.5/20)/np.abs(y).max()
            fn=f"{name}{'' if e['var']==1 else '_%02d'%(v+1)}"
            for it in range(4):
                sf.write(f'/tmp/{fn}.wav',(y*g).astype(np.float32),SR,subtype='FLOAT')
                subprocess.run(['ffmpeg','-y','-loglevel','error','-i',f'/tmp/{fn}.wav','-c:a','libvorbis','-q:a','5','-ac','1',f'{out}/{fn}.ogg'],check=True)
                I,P=meas(f'{out}/{fn}.ogg')
                if P<=-1.0: break
                g*=10**((-1.2-P)/20)
            r={'event':name,'dur_s':round(len(y)/SR,3),'samples':len(y),'LUFS':I,'TP':P,'hit_ms_spec':e['hit_ms'],'first50_ms':onsets(y*g)[0],'max_rise_ms':onsets(y*g)[1],'trigger':e['trigger'],'loop':e['loop']}
            if e['loop']: r['sync_ok']=True
            elif e['sync']=='end':   # pre-roll: loudest just before the land, silent after hit_ms
                yy=np.abs(y*g); pk=int(np.argmax(yy)/SR*1000); r['peak_ms']=pk; r['sync_ok']=bool(e['hit_ms']-80<=pk<=e['hit_ms'] and yy[N(e['hit_ms']/1000+.015):].max()<.05)
            elif e['sync']=='body': r['body_onset_ms']=body_ms(y*g); r['sync_ok']=r['body_onset_ms']<=40
            else: r['hits_ms']=e['hits']; r['sync_ok']=all(hit_ok(y*g,h) for h in e['hits'])
            if e['loop']:
                yy=y*g; r['seam_jump']=round(float(abs(yy[-1]-yy[0])),5); r['p99_step']=round(float(np.percentile(np.abs(np.diff(yy)),99)),5)
            rep[fn]=r; print(fn,r,flush=True)
    json.dump({'events':{k:{x:v[x] for x in ('var','hit_ms','trigger','desc','loop')} for k,v in EV.items()},'files':rep},open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'sfx_report.json'),'w'),indent=1)
