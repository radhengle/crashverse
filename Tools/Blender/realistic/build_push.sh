#!/bin/bash
# Realistic v1: build each L1-10 + Daily Boss asset (most-used first), validate, commit and push (fast-forward) after each one.
# usage: Tools/Blender/realistic/build_push.sh [asset ...]   (default: order from models/Realistic_Scope_L1-10.json)
set -e; cd "$(dirname "$0")/../../.."; BL=${BL:-/opt/blender-4.2.3-linux-x64/blender}; R=Assets/_Project/Models/Realistic
[ -d /tmp/kitsrc/Kit_Brick ] || KIT_GLB=/tmp/kitglb $BL -b -P Tools/Blender/build_kit2.py -- /tmp/kitsrc Brick,Steel,Wood Block >/dev/null 2>&1
A=("$@"); [ ${#A[@]} -gt 0 ] || A=($(python3 -c "import json;print(' '.join(json.load(open('models/Realistic_Scope_L1-10.json'))['order']))"))
mkdir -p models/validation_realistic
for a in "${A[@]}"; do
  $BL -b -P Tools/Blender/realistic/resurf.py -- $a 2>&1 | grep "^DONE\|steel faces\|Error\|Traceback" || true
  k=$(echo $a | sed -E 's/Kit_Real([A-Za-z]+)_.*/\1/'); D=$(ls -d $R/*/Kit_Real$k | head -1)
  LOG=models/validation_realistic/Kit_Real$k.txt
  { $BL -b -P Tools/Blender/validate_fracture.py -- $D; $BL -b -P Tools/Blender/validate_colliders.py -- $D; $BL -b -P Tools/Blender/validate_meshdata.py -- $D; } 2>&1 | grep -v "^Blender\|^$\|FBX version\|quit\|Import finished\|^Read" > $LOG
  $BL -b -P Tools/Blender/realism/validate_realism.py -- $R 2>&1 | grep "Kit_Real$k\|REALISM RESULT" -A2 >> $LOG
  if grep -q "FAIL" $LOG; then echo "VALIDATION FAILED $a"; cat $LOG; exit 1; fi
  git add -A $D models/HeroBuildings/Kit_Real${k}_stats.json $LOG
  git commit -q -m "realistic v1: $a (kit v3.1 geometry/fracture re-surfaced with graded CC0; validators pass)" && git push -q origin HEAD:3d-modeler/realistic-v1 && echo "PUSHED $a $(git rev-parse --short HEAD)"
done
