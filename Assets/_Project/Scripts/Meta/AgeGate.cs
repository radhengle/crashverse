using System;

namespace Crashverse.Meta
{
    /// <summary>
    /// B6 age gate (first launch). PLACEHOLDER RULES — TODO(Security Admin): replace thresholds/behaviour when requirements arrive.
    /// Only the resulting band is stored (no birth date). Neutral entry: the player picks a year; no default that implies "adult".
    /// </summary>
    public static class AgeGate
    {
        public enum Band { Unknown = 0, Under13 = 1, Teen = 2, Adult = 3 }
        public const int MinAgeFull = 13;   // TODO(Security Admin): confirm (COPPA 13 / GDPR-K up to 16 per country)
        public const int AdultAge = 18;     // TODO(Security Admin)
        public const int Version = 1;       // bump to re-ask everyone when rules change

        /// <summary>Conservative: assumes the birthday hasn't happened yet this year.</summary>
        public static Band Classify(int birthYear, int currentYear)
        {
            if (birthYear <= 1900 || birthYear > currentYear) return Band.Unknown;
            int age = currentYear - birthYear - 1;
            return age < MinAgeFull ? Band.Under13 : age < AdultAge ? Band.Teen : Band.Adult;
        }

        /// <summary>Placeholder restrictions for Under13: no account linking/email, no public leaderboards names, no store. TODO(Security Admin).</summary>
        public static bool CanLinkAccount(Band b) => b == Band.Teen || b == Band.Adult;
        public static bool CanUsePublicLeaderboards(Band b) => b == Band.Teen || b == Band.Adult;
        public static bool CanPurchase(Band b) => b == Band.Adult; // TODO(Security Admin): parental consent flow
    }
}
