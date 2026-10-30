#!/usr/bin/env python3
"""CRASHVERSE music batch 3 (original, code-synthesised): Tidewarden P1/P2, island gameplay variants,
chapter-clear + results-loop stingers. Usage: python crashverse_music_b3.py build"""
import sys,json,numpy as np,soundfile as sf
import crashverse_music as C, crashverse_music_b2 as C2, crashverse_music_boss  # registers riser/forge
from crashverse_music import Track,m,SR
C.CH.update({'D':['D3','F#3','A3'],'Em':['E3','G3','B2'],'Bm':['B2','D3','F#3'],'E':['E3','G#3','B3']})
def chord(c,root=0): return [m(x)+root for x in C.CH[c]]
def gameplay_variant(name,bpm,key_root,prog,feel,minor=False,intro=2,bars=32,drive='drive'):
    """Shared gameplay template: drums+bass+low pad, sparse high accents, motif only every 8 bars (SFX room)."""
    T=Track(name,bpm); mot=C2.motif_min if minor else C.motif
    for bar in range(intro):  # pickup: drum fill + motif head
        b=bar*4; [T.d(b+k*.5,'snare',.25+.06*k) for k in range(8)] if bar==intro-1 else T.d(b,'kick',.6)
        if bar==0: T.n(b,'pad',chord(prog[0],key_root)[0],8,.4)
    for bar in range(bars):
        b=(intro+bar)*4; c=prog[bar%len(prog)]; ns=chord(c,key_root)
        if bar%4 in (0,2) or drive=='drive': C.beat_bar(T,b,drive if bar>=8 else 'light',vel=.65)
        else: C.beat_bar(T,b,'light',vel=.6)
        T.n(b,'bass',ns[0]-12,1.5,.75); T.n(b+2,'bass',ns[0]-5,1,.6); T.n(b+3.5,'bass',ns[0]-12,.5,.55)
        for p in ns: T.n(b,'pad',p,4,.25)
        feel(T,b,bar,ns)
        if bar%8==4: mot(T,b,'glock',root=key_root+(12 if key_root<0 else 0),vel=.35)
    return T,(intro+bars)*4,intro*4
def harbor_feel(T,b,bar,ns):  # shanty bounce: oom-pah pluck on off-beats + sea-swell pad every 2 bars
    if bar%2==0: T.n(b,'warm',ns[0],8,.35)
    for k in (1,3): [T.n(b+k+j*.015,'pluck',p+12,.35,.35) for j,p in enumerate(ns)]
def mill_feel(T,b,bar,ns):    # folk: claps + banjo-ish roll high
    T.d(b+1,'snare',.3); T.d(b+3,'snare',.3)
    for k in range(4): T.n(b+k+.5,'pluck',ns[k%3]+24,.25,.25)
def clock_feel(T,b,bar,ns):   # clockwork: tick-tock anvil + brass stabs
    for k in range(8): T.n(b+k*.5,'anvil','E7' if k%2 else 'B6',.1,.12)
    if bar%2: T.n(b+2.5,'brass',ns[1],.25,.35)
def storm_feel(T,b,bar,ns):   # rain shaker 16ths, timpani-ish taiko rolls, low thunder piano
    if bar%4==3: T.n(b+2,'lowpiano',ns[0]-12,2,.5)
    for k in range(16): T.d(b+k*.25,'shaker',.15+.1*(k%2))
    if bar%4==3: [T.n(b+2+k*.25,'taiko',ns[0]-12,.2,.25+.07*k) for k in range(8)]
def tide(T,b,bar,ns,p2):
    C2.march_bar(T,b,.75 if not p2 else .85)
    T.n(b,'taiko',ns[0]-12,.5,.6); T.n(b+2.5,'taiko',ns[0]-12,.5,.45)
    T.n(b,'bass',ns[0]-12,1,.85); T.n(b+1.5,'bass',ns[0]-12,.5,.6); T.n(b+2,'bass',ns[0]-5,1,.75); T.n(b+3,'bass',ns[0]-12,.75,.6)
    T.n(b,'glock',ns[0]+12,1.5,.3)                                           # bell toll
    for p in ns: T.n(b,'pad',p+(12 if p2 else 0),4,.3 if not p2 else .4)    # P2: choir-ish pad up an octave
    if bar%8 in (0,4) and bar>=4: C2.motif_min(T,b,'brass',root=2-12,vel=.55)
    if p2:
        [T.d(b+k*.5,'hat',.4+.2*(k%2)) for k in range(8)]
        if bar%4==2: C2.motif_min(T,b,'glock',root=2+12,vel=.45)            # lantern-bell lead
