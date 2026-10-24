#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>
    /// Art & UI pack v2 (/workspace/art/ui_v2, copied to Resources/UI): every texture there is a UI sprite (no mips, clamp,
    /// alpha-is-transparency for the 256px icons). Backgrounds (bg_*) keep their full 1080x2340 size (max 2048 tall would
    /// only lose ~12%), compressed; icons stay at 256. No baked text: all labels are engine UI.
    /// </summary>
    public class UiArtImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/UI/")) return;
            var imp = (TextureImporter)assetImporter;
            bool bg = System.IO.Path.GetFileName(p).StartsWith("bg_");
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaIsTransparency = !bg;
            imp.sRGBTexture = true;
            imp.maxTextureSize = bg ? 4096 : 256;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
#endif
