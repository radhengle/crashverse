# CRASHVERSE model import pipeline (VFX & Tech Art)

Editor-only code in `Assets/_Project/Editor/Import/`. Scope is `Assets/_Project/Models/**` only. Target is Unity 2022.3 with URP 14 on Android.
Stacked on `vfx/v1-foundation` (PR #2), which holds the `Crashverse/GlossyPBR` shader.

| File | Role |
|---|---|
| `CrashverseImportConventions.cs` | Pure-C# rules for naming, asset classes, LOD heights, texture policy, budgets and texel targets |
| `CrashverseImportPostprocessor.cs` | **The only** model and texture `AssetPostprocessor`: FBX settings, texture import with Android ASTC, material hookup, `attach_to` |
| `MaterialWiring.cs` | Creates and wires `M_*` materials to GlossyPBR. Menu: **Crashverse ▸ Import ▸ Rewire Materials** |
| `CrashversePrefabBuilder.cs` | Menus: **Build Prefabs (all / selected)** |
| `Assets/_Project/Scripts/Import/ImportedPhysicsHints.cs` | Runtime **data-only** marker: `mass` (kg), `breakForce` (N), `breakTorque` (N·m), `anchorTo` (string) from FBX user properties |
| `CrashverseImportValidator.cs` | Menu: **Validation Report**. Writes `Logs/CrashverseImportReport.md` and never modifies assets |

## Ownership (one owner per concern)
| Concern | Owner |
|---|---|
| FBX export (axes, Apply Transform, scale 1, naming, hulls, sockets, LOD meshes, texture bakes) | 3D Modeler (Blender scripts) |
| FBX import settings, texture import and ASTC, material creation and wiring, prefabs, LODGroup, colliders, validation | VFX (this pipeline) |
| Rigidbody, joints from `JNT_*`, mass and break forces, destruction components on fragments | Physics |
| `Fragment` tag in TagManager, Low-tier `globalTextureMipmapLimit` in QualitySettings | Developer (ProjectSettings) |

**PM decision (PR #28):** this pipeline is the **only** model and texture postprocessor owner. The 3D Modeler draft `CrashverseModelPostprocessor` is retired and the yield-to-draft branch is removed; its texture paths and material naming are kept.

## Folder and naming conventions
```
Assets/_Project/Models/<Asset>/<Asset>.fbx                      single file, in-file LODs: <Part>_LOD0/_LOD1/_LOD2
Assets/_Project/Models/<Asset>/<Asset>_LOD0|1|2.fbx             or one FBX per LOD (v3)
Assets/_Project/Models/<Asset>/textures/T_<Asset>_A|_N|_ORM|_E|_Crack.png
Assets/_Project/Models/<Asset>/M_<Asset>[_Emissive|_Inner].mat  created on import (External)
Assets/_Project/Prefabs/Models/<Folder>/<Asset>.prefab          built by menu
```
### Object naming (final, one table)
Sources: models/Model_Spec_v1.md (3D Modeler: rules 3 and 7, colliders), the Physics feel proposal (`FixedJoint` + `ANCHOR_` supports), and the Physics review of #28.

| Name pattern | Meaning | Pipeline does | FBX user properties, stored on `ImportedPhysicsHints` of that object |
|---|---|---|---|
| `<Piece>_frag_##` | fragment of a `Kit_*_Fractured` piece; pivot = centre of mass | tagged `Fragment` if the tag exists; ordinal sort; LOD0 renderer | `mass_kg` → `mass` (kg) |
| `<Part>_LOD0/1/2`, `<Asset>_LOD0/1/2.fbx` | LOD meshes (not used on fractured kit: warned and ignored) | LODGroup | none |
| `UCX_<Piece>_frag_##` | **fragment hull, exact match, frozen**: exactly one per fragment | convex MeshCollider on the fragment, or on a `COL_<Target>` child if the pivot differs | none |
| `UCX_<Name>[_##]` | convex hull for a non-fragment (`_##` is the hull index; ≤8 per piece, ≤255 tris) | convex MeshCollider on `<Name>`. **Missing target: error, skipped** | none |
| `COL_<Target>` | child collider object (created by the pipeline when the hull pivot ≠ target pivot; also accepted when authored) | kept | none |
| `<Target>_COL[_##]` | **legacy alias** of `UCX_<Target>` | as `UCX_` | none |
| `JNT_<FragA>__<FragB>[_##]` | joint / break point between two adjacent fragments (empty at the contact); the optional `_##` handles several joints between one pair | kept; ordinal sort after fragments; validator checks both fragments exist | `break_force_N` → `breakForce` (N), `break_torque_Nm` → `breakTorque` (N·m). Blank `""` = not authored (Physics material defaults apply) |
| module root or `<Piece>_frag_##` of a hero kit | fracture class | written from the stats sidecar (see *Hero buildings*) | `fracture_class` → `fractureClass` (string), overrides the JSON |
| `ANCHOR_<Frag>` | anchor / support mark where a fragment touches the ground (Physics: a piece collapses when all `FixedJoint` + `ANCHOR_` supports are gone) | kept; validator checks the fragment exists | `anchor_to` → `anchorTo` (`"Ground"`, or set at level assembly) |
| `Socket_Muzzle/Core/Module/Booster/WheelDust` (`Muzzle_Spawn` = v1 Muzzle) | gameplay/VFX attach points | kept; all 5 required on the Wrecker | none |
| `Spawn_*`, `*_Hint`, `VFX_*` | helpers | kept | none |
| any object | — | reparented under the named object, keeping its world pose | `attach_to` (string) |
| any object | — | not imported (`density_kg_m3` is informational) | `density_kg_m3` |

- A property that is missing or blank never overwrites an existing hint value. Hints are float SI authoring defaults; Physics owns conversion and use (see *Physics hints*).
- Asset class comes from the file prefix: `Wrecker*` and `Hero_*` are **Hero**; `Island_*` and `Env_*` are **Environment**; `Kit_*` is **Kit**; `Kit_*_Fractured` is **KitFractured**. Textures are classified by folder (`BuildingKit/` = Kit).
- **Kit files must be under `Assets/_Project/Models/BuildingKit/`**; the validator reports any other location as an error.
- No spaces and no Blender `.001` suffixes; the validator reports both as errors.

## 1 · FBX import settings
| Setting | Value |
|---|---|
| Scale | `globalScale 1`, `useFileScale`, `bakeAxisConversion off` (exported with Apply Transform, 1 u = 1 m) |
| Mesh compression | Low for Hero and fragments (to avoid seam cracks), Medium for environment and intact kit |
| Read/Write | off. Colliders are cooked in the editor on the prefab |
| Normals / tangents | Import / **MikkTSpace** (matches the Blender bakes) |
| UV2 lightmap | generated for **static** classes only (Environment, intact Kit): margin 8, hard angle 88 |
| Animation | off, `animationType None`, except files ending `_Rig` or `_Anim` |
| Off | blend shapes, cameras, lights, visibility, auto collider |
| Materials | ImportStandard, External, BasedOnMaterialName, Local search |
| `attach_to` user property | the object is reparented under the named object, keeping its world pose (models README) |
| `mass_kg`, `break_force_N`, `break_torque_Nm`, `anchor_to`, `fracture_class` | stored on `ImportedPhysicsHints` (`mass`, `breakForce`, `breakTorque`, `anchorTo`, `fractureClass`) as **authoring defaults**. A property missing from the FBX never overwrites an existing value. See *Physics hints* below |

## 2 · Materials and textures
- Every `M_*` gets `Crashverse/GlossyPBR` with these maps: `T_<Asset>_A` to `_BaseMap`, `_N` to `_BumpMap`, `_ORM` to `_ORMMap`, `_E` to `_EmissionMap`, and `_Crack` to `_CrackMap` if present. `M_X_Emissive` and `M_X_Inner` read `T_X_*`.
- `M_<Mat>_Inner` **prefers `T_<Mat>_Inner_A/_N/_ORM`**, falling back per map to the main `T_<Mat>_A/_N/_ORM` when an Inner file is missing. The validator errors only when both are missing.
- `_InnerFace` is 1 on `M_<Mat>_Inner` and 0 otherwise. **No keywords** are set. A missing map leaves the shader default (white ORM, flat bump).
- `_EmissionColor` is set to white ×2.5 only on an `_Emissive` material whose emission is still black (Art: Core emissive about 2.5 with bloom).
- If the textures import after the FBX, the folder's materials are rewired automatically.

| Map | Type | sRGB | Max (default) | Android override | ASTC |
|---|---|---|---|---|---|
| `_A` | Default | on | 2048 | Hero/Env 1024, Kit 512, **hero building 1024**, Inner 256 | 6×6 |
| `_N` | **NormalMap** | off | 2048 | same as `_A` | 5×5 |
| `_ORM` | Default | **off** | 2048 | same as `_A` | 6×6 |
| `_E` | Default | on | 1024 | ≤512 | 8×8 |
| `_Crack` | Default | off | 1024 | ≤512 | 8×8 |

**Texture groups (Android max)** are decided by folder or asset prefix, never by the file's map name alone: any `_Inner` map is 256. A **hero building** texture is 1024: asset prefix `T_Bld_*`, any folder under `Models/` named `Bld_*`, or a hero building kit folder `BuildingKit/Kit_Keep|Kit_Tower|Kit_Gate|Kit_House/`. **Shared props (`Kit_Props`) stay at the 512 kit cap** (PM), and a stats sidecar alone never makes a kit a hero building. Every other texture under `BuildingKit/` is kit (512); the rest go by the model folder's class (Hero/Environment 1024). `_E` and `_Crack` stay at ≤512. ASTC blocks are the same for every group. The hero kits name their atlases `T_<B>_A/_N/_ORM` (e.g. `T_House_A`), not `T_Bld_*`, which is why the folder rule exists (PM-approved via 3D Modeler).

Mipmaps and mip streaming are on, Read/Write is off, alpha is none, and aniso is 2 on A/N and 1 elsewhere.
**Tiers:** Unity has one texture import per platform, so High and Mid share the Android override above (spec §4). **Low** gets half resolution through `QualitySettings.globalTextureMipmapLimit = 1` on the Low quality level (Developer-owned; requested). Low also skips N and ORM sampling through the shader's LOD 150 SubShader.

## 3 · Prefabs (menu: Build Prefabs)
**Hard gate (QA review of #28): the builder refuses to build, logs an error and writes no prefab when:**
- the LOD0 triangle total is over budget (Wrecker > 12,000, island > 10,000, kit piece > 1,500), or any fragment has 400 or more triangles (fragments must be < 400);
- or a Wrecker/Hero, island/Environment or **intact** kit asset is missing any of LOD0, LOD1 or LOD2 (per-file or in-file).

**Fractured kit (`Kit_*_Fractured`) is exempt from the LOD rule** (Physics: fragments need no LODs). It builds with LOD0 only and no LODGroup. Any LOD1/LOD2 files or `_LOD1`/`_LOD2` objects get a warning, are ignored, and are left out of the prefab. The 1,500-triangle budget still applies to the fractured LOD0 total.

The validator reports the same conditions as errors (`gate`). Kit pieces under 500 triangles are only a warning.

- There is one prefab per model group, as a plain unlinked copy, so re-run the menu after a model changes. Output goes to `Assets/_Project/Prefabs/Models/<Folder>/<Asset>.prefab`.
- **LODGroup:** per-file LODs become children `LOD1`/`LOD2` with hulls and helpers stripped, while in-file `_LODn` renderers are grouped. There is no cross-fade. Screen-relative heights (LOD0→1, LOD1→2, cull):

| Class | LOD0 | LOD1 | Cull |
|---|---|---|---|
| Hero | 0.30 | 0.12 | 0.02 |
| Environment | 0.45 | 0.18 | 0.005 |
| Kit (intact) | 0.20 | 0.08 | 0.015 |
| Fractured kit | no LODGroup | | |
| Other | 0.25 | 0.10 | 0.02 |

- **Colliders:** each `UCX_`/`_COL` hull becomes a **convex MeshCollider** on its target, or on a `COL_<Target>` child when the pivot differs (**approved by PM**), and the hull renderer object is removed. Fallbacks: a fragment without a hull gets a convex collider from its own mesh, and a model with no collider at all gets one root BoxCollider from the renderer bounds.
- A hull whose target doesn't exist is **logged as an error and skipped**; there is no root fallback.
- **Sockets**, `JNT_*` and the other helpers are preserved with their transforms. Fragments and `JNT_*` empties are **sorted by ordinal name** within their parent (fragments first, then joints, in the sibling slots they already used).
- **Fragments** are flagged only by the `Fragment` tag, if it exists in TagManager; the `_frag_##` name stays the marker in any case. No Rigidbody, joint or destruction component is added.
- Static classes get the static flags ContributeGI, BatchingStatic, OccludeeStatic and ReflectionProbeStatic.

## 4 · Validation report (menu: Validation Report)
Checks per model group:
- Naming: spaces, `.00n`, `M_` prefix, `T_<Asset>_<Map>`, and an unknown class prefix
- LOD set consistency
- Hero sockets (all 5 are required)
- Missing hulls. For **KitFractured**, an error unless each fragment has **exactly one** `UCX_<Piece>_frag_##`, and an error for hulls with no matching fragment
- Fragment pivot far from its mesh bounds centre (warning above max(0.05 m, 10% of the bounds diagonal))
- Kit files outside `BuildingKit/`
- Missing A/N/ORM maps (errors; warnings for `_Inner`), `_E` for `_Emissive`, and `_InnerFace`
- Texture import: NormalMap type, sRGB, and the Android ASTC override
- Triangle budgets (table below)
- Texel density hint

| Budget (LOD0 triangles, PM decision) | Value | Validator |
|---|---|---|
| Hero (Wrecker) | **≤ 12,000** total | error above, prefab refused |
| Environment (island) | **≤ 10,000** total | error above, prefab refused |
| Kit piece (intact, and fractured total) | **500–1,500** | error above 1,500 (prefab refused), warning below 500 |
| Fragment | **< 400** per fragment (Model_Spec_v1 v3) | error at 400 or more, prefab refused |
| Other | ≤ 3,000 | error above |

Texel density hint = sqrt(UV area × albedo w × h / world area) over the LOD0 meshes. Targets: Hero 180–360 px/m (≈256), Kit 128–256, Environment 16–256 (macro map). The check warns below 0.75× min or above 1.5× max.

## Hero buildings (fracture class sidecar)
Hero building kits (`Assets/_Project/Models/BuildingKit/Kit_<B>/Kit_<B>_<Module>[_Fractured][_LODn].fbx`, 3D Modeler hero buildings v1) keep their per-module numbers in **`models/HeroBuildings/Kit_<B>_stats.json`** (repo root, outside Assets). Schema as shipped by 3D Modeler:
```json
{ "building": "House", "texel_px_per_m": 77.1, "atlas": 2048,
  "modules": { "GroundDoor": { "frac": "Stone", "LOD0": 640, "LOD1": 319, "LOD2": 126, "ucx": 8, "aabb": [[..],[..]],
                               "anchor": "bottom", "anchor_to": "Ground", "note": "...", "frags": 7, "frag_max": 213,
                               "Fractured": 1057, "mass": "686.8-1774.3", "joints": 10, "anchors": 4, "trims_dropped": 0 }, ... } }
```
- The importer uses only `modules.<Module>.frac` (`fracture_class` is accepted as an alias key). Current values: `Stone`, `Wood`, `Steel`, `Roof`. `modules` is a keyed object, so it is read with a small built-in JSON reader (`ImportConventions.MiniJson`; no package, JsonUtility can't read keyed objects).
- Kits that read a sidecar: the hero building kits (Keep, Tower, Gate, House), the shared `Kit_Props`, and any other `BuildingKit/Kit_<B>` with a sidecar. (Only the four hero building kits get the 1024 texture cap.)
- The module is taken from the **folder** `Kit_<B>` and the file name `Kit_<B>_<Module>` (minus `_Fractured` and `_LODn`).
- On import (`OnPostprocessModel`), `fractureClass` is written to `ImportedPhysicsHints` on the module root and on every LOD0 `<Piece>_frag_##`. **Precedence per object:** its own FBX `fracture_class` > the root's FBX `fracture_class` > the JSON entry.
- **Missing sidecar, missing module entry, or an entry without `frac`:** nothing is written or cleared (no component is added), and the validator warns.
- Each sidecar is an AssetDatabase custom dependency (`crashverse/hero-stats/Kit_<B>`, content hash) of that kit's FBX files, so they reimport when it changes; menu **Crashverse/Import/Reimport Hero Building Kits** forces it.
- **Validator (warnings only):** no readable sidecar; module not in the JSON or without `frac`; root/fragment without `fractureClass`; an FBX `fracture_class` that differs from the JSON; and the cross-check that every module in the JSON has a `Kit_<B>_<Module>[_Fractured].fbx` and every hero kit FBX module has a JSON entry.
- Hero kits are still class `Kit`/`KitFractured` for budgets, LODs and colliders. Hero building kit textures get the 1024 Android cap (section 2); `Kit_Props` keeps 512.

## Physics hints (`ImportedPhysicsHints`)
- Values are **float SI units**: `mass` in kg, `breakForce` in N, `breakTorque` in N·m, plus `anchorTo` as a name and `fractureClass` as a string (hero buildings). They are authoring defaults only; Physics maps `fractureClass` onto its materials.
- **Physics** converts them to its integer units and maps them onto `MaterialDefinition`; the pipeline adds no physics logic.
- Steel `mass_kg` uses an **effective density of 800 kg/m³** (hollow sections), not solid steel.

## Tests
- **Unity EditMode** (`Assets/_Project/Editor/Import/Tests`, asmdef `Crashverse.Import.Tests`, Editor-only, `UNITY_INCLUDE_TESTS`, same layout as `Crashverse.Rules.Tests`):
  - **Naming:** hull targets, fragment, joint, socket and kit-path rules.
  - **Hints:** `ImportedPhysicsHints` (SI values, blank and missing never overwrite).
  - **Prefab build:** builds a procedural fractured piece at test time under `BuildingKit/__ImportTest`, then deletes it. It checks one convex collider per fragment, the `COL_` child, no root fallback, the error on a missing target, the LODGroup, kept sockets, joints and anchors, ordinal order, and that no physics components are added. **Gate:** over-budget kit and Wrecker are rejected with no prefab, a kit at exactly 1,500 builds, and a missing LOD on intact kit, island or Wrecker is rejected. A fractured kit builds with LOD0 only and no LODGroup, ignores LOD objects with a warning, and is rejected when its total is over 1,500.
  - **Hero buildings:** Android cap (`T_Bld_X_A` → 1024, Kit_Keep/Tower/Gate/House folders → 1024, `Kit_Props` and sidecar-only kits → 512, `_Inner` 256, kit still 512, file name alone doesn't make a kit texture hero), ASTC formats unchanged, a real `T_Bld_*` texture import at 1024 next to a kit one at 512, module resolution, the sidecar schema, JSON → root + fragments, FBX `fracture_class` override (root and fragment), missing JSON/entry never overwrites, and the module cross-check.
  - **Material wiring:** checks GlossyPBR maps, `_InnerFace`, no keywords, and texture import (NormalMap, sRGB, Android ASTC and size).
- Assemblies: `Crashverse.Import` (runtime marker) and `Crashverse.Import.Editor` (Editor-only pipeline).

## Compile check
`cd vfx/compile_check/import && dotnet build` type-checks the pipeline against API-shaped UnityEngine/UnityEditor stubs. It is **not** a Unity compile; the first real Unity import is still pending (licence).

`cd vfx/compile_check/import_tests && dotnet run` runs the stub test harness on the naming rules (including `UCX_Wall_frag_03` mapping to `Wall_frag_03`), the texture caps and the hero sidecar parser (it also parses any real `models/HeroBuildings/*.json` present). It exits non-zero on failure.
