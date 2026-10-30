#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Crashverse.EditorTools
{
    /// <summary>
    /// QA V2-01 (release blocker): a Google Play services / package / overlay update sends CONFIG_ASSETS_PATHS (0x80000000). The activity
    /// didn't declare it, so Android RELAUNCHED UnityPlayerActivity → Unity onDestroy → Unity kills its own process (SIG 9).
    /// Fix: declare it. The manifest token is `assetsPaths` (configChanges flag added in the API 36 SDK; the bit itself is honoured by
    /// Android 12+ devices, incl. API 34), so the activity gets onConfigurationChanged and Unity keeps running.
    /// aapt2 only knows the token when linking against android-36 and only aapt2 >= 8.1 can read the API 35/36 framework resource table,
    /// so this hook (generated Gradle project, every Android build incl. the AAB):
    ///   1. compileSdk 36 for unityLibrary + launcher (targetSdk stays 35: Play requirement, asserted by check_apk.sh),
    ///   2. android.aapt2FromMavenOverride = the pinned aapt2 8.7.3 the workflow puts in .aapt2-override/ (unless Unity already set one),
    ///   3. appends |assetsPaths to the activity's android:configChanges.
    /// Missing platform 36 or aapt2 = a failed build (never a silent APK without the fix). check_apk.sh asserts bit 0x80000000 is set.
    /// </summary>
    public class AssetsPathsGradleHook : IPostGenerateGradleAndroidProject
    {
        public const int CompileSdk = 36;
        public const string Token = "assetsPaths";
        public int callbackOrder => 50;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var root = Directory.GetParent(unityLibraryPath).FullName;
            Debug.Log("[V2-01] gradle project " + root);
            // 1. compileSdk 36
            foreach (var module in new[] { unityLibraryPath, Path.Combine(root, "launcher") })
            {
                var g = Path.Combine(module, "build.gradle"); if (!File.Exists(g)) continue;
                var src = File.ReadAllText(g);
                var before = Regex.Match(src, @"compileSdk(Version)?\s*=?\s*\d+").Value;
                var patched = PatchCompileSdk(src, CompileSdk);
                File.WriteAllText(g, patched);
                Debug.Log($"[V2-01] {Path.GetFileName(module)}/build.gradle: '{before}' -> compileSdk {CompileSdk}");
            }
            var agp = Regex.Match(SafeRead(Path.Combine(root, "build.gradle")), @"com\.android\.tools\.build:gradle:([0-9.]+)").Groups[1].Value;
            Debug.Log("[V2-01] Android Gradle Plugin " + (agp.Length > 0 ? agp : "(not found in build.gradle)"));

            // platform 36 must be installed (the workflow passes platforms;android-36 to GameCI's sdkmanager)
            string sdk = null;
            try { sdk = AndroidExternalToolsSettings.sdkRootPath; } catch (Exception) { }
            if (string.IsNullOrEmpty(sdk)) sdk = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
            var jar = string.IsNullOrEmpty(sdk) ? null : Path.Combine(sdk, "platforms", "android-" + CompileSdk, "android.jar");
            if (jar != null && !File.Exists(jar)) InstallPlatform(sdk); // GameCI only installs the target platform (35)
            if (jar == null || !File.Exists(jar))
                throw new BuildFailedException($"[V2-01] Android SDK platform {CompileSdk} missing ({jar ?? "no SDK path"}): add platforms;android-{CompileSdk} to androidSdkManagerParameters");

            // 2. aapt2 able to read the API 36 resource table
            var props = Path.Combine(root, "gradle.properties");
            var p = SafeRead(props);
            if (!p.Contains("android.aapt2FromMavenOverride"))
            {
                var aapt2 = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".aapt2-override", "aapt2"));
                if (!File.Exists(aapt2)) throw new BuildFailedException("[V2-01] " + aapt2 + " missing: the workflow must fetch the pinned aapt2 8.7.3 first");
                p += $"\nandroid.aapt2FromMavenOverride={aapt2}\n";
            }
            if (!p.Contains("android.suppressUnsupportedCompileSdk")) p += $"android.suppressUnsupportedCompileSdk={CompileSdk}\n";
            File.WriteAllText(props, p);

            // 3. manifest token
            var man = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            var m = SafeRead(man);
            var pm = PatchConfigChanges(m);
            if (pm == m && !m.Contains(Token)) throw new BuildFailedException("[V2-01] no android:configChanges on the Unity activity in " + man);
            File.WriteAllText(man, pm);
            Debug.Log("[V2-01] configChanges: " + Regex.Match(pm, "android:configChanges=\"[^\"]*\"").Value);
        }

        /// <summary>sdkmanager "platforms;android-36" with Unity's JDK (GameCI already accepted the SDK licences for platform 35).</summary>
        static void InstallPlatform(string sdk)
        {
            var mgr = new[] { Path.Combine(sdk, "cmdline-tools"), Path.Combine(sdk, "tools", "bin") }.Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "sdkmanager", SearchOption.AllDirectories)).FirstOrDefault();
            if (mgr == null) { Debug.LogError("[V2-01] no sdkmanager under " + sdk); return; }
            var psi = new System.Diagnostics.ProcessStartInfo(mgr, $"\"platforms;android-{CompileSdk}\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            try { var jdk = AndroidExternalToolsSettings.jdkRootPath; if (!string.IsNullOrEmpty(jdk)) psi.EnvironmentVariables["JAVA_HOME"] = jdk; } catch (Exception) { }
            Debug.Log($"[V2-01] installing platforms;android-{CompileSdk} with {mgr}");
            using (var pr = System.Diagnostics.Process.Start(psi))
            {
                pr.StandardInput.WriteLine("y"); pr.StandardInput.Close();
                var o = pr.StandardOutput.ReadToEnd(); var e = pr.StandardError.ReadToEnd(); pr.WaitForExit(600000);
                Debug.Log($"[V2-01] sdkmanager exit {pr.ExitCode}\n{Tail(o)}\n{Tail(e)}");
            }
        }

        static string Tail(string s) => s == null ? "" : s.Length > 1500 ? s.Substring(s.Length - 1500) : s;

        static string SafeRead(string f) => File.Exists(f) ? File.ReadAllText(f) : "";

        /// <summary>Sets every compileSdk / compileSdkVersion in a build.gradle to <paramref name="sdk"/> (both Groovy forms).</summary>
        public static string PatchCompileSdk(string gradle, int sdk) =>
            Regex.Replace(gradle, @"(compileSdk(?:Version)?)(\s*=?\s*)\d+", m => m.Groups[1].Value + m.Groups[2].Value + sdk);

        /// <summary>Appends |assetsPaths to each android:configChanges that lacks it.</summary>
        public static string PatchConfigChanges(string manifest) =>
            Regex.Replace(manifest, "android:configChanges=\"([^\"]*)\"", m =>
                m.Groups[1].Value.Split('|').Select(s => s.Trim()).Contains(Token) ? m.Value : $"android:configChanges=\"{m.Groups[1].Value}|{Token}\"");
    }
}
#endif
