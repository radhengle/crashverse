#!/usr/bin/env python3
"""Batch 2 mastering. Standalone tracks: as master.py (-16 LUFS, memoryless soft limit, OGG q5).
Titan stem group: ONE shared linear gain for all stems (keeps stem balance + sample alignment):
base -> ~-16 LUFS, then lowered if the worst-case full mix (base+arena+int25+int50+int75) exceeds -1 dBTP.
Titan group moved to master_titan_v2.py (v2, pre-mixed arenas). main() also used by batch 3."""
import json,subprocess,re,numpy as np,soundfile as sf
from master import meas,lim
B='build'
def enc(y,name,outdir='..'):
    sf.write(f'{B}/{name}_m.wav',y.astype(np.float32),44100,subtype='FLOAT')
    ogg=f'{outdir}/{name}.ogg'
    subprocess.run(['ffmpeg','-y','-loglevel','error','-i',f'{B}/{name}_m.wav','-c:a','libvorbis','-q:a','5','-ar','44100','-ac','2',ogg],check=True)
    d=subprocess.run(['ffmpeg','-loglevel','error','-i',ogg,'-f','f32le','-'],capture_output=True).stdout
    return ogg,np.frombuffer(d,np.float32).reshape(-1,2)
def loopcheck(r,lab,w,ls):
    r[f'boundary_jump_{lab}']=round(float(abs(w[-1]-w[ls]).max()),5)
    r[f'p99_step_{lab}']=round(float(np.percentile(np.abs(np.diff(w,axis=0)).max(1),99)),5)
def tmp_meas(y):
    sf.write(f'{B}/_t.wav',y.astype(np.float32),44100,subtype='FLOAT'); return meas(f'{B}/_t.wav')
# --- Titan group ---
def main(info_file='render_info_b2.json',report='master_report_b2.json'):
    info=json.load(open(f'{B}/{info_file}')); rep={}
    # --- standalone ---
    for name,inf in info.items():
        if inf.get('group'): continue  # Titan stems -> master_titan_v2.py
        x,_=sf.read(f'{B}/{name}.wav'); g=1.0
        for it in range(6):
            y=lim(x*g); I,tp=tmp_meas(y)
            if abs(I+16)<0.3: break
            g*=10**((-16-I)/20)
        ogg,z=enc(y,name); I2,tp2=meas(ogg)
        r={'bpm':inf['bpm'],'samples_master':len(y),'samples_ogg':len(z),'dur_s':round(len(y)/44100,3),'loop_start':inf['loop_start'],'loop_end':inf['loop_end'],'LUFS_ogg':I2,'TP_ogg':tp2}
        if inf['loop_start'] is not None: loopcheck(r,'master',y,inf['loop_start']); loopcheck(r,'ogg',z,inf['loop_start'])
        rep[name]=r; print(name,r,flush=True)
    json.dump(rep,open(report,'w'),indent=1)
if __name__=='__main__':
    import sys; main(*sys.argv[1:])
