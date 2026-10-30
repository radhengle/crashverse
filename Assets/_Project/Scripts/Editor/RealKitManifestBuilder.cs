#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>Writes Resources/RealKit/RealKitManifest.asset (generated; .gitignored) so the player build ships the 14 Kit_Real* pieces
    /// and their 1024 tiling sets. Called by BuildScript before BuildPlayer; menu for local builds.</summary>
    public static class RealKitManifestBuilder
    {
        public const string Dir = "Assets/_Project/Resources/RealKit";
        public const string AssetPath = Dir + "/RealKitManifest.asset";

        [MenuItem("Crashverse/Realistic/Write RealKit manifest")]
        public static bool Write()
        {
            var m = Crashverse.Level.RealKitManifest.FromAssetDatabase();
            if (m == null) { Debug.LogWarning("[RealKit] no Kit_Real* models found: player keeps procedural cubes"); return false; }
            Directory.CreateDirectory(Dir);
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.CreateAsset(m, AssetPath);
            AssetDatabase.SaveAssets();
            int missingTex = 0; foreach (var k in m.kits) if (k.albedo == null) missingTex++;
            Debug.Log($"[RealKit] manifest: {m.pieces.Length}/14 pieces, {m.kits.Length - missingTex}/{m.kits.Length} kits with albedo -> {AssetPath}");
            return true;
        }
    }
}
#endif
