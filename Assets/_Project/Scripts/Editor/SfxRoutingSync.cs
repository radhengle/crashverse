using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>
    /// audio/sfx_routing.json (Sound Manager's source of truth, repo root, outside Assets) → Assets/_Project/Resources/Audio/sfx_routing.json,
    /// so SfxPlayer can Resources.Load it at runtime. Runs before every player build and on editor load; the copy is generated (gitignored).
    /// </summary>
    [InitializeOnLoad]
    public class SfxRoutingSync : IPreprocessBuildWithReport
    {
        public const string Source = "audio/sfx_routing.json", Target = "Assets/_Project/Resources/Audio/sfx_routing.json";
        public int callbackOrder => -100;
        static SfxRoutingSync() { EditorApplication.delayCall += () => Sync(false); }
        public void OnPreprocessBuild(BuildReport report) => Sync(true);

        [MenuItem("Crashverse/Audio/Sync sfx_routing.json")]
        public static void SyncMenu() => Sync(true);

        public static bool Sync(bool force)
        {
            if (!File.Exists(Source)) { Debug.LogWarning($"[SFX] {Source} not found (PR #32): the build will have no SFX routes"); return false; }
            if (!force && File.Exists(Target) && File.ReadAllText(Target) == File.ReadAllText(Source)) return true;
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.Copy(Source, Target, true);
            AssetDatabase.ImportAsset(Target);
            return true;
        }
    }
}
