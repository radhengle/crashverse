"""L3 destructible bread-crate decoy (realistic v1): Kit_RealBread_Crate.fbx (LOD0-2 + UCX) + Kit_RealBread_Crate_Fractured.fbx
(wood, 8-12 fragments, #28/#52: frag_## + exact UCX + JNT (break forces blank) + ANCHOR Ground, mass_kg, COM pivot, fracture_class).
Same geometry and Props_Village atlas UVs as Prop_BreadCrate (props.py bread_crate); same fracture recipe as the kit crates
(house_modular.fracture: closed components, split_long, clustered to the class range, min axis 0.15 m, mass >= 0.5 kg).
Material M_RealBread / M_RealBread_Inner -> T_Props_Village (texture_set alias in models/HeroBuildings/Kit_RealBread_stats.json).
usage: blender -b -P bread_crate.py"""
import bpy,os,sys
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE); sys.path.insert(0,os.path.join(HERE,"..","realism"))
import house_modular as HM, modkit as MK, props as PR
from house_modular import R
OUT=R.REAL+"/Props/Props_Village"; STATS=HM.ROOT+"/models/HeroBuildings/Kit_RealBread_stats.json"
from mathutils import Vector
def grid_fracture(mod,bms,cls,dens,XS=(-.155,0.0,.155),YS=(0.0,)):
    """the crate is too small for the kit clustering to reach 8 pieces with a 0.15 m min axis: cut every closed component on a
    4 x 2 grid (x = -0.155/0/0.155, y = 0) and keep one fragment per cell (8 fragments, each >= 0.155 m on every axis)"""
    cells={}
    for bm in bms:
        for c in HM.comps(bm):
            if not HM.closed(c): HM.seal(c)
            ps=[c]
            for ax,vals in ((0,XS),(1,YS)):
                for v in vals:
                    nx=[]; no=Vector((0,0,0)); no[ax]=1; co=Vector((0,0,0)); co[ax]=v
                    for q in ps:
                        lo,hi=HM.bbox_bms([q])
                        if lo[ax]<v-.004 and hi[ax]>v+.004 and HM.closed(q):
                            a=HM.half(q,co,no,True); b=HM.half(q,co,no,False)
                            if a is not None and b is not None: nx+=[a,b]; continue
                        nx.append(q)
                    ps=nx
            for q in ps:
                i=HM.info(q); i["m"]=i["vol"]*dens; k=(sum(i["c"].x>v for v in XS),sum(i["c"].y>v for v in YS))
                if k in cells: n=HM.merge_info(cells[k],i); n["bms"]=cells[k]["bms"]+[q]; cells[k]=n
                else: cells[k]=dict(i,bms=[q])
    cl=list(cells.values())
    assert 8<=len(cl)<=12 and all(HM.minax(c)>=HM.MINAX for c in cl) and all(c["m"]>=.5 for c in cl), [(HM.minax(c),c["m"]) for c in cl]
    return cl
if __name__=="__main__":
    MK.fracture=grid_fracture
    bpy.ops.wm.read_factory_settings(use_empty=True)
    P={}
    for l in (0,1,2):
        B=PR.bread_crate(min(l,1)); P[l]=(B.parts,["Crate"]*len(B.parts))
    HM.RNG["Wood"]=(8,12)   # LD: bread crate decoy breaks into 8-12 pieces
    MK.export_set(dict(kit="Kit_RealBread",out=OUT,rel="Assets/_Project/Models/Realistic/Props/Props_Village",building="Kit_RealBread",mat="M_RealBread",
        tex=OUT+"/Textures/T_Props_Village",inner_tex=R.REAL+"/Buildings/Bld_Townhouse/Textures/T_RealHouse_Inner",stats=STATS,parts=P,
        MODS=[("Crate","Bread crate (destructible decoy): plank crate heaped with loaves","Crate",None,"Crate")],ASM=[("Crate","Bread crate, destructible")],
        ATTACH={"Crate":"Ground"},cls_of=lambda t:"Wood",ground=("Crate",),front="long side faces +Z (same as Prop_BreadCrate)",
        extra={"intact_twin":"Assets/_Project/Models/Realistic/Props/Props_Village/Prop_BreadCrate.fbx"}))
    import json; d=json.load(open(STATS)); d["texture_set"]="Props_Village"; json.dump(d,open(STATS,"w"),indent=1)
