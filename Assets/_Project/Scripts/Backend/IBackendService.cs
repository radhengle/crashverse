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
        /// <summary>POST /account/link with a provider credential (token + optional codeVerifier/redirectUri/nonce).</summary>
        Task<AccountState> LinkAccount(LinkCredential credential);

        // Email auth. Passwords go to the server over TLS and are never stored client-side (not in saves, prefs or logs).
        Task<AuthResponse> AuthEmailRegister(string email, string password);
        Task<AuthResponse> AuthEmailLogin(string email, string password);
        Task<bool> RequestPasswordReset(string email);
        /// <summary>Server feature flags (e.g. xLoginEnabled).</summary>
        Task<FeatureFlags> GetFeatureFlags();

        /// <summary>POST /auth/reauth {credential} → {reauthToken, expiresAt} (valid 5 min; A3/F1).</summary>
        Task<ReauthResponse> Reauthenticate(LinkCredential credential);
        /// <summary>POST /account/delete {reauthToken} → {status, purgeAt}. Linked accounts without a valid token → REAUTH_REQUIRED (401). Guests pass null.</summary>
        Task<DeleteAccountResponse> DeleteAccount(string reauthToken);
        /// <summary>POST /account/delete/cancel → {status} (A5 grace).</summary>
        Task<StatusResponse> CancelAccountDeletion();
        /// <summary>POST /account/keep-active → {lastActiveAt} (C2).</summary>
        Task<KeepActiveResponse> KeepAccountActive();
        /// <summary>F1: unlink with a fresh reauthToken; the last login method cannot be removed (LAST_LOGIN 409).</summary>
        Task<AccountState> UnlinkAccount(AuthProvider provider, string reauthToken);
        /// <summary>POST /agegate {birthYear, birthMonth, country} → {band (under13|13to15|16to17|adult, see AgeBands), requiresParentalConsent, ageToken}.
        /// Request must also carry deviceInstallId (N4): the token is bound to that install and single use. Pass it to guest/sign-in from the same install,
        /// or to POST /account/agegate {ageToken} to re-gate an older account with no band (N5). The server stores only band + country, never the birth year/month.</summary>
        Task<AgeGateResponse> SubmitAgeGate(int birthYear, int birthMonth, string country);
        /// <summary>POST /consent/parental/request {parentEmail} → {status, requestId}.</summary>
        Task<ConsentRequestResponse> RequestParentalConsent(string parentEmail);
        /// <summary>GET /consent/parental/status → {status}.</summary>
        Task<ConsentStatusResponse> GetParentalConsentStatus();
        /// <summary>POST /profile/visibility {hideName} → {hideName} (F5).</summary>
        Task<VisibilityResponse> SetProfileVisibility(bool hideName);
        /// <summary>Server receipt verification → {status: pending|verified|rejected, wallet, season}. 403 AGE_RESTRICTED for minors without purchase consent.</summary>
        Task<PurchaseVerifyResponse> VerifyPurchase(string sku, string receipt);

        /// <summary>Security Review §4: single-use token per attempt.</summary>
        Task<AttemptToken> BeginAttempt(string levelId);
        /// <summary>POST /score {attemptToken, inputs[], clientScoreBp, buildHash}.</summary>
        Task<ScoreResponse> SubmitScore(ScoreSubmission submission);

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
