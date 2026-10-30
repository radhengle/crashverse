#!/usr/bin/env python3
"""CRASHVERSE music batch 2 (original, code-synthesised). Titan base + stems, season hubs, stingers.
Usage: python crashverse_music_b2.py build   -> build/*.wav (float), build/*.mid, build/render_info_b2.json
Stem groups: every file in a group has the identical sample length and loop wrap, so they are sample-aligned."""
import sys,json,numpy as np, soundfile as sf
import crashverse_music as C
from crashverse_music import Track,m,hz,SR,env,rng
# extra instruments
def brass(p,dur,vel):  # toy brass: band-limited saw-ish, few harmonics (stays out of 2-5 kHz mostly)
    n=int((dur+.12)*SR); t=np.arange(n)/SR; f=hz(p)
    y=sum(np.sin(2*np.pi*f*k*t)/k**1.3 for k in range(1,7) if f*k<3000)
    return y*env(n,.03,.1,.7,.1,dur)*vel*.45
def anvil(p,dur,vel):  # metallic toy clank, inharmonic
    t=np.arange(int(.7*SR))/SR; f=hz(p)
    y=sum(a*np.sin(2*np.pi*f*r*t)*np.exp(-t*d) for r,a,d in [(1,1,6),(2.41,.6,9),(3.89,.4,12),(5.3,.25,18)])
    return y*vel*.35
def taiko_(p,dur,vel):
    t=np.arange(int(.6*SR))/SR; f=hz(p)*(1+.5*np.exp(-t*30))
    return np.sin(2*np.pi*np.cumsum(f)/SR)*np.exp(-t*6)*vel*.9
C.INST.update(brass=brass,anvil=anvil,taiko=taiko_)
C.PAN.update(brass=-.15,anvil=.45,taiko=-.1)
C.GM_PROG.update(brass=61,anvil=14,taiko=116)
C.CH.update({'Bb':['A#2','D3','F3'],'Gm':['G2','A#2','D3'],'A':['A2','C#3','E3'],'D':['D3','F#3','A3'],
             'Cm':['C3','D#3','G3'],'Bm':['B2','D3','F#3'],'Eb':['D#3','G3','A#3']})
def motif_min(T,beat,inst,root=0,vel=.8,oct=0,rhythm=1.0):
    """Toybox Hook in minor: 5-1-b3-5 | b3-6 (same contour as batch 1)"""
    for b,nn,du in [(0,'G4',.5),(.5,'C5',.5),(1,'D#5',.5),(1.5,'G5',1),(2.5,'D#5',.5),(3,'A5',1)]:
        T.n(beat+b*rhythm,inst,m(nn)+root+12*oct,du*rhythm,vel)
D=2  # transpose C->D
TP=['Dm','Bb','Gm','A']; BARS=32; BPM=128
def march_bar(T,b,v=.75):
    T.d(b,'kick',v); T.d(b+2,'kick',v*.9)
    T.d(b+1,'snare',v*.6); T.d(b+3,'snare',v*.6); T.d(b+3.75,'snare',v*.35)
def titan_layers():
    L={k:Track(f'titan_{k}',BPM) for k in ['base','arena_foundry_crater','arena_cinder_bridge','arena_ash_citadel',
        'arena_obsidian_forge','int25','int50','int75']}
    for bar in range(BARS):
        b=bar*4; c=TP[bar%4]; ns=[m(x) for x in C.CH[c]]; B=L['base']
        march_bar(B,b)
        B.n(b,'bass',ns[0]-12,1,.85); B.n(b+1,'bass',ns[0],.5,.6); B.n(b+2,'bass',ns[0]-12,1,.8); B.n(b+3,'bass',ns[0]-5,.75,.6)
        for p in ns: B.n(b,'pad',p,4,.35)
        if bar%8 in (0,4) and bar>=8: motif_min(B,b,'brass',root=D-12,vel=.55)
        # arena stems
        F=L['arena_foundry_crater']; F.n(b+1,'anvil','D6',.25,.6); F.n(b+3,'anvil','D6',.25,.5); F.n(b+3.5,'anvil','A5',.25,.35)
        Cb=L['arena_cinder_bridge']
        for k in range(8): Cb.n(b+k*.5,'pluck',ns[k%3]+24+(12 if k==7 else 0),.4,.35)
        A=L['arena_ash_citadel']; A.n(b,'pad',ns[0]-12,4,.45); A.n(b,'pad',ns[0]+7-12,4,.3)
        for k in range(8): A.d(b+k*.5+.25,'shaker',.35)
        O=L['arena_obsidian_forge']; [O.n(b+k,'taiko','D2',.5,.7 if k in(0,2.5) else .45) for k in (0,1.5,2.5,3)]
        if bar%2: O.n(b+3.5,'brass',ns[0]-12,.4,.5)
        # intensity
        I1=L['int25']; [I1.d(b+k*.25,'hat',.55 if k%2 else .3) for k in range(16)]
        I2=L['int50']
        if bar%4==2: motif_min(I2,b,'brass',root=D,vel=.45)
        I2.n(b,'brass',ns[1],.5,.4); I2.n(b+2.5,'brass',ns[2],.5,.4)
        I3=L['int75']; [I3.d(b+3+k*.125,'snare',.25+.05*k) for k in range(8)]
        [I3.n(b+k,'taiko','A2',.4,.5) for k in (.5,1.5)]
        if bar%4==0: motif_min(I3,b,'glock',root=D+12,vel=.4)
    return [(t,BARS*4,0) for t in L.values()]
