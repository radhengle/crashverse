namespace Crashverse.Meta
{
    /// <summary>
    /// Contract v0.2 §5 / SG-01 / SG-24: stars from the percentage of shots left, integer maths only.
    /// 3★ if shotsLeft*100 >= star3PctInt*shotsTotal; 2★ if shotsLeft*100 >= star2PctInt*shotsTotal; else 1★.
    /// Fail = 0. SG-03: optional cap when a power-up was used (L9 = 2★). Exact thresholds (3/5 = 60%) always pass.
    /// </summary>
    public static class StarRules
    {
        public const int DefaultStar3PctInt = 60;
        public const int DefaultStar2PctInt = 30;
        public const float DefaultStar3Pct = 0.60f;
        public const float DefaultStar2Pct = 0.30f;

        /// <summary>Level data stores 0.60-style fractions; convert once to an integer percent (0.6f → 60).</summary>
        public static int ToPctInt(float frac) => (int)System.Math.Round(frac * 100.0, System.MidpointRounding.AwayFromZero);

        public static int ComputeInt(bool won, int shotsLeft, int shotsTotal, int star3PctInt = DefaultStar3PctInt,
                                     int star2PctInt = DefaultStar2PctInt, int cap = 3)
        {
            if (!won || shotsTotal <= 0) return 0;
            if (shotsLeft < 0) shotsLeft = 0;
            long left100 = (long)shotsLeft * 100;
            int stars = left100 >= (long)star3PctInt * shotsTotal ? 3 : left100 >= (long)star2PctInt * shotsTotal ? 2 : 1;
            if (cap < 1) cap = 1;
            return stars > cap ? cap : stars;
        }

        public static int Compute(bool won, int shotsLeft, int shotsTotal, float star3Pct = DefaultStar3Pct,
                                  float star2Pct = DefaultStar2Pct, int cap = 3) =>
            ComputeInt(won, shotsLeft, shotsTotal, ToPctInt(star3Pct <= 0 ? DefaultStar3Pct : star3Pct),
                       ToPctInt(star2Pct <= 0 ? DefaultStar2Pct : star2Pct), cap);
    }
}
