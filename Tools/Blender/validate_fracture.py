# usage: blender -b --python validate_fracture.py -- <dir with *_Fractured.fbx>
import bpy,sys,os,glob,re
from mathutils import Vector
D=sys.argv[sys.argv.index("--")+1]; RNG={"Wood":(8,16),"Brick":(12,24),"Steel":(4,10)}; fails=0
def tri(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
for f in sorted(glob.glob(os.path.join(D,"*_Fractured.fbx"))):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
    mat=re.search(r"Kit_(\w+?)_",os.path.basename(f)).group(1); objs=bpy.data.objects
    fr=[o for o in objs if o.type=='MESH' and re.search(r"_frag_\d\d$",o.name) and not o.name.startswith("UCX_")]
    ucx=[o for o in objs if o.name.startswith("UCX_")]; jnt=[o for o in objs if o.name.startswith("JNT_")]; anc=[o for o in objs if o.name.startswith("ANCHOR_")]
    errs=[]; lo,hi=RNG[mat]
    if not(lo<=len(fr)<=hi and len(fr)<=32): errs.append(f"count {len(fr)} not in {lo}-{hi}")
    names={o.name for o in fr}
    for o in fr:
        t=tri(o); ws=[o.matrix_world@v.co for v in o.data.vertices]
        ext=min(max(w[a] for w in ws)-min(w[a] for w in ws) for a in range(3)); m=o.get("mass_kg",0)
        mats=[s.material.name if s.material else None for s in o.material_slots]
        u=[x for x in ucx if x.name=="UCX_"+o.name]
        if t>=300: errs.append(f"{o.name} tris {t}")
        if ext<0.15-1e-4: errs.append(f"{o.name} min axis {ext:.3f}")
        if m<0.5: errs.append(f"{o.name} mass {m}")
        if len(u)!=1: errs.append(f"{o.name} UCX count {len(u)}")
        elif tri(u[0])>255: errs.append(f"{o.name} UCX tris")
        if not("M_Palette" in mats and f"M_{mat}_Inner" in mats): errs.append(f"{o.name} mats {mats}")
    for x in ucx:
        if x.name[4:] not in names: errs.append(f"orphan {x.name}")
    for j in jnt:
        a,b=j.name[4:].split("__")
        if a not in names or b not in names or "break_force_N" not in j or "break_torque_Nm" not in j: errs.append(f"bad joint {j.name}")
    for a in anc:
        if a.name[7:] not in names: errs.append(f"bad anchor {a.name}")
    tt=[tri(o) for o in fr]; ms=[o["mass_kg"] for o in fr]
    print(f"{os.path.basename(f)}: frags={len(fr)} (spec {lo}-{hi}) tris max={max(tt)} total={sum(tt)} mass {min(ms):.1f}-{max(ms):.1f} kg UCX={len(ucx)} JNT={len(jnt)} ANCHOR={len(anc)} -> {'PASS' if not errs else 'FAIL'}")
    for e in errs: print("   ",e)
    fails+=bool(errs)
print("RESULT:", "ALL PASS" if not fails else f"{fails} FAILED")
