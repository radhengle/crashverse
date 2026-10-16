# Kit v3.1: composite pieces (walls with door/window openings, frames, roof panels, gables) + the original wall/floor/joint.
# Rules (#28 @723ccb3 + Physics): intact <Piece>_LOD0/1/2 500-1.5k tris, UCX_<Piece>_LOD0_## per sub-box (<=8);
# fractured set LOD0 only, <=1.5k total, fragments <300 tris, UCX_<Piece>_frag_## exact, pivot at COM, mass_kg,
# JNT_<A>__<B>[_##] from real face contacts (break_force_N/break_torque_Nm blank), ANCHOR_<Frag> with anchor_to
# ("Ground" for pieces standing on the ground; "Support" for floors/roofs/gables = resolved per instance in the building layout JSON).
# usage: blender -b --python build_kit2.py -- <Assets/_Project/Models/BuildingKit> [Mat,Mat] [Piece,Piece]
import sys,os,bpy,bmesh,random,math; sys.path.insert(0,os.path.dirname(__file__)); from cv_lib import *
from mathutils import Vector, Matrix
A=sys.argv[sys.argv.index("--")+1:]; OUT=A[0]; ONLYM=A[1].split(",") if len(A)>1 and A[1] else None; ONLYP=A[2].split(",") if len(A)>2 else None
GLB="/workspace/wt/3d-modeler-v3/models/BuildingKit"
RNG={"Wood":(8,16),"Brick":(12,24),"Steel":(4,10),"Glass":(8,16),"Stone":(4,10),"Roof":(8,16)}
MINAX={"Glass":0.02}
CELLSZ={"Wood":(0.25,1.5),"Brick":(0.5,0.25),"Steel":(1.0,0.5),"Glass":(0.75,0.75),"Stone":(0.75,0.5),"Roof":(0.6,0.5)}
TH={"Wood":0.2,"Brick":0.25,"Steel":0.16,"Stone":0.4,"Glass":0.03}
def B(cx,cy,z0,sx,sy,sz,frame=False): return dict(c=Vector((cx,cy,z0)),s=Vector((sx,sy,sz)),frame=frame)
def wall(t): return [B(0,0,0,3,t,3)]
def walldoor(t,w=1.0,h=2.1,W=3): s=(W-w)/2; return [B(-(w/2+s/2),0,0,s,t,3),B(w/2+s/2,0,0,s,t,3),B(0,0,h,w,t,3-h)]
def wallwin(t,w=1.0,z0=1.0,z1=2.0): s=(3-w)/2; return [B(-(w/2+s/2),0,0,s,t,3),B(w/2+s/2,0,0,s,t,3),B(0,0,0,w,t,z0),B(0,0,z1,w,t,3-z1)]
def frame(w,h,sill=True,bar=0.15,d=0.3,z=0.0):
    r=[B(-(w/2+bar/2),0,z,bar,d,h+bar),B(w/2+bar/2,0,z,bar,d,h+bar),B(0,0,z+h,w,d,bar)]
    if sill: r.append(B(0,0,z-bar,w+2*bar,d,bar))
    return r
GABLE_CLIP=lambda W,H:[(Vector((-W/2,0,0)),Vector((-H,0,W/2)).normalized()),(Vector((W/2,0,0)),Vector((H,0,W/2)).normalized())]
PIECES={  # name: (boxes, fracture grid hint per box (n along longest axes), clip planes, anchor mode)
 "Wall":lambda m:(wall(TH[m]),None,"ground"),
 "WallDoor":lambda m:(walldoor(TH[m],2.0 if m=="Steel" else 1.0,2.4 if m=="Steel" else 2.1),None,"ground"),
 "WallWindow":lambda m:(wallwin(TH[m]),None,"ground"),
 "Floor":lambda m:([B(0,0,0,3,3,TH[m] if m!="Stone" else 0.3)],None,"support"),
 "Joint":lambda m:([B(0,0,0,{"Brick":0.4,"Stone":0.45}.get(m,0.3),{"Brick":0.4,"Stone":0.45}.get(m,0.3),3)],None,"ground"),
 "DoorFrame":lambda m:(frame(1.0,2.1,False),None,"ground"),
 "WindowFrame":lambda m:(frame(1.0,1.0,True,z=0.15),None,"support"),
 "Gable":lambda m:([B(0,0,0,3.0,TH[m],1.05)],GABLE_CLIP(3.0,1.05),"support"),
 "Roof":lambda m:([B(0,0,0,3.6,2.3,0.16)],None,"support"),
 "GlassWall":lambda m:([B(0,0,0.12,2.76,0.03,2.76),B(-1.44,0,0,0.12,0.12,3),B(1.44,0,0,0.12,0.12,3),B(0,0,0,2.76,0.12,0.12,True),B(0,0,2.88,2.76,0.12,0.12,True)],None,"ground"),
}
for k in ("Wall",):
    pass
