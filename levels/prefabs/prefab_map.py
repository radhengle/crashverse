#!/usr/bin/env python3
"""Maps every Ch1 L1-10 and Daily Boss (Daily Titan) layout piece to a 3D Modeler building-kit module (rev 8).
rev 8: L3 blocks are the realistic Bld_Townhouse LD modules (one block per module; the module pieces spawn at its pivot).
Data-only sidecar: LevelBuilder.Build still spawns one scaled cube per BlockData and has no field to name a model,
so nothing here is read at runtime yet (see CODE GAPS in PREFAB_MAP.md). Each entry gives the module, its intact and
fractured FBX, the per-axis scale that turns the module's AABB into the block's size, the yaw the module needs (BlockData
has no rotation field), and whether the module's fracture class matches the gameplay material.
Inputs: ../ch1/gen_levels.py (authored +z-front frame, before face_wrecker), Resources titan layouts, prefab_inventory.json.
Writes prefab_map.json and PREFAB_MAP.md."""
import json, os, re, sys
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,os.path.join(HERE,'../ch1'))
import gen_levels as G
INV=json.load(open(os.path.join(HERE,'prefab_inventory.json'))); MOD=INV['modules']
RES=os.path.join(HERE,'../../Assets/_Project/Resources/Data')
MATCLS={'wood':{'Wood'},'brick':{'Stone','Brick','Roof'},'steel':{'Steel'}}
# Building each level is re-skinned as (rev 7). 'stylized' = modular Kit_* (landed, breakable); 'realistic' = CC0 Real look.
BUILDING={3:'Bld_Townhouse (realistic, modular, e725121) - INTERIM until the bakery',7:'Bld_Watchtower',9:'Bld_Watchtower + Bld_Gatehouse wall','titan':'Bld_CastleKeep modules'}
WAITING={3:['8x6 m bakery prefab (3D Modeler, due Oct 31): L3 moves to it; the realistic townhouse is the interim skin'],
 7:['realistic Watchtower (only the stylized Kit_Tower set exists)'],
 8:['Stall module + arch with steel sign (3D Modeler, due Oct 31 - Nov 1): block layout kept until then'],
 9:['realistic Watchtower and Gatehouse wall (only stylized sets exist)'],
 'titan':['realistic castle keep/wall (only the stylized Kit_Keep set exists)'],
 10:['none planned: Tidewarden boss body is Developer/3D Modeler boss art, not a building prefab']}
