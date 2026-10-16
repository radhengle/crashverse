using System;

namespace Crashverse.Meta
{
    /// <summary>
    /// Neutral age gate (Deletion_AgeGate_Requirements §E). The game is 13+. Year + month of birth; only the band is kept
    /// (never the birth date). Under-13 → blocked locally, no account, no server call. [[LEGAL]] items stay TODO.
    /// </summary>
    public static class AgeGate
    {
        /// <summary>Server bands (PM/Backend 16 Oct): under13 | 13to15 | 16to17 | adult. Consent rules are NOT computed here.</summary>
        public enum Band { Unknown = 0, Under13 = 1, Age13to15 = 2, Age16to17 = 3, Adult = 4 }
        public const int MinAge = 13;
        public const int Version = 3; // v3 = 4-band server set; re-asks installs that answered v2

        /// <summary>Completed years, conservative: a birthday in the current month is treated as not yet reached.</summary>
        public static int AgeYears(int birthYear, int birthMonth, int nowYear, int nowMonth)
        {
            int months = (nowYear * 12 + nowMonth) - (birthYear * 12 + birthMonth);
            return months <= 0 ? 0 : (months - 1) / 12;
        }

        /// <summary>Local use is limited to the E2 under-13 block (no account, no server call). The authoritative band comes from /v1/agegate.</summary>
        public static Band Classify(int birthYear, int birthMonth, int nowYear, int nowMonth)
        {
            if (birthMonth < 1 || birthMonth > 12 || birthYear < nowYear - 120 || (birthYear * 12 + birthMonth) > (nowYear * 12 + nowMonth)) return Band.Unknown;
            int age = AgeYears(birthYear, birthMonth, nowYear, nowMonth);
            return age < 13 ? Band.Under13 : age < 16 ? Band.Age13to15 : age < 18 ? Band.Age16to17 : Band.Adult;
        }

        public static string ToServer(Band b) => b == Band.Under13 ? "under13" : b == Band.Age13to15 ? "13to15" : b == Band.Age16to17 ? "16to17" : b == Band.Adult ? "adult" : "";
        public static Band FromServer(string s) => s == "under13" ? Band.Under13 : s == "13to15" ? Band.Age13to15 : s == "16to17" ? Band.Age16to17 : s == "adult" ? Band.Adult : Band.Unknown;

        /// <summary>Acts only on the server's answer: if /v1/agegate said requiresParentalConsent, the feature waits for the
        /// server-granted consent flag. No age→consent mapping lives in the client (per-market server config).</summary>
        public static bool Allowed(bool requiresParentalConsent, bool serverConsentFlag) => !requiresParentalConsent || serverConsentFlag;
        public static bool AllowTracking() => false; // analytics/ads are off for everyone today
        /// <summary>C3: guest "link an account" prompt, only where the server allows social sign-in.</summary>
        public static bool ShowLinkPrompt(bool linked, bool requiresParentalConsent, bool consentSocial) => !linked && Allowed(requiresParentalConsent, consentSocial);
    }
}
