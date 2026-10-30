#!/usr/bin/env python3
"""CRASHVERSE v2 win/reward family: warm sampled piano + fingerpicked nylon arpeggios + soft pad swell
(FluidR3_GM: 0 Acoustic Grand, 24 Nylon Gtr, 89 Warm Pad) with lush reverb, IV-V-I with add9/maj7 colour.
Background (bass/kick/shaker/synth pad) stays synth. Shared motif kept (5-1-3-5 | 3-6 contour)."""
import crashverse_music as C, crashverse_music_b2  # registers taiko/brass/anvil
from crashverse_music import Track,m
def motif_piano(T,beat,root=0,vel=.75,rh=1.0):
    for b,nn,du in [(0,'G4',.5),(.5,'C5',.5),(1,'E5',.5),(1.5,'G5',1),(2.5,'E5',.5),(3,'A5',1)]:
        T.n(beat+b*rh,'piano',m(nn)+root,du*rh,vel)
def arp(T,beat,notes,step=.25,vel=.45,dur=1.0):  # fingerpicked nylon pattern
    for k,nn in enumerate(notes): T.n(beat+k*step,'nylon',nn,dur,vel*(1-.04*k))
def swell(T,beat,notes,dur,vel=.5):
    for nn in notes: T.n(beat,'warm',nn,dur,vel)
IV=['F3','A3','C4','G4']; V=['G3','B3','D4','A4']; Imaj9=['C3','G3','B3','D4','E4']
def stinger_win():                      # 4.0 s: motif over IV->V, resolve to Cmaj9 with rolled arpeggio
    T=Track('stinger_win',100,tail=1.0)
    swell(T,0,['F3','C4','A4'],2,.45); swell(T,2,['G3','D4','B4'],1,.45); swell(T,3,['C3','G3','E4','B4'],2,.55)
    motif_piano(T,0,rh=.75)
    arp(T,0,['F3','C4','G4','A4'],.25,.4); arp(T,2,['G3','D4','A4'],.25,.4)
    arp(T,3,['C3','G3','D4','E4','B4','D5'],.12,.5,1.8)                       # rolled Cmaj9 bloom
    for nn in Imaj9: T.n(3,'piano',nn,2,.55)
    T.n(0,'bass','F2',2,.6); T.n(2,'bass','G2',1,.6); T.n(3,'bass','C2',2,.75); T.d(3,'kick',.5)
    return T,5,None
def stinger_win3():                     # 4.8 s: win + second climbing bloom (Fmaj7 -> G6 -> Cadd9/maj9)
    T=Track('stinger_win_3star',100,tail=1.2)
    swell(T,0,['F3','C4','A4'],2,.45); swell(T,2,['G3','D4','B4'],1,.45); swell(T,3,['C3','G3','E4','B4','D5'],3,.55)
    motif_piano(T,0,rh=.75)
    arp(T,0,['F3','C4','E4','A4'],.25,.4); arp(T,2,['G3','D4','E4'],.25,.4)
    arp(T,3,['C3','G3','D4','E4','B4','D5'],.12,.5,2.5)
    for nn in Imaj9: T.n(3,'piano',nn,3,.55)
    for k,nn in enumerate(['E5','G5','B4','D5','E5','G5']): T.n(4+k*.25,'piano',m(nn)-12 if k>3 else m(nn),.8,.35)  # three-star shimmer, kept <= G5
    T.n(0,'bass','F2',2,.6); T.n(2,'bass','G2',1,.6); T.n(3,'bass','C2',3,.75); T.d(3,'kick',.5)
    return T,6,None
