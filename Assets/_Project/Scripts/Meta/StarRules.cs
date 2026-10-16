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

        /// <summary>
        /// BUG-09: exact level fraction, no rounding to whole percent. (decimal)float keeps the authored decimal value
        /// (0.625f → 0.625, 0.6f → 0.6), so thresholds compare exactly: shotsLeft >= frac × total.
        /// </summary>
        public static decimal Frac(float f) => (decimal)f;

        /// <summary>Smallest shotsLeft with shotsLeft >= pct × total (briefing star bands).</summary>
        public static int MinShotsLeft(int shotsTotal, float pct)
        {
            decimal need = Frac(pct) * shotsTotal;
            for (int k = 0; k <= shotsTotal; k++) if (k >= need) return k;
            return shotsTotal;
        }

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

        /// <summary>Level-data entry point: exact fractions (BUG-09). Same results as ComputeInt for whole-percent thresholds.</summary>
        public static int Compute(bool won, int shotsLeft, int shotsTotal, float star3Pct = DefaultStar3Pct,
                                  float star2Pct = DefaultStar2Pct, int cap = 3)
        {
            if (!won || shotsTotal <= 0) return 0;
            if (shotsLeft < 0) shotsLeft = 0;
            decimal s3 = Frac(star3Pct <= 0 ? DefaultStar3Pct : star3Pct) * shotsTotal;
            decimal s2 = Frac(star2Pct <= 0 ? DefaultStar2Pct : star2Pct) * shotsTotal;
            int stars = shotsLeft >= s3 ? 3 : shotsLeft >= s2 ? 2 : 1;
            if (cap < 1) cap = 1;
            return stars > cap ? cap : stars;
        }
    }
}
