"""Realistic v1: re-surface BuildingKit v3.1 modules (geometry, LODs, UCX, fracture, JNT/ANCHOR, masses kept) with graded CC0 tiling sets.
usage: blender -b -P resurf.py -- Kit_RealBrick_Block [Kit_RealWood_Joint ...]
* UV0 = metric box projection (uv = pos_m / tile_m on the face's dominant axis; wood grain and brick courses follow the module's
  long axis / world up). Intact LODs and fragments share module space, so cut pieces line up with the intact piece.
* One tiling material per kit (M_Real<M>) + M_Real<M>_Inner for cut faces; props share one 512 atlas (wood | steel half).
* Vertex colour Col is kept as-is: GlossyPBR reads R = damage mask, A = flex.
* fracture_class is written on every _Fractured root; stats go to models/HeroBuildings/Kit_Real<M>_stats.json."""
import bpy,bmesh,sys,os,json,re,shutil,math
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(HERE,"..","..",".."))
sys.path.insert(0,os.path.join(HERE,"..","realism")); import rlib as R
KIT=ROOT+"/Assets/_Project/Models/BuildingKit"; SRCX="/tmp/kitsrc"      # Block modules: build_kit2.py -- /tmp/kitsrc Brick,Steel,Wood Block
REAL=ROOT+"/Assets/_Project/Models/Realistic"; SHARED=REAL+"/Materials/Textures"; STATS=ROOT+"/models/HeroBuildings"
# kit: (source kit, folder, surface set, inner set, tile_m, fracture class, grain)
SETS={"Brick":("Brick","Buildings/Kit_RealBrick","Brick","BrickInner",1.75,"Brick","courses"),
      "Wood": ("Wood","Buildings/Kit_RealWood","WoodPlank","WoodInner",1.4,"Wood","long"),
      "Steel":("Steel","Buildings/Kit_RealSteel","Steel","Steel",1.5,"Steel","courses"),
      "Roof": ("Roof","Buildings/Kit_RealRoof","Roof","BrickInner",2.0,"Roof","slope"),
      "Props":("Props","Props/Kit_RealProps",None,None,1.0,"Wood","long")}
def tex_set(kit,name,src,size=1024):
    d=f"{REAL}/{SETS[kit][1]}/Textures"; os.makedirs(d,exist_ok=True)
    for m in ("A","N","ORM"):
        dst=f"{d}/T_{name}_{m}.png"; s=f"{SHARED}/T_{src}_{m}.png"
        if not os.path.exists(s): s=f"{d}/T_{src}_{m}.png"
        if size==1024: shutil.copyfile(s,dst)
        else: R  # noqa
    return f"{d}/T_{name}"
def props_atlas():
    """512 atlas: left half WoodPlank (staves/planks), right half Steel (hoops/straps); inner = WoodInner. Non-tiling (props <= 1 m)."""
    import subprocess
    d=f"{REAL}/Props/Kit_RealProps/Textures"; os.makedirs(d,exist_ok=True)
    code=f"""
from PIL import Image
S='{SHARED}'; D='{d}'
for m in ('A','N','ORM'):
    a=Image.open(f'{{S}}/T_WoodPlank_{{m}}.png').convert('RGB').resize((512,512),Image.LANCZOS)
    s=Image.open(f'{{S}}/T_Steel_{{m}}.png').convert('RGB').resize((512,512),Image.LANCZOS)
    o=Image.new('RGB',(512,512)); o.paste(a.crop((0,0,256,512)),(0,0)); o.paste(s.crop((0,0,256,512)),(256,0)); o.save(f'{{D}}/T_RealProps_{{m}}.png')
    Image.open(f'{{S}}/T_WoodInner_{{m}}.png').convert('RGB').resize((512,512),Image.LANCZOS).save(f'{{D}}/T_RealProps_Inner_{{m}}.png')
"""
    subprocess.check_call(["/usr/bin/python3","-c",code]); return f"{d}/T_RealProps"
