using System;
using System.Collections.Generic;
using Crashverse.Data;

namespace Crashverse.Backend
{
    public enum BoardType { DailyTitan = 0, WeeklyEvent = 1, Season = 2, Campaign = 3 }
    public enum BoardPeriod { Current = 0, Previous = 1 }
    public enum SubmitStatus { Accepted, RejectedInvalid, RejectedDuplicate, RefundedAsPractice, QueuedOffline }

    [Serializable] public class Wallet { public int coins; public int shards; public int extraCharges; public List<string> cosmetics = new List<string>(); public List<string> powerUps = new List<string>(); }

    [Serializable] public class InboxItem { public string id; public string source; public Reward reward; public long expiresUnixUtc; public bool claimed; }

    [Serializable] public class RankedTicket { public string token; public string dayKey; public long issuedUnixUtc; public int shotBudget; }

    /// <summary>
    /// SEC-01: one settled shot of a ranked run. The client fills the numbers; only the SERVER fills <see cref="receipt"/>
    /// (HMAC-SHA256 with a per-run key the client never sees). Offline shots have an empty receipt.
    /// </summary>
    [Serializable]
    public class RankedCheckpoint
    {
        public string token; public int shotIndex; public int damageScore; public int shotsLeft; public bool killed;
        public string receipt;
    }

    /// <summary>Client-side journal of a ranked run. Sent to the server verbatim; the client never signs or re-signs it.</summary>
    [Serializable]
    public class RankedJournal
    {
        public string token; public bool final;
        public List<RankedCheckpoint> checkpoints = new List<RankedCheckpoint>();
        public RankedCheckpoint Last() => checkpoints.Count > 0 ? checkpoints[checkpoints.Count - 1] : null;
    }

    /// <summary>
    /// BUG-13: what the client reports for one campaign win. The server re-validates it against content and its own
    /// progress, recomputes the score and keeps the per-level best; the client never sends a score or a total.
    /// </summary>
    [Serializable]
    public class CampaignRunReport
    {
        public string reportId; // idempotency key for queued retries (BUG-14)
        public string levelId; public bool won; public float destroyedRatio; public int shotsLeft; public int bestCombo; public bool mastery;
    }

    [Serializable]
    public class CampaignSubmitResponse { public bool accepted; public string message; public int levelBest; public int total; public bool firstClear; }

    [Serializable] public class SubmitResponse { public SubmitStatus status; public string message; public int finalScore; }

    [Serializable]
    public class RankedStatus { public string dayKey; public bool attemptUsed; public bool submitted; public int score; public string status; }

    [Serializable]
    public class DailyRewardState
    {
        public int streakIndex; // 0..6, never resets (SG-14)
        public string lastClaimDayKey; public bool canClaimToday; public long nextResetUnixUtc; public Reward[] days;
    }

    [Serializable]
    public class SeasonState
    {
        public string seasonId; public string name; public int xp; public int tier; public int weekIndex; public string weekEventId;
        public long weekEndsUnixUtc; public long seasonEndsUnixUtc; public bool premium; public int bonusChests;
        public List<int> claimedFree = new List<int>(); public List<int> claimedPremium = new List<int>();
    }

    [Serializable] public class LeaderboardEntry { public int rank; public string playerId; public string name; public int score; public long submittedTicks; public bool isFriend; public bool isMe; }

    [Serializable]
    public class LeaderboardPage
    {
        public BoardType board; public string periodKey; public bool closed; public List<LeaderboardEntry> top = new List<LeaderboardEntry>();
        public LeaderboardEntry me; public int totalEntries; public bool meIsGuestHidden; public long fetchedUnixUtc;
    }

    [Serializable] public class AccountState
    {
        public string playerId; public string displayName; public bool linked; public string provider;
        public List<string> identities = new List<string>();      // F4: linked login providers
        public long pendingDeletionUnixUtc;                        // A5: >0 while in the 7-day grace period
        public long deletionWarningAtUnixUtc, guestPurgeAtUnixUtc; // C2: guest inactivity warning (day 335) and purge date (day 365)
        public string ageBand = "";                                // E3: "13to17" | "adult" (never a birth date)
        public bool consentSocial, consentName, consentPurchases;  // E4: parental consent scopes (13-17)
        public bool consentPending;
        public bool hideName;                                      // F5
    }

    public class BackendException : Exception
    {
        public string Code { get; } public int Status { get; }
        public BackendException(string m) : base(m) { Code = m; }
        public BackendException(string code, int status, string m = null) : base(m ?? code) { Code = code; Status = status; }
        public bool IsOffline => Code == "offline" || Code == "server time unavailable";
    }
}
