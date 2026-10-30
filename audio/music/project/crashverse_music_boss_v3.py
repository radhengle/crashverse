#!/usr/bin/env python3
"""CRASHVERSE boss music v3: each boss has its own identity + motif variation, built from thriller devices.
 Tidewarden (ocean/tide, 144 BPM, 12/8 roll): 'Tide Call' motif (triplet swell, tritone G#) answered by a heroic hook.
 Titan (molten forge, 150 BPM, straight 16ths): 'Forge Call' motif (hammered repeated notes, tritone Ab) answered by the anthem.
Devices: heartbeat sub-pulse, tremolo-string + piano-tremolo builds, Shepard risers, sub-booms, reversed swells,
filtered breakdowns, gate/stutter, m2/tritone stabs, call-and-response, a 32-bar dynamic arc
(DROP 0-7, CALL/RESPONSE 8-15, BREAKDOWN 16-19, REBUILD 20-27, GATE/STUTTER BUILD 28-31 -> loops into DROP).
Leads: sampled piano / guitars / strings (FluidR3_GM), no harsh highs. Background and devices: numpy synth."""
import numpy as np
from scipy.signal import butter,sosfilt,sosfiltfilt
import crashverse_music as C, crashverse_music_b2 as C2, crashverse_music_boss as B2
from crashverse_music import Track,m,hz,SR,rng
# ---------- device instruments (synth, background layer) ----------
def heart(vel):   # lub-dub sub pulse (~55/48 Hz), no highs
    t=np.arange(int(.5*SR))/SR
    def th(f,t0,a): tt=np.clip(t-t0,0,None); return a*np.sin(2*np.pi*f*tt)*np.exp(-tt*14)*(t>=t0)
    return (th(55,0,1)+th(48,.17,.7))*vel
def boom(vel):    # sub-boom: 62->28 Hz drop + soft thump
    t=np.arange(int(2.2*SR))/SR; f=28+34*np.exp(-t*4)
    y=np.sin(2*np.pi*np.cumsum(f)/SR)*np.exp(-t*2.2)
    nz=sosfilt(butter(2,180,'low',fs=SR,output='sos'),rng.uniform(-1,1,len(t)))*np.exp(-t*20)*2
    return (y+nz)*vel
def shepard(p,dur,vel):   # Shepard-style endless riser: octave-spaced glissandi under a fixed spectral bell
    n=int(dur*SR); t=np.arange(n)/SR; y=np.zeros(n); up=1.0   # rises one octave over dur
    for k in range(7):
        pos=(k+up*t/dur)%7                      # log2 position 0..7 above 55 Hz
        f=55*2**pos; a=np.exp(-((pos-3.2)**2)/2.2)
        y+=a*np.sin(2*np.pi*np.cumsum(f)/SR)
    env=(t/dur)**1.6; y=y*env/3
    return sosfilt(butter(2,3500,'low',fs=SR,output='sos'),y)*vel
def revswell(p,dur,vel):  # reversed swell: chord + air, exponential rise, cut at the end
    n=int(dur*SR); t=np.arange(n)/SR; f=hz(p)
    y=sum(np.sin(2*np.pi*f*r*t) for r in (1,1.5,2,3))/4+sosfilt(butter(2,[200,2500],'band',fs=SR,output='sos'),rng.uniform(-1,1,n))*.5
    env=(t/dur)**3; env[-int(.006*SR):]*=np.linspace(1,0,int(.006*SR))
    return y*env*vel
C.DRUM.update(heart=heart,boom=boom); C.GM_DRUM.update(heart=35,boom=41)
C.INST.update(shepard=shepard,revswell=revswell); C.PAN.update(heart=0,boom=0,shepard=0,revswell=0)
C.GM_PROG.update(shepard=95,revswell=94)
def ch(c): return [m(x) for x in C.CH[c]]
def seq(T,beat,inst,notes,root=0,vel=.8,oct=0):
    for b,nn,du in notes: T.n(beat+b,inst,m(nn)+root+12*oct,du,vel)
