#!/usr/bin/env python3
"""CRASHVERSE in-house music generator (original composition, pure numpy DSP synth).
No samples, no soundfonts: every sound is synthesised here. Writes .mid (score) + .wav (float) per track.
Usage: python crashverse_music.py <outdir>   (then render.sh normalises + encodes OGG)
Shared motif "Toybox Hook" (scale degrees 5-1-3-5 | 3-6): G C E G . E A  in C."""
import sys, os, json, numpy as np, mido, soundfile as sf
SR = 44100
rng = np.random.default_rng(7)
NOTE = {n:i for i,n in enumerate(['C','C#','D','D#','E','F','F#','G','G#','A','A#','B'])}
def m(n):  # 'C5' -> midi
    return NOTE[n[:-1]] + 12*(int(n[-1])+1)
def hz(p): return 440*2**((p-69)/12)

# ---------- instruments (mono, return np array) ----------
def env(n, a, d, s, r, sus_len):
    a,d,r=int(a*SR),int(d*SR),int(r*SR); sl=max(int(sus_len*SR)-a-d,0)
    e=np.concatenate([np.linspace(0,1,a,False),np.linspace(1,s,d,False),np.full(sl,s),np.linspace(s,0,r)])[:n]
    return np.pad(e,(0,n-len(e)))
def glock(p,dur,vel):
    t=np.arange(int((dur+1.2)*SR))/SR; f=hz(p)
    y=sum(a*np.sin(2*np.pi*f*k*t)*np.exp(-t*dec) for k,a,dec in [(1,1,3.5),(2.76,.35,9),(5.4,.15,16)])
    return y*np.minimum(t*800,1)*vel
def pluck(p,dur,vel):  # Karplus-Strong "ukulele"
    f=hz(p); N=int(SR/f); n=int((dur+.6)*SR)
    buf=rng.uniform(-1,1,N); out=np.empty(n)
    for i in range(n):
        out[i]=buf[i%N]; buf[i%N]=0.497*(buf[i%N]+buf[(i+1)%N])
    t=np.arange(n)/SR; g=np.where(t<dur,1,np.exp(-(t-dur)*12))
    return out*g*vel*0.8
def whistle(p,dur,vel):  # sine lead w/ vibrato -- toy flute
    n=int((dur+.15)*SR); t=np.arange(n)/SR
    ph=2*np.cumsum(np.pi*hz(p)*(1+.006*np.sin(2*np.pi*5.5*t)*np.minimum(t/.25,1)))/SR
    y=np.sin(ph)+.12*np.sin(2*ph)
    return y*env(n,.02,.08,.75,.12,dur)*vel*.6
def bass(p,dur,vel):  # rounded triangle, low-passed feel; little 1-4 kHz energy
    n=int((dur+.08)*SR); t=np.arange(n)/SR; f=hz(p)
    y=2/np.pi*np.arcsin(np.sin(2*np.pi*f*t))*.8+.3*np.sin(2*np.pi*f*t)
    return y*env(n,.005,.12,.6,.06,dur)*vel
def pad(p,dur,vel):  # soft detuned sines, sits low-mid, leaves SFX band free
    n=int((dur+.4)*SR); t=np.arange(n)/SR; f=hz(p)
    y=sum(np.sin(2*np.pi*f*d*t+k) for k,d in enumerate([1,1.004,.996]))/3
    return y*env(n,.25,.2,.8,.4,dur)*vel*.35
def kick(vel):
    t=np.arange(int(.35*SR))/SR; f=50+90*np.exp(-t*35)
    return np.sin(2*np.pi*np.cumsum(f)/SR)*np.exp(-t*9)*vel
def snare(vel):  # bright clap-ish toy snare
    t=np.arange(int(.2*SR))/SR; nz=rng.uniform(-1,1,len(t)); nz=np.diff(nz,prepend=0)
    return (nz*.5*np.exp(-t*22)+.4*np.sin(2*np.pi*190*t)*np.exp(-t*30))*vel
def hat(vel):
    t=np.arange(int(.06*SR))/SR; nz=np.diff(rng.uniform(-1,1,len(t)),prepend=0)
    return np.diff(nz,prepend=0)*.25*np.exp(-t*70)*vel
def shaker(vel):
    t=np.arange(int(.09*SR))/SR; nz=np.diff(rng.uniform(-1,1,len(t)),prepend=0)
    return nz*.18*np.sin(np.pi*t/.09)*vel
