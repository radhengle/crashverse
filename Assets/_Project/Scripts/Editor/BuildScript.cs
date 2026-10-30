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
            bool release = string.Equals(Profile(), "release", StringComparison.OrdinalIgnoreCase);
            Debug.Log("[BuildScript] profile " + (release ? "release" : "dev"));
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
            // QA V2-08: the PR-only release check APK adds x86_64 (-crashverseEmulatorAbi) so the release variant can be launched on
            // the x86_64 CI emulator. Never used for the AAB (refused below) or anything published.
            bool emuAbi = release && HasArg("-crashverseEmulatorAbi");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64 | (release && !emuAbi ? 0 : AndroidArchitecture.X86_64);
            // Security #55 M-1: -crashverseSignAfter = Unity never sees a keystore or password (GameCI would put them on Unity's argv);
            // the workflow signs afterwards on the runner (apksigner/jarsigner with passwords from env) and pins the certificate.
            bool signAfter = HasArg("-crashverseSignAfter");
            // BUG-21: every dev build must be signed with the same persistent dev key (GameCI otherwise makes a fresh debug
            // keystore per runner, so a new build can't update the old one) and carry an increasing versionCode.
            // GameCI's docker only forwards its own variables (ANDROID_*), so read those first, then ours.
            string Env(params string[] names) { foreach (var n in names) { var v = Environment.GetEnvironmentVariable(n); if (!string.IsNullOrEmpty(v)) return v; } return null; }
            var vc = Env("ANDROID_VERSION_CODE", "CRASHVERSE_VERSION_CODE");
            if (int.TryParse(vc, out var code) && code > 0) PlayerSettings.Android.bundleVersionCode = code;
            else if (Environment.GetEnvironmentVariable("CI") != null && !signAfter) { Debug.LogError("CRASHVERSE_VERSION_CODE missing in CI"); EditorApplication.Exit(1); }
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
            // PR #8 (folded): signed release AAB for Play, upload key from the release environment's secrets (never the dev key, see above)
            bool aab = WantsAab();
            if (aab && (!release || (!PlayerSettings.Android.useCustomKeystore && !signAfter))) { Debug.LogError("AAB builds are release-only and need the upload keystore or -crashverseSignAfter"); EditorApplication.Exit(1); return; }
            if (aab && emuAbi) { Debug.LogError("-crashverseEmulatorAbi is for the PR check APK only, never an AAB"); EditorApplication.Exit(1); return; }
            if (signAfter && PlayerSettings.Android.useCustomKeystore) { Debug.LogError("-crashverseSignAfter with a keystore passed to Unity: remove the androidKeystore* inputs"); EditorApplication.Exit(1); return; }
            EditorUserBuildSettings.buildAppBundle = aab;
            var opts = new BuildPlayerOptions
            {
                scenes = ScenesFor(release),
                locationPathName = "build/Android/CRASHVERSE" + (release ? "" : "-dev") + (aab ? ".aab" : ".apk"),
                target = BuildTarget.Android,
                options = release ? BuildOptions.None : BuildOptions.Development,
            };
            RealKitManifestBuilder.Write(); // realistic v1 pieces into Resources (generated)
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded) { EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Build profile. GameCI's docker forwards only its own variables (not CRASHVERSE_BUILD_PROFILE), so CI passes it as a Unity
        /// argument (customParameters: -crashverseProfile release); the env var still works for local builds. QA R-3: before this the
        /// "release" profile silently built a dev (debuggable, CRASHVERSE_DEV) player.
        /// </summary>
        public static string Profile()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-crashverseProfile") return args[i + 1];
            return Environment.GetEnvironmentVariable("CRASHVERSE_BUILD_PROFILE") ?? "dev";
        }

        public static bool HasArg(string a) => Environment.GetCommandLineArgs().Contains(a);

        /// <summary>Android App Bundle when GameCI is asked for one (androidExportType: androidAppBundle → ANDROID_EXPORT_TYPE).</summary>
        public static bool WantsAab() =>
            string.Equals(Environment.GetEnvironmentVariable("ANDROID_EXPORT_TYPE"), "androidAppBundle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("CRASHVERSE_ANDROID_EXPORT"), "aab", StringComparison.OrdinalIgnoreCase);

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
                .Where(t => iface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && t.Name != "LocalBackendService").ToList(); // v2: OfflineBackendService qualifies (fails every call, grants nothing)
            if (real.Count == 0)
                throw new BuildFailedException("Release build has no server IBackendService implementation; it would fall back to LocalBackendService (dev-only). Build a DEV profile or add the real backend.");
        }
    }
}
#endif
