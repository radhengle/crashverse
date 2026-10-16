# v3 mesh-data checks on re-imported FBX: vertex colour, UV channels (UV0 + UV2 lightmap on statics), materials/batching, sockets, VFX placeholder, map files
import bpy,sys,glob,os,re
R=sys.argv[sys.argv.index("--")+1]; bad=0
SOCK=["Socket_Muzzle","Socket_Core","Socket_Module","Socket_Booster","Socket_WheelDust"]
for f in sorted(glob.glob(R+"/**/*.fbx",recursive=True)):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f); e=[]; rel=os.path.relpath(f,R)
    meshes=[o for o in bpy.data.objects if o.type=='MESH' and not o.name.startswith("UCX_")]
    mats=sorted({s.material.name for o in meshes for s in o.material_slots if s.material})
    for o in meshes:
        if not o.data.color_attributes: e.append(f"{o.name}: no vertex colour")
        nuv=len(o.data.uv_layers)
        static=o.name.endswith(("_LOD0","_LOD1")) or (o.name.startswith("Island_") )
        if nuv<1: e.append(f"{o.name}: no UV0")
        if static and nuv<2: e.append(f"{o.name}: missing UV2 lightmap")
    m=re.search(r"Kit_(\w+?)_",os.path.basename(f))
    if m and set(mats)-{f"M_{m.group(1)}",f"M_{m.group(1)}_Inner"}: e.append(f"extra materials {mats}")
    if "Wrecker" in f:
        names={o.name for o in bpy.data.objects}
        for s in SOCK:
            if s not in names: e.append("missing "+s)
            elif "attach_to" not in bpy.data.objects[s]: e.append(s+" no attach_to")
        for s in ("Slot_Core","Slot_Module","Slot_Booster"):
            if "M_Wrecker_Emissive" not in [x.material.name for x in bpy.data.objects[s].material_slots]: e.append(s+" no emissive slot")
        if mats!=["M_Wrecker","M_Wrecker_Emissive"]: e.append(f"materials {mats}")
        if any(o.parent and o.parent.parent for o in bpy.data.objects): e.append("hierarchy deeper than 1")
    if "Island" in f and "VFX_Waterfall_Placeholder" not in bpy.data.objects: e.append("no VFX_Waterfall_Placeholder")
    tex=os.path.join(os.path.dirname(f),"textures")
    for im in bpy.data.images:
        if not os.path.exists(bpy.path.abspath(im.filepath)): e.append("missing texture "+im.filepath)
    print(f"{rel}: meshes={len(meshes)} materials={mats} -> {'PASS' if not e else 'FAIL'}")
    for x in e[:6]: print("   ",x)
    bad+=bool(e)
print("MESHDATA RESULT:", "ALL PASS" if not bad else f"{bad} FAILED")
