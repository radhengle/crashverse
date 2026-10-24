# PR #28 collider/naming rules for every FBX. usage: blender -b --python validate_colliders.py -- <root> [<root>...]
import bpy,sys,os,glob,re,bmesh
from mathutils import Vector
FRAG=re.compile(r"_frag_\d{2}$"); OPT={"mass_kg","break_force_N","break_torque_Nm","anchor_to","attach_to","static"}
def target(n):
    if n.startswith("UCX_"):
        b=n[4:]; return b if FRAG.search(b) else re.sub(r"_\d{2}$","",b)
    m=re.match(r"^(?P<t>.+?)_COL(_\d{2})?$",n); return m.group("t") if m else None
def tri(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
def com_local(o):
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm,faces=bm.faces); V=0; c=Vector()
    for f in bm.faces:
        a,b,d=(v.co for v in f.verts); v=a.dot(b.cross(d))/6; V+=v; c+=v*(a+b+d)/4
    bm.free(); return (c/V if abs(V)>1e-9 else Vector()), max(o.dimensions)
roots=sys.argv[sys.argv.index("--")+1:]; files=sorted(f for r in roots for f in glob.glob(os.path.join(r,"**","*.fbx"),recursive=True)); bad=0
for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
    objs=list(bpy.data.objects); names={o.name for o in objs}; errs=[]; warn=set()
    hulls=[o for o in objs if o.type=='MESH' and target(o.name)]
    frags=[o for o in objs if o.type=='MESH' and FRAG.search(o.name) and o not in hulls]
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
        if n!=1: errs.append(f"{o.name}: {n} hulls (need exactly 1, UCX_{o.name})")
        if tri(o)>=400: errs.append(f"{o.name}: {tri(o)} tris >=400")
        c,ext=com_local(o)
        if c.length>max(0.05,0.1*ext): errs.append(f"{o.name}: pivot {c.length:.3f} m from COM")
        for k in o.keys():
            if not k.startswith("_") and k not in OPT: warn.add(k)
    if len(frags)>32: errs.append(f"{len(frags)} fragments >32")
    bad+=bool(errs)
    print(f"{os.path.relpath(f)}: hulls={len(hulls)} frags={len(frags)} -> {'PASS' if not errs else 'FAIL'}")
    for e in errs[:12]: print("    "+e)
    if warn: print(f"    note: extra props ignored by #28: {sorted(warn)}")
print(f"RESULT: {len(files)} files, {bad} failed" if bad else f"RESULT: ALL PASS ({len(files)} files)")
