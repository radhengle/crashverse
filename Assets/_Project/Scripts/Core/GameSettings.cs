using System;

namespace Crashverse.Core
{
    public interface IIntPrefs { bool TryGet(string key, out int v); void Set(string key, int v); }

    /// <summary>
    /// Checklist #45: persisted Music / SFX volume and graphics quality. Engine-free (tested in Tools/Tests).
    /// Haptics + Reduce shake / Reduce motion live in Feel.MotionSettings (PR #29) — not duplicated here.
    /// Volumes are stored as integer percent (0–100, 10 % steps in the UI). Quality: 0 = Low, 1 = Mid, 2 = High (Feel.QualityTier order).
    /// </summary>
    public class GameSettings
    {
        public const string KeyMusic = "cv_music_volume", KeySfx = "cv_sfx_volume", KeyQuality = "cv_graphics_quality";
        public const int DefaultMusic = 80, DefaultSfx = 100, DefaultQuality = 1, Step = 10;
        public static GameSettings Current = new GameSettings(null);
        readonly IIntPrefs _prefs;
        public event Action Changed;

        public GameSettings(IIntPrefs prefs) { _prefs = prefs; }

        int Get(string k, int d) => _prefs != null && _prefs.TryGet(k, out var v) ? v : d;
        public int MusicPercent => Clamp(Get(KeyMusic, DefaultMusic), 0, 100);
        public int SfxPercent => Clamp(Get(KeySfx, DefaultSfx), 0, 100);
        public float MusicVolume => MusicPercent / 100f;
        public float SfxVolume => SfxPercent / 100f;
        public int Quality => Clamp(Get(KeyQuality, DefaultQuality), 0, 2);

        public void SetMusicPercent(int p) { _prefs?.Set(KeyMusic, Clamp(p, 0, 100)); Changed?.Invoke(); }
        public void SetSfxPercent(int p) { _prefs?.Set(KeySfx, Clamp(p, 0, 100)); Changed?.Invoke(); }
        public void SetQuality(int q) { _prefs?.Set(KeyQuality, Clamp(q, 0, 2)); Changed?.Invoke(); }
        /// <summary>Low → Mid → High → Low.</summary>
        public int NextQuality() => (Quality + 1) % 3;
        public static string QualityName(int q) => q == 0 ? "Low" : q == 2 ? "High" : "Medium";

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
