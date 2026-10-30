#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using C = Crashverse.EditorTools.Import.ImportConventions;

namespace Crashverse.EditorTools.Import
{
    /// <summary>Creates/assigns Crashverse/GlossyPBR materials and wires T_&lt;Asset&gt;_* maps. GlossyPBR has no keywords: only properties are set.</summary>
    public static class MaterialWiring
    {
        public static Material CreateOrWire(string matName, string modelDir, Shader fallback)
        {
            string matPath = $"{modelDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find(C.ShaderName);
            if (shader == null) Debug.LogError($"[CrashverseImport] Shader {C.ShaderName} not found (needs vfx/v1-foundation). Using {fallback?.name}.");
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : fallback) { name = matName };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (shader != null && mat.shader != shader) mat.shader = shader;
            Wire(mat, modelDir);
            // Rendering (RealisticPbrTests): foliage materials need GPU instancing as the fallback when SRP batching breaks
            if (matPath.Replace('\\', '/').Contains("/Foliage/")) mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void Wire(Material mat, string modelDir)
        {
            string asset = C.TextureAssetForMaterial(mat.name, out bool inner, out bool emissive);
            string tex = $"{modelDir}/textures/T_{asset}";
            // M_<Mat>_Inner prefers T_<Mat>_Inner_A/_N/_ORM and falls back per map to the main T_<Mat>_* set.
            string innerTex = tex + "_Inner";
            SetPref(mat, "_BaseMap", inner, innerTex + "_A", tex + "_A");
            SetPref(mat, "_BumpMap", inner, innerTex + "_N", tex + "_N");
            SetPref(mat, "_ORMMap", inner, innerTex + "_ORM", tex + "_ORM");
            if (Set(mat, "_EmissionMap", tex + "_E") && emissive && mat.HasProperty("_EmissionColor") && mat.GetColor("_EmissionColor").maxColorComponent <= 0f)
                mat.SetColor("_EmissionColor", Color.white * 2.5f);   // Art: Core emissive ~2.5 with bloom
            Set(mat, "_CrackMap", tex + "_Crack");
            if (mat.HasProperty("_InnerFace")) mat.SetFloat("_InnerFace", inner ? 1f : 0f);
        }

        static bool SetPref(Material m, string prop, bool inner, string innerPath, string mainPath) =>
            (inner && Set(m, prop, innerPath)) || Set(m, prop, mainPath);

        static bool Set(Material m, string prop, string pathNoExt)
        {
            if (!m.HasProperty(prop)) return false;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(pathNoExt + ".png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>(pathNoExt + ".tga");
            if (t == null) return false;                 // missing map -> keep shader default (white ORM, flat bump)
            m.SetTexture(prop, t);
            return true;
        }

        public static int RewireFolder(string modelDir)
        {
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { modelDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetDirectoryName(p).Replace('\\', '/') != modelDir) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null || !m.name.StartsWith("M_")) continue;
                if (m.shader == null || m.shader.name != C.ShaderName) { var s = Shader.Find(C.ShaderName); if (s != null) m.shader = s; }
                Wire(m, modelDir); EditorUtility.SetDirty(m); n++;
            }
            if (n > 0) AssetDatabase.SaveAssets();
            return n;
        }

        [MenuItem("Crashverse/Import/Rewire Materials (selected or all models)")]
        static void RewireMenu()
        {
            int n = 0;
            foreach (var d in CrashversePrefabBuilder.ModelDirs()) n += RewireFolder(d);
            Debug.Log($"[CrashverseImport] Rewired {n} materials to {C.ShaderName}.");
        }
    }
}
#endif
