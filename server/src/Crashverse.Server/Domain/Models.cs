using System.Text.Json;
using Crashverse.Backend;
using Crashverse.Data;
using Crashverse.Server.Infra;

namespace Crashverse.Server.Domain;

/// <summary>All tunables. Defaults follow Backend_Plan_v1 + PM decisions + Security review (Backend_Plan_v1_Review.md).</summary>
public sealed class ServerOptions
{
    public CrashverseEnv Env = CrashverseEnv.Test;     // CRASHVERSE_ENV; missing/unknown = prod (Main)
    public string Flavor => Envs.Flavor(Env);
    public string ContentRoot;                       // Assets/_Project/Resources/Data
    public string PackageName = "com.crashverse.game";
    public string[] CertDigests = { "dev-cert-sha256" };
    public string SimVersion = "dev";
    public int PhysicsHz = 50;                       // P9 / D3
    /// <summary>"enforce" (default) or "logOnly" (closed testing: verdicts are logged and fed to riskScore, never downgrade runs).</summary>
    public string IntegrityMode = "enforce";
    public int IntegrityMaxAgeSeconds = 300;
    public bool XEnabled = false;                    // auth.x.enabled (PM decision 2)
    /// <summary>"gemDebt" (default, Security S6 adopted by PM) or "floorAtZero" (PM's original decision 3).</summary>
    public string RefundPolicy = RefundPolicies.GemDebt;
    public int RefundAbuseCount = 2; public int RefundAbuseWindowDays = 90;
    public int MinShotIntervalMs = 1500;
    public int TopN = 100; public double RandomSampleRate = 0.05; public int HighRiskScore = 50;
    public int AttemptPoolSize = 20; public int AttemptTokenTtlDays = 7;
    public int FirstClearDailyCap = 3;
    public int RunTokenTtlHours = 26; public int AccessTtlMinutes = 15; public int RefreshTtlDays = 60;
    public int TolPerShotAbs = 150; public double TolPerShotPct = 0.05; public double TolFinalPct = 0.03;
    public float TolRatio = 0.02f; public int TolCombo = 1;
    public int PasswordIterations = 210_000;
    public int GuestPerIpPerHour = 10; public int GuestPerDevicePerHour = 10; public int AuthPerMinute = 5; public int TimePerIpPerMinute = 120;
    public int AccountPerMinute = 240;
    public long MaxBodyBytes = 64 * 1024; public long MaxJournalBytes = 256 * 1024;
    public string ConsentAgesPath; public string EconomyPath;
    /// <summary>TRUST_X_FORWARDED_FOR (exact "true"): only when the service sits behind Google's front end.</summary>
    public bool TrustForwardedFor = false; public int TrustedProxyHops = 1;
    /// <summary>IAP_ENABLED (exact "true"). Economy v1 sells nothing for real money, so Play purchase verification is off.</summary>
    public bool IapEnabled = false;
    /// <summary>ENABLE_GEM_PACKS (exact "true"; anything else = false). Off: verify rejects the gem packs with GEM_PACKS_DISABLED and grants nothing.</summary>
    public bool EnableGemPacks = false;
    /// <summary>Real-money gem packs, product ids from client PR #10 store_catalog.json (requiresFlag enableGemPacks): Pouch $0.99, Chest $4.99.</summary>
    public static readonly string[] GemPackSkus = { "gems_small", "gems_large" };
    public int GuestInactivityWarnDays = 335, GuestInactivityDeleteDays = 365, DeletionGraceDays = 7, DeletionAlertDays = 25;
    public int InputLogRetentionDays = 90;
    /// <summary>NEEDS LEGAL REVIEW: how long pseudonymised purchase rows are kept for tax (TAX_RETENTION_DAYS). Placeholder 8 years.</summary>
    public int TaxRetentionDays = 8 * 365;
    public byte[] DeletionPepper;      // N2: loaded from ISecretProvider ("deletion-pepper") by the context; never generated in prod                   // null = server/config/consent_ages.json (or CONSENT_AGES_PATH)
    /// <summary>ALLOW_CLIENT_RUN_SUMMARY: accept the client's won/ratio/shotsLeft/combo claims. Development only; elsewhere forced false
    /// and campaign results wait for the server replay.</summary>
    public bool AllowClientRunSummary = false;
    /// <summary>RANKED_PUBLIC_BOARD_ENABLED (default false). Even when true, the public Daily Titan board fails closed unless the replay
    /// worker is configured AND every top-N entry has been replayed (B3).</summary>
    public bool RankedPublicBoardEnabled = false;
    public string AdminKey = null;