TPRG=['Dm','C','Bb','A']
def tidewarden_p1():
    T=Track('tidewarden_p1',128)
    for bar in range(4):  # intro: bell tolls + taiko swell
        b=bar*4; T.n(b,'glock','D5',2,.5); T.n(b,'pad',m('D3'),4,.35); T.n(b+2,'taiko','D2',.5,.4+.12*bar)
        if bar==3: [T.d(b+k*.25,'snare',.2+.04*k) for k in range(16)]
    for bar in range(32): tide(T,16+bar*4,bar,chord(TPRG[bar%4]),False)
    return T,16+128,16
def tidewarden_p2():
    T=Track('tidewarden_p2',128)
    for bar in range(32): tide(T,bar*4,bar,chord(TPRG[bar%4]),True)
    return T,128,0
def chapter_clear():  # 5 s: motif, then rising major fanfare
    T=Track('stinger_chapter_clear',120)
    C.motif(T,0,'glock',vel=.8); C.motif(T,0,'brass',root=-12,vel=.55)
    for k,nn in enumerate(['C5','E5','G5']): T.n(4+k*.5,'whistle',nn,.5,.6)
    for p in ['C3','G3','C4','E4','G4']: T.n(5.5,'pad',m(p),3.5,.6); T.n(5.5,'brass',m(p),2.5,.35)
    T.n(5.5,'glock','C6',2,.7); T.n(5.5,'bass','C2',2.5,.9); T.d(5.5,'kick',.9); T.d(5.5,'snare',.6)
    [T.d(k,'shaker',.45) for k in range(4)]
    return T,9,None
def results_loop():  # 8-bar low-energy bed, 100 BPM C major
    T=Track('results_loop',100); prog=['C','Am','F','G']
    for bar in range(8):
        b=bar*4; c=prog[bar%4]; ns=chord(c)
        for p in ns: T.n(b,'pad',p+12,4,.35)
        T.n(b,'bass',ns[0]-12,2,.55); T.n(b+2,'bass',ns[0]-5,2,.45)
        for k in range(4): T.n(b+k+.5,'pluck',ns[k%3]+24,.4,.22)
        T.d(b,'kick',.35); [T.d(b+k,'shaker',.25) for k in (1,3)]
    C.motif(T,16,'glock',vel=.35)
    return T,32,0
JOBS=[tidewarden_p1,tidewarden_p2,
 lambda: gameplay_variant('gameplay_harbor_isle',110,7,['C','F','G','C','Am','F','G','G'],harbor_feel),
 lambda: gameplay_variant('gameplay_mill_isle',116,2,['C','G','Am','F','C','F','G','C'],mill_feel),
 lambda: gameplay_variant('gameplay_clocktower_isle',120,0,['Am','F','C','G','Am','F','G','C'],clock_feel,minor=True),
 lambda: gameplay_variant('gameplay_storm_isle',96,2,['Dm','Bb','Gm','A'],storm_feel,minor=True,intro=4,bars=16,drive='light'),
 chapter_clear,results_loop]
from crashverse_music_reward import chapter_clear as _cc, results_loop as _rl
JOBS[-2:]=[_cc,_rl]
from crashverse_music_boss_v3 import tidewarden_p1 as _t1, tidewarden_p2 as _t2
JOBS[0:2]=[_t1,_t2]
if __name__=='__main__':
    out=sys.argv[1]; info={}
    for f in JOBS:
        T,beats,lf=f(); y=T.render(beats,lf)
        sf.write(f'{out}/{T.name}.wav',y.astype(np.float32),SR,subtype='FLOAT'); T.midi(f'{out}/{T.name}.mid')
        info[T.name]={'bpm':T.bpm,'samples':len(y),'loop_start':None if lf is None else int(round(lf*T.spb()*SR)),'loop_end':None if lf is None else len(y)}
        print(T.name,info[T.name],round(len(y)/SR,2),flush=True)
    json.dump(info,open(f'{out}/render_info_b3.json','w'),indent=1)
