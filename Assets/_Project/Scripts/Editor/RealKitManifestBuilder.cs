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
            // Level Designer prefab map: every BlockData.model used by a bundled layout ships too
            var list = new System.Collections.Generic.List<Crashverse.Level.RealKitManifest.Piece>(m.pieces);
            foreach (var f in Directory.GetFiles("Assets/_Project/Resources/Data/Layouts", "*.json"))
            {
                var lay = JsonUtility.FromJson<Crashverse.Data.LayoutData>(File.ReadAllText(f));
                if (lay?.blocks == null) continue;
                foreach (var b in lay.blocks)
                {
                    if (b.parts != null)
                        foreach (var part in b.parts)
                        {
                            var pn = Crashverse.Level.RealKit.ModuleName(part);
                            if (string.IsNullOrEmpty(pn) || m.Find(pn) != null || list.Exists(x => x.name == pn)) continue;
                            var pe = Crashverse.Level.RealKitManifest.ResolveEditor(part);
                            if (pe != null) list.Add(pe); else Debug.LogError($"[RealKit] {Path.GetFileName(f)}:{b.id} part '{part}' not found under Assets/_Project/Models");
                        }
                    if (string.IsNullOrEmpty(b.model) || m.Find(Crashverse.Level.RealKit.ModuleName(b.model)) != null || list.Exists(x => x.name == Crashverse.Level.RealKit.ModuleName(b.model))) continue;
                    var e = Crashverse.Level.RealKitManifest.ResolveEditor(b.model);
                    if (e != null) list.Add(e); else Debug.LogError($"[RealKit] {Path.GetFileName(f)}:{b.id} model '{b.model}' not found under Assets/_Project/Models");
                }
            }
            m.pieces = list.ToArray();
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