    /// <summary>PR #1 decisions. ALLOW_CLIENT_RUN_SUMMARY is honoured only when ASPNETCORE_ENVIRONMENT (or DOTNET_ENVIRONMENT) is
    /// Development, where it defaults to true; in any other environment it is forced false. RANKED_PUBLIC_BOARD_ENABLED must be exactly "true".</summary>
    public static void ApplyEnvFlags(ServerOptions o, Func<string, string> env)
    {
        var name = env("ASPNETCORE_ENVIRONMENT") ?? env("DOTNET_ENVIRONMENT") ?? "Production";
        bool dev = string.Equals(name, "Development", StringComparison.OrdinalIgnoreCase);
        o.AllowClientRunSummary = dev && (env("ALLOW_CLIENT_RUN_SUMMARY") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        o.RankedPublicBoardEnabled = env("RANKED_PUBLIC_BOARD_ENABLED") == "true"; // strict: anything else (1, TRUE, " true") is off
        o.IapEnabled = env("IAP_ENABLED") == "true";
        o.EnableGemPacks = env("ENABLE_GEM_PACKS") == "true";
        o.TrustForwardedFor = env("TRUST_X_FORWARDED_FOR") == "true";
        if (int.TryParse(env("TRUSTED_PROXY_HOPS"), out var hops) && hops is >= 1 and <= 5) o.TrustedProxyHops = hops;
        o.PurchaseBindingSecretVersion = env("SECRET_VERSION_PURCHASE_BINDING");
        o.PurchaseBindingPreviousSecretVersion = env("SECRET_VERSION_PURCHASE_BINDING_PREVIOUS") is { Length: > 0 } pv ? pv : null;
        o.DeletionPepperSecretVersion = env("SECRET_VERSION_DELETION_PEPPER");
        o.PreviousAcceptUntilRaw = env("SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL") is { Length: > 0 } au ? au : null;
        o.PreviousAcceptUntilUnixMs = ParseUtcDate(o.PreviousAcceptUntilRaw);
        if (int.TryParse(env("SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS"), out var md) && md > 0) o.PreviousAcceptUntilMaxDays = md;
        if (int.TryParse(env("TAX_RETENTION_DAYS"), out var tax) && tax > 0) o.TaxRetentionDays = tax;
        // CRASHVERSE_ENV gates the run summary too: never honoured in prod, even in "Development" ASP.NET environment.
        if (o.Env == CrashverseEnv.Prod) o.AllowClientRunSummary = false;
    }                   // break-glass admin header; null = admin endpoints disabled
    /// <summary>N2: purchase-binding secret (obfuscatedAccountId HMAC key), loaded from ISecretProvider "purchase-binding-pepper": Secret Manager in prod,
    /// a dev-generated value only in dev/test. Must be stable across restarts and instances in prod.</summary>
    public byte[] ObfuscationPepper; public string ObfuscationPepperVersion = "p1";
    /// <summary>Low-3: previous purchase-binding secret, still accepted for existing bindings during rotation (new bindings use the current one).</summary>
    public byte[] PreviousObfuscationPepper; public string PreviousObfuscationPepperVersion;
    /// <summary>Low-3: pinned Secret Manager versions (SECRET_VERSION_PURCHASE_BINDING, _PURCHASE_BINDING_PREVIOUS, _DELETION_PEPPER).</summary>
    public string PurchaseBindingSecretVersion, PurchaseBindingPreviousSecretVersion, DeletionPepperSecretVersion;
    /// <summary>SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL: ISO-8601 UTC date or date-time ("2026-12-31" or "2026-12-31T00:00:00Z")
    /// after which the previous purchase-binding secret is no longer accepted. Tie it to the Play refund window: set it to the rotation
    /// date plus the longest period in which Play can still send a refund/void or a late verify for a purchase bound with the old secret
    /// (refunds can arrive months after purchase), then drop _PREVIOUS. Prod refuses to start if _PREVIOUS is set without it or after it.
    /// A date-only value means the end of that UTC day.</summary>
    public string PreviousAcceptUntilRaw; public long? PreviousAcceptUntilUnixMs;
    /// <summary>SECRET_VERSION_PURCHASE_BINDING_PREVIOUS_ACCEPT_UNTIL_MAX_DAYS (default 400): prod refuses to start if ACCEPT_UNTIL is further
    /// than this from now. Keep it at the Play refund window plus a margin, so an old secret can't be kept alive indefinitely.</summary>
    public int PreviousAcceptUntilMaxDays = 400;
    public static long? ParseUtcDate(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var st = System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal;
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$") &&
            DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, st, out var d))
            return new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).AddDays(1).ToUnixTimeMilliseconds() - 1;
        if ((s.EndsWith("Z") || s.EndsWith("+00:00")) && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, st, out var t))
            return t.ToUnixTimeMilliseconds();
        return null; // not ISO UTC: treated as missing
    }
}

