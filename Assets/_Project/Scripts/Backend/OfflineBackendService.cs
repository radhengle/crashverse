using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crashverse.Data;

namespace Crashverse.Backend
{
    /// <summary>
    /// integration/v2 release backend while the server stays offline: every call fails with BackendException("offline"), so the game
    /// runs fully local (campaign, profile, wallet, Daily Boss) and every online feature shows its offline / "Coming soon" state.
    /// It grants, validates and stores nothing (no rewards, scores, purchases or account data), unlike the dev-only LocalBackendService.
    /// Replace with the real server client by setting BackendLocator.Current before boot.
    /// </summary>
    public sealed class OfflineBackendService : IBackendService
    {
        public const string Code = "offline";
        public bool IsOnline => false;
        static Task<T> Off<T>() => Task.FromException<T>(new BackendException(Code));

        public Task<DateTime> GetServerTimeUtc() => Off<DateTime>();
        public Task<AccountState> GetAccount() => Off<AccountState>();
        public Task<AccountState> LinkAccount(LinkCredential credential) => Off<AccountState>();
        public Task<AuthResponse> AuthEmailRegister(string email, string password) => Off<AuthResponse>();
        public Task<AuthResponse> AuthEmailLogin(string email, string password) => Off<AuthResponse>();
        public Task<bool> RequestPasswordReset(string email) => Off<bool>();
        public Task<FeatureFlags> GetFeatureFlags() => Off<FeatureFlags>();
        public Task<ReauthResponse> Reauthenticate(LinkCredential credential) => Off<ReauthResponse>();
        public Task<DeleteAccountResponse> DeleteAccount(string reauthToken) => Off<DeleteAccountResponse>();
        public Task<StatusResponse> CancelAccountDeletion() => Off<StatusResponse>();
        public Task<KeepActiveResponse> KeepAccountActive() => Off<KeepActiveResponse>();
        public Task<AccountState> UnlinkAccount(AuthProvider provider, string reauthToken) => Off<AccountState>();
        public Task<AgeGateResponse> SubmitAgeGate(int birthYear, int birthMonth, string country) => Off<AgeGateResponse>();
        public Task<ConsentRequestResponse> RequestParentalConsent(string parentEmail) => Off<ConsentRequestResponse>();
        public Task<ConsentStatusResponse> GetParentalConsentStatus() => Off<ConsentStatusResponse>();
        public Task<VisibilityResponse> SetProfileVisibility(bool hideName) => Off<VisibilityResponse>();
        public Task<PurchaseVerifyResponse> VerifyPurchase(string sku, string receipt) => Off<PurchaseVerifyResponse>();
        public Task<AttemptToken> BeginAttempt(string levelId) => Off<AttemptToken>();
        public Task<ScoreResponse> SubmitScore(ScoreSubmission submission) => Off<ScoreResponse>();
        public Task<Wallet> GetWallet() => Off<Wallet>();
        public Task<bool> ConsumeExtraCharge() => Off<bool>();
        public Task<List<InboxItem>> GetInbox() => Off<List<InboxItem>>();
        public Task<Wallet> ClaimInbox(string itemId) => Off<Wallet>();
        public Task<DailyTitanConfig> GetDailyTitan() => Off<DailyTitanConfig>();
        public Task<RankedStatus> GetRankedStatus() => Off<RankedStatus>();
        public Task<RankedTicket> BeginRankedRun() => Off<RankedTicket>();
        public Task<RankedCheckpoint> ReportRankedCheckpoint(RankedCheckpoint cp) => Off<RankedCheckpoint>();
        public Task<SubmitResponse> FinalizeRanked(RankedJournal journal) => Off<SubmitResponse>();
        public Task<DailyRewardState> GetDailyRewardState() => Off<DailyRewardState>();
        public Task<Wallet> ClaimDailyReward() => Off<Wallet>();
        public Task<SeasonState> GetSeasonState() => Off<SeasonState>();
        public Task<SeasonState> ReportEventStars(string eventLevelId, int stars) => Off<SeasonState>();
        public Task<SeasonState> ClaimSeasonTier(int tier, bool premium) => Off<SeasonState>();
        public Task<int> SubmitEventScore(string eventLevelId, int stars) => Off<int>();
        public Task<CampaignSubmitResponse> SubmitCampaignResult(CampaignRunReport report) => Off<CampaignSubmitResponse>();
        public Task<LeaderboardPage> GetLeaderboard(BoardType board, BoardPeriod period, bool friendsOnly) => Off<LeaderboardPage>();
    }
}
