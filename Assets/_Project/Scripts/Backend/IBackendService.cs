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
        Task<AccountState> LinkAccount(string provider);

        Task<Wallet> GetWallet();
        Task<bool> ConsumeExtraCharge();
        Task<List<InboxItem>> GetInbox();
        Task<Wallet> ClaimInbox(string itemId);
        Task<Wallet> GrantCampaignReward(string levelId, Reward[] rewards); // first clear only (SG-07)

        Task<DailyTitanConfig> GetDailyTitan();
        Task<RankedStatus> GetRankedStatus();
        Task<RankedTicket> BeginRankedRun();
        Task<SubmitResponse> SubmitRanked(RankedResult result);

        Task<DailyRewardState> GetDailyRewardState();
        Task<Wallet> ClaimDailyReward();

        Task<SeasonState> GetSeasonState();
        Task<SeasonState> ReportEventStars(string eventLevelId, int stars);
        Task<SeasonState> ClaimSeasonTier(int tier, bool premium);
        Task<int> SubmitEventScore(string eventLevelId, int stars);

        Task<LeaderboardPage> GetLeaderboard(BoardType board, BoardPeriod period, bool friendsOnly);
    }

    /// <summary>Swap point for a real server.</summary>
    public static class BackendLocator
    {
        public static IBackendService Current { get; set; }
    }
}
