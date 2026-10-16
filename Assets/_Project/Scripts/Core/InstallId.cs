using System;
using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>
    /// deviceInstallId for POST /v1/agegate (N4): a random UUID created once per install. Never a hardware or advertising ID.
    /// Android: kept in EncryptedSharedPreferences (AndroidX Security, key in the Android Keystore). Editor/other: PlayerPrefs.
    /// Cleared by uninstall or clear-data; backups are off (APK-04), so it never moves to another device.
    /// </summary>
    public static class InstallId
    {
        const string Key = "cv_install_id", File = "crashverse_secure_prefs";
        static string _cached;

        public static string Get()
        {
            if (!string.IsNullOrEmpty(_cached)) return _cached;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _cached = AndroidSecure(); } catch (Exception e) { Debug.LogWarning("[InstallId] Keystore prefs unavailable: " + e.Message); }
#endif
            if (string.IsNullOrEmpty(_cached))
            {
                _cached = PlayerPrefs.GetString(Key, "");
                if (string.IsNullOrEmpty(_cached)) { _cached = Guid.NewGuid().ToString(); PlayerPrefs.SetString(Key, _cached); PlayerPrefs.Save(); }
            }
            return _cached;
        }

        public static void Reset() { _cached = null; PlayerPrefs.DeleteKey(Key); } // account deletion wipe (A4)

#if UNITY_ANDROID && !UNITY_EDITOR
        static string AndroidSecure()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var ctx = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var builder = new AndroidJavaObject("androidx.security.crypto.MasterKey$Builder", ctx))
            {
                var scheme = new AndroidJavaClass("androidx.security.crypto.MasterKey$KeyScheme").GetStatic<AndroidJavaObject>("AES256_GCM");
                var master = builder.Call<AndroidJavaObject>("setKeyScheme", scheme).Call<AndroidJavaObject>("build");
                var kEnc = new AndroidJavaClass("androidx.security.crypto.EncryptedSharedPreferences$PrefKeyEncryptionScheme").GetStatic<AndroidJavaObject>("AES256_SIV");
                var vEnc = new AndroidJavaClass("androidx.security.crypto.EncryptedSharedPreferences$PrefValueEncryptionScheme").GetStatic<AndroidJavaObject>("AES256_GCM");
                var prefs = new AndroidJavaClass("androidx.security.crypto.EncryptedSharedPreferences").CallStatic<AndroidJavaObject>("create", ctx, File, master, kEnc, vEnc);
                var id = prefs.Call<string>("getString", Key, null);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                    prefs.Call<AndroidJavaObject>("edit").Call<AndroidJavaObject>("putString", Key, id).Call("apply");
                }
                return id;
            }
        }
#endif
    }
}
