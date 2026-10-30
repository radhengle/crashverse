#!/usr/bin/env python3
"""Building / kit-module inventory for the Ch1 + Daily Boss re-layout (read-only on the 3D Modeler's branch).
Reads origin/3d-modeler/realistic-v1 with `git show` (never checks it out) and writes prefab_inventory.json:
  buildings: Bld_*.layout.json (crashverse.building_layout v1): pieces, kit module, intact/fractured FBX, anchor_to,
             fracture_class, connections (JNT break force/torque) and the assembled AABB (Unity metres, Y up)
  modules:   models/HeroBuildings/Kit_*_stats.json: size, pivot, fragments, joints, anchors, mass range, fracture class
Stats AABBs are Blender space (Z up, Y = depth); Unity size = (x, z, y). Module pivot: anchor 'bottom' / aabb z-min 0 =
bottom-centre; Kit_Real* fragments pivot at their centre of mass (pivot_com).
Usage: inventory.py [ref]   (default origin/3d-modeler/realistic-v1)"""
import json, math, os, subprocess, sys
HERE=os.path.dirname(os.path.abspath(__file__)); REF=sys.argv[1] if len(sys.argv)>1 else 'origin/3d-modeler/realistic-v1'
ROOT=os.path.abspath(os.path.join(HERE,'../..'))
git=lambda *a: subprocess.run(['git',*a],cwd=ROOT,capture_output=True,text=True,check=True).stdout
def show(p): return json.loads(git('show',f'{REF}:{p}'))
files=git('ls-tree','-r','--name-only',REF).split('\n')
fbx=set(f for f in files if f.endswith('.fbx'))
modules={}
for f in sorted(x for x in files if x.startswith('models/HeroBuildings/Kit_') and x.endswith('_stats.json')):
    kit=os.path.basename(f)[:-len('_stats.json')]; d=show(f)
    for m,v in d['modules'].items():
        name=f'{kit}_{m}'
        if 'aabb' not in v:   # Kit_RealHouse (realistic townhouse) stats: bounds_m = Blender x/y/z extents, pivot = module pivot
            bx,by,bz=v['bounds_m']; intact=[p for p in fbx if p.endswith('/'+name+'.fbx')]; frac=[p for p in fbx if p.endswith('/'+name+'_Fractured.fbx')]
            modules[name]=dict(kit=kit,module=m,size_unity=[bx,bz,by],pivot='module pivot (centre of the module base footprint, +Z street)',
                frag_pivot='fragment volume centre of mass',frags=v.get('fragments'),joints=v.get('joints'),anchors=v.get('anchors'),
                fracture_class=v.get('frac') or 'Indestructible',mass_kg=v.get('mass_kg_total'),density_kg_m3=v.get('density_kg_m3'),
                lod0_tris=(v.get('tris') or {}).get('LOD0') if isinstance(v.get('tris'),dict) else v.get('tris'),
                intact_fbx=intact[0] if intact else None,fractured_fbx=frac[0] if frac else None)
            continue
        (x0,y0,z0),(x1,y1,z1)=v['aabb']
        intact=[p for p in fbx if p.endswith('/'+name+'.fbx')]; frac=[p for p in fbx if p.endswith('/'+name+'_Fractured.fbx')]
        modules[name]=dict(kit=kit,module=m,size_unity=[round(x1-x0,3),round(z1-z0,3),round(y1-y0,3)],
            pivot='bottom-centre' if abs(z0)<0.02 else 'top-centre (slab hangs below its pivot)' if abs(z1)<0.02
                  else f'bottom-centre (mesh dips {-z0:.2f} m below the pivot)' if z0<0 else 'offset',
            frag_pivot=v.get('pivot_com','fragment origin per #28 kit'),frags=v.get('frags'),joints=v.get('joints'),anchors=v.get('anchors'),
            anchor_to=v.get('anchor_to'),fracture_class=v.get('frac'),mass_kg=v.get('mass'),lod0_tris=v.get('LOD0'),
            intact_fbx=intact[0] if intact else None,fractured_fbx=frac[0] if frac else None,note=v.get('note',''))