EXPLICIT={  # level -> [(id regex, module, building piece it stands for)]
 3:[(r'propL|propR','Kit_RealWood_Joint','cellar prop (level scaffolding, not in the model)'),
    (r'slab[LR]','Kit_RealBrick_Floor','cellar deck (level scaffolding, not in the model)'),
    (r'awningPost','Kit_RealWood_Joint','shop awning post (level decoy)'),(r'awning','Kit_RealWood_Floor','shop awning (level decoy)')],
 7:[(r'plinth','Kit_Base_Indestructible','base'),(r'ring\d','Kit_RealSteel_Floor','gold clockwork ring between storeys (JNT seam)'),
    (r'wallF1','Kit_Tower_RingDoor','S0_RingDoor (front quarter)'),(r'wall[BLR]1','Kit_Tower_RingDoor','S0_RingDoor (quarter)'),
    (r'wall.2','Kit_Tower_RingSlit','S1_RingSlit (quarter)'),(r'crownFloor','Kit_RealBrick_Floor','Floor_4 (brick deck under the crown)'),
    (r'crownWood','Kit_Tower_RingWindow','S3_RingWindow (clock room)'),(r'crownBrick','Kit_Tower_Crown','Crown'),
    (r'crownRoof','Kit_Tower_Roof','Roof (spire)'),(r'annex','Kit_Props_Shed','Annex'),(r'wing[LR]','Kit_RealWood_Joint','hoist pole'),
    (r'crateTop','Kit_Props_CrateSmall','Crate_1'),(r'crate','Kit_Props_Crate','Crate_0'),(r'barrel','Kit_Props_Barrel','Barrel_*')],
 9:[(r'lhBase|breakwater','Kit_Base_Indestructible','base'),(r'lhRing','Kit_RealSteel_Floor','steel ring (JNT seam)'),
    (r'lhShaft0','Kit_Tower_RingSlit','S1_RingSlit'),(r'lhShaft1','Kit_Tower_RingSlit','S2_RingSlit'),(r'lhGallery','Kit_Tower_Floor','Floor_4 gallery'),
    (r'lhLantern','Kit_Tower_RingWindow','S3_RingWindow (lantern)'),(r'lhCap','Kit_Tower_Roof','Roof'),
    (r'prop[AB]','Kit_RealWood_Joint','seawall prop'),(r'seaDeck','Kit_RealBrick_Floor','seawall deck'),
    (r'sea\d','Kit_Gate_Wall','Gatehouse Wall_* segment'),(r'seaCrest','Kit_Gate_Crown','Gatehouse Crown (battlement)'),
    (r'bellTower','Kit_RealWood_Block','bell house'),(r'catwalk','Kit_RealWood_Floor','catwalk')],
 'titan':[(r'leg[LR]|arm[LR]','Kit_Keep_Turret','keep turret column'),(r'pelvis|neck','Kit_RealSteel_Floor','steel collar (JNT seam)'),
    (r'belly','Kit_Keep_WallBanner','keep wall (banner)'),(r'chest','Kit_Keep_WallSlit','keep wall (slits)'),
    (r'sh[LR]','Kit_RealSteel_Block','steel shoulder'),(r'head','Kit_RealWood_Block','head (timber)'),(r'crate','Kit_RealProps_Crate','prop'),
    (r'.*','Kit_Keep_Wall','keep wall')],
}
def generic(b):
    if b.get('indestructible') or b.get('role')=='island': return 'Kit_Base_Indestructible','held base / island'
    m={'wood':'RealWood','brick':'RealBrick','steel':'RealSteel'}[b['mat']]; sx,sy,sz=b['s']
    if b.get('role')=='roof' or (b['mat']=='brick' and b.get('color')=='4D7BC4'): return 'Kit_RealRoof_Roof','roof'
    if max(sx,sz)<=.6 and sy>=1.0: return f'Kit_{m}_Joint','post / stilt / prop'
    if sy<=.6 and min(sx,sz)>=.9: return f'Kit_{m}_Floor','deck / slab / beam'
    if min(sx,sz)<=.45 and sy>=.8: return f'Kit_{m}_Wall','wall panel'
    if b['mat']=='wood' and max(b['s'])<=1.0: return 'Kit_RealProps_Crate','small timber prop'
    return f'Kit_{m}_Block','block'
RH=next((v for k,v in INV['buildings'].items() if k.startswith('Bld_Townhouse (realistic')),None)
RHMOD={m['id']:m for m in (RH or {}).get('modules',[])}
MATRH={'Stone':'brick','Wood':'wood','Roof':'brick','Indestructible':'brick'}
def rh_entry(b):
    m=RHMOD[b['id']]; ps=[MOD['Kit_RealHouse_'+p] for p in m['pieces']]; cls=sorted({p['fracture_class'] for p in ps})
    return dict(id=b['id'],mat=b['mat'],role=b.get('role',''),size=b['s'],module='Bld_Townhouse(realistic) module '+m['id'],
        stands_for=m['label'],yaw_deg=0,scale=[1,1,1],module_pivot_house_local=m['pivot'],place_module_at=[round(m['pivot'][0],4),round(m['pivot'][1]+1.8,4),round(m['pivot'][2],4)],
        placement_note='authored +z-front frame before face_wrecker; y + 1.8 m cellar lift (Base stays at y 0)',module_footprint_xz=m['footprint_m'],
        module_height=m['height_m'],anchor_to=m['anchor_to'],pieces=m['pieces'],
        intact_fbx=[p['intact_fbx'] for p in ps],fractured_fbx=[p['fractured_fbx'] for p in ps if p['fractured_fbx']],
        frags=sum(p['frags'] or 0 for p in ps) or None,joints=sum(p['joints'] or 0 for p in ps) or None,
        fracture_class='/'.join(cls),material_match=all(MATRH[c]==b['mat'] for c in cls),
        breakable=not b.get('indestructible') and all(p['fractured_fbx'] for p in ps))