for nm in ("GlassWall",):   # frame flags on the posts too
    pass
KIT={"Wood":["Wall","WallDoor","WallWindow","Floor","Joint","DoorFrame","WindowFrame","Gable"],
     "Brick":["Wall","WallDoor","WallWindow","Floor","Joint","Gable"],
     "Steel":["Wall","WallDoor","WallWindow","Floor","Joint"],
     "Stone":["Wall","WallDoor","WallWindow","Floor","Joint"],
     "Glass":["Wall"],
     "Roof":["Roof"]}
def piece_def(m,p):
    if m=="Glass" and p=="Wall":
        bx,cl,an=PIECES["GlassWall"](m)
        for b in bx[1:]: b["frame"]=True
        return bx,cl,an
    return PIECES[p](m)
def clip(bm,planes):
    for co,no in planes:
        r=bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=co,plane_no=no,clear_outer=True)
        bmesh.ops.contextual_create(bm,geom=[e for e in r['geom_cut'] if isinstance(e,bmesh.types.BMEdge)])
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-5); return bm
def mv(bm,v): bmesh.ops.translate(bm,verts=bm.verts,vec=v); return bm
def merge(dst,src):
    me=bpy.data.meshes.new("t"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)
def outer_uv(f,uv):
    n=f.normal; ax=max(range(3),key=lambda a:abs(n[a]))
    for l in f.loops:
        p=l.vert.co; l[uv].uv=((p[1] if ax==0 else p[0])/2,(p[1] if ax==2 else p[2])/2)
def cells_bm(mat,b,lo,hi,frame_mi):
    """LOD0 detail for one sub-box: material cells proud of a core box (see v3 kit). Brick rows staggered to match T_Brick_A."""
    sx,sy,sz=b["s"]; D=[sx,sy,sz]; th=min(range(3),key=lambda a:D[a]); ax=[a for a in range(3) if a!=th]
    lng=max(ax,key=lambda a:D[a]); sh=[a for a in ax if a!=lng][0]
    T=12 if mat=="Brick" else 44
    c1,c2=CELLSZ[mat]
    if lng==2 or (sh!=2 and lng!=2 and False): pass
    # sizes along world axes: x uses c1, z uses c2 (brick 0.5 wide x 0.25 tall), y (floors) uses c2
    csz={0:c1,1:c2,2:c2}
    n={sh:max(1,round(D[sh]/csz[sh])),lng:max(1,round(D[lng]/csz[lng]))}
    while n[sh]*n[lng]<lo: n[lng if D[lng]/n[lng]>=D[sh]/n[sh] else sh]+=1
    while n[sh]*n[lng]>max(hi,1) and n[sh]*n[lng]>1: n[max(ax,key=lambda a:n[a])]-=1
    g=0.02 if mat!="Glass" else 0.04
    bm=box_bm(sx,sy,sz); off=[-sx/2,-sy/2,0]
    if mat=="Glass" or b["frame"]:
        if b["frame"] or mat=="Glass":
            if b["frame"]: bevel_hard(bm,0.015,1)
            for f in bm.faces: f.material_index=frame_mi if b["frame"] else 0
            return mv(bm,b["c"]-Vector((0,0,0)))
    # brick: texture rows start staggered at world z=0 (row 7 of the 8-row sheet), geometry follows world z
    bm2=bmesh.new()
    rows=lng if mat!="Brick" else (2 if 2 in ax else lng); cols=[a for a in ax if a!=rows][0]
    if mat=="Brick":
        n={rows:max(1,round(D[rows]/0.25)),cols:max(1,round(D[cols]/0.5))}
    for j in range(n[rows]):
        z_world=b["c"][rows]+off[rows]+(j+0.5)*D[rows]/n[rows] if rows==2 else 0
        stag=(mat=="Brick" and rows==2 and int(math.floor((b["c"][2]+(j)*0.25)/0.25+1e-6))%2==0)
        for i in range(n[cols]+(1 if stag else 0)):
            c=[0,0,0]; s=[0,0,0]
            L=D[rows]/n[rows]; c[rows]=off[rows]+(j+0.5)*L; s[rows]=L-g
            L=D[cols]/n[cols]
            if stag:
                # align to the global 0.5 m brick grid shifted by 0.25 m
                a0=off[cols]+(i-0.5)*L
            else: a0=off[cols]+i*L
            a1=a0+L; a0=max(a0,off[cols]); a1=min(a1,off[cols]+D[cols])
            if a1-a0<0.06: continue
            c[cols]=(a0+a1)/2; s[cols]=(a1-a0)-g
            c[th]=off[th]+D[th]/2; s[th]=D[th]+0.02
            bb=box_bm(*s)
            for v in bb.verts: v.co+=Vector((c[0],c[1],c[2]-s[2]/2))
            if T==44: bevel_hard(bb,min(s)*0.15,1)
            merge(bm2,bb)
    merge(bm,bm2); return mv(bm,b["c"])
