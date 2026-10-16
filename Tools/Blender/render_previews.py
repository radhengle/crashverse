import bpy,os,sys,math,glob
from mathutils import Vector
R="/workspace/wt/3d-modeler/models"; P=os.path.join(R,"previews"); os.makedirs(P,exist_ok=True)
def imp(f,off=(0,0,0),explode=0.0):
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=f); new=[o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None: o.location+=Vector(off)
        if o.name.startswith("UCX_"): o.hide_render=True
    bpy.context.view_layer.update()
    if explode:
        for o in new:
            if "_frag_" in o.name and not o.name.startswith("UCX"):
                c=o.matrix_world.translation; root=o.parent.matrix_world.translation if o.parent else Vector()
                d=(c-root); d.z*=0.6; o.location+=d*explode
    return new
def render(name,cam_dir=(1,-1.3,0.8),lens=50):
    sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sh=sc.display.shading
    sh.light='STUDIO'; sh.color_type='TEXTURE'; sh.show_specular_highlight=True; sh.show_object_outline=True; sh.object_outline_color=(0.06,0.09,0.16)
    sh.show_shadows=True; sh.show_cavity=True
    sc.render.resolution_x=sc.render.resolution_y=768; sc.render.film_transparent=False
    w=bpy.data.worlds.new("w"); sc.world=w; w.color=(0.55,0.78,1.0); sc.display.shading.background_type='WORLD'
    pts=[o.matrix_world@Vector(c) for o in sc.objects if o.type=='MESH' and not o.hide_render for c in o.bound_box]
    mn=Vector([min(p[i] for p in pts) for i in range(3)]); mx=Vector([max(p[i] for p in pts) for i in range(3)]); c=(mn+mx)/2; r=(mx-mn).length/2
    cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); cam.data.lens=lens
    d=Vector(cam_dir).normalized(); cam.location=c+d*r*2.6; f=(c-cam.location).normalized(); rgt=f.cross(Vector((0,0,1))).normalized(); up=rgt.cross(f)
    from mathutils import Matrix
    cam.rotation_euler=Matrix((rgt,up,-f)).transposed().to_euler(); sc.camera=cam
    sc.render.filepath=os.path.join(P,name+".png"); bpy.ops.render.render(write_still=True); print("PREVIEW",sc.render.filepath)
def fresh(): bpy.ops.wm.read_factory_settings(use_empty=True)
fresh(); imp(f"{R}/Wrecker_v2/Wrecker_v2.fbx"); render("Wrecker_v2",(1.2,-1.0,0.5))
fresh(); imp(f"{R}/Island_Sunnyville/Island_Sunnyville.fbx"); render("Island_Sunnyville",(0.6,-1.2,0.6))
for m in ("Wood","Brick","Steel"):
    fresh(); K=f"{R}/BuildingKit/Kit_{m}"; x=0
    for p,w in (("Wall",3.6),("Floor",3.8),("Joint",1.2)):
        imp(f"{K}/Kit_{m}_{p}.fbx",(x,0,0)); imp(f"{K}/Kit_{m}_{p}_Fractured.fbx",(x,-0 ,0) if False else (x,4.5,0),explode=0.25); x+=w+0.6
    render(f"BuildingKit_{m}",(0.5,-1.4,0.9))