# motifs (beats)
TIDE_CALL=[(0,'A3',.667),(.667,'D4',.333),(1,'F4',.667),(1.667,'A4',1.333),(3,'G#4',.333),(3.333,'A4',.667)]   # tritone lean
TIDE_HERO=[(0,'C4',.333),(.333,'F4',.333),(.667,'A4',.333),(1,'C5',1),(2,'A4',.667),(2.667,'F4',.333),(3,'G4',1)]
FORGE_CALL=[(0,'D4',.25),(.5,'D4',.25),(1,'D4',.25),(1.5,'F4',.5),(2,'D4',.5),(2.5,'G#4',.5),(3,'A4',1)]          # hammered + tritone
FORGE_HERO=[(0,'A3',.5),(.5,'D4',.5),(1,'F4',.5),(1.5,'A4',1),(2.5,'F4',.5),(3,'A#4',1),(4,'A4',1.5),(5.5,'G4',.5),(6,'F4',.5),(6.5,'E4',.5),(7,'D4',1)]
def arc_fx(T,bars=32,off=0,gate_step=.25):
    o=off*4
    T.fx+=[('lp',o+15.5*4,o+16*4,o+19*4,o+20*4,420),          # filtered breakdown bars 16-19, sweeps back up into 20
           ('gate',o+28*4,o+30*4,gate_step,.55),              # trance gate build bars 28-29
           ('stutter',o+30*4,0.5,4),('stutter',o+31*4+2,0.25,4)]  # stutters in bar 30 and the end of bar 31 (not at the seam)
def section(bar): return 'drop' if bar<8 else 'cr' if bar<16 else 'bd' if bar<20 else 'reb' if bar<28 else 'build'
def devices(T,b,bar,r,triplet=False):
    sec=section(bar)
    hb=[0,2] if sec in('bd','reb') else [0]                                  # heartbeat pulse, faster in tension
    for k in hb: T.d(b+k,'heart',.8 if sec=='bd' else .6)
    if bar%8==0: T.d(b,'boom',.8)                                            # sub-boom on every 8-bar downbeat
    if sec=='reb':                                                           # rising tremolo build
        lvl=(bar-20)/8; st=1/6 if triplet else .125
        for k in range(int(4/st)): T.n(b+k*st,'trem',r+24+(1 if k%2 else 0)*0,st,.25+.5*lvl)
        if bar>=24: [T.n(b+k*.25,'lowpiano',r+(12 if k%2 else 0),.25,.3+.06*(bar-24)) for k in range(16)]  # piano octave tremolo
    if bar==24: T.n(b,'shepard',0,16,.6)                                     # 4-bar Shepard riser into the build
    if bar==31: T.n(b,'revswell',r+12,4,.7)                                   # reversed swell sucks into the DROP
    if sec in ('cr','reb') and bar%2==1:                                     # dissonant stabs: m2 + tritone
        for p in (r+12,r+13): T.n(b+3.5,'chug',p,.2,.6)
        for p in (r+24,r+30): T.n(b+3.5,'strings',p,.2,.45)