def box_uv(objs,tile,grain,long_axis,props=False,steel_faces=None):
    for o in objs:
        me=o.data; M=o.matrix_world; Mn=M.to_3x3().inverted().transposed()
        while len(me.uv_layers)>(2 if "UV2_Lightmap" in me.uv_layers else 1):
            me.uv_layers.remove([u for u in me.uv_layers if u.name!="UV2_Lightmap"][-1])
        uv=[u for u in me.uv_layers if u.name!="UV2_Lightmap"]
        uv=uv[0] if uv else me.uv_layers.new(name="UV0")
        uv.name="UV0"; me.uv_layers.active=uv
        try: me.uv_layers.move(me.uv_layers.find("UV0"),0)
        except Exception: pass
        for p in me.polygons:
            n=(Mn@p.normal).normalized(); ax=max(range(3),key=lambda a:abs(n[a])); fa=[a for a in range(3) if a!=ax]
            if grain=="courses":   # brick/steel: v is world up on side faces
                v_ax=2 if 2 in fa else (long_axis if long_axis in fa else fa[1]); u_ax=[a for a in fa if a!=v_ax][0]
            else:                  # wood/roof/props: grain (texture v) along the module's long axis
                v_ax=long_axis if long_axis in fa else (2 if 2 in fa else fa[1]); u_ax=[a for a in fa if a!=v_ax][0]
            sgn=1 if n[ax]>=0 else -1
            for li in p.loop_indices:
                w=M@me.vertices[me.loops[li].vertex_index].co
                u=w[u_ax]*sgn*(1 if ax!=1 else -1)/tile; v=w[v_ax]/tile
                if props:   # props <= 1 m: offset by 0.5 m so a face never crosses a cell edge; steel faces use the right half
                    u=min(max(u+.5,0.0),1.0)*.5+(.5 if (steel_faces and (o.name,p.index) in steel_faces) else 0.0)
                uv.data[li].uv=(u,v)
def mat(name,prefix):
    m=bpy.data.materials.get(name)
    if m: m.name=name+"_old"
    return R.lit_material(name,prefix,vcol=False)