INST={'trem':pad,'hero':glock,'chug':bass,'strings':pad,'lowpiano':bass,'glock':glock,'pluck':pluck,'whistle':whistle,'bass':bass,'pad':pad,'piano':glock,'nylon':pluck,'warm':pad}
DRUM={'kick':kick,'snare':snare,'hat':hat,'shaker':shaker}
PAN={'trem':.25,'hero':.05,'chug':-.35,'strings':.35,'lowpiano':-.1,'piano':.1,'nylon':-.3,'warm':0,'glock':.35,'pluck':-.3,'whistle':.1,'bass':0,'pad':0,'kick':0,'snare':.05,'hat':.4,'shaker':-.4}
GM_PROG={'trem':44,'hero':0,'chug':29,'strings':48,'lowpiano':0,'piano':0,'nylon':24,'warm':89,'glock':9,'pluck':24,'whistle':72,'bass':33,'pad':89}
GM_DRUM={'kick':36,'snare':39,'hat':42,'shaker':70}

# ---------- v2 leads: sampled piano/guitar via FluidSynth (FluidR3_GM, MIT) ----------
import subprocess,tempfile
from scipy.signal import butter,sosfiltfilt
SF2='/usr/share/sounds/sf2/FluidR3_GM.sf2'
LEAD_INST={'glock','whistle','brass','anvil','piano','nylon','warm','hero','chug','strings','lowpiano','trem'}; LEAD_TRIM=1.0
DARK_PREFIX=('titan','tidewarden','stinger_boss_phase2')
REWARD=('stinger_win','stinger_win_3star','stinger_chapter_clear','stinger_boss_defeat','results_loop')
# GM programs: 0 acoustic grand, 24 nylon gtr, 25 steel gtr, 28 muted gtr, 29 overdriven gtr
LEADMAP={'bright':{'glock':0,'whistle':24,'brass':25,'anvil':28,'pluck':24,'piano':0,'nylon':24,'warm':89,'hero':0,'chug':29,'strings':48,'lowpiano':0,'trem':44},
         'dark':  {'glock':0,'whistle':28,'brass':29,'anvil':28,'pluck':28,'piano':0,'nylon':24,'warm':89,'hero':0,'chug':29,'strings':48,'lowpiano':0,'trem':44}}
def is_lead(e): return e[1] in LEAD_INST or (e[1]=='pluck' and e[2]>=72)
def fold(p,inst,mode):
    if inst in ('warm','lowpiano','chug'): return p
    if inst=='trem':
        while p>76: p-=12
        return p
    if inst=='hero':
        while p>79: p-=12
        return p
    if inst=='anvil': p-=24                    # metallic ticks -> muted-guitar ticks two octaves down
    top=72 if mode=='dark' else 79             # nothing above C5 (dark) / G5 (bright)
    while p>top: p-=12
    return p
def fluid_leads(T,ld,EXT):
    mode='dark' if T.name.startswith(DARK_PREFIX) else 'bright'; pm=LEADMAP[mode]
    mf=mido.MidiFile(ticks_per_beat=960); tr=mido.MidiTrack(); mf.tracks.append(tr)
    tr.append(mido.MetaMessage('set_tempo',tempo=mido.bpm2tempo(T.bpm)))
    insts=sorted({e[1] for e in ld}); ch={k:n for n,k in enumerate(insts)}; msgs=[]
    for k in insts:
        msgs+= [(0,0,mido.Message('program_change',channel=ch[k],program=pm[k])),
                (0,0,mido.Message('control_change',channel=ch[k],control=10,value=int(64+PAN[k]*50))),
                (0,0,mido.Message('control_change',channel=ch[k],control=7,value=100))]
    for b,i,p,du,v in ld:
        q=fold(p,i,mode); vel=int(np.clip(v*(95 if mode=='dark' else 110),20,120))
        d=max(du,.25) if i not in ('anvil','chug','strings') else max(du,.12)
        if i=='warm': vel=int(np.clip(v*90,20,110))
        msgs.append((int(b*960),1,mido.Message('note_on',channel=ch[i],note=q,velocity=vel)))
        msgs.append((int((b+d)*960),0,mido.Message('note_off',channel=ch[i],note=q,velocity=0)))
    msgs.sort(key=lambda x:(x[0],x[1])); now=0
    for t,_,mm in msgs: tr.append(mm.copy(time=t-now)); now=t
    with tempfile.TemporaryDirectory() as td:
        mf.save(f'{td}/l.mid')
        rv=['-o','synth.reverb.room-size=0.88','-o','synth.reverb.damp=0.45','-o','synth.reverb.width=1.0','-o','synth.reverb.level=0.85'] if T.name in REWARD else []
        subprocess.run(['fluidsynth','-ni','-q','-R','1','-C','0',*rv,'-g','0.5','-r',str(SR),'-F',f'{td}/l.wav',SF2,f'{td}/l.mid'],check=True,capture_output=True)
        y,sr=sf.read(f'{td}/l.wav'); assert sr==SR
    if y.ndim==1: y=np.stack([y,y],1)
    out=np.zeros((EXT,2)); n=min(len(y),EXT); out[:n]=y[:n]
    sos=butter(4,6000 if mode=='dark' else 7500,'low',fs=SR,output='sos')   # roll off harsh top end
    return sosfiltfilt(sos,out,axis=0)