def season1():  # 'The Wrecking Frontier' -- frontier gallop, F major 116
    T=Track('season1_wrecking_frontier_hub',116); prog=['F','C','Bb','C','F','Dm','Bb','C']
    for r in range(4):
        for i,c in enumerate(prog):
            b=r*32+i*4; ns=[m(x) for x in C.CH[c]]
            T.d(b,'kick',.6); T.d(b+2,'kick',.5)
            for k in (0,.75,1,2,2.75,3): T.d(b+k,'shaker',.4)      # gallop
            T.n(b,'bass',ns[0]-12,1,.75); T.n(b+2,'bass',ns[0]-5,1,.65)
            for k in (1,3):
                for j,p in enumerate(ns): T.n(b+k+j*.02,'pluck',p+12,.4,.35)
        C.motif(T,r*32+(8 if r%2 else 0),'whistle',root=5-12+12,vel=.55)
        C.motif(T,r*32+16,'glock',root=5,vel=.45)
        if r%2: T.n(r*32+24,'anvil','F5',.25,.4)
    return T,128,0
def season2():  # 'Stormfront' -- E minor 100, rain shaker, thunder taiko
    T=Track('season2_stormfront_hub',100); prog=['Em','C','Am','Bm']
    C.CH.setdefault('Em',['E3','G3','B3'])
    for bar in range(32):
        b=bar*4; c=prog[bar%4]; ns=[m(x) for x in C.CH[c]]
        for p in ns: T.n(b,'pad',p,4,.4)
        for k in range(16): T.d(b+k*.25,'shaker',.2+.15*(k%2))
        T.n(b,'bass',ns[0]-12,2,.7); T.n(b+2.5,'bass',ns[0]-12,1,.55)
        if bar%8==7: T.n(b+2,'taiko','E2',1,.8); T.n(b+3,'taiko','E2',1,.6)
        if bar%8 in (2,6): motif_min(T,b,'glock',root=4-12+12,vel=.45)
        if bar%16==12: motif_min(T,b,'whistle',root=4,vel=.45)
    return T,128,0
def stinger_win3():
    T=Track('stinger_win_3star',132)
    C.motif(T,0,'glock',vel=.8); C.motif(T,0,'whistle',vel=.6)
    for p in ['C4','E4','G4','C5']: T.n(4,'pad',p,3.5,.7)
    T.n(4,'bass','C2',2,.9); T.d(4,'kick',.9); T.d(4,'snare',.6)
    for k,nn in enumerate(['C6','E6','G6','C7','G6','C7']): T.n(4+k*.33,'glock',nn,.6,.5)   # sparkle
    for k in range(4): T.d(k,'shaker',.5)
    return T,8,None
def stinger_boss_defeat():
    T=Track('stinger_boss_defeat',128)
    motif_min(T,0,'brass',root=D-12,vel=.7); [march_bar(T,b) for b in (0,)]
    for k in range(8): T.d(3+k*.125,'snare',.3+.06*k)
    C.motif(T,4,'brass',root=D-12,vel=.75); C.motif(T,4,'glock',root=D,vel=.6)   # minor -> major: victory
    for p in ['D3','F#3','A3','D4']: T.n(8,'pad',m(p),4.5,.6); T.n(8,'brass',m(p),3,.45)
    T.n(8,'bass','D2',3,.9); T.d(8,'kick',.9); T.n(8,'taiko','D2',1,.8); T.n(8,'anvil','D6',.3,.5)
    return T,12,None
def stinger_phase2():
    T=Track('stinger_boss_phase2',128)
    motif_min(T,0,'brass',root=D-12,vel=.7,rhythm=.5)
    for k in range(8): T.d(2+k*.125,'snare',.3+.06*k)
    T.n(2,'taiko','D2',1,.8); T.n(3,'taiko','D2',1,.9); T.n(3,'bass','D2',1.2,.8); T.n(3,'anvil','D#6',.3,.45)
    return T,4,None
if __name__=='__main__':
    out=sys.argv[1]; info={}
    from crashverse_music_reward import stinger_win3,stinger_boss_defeat
    from crashverse_music_boss_v3 import titan_layers,stinger_boss_phase2 as stinger_phase2,stinger_tidewarden_defeat,stinger_titan_defeat
    jobs=[(x,'titan') for x in titan_layers()]+[(f(),None) for f in (season1,season2,stinger_win3,stinger_boss_defeat,stinger_phase2,stinger_tidewarden_defeat,stinger_titan_defeat)]
    for (T,beats,lf),grp in jobs:
        y=T.render(beats,lf); sf.write(f'{out}/{T.name}.wav',y.astype(np.float32),SR,subtype='FLOAT'); T.midi(f'{out}/{T.name}.mid')
        info[T.name]={'bpm':T.bpm,'samples':len(y),'group':grp,'loop_start':None if lf is None else int(round(lf*T.spb()*SR)),'loop_end':None if lf is None else len(y)}
        print(T.name,info[T.name],flush=True)
    json.dump(info,open(f'{out}/render_info_b2.json','w'),indent=1)
