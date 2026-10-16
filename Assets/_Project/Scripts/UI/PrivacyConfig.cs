using System;
using UnityEngine;

namespace Crashverse.UI
{
    /// <summary>Security Admin: the privacy policy URL comes from config (Resources/PrivacyConfig.json), never from code.
    /// Empty or invalid → the app hides every privacy link (no dead links). Release builds fail if it is empty (PrivacyUrlBuildGuard).</summary>
    public static class PrivacyConfig
    {
        public const string ResourcePath = "PrivacyConfig";
        [Serializable] class Data { public string privacyUrl = ""; }
        static string _url; static bool _loaded;

        public static string Url { get { if (!_loaded) { _url = Parse(Resources.Load<TextAsset>(ResourcePath)?.text); _loaded = true; } return _url; } }
        public static bool HasUrl => IsValid(Url);

        /// <summary>Returns the trimmed URL from config JSON, or "" when missing/malformed.</summary>
        public static string Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            try { return (JsonUtility.FromJson<Data>(json)?.privacyUrl ?? "").Trim(); } catch { return ""; }
        }
        /// <summary>Only absolute https URLs count; anything else is treated as empty.</summary>
        public static bool IsValid(string url) =>
            !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(u.Host);

        public static void Open() { if (HasUrl) Application.OpenURL(Url); }
        /// <summary>Tests only.</summary>
        public static void OverrideForTests(string url) { _url = url ?? ""; _loaded = true; }
        public static void ResetForTests() { _loaded = false; _url = null; }
    }
}