def apply_fx(T,y):
    """Post FX on the full (wrapped) mix, linear so stems stay additive. Envelopes are smooth and equal 1 at
    loop edges, keeping seams continuous.
    ('lp', b0,b1,b2,b3, cutoff): dry->low-passed crossfade (in b0..b1, hold, out b2..b3) = filtered breakdown
    ('gate', b0,b1, step, duty): trance gate (3 ms ramps)
    ('stutter', b_src, slice_beats, repeats): repeat a slice with 4 ms crossfades"""
    spb=60/T.bpm; S=lambda b:int(round(b*spb*SR)); n=len(y)
    for f in T.fx:
        if f[0]=='lp':
            _,b0,b1,b2,b3,fc=f; lp=sosfiltfilt(butter(4,fc,'low',fs=SR,output='sos'),y,axis=0)
            w=np.zeros(n); w[S(b0):S(b1)]=np.linspace(0,1,S(b1)-S(b0)); w[S(b1):S(b2)]=1; w[S(b2):S(b3)]=np.linspace(1,0,S(b3)-S(b2))
            y=y*(1-w[:,None])+lp*w[:,None]
        elif f[0]=='gate':
            _,b0,b1,st,duty=f; g=np.ones(n); r=int(.003*SR)
            b=b0
            while b<b1-1e-9:
                a=S(b+st*duty); e=S(b+st); g[a:e]=0.08
                g[a:a+r]=np.linspace(1,.08,r); g[e-r:e]=np.linspace(.08,1,r); b+=st
            y=y*g[:,None]
        elif f[0]=='stutter':
            _,bs,sl,rep=f; a=S(bs); L=S(sl); src=y[a:a+L].copy(); fd=int(.004*SR)
            for k in range(1,rep):
                d=a+k*L; seg=src.copy(); seg[:fd]*=np.linspace(0,1,fd)[:,None]; seg[-fd:]*=np.linspace(1,0,fd)[:,None]
                orig=y[d:d+L].copy(); orig[fd:-fd]=0; orig[:fd]*=np.linspace(1,0,fd)[:,None]; orig[-fd:]*=np.linspace(0,1,fd)[:,None]
                y[d:d+L]=seg+orig
    return y

