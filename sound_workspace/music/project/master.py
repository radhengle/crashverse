#!/usr/bin/env python3
"""Normalise each build/*.wav to -16 LUFS (measured by ffmpeg ebur128), memoryless soft-limit
(keeps loops sample-continuous), write 16-bit WAV master, encode OGG Vorbis q5, verify."""
import json,subprocess,re,sys,numpy as np,soundfile as sf
B='build'; OUT='..'
def meas(path):
    r=subprocess.run(['ffmpeg','-hide_banner','-nostats','-i',path,'-af','ebur128=peak=true','-f','null','-'],capture_output=True,text=True).stderr
    s=r[r.rfind('Summary:'):]
    return float(re.search(r'I:\s+(-?[\d.]+|-inf)',s).group(1)),float(re.search(r'Peak:\s+(-?[\d.]+|-inf)',s).group(1))
def lim(x,c=0.75):  # linear below c, tanh knee above, ceiling ~ -1 dBFS sample peak
    a=np.abs(x); k=1-c; y=np.where(a<c,a,c+k*np.tanh((a-c)/k)); return np.sign(x)*y*0.78/ (c+k)
if __name__=='__main__':
    info=json.load(open(f'{B}/render_info.json'))
    rep={}
    for name,inf in info.items():
        x,_=sf.read(f'{B}/{name}.wav'); g=1.0
        for it in range(6):
            y=lim(x*g); sf.write(f'{B}/{name}_m.wav',y,44100,subtype='FLOAT')
            I,tp=meas(f'{B}/{name}_m.wav'); 
            if abs(I+16)<0.3: break
            g*=10**((-16-I)/20)
        sf.write(f'{B}/{name}_m16.wav',y,44100,subtype='PCM_16')
        # main_theme.ogg (v1) is USER-APPROVED and must stay byte-identical: v2 renders go to main_theme_alt_piano.ogg
        ogg=f'{OUT}/{name}_alt_piano.ogg' if name=='main_theme' else f'{OUT}/{name}.ogg'
        subprocess.run(['ffmpeg','-y','-loglevel','error','-i',f'{B}/{name}_m.wav','-c:a','libvorbis','-q:a','5','-ar','44100','-ac','2',ogg],check=True)
        I2,tp2=meas(ogg)
        d=subprocess.run(['ffmpeg','-loglevel','error','-i',ogg,'-f','f32le','-'],capture_output=True).stdout
        z=np.frombuffer(d,np.float32).reshape(-1,2)
        r={'bpm':inf['bpm'],'samples_master':len(y),'samples_ogg':len(z),'dur_s':round(len(y)/44100,3),
           'loop_start':inf['loop_start'],'loop_end':inf['loop_end'],'LUFS_master':I,'TP_master':tp,'LUFS_ogg':I2,'TP_ogg':tp2}
        if inf['loop_start'] is not None:
            ls=inf['loop_start']
            for lab,w in [('master',y),('ogg',z)]:
                j=abs(w[-1]-w[ls]).max(); typ=np.percentile(np.abs(np.diff(w,axis=0)).max(1),99)
                r[f'boundary_jump_{lab}']=round(float(j),5); r[f'p99_step_{lab}']=round(float(typ),5)
        rep[name]=r; print(name,r,flush=True)
    json.dump(rep,open('master_report.json','w'),indent=1)
