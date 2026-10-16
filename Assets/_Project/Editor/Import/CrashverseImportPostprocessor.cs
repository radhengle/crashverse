#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using C = Crashverse.EditorTools.Import.ImportConventions;

namespace Crashverse.EditorTools.Import
{
    /// <summary>
    /// The ONE model/texture AssetPostprocessor for Assets/_Project/Models/** (VFX & Tech Art).
    /// Concerns owned here: FBX import settings, texture import + Android ASTC overrides, material creation/wiring to
    /// Crashverse/GlossyPBR, attach_to reparenting. Prefabs are built by the menu tool (CrashversePrefabBuilder), not on import.
    ///
    /// Compatibility with the 3D Modeler draft (Crashverse.EditorTools.CrashverseModelPostprocessor, models-v3, uncommitted):
    /// same texture paths (&lt;ModelDir&gt;/textures/T_&lt;Asset&gt;_*), same material naming, same External material location.
    /// While that class is present in the project, the duplicated concerns (texture type/sRGB, material wiring) are skipped
    /// here and a warning is logged once, so the two never fight. See vfx/IMPORT_PIPELINE.md "Ownership".
    /// </summary>
    public class CrashverseImportPostprocessor : AssetPostprocessor
    {
        // Run after any generic project postprocessors so these settings win.
        public override int GetPostprocessOrder() => 100;
        public override uint GetVersion() => 1;

        static bool? _draft;
        internal static bool ModelerDraftPresent
        {
            get
            {
                if (_draft == null)
                {
                    _draft = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("Crashverse.EditorTools.CrashverseModelPostprocessor", false) != null);
                    if (_draft.Value) Debug.LogWarning("[CrashverseImport] 3D Modeler postprocessor draft found: material wiring + texture type are left to it. Remove the draft once vfx/import-pipeline merges (one owner per concern).");
                }
                return _draft.Value;
            }
        }

        // ---------------- FBX ----------------
        void OnPreprocessModel()
        {
            if (!C.InScope(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var cls = C.Classify(assetPath);

            // Scale: FBX exported with Apply Transform, 1 unit = 1 m (models README).
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;

            // Mesh
            mi.meshCompression = cls == C.AssetClass.Hero || cls == C.AssetClass.KitFractured ? ModelImporterMeshCompression.Low : ModelImporterMeshCompression.Medium;
            mi.isReadable = false;                       // colliders are cooked in-editor on the prefab; runtime never reads vertices
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.addCollider = false;                      // colliders come from UCX_/_COL meshes in the prefab builder
            mi.importNormals = ModelImporterNormals.Import;  // baked-normal workflow: keep authored normals
            mi.importTangents = ModelImporterTangents.CalculateMikk; // must match the Blender bake (MikkTSpace)

            // UV2 lightmap UVs for static assets only
            bool isStatic = C.IsStatic(cls);
            mi.generateSecondaryUV = isStatic;
            if (isStatic) { mi.secondaryUVPackMargin = 8f; mi.secondaryUVHardAngle = 88f; }

            // Animation off for props
            if (!C.WantsAnimation(assetPath))
            {
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
            }

            // Materials: External M_*.mat beside the FBX, reused across LOD files (same as the 3D Modeler draft).
            if (!ModelerDraftPresent)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.External;
                mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                mi.materialSearch = ModelImporterMaterialSearch.Local;
            }
        }

        Material OnAssignMaterialModel(Material source, Renderer renderer)
        {
            if (!C.InScope(assetPath) || ModelerDraftPresent) return null;
            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            return MaterialWiring.CreateOrWire(source.name, dir, source.shader);
        }

        /// <summary>models README: objects with attach_to=&lt;Sibling&gt; are reparented keeping their world pose.</summary>
        void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
        {
            if (!C.InScope(assetPath)) return;
            int i = Array.IndexOf(names, "attach_to");
            if (i < 0 || !(values[i] is string target) || string.IsNullOrEmpty(target)) return;
            var root = go.transform.root;
            var parent = FindDeep(root, target);
            if (parent != null && parent != go.transform) go.transform.SetParent(parent, true);
            else Debug.LogWarning($"[CrashverseImport] {assetPath}: attach_to '{target}' not found for {go.name}");
        }

        internal static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        // ---------------- Textures ----------------
        void OnPreprocessTexture()
        {
            if (!C.InScope(assetPath)) return;
            string stem = C.FileStem(assetPath);
            if (C.MapSuffix(stem) == null) return;            // not a T_<Asset>_<Map>: leave alone (palettes, previews)
            var ti = (TextureImporter)assetImporter;
            var p = C.PolicyFor(stem, assetPath);

            if (!ModelerDraftPresent)
            {
                ti.textureType = p.normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = p.sRGB;
            }
            ti.mipmapEnabled = true;
            ti.streamingMipmaps = true;
            ti.isReadable = false;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.anisoLevel = p.normal || stem.EndsWith("_A") ? 2 : 1;
            ti.maxTextureSize = p.maxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;

            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = p.androidMax;
            android.format = Astc(p.astc);
            android.compressionQuality = 50;
            ti.SetPlatformTextureSettings(android);
        }

        static TextureImporterFormat Astc(string block)
        {
            switch (block)
            {
                case "4x4": return TextureImporterFormat.ASTC_4x4;
                case "5x5": return TextureImporterFormat.ASTC_5x5;
                case "8x8": return TextureImporterFormat.ASTC_8x8;
                default: return TextureImporterFormat.ASTC_6x6;
            }
        }

        /// <summary>Textures often land after the FBX; re-wire existing materials in the same model folder.</summary>
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (ModelerDraftPresent) return;
            var dirs = imported.Where(p => C.InScope(p) && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && C.MapSuffix(C.FileStem(p)) != null)
                               .Select(p => Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/')).Distinct().ToArray();
            foreach (var d in dirs) MaterialWiring.RewireFolder(d);
        }
    }
}
#endif
