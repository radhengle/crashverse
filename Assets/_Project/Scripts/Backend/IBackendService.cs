using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crashverse.Data;

namespace Crashverse.Backend
{
    /// <summary>
    /// Authoritative live-ops boundary. Everything time-gated, reward-granting or competitive goes through here
    /// (QA-UI-006, SG-20). LocalBackendService implements it with bundled JSON + a fake leaderboard; a real
    /// HTTP/Firebase service can be swapped in by implementing this interface (see BackendLocator).
    /// All methods throw BackendException("offline") when unreachable.
    /// </summary>
    public interface IBackendService
    {
        bool IsOnline { get; }
        Task<DateTime> GetServerTimeUtc();
        Task<AccountState> GetAccount();
        /// <summary>Link the current (guest) player to a provider using a provider-issued token (OAuth id token, or email session token).</summary>
        Task<AccountState> LinkAccount(AuthProvider provider, string credential);

        // Email auth. Passwords go to the server over TLS and are never stored client-side (not in saves, prefs or logs).
        Task<AuthSession> AuthEmailRegister(string email, string password);
        Task<AuthSession> AuthEmailLogin(string email, string password);
        Task<bool> RequestPasswordReset(string email);
        /// <summary>A3/F1: fresh provider re-auth (or email OTP token). Valid for 5 minutes for deletion/unlink.</summary>
        Task<bool> Reauthenticate(AuthProvider provider, string credential);
        /// <summary>A4: schedules deletion (7-day grace). Linked accounts without fresh re-auth → BackendException REAUTH_REQUIRED (401).</summary>
        Task<DeleteAccountResponse> DeleteAccount();
        /// <summary>A5: cancel during the grace period.</summary>
        Task<AccountState> CancelAccountDeletion();
        /// <summary>C2: "keep my account" after the inactivity warning.</summary>
        Task<AccountState> KeepAccountActive();
        /// <summary>F1: needs re-auth; the last login method cannot be removed (LAST_LOGIN 409).</summary>
        Task<AccountState> UnlinkAccount(AuthProvider provider);
        /// <summary>E3: band + country only; the server keeps the most restrictive band.</summary>
        Task<AccountState> SetAgeGate(string band, string country);
        /// <summary>E4: send the parent a consent link (verification happens on the web).</summary>
        Task<bool> RequestParentalConsent(string parentEmail);
        /// <summary>F5: hide the real name on every board.</summary>
        Task<AccountState> SetHideName(bool hide);
        /// <summary>E6: server receipt verification. 403 AGE_RESTRICTED for 13-17 without purchase consent in configured markets.</summary>
        Task<Wallet> VerifyPurchase(string sku, string receipt);
        /// <summary>Server feature flags (e.g. xLoginEnabled).</summary>
        Task<FeatureFlags> GetFeatureFlags();

        /// <summary>Security Review §4: single-use token per attempt; reports reference it.</summary>
        Task<AttemptToken> BeginAttempt(string levelId);
        Task<InputLogResponse> SubmitInputLog(InputLog log);

        Task<Wallet> GetWallet();
        Task<bool> ConsumeExtraCharge();
        Task<List<InboxItem>> GetInbox();
        Task<Wallet> ClaimInbox(string itemId);

        /// <summary>Daily config incl. any live override: release builds take overrides only from here (server), never a local file.</summary>
        Task<DailyTitanConfig> GetDailyTitan();
        Task<RankedStatus> GetRankedStatus();
        Task<RankedTicket> BeginRankedRun();
        /// <summary>SEC-01: report a settled shot; the server validates it against its own run state and returns it with a server receipt.</summary>
        Task<RankedCheckpoint> ReportRankedCheckpoint(RankedCheckpoint cp);
        /// <summary>Close a ranked run (normal end, or relaunch after the app was killed). The journal is sent as-is.</summary>
        Task<SubmitResponse> FinalizeRanked(RankedJournal journal);

        Task<DailyRewardState> GetDailyRewardState();
        Task<Wallet> ClaimDailyReward();

        Task<SeasonState> GetSeasonState();
        Task<SeasonState> ReportEventStars(string eventLevelId, int stars);
        Task<SeasonState> ClaimSeasonTier(int tier, bool premium);
        Task<int> SubmitEventScore(string eventLevelId, int stars);

        /// <summary>
        /// SC-13 / BUG-13 / SG-07: report a campaign win. The server validates it, recomputes the score, keeps the best per level,
        /// sums the board total itself and grants first-clear rewards from content. Idempotent per reportId.
        /// </summary>
        Task<CampaignSubmitResponse> SubmitCampaignResult(CampaignRunReport report);

        Task<LeaderboardPage> GetLeaderboard(BoardType board, BoardPeriod period, bool friendsOnly);
    }

    /// <summary>Swap point for a real server.</summary>
    public static class BackendLocator
    {
        public static IBackendService Current { get; set; }
    }
}
