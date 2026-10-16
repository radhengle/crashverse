using System;
using System.Collections.Generic;
using Crashverse.Data;

namespace Crashverse.Backend
{
    public enum BoardType { DailyTitan = 0, WeeklyEvent = 1, Season = 2 }
    public enum BoardPeriod { Current = 0, Previous = 1 }
    public enum SubmitStatus { Accepted, RejectedInvalid, RejectedDuplicate, RefundedAsPractice, QueuedOffline }

    [Serializable] public class Wallet { public int coins; public int shards; public int extraCharges; public List<string> cosmetics = new List<string>(); public List<string> powerUps = new List<string>(); }

    [Serializable] public class InboxItem { public string id; public string source; public Reward reward; public long expiresUnixUtc; public bool claimed; }

    [Serializable] public class RankedTicket { public string token; public string dayKey; public long issuedUnixUtc; public int shotBudget; }

    [Serializable]
    public class RankedResult
    {
        public string token; public int damageScore; public int shotsLeft; public bool killed; public bool partial;
        public string signature; // HMAC in real backend; local stub uses a checksum
    }

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

    [Serializable] public class AccountState { public string playerId; public string displayName; public bool linked; public string provider; }

    public class BackendException : Exception { public BackendException(string m) : base(m) { } }
}
