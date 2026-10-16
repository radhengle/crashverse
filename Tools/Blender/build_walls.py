import sys,os,bpy,bmesh,random,json; sys.path.insert(0,os.path.dirname(__file__)); from cv_lib import *
from mathutils import Vector
OUT=sys.argv[sys.argv.index("--")+1]; MODE=sys.argv[sys.argv.index("--")+2]  # validation|kit
ROOT="/workspace/wt/3d-modeler/models/BuildingKit"
SPEC={ # piece: (sx width X, sy depth Y, sz height Z) ; grid nx,nz fragments ; count range
 "Wood": dict(cell="wood",dk="wood_dk",rough=0.45,metal=0,rng=(8,16),
   pieces={"Wall":((3,0.2,3),(4,3,1)),"Floor":((3,3,0.2),(3,3,1)),"Joint":((0.3,0.3,3),(1,1,8))}),
 "Brick":dict(cell="brick",dk="mortar",rough=0.5,metal=0,rng=(12,24),
   pieces={"Wall":((3,0.25,3),(6,3,1)),"Floor":((3,3,0.25),(4,4,1)),"Joint":((0.4,0.4,3),(1,1,12))}),
 "Steel":dict(cell="steel",dk="steel_dk",rough=0.3,metal=0.0,rng=(4,10),
   pieces={"Wall":((3,0.16,3),(3,2,1)),"Floor":((3,3,0.16),(3,2,1)),"Joint":((0.3,0.3,3),(1,1,5))}),
}
def seeds_for(sx,sy,sz,nx,ny,nz,rnd):
    S=[]
    # grid over the two largest axes, jittered
    dims=[(sx,nx),(sy,ny),(sz,nz)]
    for i in range(nx):
        for j in range(ny):
            for k in range(nz):
                p=[]
                for (L,n),idx in zip(dims,(i,j,k)):
                    c=(idx+0.5)/n; c+= (rnd.random()-0.5)*0.5/n; p.append(c*L)
                S.append(Vector((p[0]-sx/2,p[1]-sy/2,p[2])))
    return S
