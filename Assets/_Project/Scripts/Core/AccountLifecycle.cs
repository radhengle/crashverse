using System;
using System.Threading.Tasks;
using Crashverse.Backend;
using Crashverse.Meta;
using Crashverse.UI;

namespace Crashverse.Core
{
    /// <summary>Account-state hooks run whenever the backend is reachable (launch/resume): §E3, §A5, §A6, §C2.</summary>
    public static class AccountLifecycle
    {
        /// <summary>Provider re-auth hook (A3/F1): returns a fresh provider token or email-OTP token. Wired to provider SDKs later.
        /// TODO(Dev): Google/Apple/X SDK re-auth and email OTP UI. Dev builds return a fake token.</summary>
        public static Func<AuthProvider, Task<LinkCredential>> ReauthProvider = p =>
#if CRASHVERSE_DEV
            Task.FromResult(new LinkCredential { provider = p, token = "dev-reauth-" + p });
#else
            Task.FromResult<LinkCredential>(null);
#endif

        /// <summary>Provider re-auth → POST /auth/reauth → reauthToken (memory only, 5 min). Null if the user didn't complete it.</summary>
        public static async Task<string> FreshReauthToken(AuthProvider p)
        {
            var cred = await ReauthProvider(p);
            if (cred == null || string.IsNullOrEmpty(cred.token)) return null;
            return (await AppServices.Backend.Reauthenticate(cred)).reauthToken;
        }

        public static async Task OnConnected()
        {
            var b = AppServices.Backend; var r = AppServices.Router;
            try
            {
                if (AgeGateScreen.PendingDob.HasValue) // E3: DOB held in memory only since the gate; sent once, then dropped
                {
                    var d = AgeGateScreen.PendingDob.Value;
                    var ag = await b.SubmitAgeGate(d.year, d.month, AgeGateScreen.Country());
                    AgeGateScreen.OnServerBand(AgeGate.FromServer(ag.band), ag.requiresParentalConsent);
                }
                if (AccountDeletion.Pending)
                {
                    var res = await AccountDeletion.Flush(b); // A6: queued offline request goes out now
                    if (res == AccountDeletion.Result.WipedLocal) { GameBootstrap.RestartFirstRun(); r?.Toast("Your data was deleted from this device.", 4f); return; }
                    if (res == AccountDeletion.Result.Scheduled) { GameBootstrap.RestartFirstRun(); r?.Toast("Your account deletion request was sent.", 4f); return; }
                    if (res == AccountDeletion.Result.ReauthRequired) r?.Toast("Sign in again to finish deleting your account.", 4f);
                }
                var acc = AppServices.Account = await b.GetAccount();
                if (acc.pendingDeletionUnixUtc > 0) // A5: signing in during grace offers cancel
                    r?.Push(new ConfirmModal("Account scheduled for deletion",
                        $"Your account will be permanently deleted on {DateTimeOffset.FromUnixTimeSeconds(acc.pendingDeletionUnixUtc):d MMM yyyy} (UTC). Cancel the deletion to keep it.",
                        "Cancel deletion", async () => { await b.CancelAccountDeletion(); AppServices.Account = await b.GetAccount(); AccountDeletion.Cancel(); r.Toast("Deletion cancelled."); }));
                else if (acc.deletionWarningAtUnixUtc > 0) // C2
                    r?.Push(new ConfirmModal("Inactive guest account",
                        $"Guest accounts are deleted after 365 days without play. This account will be deleted on {DateTimeOffset.FromUnixTimeSeconds(acc.guestPurgeAtUnixUtc):d MMM yyyy} (UTC).",
                        "Keep my account", async () => { await b.KeepAccountActive(); AppServices.Account = await b.GetAccount(); }));
            }
            catch (BackendException) { /* offline: try again on next connection */ }
        }
    }
}