# ---------------- TIDEWARDEN v3 ----------------
WB=144; WP=['Dm','C','Bb','A']
def tide_bar(T,b,bar,ns,p2):
    r=ns[0]-12; sec=section(bar)
    full=sec in ('drop','cr','build') or p2 and sec!='bd'
    if sec!='bd':
        T.d(b,'kick',.85); T.d(b+1.667,'kick',.55 if sec!='reb' else .3); T.d(b+2,'kick',.8)
        if full: T.d(b+1,'snare',.6); T.d(b+3,'snare',.7)
        T.n(b,'taiko',r,.6,.75); T.n(b+2.667,'taiko',r,.4,.5); T.n(b+3.333,'taiko',r,.4,.55)
    T.n(b,'bass',r,1.5,.85 if sec!='bd' else .6); T.n(b+2,'bass',r,1,.7); T.n(b+3.333,'bass',r-5,.6,.6)
    if full:
        for k in range(12): T.n(b+k/3,'chug',r+12+(7 if k in (5,11) else 0),.2,.7 if k%3==0 else .4)
    T.n(b,'warm',r+12,4,.45); T.n(b,'warm',ns[2],4,.35)
    for p in ns: T.n(b,'pad',p,4,.25)
    if p2 and sec!='bd':
        for k in range(12): T.d(b+k/3,'hat',.45 if k%3==0 else .25)
        for k in range(6): T.n(b+k*.667,'strings',ns[[0,1,2,1,0,2][k]]+12,.2,.45)
    # call-and-response: boss Tide Call (low piano + muted gtr) <-> heroic hook (piano)
    if sec=='drop' and bar%4==0: seq(T,b,'hero',TIDE_CALL,vel=.8); seq(T,b,'lowpiano',TIDE_CALL,root=-12,vel=.5)
    if sec=='drop' and bar%4==2: seq(T,b,'hero',TIDE_HERO,vel=.75)
    if sec=='cr':
        if bar%2==0: seq(T,b,'lowpiano',TIDE_CALL,root=-12,vel=.6); seq(T,b,'chug',TIDE_CALL,root=-12,vel=.4)
        else: seq(T,b,'hero',TIDE_HERO,vel=.7)
    if sec=='bd' and bar==16: seq(T,b,'hero',TIDE_CALL,vel=.5)                 # lonely call in the breakdown
    if sec=='build': seq(T,b,'hero',TIDE_HERO if bar%2 else TIDE_CALL,vel=.6+.05*(bar-28))
    devices(T,b,bar,r,triplet=True)
def tidewarden_p1():
    T=Track('tidewarden_p1',WB)
    for bar in range(4):                                       # intro: heartbeat + tide rising + Shepard + reversed swell
        b=bar*4; T.d(b,'heart',.5+.1*bar); T.d(b+2,'heart',.5+.1*bar)
        T.n(b,'warm','D3',4,.35+.08*bar); T.n(b,'bass','D2',4,.55)
        for k in range(12): T.n(b+k/3,'chug','D3',.2,.2+.1*bar if k%3==0 else .15+.05*bar)
        if bar==0: T.d(b,'boom',.8); T.n(b,'shepard',0,16,.55); seq(T,b+1,'hero',TIDE_CALL,vel=.55)
        if bar==3: T.n(b,'revswell','D3',4,.7)
    for bar in range(32): tide_bar(T,16+bar*4,bar,ch(WP[bar%4]),False)
    arc_fx(T,off=4,gate_step=1/3)
    return T,16+128,16
def tidewarden_p2():
    T=Track('tidewarden_p2',WB)
    for bar in range(32): tide_bar(T,bar*4,bar,ch(WP[(bar+2)%4]),True)          # P2: harmony shifted (Bb-A-Dm-C) = more unstable
    arc_fx(T,gate_step=1/6)
    return T,128,0
def stinger_boss_phase2():  # Tidewarden phase change: Shepard + tremolo + m2 stabs, sub-boom + hit at 1.25 s (BOSS_TIDE_PHASE2)
    T=Track('stinger_boss_phase2',WB,tail=.7); hit=1.25*WB/60
    T.n(0,'shepard',0,hit,.75); T.n(0,'revswell','D3',hit,.6)
    for k in range(int(hit*6)): T.n(k/6,'trem',m('D4')+(1 if k%2 else 0),1/6,.3+.5*k/(hit*6))
    for k in range(8): T.d(hit-1+k*.125,'snare',.25+.06*k)
    T.d(hit,'boom',1.0); T.n(hit,'taiko','D2',1,.95); T.d(hit,'kick',.95); T.n(hit,'bass','D2',1.5,.9)
    seq(T,hit,'hero',TIDE_CALL[:4],vel=.85); seq(T,hit,'lowpiano',TIDE_CALL[:4],root=-12,vel=.55)
    for p in ('D3','D#3'): T.n(hit+.5,'chug',p,.6,.7)
    return T,hit+2,None
