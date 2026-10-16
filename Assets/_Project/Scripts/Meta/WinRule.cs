namespace Crashverse.Meta
{
    /// <summary>
    /// BUG-17: the win rule is exactly ">= threshold" (85% = 8500 bp), compared in integer basis points with no tolerance.
    /// (decimal)float keeps the float's 7 significant digits, which removes binary noise: a real 85% that arrives as
    /// 0.84999996f becomes 0.85 → 8500. Flooring means 84.99% (8499) never passes.
    /// </summary>
    public static class WinRule
    {
        public static int ToBp(float ratio)
        {
            if (float.IsNaN(ratio) || ratio <= 0f) return 0;
            if (ratio >= 1f) return 10000;
            return (int)decimal.Floor((decimal)ratio * 10000m);
        }

        public static bool Meets(float destroyedRatio, float winThreshold) => ToBp(destroyedRatio) >= ToBp(winThreshold);
    }
}