def build(mat,piece,outdir,texdir,glbdir):
    global SRCDIR; SRCDIR=("BuildingKit_validation" if MODE=="validation" else f"Kit_{mat}")
    reset(); sp=SPEC[mat]; (sx,sy,sz),grid=sp["pieces"][piece]
    if piece=="Floor": nx,ny,nz=grid[0],grid[1],1
    elif piece=="Wall": nx,ny,nz=grid[0],1,grid[1]
    else: nx,ny,nz=1,1,grid[2]
    os.makedirs(texdir,exist_ok=True)
    def mats():
        gen_tex("sheet",mat,texdir); gen_tex("inner",mat,texdir)
        t=lambda k:os.path.join(texdir,f"T_{mat}_{ {'Albedo':'A','Normal':'N','ORM':'ORM'}[k]}.png")
        m=material_pbr(f"M_{mat}",t("Albedo"),t("Normal"),t("ORM"))
        return m, material_pbr(f"M_{mat}_Inner",os.path.join(texdir,f"T_{mat}_Inner_A.png"),rough=0.8)
    def outer_uv(f,uv,off=Vector()):
        """tiling sheet: planar box mapping, 1 UV unit = 2 m (512 px sheet -> 256 px/m)"""
        n=f.normal; ax=max(range(3),key=lambda a:abs(n[a]))
        for l in f.loops:
            p=l.vert.co+off
            l[uv].uv=((p[1] if ax==0 else p[0])/2, (p[1] if ax==2 else p[2])/2)
    M,MI=mats()
    name=f"Kit_{mat}_{piece}"
    # ---- intact + LODs + UCX
    grp=empty(name,(0,0,0))
    for lod in (0,1):
        bm=box_bm(sx,sy,sz)
        if lod==0: bevel_hard(bm,min(sx,sy,sz)*0.12,2)
        uv=bm.loops.layers.uv.verify()
        for f in bm.faces: outer_uv(f,uv)
        lo=mesh_from_bm(f"{name}_LOD{lod}",bm,[M],grp); add_vcol(lo); lo["static"]=True
    u=mesh_from_bm(f"UCX_{name}_LOD0_01",box_bm(sx,sy,sz),[],grp); u.display_type='WIRE'
    add_lightmap_uv([o for o in bpy.data.objects if o.name.startswith(name+"_LOD")])
    save_src(SRCDIR,name)
    export(os.path.join(outdir,f"{name}.fbx"), os.path.join(glbdir,f"{name}.glb") if glbdir else None)
    # ---- fracture (reseed until every fragment passes)
    dens=DENS[mat]
    for attempt in range(200):
        rnd=random.Random(hash((mat,piece))%1000+attempt)
        S=seeds_for(sx,sy,sz,nx,ny,nz,rnd); cells=voronoi_cells(sx,sy,sz,S); ok=True
        for bm in cells:
            vs=[v.co for v in bm.verts]
            if not vs: ok=False;break
            ext=[max(c[a] for c in vs)-min(c[a] for c in vs) for a in range(3)]
            if min(ext)<0.15 or bm.calc_volume()*dens<0.5: ok=False;break
        if ok: break
        for bm in cells: bm.free()
    reset(); M,MI=mats()
    root=empty(name+"_Fractured"); frags=[]; info=[]
    for i,bm in enumerate(cells):
        fn=f"{name}_frag_{i+1:02d}"
        hull0=bm.copy(); bevel_hard(bm,0.025,1)      # bevelled breakup edges (bevel faces = inner, darker)
        uv=bm.loops.layers.uv.verify()
        for f in bm.faces:
            if on_box(f,sx,sy,sz):
                f.material_index=0; outer_uv(f,uv)
            else:  # inner: own 0-1 planar island
                f.material_index=1; n=f.normal; t=n.orthogonal().normalized(); b=n.cross(t)
                pts=[(l.vert.co.dot(t),l.vert.co.dot(b)) for l in f.loops]
                mu=min(p[0] for p in pts);mv=min(p[1] for p in pts); s=max(max(p[0] for p in pts)-mu,max(p[1] for p in pts)-mv,1e-6)
                for l,p in zip(f.loops,pts): l[uv].uv=((p[0]-mu)/s,(p[1]-mv)/s)
        vol=bm.calc_volume(); com=sum((v.co for v in bm.verts),Vector())/len(bm.verts)
        # true centroid via tetra decomposition
        bmt=bm.copy(); bmesh.ops.triangulate(bmt,faces=bmt.faces[:]); acc=Vector(); V=0
        for f in bmt.faces:
            a,b_,c=[v.co for v in f.verts]; v6=a.dot(b_.cross(c)); acc+=v6*(a+b_+c)/4; V+=v6
        bmt.free(); com=acc/V
        for v in bm.verts: v.co-=com
        hull=hull0
        for v in hull.verts: v.co-=com
        ob=mesh_from_bm(fn,bm,[M,MI],root,com); add_vcol(ob); ob["mass_kg"]=round(vol*dens,2); ob["density_kg_m3"]=dens
        for p in hull.faces: p.material_index=0
        u=mesh_from_bm("UCX_"+fn,hull,[],root,com); u.display_type='WIRE'
        frags.append((ob,com))
        info.append((fn,tris(ob),round(vol*dens,2)))
    # joints: shared faces between cells
    bpy.context.view_layer.update()
    S2=S
    for i in range(len(S2)):
        for j in range(i+1,len(S2)):
            n=(S2[j]-S2[i]).normalized(); mid=(S2[i]+S2[j])/2
            pts=[]; ob=frags[i][0]
            for p in ob.data.polygons:
                c=Vector(p.center)+frags[i][1]
                if abs((c-mid).dot(n))<1e-4 and Vector(p.normal).dot(n)>0.999 and p.area>1e-3: pts.append(c)
            if pts:
                e=empty(f"JNT_{frags[i][0].name}__{frags[j][0].name}",sum(pts,Vector())/len(pts),root,0.1)
                e["break_force_N"]=""; e["break_torque_Nm"]=""
    for ob,com in frags:
        zs=[(ob.matrix_world@v.co).z for v in ob.data.vertices]
        if min(zs)<1e-4:
            bot=[ob.matrix_world@v.co for v in ob.data.vertices if (ob.matrix_world@v.co).z<1e-4]
            c=sum(bot,Vector())/len(bot); a=empty("ANCHOR_"+ob.name,(c.x,c.y,0),root,0.15); a["anchor_to"]="Ground"
    export(os.path.join(outdir,f"{name}_Fractured.fbx"), os.path.join(glbdir,f"{name}_Fractured.glb") if glbdir else None)
    save_src(SRCDIR,name+"_Fractured")
    return info
mats=["Wood","Brick","Steel"]; pieces=["Wall"] if MODE=="validation" else ["Wall","Floor","Joint"]
for m in mats:
    for p in pieces:
        if MODE=="validation": d=OUT; i=build(m,p,d,os.path.join(d,"textures"),os.path.join(d,"source"))
        else: d=os.path.join(OUT,f"Kit_{m}"); i=build(m,p,d,os.path.join(d,"textures"),f"/workspace/wt/3d-modeler/models/BuildingKit/Kit_{m}/source")
        print("BUILT",m,p,len(i),i)
