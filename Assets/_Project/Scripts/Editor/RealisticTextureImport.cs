#if UNITY_EDITOR
using Crashverse.Rendering;
using UnityEditor;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>
    /// Realistic PBR texture rules for Assets/_Project/Models/Realistic/** (Buildings, Foliage, Props, Materials/Textures; never a v2 folder).
    /// Runs AFTER CrashverseImportPostprocessor (VFX, order 100), which still owns FBX/material wiring for all of Models/**; this only
    /// narrows the texture settings for the realistic set. All numbers come from RenderTiers.TexturePolicy (single source of truth).
    ///   T_&lt;X&gt;_A   albedo, sRGB, ASTC 6x6 (Foliage: leaf atlas 1024, alpha kept for clipping, coverage-preserving mips)
    ///   T_&lt;X&gt;_N   NormalMap type, linear, ASTC 5x5
    ///   T_&lt;X&gt;_ORM  packed R=AO G=roughness B=metallic, linear, ASTC 6x6 (CrashverseGlossyPBR._ORMMap; not Unity's mask-map layout)
    /// Sources are 2048; Android is capped at 1024 (PM; tiling materials 1024). Realistic/Props/** are 512 on every platform.
    /// Per-tier size on device = import max >> QualitySettings.globalTextureMipmapLimit.
    /// </summary>
    public class RealisticTextureImport : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 200;
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!RenderTiers.InRealisticScope(assetPath)) return;
            var stem = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (RenderTiers.IsUnpackedMaskName(stem))
            {
                Debug.LogWarning($"[RealisticTextureImport] {assetPath}: pack metallic/AO/roughness into T_<X>_ORM (R=AO, G=roughness, B=metallic, linear) for CrashverseGlossyPBR._ORMMap.");
                return;
            }
            var pol = RenderTiers.TexturePolicy(assetPath);
            if (pol == null) return;
            var p = pol.Value;
            var ti = (TextureImporter)assetImporter;

            ti.textureType = p.normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = p.sRGB;
            ti.mipmapEnabled = true;
            ti.isReadable = false;
            ti.alphaSource = p.keepAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = p.keepAlpha;
            ti.mipMapsPreserveCoverage = p.keepAlpha;   // leaf cards stay full in distant mips
            if (p.keepAlpha) ti.alphaTestReferenceValue = 0.5f;
            ti.maxTextureSize = p.maxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;

            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = p.androidMax;
            android.format = Astc(p.astc);
            android.compressionQuality = 50;
            ti.SetPlatformTextureSettings(android);
        }

        public static TextureImporterFormat Astc(string block)
        {
            switch (block)
            {
                case "4x4": return TextureImporterFormat.ASTC_4x4;
                case "5x5": return TextureImporterFormat.ASTC_5x5;
                case "8x8": return TextureImporterFormat.ASTC_8x8;
                default: return TextureImporterFormat.ASTC_6x6;
            }
        }
    }
}
#endif