def stinger_tidewarden_defeat():  # 5.6 s: final Tide Call collapses -> heroic hook in D major, Dmaj9 bloom
    T=Track('stinger_tidewarden_defeat',WB,tail=1.2)
    T.d(0,'boom',.9); seq(T,0,'hero',TIDE_CALL,vel=.75); seq(T,0,'lowpiano',TIDE_CALL,root=-12,vel=.5)
    for k in range(12): T.n(k/3,'chug','D3',.2,.5-.03*k)
    T.n(3,'revswell','A3',1,.6)
    seq(T,4,'hero',[(0,'D4',.333),(.333,'F#4',.333),(.667,'A4',.333),(1,'D5',1),(2,'B4',.667),(2.667,'A4',.333),(3,'F#4',1)],vel=.8)
    for nn in ['G3','D4','A4','B4']: T.n(4,'warm',m(nn),2,.45)
    for nn in ['A3','E4','C#5']: T.n(6,'warm',m(nn),2,.45)
    for nn in ['D3','A3','F#4','C#5','E5']: T.n(8,'warm',m(nn),4,.6)
    for k,nn in enumerate(['D3','A3','E4','F#4','C#5','E5']): T.n(8+k*.15,'nylon',nn,3,.5)
    for nn in ['D3','A3','C#4','E4','F#4']: T.n(8,'piano',nn,3.5,.6)
    T.n(4,'bass','G2',2,.65); T.n(6,'bass','A2',2,.65); T.n(8,'bass','D2',3.5,.85); T.d(8,'boom',.7); T.n(8,'taiko','D2',1,.6)
    return T,10.5,None
