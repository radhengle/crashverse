using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Data
{
    /// <summary>Loads bundled JSON from Resources/Data. No per-level code: everything comes from data.</summary>
    public class ContentDatabase
    {
        public ChapterData Chapter1 { get; private set; }
        public PowerUpCatalog PowerUps { get; private set; }
        public TitanTemplates Titan { get; private set; }
        public DailyRewardsData DailyRewards { get; private set; }
        public SeasonData Season { get; private set; }
        readonly Dictionary<string, LayoutData> _layouts = new Dictionary<string, LayoutData>();

        public static string LoadText(string resourcePath)
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null) { Debug.LogError("[Content] missing " + resourcePath); return null; }
            return ta.text;
        }

        public static T LoadJson<T>(string resourcePath) where T : class
        {
            var t = LoadText(resourcePath);
            return t == null ? null : JsonUtility.FromJson<T>(t);
        }

        public static ContentDatabase LoadBundled()
        {
            return new ContentDatabase
            {
                Chapter1 = LoadJson<ChapterData>("Data/chapter1"),
                PowerUps = LoadJson<PowerUpCatalog>("Data/powerups"),
                Titan = LoadJson<TitanTemplates>("Data/titan_templates"),
                DailyRewards = LoadJson<DailyRewardsData>("Data/daily_rewards"),
                Season = LoadJson<SeasonData>("Data/season_s1"),
            };
        }

        public LayoutData GetLayout(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_layouts.TryGetValue(id, out var l)) return l;
            l = LoadJson<LayoutData>("Data/Layouts/" + id);
            if (l != null) _layouts[id] = l;
            return l;
        }

        public LevelEntry GetLevel(string id)
        {
            foreach (var l in Chapter1.levels) if (l.id == id) return l;
            if (Season != null)
            {
                foreach (var w in Season.weeks) foreach (var l in w.levels) if (l.id == id) return l;
                if (Season.finale != null && Season.finale.id == id) return Season.finale;
            }
            return null;
        }

        public PowerUpData GetPowerUp(string id)
        {
            foreach (var p in PowerUps.powerups) if (p.id == id) return p;
            return null;
        }
    }
}
