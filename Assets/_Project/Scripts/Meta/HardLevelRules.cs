using Crashverse.Data;

namespace Crashverse.Meta
{
    /// <summary>Target UI §7: every 10th level is a hard level (skull node on the map): tighter steps and a darker theme.</summary>
    public static class HardLevelRules
    {
        public const int Every = 10;
        /// <summary>Steps (shots) removed on a hard level, never below MinShots.</summary>
        public const int ShotPenalty = 1, MinShots = 3;

        public static bool IsHard(int levelNumber) => levelNumber > 0 && levelNumber % Every == 0;
        public static bool IsHard(LevelEntry l) => l != null && IsHard(l.number);
        public static int Shots(LevelEntry l) => l == null ? 0 : IsHard(l) ? System.Math.Max(System.Math.Min(MinShots, l.shots), l.shots - ShotPenalty) : l.shots;
        /// <summary>Dark theme multiplier for the arena light / UI tint (1 = normal).</summary>
        public static float ThemeDarken(LevelEntry l) => IsHard(l) ? 0.55f : 1f;
    }
}
