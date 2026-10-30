#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Crashverse.EditorTools.Import
{
    /// <summary>RealisticPbrTests.RealisticFoliageMaterials_EnableInstancing: every material under Models/**/Foliage/ gets GPU instancing
    /// (fallback when SRP batching breaks), however it was created (FBX external-material extraction, CreateOrWire, or by hand). Runs on
    /// every imported/moved .mat, so a cached FBX import that skipped OnAssignMaterialModel can't leave a foliage material without it.</summary>
    public class FoliageInstancingPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool any = false;
            foreach (var list in new[] { imported, moved })
                foreach (var p in list)
                    if (Fix(p)) any = true;
            if (any) EditorApplication.delayCall += AssetDatabase.SaveAssets;
        }

        public static bool Fix(string path)
        {
            if (path == null || !path.EndsWith(".mat")) return false;
            var p = path.Replace('\\', '/');
            if (!p.StartsWith("Assets/_Project/Models/") || !p.Contains("/Foliage/")) return false;
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null || m.enableInstancing) return false;
            m.enableInstancing = true; EditorUtility.SetDirty(m); return true;
        }

        [MenuItem("Crashverse/Import/Enable instancing on foliage materials")]
        public static int SweepAll()
        {
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Models" }))
                if (Fix(AssetDatabase.GUIDToAssetPath(g))) n++;
            if (n > 0) AssetDatabase.SaveAssets();
            return n;
        }
    }
}
#endif
