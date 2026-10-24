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
        /// <summary>Dev-only scenes: appended to the dev build's scene list, never in release (and never in EditorBuildSettings).</summary>
        public static readonly string[] DevScenes = { "Assets/_Project/Scenes/Dev/L10_PerfFullBudget.unity" };
        public static bool IsDevScene(string path) => path != null && path.Replace('\\', '/').Contains("/Scenes/Dev/");

        /// <summary>Scene list for a profile: EditorBuildSettings (minus any dev scene) + DevScenes for dev builds only.</summary>
        public static string[] ScenesFor(bool release)
        {
            var baseScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Where(p => !IsDevScene(p));
            return (release ? baseScenes : baseScenes.Concat(DevScenes.Where(p => System.IO.File.Exists(p)))).Distinct().ToArray();
        }

        /// <summary>Frame Timing Stats (FrameTimingManager GPU/CPU times for the VFX perf probe): dev builds only.</summary>
        public static void SetFrameTimingStats(bool dev) => PlayerSettings.enableFrameTimingStats = dev;

        public static void BuildAndroid()
        {
            bool release = string.Equals(Environment.GetEnvironmentVariable("CRASHVERSE_BUILD_PROFILE"), "release", StringComparison.OrdinalIgnoreCase);
            SetDevDefine(!release);
            UrpSetup.Ensure(); LightingSetup.Ensure(); // BUG-23: URP pipeline assigned in Graphics + every Quality level before building
            SetFrameTimingStats(!release);
            // QA: Play needs 64-bit. IL2CPP + ARMv7|ARM64 in code too, so the build never depends on ProjectSettings alone.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // APK-05 (#21): portrait only, no autorotation.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
            // Release: arm64 + v7a. Dev: also x86_64 so QA can run it on emulators/Chromebooks.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64 | (release ? 0 : AndroidArchitecture.X86_64);
            // BUG-21: every dev build must be signed with the same persistent dev key (GameCI otherwise makes a fresh debug
            // keystore per runner, so a new build can't update the old one) and carry an increasing versionCode.
            // GameCI's docker only forwards its own variables (ANDROID_*), so read those first, then ours.
            string Env(params string[] names) { foreach (var n in names) { var v = Environment.GetEnvironmentVariable(n); if (!string.IsNullOrEmpty(v)) return v; } return null; }
            var vc = Env("ANDROID_VERSION_CODE", "CRASHVERSE_VERSION_CODE");
            if (int.TryParse(vc, out var code) && code > 0) PlayerSettings.Android.bundleVersionCode = code;
            else if (Environment.GetEnvironmentVariable("CI") != null) { Debug.LogError("CRASHVERSE_VERSION_CODE missing in CI"); EditorApplication.Exit(1); }
            var ks = Env("ANDROID_KEYSTORE_NAME", "CRASHVERSE_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(ks))
            {
                Debug.Log("[BuildScript] signing with keystore " + ks + ", versionCode " + PlayerSettings.Android.bundleVersionCode);
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = System.IO.Path.GetFullPath(ks);
                PlayerSettings.Android.keystorePass = Env("ANDROID_KEYSTORE_PASS", "CRASHVERSE_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Env("ANDROID_KEYALIAS_NAME", "CRASHVERSE_KEYALIAS_NAME");
                PlayerSettings.Android.keyaliasPass = Env("ANDROID_KEYALIAS_PASS", "CRASHVERSE_KEYALIAS_PASS");
            }
            else if (Environment.GetEnvironmentVariable("CI") != null) { Debug.LogError("CRASHVERSE_KEYSTORE_PATH missing in CI: refusing a throwaway debug key"); EditorApplication.Exit(1); }
            // Security: the dev key is never the Play upload key. A release build signed with the dev keystore/alias is refused.
            if (release && PlayerSettings.Android.useCustomKeystore &&
                (PlayerSettings.Android.keyaliasName == "crashverse-dev" || (PlayerSettings.Android.keystoreName ?? "").EndsWith("dev.keystore", StringComparison.OrdinalIgnoreCase)))
            { Debug.LogError("Refusing a release build signed with the dev key"); EditorApplication.Exit(1); return; }
            var opts = new BuildPlayerOptions
            {
                scenes = ScenesFor(release),
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
            foreach (var sc in EditorBuildSettings.scenes)
                if (sc.enabled && BuildScript.IsDevScene(sc.path)) throw new BuildFailedException($"Release build lists dev-only scene {sc.path}.");
            if (PlayerSettings.enableFrameTimingStats) throw new BuildFailedException("Release build has Frame Timing Stats on (dev-only).");
            var iface = typeof(Crashverse.Backend.IBackendService);
            var real = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => iface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && t.Name != "LocalBackendService").ToList();
            if (real.Count == 0)
                throw new BuildFailedException("Release build has no server IBackendService implementation; it would fall back to LocalBackendService (dev-only). Build a DEV profile or add the real backend.");
        }
    }
}
#endif