def chapter_clear():                    # 5.1 s: motif, IV-V-vi-IV-V-I journey, big Cmaj9 bloom
    T=Track('stinger_chapter_clear',100,tail=1.0)
    motif_piano(T,0,rh=.75)
    for b,ch,ns in [(0,'F',IV),(1.5,'G',V),(3,'Am',['A2','E3','B3','C4']),(4,'F',['F3','C4','E4','A4']),(5,'G',['G3','D4','F4','A4'])]:
        swell(T,b,ns[:3],1.5,.45); arp(T,b,ns,.25,.38); T.n(b,'bass',ns[0][:-1]+'2',1.5,.6)
    T.n(3,'piano','C5',.5,.6); T.n(3.5,'piano','E5',.5,.6); T.n(4,'piano','A4',1,.6); T.n(5,'piano','B4',1,.6)
    swell(T,6,['C3','G3','E4','B4','D5'],2,.6); arp(T,6,['C3','G3','D4','E4','B4','D5','E5'],.12,.5,2.2)
    for nn in Imaj9: T.n(6,'piano',nn,2,.6)
    T.n(6,'bass','C2',2,.8); T.d(6,'kick',.55)
    return T,7,None
def stinger_boss_defeat():              # 6.2 s: minor motif (Dm) -> IV-V-I in D major, Dmaj9 bloom
    T=Track('stinger_boss_defeat',144,tail=1.2)
    for k in range(16): T.n(k*.25,'chug','D3',.15,.35+.03*k)
    T.n(0,'taiko','D2',.5,.7); T.n(2,'taiko','D2',.5,.6); T.n(4,'taiko','G1',.5,.8); T.n(6,'taiko','A1',.5,.8)
    for k in range(8): T.n(4+k*.5,'lowpiano',['G2','G2','D3','G2','A2','A2','E3','A2'][k],.4,.5)
    for b,nn,du in [(0,'A3',.5),(.5,'D4',.5),(1,'F4',.5),(1.5,'A4',1),(2.5,'F4',.5),(3,'B4',1)]: T.n(b,'piano',nn,du,.7)
    swell(T,0,['D3','A3','F4'],4,.4); arp(T,0,['D3','A3','D4','F4'],.5,.35,1.5); T.n(0,'bass','D2',4,.6)
    for k in range(8): T.d(3+k*.125,'snare',.2+.04*k)
    # victory: major motif over G(add9) -> A(sus->maj)
    for b,nn,du in [(4,'A4',.5),(4.5,'D5',.5),(5,'F#5',.5),(5.5,'A5',1),(6.5,'F#5',.5),(7,'B5',1)]: T.n(b,'piano',m(nn)-12 if m(nn)>79 else nn,du,.75)
    swell(T,4,['G3','D4','A4','B4'],2,.45); arp(T,4,['G3','D4','A4','B4'],.25,.4); T.n(4,'bass','G2',2,.65)
    swell(T,6,['A3','E4','C#5'],2,.45);     arp(T,6,['A3','E4','B4','C#5'],.25,.4); T.n(6,'bass','A2',2,.65)
    swell(T,8,['D3','A3','F#4','C#5','E5'],4,.6); arp(T,8,['D3','A3','E4','F#4','C#5','E5'],.15,.5,3)
    for nn in ['D3','A3','C#4','E4','F#4']: T.n(8,'piano',nn,3.5,.6)
    T.n(8,'bass','D2',3.5,.85); T.d(8,'kick',.7); T.n(8,'taiko','D2',1,.5)
    return T,10.5,None
def results_loop():                     # 8-bar bed: background unchanged (pad/bass/kick/shaker); leads now nylon + piano
    T=Track('results_loop',100); prog=['C','Am','F','G']
    colour={'C':['C4','G4','D5','E5'],'Am':['A3','E4','G4','C5'],'F':['F3','C4','E4','A4'],'G':['G3','D4','F4','A4']}
    for bar in range(8):
        b=bar*4; c=prog[bar%4]; ns=[m(x) for x in C.CH[c]]
        for p in ns: T.n(b,'pad',p+12,4,.35)
        T.n(b,'bass',ns[0]-12,2,.55); T.n(b+2,'bass',ns[0]-5,2,.45)
        T.d(b,'kick',.35); [T.d(b+k,'shaker',.25) for k in (1,3)]
        cl=colour[c]; [T.n(b+k*.5,'nylon',cl[[0,1,2,3,2,1,2,3][k]],1.2,.32) for k in range(8)]   # fingerpicked maj7/add9 colour
    motif_piano(T,16,vel=.45); T.n(28,'piano','E5',4,.35); T.n(28,'piano','B4',4,.3)
    return T,32,0
