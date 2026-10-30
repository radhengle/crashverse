namespace Crashverse.Meta
{
    /// <summary>
    /// User direction (2026-10-30, via PM): "no future developments... first complete what we have: level 1 to 10 and boss".
    /// Unfinished player-facing features are OFF: no button, no tile, no "coming soon" stub anywhere. The code stays (nothing deleted);
    /// flip a flag to bring a feature back once it is finished. The shipped scope is Chapter 1 levels 1–10 plus the Daily Boss.
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>Seasons 2–4 (delivered chapters and the procedural "(preview)" placeholders), the Seasons screen and the next-season island.</summary>
        public static bool Seasons2to4 = false;
        public static bool SeasonPass = false;
        public static bool Leaderboard = false;
        public static bool Achievements = false;
        public static bool DailyLogin = false;
        /// <summary>Daily Titan needs the (offline) server.</summary>
        public static bool OnlineTitan = false;
        /// <summary>The store has nothing to sell offline (gem packs are hidden; gems are earned only).</summary>
        public static bool Store = false;

        /// <summary>Levels on the map while Seasons2to4 is off: Chapter 1, levels 1–10.</summary>
        public const int ShippedLevelCount = 10;

        /// <summary>True when any of the hidden meta features is on (the map's "More" list only exists then).</summary>
        public static bool AnyExtra => SeasonPass || Leaderboard || DailyLogin || OnlineTitan || Store;
    }
}