public static class RefundPolicies { public const string GemDebt = "gemDebt"; public const string FloorAtZero = "floorAtZero"; }

public sealed class ApiError : Exception
{
    public int Status; public string Code; public bool Retryable; public object Extra;
    public ApiError(int status, string code, string message, bool retryable = false, object extra = null) : base(message)
    { Status = status; Code = code; Retryable = retryable; Extra = extra; }
}

// ---------------- wire extensions (P1, P3, P6, P7, P8, D7) ----------------
/// <summary>Agreed interface: one input event, fixed point at 1/1000 units. tick = physics tick (PhysicsHz).
/// type: aim (x = yaw, y = pitch, milli-degrees) | power (x = power 0..1000) | powerup (x = slot) | fire (x = yaw, y = pitch) | settle.</summary>
// InputEvent is Developer's Crashverse.Backend.InputEvent {tick, type, x, y} (linked ServerContracts.cs), used as-is.
/// <summary>Agreed interface: SubmitScore {attemptToken, inputs[], clientScoreBp, buildHash}.</summary>
public class SubmitScore { public string attemptToken; public List<InputEvent> inputs = new(); public int clientScoreBp; public string buildHash; }
public sealed class AccountStateEx : AccountState { public List<string> providers = new(); public string obfuscatedAccountId; public string friendCode; public string status; public long purgeAt; public bool hideName; }
public sealed class WalletEx : Wallet { public int gems; public bool gemSpendLocked; }
public sealed class RankedTicketEx : RankedTicket { public long seed; public string configHash; public int physicsHz; public string simVersion; public bool practice; public string practiceReason; }
public sealed class RankedCheckpointEx : RankedCheckpoint { public List<InputEvent> inputs; }
public sealed class RankedJournalEx { public string token; public bool final; public string simVersion; public List<RankedCheckpointEx> checkpoints = new(); }
public sealed class CampaignRunReportEx : CampaignRunReport { public string attemptToken; public string simVersion; public List<InputEvent> inputs = new(); public int clientScoreBp; public string buildHash; }
/// <summary>Also satisfies the agreed ScoreResponse {accepted, status, message, serverScoreBp}.</summary>
public sealed class CampaignSubmitResponseEx : CampaignSubmitResponse { public int provisionalBest; public string validation; public bool rewardDeferred; public string status; public int serverScoreBp; }
public sealed class LeaderboardEntryEx : LeaderboardEntry { public long submittedUnixMs; public bool provisional; }
/// <summary>Same fields as BackendModels.LeaderboardPage, typed with the extended entry so the extra fields serialise.</summary>
public sealed class LeaderboardPageEx
{
    public bool publicBoard = true; public string publicBoardReason;
    public BoardType board; public string periodKey; public bool closed; public List<LeaderboardEntryEx> top = new();
    public LeaderboardEntryEx me; public int totalEntries; public bool meIsGuestHidden; public long fetchedUnixUtc;
}
public sealed class DailyTitanConfigEx : DailyTitanConfig { public string configHash; }
public sealed class ListResponse<T> { public List<T> items = new(); public ListResponse(IEnumerable<T> i) { items = i.ToList(); } public ListResponse() { } }
public sealed class AuthResponse { public string accessToken; public string refreshToken; public AccountStateEx account; }

