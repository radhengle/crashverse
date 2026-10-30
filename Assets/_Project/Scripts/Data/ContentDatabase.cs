using System.Collections.Generic;
using UnityEngine;

namespace Crashverse.Data
{
    /// <summary>Loads bundled JSON from Resources/Data. No per-level code: everything comes from data.</summary>
    public class ContentDatabase
    {
        /// <summary>The campaign: chapter1.json plus Seasons 2–4 (delivered chapter2..4.json when present, else placeholder levels).</summary>
        public ChapterData Chapter1 { get; private set; }
        /// <summary>chapter1.json exactly as authored (Season 1 only).</summary>
        public ChapterData Chapter1Authored { get; private set; }
        public PowerUpCatalog PowerUps { get; private set; }
        public TitanTemplates Titan { get; private set; }
        public DailyRewardsData DailyRewards { get; private set; }
        public SeasonData Season { get; private set; }
        public StoreCatalog Store { get; private set; }
        public EconomyData Economy { get; private set; }
        public WreckerTuning Wrecker { get; private set; }
        /// <summary>integration/v2 local meta: rewards, Daily Boss pool, the 4 seasons, achievements, avatars/badges.</summary>
        public MetaV2Data Meta { get; private set; }
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

        /// <summary>Like LoadText but silent when the resource doesn't exist (optional content).</summary>
        public static string TryLoadText(string resourcePath) { var ta = Resources.Load<TextAsset>(resourcePath); return ta == null ? null : ta.text; }

        public static ContentDatabase LoadBundled() => LoadBundled(Crashverse.Meta.FeatureFlags.Seasons2to4);

        /// <summary>allSeasons: Seasons 2–4 merged in (delivered chapters + placeholders). The game passes FeatureFlags.Seasons2to4 (off).</summary>
        public static ContentDatabase LoadBundled(bool allSeasons)
        {
            var db = LoadCore();
            db.Chapter1Authored = db.Chapter1;
            var delivered = new List<ChapterData>();
            for (int n = 2; n <= 4; n++)
            {
                var t = TryLoadText("Data/chapter" + n);
                if (t == null) continue;
                try { var c = JsonUtility.FromJson<ChapterData>(t); if (c != null) delivered.Add(c); }
                catch (System.Exception e) { Debug.LogWarning("[Content] chapter" + n + " ignored: " + e.Message); }
            }
            if (allSeasons)
                db.Chapter1 = Crashverse.Meta.SeasonContent.Merge(db.Chapter1, db.Meta, delivered, id => db.TryGetLayout(id) != null, l => db._layouts[l.id] = l);
            else HideUnshippedSeasons(db); // user scope 2026-10-30: Chapter 1 levels 1-10 + Daily Boss only
            return db;
        }

        /// <summary>FeatureFlags.Seasons2to4 off: only Chapter 1 levels 1–10 and Season 1 exist for the player (no placeholders, no
        /// "coming soon" seasons). Data files stay in the build; nothing is deleted.</summary>
        public static void HideUnshippedSeasons(ContentDatabase db)
        {
            if (db?.Chapter1?.levels != null)
                db.Chapter1 = new ChapterData { id = db.Chapter1.id, name = db.Chapter1.name, nextRegion = db.Chapter1.nextRegion, islands = db.Chapter1.islands,
                    levels = System.Array.FindAll(db.Chapter1.levels, l => l != null && l.number >= 1 && l.number <= Crashverse.Meta.FeatureFlags.ShippedLevelCount) };
            if (db?.Meta?.seasons != null) db.Meta.seasons = System.Array.FindAll(db.Meta.seasons, s => s != null && s.number <= 1);
        }

        static ContentDatabase LoadCore()
        {
            return new ContentDatabase
            {
                Chapter1 = LoadJson<ChapterData>("Data/chapter1"),
                PowerUps = LoadJson<PowerUpCatalog>("Data/powerups"),
                Titan = LoadJson<TitanTemplates>("Data/titan_templates"),
                DailyRewards = LoadJson<DailyRewardsData>("Data/daily_rewards"),
                Season = LoadJson<SeasonData>("Data/season_s1"),
                Store = LoadJson<StoreCatalog>("Data/store_catalog"),
                Economy = LoadJson<EconomyData>("Data/economy") ?? new EconomyData(),
                Wrecker = LoadJson<WreckerTuning>("Data/wrecker") ?? new WreckerTuning(),
                Meta = LoadJson<MetaV2Data>("Data/meta_v2") ?? new MetaV2Data(),
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

        LayoutData TryGetLayout(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_layouts.TryGetValue(id, out var l)) return l;
            var t = TryLoadText("Data/Layouts/" + id); if (t == null) return null;
            l = JsonUtility.FromJson<LayoutData>(t); if (l != null) _layouts[id] = l;
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
