using UnityEngine;

namespace Crashverse.Rendering
{
    /// <summary>Single source of truth for the per-tier mobile look and budgets (bright glossy + realistic PBR).
    /// UrpSetup/LightingSetup (editor) apply it to URP_{Low,Mid,High} and the Low/Mid/High QualitySettings levels;
    /// RealisticTextureImport applies the texture rules; tests assert the generated assets match. Change values here, not in the assets.
    /// Frame rate, vSync, fixed step and debris caps are NOT here: Perf/QualityController owns them (locked for ranked determinism).</summary>
    public struct RenderTier
    {
        public string name;
        public bool hdr; public int msaa; public float renderScale;
        public bool mainShadows; public int shadowResolution; public float shadowDistance; public int cascades; public float cascade2Split; public bool softShadows;
        public int additionalLights; // 0 = disabled, else per-pixel with this per-object limit
        public bool srpBatcher;
        public bool bloom; public bool vignette;

        // Realistic PBR budgets (3D Modeler to confirm). Effective size on device = import cap (Android) >> textureMipLimit.
        public int textureMipLimit;   // QualitySettings.globalTextureMipmapLimit: 0 = full, 1 = half
        public int albedoMax, normalMax, ormMax; // effective max px for _A / _N / _ORM on this tier (asserted == AndroidImportMax >> textureMipLimit)
        public AnisotropicFiltering anisotropic;
        // Foliage / LOD
        public float lodBias; public int maximumLODLevel;
        public bool foliageShadows;   // Low: off (Low has no main-light shadows at all, so foliage never casts there)
    }

    /// <summary>Per-map import policy for Assets/_Project/Models/Realistic/** (pure data so EditMode tests can check it).</summary>
    public struct PbrTexturePolicy
    {
        public string map;          // "_A", "_N", "_ORM"
        public bool sRGB, normalMap, keepAlpha;
        public int maxSize;         // default platform (editor/standalone): source size
        public int androidMax;      // Android override (PM cap 1024)
        public string astc;         // Android ASTC block, e.g. "6x6"
    }

    /// <summary>Foliage triangle budgets from 3D Modeler (trees; bushes get half). Final LOD must be an impostor/billboard.</summary>
    public struct FoliageBudget
    {
        public int[] lodTris;       // max tris for LOD0, LOD1, LOD2 (a LOD index past the end uses the last entry)
        public int impostorMinTris, impostorMaxTris;
    }

    public static class RenderTiers
    {
        // Mid is the 60 fps reference tier (Nothing Phone 3a, Android 16); Physics owns the on-device frame-time check.
        public static readonly RenderTier[] All =
        {
            new RenderTier { name = "Low",  hdr = false, msaa = 1, renderScale = 0.75f, mainShadows = false, shadowResolution = 512,  shadowDistance = 15f, cascades = 1, cascade2Split = 0.25f, softShadows = false, additionalLights = 0, srpBatcher = true, bloom = false, vignette = false,
                             textureMipLimit = 1, albedoMax = 512,  normalMax = 512,  ormMax = 512,  anisotropic = AnisotropicFiltering.Disable, lodBias = 0.7f, maximumLODLevel = 1, foliageShadows = false },
            new RenderTier { name = "Mid",  hdr = true,  msaa = 2, renderScale = 0.9f,  mainShadows = true,  shadowResolution = 1024, shadowDistance = 30f, cascades = 1, cascade2Split = 0.25f, softShadows = false, additionalLights = 2, srpBatcher = true, bloom = true,  vignette = true,
                             textureMipLimit = 0, albedoMax = 1024, normalMax = 1024, ormMax = 1024, anisotropic = AnisotropicFiltering.Enable,  lodBias = 1.0f, maximumLODLevel = 0, foliageShadows = true },
            // High albedo stays 1024: PM capped Android textures at 1024. High 2048 albedo would need PM approval (raise AndroidImportMax
            // for _A only and keep High at mip limit 0; Mid/Low would then need limit 1/2 to stay on budget).
            new RenderTier { name = "High", hdr = true,  msaa = 4, renderScale = 1.0f,  mainShadows = true,  shadowResolution = 2048, shadowDistance = 50f, cascades = 2, cascade2Split = 0.25f, softShadows = true,  additionalLights = 4, srpBatcher = true, bloom = true,  vignette = true,
                             textureMipLimit = 0, albedoMax = 1024, normalMax = 1024, ormMax = 1024, anisotropic = AnisotropicFiltering.Enable,  lodBias = 1.5f, maximumLODLevel = 0, foliageShadows = true },
        };

        public static int IndexFor(string qualityName)
        {
            var n = (qualityName ?? "").ToLowerInvariant();
            return n.Contains("low") ? 0 : n.Contains("high") ? 2 : 1;
        }

        public const string VolumeLowResource = "Lighting/VP_Global_Low";
        public const string VolumeFullResource = "Lighting/VP_Global";
        public const string SkyboxResource = "Lighting/M_Sky_Bright";
        public static readonly Color SunColor = new Color(1f, 0.96f, 0.88f);
        public const float SunIntensity = 1.25f;
        public static readonly Vector3 SunEuler = new Vector3(50f, -35f, 0f);
        public static readonly Color AmbientSky = new Color(0.78f, 0.86f, 1f), AmbientEquator = new Color(0.86f, 0.84f, 0.80f), AmbientGround = new Color(0.55f, 0.52f, 0.48f);

