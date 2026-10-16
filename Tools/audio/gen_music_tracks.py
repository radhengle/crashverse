#!/usr/bin/env python3
"""Generate Assets/_Project/Resources/Audio/music_tracks.json from Sound Manager's data.
Samples come from audio/music/loop_points.md (authoritative); BPM (and main_theme, which is frozen v1 and
not in loop_points.md) from audio/music/README.md. Run after every music drop: python3 Tools/audio/gen_music_tracks.py [--check]"""
import json, re, sys, pathlib
ROOT = pathlib.Path(__file__).resolve().parents[2]
LP, RD = ROOT/"audio/music/loop_points.md", ROOT/"audio/music/README.md"
OUT = ROOT/"Assets/_Project/Resources/Audio/music_tracks.json"
# Not in the base APK: season2 is a remote download; preview and alt piano are not shipped (Sound Manager, 16 Oct).
EXCLUDED = ["season2_stormfront_hub", "titan_preview_obsidian_full", "main_theme_alt_piano"]
def rid(cell): return pathlib.Path(re.sub(r"[*()]|\bv1\b.*", "", cell).strip().split()[0]).stem
def rows(path):
    for ln in path.read_text().splitlines():
        if ln.startswith("|") and ".ogg" in ln: yield [c.strip() for c in ln.strip("|").split("|")]
def build():
    bpm = {}
    for c in rows(RD):
        if len(c) >= 3 and re.fullmatch(r"\d+(\.\d+)?", c[2]): bpm[rid(c[0])] = float(c[2])
    titan = re.search(r"Titan.*?(\d+) BPM", RD.read_text()); tb = float(titan.group(1))
    tracks = {}
    for c in rows(LP):
        i = rid(c[0]); total = int(c[1]); one = c[2] in ("–", "-")
        tracks[i] = dict(id=i, totalSamples=total, loopStart=0 if one else int(c[2]), loopEnd=total if one else int(c[3]),
                         bpm=tb if i.startswith("titan_") else bpm.get(i, 0.0), oneShot=one)
    m = next(c for c in rows(RD) if rid(c[0]) == "main_theme")
    a, b = [int(x) for x in re.findall(r"\d+", m[5])]
    tracks["main_theme"] = dict(id="main_theme", totalSamples=b, loopStart=a, loopEnd=b, bpm=float(m[2]), oneShot=False)
    for e in EXCLUDED: tracks.pop(e, None)
    return json.dumps({"source": "audio/music/loop_points.md + README.md (generated)", "excluded": EXCLUDED,
                       "tracks": sorted(tracks.values(), key=lambda t: t["id"])}, indent=1) + "\n"
s = build()
if "--check" in sys.argv:
    ok = OUT.exists() and OUT.read_text() == s; print("music_tracks.json " + ("up to date" if ok else "STALE: rerun gen_music_tracks.py")); sys.exit(0 if ok else 1)
OUT.write_text(s); print("wrote", OUT)
