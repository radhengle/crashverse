using System;
using System.Collections.Generic;
using Crashverse.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Crashverse.Backend
{
    /// <summary>Shared (client + server) Titan scoring formula. Lives outside LocalBackendService so release builds keep it.</summary>
    public static class TitanScoring
    {
        public static int Score(int damageScore, int shotsLeft, bool killed, TitanTemplates t)
        {
            int d = Math.Max(0, Math.Min(t.maxDamageScore, damageScore));
            return d + (killed ? t.perShotLeftBonus * Math.Max(0, shotsLeft) : 0);
        }
    }

    /// <summary>Serialized as its name string on the wire ("Google", "Facebook", ...), never as an int.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AuthProvider { Google, Apple, Email, X, Facebook }

    /// <summary>POST /account/link (and /auth/reauth): provider token plus the optional PKCE / OIDC fields. Never a password.</summary>
    [Serializable] public class LinkCredential
    {
        public AuthProvider provider; public string token;
        public string codeVerifier; public string redirectUri; public string nonce; // optional (null = omitted)
    }

    /// <summary>Email login/register and provider sign-in. Tokens are kept in memory only.</summary>
    [Serializable] public class AuthResponse { public string accessToken; public string refreshToken; public AccountState account; }

    /// <summary>Server-provided feature flags (Security Review v1: X login is gated here, never by a client constant).</summary>
    [Serializable] public class FeatureFlags { public bool xLoginEnabled; public bool emailAuthEnabled = true; public bool accountDeletionEnabled = true; }

    /// <summary>Per-attempt token (Security Review v1 §4): issued before a run, bound to level + seed, single use.</summary>
    [Serializable] public class AttemptToken { public string token; public string levelId; public long seed; public long issuedUnixUtc; public long expiresUnixUtc; }

    /// <summary>One captured input. x/y are fixed-point ints in 1/1000 units, converted at capture (InputRecorder.ToFixed); tick = physics step.</summary>
    [Serializable] public class InputEvent { public int tick; public string type; public int x, y; }

    /// <summary>POST /score: the server replays inputs and recomputes; clientScoreBp is advisory only.</summary>
    [Serializable] public class ScoreSubmission { public string attemptToken; public InputEvent[] inputs; public int clientScoreBp; public string buildHash; }
    [Serializable] public class ScoreResponse { public bool accepted; public string status; public string message; public int serverScoreBp; }

    /// <summary>Records inputs as fixed-point at capture time so client and server agree bit-for-bit.</summary>
    public class InputRecorder
    {
        public const int Scale = 1000;
        public static int ToFixed(float v) => (int)Math.Round(v * Scale, MidpointRounding.AwayFromZero);
        public static float FromFixed(int v) => v / (float)Scale;
        readonly List<InputEvent> _events = new List<InputEvent>();
        public void Record(int tick, string type, float x, float y) => _events.Add(new InputEvent { tick = tick, type = type, x = ToFixed(x), y = ToFixed(y) });
        public InputEvent[] ToArray() => _events.ToArray();
        public void Clear() => _events.Clear();
        public int Count => _events.Count;
    }

    public static class PurchaseStatus { public const string Pending = "pending", Verified = "verified", Rejected = "rejected"; }
    [Serializable] public class PurchaseVerifyResponse { public string status; public Wallet wallet; public SeasonState season; }

    // ---- Account / privacy endpoints (times are Unix seconds UTC) ----
    [Serializable] public class ReauthResponse { public string reauthToken; public long expiresAt; }          // POST /auth/reauth
    [Serializable] public class DeleteAccountResponse { public string status; public long purgeAt; }          // POST /account/delete
    [Serializable] public class StatusResponse { public string status; }                                      // POST /account/delete/cancel
    [Serializable] public class KeepActiveResponse { public long lastActiveAt; }                               // POST /account/keep-active
    [Serializable] public class AgeGateResponse { public string band; public bool requiresParentalConsent; public string ageToken; }   // POST /v1/agegate
    /// <summary>POST /v1/agegate body. deviceInstallId: random UUID per install (never a hardware ID); the ageToken is bound to it, single use.</summary>
    [Serializable] public class AgeGateRequest { public int birthYear; public int birthMonth; public string country; public string deviceInstallId; }
    /// <summary>POST /v1/account/agegate {ageToken}: re-gate an older account that has no band (server answered 403 AGE_GATE_REQUIRED).</summary>
    [Serializable] public class AccountAgeGateRequest { public string ageToken; }
    [Serializable] public class ConsentRequestResponse { public string status; public string requestId; }     // POST /consent/parental/request
    [Serializable] public class ConsentStatusResponse { public string status; }                               // GET /consent/parental/status
    [Serializable] public class VisibilityResponse { public bool hideName; }                                  // POST /profile/visibility
    public static class ConsentStatus { public const string None = "none", Pending = "pending", Granted = "granted", Denied = "denied", Withdrawn = "withdrawn"; }

    /// <summary>Exactly the server's band strings. The client never derives consent from these.</summary>
    public static class AgeBands
    {
        public const string Under13 = "under13", Teen13to15 = "13to15", Teen16to17 = "16to17", Adult = "adult";
        public static bool IsMinor(string band) => band == Teen13to15 || band == Teen16to17;
    }

    /// <summary>Server routes (PR #1, backend/server-v0): everything under /v1.</summary>
    public static class ApiRoutes
    {
        public const string Prefix = "/v1";
        public const string Attempts = Prefix + "/attempts", Score = Prefix + "/score", Reauth = Prefix + "/auth/reauth",
            AccountDelete = Prefix + "/account/delete", AccountDeleteCancel = Prefix + "/account/delete/cancel", KeepActive = Prefix + "/account/keep-active",
            AgeGate = Prefix + "/agegate", ParentalConsentRequest = Prefix + "/consent/parental/request", ParentalConsentStatus = Prefix + "/consent/parental/status",
            ProfileVisibility = Prefix + "/profile/visibility", AccountAgeGate = Prefix + "/account/agegate", PurchaseVerify = Prefix + "/purchases/google/verify";
    }

    /// <summary>
    /// SERVER-ONLY. Purchases / premium pass entitlements are granted by the server after store receipt validation.
    /// No client implementation exists or may exist; the client only reads SeasonState.premium. (Security Review v1 §2.)
    /// </summary>
    public interface IServerEntitlements
    {
        void GrantPremiumPass(string playerId, string seasonId, string verifiedReceiptId);
        void GrantPurchase(string playerId, string sku, string verifiedReceiptId);
    }
}
