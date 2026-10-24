#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using C = Crashverse.EditorTools.Import.ImportConventions;

namespace Crashverse.EditorTools.Import
{
    /// <summary>
    /// Reads the hero building stats sidecars (models/HeroBuildings/Kit_&lt;B&gt;_stats.json, 3D Modeler) for the import pipeline.
    /// The files live outside Assets, so each kit gets an AssetDatabase custom dependency (content hash): hero module FBX files
    /// declare it in OnPreprocessModel and reimport on the next refresh after a sidecar changes (menu: Reimport Hero Building Kits).
    /// </summary>
    [InitializeOnLoad]
    public static class HeroStatsCache
    {
        static HeroStatsCache() { RegisterDependencies(); }

        public static void RegisterDependencies()
        {
            foreach (var kit in C.KnownStatsKits) Register(kit);
            if (!Directory.Exists(C.HeroStatsDir)) return;
            foreach (var f in Directory.GetFiles(C.HeroStatsDir, "Kit_*_stats.json"))
            {
                string n = Path.GetFileName(f);
                Register(n.Substring(4, n.Length - 4 - "_stats.json".Length));
            }
        }

        static void Register(string kit)
        {
            string p = C.HeroStatsPath(kit);
            string body = File.Exists(p) ? File.ReadAllText(p) : "<missing>";
            AssetDatabase.RegisterCustomDependency(C.HeroStatsDependency(kit), Hash128.Compute(body));
        }

        /// <summary>Parsed sidecar for kit &lt;B&gt;, or null when it is missing or malformed (<paramref name="error"/> says which).</summary>
        public static C.HeroStats Load(string kit, out string error)
        {
            error = null;
            string p = C.HeroStatsPath(kit);
            if (!File.Exists(p)) { error = $"no {p}"; return null; }
            try { return C.ParseHeroStats(File.ReadAllText(p)); }
            catch (Exception e)
            {
                error = $"{p} unreadable: {e.Message}";
                Debug.LogWarning($"[CrashverseImport] {error}. fractureClass not written from JSON.");
                return null;
            }
        }

        [MenuItem("Crashverse/Import/Reimport Hero Building Kits")]   // all stats kits, incl. Kit_Props
        public static void ReimportHeroKits()
        {
            RegisterDependencies();
            AssetDatabase.Refresh();
            foreach (var d in AssetDatabase.GetSubFolders(C.KitRoot.TrimEnd('/')))
                if (C.UsesHeroStats(C.KitFolder(d + "/x"))) AssetDatabase.ImportAsset(d, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
