#!/usr/bin/env python3
"""CRASHVERSE v2 boss family: fast, tense, heroic. Titan (molten forge, 150 BPM, straight 16ths, relentless)
and Tidewarden (ocean, 144 BPM in a 12/8 triplet roll, deep low end). Shared motif becomes the BOSS ANTHEM hook
(5-1-b3-5 | b3-6 contour, extended into a 2-bar heroic phrase). Leads: sampled piano / overdriven palm-muted
guitar / staccato strings (FluidR3_GM). Drums/bass/pads/taiko/forge stay synth."""
import numpy as np
from scipy.signal import butter,sosfilt
import crashverse_music as C, crashverse_music_b2 as C2
from crashverse_music import Track,m,hz,SR,rng
def forge(p,dur,vel):  # low anvil/hammer: inharmonic, pitched ~D3-A3 so it never gets shrill
    t=np.arange(int(.9*SR))/SR; f=hz(p)
    y=sum(a*np.sin(2*np.pi*f*r*t)*np.exp(-t*d) for r,a,d in [(1,1,5),(2.41,.55,8),(3.89,.3,12)])
    nz=rng.uniform(-1,1,len(t))*np.exp(-t*60)*.4
    return (y*.5+nz)*vel
def riser(p,dur,vel):  # filtered noise + upward sine sweep (an octave), ends with a cut
    n=int(dur*SR); t=np.arange(n)/SR; r=(t/dur)**2
    nz=sosfilt(butter(2,[300,3500],'band',fs=SR,output='sos'),rng.uniform(-1,1,n))
    sw=np.sin(2*np.pi*np.cumsum(hz(p)*2**(t/dur))/SR)
    return (nz*.6+sw*.4)*r*vel
C.INST.update(forge=forge,riser=riser); C.PAN.update(forge=.25,riser=0); C.GM_PROG.update(forge=14,riser=92)
def anthem(T,beat,inst='hero',root=0,vel=.8,oct=0):
    """Boss anthem hook (2 bars), D minor at root=0: A D F A~ F Bb | A~ G F E D"""
    for b,nn,du in [(0,'A3',.5),(.5,'D4',.5),(1,'F4',.5),(1.5,'A4',1),(2.5,'F4',.5),(3,'A#4',1),
                    (4,'A4',1.5),(5.5,'G4',.5),(6,'F4',.5),(6.5,'E4',.5),(7,'D4',1)]:
        T.n(beat+b,inst,m(nn)+root+12*oct,du,vel)
def ch(c): return [m(x) for x in C.CH[c]]
# ---------------- TITAN: 150 BPM, 32 bars, Dm - Bb - Gm - A (molten forge) ----------------
TB=150; TP=['Dm','Bb','Gm','A']; BARS=32
def titan_layers():
    L={k:Track(f'titan_{k}',TB) for k in ['base','arena_foundry_crater','arena_cinder_bridge','arena_ash_citadel',
        'arena_obsidian_forge','int25','int50','int75']}
    for bar in range(BARS):
        b=bar*4; c=TP[bar%4]; ns=ch(c); r=ns[0]-12; B=L['base']
        for k in range(8): B.d(b+k*.5,'kick',.75 if k%2==0 else .5)            # relentless 8th kick
        B.d(b+1,'snare',.7); B.d(b+3,'snare',.7)
        B.n(b,'taiko',r,.5,.7); B.n(b+2.5,'taiko',r,.5,.5)
        for k in range(8): B.n(b+k*.5,'bass',r,.4,.75)                         # 8th bass pump
        for k in range(16):                                                     # palm-muted 16th chug, accent riff
            acc=k in (0,3,6,10,12)
            B.n(b+k*.25,'chug',r+12+(3 if k in (10,11) else 0),.15,.75 if acc else .45)
        B.n(b,'forge','D3',.5,.7)                                               # anvil on the one
        for p in ns: B.n(b,'pad',p,4,.25)
        if bar%8 in (4,): anthem(B,b,'hero',vel=.8); anthem(B,b,'lowpiano',root=-12,vel=.55)
        if bar%8==0 and bar>=8: anthem(B,b,'hero',vel=.75)
        F=L['arena_foundry_crater']                                             # hammer & anvil foundry
        for k,v in [(1,.6),(3,.6),(3.5,.4)]: F.n(b+k,'forge','A3' if k==3.5 else 'D3',.3,v)
        Cb=L['arena_cinder_bridge']                                             # staccato strings ostinato
        for k in range(8): Cb.n(b+k*.5,'strings',ns[[0,1,2,1][k%4]]+12,.2,.5)
        A=L['arena_ash_citadel']                                                # ash: low drone + low piano tolls
        A.n(b,'pad',r,4,.45); A.n(b,'lowpiano',r,2,.55); A.n(b+2,'lowpiano',r+7,2,.4)
        O=L['arena_obsidian_forge']                                             # big taiko/tom ensemble
        for k,v in [(0,.8),(.75,.5),(1.5,.7),(2,.5),(2.5,.8),(3.25,.5),(3.5,.6),(3.75,.6)]: O.n(b+k,'taiko',r-(12 if k in (0,2.5) else 0),.4,v)
        I1=L['int25']
        for k in range(16): I1.d(b+k*.25,'hat',.55 if k%2 else .3)
        I2=L['int50']                                                           # staccato string counter-ostinato
        for k in range(8): I2.n(b+k*.5+.25,'strings',ns[2]+12 if k%2 else ns[1]+12,.15,.45)
        I3=L['int75']                                                           # snare build + anthem octave + riser
        for k in range(8): I3.d(b+3+k*.125,'snare',.25+.05*k)
        if bar%8==4: anthem(I3,b,'hero',oct=1,vel=.55)
        if bar%8==7: I3.n(b,'riser',r+12,4,.5)
    return [(t,BARS*4,0) for t in L.values()]
