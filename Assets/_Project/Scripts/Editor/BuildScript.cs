#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>
    /// CI entry point (GameCI buildMethod). Profile from env CRASHVERSE_BUILD_PROFILE: "dev" (default) adds CRASHVERSE_DEV and a
    /// Development build; "release" strips it. Security Review §1.
    /// </summary>
    public static class BuildScript
    {
        public const string DevDefine = "CRASHVERSE_DEV";

        public static void BuildAndroid()
        {
            bool release = string.Equals(Environment.GetEnvironmentVariable("CRASHVERSE_BUILD_PROFILE"), "release", StringComparison.OrdinalIgnoreCase);
            SetDevDefine(!release);
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "build/Android/CRASHVERSE" + (release ? "" : "-dev") + ".apk",
                target = BuildTarget.Android,
                options = release ? BuildOptions.None : BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded) { EditorApplication.Exit(1); }
        }

        public static void SetDevDefine(bool on)
        {
            var target = NamedBuildTarget.Android;
            var defs = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0 && d != DevDefine).ToList();
            if (on) defs.Add(DevDefine);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defs));
        }
    }

    /// <summary>Release builds must fail if they would fall back to the local backend (no real IBackendService) or still carry dev defines.</summary>
    public class ReleaseBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            bool dev = (report.summary.options & BuildOptions.Development) != 0;
            var defs = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup));
            bool devDefine = defs.Split(';').Contains(BuildScript.DevDefine);
            if (dev || devDefine) return;
            var iface = typeof(Crashverse.Backend.IBackendService);
            var real = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => iface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && t.Name != "LocalBackendService").ToList();
            if (real.Count == 0)
                throw new BuildFailedException("Release build has no server IBackendService implementation; it would fall back to LocalBackendService (dev-only). Build a DEV profile or add the real backend.");
        }
    }
}
#endif
