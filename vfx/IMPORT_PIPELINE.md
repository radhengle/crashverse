# CRASHVERSE model import pipeline (VFX & Tech Art)

Editor-only code in `Assets/_Project/Editor/Import/`. Scope is `Assets/_Project/Models/**` only. Target is Unity 2022.3 with URP 14 on Android.
Stacked on `vfx/v1-foundation` (PR #2), which holds the `Crashverse/GlossyPBR` shader.

| File | Role |
|---|---|
| `CrashverseImportConventions.cs` | Pure-C# rules for naming, asset classes, LOD heights, texture policy, budgets and texel targets |
| `CrashverseImportPostprocessor.cs` | **The only** model and texture `AssetPostprocessor`: FBX settings, texture import with Android ASTC, material hookup, `attach_to` |
| `MaterialWiring.cs` | Creates and wires `M_*` materials to GlossyPBR. Menu: **Crashverse ▸ Import ▸ Rewire Materials** |
| `CrashversePrefabBuilder.cs` | Menus: **Build Prefabs (all / selected)** |
| `CrashverseImportValidator.cs` | Menu: **Validation Report**. Writes `Logs/CrashverseImportReport.md` and never modifies assets |

## Ownership (one owner per concern)
| Concern | Owner |
|---|---|
| FBX export (axes, Apply Transform, scale 1, naming, hulls, sockets, LOD meshes, texture bakes) | 3D Modeler (Blender scripts) |
| FBX import settings, texture import and ASTC, material creation and wiring, prefabs, LODGroup, colliders, validation | VFX (this pipeline) |
| Rigidbody, joints from `JNT_*`, mass and break forces, destruction components on fragments | Physics |
| `Fragment` tag in TagManager, Low-tier `globalTextureMipmapLimit` in QualitySettings | Developer (ProjectSettings) |

The 3D Modeler has a draft, `Assets/_Project/Editor/CrashverseModelPostprocessor.cs`, in its uncommitted models-v3 worktree. It covers texture type and sRGB, material wiring, and some FBX flags. This pipeline uses **the same rules** (paths, naming, External material location, `_InnerFace`, optional `_CrackMap`). While that class exists in the project, this pipeline **skips** texture type/sRGB and material wiring and logs one warning, so the two never fight. The proposal is to drop the draft once this PR merges, so that one postprocessor owns the whole concern.

## Folder and naming conventions
```
Assets/_Project/Models/<Asset>/<Asset>.fbx                      single file, in-file LODs: <Part>_LOD0/_LOD1/_LOD2
Assets/_Project/Models/<Asset>/<Asset>_LOD0|1|2.fbx             or one FBX per LOD (v3)
Assets/_Project/Models/<Asset>/textures/T_<Asset>_A|_N|_ORM|_E|_Crack.png
Assets/_Project/Models/<Asset>/M_<Asset>[_Emissive|_Inner].mat  created on import (External)
Assets/_Project/Prefabs/Models/<Folder>/<Asset>.prefab          built by menu
```
- Asset class comes from the file prefix: `Wrecker*` and `Hero_*` are **Hero**; `Island_*` and `Env_*` are **Environment**; `Kit_*` is **Kit**; `Kit_*_Fractured` is **KitFractured**.
- Colliders: `UCX_<Target>[_##]` (models README) or `<Target>_COL[_##]`.
- Sockets: `Socket_Muzzle|Core|Module|Booster|WheelDust` (v3). The v1 name `Muzzle_Spawn` is read as Muzzle.
- Fragments are `<Piece>_frag_##`, and joints are `JNT_<A>__<B>`. Helpers (`Spawn_*`, `ANCHOR*`, `*_Hint`, `VFX_*`) are kept untouched.
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

## 2 · Materials and textures
- Every `M_*` gets `Crashverse/GlossyPBR` with these maps: `T_<Asset>_A` to `_BaseMap`, `_N` to `_BumpMap`, `_ORM` to `_ORMMap`, `_E` to `_EmissionMap`, and `_Crack` to `_CrackMap` if present. `M_X_Emissive` and `M_X_Inner` read `T_X_*`.
- `_InnerFace` is 1 on `M_<Mat>_Inner` and 0 otherwise. **No keywords** are set. A missing map leaves the shader default (white ORM, flat bump).
- `_EmissionColor` is set to white ×2.5 only on an `_Emissive` material whose emission is still black (Art: Core emissive about 2.5 with bloom).
- If the textures import after the FBX, the folder's materials are rewired automatically.

| Map | Type | sRGB | Max (default) | Android override | ASTC |
|---|---|---|---|---|---|
| `_A` | Default | on | 2048 | Hero/Env 1024, Kit 512, Inner 256 | 6×6 |
| `_N` | **NormalMap** | off | 2048 | same as `_A` | 5×5 |
| `_ORM` | Default | **off** | 2048 | same as `_A` | 6×6 |
| `_E` | Default | on | 1024 | ≤512 | 8×8 |
| `_Crack` | Default | off | 1024 | ≤512 | 8×8 |

Mipmaps and mip streaming are on, Read/Write is off, alpha is none, and aniso is 2 on A/N and 1 elsewhere.
**Tiers:** Unity has one texture import per platform, so High and Mid share the Android override above (spec §4). **Low** gets half resolution through `QualitySettings.globalTextureMipmapLimit = 1` on the Low quality level (Developer-owned; requested). Low also skips N and ORM sampling through the shader's LOD 150 SubShader.

## 3 · Prefabs (menu: Build Prefabs)
- There is one prefab per model group, as a plain unlinked copy, so re-run the menu after a model changes. Output goes to `Assets/_Project/Prefabs/Models/<Folder>/<Asset>.prefab`.
- **LODGroup:** per-file LODs become children `LOD1`/`LOD2` with hulls and helpers stripped, while in-file `_LODn` renderers are grouped. There is no cross-fade. Screen-relative heights (LOD0→1, LOD1→2, cull):

| Class | LOD0 | LOD1 | Cull |
|---|---|---|---|
| Hero | 0.30 | 0.12 | 0.02 |
| Environment | 0.45 | 0.18 | 0.005 |
| Kit / fractured | 0.20 | 0.08 | 0.015 |
| Other | 0.25 | 0.10 | 0.02 |

- **Colliders:** each `UCX_`/`_COL` hull becomes a **convex MeshCollider** on its target, or on a `COL_<Target>` child when the pivot differs, and the hull renderer object is removed. Fallbacks: a fragment without a hull gets a convex collider from its own mesh, and a model with no collider at all gets one root BoxCollider from the renderer bounds.
- **Sockets**, `JNT_*` and the other helpers are preserved with their transforms.
- **Fragments** are flagged only by the `Fragment` tag, if it exists in TagManager; the `_frag_##` name stays the marker in any case. No Rigidbody, joint or destruction component is added.
- Static classes get the static flags ContributeGI, BatchingStatic, OccludeeStatic and ReflectionProbeStatic.

## 4 · Validation report (menu: Validation Report)
Checks per model group:
- Naming: spaces, `.00n`, `M_` prefix, `T_<Asset>_<Map>`, and an unknown class prefix
- LOD set consistency
- Hero sockets (all 5 are required)
- Missing hulls
- Missing A/N/ORM maps (errors; warnings for `_Inner`), `_E` for `_Emissive`, and `_InnerFace`
- Texture import: NormalMap type, sRGB, and the Android ASTC override
- Triangle budgets
- Texel density hint

| Budget (LOD0) | Value | Source |
|---|---|---|
| Hero (Wrecker) | 12,000 total | v3 LP is about 11k. **v1 README said ≤3,000: PM to confirm** |
| Environment (island) | 10,000 total | v1 said 2,500 before the v3 unique macro map: **confirm** |
| Kit intact | 300 per mesh | models README |
| Fragment | 300 per fragment | models README |

Texel density hint = sqrt(UV area × albedo w × h / world area) over the LOD0 meshes. Targets: Hero 180–360 px/m (≈256), Kit 128–256, Environment 16–256 (macro map). The check warns below 0.75× min or above 1.5× max.

## Compile check
`cd vfx/compile_check/import && dotnet build` type-checks the pipeline against API-shaped UnityEngine/UnityEditor stubs. It is **not** a Unity compile; the first real Unity import is still pending (licence).
