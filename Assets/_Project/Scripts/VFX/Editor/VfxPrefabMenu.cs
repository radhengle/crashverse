#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Crashverse.VFX.EditorTools
{
    /// <summary>
    /// Crashverse/VFX/Build Effect Prefabs: builds every VfxRecipe with VfxParticleBuilder and saves VFX_&lt;Id&gt;.prefab plus its shared
    /// particle materials under Assets/_Project/VFX/{Prefabs,Materials}. Re-run after editing VfxRecipes (prefabs are generated output;
    /// the runtime VfxDirector does not need them, it builds the same systems from code).
    /// </summary>
    public static class VfxPrefabMenu
    {
        const string Root = "Assets/_Project/VFX/", Tex = Root + "Textures/";

        public static VfxAssets LoadAssets() => new VfxAssets
        {
            particleShader = Shader.Find("Crashverse/VfxParticle"),
            puffFlipbook = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "T_VFX_PuffFlipbook_4x4.png"),
            sparkStreak = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "T_VFX_SparkStreak.png"),
            ring = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "T_VFX_Ring.png"),
            debrisAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "T_VFX_DebrisAtlas_4x2.png"),
            muzzleFlash = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "T_VFX_MuzzleFlash_Core.png"),
        };

        [MenuItem("Crashverse/VFX/Build Effect Prefabs")]
        public static void BuildAll()
        {
            var assets = LoadAssets();
            if (assets.particleShader == null) { Debug.LogError("Crashverse/VfxParticle shader not found"); return; }
            Directory.CreateDirectory(Root + "Prefabs"); Directory.CreateDirectory(Root + "Materials");
            VfxParticleBuilder.ClearMaterialCache();
            foreach (var id in VfxIds.All)
            {
                var systems = new System.Collections.Generic.List<ParticleSystem>();
                var go = VfxParticleBuilder.Build(VfxRecipes.Get(id), assets, 1, false, null, systems);
                foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>())
                {
                    var m = r.sharedMaterial; if (m == null) continue;
                    string mp = Root + "Materials/" + m.name + ".mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(mp);
                    if (existing == null) { AssetDatabase.CreateAsset(m, mp); existing = m; }
                    else if (existing != m) { existing.CopyPropertiesFromMaterial(m); }
                    r.sharedMaterial = existing;
                }
                PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/VFX_" + id + ".prefab");
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"VFX: built {VfxIds.All.Length} effect prefabs");
        }

        [MenuItem("Crashverse/VFX/Add VfxDirector To Scene")]
        public static void AddDirector()
        {
            var go = new GameObject("VfxDirector");
            go.AddComponent<VfxDirector>().assets = LoadAssets();
            Undo.RegisterCreatedObjectUndo(go, "Add VfxDirector");
            Selection.activeGameObject = go;
        }
    }

    /// <summary>Import rules for VFX-authored textures (README in Assets/_Project/VFX/Textures).</summary>
    public class VfxTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/VFX/")) return;
            var ti = (TextureImporter)assetImporter;
            string f = Path.GetFileNameWithoutExtension(assetPath);
            if (f == "T_SkyFallback_Cube")
            {
                ti.textureShape = TextureImporterShape.TextureCube; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.maxTextureSize = 128;
                var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
                s.cubemapConvolution = TextureImporterCubemapConvolution.Specular; s.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap; s.seamlessCubemap = true;
                ti.SetTextureSettings(s); return;
            }
            if (!assetPath.StartsWith("Assets/_Project/VFX/Textures/")) return;
            ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear;
            if (f.StartsWith("T_CrackMask")) { ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.None; ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 512; }
            else ti.maxTextureSize = f.Contains("Flipbook") ? 512 : 256;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = ti.maxTextureSize,
                format = f.StartsWith("T_CrackMask") ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_5x5
            });
        }
    }
}
#endif