def seeds_for(b,k,rnd):
    s=b["s"]; ax=sorted(range(3),key=lambda a:-s[a]); n=[1,1,1]
    while n[0]*n[1]*n[2]<k:
        a=max(ax[:2],key=lambda a:s[a]/(n[a]+1))
        if s[a]/(n[a]+1)<0.3: break
        n[a]+=1
    S=[]
    for i in range(n[0]):
        for j in range(n[1]):
            for kk in range(n[2]):
                p=[(idx+0.5+(rnd.random()-0.5)*0.5)/n[a]*s[a] for a,idx in zip(range(3),(i,j,kk))]
                S.append(Vector((p[0]-s[0]/2,p[1]-s[1]/2,p[2])))
    return S
def contact(oa,ob):
    """face contact between two fragment meshes (world space): opposite normals, coplanar, overlapping. Returns contact point or None."""
    ma,mb=oa.matrix_world,ob.matrix_world; ra,rb=ma.to_3x3(),mb.to_3x3()
    def aabb(o):
        p=[o.matrix_world@v.co for v in o.data.vertices]; return Vector([min(q[i] for q in p) for i in range(3)]),Vector([max(q[i] for q in p) for i in range(3)])
    a0,a1=aabb(oa); b0,b1=aabb(ob)
    if any(a0[i]>b1[i]+1e-3 or b0[i]>a1[i]+1e-3 for i in range(3)): return None
    pts=[]
    for pa in oa.data.polygons:
        na=(ra@pa.normal).normalized(); ca=ma@pa.center
        for pb in ob.data.polygons:
            nb=(rb@pb.normal).normalized()
            if na.dot(nb)>-0.999: continue
            cb=mb@pb.center
            if abs((cb-ca).dot(na))>1e-3: continue
            if (ca-cb).length<max(math.sqrt(pa.area),math.sqrt(pb.area))*0.75 and min(pa.area,pb.area)>2e-4: pts.append((ca+cb)/2)
    return sum(pts,Vector())/len(pts) if pts else None
