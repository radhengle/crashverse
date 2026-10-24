# Tiling kit sheets baked from real high-poly tiles: 4 m x 4 m tile -> 1024 px (256 px/m). Seamless via 3x3 wrap copies.
import sys,os,bpy,bmesh,random,math; sys.path.insert(0,os.path.dirname(__file__)); import hp
from mathutils import Vector, Matrix
OUT=sys.argv[sys.argv.index("--")+1]; KINDS=sys.argv[sys.argv.index("--")+2].split(",")
T=4.0; SIZE=1024
def link(o): bpy.context.scene.collection.objects.link(o); return o
def mesh(name,bm,mat):
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat.m); return link(bpy.data.objects.new(name,me))
def box(bm,c,s):
    r=bmesh.ops.create_cube(bm,size=1); 
    for v in r['verts']: v.co=Vector((v.co.x*s[0]+c[0],v.co.y*s[1]+c[1],v.co.z*s[2]+c[2]))
    return r['verts']
def cyl(bm,c,r,h,seg):
    q=bmesh.ops.create_cone(bm,cap_ends=True,segments=seg,radius1=r,radius2=r,depth=h)
    for v in q['verts']: v.co+=Vector(c)
def sphere(bm,c,r):
    q=bmesh.ops.create_uvsphere(bm,u_segments=10,v_segments=6,radius=r)
    for v in q['verts']: v.co=Vector((v.co.x+c[0],v.co.y+c[1],max(v.co.z,-0.002*0)+c[2]))
def mods(o,bev=0.004,seg=2,disp=0.0,dscale=0.08,sub=0):
    if sub: m=o.modifiers.new("sub",'SUBSURF'); m.subdivision_type='SIMPLE'; m.levels=m.render_levels=sub
    if bev: b=o.modifiers.new("bev",'BEVEL'); b.width=bev; b.segments=seg; b.limit_method='ANGLE'
    if disp:
        s2=o.modifiers.new("sub2",'SUBSURF'); s2.subdivision_type='SIMPLE'; s2.levels=s2.render_levels=1
        t=bpy.data.textures.new("chips",'CLOUDS'); t.noise_scale=dscale; t.noise_depth=3
        d=o.modifiers.new("disp",'DISPLACE'); d.texture=t; d.strength=disp; d.mid_level=0.75; d.texture_coords='GLOBAL'
def wrap(fn):
    """call fn(bm, ox, oy) for 3x3 tile offsets"""
    bm=bmesh.new()
    for ox in (-T,0,T):
        for oy in (-T,0,T): fn(bm,ox,oy)
    return bm
def ramp_tint(nt,base,var,src="island"):
    g=nt.nodes.new("ShaderNodeNewGeometry") if src=="island" else None
    oi=nt.nodes.new("ShaderNodeObjectInfo")
    mx=nt.nodes.new("ShaderNodeMix"); mx.data_type='RGBA'; mx.blend_type='MULTIPLY'; mx.inputs[0].default_value=1
    r=nt.nodes.new("ShaderNodeMapRange"); r.inputs[3].default_value=1-var; r.inputs[4].default_value=1+var
    nt.links.new((g.outputs["Random Per Island"] if g else oi.outputs["Random"]),r.inputs[0])
    c=nt.nodes.new("ShaderNodeCombineColor"); [nt.links.new(r.outputs[0],c.inputs[i]) for i in range(3)]
    rgb=nt.nodes.new("ShaderNodeRGB"); rgb.outputs[0].default_value=tuple(base)+(1,)
    nt.links.new(rgb.outputs[0],mx.inputs[6]); nt.links.new(c.outputs[0],mx.inputs[7]); return mx.outputs[2]
def lp_plane():
    bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=1,y_segments=1,size=T/2)
    uv=bm.loops.layers.uv.new("UV0")
    for f in bm.faces:
        for l in f.loops: l[uv].uv=(l.vert.co.x/T+0.5,l.vert.co.y/T+0.5)
    me=bpy.data.meshes.new("LP"); bm.to_mesh(me); bm.free(); return link(bpy.data.objects.new("LP",me))
