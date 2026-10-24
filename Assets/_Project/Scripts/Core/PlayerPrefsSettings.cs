using UnityEngine;

namespace Crashverse.Core
{
    /// <summary>PlayerPrefs-backed store for GameSettings (same pattern as Feel.PlayerPrefsMotionPrefs).</summary>
    public class PlayerPrefsIntPrefs : IIntPrefs
    {
        public bool TryGet(string key, out int v) { v = PlayerPrefs.GetInt(key, 0); return PlayerPrefs.HasKey(key); }
        public void Set(string key, int v) { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() { GameSettings.Current = new GameSettings(new PlayerPrefsIntPrefs()); }
    }
}
