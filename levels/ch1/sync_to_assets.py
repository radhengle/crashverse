#!/usr/bin/env python3
"""Publish Level Designer Ch1 data into the game's Resources (rev 6 pipeline, run from levels/ch1):
  1) gen_levels.py      -> layouts/*.json + chapter1_levels.json
  2) copy layouts       -> Assets/_Project/Resources/Data/Layouts/
  3) chapter1.json      -> its "levels" array replaced by chapter1_levels.json (id/name/nextRegion/islands untouched)
  4) Tools/ContentGen/derive_hints.py (gravity x1.0 = live GameSession gravity and the CI --check) writes hintYaw/hintPitch
  5) gen_levels.py again so chapter1_levels.json carries the derived hint angles
Then run sim.py, ci_win_shots.py, frame_check.py and mkdoc.py."""
import json, os, shutil, subprocess, sys
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(HERE,'../..'))
DATA=os.path.join(ROOT,'Assets/_Project/Resources/Data')
run=lambda *a: subprocess.run([sys.executable,*a],cwd=HERE,check=True)
run('gen_levels.py')
for f in os.listdir(os.path.join(HERE,'layouts')): shutil.copyfile(os.path.join(HERE,'layouts',f),os.path.join(DATA,'Layouts',f))
p=os.path.join(DATA,'chapter1.json'); ch=json.load(open(p))
ch['levels']=json.load(open(os.path.join(HERE,'chapter1_levels.json')))['levels']
json.dump(ch,open(p,'w'),indent=1)
subprocess.run([sys.executable,os.path.join(ROOT,'Tools/ContentGen/derive_hints.py')],cwd=ROOT,check=True)
run('gen_levels.py')
print('synced')