def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True); hp.ALL.clear(); R=random.Random(5); hps=[]
    if kind=="Wood":
        def wcol(nt):
            base=ramp_tint(nt,hp.lin("#E3A766"),0.12)
            w=nt.nodes.new("ShaderNodeTexWave"); w.wave_type='BANDS'; w.bands_direction='X'; w.inputs["Scale"].default_value=18; w.inputs["Distortion"].default_value=9; w.inputs["Detail"].default_value=4
            tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(1,0.08,1)
            nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],w.inputs[0])
            mx=nt.nodes.new("ShaderNodeMix"); mx.data_type='RGBA'; mx.blend_type='MULTIPLY'; mx.inputs[0].default_value=0.22
            nt.links.new(base,mx.inputs[6]); nt.links.new(w.outputs[0],mx.inputs[7]); return mx.outputs[2]
        def wbump(nt):
            w=nt.nodes.new("ShaderNodeTexWave"); w.wave_type='BANDS'; w.bands_direction='X'; w.inputs["Scale"].default_value=18; w.inputs["Distortion"].default_value=9
            tc=nt.nodes.new("ShaderNodeTexCoord"); mp=nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value=(1,0.08,1)
            nt.links.new(tc.outputs["Object"],mp.inputs[0]); nt.links.new(mp.outputs[0],w.inputs[0])
            b=nt.nodes.new("ShaderNodeBump"); b.inputs["Strength"].default_value=0.25; b.inputs["Distance"].default_value=0.002; nt.links.new(w.outputs[0],b.inputs["Height"]); return b.outputs[0]
        MW=hp.HPMat("HP_Wood",wcol,0.66,0.0,bump=wbump); MN=hp.HPMat("HP_Nail",hp.lin("#B8BEC6"),0.35,1.0,bump=lambda nt:hp.noise_bump(nt,300,0.3,0.0005))
        MG=hp.HPMat("HP_Gap",hp.lin("#5A3A20"),0.85,0.0)
        splits=[R.uniform(0.6,3.4) for _ in range(16)]; tilt=[R.uniform(-0.002,0.002) for _ in range(16)]
        def boards(bm,ox,oy):
            for i in range(16):
                x=-T/2+0.125+i*0.25+ox
                for y0,y1 in ((-T/2,-T/2+splits[i]),(-T/2+splits[i],T/2)):
                    vs=box(bm,(x,(y0+y1)/2+oy,-0.012+tilt[i]),(0.242,y1-y0-0.006,0.024))
        def nails(bm,ox,oy):
            for i in range(16):
                for y in (-1.6,0.4):
                    for dx in (-0.06,0.06): cyl(bm,(-T/2+0.125+i*0.25+dx+ox,y+oy,0.0015),0.011,0.006,12)
        o=mesh("HP_Boards",wrap(boards),MW); mods(o,0.006,3); hps.append(o)
        o=mesh("HP_Nails",wrap(nails),MN); mods(o,0.002,2); hps.append(o)
        bm=bmesh.new(); box(bm,(0,0,-0.03),(3*T,3*T,0.01)); hps.append(mesh("HP_Back",bm,MG))
    elif kind=="Brick":
        MB=hp.HPMat("HP_Brick",lambda nt:ramp_tint(nt,hp.lin("#D2664C"),0.14),
                    lambda nt:(lambda r:(r.inputs[3].__setattr__("default_value",0.6),r.inputs[4].__setattr__("default_value",0.74),nt.links.new(nt.nodes.new("ShaderNodeNewGeometry").outputs["Random Per Island"],r.inputs[0]),r.outputs[0])[-1])(nt.nodes.new("ShaderNodeMapRange")),
                    0.0,bump=lambda nt:hp.noise_bump(nt,60,0.35,0.002))
        MM=hp.HPMat("HP_Mortar",hp.lin("#7A3326"),0.88,0.0,bump=lambda nt:hp.noise_bump(nt,120,0.5,0.002))
        def bricks(bm,ox,oy):
            for r in range(16):
                off=0.25 if r%2 else 0.0
                for c in range(9):
                    x=-T/2+off+c*0.5+ox; y=-T/2+0.125+r*0.25+oy
                    if abs(x)>T/2+0.35 or abs(y)>T/2+0.2: continue   # only bricks touching the centre tile
                    box(bm,(x,y,-0.02),(0.46,0.21,0.04))
        o=mesh("HP_Bricks",wrap(bricks),MB); mods(o,0.012,1,disp=0.012,dscale=0.06,sub=3); hps.append(o)
        bm=bmesh.new(); box(bm,(0,0,-0.03),(3*T,3*T,0.01)); hps.append(mesh("HP_Mortar",bm,MM))
    else: # Steel
        MS=hp.HPMat("HP_Steel",lambda nt:ramp_tint(nt,hp.lin("#C8CFD8"),0.04),lambda nt:(lambda n:(n.inputs["Scale"].__setattr__("default_value",30),nt.links.new(n.outputs[0],nt.nodes.new("ShaderNodeMath").inputs[0]),n)[2])(nt.nodes.new("ShaderNodeTexNoise")).outputs[0] if False else hp.HPMat.val.__get__(type("x",(),{"nt":nt}))(0.3),1.0,
                    bump=lambda nt:hp.noise_bump(nt,400,0.08,0.0004))
        MR=hp.HPMat("HP_Rivet",hp.lin("#C8CFD8"),0.28,1.0); MD=hp.HPMat("HP_SteelGap",hp.lin("#5B6472"),0.5,1.0)
        def plates(bm,ox,oy):
            for i in range(4):
                for j in range(4): box(bm,(-T/2+0.5+i+ox,-T/2+0.5+j+oy,-0.006),(0.992,0.992,0.012))
        def rivets(bm,ox,oy):
            for i in range(4):
                for j in range(4):
                    cx,cy=-T/2+0.5+i+ox,-T/2+0.5+j+oy
                    for k in range(1,9):
                        t=-0.5+k*0.111
                        for p in ((cx+t,cy-0.45),(cx+t,cy+0.45),(cx-0.45,cy+t),(cx+0.45,cy+t)): sphere(bm,(p[0],p[1],0.0),0.012)
                    for sx in (-0.45,0.45):
                        for sy in (-0.45,0.45): cyl(bm,(cx+sx,cy+sy,0.004),0.022,0.01,6); cyl(bm,(cx+sx,cy+sy,0.0005),0.03,0.002,12)
        o=mesh("HP_Plates",wrap(plates),MS); mods(o,0.004,3); hps.append(o)
        hps.append(mesh("HP_Rivets",wrap(rivets),MR))
        bm=bmesh.new(); box(bm,(0,0,-0.02),(3*T,3*T,0.01)); hps.append(mesh("HP_Back",bm,MD))
    lp=lp_plane(); bpy.context.view_layer.update()
    paths=hp.bake_maps(lp,hps,SIZE,kind,OUT,extrusion=0.05,ray=0.1,ao_dist=0.06,ao_samples=48)
    for k,p in paths.items(): print("SHEET",kind,k,p,hp.png_stats(p))
    bpy.ops.wm.save_as_mainfile(filepath=f"/workspace/crashverse/models_src/Sheets/Sheet_{kind}_HP.blend",compress=True) if os.makedirs("/workspace/crashverse/models_src/Sheets",exist_ok=True) is None else None
for k in KINDS: build(k)