def entry(lv,b):
    if lv==3 and b['id'] in RHMOD: return rh_entry(b)
    mod=note=None
    for rx,mm,nn in EXPLICIT.get(lv,[]):
        if re.fullmatch(rx,b['id']): mod,note=mm,nn; break
    if not mod: mod,note=generic(b)
    m=MOD.get(mod); sx,sy,sz=b['s']; yaw=0
    if m:
        mx,my,mz=m['size_unity']
        if sx<sz and mx>mz*1.5: yaw=90; mx,mz=mz,mx    # side wall / thin-in-x piece: module turned 90 deg about Y
        scale=[round(sx/mx,2),round(sy/my,2),round(sz/mz,2)]
    else: scale=None
    cls=(m or {}).get('fracture_class') or ('Stone' if mod=='Kit_Base_Indestructible' else None)
    return dict(id=b['id'],mat=b['mat'],role=b.get('role',''),size=b['s'],module=mod,stands_for=note,yaw_deg=yaw,scale=scale,
        intact_fbx=(m or {}).get('intact_fbx') or f'Assets/_Project/Models/BuildingKit/Kit_Base/{mod}.fbx',
        fractured_fbx=(m or {}).get('fractured_fbx'),frags=(m or {}).get('frags'),joints=(m or {}).get('joints'),
        fracture_class=cls,material_match=(cls in MATCLS[b['mat']]) if cls else None,
        breakable=not b.get('indestructible') and bool((m or {}).get('fractured_fbx')))
out=dict(inventory_ref=INV['commit'],levels={})
for n,(lid,f) in G.LAYOUTS.items():
    out['levels'][f'c1_l{n:02d}']=dict(layout=lid,building=BUILDING.get(n,'generic Kit_Real* modules'),waiting_on=WAITING.get(n,[]),
                                       pieces=[entry(n,b) for b in f()])
for t in sorted(x[:-5] for x in os.listdir(os.path.join(RES,'Layouts')) if x.startswith('titan_') and x.endswith('.json')):
    bl=json.load(open(os.path.join(RES,'Layouts',t+'.json')))['blocks']
    out['levels'][t]=dict(layout=t,building=BUILDING['titan'],waiting_on=WAITING['titan'],frame='already rotated (BUG-24)',
                          pieces=[entry('titan',b) for b in bl])
json.dump(out,open(os.path.join(HERE,'prefab_map.json'),'w'),indent=1)
# ---- markdown
L=['# Prefab map, Ch1 L1-10 + Daily Titan (rev 8)','',f"Generated by `prefab_map.py` from `prefab_inventory.json` (3d-modeler/realistic-v1 @ {INV['commit']}). Data only: the runtime does not read it yet.",'']
for k,v in out['levels'].items():
    mm=sum(1 for p in v['pieces'] if p['material_match'] is False)
    L+=[f"## {k} `{v['layout']}`: {v['building']}",'',('Waiting on: '+'; '.join(v['waiting_on'])) if v['waiting_on'] else 'Waiting on: nothing (all modules landed)','',
        '| piece | mat | module | stands for | yaw | scale x/y/z | frags | class ok |','|---|---|---|---|---|---|---|---|']
    for p in v['pieces']:
        mod=p['module']+(f" ({'+'.join(p['pieces'])})" if p.get('pieces') else '')
        L.append(f"| {p['id']} | {p['mat']} | {mod} | {p['stands_for']} | {p['yaw_deg']} | {'/'.join(map(str,p['scale'])) if p['scale'] else '-'} | {p['frags'] or '-'} | {'-' if p['material_match'] is None else 'yes' if p['material_match'] else 'NO'} |")
    L+=['',f"{mm} piece(s) where the module's fracture class differs from the gameplay material (gameplay mat wins; the module only skins it)." if mm else '','']
open(os.path.join(HERE,'PREFAB_MAP.md'),'w').write('\n'.join(L))
for k,v in out['levels'].items(): print(k,v['building'],len(v['pieces']),'pieces; class mismatches',sum(1 for p in v['pieces'] if p['material_match'] is False),'; waiting',len(v['waiting_on']))