# ---------------- TIDEWARDEN: 144 BPM, 12/8 roll (triplet 8ths), Dm - C - Bb - A ----------------
WB=144; WP=['Dm','C','Bb','A']
def tide_bar(T,b,bar,ns,p2):
    r=ns[0]-12
    T.d(b,'kick',.85); T.d(b+1.667,'kick',.6); T.d(b+2,'kick',.8)              # rolling 12/8 kick
    T.d(b+1,'snare',.6); T.d(b+3,'snare',.7)
    T.n(b,'taiko',r,.6,.75); T.n(b+2.667,'taiko',r,.4,.5); T.n(b+3.333,'taiko',r,.4,.55)
    T.n(b,'bass',r,1.5,.85); T.n(b+2,'bass',r,1,.7); T.n(b+3.333,'bass',r+7-12,.6,.6)   # deep watery low end
    for k in range(12):                                                         # triplet chug = tide rolling in
        T.n(b+k/3,'chug',r+12+(7 if k in (5,11) else 0),.2,.7 if k%3==0 else .4)
    T.n(b,'warm',r+12,4,.45); T.n(b,'warm',ns[2],4,.35)                          # swell on each bar
    for p in ns: T.n(b,'pad',p,4,.25)
    if p2:
        for k in range(12): T.d(b+k/3,'hat',.45 if k%3==0 else .25)
        for k in range(6): T.n(b+k*.667,'strings',ns[[0,1,2,1,0,2][k]]+12,.2,.45)
def tide_hook(T,b,bar,p2):
    if bar%8==4 or (p2 and bar%8==0):
        anthem(T,b,'hero',root=0,vel=.8); anthem(T,b,'lowpiano',root=-12,vel=.5)
def tidewarden_p1():
    T=Track('tidewarden_p1',WB)
    for bar in range(4):                                                        # intro: tide rises, lantern ignites
        b=bar*4; T.n(b,'warm','D3',4,.4+.08*bar); T.n(b,'bass','D2',4,.6)
        for k in range(12): T.n(b+k/3,'chug','D3',.2,.25+.12*bar if k%3==0 else .2+.06*bar)
        T.n(b,'taiko','D2',.5,.4+.12*bar)
        if bar==3: T.n(b,'riser','D4',4,.5); [T.d(b+2+k*.125,'snare',.2+.04*k) for k in range(16)]
    for bar in range(32):
        b=16+bar*4; ns=ch(WP[bar%4]); tide_bar(T,b,bar,ns,False); tide_hook(T,b,bar,False)
    return T,16+128,16
def tidewarden_p2():
    T=Track('tidewarden_p2',WB)
    for bar in range(32):
        b=bar*4; ns=ch(WP[bar%4]); tide_bar(T,b,bar,ns,True); tide_hook(T,b,bar,True)
        if bar%8==7: T.n(b,'riser','D4',4,.45)
    return T,128,0
def stinger_boss_phase2():  # riser climaxes at 1.25 s = BOSS_TIDE_PHASE2 core flare (anim table), hit + tail
    T=Track('stinger_boss_phase2',WB,tail=.6)
    hit=1.25*WB/60                                                              # 3.0 beats
    T.n(0,'riser','D4',hit,.7)
    for k in range(12): T.n(k*.25,'chug','D3',.15,.3+.04*k)
    for k in range(8): T.d(hit-1+k*.125,'snare',.25+.06*k)
    T.n(hit,'taiko','D2',1,.95); T.d(hit,'kick',.95); T.n(hit,'bass','D2',1.5,.9); T.n(hit,'forge','D3',.5,.5)
    anthem_head=[('A3',0),('D4',.25),('F4',.5),('A4',.75)]
    for nn,o in anthem_head: T.n(hit+o,'hero',nn,.6,.8); T.n(hit+o,'lowpiano',m(nn)-12,.6,.55)
    T.n(hit+1,'chug','D3',1,.8)
    return T,hit+2,None
