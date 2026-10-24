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
    /// PM decision: this is the only postprocessor owner for these concerns (the 3D Modeler draft
    /// CrashverseModelPostprocessor is retired; its texture paths and material naming are kept). See vfx/IMPORT_PIPELINE.md.
    /// </summary>
    public class CrashverseImportPostprocessor : AssetPostprocessor
    {
        // Run after any generic project postprocessors so these settings win.
        public override int GetPostprocessOrder() => 100;
        public override uint GetVersion() => 3;   // 3: hero building fractureClass + T_Bld/hero kit 1024 Android cap

        // ---------------- FBX ----------------
        void OnPreprocessModel()
        {
            if (!C.InScope(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var cls = C.Classify(assetPath);
            // Hero building modules read models/HeroBuildings/Kit_<B>_stats.json (outside Assets): reimport when its hash changes.
            if (C.TryHeroModule(assetPath, out var heroKit, out _, out _)) context.DependsOnCustomDependency(C.HeroStatsDependency(heroKit));

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
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.External;
                mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                mi.materialSearch = ModelImporterMaterialSearch.Local;
            }
        }

        /// <summary>Runs after OnPostprocessGameObjectWithUserProperties, so any FBX fracture_class is already on the hints.</summary>
        void OnPostprocessModel(GameObject root)
        {
            if (!C.InScope(assetPath) || !C.TryHeroModule(assetPath, out var kit, out var module, out _)) return;
            var stats = HeroStatsCache.Load(kit, out _);
            ApplyFractureClass(root, stats?.ClassFor(module), assetPath);
        }

        /// <summary>Testable core of the fracture-class step. Precedence per object: its own FBX fracture_class, then the module value
        /// (the root's FBX fracture_class, else <paramref name="jsonClass"/> from the stats sidecar). Writes the module root and every
        /// LOD0 fragment. With no value at all (missing JSON or entry) nothing is written or cleared; the validator warns instead.
        /// Returns the number of objects written.</summary>
        public static int ApplyFractureClass(GameObject root, string jsonClass, string assetPath)
        {
            var rh = root.GetComponent<Crashverse.Import.ImportedPhysicsHints>();
            string moduleClass = rh != null && !string.IsNullOrEmpty(rh.fractureClass) ? rh.fractureClass : jsonClass;
            if (string.IsNullOrEmpty(moduleClass)) return 0;
            int n = 0;
            if (rh == null) rh = root.AddComponent<Crashverse.Import.ImportedPhysicsHints>();
            if (string.IsNullOrEmpty(rh.fractureClass)) { rh.fractureClass = moduleClass; n++; }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform || !C.IsFragment(t.name) || C.LodLevel(t.name, out _) > 0) continue;
                var h = t.GetComponent<Crashverse.Import.ImportedPhysicsHints>();
                if (h != null && !string.IsNullOrEmpty(h.fractureClass)) continue;   // FBX fracture_class on the fragment wins
                if (h == null) h = t.gameObject.AddComponent<Crashverse.Import.ImportedPhysicsHints>();
                h.fractureClass = moduleClass; n++;
            }
            return n;
        }

        Material OnAssignMaterialModel(Material source, Renderer renderer)
        {
            if (!C.InScope(assetPath)) return null;
            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            return MaterialWiring.CreateOrWire(source.name, dir, source.shader);
        }

        /// <summary>FBX user properties. attach_to=&lt;Object&gt; reparents keeping world pose (models README).
        /// mass_kg, break_force_N, break_torque_Nm, anchor_to, fracture_class are stored as SI authoring defaults on ImportedPhysicsHints
        /// (data only; Physics owns conversion and use). A property that is missing never overwrites an existing value.</summary>
        void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
        {
            if (!C.InScope(assetPath)) return;
            ApplyUserProperties(go, names, values, assetPath);
        }

        /// <summary>Testable core of the user-property step (EditMode tests call it directly).</summary>
        public static void ApplyUserProperties(GameObject go, string[] names, object[] values, string assetPath)
        {
            float? mass = Num(names, values, "mass_kg"), brk = Num(names, values, "break_force_N"), tq = Num(names, values, "break_torque_Nm");
            string anchor = Str(names, values, "anchor_to"), frac = Str(names, values, "fracture_class");
            if (mass.HasValue || brk.HasValue || tq.HasValue || anchor != null || frac != null)
            {
                var h = go.GetComponent<Crashverse.Import.ImportedPhysicsHints>();
                if (h == null) h = go.AddComponent<Crashverse.Import.ImportedPhysicsHints>();
                if (mass.HasValue) h.mass = mass.Value;
                if (brk.HasValue) h.breakForce = brk.Value;
                if (tq.HasValue) h.breakTorque = tq.Value;
                if (anchor != null) h.anchorTo = anchor;
                if (frac != null) h.fractureClass = frac;      // overrides the hero stats JSON (ApplyFractureClass keeps it)
            }
            string target = Str(names, values, "attach_to");
            if (string.IsNullOrEmpty(target)) return;
            var parent = FindDeep(go.transform.root, target);
            if (parent != null && parent != go.transform) go.transform.SetParent(parent, true);
            else Debug.LogWarning($"[CrashverseImport] {assetPath}: attach_to '{target}' not found for {go.name}");
        }

        internal static float? Num(string[] names, object[] values, string key)
        {
            int i = Array.IndexOf(names, key);
            if (i < 0 || values[i] == null) return null;
            switch (values[i])
            {
                case float f: return f;
                case double d: return (float)d;
                case int n: return n;
                case long l: return l;
                case string str when float.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v): return v;
                default: return null;
            }
        }

        internal static string Str(string[] names, object[] values, string key)
        {
            int i = Array.IndexOf(names, key);
            if (i < 0 || values[i] == null) return null;
            string v = values[i].ToString();
            return v.Length == 0 ? null : v;                 // blank ("") = not authored, never overwrites
        }

        public static Transform FindDeep(Transform t, string name)
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
            var dirs = imported.Where(p => C.InScope(p) && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && C.MapSuffix(C.FileStem(p)) != null)
                               .Select(p => Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/')).Distinct().ToArray();
            foreach (var d in dirs) MaterialWiring.RewireFolder(d);
        }
    }
}
#endif
