# PR #28 (vfx/IMPORT_PIPELINE.md) naming/collider/LOD/budget rules for every FBX.
# usage: blender -b --python validate_colliders.py -- <root> [<root>...]
import bpy,sys,os,glob,re,bmesh
from mathutils import Vector
FRAG=re.compile(r"_frag_\d{2}$"); LODRX=re.compile(r"^(?P<b>.+?)_LOD(?P<l>[0-2])$")
def target(n):
    if n.startswith("UCX_"):
        b=n[4:]; return b if FRAG.search(b) else re.sub(r"_\d{2}$","",b)
    m=re.match(r"^(?P<t>.+?)_COL(_\d{2})?$",n); return m.group("t") if m else None
def lodlvl(n):
    m=LODRX.match(n); return (int(m.group("l")),m.group("b")) if m else (-1,n)
def cls(stem):
    if stem.startswith(("Wrecker","Hero_")): return "Hero"
    if stem.startswith(("Island_","Env_")): return "Environment"
    if stem.startswith("Kit_"): return "KitFractured" if stem.endswith("_Fractured") else "Kit"
    return "Unknown"
BUDGET={"Hero":12000,"Environment":10000,"Kit":1500,"KitFractured":1500,"Unknown":3000}
def tri(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
def com_local(o):
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm,faces=bm.faces); V=0; c=Vector()
    for f in bm.faces:
        a,b,d=(v.co for v in f.verts); v=a.dot(b.cross(d))/6; V+=v; c+=v*(a+b+d)/4
    bm.free(); return (c/V if abs(V)>1e-9 else Vector()), max(o.dimensions)
roots=sys.argv[sys.argv.index("--")+1:]
files=sorted(f for r in roots for f in glob.glob(os.path.join(r,"**","*.fbx"),recursive=True))
groups={}
for f in files:
    l,b=lodlvl(os.path.splitext(os.path.basename(f))[0]); groups.setdefault((os.path.dirname(f),b),{})[l]=f
bad=0
for (dirn,base),fl in sorted(groups.items()):
    errs=[]; warn=[]; C=cls(base)
    if -1 in fl and any(k>=0 for k in fl): warn.append("plain FBX next to _LODn files (plain ignored by #28)")
    lod0=fl.get(0,fl.get(-1))
    if lod0 is None: print(f"{base}: FAIL\n    LOD1/LOD2 without LOD0"); bad+=1; continue
    if C.startswith("Kit") and "/Assets/_Project/Models/BuildingKit/" not in os.path.abspath(lod0)+"/": errs.append("kit file outside Assets/_Project/Models/BuildingKit/")
    if C=="Unknown": warn.append("unknown class prefix")
    levels={k for k in fl if k>=0}
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=lod0)
    objs=list(bpy.data.objects); names={o.name for o in objs}
    for n in names:
        if " " in n or re.search(r"\.\d{3}$",n): errs.append(f"bad name '{n}'")
    hulls=[o for o in objs if o.type=='MESH' and target(o.name)]
    rend=[o for o in objs if o.type=='MESH' and o not in hulls]
    for o in rend:
        l,_=lodlvl(o.name); levels.add(max(l,0))
    frags=[o for o in rend if FRAG.search(o.name)]
    for h in hulls:
        t=target(h.name)
        if t not in names: errs.append(f"{h.name}: target '{t}' not found")
        if tri(h)>255: errs.append(f"{h.name}: {tri(h)} tris >255")
    per={}
    for h in hulls: per.setdefault(target(h.name),[]).append(h)
    for t,hs in per.items():
        if len(hs)>8: errs.append(f"{t}: {len(hs)} hulls >8")
    for o in frags:
        n=len(per.get(o.name,[]))
        if n!=1: errs.append(f"{o.name}: {n} hulls (need exactly UCX_{o.name})")
        if tri(o)>=300: errs.append(f"{o.name}: {tri(o)} tris (fragment <300)")
        c,ext=com_local(o)
        if c.length>max(0.05,0.1*ext): errs.append(f"{o.name}: pivot {c.length:.3f} m from COM")
    if len(frags)>32: errs.append(f"{len(frags)} fragments >32")
    for j in [o for o in objs if o.name.startswith("JNT_")]:
        m=re.match(r"^JNT_(?P<a>.+?_frag_\d{2})__(?P<b>.+?_frag_\d{2})(_\d{2})?$",j.name)
        if not m or m.group("a") not in names or m.group("b") not in names: errs.append(f"{j.name}: bad joint names")
        if "break_force_N" not in j or "break_torque_Nm" not in j: errs.append(f"{j.name}: missing break_force_N/break_torque_Nm")
    for a in [o for o in objs if o.name.startswith("ANCHOR_")]:
        if a.name[7:] not in names: errs.append(f"{a.name}: fragment missing")
        if "anchor_to" not in a: errs.append(f"{a.name}: missing anchor_to")
    lod0tris=sum(tri(o) for o in rend if lodlvl(o.name)[0]<=0)
    if lod0tris>BUDGET[C]: errs.append(f"LOD0 {lod0tris} tris over {C} budget {BUDGET[C]}")
    if C in ("Kit","KitFractured") and lod0tris<500: warn.append(f"LOD0 {lod0tris} < 500")
    need_lods=C in ("Hero","Environment","Kit")   # VFX ruling: fractured sets are LOD0 only
    if need_lods and not {0,1,2}<=levels: errs.append(f"LOD set {sorted(levels)} incomplete (LOD0-2 required for {C})")
    if C=="KitFractured" and levels-{0}: errs.append(f"fractured set must be LOD0 only, found {sorted(levels)}")
    bad+=bool(errs)
    print(f"{os.path.relpath(lod0)} [{C}] LOD0 tris={lod0tris} LODs={sorted(levels)} hulls={len(hulls)} frags={len(frags)} -> {'PASS' if not errs else 'FAIL'}")
    for e in errs[:12]: print("    ERROR "+e)
    for w in warn: print("    warn  "+w)
print(f"RESULT: {len(groups)} groups, {bad} failed" if bad else f"RESULT: ALL PASS ({len(groups)} groups, {len(files)} files)")
