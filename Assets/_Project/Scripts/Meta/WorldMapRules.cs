using Crashverse.Data;

namespace Crashverse.Meta
{
    public enum MapNodeKind { Normal, Hard, Boss, DailyBoss, NextSeason }
    public enum MapNodeState { Locked, Open, Cleared }

    /// <summary>
    /// Target UI §3 world map: a vertical, scrolling, winding path (level 1 at the bottom), stars under each node, locks, a skull on
    /// every 10th (hard) level, a Daily Boss node beside the path and the next season's locked island at the top.
    /// Positions are normalised: x in [0,1] across the map width, y in dp from the bottom of the path content.
    /// </summary>
    public static class WorldMapRules
    {
        public const float Spacing = 132f, BottomPad = 220f, TopPad = 420f, Amplitude = 0.30f;
        /// <summary>Top padding when there is no next-season island above the last level (FeatureFlags.Seasons2to4 off).</summary>
        public const float TopPadNoIsland = 150f;
        /// <summary>QA MED (V2-05 follow-up): the map header (wallet bar 56 dp + season ribbon / name row) and the dock own these bands;
        /// the scrolling path viewport sits between them, so a node can never render under the ★/coin/gem pills or the dock.</summary>
        public const float HeaderHeight = 128f, DockHeight = 76f;

        public static MapNodeKind KindFor(LevelEntry l) => HardLevelRules.IsHard(l) ? MapNodeKind.Hard : l.IsBoss ? MapNodeKind.Boss : MapNodeKind.Normal;

        public static MapNodeState StateFor(LevelEntry l, CampaignSave save) =>
            save.IsCleared(l.id) ? MapNodeState.Cleared : UnlockRules.IsLevelUnlocked(l, save) ? MapNodeState.Open : MapNodeState.Locked;

        /// <summary>Node i (0-based) on the serpentine path.</summary>
        public static float X(int i) => 0.5f + Amplitude * (float)System.Math.Sin(i * 1.05);
        public static float Y(int i) => BottomPad + i * Spacing;
        public static float ContentHeight(int count) => BottomPad + (count - 1) * Spacing + TopPad;
        public static float ContentHeight(int count, bool nextSeasonIsland) => BottomPad + (count - 1) * Spacing + (nextSeasonIsland ? TopPad : TopPadNoIsland);

        /// <summary>Daily Boss node sits beside the path near the player's frontier, on the side away from the path.</summary>
        public static float DailyBossX(int frontier) => X(frontier) > 0.5f ? 0.2f : 0.8f;
        public static float DailyBossY(int frontier) => Y(frontier); // level with the frontier, >= 0.3 map widths across: never overlaps a node

        /// <summary>Index of the first not-cleared, unlocked level (the "you are here" node), or the last level.</summary>
        public static int Frontier(ChapterData ch, CampaignSave save)
        {
            for (int i = 0; i < ch.levels.Length; i++) if (!save.IsCleared(ch.levels[i].id)) return i;
            return ch.levels.Length - 1;
        }
    }
}
