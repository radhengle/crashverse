#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Crashverse.UI;

namespace Crashverse.EditorTools
{
    /// <summary>Security Admin: release (non-development) builds fail when Resources/PrivacyConfig.json has no valid https privacyUrl.</summary>
    public class PrivacyUrlBuildGuard : IPreprocessBuildWithReport
    {
        public const string ConfigPath = "Assets/_Project/Resources/PrivacyConfig.json";
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            bool dev = (report.summary.options & BuildOptions.Development) != 0;
            var json = File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : null;
            var err = Check(dev, json);
            if (err != null) throw new BuildFailedException(err);
        }
        /// <summary>null = OK; otherwise the failure reason. Development builds always pass (links are hidden at runtime).</summary>
        public static string Check(bool development, string configJson)
        {
            if (development) return null;
            if (configJson == null) return "Release build: " + ConfigPath + " is missing. Add privacyUrl (https) before shipping.";
            var url = PrivacyConfig.Parse(configJson);
            return PrivacyConfig.IsValid(url) ? null : "Release build: privacyUrl in " + ConfigPath + " is empty or not an https URL. Security Admin must supply the published privacy policy URL.";
        }
    }
}
#endif