# ---------------- TITAN v3 (stems; same 3-voice rule) ----------------
TB=150; TP=['Dm','Bb','Gm','A']; BARS=32
def titan_layers():
    L={k:Track(f'titan_{k}',TB) for k in ['base','arena_foundry_crater','arena_cinder_bridge','arena_ash_citadel',
        'arena_obsidian_forge','int25','int50','int75']}
    for bar in range(BARS):
        b=bar*4; c=TP[bar%4]; ns=ch(c); r=ns[0]-12; B=L['base']; sec=section(bar)
        full=sec in ('drop','cr','build')
        if sec!='bd':
            for k in range(8): B.d(b+k*.5,'kick',(.75 if k%2==0 else .5)*(1 if full else .6))
            B.d(b+1,'snare',.7 if full else .4); B.d(b+3,'snare',.7)
            B.n(b,'taiko',r,.5,.7); B.n(b+2.5,'taiko',r,.5,.5)
        for k in range(8): B.n(b+k*.5,'bass',r,.4,.75 if sec!='bd' else .5)
        if full:
            for k in range(16):
                acc=k in (0,3,6,10,12); B.n(b+k*.25,'chug',r+12+(3 if k in (10,11) else 0),.15,.75 if acc else .45)
        B.n(b,'forge','D3',.5,.7 if sec!='bd' else .9)                    # anvil downbeat (louder alone in breakdown)
        if sec=='bd': B.n(b+2,'forge','G#3',.5,.6)                         # tritone anvil answer
        for p in ns: B.n(b,'pad',p,4,.25)
        if sec=='drop' and bar%4==0: seq(B,b,'hero',FORGE_CALL,vel=.8); seq(B,b,'lowpiano',FORGE_CALL,root=-12,vel=.55)
        if sec=='drop' and bar%4==2: seq(B,b,'hero',FORGE_HERO[:6],vel=.75)
        if sec=='cr':
            if bar%4==0: seq(B,b,'lowpiano',FORGE_CALL,root=-12,vel=.6); seq(B,b,'chug',FORGE_CALL,root=-12,vel=.45)
            if bar%4==1: seq(B,b,'hero',FORGE_HERO,vel=.75)
        if sec=='build': seq(B,b,'hero',FORGE_CALL,vel=.6+.05*(bar-28))
        devices(B,b,bar,r)
        F=L['arena_foundry_crater']
        for k,v in [(1,.6),(3,.6),(3.5,.4)]: F.n(b+k,'forge','A3' if k==3.5 else 'D3',.3,v)
        Cb=L['arena_cinder_bridge']
        for k in range(8): Cb.n(b+k*.5,'strings',ns[[0,1,2,1][k%4]]+12,.2,.5)
        A=L['arena_ash_citadel']; A.n(b,'pad',r,4,.45); A.n(b,'lowpiano',r,2,.55); A.n(b+2,'lowpiano',r+6 if bar%2 else r+7,2,.4)   # tritone tolls
        O=L['arena_obsidian_forge']
        for k,v in [(0,.8),(.75,.5),(1.5,.7),(2,.5),(2.5,.8),(3.25,.5),(3.5,.6),(3.75,.6)]: O.n(b+k,'taiko',r-(12 if k in (0,2.5) else 0),.4,v)
        I1=L['int25']
        for k in range(16): I1.d(b+k*.25,'hat',.55 if k%2 else .3)
        for k in (1,3): I1.d(b+k,'heart',.5)                               # double heartbeat once damaged
        I2=L['int50']
        for k in range(8): I2.n(b+k*.5+.25,'strings',ns[2]+12 if k%2 else ns[1]+12,.15,.45)
        if bar%2: [I2.n(b+3.5,'strings',p,.2,.45) for p in (r+24,r+25)]      # m2 string stabs
        I3=L['int75']
        for k in range(8): I3.d(b+3+k*.125,'snare',.25+.05*k)
        if bar%8==2: seq(I3,b,'hero',FORGE_HERO,oct=1,vel=.5)
        if bar%8==7: I3.n(b,'shepard',0,4,.5)
        for k in range(8): I3.n(b+k*.5,'trem',r+24,.5,.3)
    for t in L.values(): arc_fx(t)
    return [(t,BARS*4,0) for t in L.values()]
def stinger_titan_defeat():  # 5.6 s: Forge Call hammered, sub-boom meltdown, anthem resolves in D major
    T=Track('stinger_titan_defeat',TB,tail=1.2)
    seq(T,0,'hero',FORGE_CALL,vel=.8); seq(T,0,'lowpiano',FORGE_CALL,root=-12,vel=.55)
    for k in range(16): T.n(k*.25,'chug','D3',.15,.5)
    T.n(0,'forge','D3',.5,.8); T.n(2,'forge','G#3',.5,.7); T.n(0,'shepard',0,4,.5)
    T.d(4,'boom',1.0); T.n(4,'taiko','D2',1,.9)
    seq(T,4,'hero',[(0,'A3',.5),(.5,'D4',.5),(1,'F#4',.5),(1.5,'A4',1),(2.5,'F#4',.5),(3,'B4',1)],vel=.8)
    for nn in ['G3','D4','A4','B4']: T.n(4,'warm',m(nn),2,.45)
    for nn in ['A3','E4','C#5']: T.n(6,'warm',m(nn),2,.45)
    for nn in ['D3','A3','F#4','C#5','E5']: T.n(8,'warm',m(nn),4,.6)
    for k,nn in enumerate(['D3','A3','E4','F#4','C#5','E5']): T.n(8+k*.15,'nylon',nn,3,.5)
    for nn in ['D3','A3','C#4','E4','F#4']: T.n(8,'piano',nn,3.5,.6)
    T.n(4,'bass','G2',2,.65); T.n(6,'bass','A2',2,.65); T.n(8,'bass','D2',3.5,.85); T.n(8,'forge','D3',.5,.6)
    return T,11,None
