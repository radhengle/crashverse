using System;
using UnityEngine;

namespace Crashverse.Level
{
    /// <summary>References to the 14 Kit_Real* models and their per-kit texture sets. Written into Resources/RealKit by
    /// RealKitManifestBuilder before every player build (generated, not in git); in the editor it is assembled from the AssetDatabase.</summary>
    public class RealKitManifest : ScriptableObject
    {
        [Serializable] public class Piece { public string name; public GameObject model; }
        [Serializable] public class Kit { public string name; public Texture2D albedo, normal, orm, innerAlbedo, innerNormal, innerOrm; }
        public Piece[] pieces = new Piece[0];
        public Kit[] kits = new Kit[0];

        public Piece Find(string n) { foreach (var p in pieces) if (p != null && p.name == n) return p; return null; }
        public Kit FindKit(string n) { foreach (var k in kits) if (k != null && k.name == n) return k; return null; }

#if UNITY_EDITOR
        public static string ModelPath(string piece)
        {
            string kit = Crashverse.Meta.RealPieceRules.KitOf(piece);
            return RealKit.ModelsRoot + (kit == "Props" ? "Props/" : "Buildings/") + "Kit_Real" + kit + "/" + piece + ".fbx";
        }
        public static string TexPath(string kit, string stem) =>
            RealKit.ModelsRoot + (kit == "Props" ? "Props/" : "Buildings/") + "Kit_Real" + kit + "/Textures/T_Real" + kit + stem + ".png";

        /// <summary>Editor: build an in-memory manifest from Models/Realistic (also used by the build-time writer).</summary>
        public static RealKitManifest FromAssetDatabase()
        {
            var m = CreateInstance<RealKitManifest>();
            var ps = new System.Collections.Generic.List<Piece>(); var ks = new System.Collections.Generic.List<Kit>();
            foreach (var p in Crashverse.Meta.RealPieceRules.Pieces)
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(p));
                if (go != null) ps.Add(new Piece { name = p, model = go }); else Debug.LogWarning("[RealKit] missing " + ModelPath(p));
            }
            foreach (var k in new[] { "Brick", "Wood", "Steel", "Roof", "Props" })
            {
                Texture2D T(string s) => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath(k, s));
                ks.Add(new Kit { name = k, albedo = T("_A"), normal = T("_N"), orm = T("_ORM"), innerAlbedo = T("_Inner_A"), innerNormal = T("_Inner_N"), innerOrm = T("_Inner_ORM") });
            }
            m.pieces = ps.ToArray(); m.kits = ks.ToArray();
            return ps.Count > 0 ? m : null;
        }
#endif
    }
}
