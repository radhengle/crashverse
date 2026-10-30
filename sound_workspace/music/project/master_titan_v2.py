#!/usr/bin/env python3
"""Titan v2 (Android 3-voice rule). From batch-2 build stems (build/titan_*.wav, float, unity gain):
 - titan_<arena>.ogg x4 = base + arena pre-mixed
 - titan_int25/50/75.ogg = CUMULATIVE intensity layers (50 = 25+50, 75 = 25+50+75); only one plays at a time
One shared linear gain for all 7 files, chosen so the worst of {arena mix + any single intensity} and
{arena mix + equal-power crossfade midpoint 0.707*(a+b)} is <= -1.2 dBTP. Also renders titan_preview_obsidian_full.ogg."""
import os,json,numpy as np,soundfile as sf
from master_b2 import enc,loopcheck,tmp_meas,B
from master import meas
OUT='../titan'; os.makedirs(OUT,exist_ok=True)
r=lambda k: sf.read(f'{B}/titan_{k}.wav')[0]
base=r('base'); arenas=['foundry_crater','cinder_bridge','ash_citadel','obsidian_forge']
mix={a:base+r('arena_'+a) for a in arenas}
i25,i50,i75=r('int25'),r('int50'),r('int75'); cum={'int25':i25,'int50':i25+i50,'int75':i25+i50+i75}
def combos():
    for a in arenas:
        yield f'{a}',mix[a]
        for k,v in cum.items(): yield f'{a}+{k}',mix[a]+v
        for x,y in [('int25','int50'),('int50','int75'),('int75','int50')]:
            yield f'{a}+xfade({x}->{y})',mix[a]+.7071*(cum[x]+cum[y])
I,_=tmp_meas(mix['foundry_crater']); g=10**((-16-I)/20)
for it in range(8):
    worst=max(tmp_meas(v*g)[1] for _,v in combos())
    if worst<=-1.2: break
    g*=10**((-1.3-worst)/20)
rep={'_shared_gain_db':round(20*np.log10(g),2),'_worst_TP_any_combo':worst}; dec={}
for name,x in [(f'titan_{a}',mix[a]) for a in arenas]+[(f'titan_{k}',v) for k,v in cum.items()]:
    ogg,z=enc(x*g,name,OUT); I,tp=meas(ogg); dec[name]=z
    q={'bpm':128,'samples_ogg':len(z),'dur_s':round(len(z)/44100,3),'loop_start':0,'loop_end':len(z),'LUFS_ogg':I,'TP_ogg':tp}
    loopcheck(q,'ogg',z,0); rep[name]=q; print(name,q,flush=True)
mixes={}
for a in arenas:
    for k in cum:
        z=dec[f'titan_{a}']+dec[f'titan_{k}']; I,tp=tmp_meas(z); q={'LUFS':I,'TP':tp}; loopcheck(q,'ogg',z,0); mixes[f'{a}+{k}']=q
rep['_mixes_decoded']=mixes; print(json.dumps(mixes))
# preview: 60 s, obsidian, 0 -> 25 (15 s) -> 50 (30 s) -> 75 (45 s), 1 s equal-power crossfades, 2 s fade-out
N=60*44100; tile=lambda z: np.concatenate([z,z])[:N].astype(np.float64)   # loop is shorter than 60 s: play it looped
dec={k:(tile(v) if len(v)<N else v) for k,v in dec.items()}
m=dec['titan_obsidian_forge'][:N].astype(np.float64); lay=np.zeros_like(m)
steps=[(15,'titan_int25'),(30,'titan_int50'),(45,'titan_int75')]; F=44100; prev=None
for i,(t,k) in enumerate(steps):
    s=t*44100; e=steps[i+1][0]*44100 if i+1<len(steps) else N; th=np.linspace(0,np.pi/2,F)[:,None]
    seg=dec[k][s:e].astype(np.float64).copy(); seg[:F]*=np.sin(th)
    if prev is not None: seg[:F]+=dec[prev][s:s+F]*np.cos(th)
    lay[s:e]=seg; prev=k
p=m+lay; p[-2*44100:]*=np.linspace(1,0,2*44100)[:,None]
ogg,z=enc(p,'titan_preview_obsidian_full',OUT); I,tp=meas(ogg)
rep['titan_preview_obsidian_full']={'dur_s':round(len(z)/44100,3),'LUFS_ogg':I,'TP_ogg':tp,'steps_s':[0,15,30,45]}
print(rep['titan_preview_obsidian_full'],rep['_shared_gain_db'],worst)
json.dump(rep,open('master_report_titan_v2.json','w'),indent=1)