class Track:
    def __init__(s,name,bpm,tail=.6): s.name,s.bpm,s.ev,s.tail,s.fx=name,bpm,[],tail,[]  # (beat,inst,pitch,durbeats,vel)
    def n(s,beat,inst,note,dur,vel=.8): s.ev.append((beat,inst,m(note) if isinstance(note,str) else note,dur,vel))
    def d(s,beat,drum,vel=.8): s.ev.append((beat,drum,GM_DRUM[drum],0,vel))
    def spb(s): return 60/s.bpm
    def render(s,total_beats,loop_from=None):
        """Render to stereo. Background voices (drums/bass/pad/low strums) use the numpy synth.
        Lead/melody voices (glock, whistle, brass, anvil, plucks >= C5) are rendered with FluidSynth +
        FluidR3_GM (piano / guitars, see LEADMAP), octave-folded, low-passed, and RMS-matched to the old synth lead.
        If loop_from (beats) is given, tails past the end wrap onto loop_from so the seam is continuous."""
        L=int(round(total_beats*s.spb()*SR)); EXT=L+SR*3
        def place(evs):
            out=np.zeros((EXT,2))
            for b,i,p,du,v in evs:
                y=DRUM[i](v) if i in DRUM else INST[i](p,du*s.spb(),v)
                st=int(round(b*s.spb()*SR)); pn=PAN[i]; y=y[:EXT-st]
                out[st:st+len(y),0]+=y*np.sqrt((1-pn)/2); out[st:st+len(y),1]+=y*np.sqrt((1+pn)/2)
            return out
        def full(evs):
            o=place([e for e in evs if not is_lead(e)]); ld=[e for e in evs if is_lead(e)]
            if ld:
                old=place(ld); nw=fluid_leads(s,ld,EXT)
                o+=nw*(np.sqrt(np.mean(old**2))/max(np.sqrt(np.mean(nw**2)),1e-9))*LEAD_TRIM
            return o
        if loop_from:   # intro material must not ring into the loop region (it would not be there on repeat)
            ls=int(round(loop_from*s.spb()*SR)); intro=full([e for e in s.ev if e[0]<loop_from])
            fw=int(.03*SR); intro[ls-fw:ls]*=np.linspace(1,0,fw)[:,None]**2; intro[ls:]=0
            out=intro+full([e for e in s.ev if e[0]>=loop_from])
        else: out=full(s.ev)
        tail=out[L:]; out=out[:L]
        if loop_from is not None:
            ls=int(round(loop_from*s.spb()*SR)); tl=min(len(tail),L-ls)
            out[ls:ls+tl]+=tail[:tl]
            out=apply_fx(s,out)
        else:
            nt=int(s.tail*SR); t=tail[:nt]*np.linspace(1,0,nt)[:,None]**2; out=np.concatenate([out,t])
        return out
    def midi(s,path):
        mf=mido.MidiFile(ticks_per_beat=480); tr=mido.MidiTrack(); mf.tracks.append(tr)
        tr.append(mido.MetaMessage('set_tempo',tempo=mido.bpm2tempo(s.bpm)))
        chans={}; msgs=[]
        for b,i,p,du,v in s.ev:
            if i in DRUM: ch=9; du=.25
            else:
                if i not in chans:
                    chans[i]=len(chans) if len(chans)<9 else len(chans)+1
                    msgs.append((0,0,mido.Message('program_change',channel=chans[i],program=GM_PROG[i])))
                ch=chans[i]
            msgs.append((int(b*480),1,mido.Message('note_on',channel=ch,note=p,velocity=int(v*127))))
            msgs.append((int((b+du)*480),0,mido.Message('note_off',channel=ch,note=p,velocity=0)))
        msgs.sort(key=lambda x:(x[0],x[1])); now=0
        for t,_,msg in msgs: tr.append(msg.copy(time=t-now)); now=t
        mf.save(path)

# ---------- shared motif ----------
def motif(T,beat,inst,root=0,vel=.8,oct=0):
    """Toybox Hook: G C E G(long) E A(long) -- transposed by root semitones"""
    for b,nn,du in [(0,'G4',.5),(.5,'C5',.5),(1,'E5',.5),(1.5,'G5',1),(2.5,'E5',.5),(3,'A5',1)]:
        T.n(beat+b,inst,m(nn)+root+12*oct,du,vel)
CH={'C':['C3','E3','G3'],'F':['F2','A2','C3'],'G':['G2','B2','D3'],'Am':['A2','C3','E3'],'Dm':['D3','F3','A3'],'Em':['E3','G3','B2']}
def chord_bar(T,beat,c,style,root=0,vel=.7):
    ns=[m(x)+root for x in CH[c]]
    if style=='pad':
        for p in ns: T.n(beat,'pad',p+12,4,vel*.8)
    if style=='uke':  # island strum: down on 1, 2&, 3, 4&
        for b in [0,1.5,2,3.5]:
            for k,p in enumerate(ns): T.n(beat+b+k*.02,'pluck',p+12,.45,vel*.6)
    T.n(beat,'bass',ns[0]-12,1.5,vel); T.n(beat+2,'bass',ns[0]-12+(7 if c!='Am' else 7),1,vel*.8); T.n(beat+3.5,'bass',ns[0]-12,.5,vel*.7)
def beat_bar(T,beat,style,vel=.8):
    if style=='light':
        T.d(beat,'kick',vel*.8); T.d(beat+2,'snare',vel*.5)
        for e in range(8): T.d(beat+e*.5,'shaker',vel*(.7 if e%2 else .45))
    if style=='drive':
        for k in [0,1.5,2,3]: T.d(beat+k,'kick',vel)
        T.d(beat+1,'snare',vel*.7); T.d(beat+3,'snare',vel*.7)
        for e in range(8): T.d(beat+e*.5,'hat',vel*(.8 if e%2 else .5))