// ---------------- persisted documents ----------------
public sealed class AccountDoc
{
    public string playerId; public string displayName; public bool isGuest = true; public List<string> providers = new();
    public string primaryProvider = "guest"; public long createdUnixMs; public int riskScore; public bool refundAbuse;
    public List<long> refundTimesUnixMs = new(); public string status = "active"; public long purgeAtUnixMs; public long lastActiveUnixMs; public bool hideName; public bool minorRestricted; public long deletionWarningAtUnixMs;
    public AgeGateDoc ageGate; public string friendCode; public string deviceInstallId;
}
public sealed class AgeGateDoc { [System.Text.Json.Serialization.JsonIgnore] public string jti; [System.Text.Json.Serialization.JsonIgnore] public long exp; public string band; public string country; public bool requiresParentalConsent; public long ts; }
public sealed class ConsentDoc { public string requestId; public string playerId; public string parentEmailHash; public string status = "none"; public long requestedUnixMs; public long decidedUnixMs; }
public sealed class IdentityDoc { public string key; public string playerId; public long linkedUnixMs; public long expiresUnixMs; } // Low-2: expiresUnixMs = ageTokenUses expiry (token exp); swept by Retention()
public sealed class EmailCredDoc { public string email; public string playerId; public string salt; public string hash; public int iterations; public bool verified; public string verifyCodeHash; public string resetCodeHash; public long resetExpUnixMs; }
public sealed class SessionDoc { public string familyId; public string playerId; public string currentHash; public bool revoked; public long expUnixMs; }
public sealed class WalletDoc
{
    public string playerId; public int coins; public int shards; public int gems; public int extraCharges;
    public List<string> cosmetics = new(); public List<string> powerUps = new();
    public Dictionary<string, string> cosmeticSource = new(); // id -> cash:<purchase> | gems | coins | reward | premium_tier:<season>
}
public sealed class LedgerEntry
{
    public string id; public string playerId; public long ts; public string reason; public string refId; public string note;
    public Dictionary<string, int> delta = new(); public Dictionary<string, int> balanceAfter = new(); public List<string> itemsAdded = new(), itemsRemoved = new();
}
public sealed class AuditEntry { public string id; public long ts; public string actor; public string action; public string playerId; public string detail; }
public sealed class PurchaseDoc
{
    public string tokenHash; public string playerId; public string productId; public string orderId; public string state; // granted|voided
    public long grantedUnixMs; public long voidedUnixMs; public int gems; public List<string> cosmetics = new(); public string entitlementId; public long retainUntilUnixMs;
}
public sealed class EntitlementDoc { public string id; public string playerId; public string type; public string seasonId; public string state; public string purchaseTokenHash; }
public sealed class InboxDoc { public string id; public string playerId; public string source; public Reward reward; public long expiresUnixUtc; public bool claimed; }
public sealed class DailyRewardDoc { public int streakIndex; public string lastClaimDayKey = ""; }
public sealed class RankedStatusDoc { public string dayKey; public bool attemptUsed; public bool submitted; public int score; public string status = "available"; }
public sealed class CheckpointRec { public int shotIndex; public int damageScore; public int shotsLeft; public bool killed; public List<InputEvent> inputs; public long serverUnixMs; public bool offline; }
public sealed class RunDoc
{
    public string runId; public string playerId; public string mode; public string key; public string levelVersion; public long seed;
    public string configHash; public string simVersion; public int shotBudget; public long issuedUnixMs; public bool practice; public string practiceReason;
    public int lastShotIndex; public int lastDamage; public int lastShotsLeft; public bool lastKilled; public long lastShotServerUnixMs;
    public List<CheckpointRec> checkpoints = new(); public bool finalized; public int clientScore; public int finalScore;
    public string replayState = "none"; // none|queued|passed|failed
    public ReplayResult_ replay; public string boardKey; public List<string> boardKeys = new();
    // campaign/event specifics
    public CampaignRunReportEx campaign; public int eventStars; public List<string> provisionalLedgerIds = new(); public bool firstClearDeferred; public bool serverSummary; public bool inputsPurged;
}
public sealed class ReplayResult_ { public int damageScore; public bool killed; public float destroyedRatio; public int bestCombo; public bool mastery; public bool won; public int shotsLeft; public int score; }
public sealed class AttemptDoc { public string attemptId; public string playerId; public string mode; public string levelId; public string levelVersion; public long seed; public long expUnixMs; public bool used; }
/// <summary>BE-01: best/runId are the last *validated* best (the only value totals and boards use). A pending run's score lives in
/// provisionalBest/provisionalRunId and never overwrites the validated best; replay promotes or drops it.</summary>
public sealed class LevelBestDoc { public string levelId; public int best; public string runId; public bool validated; public int stars; public int provisionalBest; public string provisionalRunId; }
public sealed class CampaignProgressDoc { public List<string> cleared = new(); public List<string> rewarded = new(); public Dictionary<string, int> firstClearsByDay = new(); public List<string> processedReports = new(); public Dictionary<string, string> reportResponses = new(); }
public sealed class SeasonProgressDoc { public string seasonId; public int xp; public List<int> claimedFree = new(); public List<int> claimedPremium = new(); public Dictionary<string, int> eventBest = new(); public List<string> titanClearDays = new(); public bool endProcessed; public int titanXp; }
public sealed class BoardEntryDoc
{
    public string boardKey; public string playerId; public int score; public long reachedUnixMs; public bool guestAtSubmit;
    public List<string> pendingRuns = new(); public bool shadow; public bool hasValidated;
}
public sealed class FriendDoc { public string playerId; public List<string> added = new(); }
public sealed class TitanDayDoc { public string dayKey; public DailyTitanConfig overrideConfig; public string overrideBy; public bool bonusProcessed; }

/// <summary>/v1/config/flags: the agreed FeatureFlags plus enableGemPacks (client PR #10 catalog flag).</summary>
public sealed class FeatureFlagsEx : Crashverse.Backend.FeatureFlags { public bool enableGemPacks; }
