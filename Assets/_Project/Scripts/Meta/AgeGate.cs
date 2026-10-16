using System;

namespace Crashverse.Meta
{
    /// <summary>
    /// Neutral age gate (Deletion_AgeGate_Requirements §E). The game is 13+. Year + month of birth; only the band is kept
    /// (never the birth date). Under-13 → blocked locally, no account, no server call. [[LEGAL]] items stay TODO.
    /// </summary>
    public static class AgeGate
    {
        public enum Band { Unknown = 0, Under13 = 1, Minor13to17 = 2, Adult = 3 }
        public const int MinAge = 13, AdultAge = 18;
        public const int Version = 2; // v2 = year+month gate (§E1); re-asks installs that answered v1

        /// <summary>Completed years, conservative: a birthday in the current month is treated as not yet reached.</summary>
        public static int AgeYears(int birthYear, int birthMonth, int nowYear, int nowMonth)
        {
            int months = (nowYear * 12 + nowMonth) - (birthYear * 12 + birthMonth);
            return months <= 0 ? 0 : (months - 1) / 12;
        }

        public static Band Classify(int birthYear, int birthMonth, int nowYear, int nowMonth)
        {
            if (birthMonth < 1 || birthMonth > 12 || birthYear < nowYear - 120 || (birthYear * 12 + birthMonth) > (nowYear * 12 + nowMonth)) return Band.Unknown;
            int age = AgeYears(birthYear, birthMonth, nowYear, nowMonth);
            return age < MinAge ? Band.Under13 : age < AdultAge ? Band.Minor13to17 : Band.Adult;
        }

        public static string ToServer(Band b) => b == Band.Adult ? "adult" : b == Band.Minor13to17 ? "13to17" : "";
        public static Band FromServer(string s) => s == "adult" ? Band.Adult : s == "13to17" ? Band.Minor13to17 : Band.Unknown;

        // §E5 client mirrors (the server enforces). Under-18 never gets tracking/ads regardless of consent.
        public static bool CanSocialSignIn(Band b, bool consentSocial) => b == Band.Adult || (b == Band.Minor13to17 && consentSocial);
        public static bool CanCustomName(Band b, bool consentName) => b == Band.Adult || (b == Band.Minor13to17 && consentName);
        /// <summary>E5/E6: minors need purchase consent (all markets until consent; plus 18+ markets [[LEGAL]]).</summary>
        public static bool CanPurchase(Band b, bool consentPurchases) => b == Band.Adult || (b == Band.Minor13to17 && consentPurchases);
        public static bool AllowTracking(Band b) => false; // analytics/ads are off for everyone today; never for < 18 (DPDP §9)
        /// <summary>C3: guest "link an account" prompt — never to minors without consent.</summary>
        public static bool ShowLinkPrompt(Band b, bool linked, bool consentSocial) => !linked && CanSocialSignIn(b, consentSocial);
    }
}
