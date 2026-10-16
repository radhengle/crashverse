namespace Crashverse.Sim
{
    /// <summary>
    /// Debris rules that affect the sim (and so scoring) and must not vary by device.
    /// Ranked runs use one tier-independent cap for colliding debris: RankedSimCap = 60. That is the Mid-tier budget, the
    /// design baseline (OQ-D1) already profiled as the default, so Low devices pay at most 35 extra bodies, only in ranked play.
    /// Tier caps (25/60/90) apply only to non-ranked play.
    /// </summary>
    public static class DebrisRules
    {
        public const int RankedSimCap = 60;
        public const long RngSalt = 0x44454252; // "DEBR"

        /// <summary>Colliding debris cap: fixed in ranked runs, the tier cap otherwise.</summary>
        public static int SimCap(bool ranked, int tierCap) => ranked ? RankedSimCap : tierCap;

        /// <summary>Debris RNG seed for a run. Same run seed gives the same debris stream on every start and every Retry.</summary>
        public static long RngSeed(long runSeed) => runSeed ^ RngSalt;
    }
}