        // ---------------- Realistic PBR textures ----------------
        public const string RealisticRoot = "Assets/_Project/Models/Realistic/";
        public const int SourceSize = 2048;        // CC0 scans are authored at 2048; the importer downsizes for Android
        public const int AndroidImportMax = 1024;  // PM cap for Android (all maps)
        public const int LeafAtlasMax = 1024;      // leaf atlases are authored at 1024, alpha-clipped

        /// <summary>True for Models/Realistic/** but never a v2 folder (Wrecker_v2 etc. stay with the VFX import rules).</summary>
        public static bool InRealisticScope(string assetPath)
        {
            if (assetPath == null) return false;
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(RealisticRoot, System.StringComparison.Ordinal)) return false;
            foreach (var seg in p.Split('/'))
            {
                var s = seg.ToLowerInvariant();
                if (s == "v2" || s.EndsWith("_v2")) return false;
            }
            return true;
        }

        public static bool IsFoliagePath(string assetPath) => assetPath != null && assetPath.Replace('\\', '/').Contains("/Foliage/");

        /// <summary>Map suffix of a texture stem (T_&lt;X&gt;_A / _N / _ORM), else null. Unity mask-map style names are not accepted.</summary>
        public static string PbrMap(string stem)
        {
            if (string.IsNullOrEmpty(stem)) return null;
            if (stem.EndsWith("_ORM", System.StringComparison.Ordinal)) return "_ORM";
            if (stem.EndsWith("_A", System.StringComparison.Ordinal)) return "_A";
            if (stem.EndsWith("_N", System.StringComparison.Ordinal)) return "_N";
            return null;
        }

        /// <summary>Unpacked/mask-map names that must be packed into T_&lt;X&gt;_ORM (R=AO, G=roughness, B=metallic) for CrashverseGlossyPBR._ORMMap.</summary>
        public static readonly string[] UnpackedMapSuffixes = { "_MaskMap", "_Mask", "_MOHS", "_Metallic", "_MetallicSmoothness", "_Roughness", "_Smoothness", "_AO", "_Occlusion", "_M", "_R" };

        public static bool IsUnpackedMaskName(string stem)
        {
            if (string.IsNullOrEmpty(stem) || PbrMap(stem) != null) return false;
            foreach (var s in UnpackedMapSuffixes) if (stem.EndsWith(s, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Import policy for a Realistic texture, or null if out of scope / not a T_&lt;X&gt;_{A,N,ORM} map.</summary>
        public static PbrTexturePolicy? TexturePolicy(string assetPath)
        {
            if (!InRealisticScope(assetPath)) return null;
            var stem = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var map = PbrMap(stem);
            if (map == null) return null;
            bool foliage = IsFoliagePath(assetPath);
            switch (map)
            {
                case "_N":   return new PbrTexturePolicy { map = map, sRGB = false, normalMap = true,  keepAlpha = false, maxSize = SourceSize, androidMax = AndroidImportMax, astc = "5x5" };
                case "_ORM": return new PbrTexturePolicy { map = map, sRGB = false, normalMap = false, keepAlpha = false, maxSize = SourceSize, androidMax = AndroidImportMax, astc = "6x6" };
                default:     // _A: foliage albedo is a leaf atlas, alpha-clipped (keep alpha, preserve coverage in mips)
                    return new PbrTexturePolicy { map = map, sRGB = true, normalMap = false, keepAlpha = foliage,
                                                  maxSize = foliage ? LeafAtlasMax : SourceSize, androidMax = foliage ? LeafAtlasMax : AndroidImportMax, astc = "6x6" };
            }
        }

        // ---------------- Foliage LOD ----------------
        public static readonly FoliageBudget TreeBudget = new FoliageBudget { lodTris = new[] { 6000, 2500, 800 }, impostorMinTris = 2, impostorMaxTris = 16 };
        public static readonly FoliageBudget BushBudget = new FoliageBudget { lodTris = new[] { 3000, 1250, 400 }, impostorMinTris = 2, impostorMaxTris = 16 };
        public static FoliageBudget BudgetFor(string assetPath)
        {
            var p = (assetPath ?? "").ToLowerInvariant();
            return p.Contains("bush") || p.Contains("shrub") ? BushBudget : TreeBudget;
        }

        /// <summary>LOD cross-fade for foliage LODGroups. None for now: CrashverseGlossyPBR has no LOD_FADE_CROSSFADE variant, so a
        /// cross-fading LODGroup would draw both LODs with no dither (double cost, no benefit). Flip to CrossFade (dithered, animated)
        /// only after the shader gains the keyword; URP 14 (2022.3) has no per-tier cross-fade switch.</summary>
        public const LODFadeMode FoliageLodFadeMode = LODFadeMode.None;
        public const float CrossFadeAnimationDuration = 0.25f;
    }
}
