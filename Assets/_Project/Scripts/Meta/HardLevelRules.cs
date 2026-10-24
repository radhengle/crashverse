using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>Target UI §7: hard levels (skull node on the map): tighter steps and a darker theme.
    /// GD ruling (2026-10-24): only levels TAGGED hard in data (LevelEntry.hard) are hard; the "every 10th" index is a design
    /// convention, never applied automatically (c1_l10 Tidewarden keeps its authored 8 shots and 0.375/0.25 bands).</summary>
    public static class HardLevelRules
    {
        public const int Every = 10;
        /// <summary>Steps (shots) removed on a hard level, never below MinShots.</summary>
        public const int ShotPenalty = 1, MinShots = 3;

        /// <summary>Design convention only (where hard levels are normally placed); not a rule. Use IsHard(LevelEntry).</summary>
        public static bool IsHardSlot(int levelNumber) => levelNumber > 0 && levelNumber % Every == 0;
        public static bool IsHard(LevelEntry l) => l != null && l.hard;
        public static int Shots(LevelEntry l) => l == null ? 0 : IsHard(l) ? System.Math.Max(System.Math.Min(MinShots, l.shots), l.shots - ShotPenalty) : l.shots;
        /// <summary>Dark theme multiplier for the arena light / UI tint (1 = normal).</summary>
        public static float ThemeDarken(LevelEntry l) => IsHard(l) ? 0.55f : 1f;
    }
}