def build(mat,piece):
    boxes,clips,anchor_mode=piece_def(mat,piece); name=f"Kit_{mat}_{piece}"
    outdir=os.path.join(OUT,f"Kit_{mat}"); texdir=os.path.join(outdir,"textures"); os.makedirs(texdir,exist_ok=True)
    reset()
    gen_tex("sheet",mat,texdir); gen_tex("inner",mat,texdir)
    M=material_pbr(f"M_{mat}",f"{texdir}/T_{mat}_A.png",f"{texdir}/T_{mat}_N.png",f"{texdir}/T_{mat}_ORM.png")
    MI=material_pbr(f"M_{mat}_Inner",f"{texdir}/T_{mat}_Inner_A.png",rough=0.8)
    grp=empty(name,(0,0,0)); clips=clips or []
    vol=[b["s"][0]*b["s"][1]*b["s"][2] for b in boxes]; area=[sorted(b["s"])[1]*sorted(b["s"])[2] for b in boxes]; At=sum(area)
    T=12 if mat=="Brick" else 44
    for lod in (0,1,2):
        bm=bmesh.new()
        for b,a in zip(boxes,area):
            if lod==0: sb=cells_bm(mat,b,max(1,math.ceil(560*a/At/T)),max(1,math.floor(1300*a/At/T)),1)
            else:
                sb=box_bm(*b["s"]); 
                if lod==1: bevel_hard(sb,min(b["s"])*0.12,1)
                for f in sb.faces: f.material_index=1 if b["frame"] else 0
                mv(sb,b["c"])
            merge(bm,sb)
        clip(bm,clips); uv=bm.loops.layers.uv.verify()
        for f in bm.faces: outer_uv(f,uv)
        o=mesh_from_bm(f"{name}_LOD{lod}",bm,[M,MI],grp); add_vcol(o); o["static"]=True
        if lod==0: lt=tris(o)
    for i,b in enumerate(boxes[:8]):
        u=mesh_from_bm(f"UCX_{name}_LOD0_{i+1:02d}",clip(mv(box_bm(*b["s"]),b["c"]),clips),[],grp); u.display_type='WIRE'
    add_lightmap_uv([o for o in bpy.data.objects if o.name.startswith(name+"_LOD")])
    save_src(f"Kit_{mat}",name)
    export(os.path.join(outdir,f"{name}.fbx"),f"{GLB}/Kit_{mat}/source/{name}.glb")
    # ---- fracture
    lo,hi=RNG[mat]; target=(lo+hi)//2; dens=DENS[mat]; Vt=sum(vol)
    for attempt in range(300):
        rnd=random.Random(hash((mat,piece))%1000+attempt*7); cells=[]; ok=True
        k_each=[max(1,round(target*v/Vt)) for v in vol]
        while sum(k_each)<lo: k_each[vol.index(max(vol))]+=1
        while sum(k_each)>hi: k_each[k_each.index(max(k_each))]-=1
        for b,k in zip(boxes,k_each):
            S=seeds_for(b,k,rnd)
            for c in voronoi_cells(*b["s"],S): cells.append((clip(mv(c,b["c"]),clips),b))
        cells=[(c,b) for c,b in cells if len(c.verts)>=4]
        if not (lo<=len(cells)<=min(hi,32)): ok=False
        for c,b in cells:
            vs=[v.co for v in c.verts]; ext=[max(q[a] for q in vs)-min(q[a] for q in vs) for a in range(3)]
            if min(ext)<MINAX.get(mat,0.15)-1e-4 or abs(c.calc_volume())*dens<0.5: ok=False
        if ok: break
    reset(); M=material_pbr(f"M_{mat}",f"{texdir}/T_{mat}_A.png",f"{texdir}/T_{mat}_N.png",f"{texdir}/T_{mat}_ORM.png"); MI=material_pbr(f"M_{mat}_Inner",f"{texdir}/T_{mat}_Inner_A.png",rough=0.8)
    root=empty(name+"_Fractured"); frags=[]
    def on_sub(f,b):
        c=f.calc_center_median()-b["c"]; s=b["s"]; n=f.normal
        return (abs(abs(c.x)-s.x/2)<1e-4 and abs(abs(n.x)-1)<1e-3) or (abs(abs(c.y)-s.y/2)<1e-4 and abs(abs(n.y)-1)<1e-3) or ((abs(c.z)<1e-4 or abs(c.z-s.z)<1e-4) and abs(abs(n.z)-1)<1e-3) or \
            any(abs((f.calc_center_median()-co).dot(no))<1e-4 for co,no in clips)
    for i,(bm,b) in enumerate(cells):
        fn=f"{name}_frag_{i+1:02d}"; hull=bm.copy()
        bevel_hard(bm,0.01 if mat=="Glass" else 0.025,1)
        uv=bm.loops.layers.uv.verify()
        for f in bm.faces:
            if on_sub(f,b): f.material_index=1 if b["frame"] else 0; outer_uv(f,uv)
            else:
                f.material_index=1; n=f.normal; t=n.orthogonal().normalized(); bb=n.cross(t)
                pts=[(l.vert.co.dot(t),l.vert.co.dot(bb)) for l in f.loops]
                mu=min(p[0] for p in pts); mv_=min(p[1] for p in pts); sc=max(max(p[0] for p in pts)-mu,max(p[1] for p in pts)-mv_,1e-6)
                for l,p in zip(f.loops,pts): l[uv].uv=((p[0]-mu)/sc,(p[1]-mv_)/sc)
        bmt=bm.copy(); bmesh.ops.triangulate(bmt,faces=bmt.faces[:]); acc=Vector(); V=0
        for f in bmt.faces:
            a,b_,c=[v.co for v in f.verts]; v6=a.dot(b_.cross(c)); acc+=v6*(a+b_+c)/4; V+=v6
        bmt.free(); com=acc/V; vol_=abs(V)/6
        for v in bm.verts: v.co-=com
        for v in hull.verts: v.co-=com
        ob=mesh_from_bm(fn,bm,[M,MI],root,com); add_vcol(ob); ob["mass_kg"]=round(vol_*dens,2); ob["density_kg_m3"]=dens
        u=mesh_from_bm("UCX_"+fn,hull,[],root,com); u.display_type='WIRE'; frags.append(ob)
    bpy.context.view_layer.update()
    for i in range(len(frags)):
        for j in range(i+1,len(frags)):
            p=contact(frags[i],frags[j])
            if p is not None:
                e=empty(f"JNT_{frags[i].name}__{frags[j].name}",p,root,0.1); e["break_force_N"]=""; e["break_torque_Nm"]=""
    zmin=min((o.matrix_world@v.co).z for o in frags for v in o.data.vertices)
    for ob in frags:
        W=[ob.matrix_world@v.co for v in ob.data.vertices]
        if anchor_mode=="ground":
            bot=[w for w in W if w.z<zmin+1e-3]
            if bot: c=sum(bot,Vector())/len(bot); a=empty("ANCHOR_"+ob.name,(c.x,c.y,c.z),root,0.15); a["anchor_to"]="Ground"
        else:   # support: fragments touching the bearing edge/bottom (floors: perimeter; roofs: lower eave; gables: base)
            xs=[w.x for o in frags for w in [o.matrix_world@v.co for v in o.data.vertices]]; ys=[w.y for o in frags for w in [o.matrix_world@v.co for v in o.data.vertices]]
            X0,X1,Y0,Y1=min(xs),max(xs),min(ys),max(ys)
            if piece=="Floor": sel=[w for w in W if min(w.x-X0,X1-w.x,w.y-Y0,Y1-w.y)<0.2]
            elif piece=="Roof": sel=[w for w in W if w.y-Y0<0.35]
            else: sel=[w for w in W if w.z<zmin+1e-3]
            if sel: c=sum(sel,Vector())/len(sel); a=empty("ANCHOR_"+ob.name,(c.x,c.y,c.z),root,0.15); a["anchor_to"]="Support"
    export(os.path.join(outdir,f"{name}_Fractured.fbx"),f"{GLB}/Kit_{mat}/source/{name}_Fractured.glb")
    save_src(f"Kit_{mat}",name+"_Fractured")
    ft=[tris(o) for o in frags]
    print(f"BUILT {name} LOD0={lt} frags={len(frags)} frag_tris_max={max(ft)} frac_total={sum(ft)} mass={min(o['mass_kg'] for o in frags)}-{max(o['mass_kg'] for o in frags)}",flush=True)
for m,ps in KIT.items():
    if ONLYM and m not in ONLYM: continue
    for p in ps:
        if ONLYP and p not in ONLYP: continue
        build(m,p)