def main_theme():
    T=Track('main_theme',112); prog=['C','Am','F','G']
    # intro 4 bars: glock motif over pad
    for i,c in enumerate(prog): chord_bar(T,i*4,c,'pad',vel=.5)
    motif(T,0,'glock',vel=.6); motif(T,8,'glock',vel=.6,root=-3+3)  # repeat
    # loop body: 32 bars (A A' B A'') starting at bar 4
    L0=16
    for sec in range(4):
        for i in range(8):
            bar=L0+sec*32+i*4; c=(prog if sec!=2 else ['F','G','Em','Am'])[i%4]
            chord_bar(T,bar,c,'uke' if sec else 'pad',vel=.7)
            if sec: chord_bar(T,bar,c,'pad',vel=.35) if sec==2 else None
            beat_bar(T,bar,'light' if sec in (0,2) else 'drive',vel=.6 if sec==0 else .75)
        for ph in range(4):
            b=L0+sec*32+ph*8
            if sec==2:  # B section: motif inverted-ish answer on glock, whistle sustains
                T.n(b,'whistle','A5',2,.6); T.n(b+2,'whistle','G5',2,.6); T.n(b+4,'whistle','E5',3,.6)
                T.n(b+7,'whistle','D5',1,.5)
            else:
                motif(T,b,'whistle' if sec else 'glock',vel=.7)
                ans=[('G5',.5),('F5',.5),('E5',.5),('D5',.5),('C5',2)] if ph%2 else [('B4',.5),('C5',.5),('D5',1),('G4',2)]
                bb=b+4
                for nn,du in ans: T.n(bb,'whistle' if sec else 'glock',nn,du,.6); bb+=du
    return T,16+128,16
def sunnyville_map():
    T=Track('sunnyville_map_loop',104); prog=['C','F','C','G','Am','F','G','C']
    for r in range(4):  # 32 bars, no intro (seamless loop from 0)
        for i,c in enumerate(prog):
            b=r*32+i*4; chord_bar(T,b,c,'uke',vel=.6); beat_bar(T,b,'light',vel=.45 if r%2==0 else .55)
        if r in (1,3):
            motif(T,r*32,'glock',vel=.55); motif(T,r*32+16,'glock',vel=.55)
            T.n(r*32+8,'whistle','E5',2,.45); T.n(r*32+10,'whistle','D5',2,.45); T.n(r*32+12,'whistle','C5',4,.45)
        else:
            motif(T,r*32+4,'whistle',vel=.5); motif(T,r*32+20,'glock',vel=.45,oct=0)
    return T,128,0
def gameplay():
    T=Track('gameplay_loop',120); r=7  # G major; transpose prog up a fifth
    prog=['C','C','F','G','Am','F','G','G']
    for rep in range(4):  # 32 bars
        for i,c in enumerate(prog):
            b=rep*32+i*4; chord_bar(T,b,c,'pad',root=r-12 if c in('Am','G') else r,vel=.65)
            beat_bar(T,b,'drive',vel=.7)
            # sparse glock off-beat stabs in high register (above SFX mids)
            for k in [.5,2.5]: T.n(b+k,'glock',m(CH[c][0])+r+36-(12 if c in('Am','G') else 0),.25,.35)
        if rep%2: motif(T,rep*32+16,'glock',root=r,vel=.4)
        else: motif(T,rep*32+16,'whistle',root=r-12,vel=.3)
    return T,128,0
def stinger_win():
    T=Track('stinger_win',132)
    motif(T,0,'glock',vel=.8); motif(T,0,'whistle',vel=.6)
    for p in ['C4','E4','G4','C5']: T.n(4,'pad',p,2.5,.7)
    T.n(4,'glock','C6',2,.8); T.n(4,'bass','C2',2,.9); T.d(4,'kick',.9); T.d(4,'snare',.6)
    for k in range(4): T.d(k,'shaker',.5)
    return T,7,None
def stinger_fail():
    T=Track('stinger_fail',100)  # motif deflates: G C E ... then slides down, cheeky
    for b,nn,du in [(0,'G4',.5),(.5,'C5',.5),(1,'E5',.75),(2,'D#5',.5),(2.5,'D5',.5),(3,'C#5',1.5)]:
        T.n(b,'whistle',nn,du,.6); T.n(b,'glock',nn,du*.5,.35)
    T.n(3,'bass','C3',1.2,.6)
    T.d(3,'snare',.4)
    return T,4.0,None

if __name__=='__main__':
    out=sys.argv[1]; info={}
    from crashverse_music_reward import stinger_win
    for fn in [main_theme,sunnyville_map,gameplay,stinger_win,stinger_fail]:
        T,beats,lf=fn(); y=T.render(beats,lf)
        sf.write(f'{out}/{T.name}.wav',y.astype(np.float32),SR,subtype='FLOAT')
        T.midi(f'{out}/{T.name}.mid')
        info[T.name]={'bpm':T.bpm,'samples':len(y),
          'loop_start':None if lf is None else int(round(lf*T.spb()*SR)),
          'loop_end':None if lf is None else len(y)}
        print(T.name,info[T.name],flush=True)
    json.dump(info,open(f'{out}/render_info.json','w'),indent=1)
