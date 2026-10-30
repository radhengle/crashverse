# Realism-test validator (locked budgets 2026-10-24: Graphics/PM/Art). usage: blender -b -P validate_realism.py -- <Realistic root>
# Checks per FBX: names, LOD set + tris per class, materials/submeshes per LOD, UV0+UV2, vertex colour, UCX hulls,
# texture set T_<Asset>_A/_N/_ORM present (power of two, <=2048 authored), ORM layout sanity (R=AO >=0.5, B=metal 0/1).
import bpy,sys,os,glob,re
R=sys.argv[sys.argv.index("--")+1]; bad=0; rows=[]
LOD=re.compile(r"^(?P<b>.+)_LOD(?P<l>\d)$")
def tri(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
def rules(rel,base):
    if rel.startswith("Buildings/Hero/"): return {"lods":{0,1,2},"max":{0:20000,1:10000,2:4000},"mats":2}
    if rel.startswith("Buildings/"): return {"lods":{0,1,2},"max":{0:8000,1:4000,2:1600},"mats":2}
    if rel.startswith("Foliage/Bush"): return {"lods":{0,1,2,3},"max":{0:2500,1:1250,2:400,3:8},"mats":1}
    if rel.startswith("Foliage/GrassClump"): return {"lods":{0,1},"max":{0:12,1:8},"mats":1}
    if rel.startswith("Foliage/"): return {"lods":{0,1,2,3},"max":{0:5000,1:2500,2:800,3:8},"mats":1}
    if rel.startswith("Props/Ground"): return {"lods":{0},"max":{0:12000},"mats":1}
    return {"lods":{0,1},"max":{0:3000,1:1500},"mats":1}
def img_stats(p):
    im=bpy.data.images.load(p,check_existing=True); w,h=im.size; px=list(im.pixels[:]) if w*h<=1024*1024 else None
    return im,w,h
for f in sorted(glob.glob(R+"/**/*.fbx",recursive=True)):
    rel=os.path.relpath(f,R); e=[]; base=os.path.splitext(os.path.basename(f))[0]; ru=rules(rel,base)
    if base.startswith("Kit_Real") and base.endswith("_Fractured"): continue   # break-apart sets: validate_fracture.py (#28/#52 rules)
    if base.startswith("Kit_Real"): ru={**ru,"lods":{0,1,2}}                   # realistic kit modules: LOD0-2 like every kit piece
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
    objs=list(bpy.data.objects)
    for o in objs:
        if " " in o.name or re.search(r"\.\d{3}$",o.name): e.append(f"bad name {o.name}")
    hulls=[o for o in objs if o.type=='MESH' and o.name.startswith("UCX_")]
    rend=[o for o in objs if o.type=='MESH' and o not in hulls]
    lv={}; info=[]
    for o in rend:
        m=LOD.match(o.name)
        if not m: e.append(f"{o.name}: no _LODn suffix"); continue
        l=int(m.group("l")); lv[l]=o; t=tri(o); mats=[s.material.name for s in o.material_slots if s.material]
        info.append(f"LOD{l}={t}t/{len(mats)}m")
        if l in ru["max"] and t>ru["max"][l]: e.append(f"{o.name}: {t} tris > {ru['max'][l]}")
        if len(o.material_slots)>ru["mats"]: e.append(f"{o.name}: {len(o.material_slots)} submeshes > {ru['mats']}")
        if not o.data.uv_layers or o.data.uv_layers[0].name!="UV0": e.append(f"{o.name}: UV0 missing/not first")
        if len(o.data.uv_layers)<2: e.append(f"{o.name}: UV2 missing")
        if not o.data.color_attributes: e.append(f"{o.name}: no vertex colour")
    if set(lv)!=ru["lods"]: e.append(f"LOD set {sorted(lv)} != {sorted(ru['lods'])}")
    for h in hulls:
        if tri(h)>255: e.append(f"{h.name}: {tri(h)} tris >255")
    if len(hulls)>8: e.append(f"{len(hulls)} hulls > 8")
    mats=sorted({s.material.name for o in rend for s in o.material_slots if s.material})
    # textures referenced by the materials must exist; enforce naming/size
    texdir=os.path.join(os.path.dirname(f),"Textures"); asset=os.path.basename(os.path.dirname(f))
    if asset.startswith("Kit_Real"): asset=asset[4:]                             # one shared tiling set per realistic kit: T_Real<M>_*
    elif base.startswith("Kit_Real"): asset=base.split("_")[1]                   # modular building (Bld_<X>/Kit_Real<X>_<Module>): T_Real<X>_*
    need=[f"T_{asset}_{k}.png" for k in ("A","N","ORM")]
    for n in need:
        p=os.path.join(texdir,n)
        if not os.path.exists(p): e.append(f"missing {n}"); continue
        im=bpy.data.images.load(p,check_existing=True); w,h=im.size
        if w>2048 or h>2048 or (w&(w-1)) or (h&(h-1)): e.append(f"{n}: {w}x{h} not POT<=2048")
        if n.endswith("_ORM.png"):
            sm=bpy.data.images.load(p); sm.scale(64,64); px=list(sm.pixels[:]); r=px[0::4]; b=px[2::4]
            if min(r)<0.49: e.append(f"{n}: AO below 0.5 (strength limit)")
            if any(.1<x<.9 for x in b): e.append(f"{n}: metal channel not binary")
    tris0=tri(lv[0]) if 0 in lv else 0
    print(f"{rel}: {' '.join(sorted(info))} hulls={len(hulls)} materials={mats} -> {'PASS' if not e else 'FAIL'}")
    for x in e[:8]: print("    ERROR",x)
    bad+=bool(e)
print("REALISM RESULT:","ALL PASS" if not bad else f"{bad} FAILED")
