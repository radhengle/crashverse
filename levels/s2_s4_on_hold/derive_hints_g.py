#!/usr/bin/env python3
"""Gravity-parameterised wrapper around the UNCHANGED integration/v2 Tools/ContentGen/derive_hints.py (GD S2-S4 ruling #10:
derive hints at gravity x1.2). Imports the original module and calls its derive(levels, layouts_dir, v, g) with g = 9.81*scale.
Usage: derive_hints_g.py data/chapterN.json --layouts data/Layouts [--gravity 1.2] [--check]"""
import importlib.util, json, os, sys
HERE=os.path.dirname(os.path.abspath(__file__))
spec=importlib.util.spec_from_file_location("derive_hints",os.path.join(HERE,"_derive/Tools/ContentGen/derive_hints.py"))
dh=importlib.util.module_from_spec(spec); spec.loader.exec_module(dh)
def main():
    a=sys.argv[1:]; chap=[x for i,x in enumerate(a) if not x.startswith("--") and (i==0 or a[i-1] not in ("--layouts","--gravity"))][0]
    lay=a[a.index("--layouts")+1]; gs=float(a[a.index("--gravity")+1]) if "--gravity" in a else 1.2
    v=json.load(open(os.path.join(HERE,"_derive/Assets/_Project/Resources/Data/wrecker.json"))).get("launchSpeed",28.0)
    doc=json.load(open(chap)); res=dh.derive(doc["levels"],lay,v,9.81*gs); stale=[]
    for L in doc["levels"]:
        if L["id"] in res and (L.get("hintYaw"),L.get("hintPitch"))!=res[L["id"]]:
            stale.append((L["id"],(L.get("hintYaw"),L.get("hintPitch")),res[L["id"]])); L["hintYaw"],L["hintPitch"]=res[L["id"]]
    if "--check" in a:
        for x in stale: print("STALE",*x)
        print(f"hints up to date (g x{gs})" if not stale else f"{len(stale)} stale"); sys.exit(1 if stale else 0)
    json.dump(doc,open(chap,"w"),indent=1)
    for x in stale: print("updated",*x)
    print(f"{len(stale)} hint(s) updated at g x{gs}")
if __name__=="__main__": main()