def rotY(x,z,deg):
    a=math.radians(deg); return x*math.cos(a)+z*math.sin(a),-x*math.sin(a)+z*math.cos(a)
buildings={}
for f in sorted(x for x in files if x.startswith('Assets/_Project/Models/Buildings/Bld_') and x.endswith('.layout.json')):
    d=show(f); name=d['building']; lo=[1e9]*3; hi=[-1e9]*3; pcs=[]
    for p in d['pieces']:
        m=modules.get(p['kit']); pos=p['position']; ry=p['euler_zxy_deg']['y']
        if m:
            sx,sy,sz=m['size_unity']; hx,hz=abs(rotY(sx/2,0,ry)[0])+abs(rotY(0,sz/2,ry)[0]),abs(rotY(sx/2,0,ry)[1])+abs(rotY(0,sz/2,ry)[1])
            for i,(c,h) in enumerate(((pos['x'],hx),(pos['y']+sy/2,sy/2),(pos['z'],hz))): lo[i]=min(lo[i],c-h); hi[i]=max(hi[i],c+h)
        pcs.append(dict(id=p['id'],kit=p['kit'],pos=[pos['x'],pos['y'],pos['z']],rotY=ry,anchor_to=p['anchor_to'],
                        fracture_class=p.get('fracture_class') or (m or {}).get('fracture_class'),indestructible=p.get('indestructible',False),
                        intact=p.get('intact'),fractured=p.get('fractured')))
    buildings[name]=dict(layout=f,pieces=pcs,piece_count=len(pcs),connections=len(d.get('connections',[])),
        joint_break_values_set=any(c.get('break_force_N') not in ('',None) for c in d.get('connections',[])),
        aabb_unity=[[round(v,2) for v in lo],[round(v,2) for v in hi]] if lo[0]<1e8 else None,
        size_unity=[round(hi[i]-lo[i],2) for i in range(3)] if lo[0]<1e8 else None,
        sized_from='Kit_*_stats.json module AABBs' if lo[0]<1e8 else 'no stats JSON for its kit (v3 kit): size unknown',
        realistic_variant=next((x for x in fbx if f'/Hero/{name}/' in x),None))
prefabs=[x for x in files if x.endswith('.prefab')]
for f in sorted(x for x in files if x.startswith('Assets/_Project/Models/Realistic/Buildings/') and x.endswith('.layout.json')):
    d=show(f); name=d['building']+' (realistic)'; fp=d.get('footprint_m',{})
    buildings[name]=dict(layout=f,style=d.get('style'),space=d.get('space'),footprint_m=[fp.get('x'),fp.get('z')],
        modules=[dict(id=m['id'],label=m['label'],pivot=[m['pivot'][k] for k in 'xyz'],footprint_m=[m['footprint_m']['x'],m['footprint_m']['z']],
                      height_m=m['height_m'],pieces=m['pieces'],anchor_to=m['anchor_to']) for m in d['modules']],
        pieces=[dict(id=p['id'],module=p['module'],kit=p['kit'],pos=[p['position'][k] for k in 'xyz'],anchor_to=p['anchor_to'],
                     fracture_class=p['fracture_class'],intact=p.get('intact'),fractured=p.get('fractured')) for p in d['pieces']],
        piece_count=d['piece_count'],module_count=d['module_count'],connections=len(d['connections']),
        joint_break_values_set=any(c.get('break_force_N') not in ('',None) for c in d['connections']),anchor_note=d.get('anchor_note'))
out=dict(ref=REF,commit=git('rev-parse','--short',REF).strip(),unity_prefabs=prefabs,buildings=buildings,modules=modules)
json.dump(out,open(os.path.join(HERE,'prefab_inventory.json'),'w'),indent=1)
print('ref',out['commit'],'unity .prefab files:',len(prefabs))
for n,b in buildings.items(): print(f"{n:28} pieces {b['piece_count']:3} size {b.get('size_unity') or b.get('footprint_m')}")
