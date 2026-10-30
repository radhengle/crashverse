"""Generic modular-building exporter (realistic v1), generalised from house_modular.run().
export_set(cfg): per piece Kit_<Set>_<code>.fbx (LOD0-2 + UCX box) and _Fractured.fbx (frag_## + exact UCX + JNT + ANCHOR, mass, COM pivot),
stats JSON (validator lookup) and a crashverse.building_layout v1 JSON with modules (Level Designer units) + pieces + connections.
cfg keys: kit, out, rel, building, mat, tex, inner_tex, stats, parts {lod:(parts,tags)}, MODS [(code,label,tag,split,module[,bm_fn])],
ASM [(module,label)], ATTACH {code: 'Ground'|[codes]}, cls_of(tag)->class|None, ground (codes anchored to Ground), perim (codes anchored on the perimeter),
extra (dict merged into the layout), front (text)."""
import bpy,bmesh,json,os
from mathutils import Vector
import house_modular as HM
from house_modular import R,mesh_obj,empty,export,tri,bbox_bms,split_parts,fracture,cap_uv,com_exact,hull_bm,minax
def U(v): return {"x":round(-v.x,4),"y":round(v.z,4),"z":round(-v.y,4)}   # Unity = C @ Blender, C=((-1,0,0),(0,0,1),(0,-1,0)); Blender -Y front -> Unity +Z
def export_set(c):
    P=c["parts"]; MODS=c["MODS"]; KIT=c["kit"]; OUT=c["out"]; ATTACH=c["ATTACH"]
    for m in MODS: assert len(f"JNT_{KIT}_{m[0]}_frag_01__{KIT}_{m[0]}_frag_02")<=63, f"piece code too long for the 63-char name cap: {m[0]}"
    mods=[m[0] for m in MODS]; TAG={m[0]:m[2] for m in MODS}; SPL={m[0]:m[3] for m in MODS}; LBL={m[0]:m[1] for m in MODS}; ASMOF={m[0]:m[4] for m in MODS}
    FN={m[0]:(m[5] if len(m)>5 else None) for m in MODS}
    cls_m=lambda m:c["cls_of"](TAG[m]); stats={"set":KIT,"building":c["building"],"modules":{}}; pieces=[]; tri_tot={"LOD0":0,"LOD1":0,"LOD2":0,"Fractured":0}
    def get(mod,l):
        bms=split_parts([b.copy() for b,t in zip(*P[l]) if t==TAG[mod]],SPL[mod])
        if FN[mod]:
            for bm in bms: FN[mod](bm)
        return bms
    aabbs={}; abox={}
    for mod in mods:
        lo,hi=bbox_bms(get(mod,0)); A=ASMOF[mod]; aabbs[mod]=(lo,hi)
        if A in abox: L_,H_=abox[A]; abox[A]=(Vector([min(L_[k],lo[k]) for k in range(3)]),Vector([max(H_[k],hi[k]) for k in range(3)]))
        else: abox[A]=(lo,hi)
    apiv={A:Vector(((l.x+h.x)/2,(l.y+h.y)/2,l.z)) for A,(l,h) in abox.items()}; origins={}
    if c.get("pivot_of"): apiv={A:Vector(c["pivot_of"](A,l,h)) for A,(l,h) in abox.items()}   # e.g. boss blocks: pivot = runtime block centre
    def mats():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        return R.lit_material(c["mat"],c["tex"]),R.lit_material(c["mat"]+"_Inner",c["inner_tex"],vcol=False)
    for mod in mods:
        M,MI=mats(); name=f"{KIT}_{mod}"; lo,hi=aabbs[mod]; org=apiv[ASMOF[mod]]; origins[mod]=org
        root=empty(name,(0,0,0)); lods={}; root["fracture_class"]=cls_m(mod) or "None"
        for l in (0,1,2):
            bms=get(mod,l)
            for bm in bms: bmesh.ops.translate(bm,verts=bm.verts,vec=-org)
            o=mesh_obj(f"{name}_LOD{l}",bms,[M]); o.parent=root; o["static"]=True; R.add_uv2(o); lods[l]=tri(o)
        u=mesh_obj(f"UCX_{name}_LOD0_01",[R.bm_box(*(hi-lo),tuple((lo+hi)/2-org))],[]); u.parent=root; u.display_type='WIRE'
        export([o for o in bpy.data.objects],f"{OUT}/{name}.fbx")
        for k in (0,1,2): tri_tot[f"LOD{k}"]+=lods[k]
        ent={"frac":cls_m(mod),"module":ASMOF[mod],"label":LBL[mod],"tris":{f"LOD{k}":lods[k] for k in (0,1,2)},"bounds_m":[round(v,3) for v in (hi-lo)]}
        if cls_m(mod):
            cls=cls_m(mod); dens=HM.DENS[cls]; M,MI=mats()
            for src_l in (1,2):
                src=get(mod,src_l)
                for bm in src: bmesh.ops.translate(bm,verts=bm.verts,vec=-org)
                try: cl=fracture(mod,src,cls,dens); break
                except RuntimeError as e:
                    if src_l==2: raise
                    print("  retry LOD2:",e)
            rootf=empty(name+"_Fractured",(0,0,0)); rootf["fracture_class"]=cls; frags=[]
            for i,cc in enumerate(cl):
                fn=f"{name}_frag_{i+1:02d}"; com=com_exact(cc["bms"]) or cc["c"]
                for bm in cc["bms"]: cap_uv(bm,cls!="Stone"); bmesh.ops.translate(bm,verts=bm.verts,vec=-com)
                ob=mesh_obj(fn,cc["bms"],[M,MI]); ob.parent=rootf; ob.location=com; ob["mass_kg"]=round(max(.5,cc["m"]),2); ob["density_kg_m3"]=dens; ob["fracture_class"]=cls
                hb=hull_bm([v.co.copy() for v in ob.data.vertices]); u=mesh_obj("UCX_"+fn,[hb],[]); u.parent=rootf; u.location=com; u.display_type='WIRE'; frags.append((ob,cc))
            nj=0
            for i in range(len(frags)):
                for j in range(i+1,len(frags)):
                    a,b=frags[i][1],frags[j][1]; e=.03
                    if all(a["lo"][k]-e<=b["hi"][k] and b["lo"][k]-e<=a["hi"][k] for k in range(3)):
                        p=Vector([(max(a["lo"][k],b["lo"][k])+min(a["hi"][k],b["hi"][k]))/2 for k in range(3)])
                        j_=empty(f"JNT_{frags[i][0].name}__{frags[j][0].name}",p,rootf); j_["break_force_N"]=""; j_["break_torque_Nm"]=""; nj+=1
            zmin=min(cc["lo"].z for _,cc in frags); X0=min(cc["lo"].x for _,cc in frags); X1=max(cc["hi"].x for _,cc in frags); Y0=min(cc["lo"].y for _,cc in frags); Y1=max(cc["hi"].y for _,cc in frags)
            ground=ATTACH[mod]=="Ground" or mod in c.get("ground",()); na=0
            for ob,cc in frags:
                sel=min(cc["lo"].x-X0,X1-cc["hi"].x,cc["lo"].y-Y0,Y1-cc["hi"].y)<.3 if mod in c.get("perim",()) else cc["lo"].z<zmin+.08
                if sel: a=empty("ANCHOR_"+ob.name,(cc["c"].x,cc["c"].y,cc["lo"].z),rootf,.15); a["anchor_to"]="Ground" if ground else "Support"; na+=1
            export([o for o in bpy.data.objects],f"{OUT}/{name}_Fractured.fbx")
            ft=[tri(ob) for ob,_ in frags]; ms=[ob["mass_kg"] for ob,_ in frags]; tri_tot["Fractured"]+=sum(ft)
            ent.update({"fragments":len(frags),"frag_source_lod":src_l,"frag_tris_max":max(ft),"frag_tris_total":sum(ft),"mass_kg_total":round(sum(ms),1),
                        "mass_kg_range":[min(ms),max(ms)],"density_kg_m3":dens,"joints":nj,"anchors":na,"min_axis_m":round(min(minax(cc) for _,cc in frags),3)})
        stats["modules"][mod]=ent; print("BUILT",mod,{k:ent.get(k) for k in ("tris","fragments","frag_tris_total","frag_source_lod")},flush=True)
    rel=c["rel"]
    for mod in mods:
        pieces.append({"id":mod,"label":LBL[mod],"module":ASMOF[mod],"kit":f"{KIT}_{mod}","intact":f"{rel}/{KIT}_{mod}.fbx","fractured":f"{rel}/{KIT}_{mod}_Fractured.fbx" if cls_m(mod) else None,
            "position":U(origins[mod]),"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0},"euler_zxy_deg":{"x":0.0,"y":0.0,"z":0.0},
            "anchor_to":ATTACH[mod],"fracture_class":cls_m(mod) or "Indestructible","tris":stats["modules"][mod]["tris"]|({"Fractured":stats["modules"][mod]["frag_tris_total"]} if cls_m(mod) else {})})
    modules=[]
    for A,lab in c["ASM"]:
        l,h=abox[A]; mem=[m for m in mods if ASMOF[m]==A]; att=sorted({x for m in mem for x in (ATTACH[m] if isinstance(ATTACH[m],list) else [ATTACH[m]]) if x=="Ground" or ASMOF.get(x)!=A})
        att=["Ground"] if att==["Ground"] else sorted({ASMOF.get(x,x) for x in att})
        modules.append({"id":A,"label":lab,"pivot":U(apiv[A]),"pivot_rule":c.get("pivot_rule","centre of the base footprint, +Z = front"),"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0},
            "footprint_m":{"x":round(h.x-l.x,3),"z":round(h.y-l.y,3)},"height_m":round(h.z-l.z,3),"top_y_m":round(h.z,3),"pieces":mem,"anchor_to":att,
            "tris":{k:sum(stats["modules"][m]["tris"][k] for m in mem) for k in ("LOD0","LOD1","LOD2")}})
    conns=[]
    for i,a in enumerate(mods):
        for b in mods[i+1:]:
            if ASMOF[a]==ASMOF[b] and FN[a] is not None and FN[b] is None and TAG[a]==TAG[b]: continue   # colour variants are alternatives, not neighbours
            if TAG[a]==TAG[b] and SPL[a]==SPL[b]: continue
            (la,ha),(lb,hb)=aabbs[a],aabbs[b]; e=.02
            if all(la[k]-e<=hb[k] and lb[k]-e<=ha[k] for k in range(3)):
                p=Vector([(max(la[k],lb[k])+min(ha[k],hb[k]))/2 for k in range(3)])
                conns.append({"a":a,"b":b,"point":U(p),"break_force_N":"","break_torque_Nm":""})
    lay={"format":"crashverse.building_layout","version":1,"building":c["building"],"style":"realistic v1","units":"m",
         "space":"Unity (Y up, left-handed); "+c.get("front","front faces +Z")+"; positions local to the building root. Every piece pivot = its module pivot (centre of the module base footprint), so all pieces of a module drop in at the module position",
         "root":{"position":{"x":0.0,"y":0.0,"z":0.0},"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0}},"tris_total":tri_tot,
         "footprint_m":{"x":round(max(h.x for l,h in abox.values())-min(l.x for l,h in abox.values()),3),"z":round(max(h.y for l,h in abox.values())-min(l.y for l,h in abox.values()),3)},
         "height_m":round(max(h.z for l,h in abox.values()),3),"module_count":len(modules),"modules":modules,"piece_count":len(pieces),"pieces":pieces,
         "connections":conns,"unsupported_contacts":[],
         "anchor_note":"fragment ANCHOR_ empties carry anchor_to 'Ground' or 'Support'; for 'Support' use this piece's anchor_to list. Pieces without 'fractured' are indestructible."}
    lay.update(c.get("extra",{}))
    json.dump(lay,open(f"{OUT}/{c['building']}.layout.json","w"),indent=1)
    if c.get("merge_stats") and os.path.exists(c["stats"]):   # several sets sharing one Kit_<Set> stats file (validator lookup by piece code)
        old=json.load(open(c["stats"])); old["modules"].update(stats["modules"]); old.setdefault("buildings",{})[c["building"]]=tri_tot; stats=old
    else: stats["tris_total"]=tri_tot
    json.dump(stats,open(c["stats"],"w"),indent=1); print("TOTAL",tri_tot,flush=True); return lay,stats