def run(asset):
    m=re.match(r"Kit_Real(\w+?)_(\w+)$",asset); kit,mod=m.group(1),m.group(2); skit,folder,surf,inner,tile,frac,grain=SETS[kit]
    src_dir=f"{SRCX}/Kit_{skit}" if mod=="Block" else f"{KIT}/Kit_{skit}"
    out_dir=f"{REAL}/{folder}"; os.makedirs(out_dir,exist_ok=True)
    if kit=="Props": P=props_atlas(); PI=P+"_Inner"
    else: P=tex_set(kit,f"Real{kit}",surf); PI=tex_set(kit,f"Real{kit}_Inner",inner)
    st={}
    for frac_file in (False,True):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        f=f"{src_dir}/Kit_{skit}_{mod}{'_Fractured' if frac_file else ''}.fbx"
        bpy.ops.import_scene.fbx(filepath=f,axis_forward='-Z',axis_up='Y',use_custom_props=True)
        for o in list(bpy.data.objects): o.name=o.name.replace(f"Kit_{skit}_",f"Kit_Real{kit}_")
        MA=mat(f"M_Real{kit}",P); MI=mat(f"M_Real{kit}_Inner",PI)
        meshes=[o for o in bpy.data.objects if o.type=='MESH' and not o.name.startswith("UCX_")]
        allv=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
        lo=Vector([min(w[a] for w in allv) for a in range(3)]); hi=Vector([max(w[a] for w in allv) for a in range(3)]); ext=hi-lo
        long_axis=max(range(3),key=lambda a:ext[a])
        steel=set()
        if kit=="Props":   # hoops / straps / nails: faces whose old kit atlas colour is a dark low-saturation grey -> steel half
            import colorsys
            im=bpy.data.images.load(f"{KIT}/Kit_Props/textures/T_Props_A.png",check_existing=True); W,H=im.size; px=im.pixels[:]
            for o in meshes:
                me=o.data; ul=me.uv_layers[0]
                for p in me.polygons:
                    if p.material_index==1: continue
                    cu=sum(ul.data[li].uv[0] for li in p.loop_indices)/p.loop_total; cv=sum(ul.data[li].uv[1] for li in p.loop_indices)/p.loop_total
                    x=min(W-1,max(0,int((cu%1)*W))); y=min(H-1,max(0,int((cv%1)*H))); i=(y*W+x)*4
                    h_,s_,v_=colorsys.rgb_to_hsv(*px[i:i+3])
                    if s_<.22 and v_<.55: steel.add((o.name,p.index))
            print("steel faces",len(steel))
        box_uv(meshes,tile,grain,long_axis,props=(kit=="Props"),steel_faces=steel)
        for o in meshes:
            slots=o.data.materials
            for i in range(len(slots)):
                old=slots[i].name if slots[i] else ""
                slots[i]=MI if old.endswith("_Inner") or old.endswith("_Inner_old") else MA
            if kit=="Props" and len(slots)==1 and frac_file: slots.append(MI)
        for mm in [x for x in bpy.data.materials if x.name.endswith("_old") or x.name in (f"M_{skit}",f"M_{skit}_Inner")]: bpy.data.materials.remove(mm)
        roots=[o for o in bpy.data.objects if o.parent is None and o.type=='EMPTY' and o.name.startswith(f"Kit_Real{kit}_{mod}")]
        if frac_file:
            for r in roots: r["fracture_class"]=frac
            fr=[o for o in meshes if re.search(r"_frag_\d\d$",o.name)]
            for o in fr: o["fracture_class"]=frac
            tt=[R.tri_count(o) for o in fr]; ms=[o.get("mass_kg",0) for o in fr]
            mins=[min((max((o.matrix_world@v.co)[a] for v in o.data.vertices)-min((o.matrix_world@v.co)[a] for v in o.data.vertices)) for a in range(3)) for o in fr]
            st.update(frags=len(fr),frag_max=max(tt),Fractured=sum(tt),mass=f"{min(ms):.1f}-{max(ms):.1f}",min_axis_m=round(min(mins),3),
                      joints=sum(o.name.startswith("JNT_") for o in bpy.data.objects),anchors=sum(o.name.startswith("ANCHOR_") for o in bpy.data.objects),
                      anchor_to=sorted({o["anchor_to"] for o in bpy.data.objects if o.name.startswith("ANCHOR_")}),
                      pivot_com="kit v3.1: each fragment origin = closed-volume centre of mass")
        else:
            for o in meshes:
                lv=R.tri_count(o); k=re.search(r"_LOD(\d)$",o.name)
                if k: st[f"LOD{k.group(1)}"]=lv
            st.update(ucx=sum(o.name.startswith("UCX_") for o in bpy.data.objects),size_m=[round(ext[0],3),round(ext[2],3),round(ext[1],3)],
                      aabb=[list(map(lambda x:round(x,4),lo)),list(map(lambda x:round(x,4),hi))])
        objs=[o for o in bpy.data.objects]
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        path=f"{out_dir}/{asset}{'_Fractured' if frac_file else ''}.fbx"
        bpy.ops.export_scene.fbx(filepath=path,use_selection=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',global_scale=1.0,
            bake_space_transform=True,use_custom_props=True,path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='FACE',use_tspace=True,
            colors_type='LINEAR',add_leaf_bones=False,object_types={'EMPTY','MESH'})
    sp=f"{STATS}/Kit_Real{kit}_stats.json"
    S=json.load(open(sp)) if os.path.exists(sp) else {"building":f"Real{kit}","source":"BuildingKit v3.1 geometry + fracture, re-surfaced with graded CC0 (realistic v1)","modules":{}}
    S["tile_m"]=tile; S["uv"]="UV0 metric box projection, uv = pos_m / tile_m (rebuild from scaled positions after per-block scaling)"
    S["materials"]=[f"M_Real{kit}",f"M_Real{kit}_Inner"]; S["textures"]=[os.path.relpath(P,ROOT)+"_A/_N/_ORM",os.path.relpath(PI,ROOT)+"_A/_N/_ORM"]
    S["modules"][mod]={**st,"frac":frac,"source":os.path.relpath(f"{src_dir}/Kit_{skit}_{mod}.fbx",ROOT) if mod!="Block" else "Tools/Blender/build_kit2.py Block (kit v3.1 recipe)"}
    json.dump(S,open(sp,"w"),indent=1); print("DONE",asset,json.dumps(st))
for a in sys.argv[sys.argv.index("--")+1:]: run(a)
